using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using AgeOfEvolutions.Core.Diagnostics;

namespace AgeOfEvolutions.Core.Update;

/// <summary>
/// Update-Prüfung des Hauptmenüs (Nutzer 2026-10-18): die Version, die die
/// Installation nutzt, ist die AssemblyVersion (in Directory.Build.props
/// vergeben, z. B. 0.4.3), das Neueste ist das tag_name des letzten
/// öffentlichen GitHub-Release (gyurasan/AoE-Game). Läuft das Spiel älter
/// als das letzte Release, bietet das Menü das Update an; der Klick lädt die
/// Zip für diese Plattform und Variante (Mac M-Silicon → osx-arm64, Mac Intel
/// → osx-x64, Windows 64-Bit → win-x64-windowsdx oder win-x64-desktopgl, je
/// nachdem, welche Variante gerade läuft, Linux 64-Bit → linux-x64) und
/// ersetzt damit die laufende Installation AN ORT UND STELLE; eine unbekannte
/// Kombination (z. B. Windows ARM) landet auf der Release-Seite.
///
/// Ersetzen trotz laufender Exe (Nutzer 2026-10-20: das erste Update entpackte
/// die neue Version in einen Unterordner der alten, als DesktopGL statt
/// WindowsDX - die Verknüpfung startete danach weiter die alte Version, die
/// das Update erneut anbot): Windows verbietet, eine laufende Exe oder DLL zu
/// überschreiben oder zu löschen, erlaubt aber das Umbenennen (am 2026-10-20
/// an einer laufenden v0.4.4 nachgemessen: alle 231 Dateien umbenannt, auch
/// die 70 geladenen). Deshalb wandert jede alte Datei nach
/// &lt;Name&gt;.update-alt, die neue Datei an ihren Platz; die neue Version
/// startet aus demselben Ordner, die alte beendet sich, und beim nächsten
/// Start räumt CleanupAfterUpdate die *.update-alt-Dateien weg.
///
/// Testbar ohne Netzwerk und ohne Browser: FetchJson, OpenUrl, DownloadFile,
/// FetchText, StartApp und ExitApp sind austauschbare Hooks, InstallDir und
/// WindowsDX lassen sich umstellen (tools/spielablauf, Gruppe update).
///
/// VERTRAG:
/// - CheckLatest() wirft keine Ausnahme — jeder Fehler (Offline, JSON,
///   Timeout) wird zu Status.Unavailable;
/// - VersionLine() enthält immer die aktuelle Version;
/// - "aktuell" heißt: Neuestes &lt;= Aktuelles (damit bietet ein Dev-Build
///   1.0.0 kein Release-Update an);
/// - Statuswechsel und Statusanzeige sind gegen den Spielthread abgedeckt
///   (lock); Prüfung und Update laufen in einem Task.
/// </summary>
public sealed class UpdateService
{
    public const string Repo = "gyurasan/AoE-Game";
    public const string LatestApiUrl = $"https://api.github.com/repos/{Repo}/releases/latest";
    public const string ReleasePage = $"https://github.com/{Repo}/releases";

    /// <summary>Endung, unter der eine ersetzte Datei bis zum nächsten Start liegen bleibt.</summary>
    public const string OldSuffix = ".update-alt";

    public enum Status
    {
        /// <summary>Noch keine Prüfung gelaufen.</summary>
        Unknown,
        /// <summary>Prüfung läuft (im Hintergrund-Task).</summary>
        Checking,
        /// <summary>Neuestes &lt;= Aktuelles — nichts zu laden.</summary>
        UpToDate,
        /// <summary>Neuestes &gt; Aktuelles — Update anbieten.</summary>
        UpdateAvailable,
        /// <summary>Prüfung fehlgeschlagen (Offline, API, unlesbares JSON).</summary>
        Unavailable,
    }

    /// <summary>
    /// Fortschritt des Klick-Updates (Download → Prüfsumme → Entpacken →
    /// Ersetzen → Start). Idle, solange kein Update lief; am Ende Done oder Failed.
    /// </summary>
    public enum Phase
    {
        /// <summary>Kein Update aktiv.</summary>
        Idle,
        /// <summary>Zip wird heruntergeladen (ProgressDone/ProgressTotal-Bytes).</summary>
        Downloading,
        /// <summary>SHA256-Prüfsumme wird verglichen (wenn im Release vorhanden).</summary>
        Verifying,
        /// <summary>Zip wird in den Arbeitsordner update/ entpackt.</summary>
        Extracting,
        /// <summary>Die Dateien der Installation werden ersetzt.</summary>
        Installing,
        /// <summary>Neue Version wird gestartet.</summary>
        Launching,
        /// <summary>Installiert und gestartet — diese (alte) Version beendet sich.</summary>
        Done,
        /// <summary>Ein Schritt fehlgeschlagen (UpdatePhaseMessage nennt es).</summary>
        Failed,
    }

    // Test-Hooks (default: echte Welt); tools/spielablauf (Gruppe update)
    // ersetzt sie pro Fall und stellt sie danach wieder her
    public static Func<string, string> FetchJson { get; set; } = DefaultFetchJson;
    public static Action<string> OpenUrl { get; set; } = DefaultOpenUrl;

    /// <summary>Datei herunterladen (URL → Ziel-Pfad), ruft bei Fortschritt
    /// auf und liefert die Größe in Bytes. Fehler: Ausnahme.</summary>
    public static Func<string, string, Action<long, long>, long> DownloadFile { get; set; } = DefaultDownloadFile;

    /// <summary>Roh-Text aus einer URL holen (z. B. SHA256SUMS-Asset).</summary>
    public static Func<string, string> FetchText { get; set; } = DefaultFetchText;

    /// <summary>Installierte neue Version starten (Exe-Pfad, Workdir).
    /// Fehler: Ausnahme (wird im Update-Verlauf zu „Failed“).</summary>
    public static Action<string, string> StartApp { get; set; } = DefaultStartApp;

    /// <summary>
    /// Beendet diese (alte) Version, sobald die neue läuft (Phase Done). Null
    /// (Standard): das Hauptmenü beendet das Spiel regulär über Game.Exit();
    /// Tests setzen einen Zähler ein.
    /// </summary>
    public static Action ExitApp { get; set; }

    /// <summary>Der Ordner, den das Update ersetzt: die laufende Installation.</summary>
    public static string InstallDir { get; set; } = AppContext.BaseDirectory;

    /// <summary>
    /// Läuft die WindowsDX-Variante? Nur sie bringt SharpDX mit — DesktopGL
    /// hat stattdessen SDL2. Entscheidet unter Windows, welche Zip das Update lädt.
    /// </summary>
    public static bool WindowsDX { get; set; } =
        File.Exists(Path.Combine(AppContext.BaseDirectory, "SharpDX.Direct3D11.dll"));

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };
    // Downloads sind groß (170 MB+) — eigener Client; die Frist setzt
    // DefaultDownloadFile selbst, weil sie auch das Lesen des Inhalts abdecken muss
    private static readonly HttpClient _dl = new()
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    static UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AgeOfEvolutions-Updater");
        _dl.DefaultRequestHeaders.UserAgent.ParseAdd("AgeOfEvolutions-Updater");
    }

    private readonly object _lock = new();
    private int _inFlight;
    private Status _status = Status.Unknown;
    private Version _latest;

    // Update-Download/Installation (beginnt über BeginUpdate)
    private int _updating;
    private Phase _phase = Phase.Idle;
    private string _phaseMsg = "";
    private string _installedExe;
    private long _progressDone;
    private long _progressTotal;

    /// <summary>Status der letzten/laufenden Prüfung (thread-sicher lesbar).</summary>
    public Status State
    {
        get { lock (_lock) return _status; }
    }

    /// <summary>Die letzte gefundene Release-Version, oder null.</summary>
    public Version Latest
    {
        get { lock (_lock) return _latest; }
    }

    /// <summary>Phase des laufenden/letzten Klick-Updates (thread-sicher).</summary>
    public Phase UpdatePhase
    {
        get { lock (_lock) return _phase; }
    }

    /// <summary>Zusatz zur Phase (Fehlermeldung, Zielpfad …).</summary>
    public string UpdatePhaseMessage
    {
        get { lock (_lock) return _phaseMsg; }
    }

    /// <summary>Download-Fortschritt in Bytes (nur in Phase Downloading).</summary>
    public long ProgressDone
    {
        get { lock (_lock) return _progressDone; }
    }

    /// <summary>Download-Gesamtgröße in Bytes (0, wenn unbekannt).</summary>
    public long ProgressTotal
    {
        get { lock (_lock) return _progressTotal; }
    }

    /// <summary>Start-Pfad der installierten neuen Version (ab Phase Launching).</summary>
    public string InstalledExe
    {
        get { lock (_lock) return _installedExe; }
    }

    /// <summary>
    /// Läuft ein Download/Update gerade? </summary>
    public bool UpdateActive
        => Volatile.Read(ref _updating) == 1;

    /// <summary>
    /// Lässt sich das Update jetzt starten? Ein Angebot liegt vor
    /// (Status.UpdateAvailable), es läuft keines und das letzte ist nicht
    /// schon fertig (Phase Idle oder Failed).
    /// </summary>
    public bool CanStartUpdate
    {
        get
        {
            if (UpdateActive)
                return false;
            lock (_lock)
                return _status == Status.UpdateAvailable && (_phase == Phase.Idle || _phase == Phase.Failed);
        }
    }

    /// <summary>
    /// Die Version dieser Installation aus der AssemblyVersion (drei Ziffern:
    /// 0.4.3). Quelle: `Directory.Build.props` (ohne Angabe würde MSBuild auf
    /// 1.0.0.0 fallen und das Menü „Version 1.0.0“ zeigen; Release-Skripte
    /// überschreiben beim Publish per -p:Version=).
    /// </summary>
    public string CurrentVersionText { get; } = VersionText();

    private static string VersionText()
    {
        var v = typeof(UpdateService).Assembly.GetName().Version;
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>
    /// Synchroner Update-Check: holt das letzte Release (über FetchJson),
    /// parst das tag_name und setzt den Status. Wirft nie — Fehler werden
    /// zu Status.Unavailable (VERTRAG oben).
    /// </summary>
    public void CheckLatest()
    {
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
            return; // eine Prüfung läuft gerade — zweiter Aufruf wartet nicht
        lock (_lock) _status = Status.Checking;
        try
        {
            string json = FetchJson(LatestApiUrl);
            var latest = ParseLatestTag(json);
            lock (_lock)
            {
                _latest = latest;
                _status = latest > CurrentVersionValue
                    ? Status.UpdateAvailable
                    : Status.UpToDate;
            }
        }
        catch
        {
            lock (_lock) _status = Status.Unavailable;
        }
        finally
        {
            Interlocked.Exchange(ref _inFlight, 0);
        }
    }

    /// <summary>Die aktuelle Version als System.Version (für Vergleiche).</summary>
    public Version CurrentVersionValue { get; } = ParseCurrent();

    private static Version ParseCurrent()
    {
        var v = typeof(UpdateService).Assembly.GetName().Version;
        return v is null ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, v.Build);
    }

    /// <summary>Browser-Fallback: öffnet die direkte Download-URL (Plattform-Zip)
    /// des neuen Releases über OpenUrl; unbekanntes OS/Arch (kein Asset) →
    /// die Release-Seite. Ohne gefundene Version passiert nichts.</summary>
    public void OpenUpdate()
    {
        Version latest;
        lock (_lock) latest = _latest;
        if (latest is null)
            return;
        OpenUrl(UpdateUrl(latest));
    }

    /// <summary>
    /// Klick-Update starten (asynchron, blockiert nie). Ohne Plattform-Zip
    /// (PlatformZip null) oder mit nicht beschreibbarem InstallDir öffnet
    /// OpenUpdate stattdessen die Download-Seite im Browser, UpdatePhaseMessage
    /// nennt den Grund und die Phase bleibt, wie sie war. Sonst Phase
    /// Downloading und RunUpdate im Hintergrund-Task. Läuft bereits ein
    /// Update, ist der zweite Aufruf wirkungslos.
    /// VERTRAG, der Reihe nach: latest (unter _lock gelesen) null → false.
    /// PlatformZip(latest) null → OpenUpdate(); unter _lock _phaseMsg = "kein Paket für
    /// diesen Computer, Download-Seite geöffnet"; false. !Writable(InstallDir) →
    /// OpenUpdate(); unter _lock _phaseMsg = "Spielordner schreibgeschützt, Download-Seite
    /// geöffnet"; false. Interlocked.CompareExchange(ref _updating, 1, 0) != 0 → true
    /// (läuft schon). Sonst SetPhase(Downloading, ""); unter _lock _progressDone = 0,
    /// _progressTotal = 0, _installedExe = null; Task.Run(() =&gt; RunUpdate(latest)); true.
    /// </summary>
    /// <returns>true, wenn ein Update im Hintergrund begonnen hat (oder schon lief);
    /// false ohne Angebot oder beim Browser-Fallback.</returns>
    public bool BeginUpdate()
    {
        Version latest;
        lock (_lock) latest = _latest;
        if (latest is null)
            return false;
        string asset = PlatformZip(latest);
        if (asset is null)
        {
            OpenUpdate();
            lock (_lock) _phaseMsg = "kein Paket für diesen Computer, Download-Seite geöffnet";
            return false;
        }
        if (!Writable(InstallDir))
        {
            OpenUpdate();
            lock (_lock) _phaseMsg = "Spielordner schreibgeschützt, Download-Seite geöffnet";
            return false;
        }
        if (Interlocked.CompareExchange(ref _updating, 1, 0) != 0)
            return true; // läuft schon
        SetPhase(Phase.Downloading, "");
        lock (_lock)
        {
            _progressDone = 0;
            _progressTotal = 0;
            _installedExe = null;
        }
        System.Threading.Tasks.Task.Run(() => RunUpdate(latest));
        return true;
    }

    /// <summary>
    /// Der eigentliche Ablauf (läuft im Hintergrund-Task). Arbeitsordner ist
    /// &lt;InstallDir&gt;/update — auf demselben Laufwerk wie die Installation,
    /// damit das Ersetzen nur umbenennt statt zu kopieren.
    /// VERTRAG: asset = PlatformZip(latest), installDir = InstallDir (einmal gelesen),
    /// work = installDir/update, tmp = work/&lt;asset&gt;.part, staging = work/neu. In einem try:
    /// 1) json = FetchJson(LatestApiUrl); url = AssetUrl(json, asset); null → throw
    ///    new Exception($"Asset {asset} fehlt im Release"). SetPhase(Downloading, asset);
    ///    Directory.CreateDirectory(work); bytes = DownloadFile(url, tmp, (d, t) =&gt; unter
    ///    _lock _progressDone = d, _progressTotal = t); bytes &lt;= 0 oder tmp fehlt → throw
    ///    new Exception("Download fehlgeschlagen").
    /// 2) SetPhase(Verifying, ""); expected = ExpectedSha(json, asset); ist expected nicht
    ///    null und ungleich Sha256File(tmp) (OrdinalIgnoreCase) → throw new
    ///    Exception("SHA256-Prüfsumme stimmt nicht überein").
    /// 3) SetPhase(Extracting, ""); staging löschen, falls da (rekursiv); ExtractZip(tmp,
    ///    staging); root = StagedRoot(staging); fehlt root/AppHostName → throw new
    ///    Exception($"{AppHostName} fehlt in der Zip").
    /// 4) SetPhase(Installing, ""); ReplaceInstallation(root, installDir).
    /// 5) exe = installDir/AppHostName; SetPhase(Launching, exe); unter _lock
    ///    _installedExe = exe; StartApp(exe, installDir); SetPhase(Done, exe).
    /// catch (Exception e): GameLog.Error(e, "Update"); SetPhase(Failed, e.Message).
    /// finally: _updating auf 0 (Interlocked.Exchange); tmp löschen, falls da, und
    /// staging rekursiv löschen, falls da — beides je in eigenem try/catch, das schluckt.
    /// </summary>
    private void RunUpdate(Version latest)
    {
        string asset = PlatformZip(latest);
        string installDir = InstallDir;
        string work = Path.Combine(installDir, "update");
        string tmp = Path.Combine(work, asset + ".part");
        string staging = Path.Combine(work, "neu");
        try
        {
            // 1) Zip herunterladen
            string json = FetchJson(LatestApiUrl);
            string url = AssetUrl(json, asset);
            if (url is null)
                throw new Exception($"Asset {asset} fehlt im Release");
            SetPhase(Phase.Downloading, asset);
            Directory.CreateDirectory(work);
            long bytes = DownloadFile(url, tmp, (d, t) =>
            {
                lock (_lock)
                {
                    _progressDone = d;
                    _progressTotal = t;
                }
            });
            if (bytes <= 0 || !File.Exists(tmp))
                throw new Exception("Download fehlgeschlagen");

            // 2) Prüfsumme vergleichen (wenn im Release vorhanden)
            SetPhase(Phase.Verifying, "");
            string expected = ExpectedSha(json, asset);
            string actual = Sha256File(tmp);
            if (expected is not null && !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new Exception("SHA256-Prüfsumme stimmt nicht überein");

            // 3) Entpacken und Top-Ordner bestimmen
            SetPhase(Phase.Extracting, "");
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            ExtractZip(tmp, staging);
            string root = StagedRoot(staging);
            if (!File.Exists(Path.Combine(root, AppHostName)))
                throw new Exception($"{AppHostName} fehlt in der Zip");

            // 4) Installation an Ort und Stelle ersetzen
            SetPhase(Phase.Installing, "");
            ReplaceInstallation(root, installDir);

            // 5) Neue Version starten, diese Version schließt sich
            string exe = Path.Combine(installDir, AppHostName);
            SetPhase(Phase.Launching, exe);
            lock (_lock) _installedExe = exe;
            StartApp(exe, installDir);
            SetPhase(Phase.Done, exe);
        }
        catch (Exception e)
        {
            GameLog.Error(e, "Update");
            SetPhase(Phase.Failed, e.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _updating, 0);
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
        }
    }

    private void SetPhase(Phase p, string msg)
    {
        lock (_lock) { _phase = p; _phaseMsg = msg ?? ""; }
    }

    /// <summary>
    /// Der Ordner mit den Programmdateien in einer entpackten Zip: liegen in
    /// <paramref name="extractedDir"/> keine Dateien und genau ein Unterordner
    /// (der Top-Ordner der Release-Zips, z. B. win-x64-windowsdx/), dann dieser,
    /// sonst <paramref name="extractedDir"/> selbst.
    /// </summary>
    public static string StagedRoot(string extractedDir)
    {
        // Top-Ordner der Release-Zips (z. B. win-x64-windowsdx/): genau ein
        // Unterordner und keine Dateien direkt in extractedDir
        var dirs = Directory.GetDirectories(extractedDir);
        if (dirs.Length == 1 && Directory.GetFiles(extractedDir).Length == 0)
            return dirs[0];
        return extractedDir;
    }

    /// <summary>
    /// Ersetzt die Installation durch die Dateien aus <paramref name="sourceDir"/>
    /// (gleiche relative Pfade, Unterordner wie Content/ eingeschlossen) und
    /// leert dabei sourceDir. Je Datei: eine vorhandene Zieldatei wird zu
    /// BackupName(Ziel) umbenannt (Umbenennen klappt auch bei laufender Exe/DLL),
    /// dann die neue Datei an ihren Platz verschoben; fehlende Zielordner werden
    /// angelegt. Dateien der Installation, die die neue Version nicht mehr hat,
    /// bleiben liegen. Scheitert ein Schritt, wird alles bis dahin Getane
    /// rückgängig gemacht (neue Dateien zurück nach sourceDir, Sicherungen an
    /// ihren alten Namen) und die Ausnahme weitergeworfen — die Installation
    /// bleibt dann die alte.
    /// </summary>
    public static void ReplaceInstallation(string sourceDir, string installDir)
    {
        // Zuerst ALLE Quelldateien holen, bevor etwas verschoben wird
        string[] sources = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        var done = new List<(string Source, string Target, string Backup)>();
        try
        {
            foreach (string src in sources)
            {
                string target = Path.Combine(installDir, Path.GetRelativePath(sourceDir, src));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                string backup = null;
                if (File.Exists(target))
                {
                    // Umbenennen klappt auch bei laufender Exe/DLL —
                    // weder Überschreiben noch Löschen geht
                    backup = BackupName(target);
                    File.Move(target, backup);
                }
                // Sicherung vor dem zweiten Verschieben notieren, damit sie
                // auch zurückkommt, wenn es jetzt scheitert
                done.Add((src, target, backup));
                File.Move(src, target);
            }
        }
        catch
        {
            // Alles bis dahin rückgängig machen (je Schritt eigenes try/catch)
            for (int i = done.Count - 1; i >= 0; i--)
            {
                var (src, target, backup) = done[i];
                try
                {
                    if (File.Exists(target) && !File.Exists(src))
                        File.Move(target, src);
                    if (backup is not null && File.Exists(backup))
                        File.Move(backup, target);
                }
                catch
                {
                    // Ein Rückgängigmachen-Schritt scheitert: weitermachen
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Freier Sicherungsname für <paramref name="path"/>: path + OldSuffix; liegt
    /// dort schon eine Datei (Rest eines früheren Updates), wird sie gelöscht,
    /// und klappt das nicht (noch geladen), path + OldSuffix + 1, 2, … bis zum
    /// ersten freien Namen.
    /// </summary>
    public static string BackupName(string path)
    {
        string name = path + OldSuffix;
        for (int n = 1; File.Exists(name); n++)
        {
            try
            {
                File.Delete(name);
            }
            catch
            {
                // noch geladen: nächsten Namen versuchen
                name = path + OldSuffix + n;
            }
        }
        return name;
    }

    /// <summary>
    /// Räumt nach einem Update auf: löscht in <paramref name="installDir"/> (mit
    /// Unterordnern) alle Dateien, deren Name OldSuffix enthält, und beim ersten
    /// Versuch den Arbeitsordner update/ (Rest eines abgebrochenen Laufs - nur
    /// dann, denn danach könnte schon ein neues Update darin laden). Noch
    /// gesperrte Dateien (die alte Version beendet sich gerade) werden bis zu
    /// <paramref name="attempts"/>-mal im Abstand von <paramref name="delayMs"/> ms
    /// erneut versucht. Wirft nie.
    /// </summary>
    /// <returns>Anzahl der Sicherungsdateien, die danach noch liegen.</returns>
    public static int CleanupAfterUpdate(string installDir, int attempts = 20, int delayMs = 500)
    {
        int left = 0;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0)
                Thread.Sleep(delayMs);
            left = 0;
            // Arbeitsordner nur beim ersten Versuch löschen — danach könnte
            // schon ein neues Update darin laden
            if (attempt == 0)
            {
                try
                {
                    string work = Path.Combine(installDir, "update");
                    if (Directory.Exists(work))
                        Directory.Delete(work, true);
                }
                catch
                {
                    // gesperrt - beim nächsten Start wieder (der Arbeitsordner wird nur
                    // beim ersten Versuch gelöscht, ein späterer fasst ihn nicht mehr an)
                }
            }
            try
            {
                foreach (string f in Directory.EnumerateFiles(installDir, "*" + OldSuffix + "*", SearchOption.AllDirectories).ToList())
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch
                    {
                        left++;
                    }
                }
            }
            catch
            {
                // Ordner fehlt: nichts zu räumen
            }
            if (left == 0)
                break;
        }
        return left;
    }

    /// <summary>browsable_download_url eines Assets aus der Releases/latest-Antwort,
    /// sonst null (Feld "assets", Einträge mit "name").</summary>
    public static string AssetUrl(string latestJson, string assetName)
    {
        using var doc = JsonDocument.Parse(latestJson);
        if (!doc.RootElement.TryGetProperty("assets", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var a in arr.EnumerateArray())
        {
            if (a.TryGetProperty("name", out var n) && n.GetString() == assetName
                && a.TryGetProperty("browser_download_url", out var u)
                && u.ValueKind == JsonValueKind.String)
                return u.GetString();
        }
        return null;
    }

    /// <summary>Erwartete SHA256 (hex, klein) für <paramref name="assetName"/> aus
    /// dem SHA256SUMS-Asset des Releases; null, wenn das Asset fehlt, unlesbar
    /// ist oder keine Zeile für die Datei trägt (Prüfung wird übersprungen,
    /// nicht abgebrochen).</summary>
    private static string ExpectedSha(string latestJson, string assetName)
    {
        Version v;
        try { v = ParseLatestTag(latestJson); }
        catch { return null; }
        string sumsUrl = AssetUrl(latestJson, SumsName(v));
        if (sumsUrl is null)
            return null;
        string text;
        try { text = FetchText(sumsUrl); }
        catch { return null; }
        foreach (var line in text.Split('\n'))
        {
            var parts = line.Trim().Split(' ', 2);
            if (parts.Length == 2 && parts[1].Trim() == assetName)
                return parts[0].Trim();
        }
        return null;
    }

    /// <summary>Name des Prüfsummen-Assets (make_release_zips.py schreibt es so).</summary>
    public static string SumsName(Version v) => $"SHA256SUMS_{Tag(v)}.md";

    /// <summary>SHA256 (hex, klein) einer Datei.</summary>
    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static bool Writable(string dir)
    {
        try
        {
            string probe = Path.Combine(dir, $".probe-{Environment.ProcessId}");
            using (File.Create(probe)) { }
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Zip entpacken (Datei-nach-Datei, mit Protection gegen "../"-Einträge
    /// außerhalb des Zielverzeichnisses). Defekte Zips werfen — der Caller
    /// meldet es als Failed. WICHTIG (.NET 10): `entry.Name` enthält nur
    /// noch den Dateinamen OHNE Ordner; der volle Eintragspfad steht in
    /// `entry.FullName` — ohne ihn wäre der Top-Ordner der Release-Zips
    /// (z. B. win-x64-desktopgl/) weg und alles läge flach im Ziel.
    /// ExtractToFile übernimmt unter macOS/Linux die Unix-Rechte aus der Zip
    /// (make_release_zips.py setzt 0755 auf den Apphost) — ein Kopieren des
    /// Streams verlöre das Ausführungsrecht, und die neue Version startete nicht.
    /// </summary>
    public static void ExtractZip(string zipPath, string destDir)
    {
        string dest = Path.GetFullPath(destDir).TrimEnd(Path.DirectorySeparatorChar)
                      .TrimEnd(Path.AltDirectorySeparatorChar)
                      + Path.DirectorySeparatorChar;
        using var zf = ZipFile.OpenRead(zipPath);
        foreach (var entry in zf.Entries)
        {
            string name = entry.FullName;
            if (string.IsNullOrEmpty(name) || name.EndsWith('/') || name.Contains('\\'))
                continue; // reiner Verzeichniseintrag oder fremder Trenner
            string full = Path.GetFullPath(Path.Combine(dest, name));
            if (!full.StartsWith(dest, StringComparison.Ordinal))
                continue; // würde aus dem Ziel entkommen — ignorieren
            string dirName = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dirName))
                Directory.CreateDirectory(dirName);
            // ExtractToFile statt Stream-Kopie: übernimmt unter macOS/Linux das
            // Ausführungsrecht aus der Zip, eine Kopie des Streams verlöre es
            entry.ExtractToFile(full, true);
        }
    }

    /// <summary>Dateiname des Apphosts dieser Plattform.</summary>
    public static string AppHostName
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "AgeOfEvolutions.exe" : "AgeOfEvolutions";

    /// <summary>
    /// Die Zip-Datei für diese Plattform (Name im GitHub-Release), oder null,
    /// wenn es für OS+Architektur kein Release-Asset gibt (z. B. Windows
    /// ARM, Linux ARM — die 5 RIDs: win-x64-desktopgl, win-x64-windowsdx,
    /// osx-arm64, osx-x64, linux-x64). Windows 64-Bit bekommt dieselbe
    /// Variante, die gerade läuft (WindowsDX).
    /// </summary>
    public static string PlatformZip(Version v)
    {
        string ver = $"{v.Major}.{v.Minor}.{v.Build}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? (WindowsDX
                    ? $"AoE_Evolutions_v{ver}_win-x64-windowsdx.zip"
                    : $"AoE_Evolutions_v{ver}_win-x64-desktopgl.zip")
                : null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => $"AoE_Evolutions_v{ver}_osx-arm64.zip",
                Architecture.X64 => $"AoE_Evolutions_v{ver}_osx-x64.zip",
                _ => null,
            };
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? $"AoE_Evolutions_v{ver}_linux-x64.zip"
                : null;
        return null;
    }

    /// <summary>Direkte Download-URL der Plattform-Zip, sonst Release-Seite.</summary>
    public static string UpdateUrl(Version v)
    {
        string asset = PlatformZip(v);
        string ver = $"{v.Major}.{v.Minor}.{v.Build}";
        return asset is null
            ? $"https://github.com/{Repo}/releases/tag/v{ver}"
            : $"https://github.com/{Repo}/releases/download/v{ver}/{asset}";
    }

    /// <summary>
    /// Text des Menüeintrags „Update“ — kurz genug für eine Menüplatte —, oder
    /// null, solange es nichts anzubieten gibt (Status nicht UpdateAvailable;
    /// dann zeigt das Menü den Eintrag nicht). Je Phase: Idle „Update auf
    /// v0.4.5“, Downloading „Lade v0.4.5 … 37 %“ (ohne bekannte Größe „… 12 MB“),
    /// Verifying „Prüfe v0.4.5 …“, Extracting „Entpacke v0.4.5 …“, Installing
    /// „Installiere v0.4.5 …“, Launching und Done „Starte v0.4.5 …“, Failed
    /// „Update erneut versuchen“.
    /// </summary>
    public string EntryText()
    {
        Status s;
        Phase phase;
        Version latest;
        long done, total;
        lock (_lock)
        {
            s = _status;
            phase = _phase;
            latest = _latest;
            done = _progressDone;
            total = _progressTotal;
        }
        if (s != Status.UpdateAvailable)
            return null;
        string neu = latest is null ? "?" : Tag(latest);
        return phase switch
        {
            Phase.Downloading => total > 0
                ? $"Lade {neu} … {Math.Min(100, done * 100 / total)} %"
                : $"Lade {neu} … {Mb(done)} MB",
            Phase.Verifying => $"Prüfe {neu} …",
            Phase.Extracting => $"Entpacke {neu} …",
            Phase.Installing => $"Installiere {neu} …",
            Phase.Launching or Phase.Done => $"Starte {neu} …",
            Phase.Failed => "Update erneut versuchen",
            _ => $"Update auf {neu}",
        };
    }

    /// <summary>
    /// Die kleine Versionszeile unter den Menüeinträgen: immer „Version
    /// &lt;aktuell&gt;“, danach der Zustand — Checking „– suche Updates …“,
    /// UpToDate „– aktuell“, Unavailable „– Update-Prüfung nicht möglich“;
    /// mit Angebot: Failed „– Update fehlgeschlagen: &lt;Grund&gt;“, laufendes
    /// Update „– Update läuft, bitte warten“, Done „– v0.4.5 startet, diese
    /// Version schließt sich“, sonst mit Meldung (Browser-Fallback) „– &lt;Meldung&gt;“,
    /// ohne „– v0.4.5 ist verfügbar“.
    /// </summary>
    public string VersionLine()
    {
        Status s;
        Phase phase;
        Version latest;
        string msg;
        lock (_lock)
        {
            s = _status;
            phase = _phase;
            latest = _latest;
            msg = _phaseMsg;
        }
        string cur = $"Version {CurrentVersionText}";
        string neu = latest is null ? "?" : Tag(latest);
        return s switch
        {
            Status.UpdateAvailable => phase switch
            {
                Phase.Failed => $"{cur} – Update fehlgeschlagen: {msg}",
                Phase.Done => $"{cur} – {neu} startet, diese Version schließt sich",
                Phase.Idle => string.IsNullOrEmpty(msg)
                    ? $"{cur} – {neu} ist verfügbar"
                    : $"{cur} – {msg}",
                _ => $"{cur} – Update läuft, bitte warten",
            },
            Status.UpToDate => $"{cur} – aktuell",
            Status.Checking => $"{cur} – suche Updates …",
            Status.Unavailable => $"{cur} – Update-Prüfung nicht möglich",
            _ => cur,
        };
    }

    private static string Mb(long bytes)
        => (bytes / 1048576.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Release-Tag aus der API-Antwort: "v0.4.4" → Version 0.4.4;
    /// Prädikate wie "-rc.1" werden abgeschnitten. Wirft bei unlesbarem
    /// JSON oder fehlendem tag_name (Caller fängt → Unavailable).
    /// </summary>
    public static Version ParseLatestTag(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tag_name", out var tag)
            || tag.ValueKind != JsonValueKind.String)
            throw new FormatException("Antwort hat kein tag_name");
        string t = tag.GetString().Trim();
        while (t.Length > 0 && (t[0] == 'v' || t[0] == 'V'))
            t = t[1..];
        int cut = t.Length;
        for (int i = 0; i < t.Length; i++)
        {
            if (!char.IsDigit(t[i]) && t[i] != '.')
            {
                cut = i;
                break;
            }
        }
        if (!Version.TryParse(t.Substring(0, cut), out var v))
            throw new FormatException($"Release-Version unlesbar: {tag.GetString()}");
        return v;
    }

    /// <summary>Label einer Release-Version für die Anzeige: v0.4.4.</summary>
    public static string Tag(Version v)
        => $"v{v.Major}.{v.Minor}.{v.Build}";

    private static string DefaultFetchJson(string url)
    {
        using var resp = _http.GetAsync(url).GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();
        return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    private static void DefaultOpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Kein Browser erreichbar: die Statuszeile bleibt lesbar,
            // die URL steht in den Releasenotes/README
        }
    }

    /// <summary>Download mit Fortschritts-Meldungen (~1 MB pro Meldung);
    /// schreibt erst ins Ziel, wenn der Stream sauber durchgelaufen ist
    /// (.inflight → Move, damit ein Abbruch keine halbe Datei hinterlässt).
    /// ResponseHeadersRead ist nötig: ohne liest GetAsync die ganze Zip in den
    /// Speicher, bevor die Schleife beginnt, und der Fortschritt stünde bis
    /// zum Schluss auf 0. Die 15-Minuten-Frist deckt so Kopf und Inhalt ab.</summary>
    private static long DefaultDownloadFile(string url, string target, Action<long, long> progress)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        using var resp = _dl.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token).GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();
        string tmp = target + ".inflight";
        try
        {
            using var src = resp.Content.ReadAsStreamAsync(cts.Token).GetAwaiter().GetResult();
            using var dst = File.Create(tmp);
            long done = 0;
            long total = resp.Content.Headers.ContentLength ?? 0;
            long lastReport = 0;
            var buf = new byte[1 << 20];
            int n;
            progress?.Invoke(0, total);
            while ((n = src.ReadAsync(buf, 0, buf.Length, cts.Token).GetAwaiter().GetResult()) > 0)
            {
                dst.Write(buf, 0, n);
                done += n;
                if (progress is not null && done - lastReport >= buf.Length)
                {
                    progress(done, total);
                    lastReport = done;
                }
            }
            if (progress is not null)
                progress(done, done > 0 ? done : total);
            dst.Flush();
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            throw;
        }
        File.Move(tmp, target, true);
        return new FileInfo(target).Length;
    }

    private static string DefaultFetchText(string url)
    {
        using var resp = _http.GetAsync(url).GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();
        return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    private static void DefaultStartApp(string exe, string workdir)
    {
        Process.Start(new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            WorkingDirectory = workdir,
        });
    }
}
