using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace AgeOfEvolutions.Core.Update;

/// <summary>
/// Update-Prüfung des Hauptmenüs (Nutzer 2026-10-18): die Version, die die
/// Installation nutzt, ist die AssemblyVersion (in Directory.Build.props
/// vergeben, z. B. 0.4.3), das Neueste ist das tag_name des letzten
/// öffentlichen GitHub-Release (gyurasan/AoE-Game). Läuft das Spiel älter
/// als das letzte Release, bietet das Menü das Update an; der Klick öffnet
/// die DIREKTE Download-URL der Zip für diese Plattform (Mac M-Silicon →
/// osx-arm64, Mac Intel → osx-x64, Windows 64-Bit → win-x64-desktopgl,
/// Linux 64-Bit → linux-x64) im Browser; eine unbekannte Kombination
/// (z. B. Windows ARM) landet auf der Release-Seite.
///
/// Testbar ohne Netzwerk und ohne Browser: FetchJson und OpenUrl sind
/// austauschbare Hooks (tools/spielablauf, Gruppe update); standardmäßig
/// echte Http-Abfrage (5 s Timeout, GitHub-Release-API) und Browser-Start.
///
/// VERTRAG:
/// - CheckLatest() wirft keine Ausnahme — jeder Fehler (Offline, JSON,
///   Timeout) wird zu Status.Unavailable;
/// - StatusText() enthält immer die aktuelle Version;
/// - "aktuell" heißt: Neuestes &lt;= Aktuelles (damit bietet ein Dev-Build
///   1.0.0 kein Release-Update an);
/// - Statuswechsel und Statusanzeige sind gegen den Spielthread abgedeckt
///   (lock); die Prüfung läuft in einem Task (StartCheck).
/// </summary>
public sealed class UpdateService
{
    public const string Repo = "gyurasan/AoE-Game";
    public const string LatestApiUrl = $"https://api.github.com/repos/{Repo}/releases/latest";
    public const string ReleasePage = $"https://github.com/{Repo}/releases";

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
    /// Start). Idle, solange kein Update lief; am Ende Done oder Failed.
    /// </summary>
    public enum Phase
    {
        /// <summary>Kein Update aktiv.</summary>
        Idle,
        /// <summary>Zip wird heruntergeladen (ProgressDone/ProgressTotal-Bytes).</summary>
        Downloading,
        /// <summary>SHA256-Prüfsumme wird verglichen (wenn im Release vorhanden).</summary>
        Verifying,
        /// <summary>Zip wird in das Installationsverzeichnis entpackt.</summary>
        Extracting,
        /// <summary>Neue Version wird gestartet.</summary>
        Launching,
        /// <summary>Installiert und gestartet.</summary>
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

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };
    // Downloads sind groß (170 MB+) — eigener Client mit langem Timeout
    private static readonly HttpClient _dl = new()
    {
        Timeout = TimeSpan.FromMinutes(15),
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

    /// <summary>Start-Pfad der installierten neuen Version (Phase Done/Failed).</summary>
    public string InstalledExe
    {
        get { lock (_lock) return _installedExe; }
    }

    /// <summary>
    /// Läuft ein Download/Update gerade? </summary>
    public bool UpdateActive
        => System.Threading.Volatile.Read(ref _updating) == 1;

    /// <summary>
    /// Schließt die alte (aktuell laufende) Version — die neue Version läuft
    /// bereits aus dem neuen Verzeichnis. Default beendet den Prozess; Tests
    /// ersetzen den Hook.
    /// </summary>
    public static Action ExitApp { get; set; } = () => System.Environment.Exit(0);

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
        if (System.Threading.Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
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
            System.Threading.Interlocked.Exchange(ref _inFlight, 0);
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
    /// Klick-Update starten (asynchron, blockiert nie): holt die Asset-URL
    /// des passenden Plattform-Zips aus dem Release, lädt die Zip in ein
    /// Arbeitsverzeichnis, vergleicht die SHA256 aus dem SHA256SUMS-Asset
    /// (fehlt es, wird die Prüfung übersprungen; stimmt sie NICHT, Abbruch),
    /// entpackt nach InstallRoot()/AoE_Evolutions_v&lt;tag&gt;/ und startet die
    /// neue Version. Läuft kein passendes Release-Asset (OS+Arch), öffnet das
    /// der Browser die Release-Seite (OpenUpdate). Läuft bereits ein Update,
    /// ist der zweite Aufruf wirkungslos.
    /// </summary>
    /// <returns>true, wenn ein lokaler Download/Install begonnen (oder bereits lief);
    /// false, wenn es nur den Browser-Fallback gegeben hat.</returns>
    public bool BeginUpdate()
    {
        Version latest;
        lock (_lock) latest = _latest;
        if (latest is null)
            return false;
        if (PlatformZip(latest) is null)
        {
            // Kein Asset für diese OS+Arch — Browser zur Release-Seite
            OpenUpdate();
            lock (_lock) _phaseMsg = "kein Release für diese Plattform — Seite geöffnet";
            return false;
        }
        if (System.Threading.Interlocked.CompareExchange(ref _updating, 1, 0) != 0)
            return true; // läuft bereits
        SetPhase(Phase.Downloading, "");
        lock (_lock) { _progressDone = 0; _progressTotal = 0; }
        System.Threading.Tasks.Task.Run(() => RunUpdate(latest));
        return true;
    }

    /// <summary>Der eigentliche Ablauf (läuft im Hintergrund-Task).</summary>
    private void RunUpdate(Version latest)
    {
        string tag = Tag(latest);
        string asset = PlatformZip(latest);
        string tmp = null;
        try
        {
            // 1) Asset-URL aus dem Release (gleiche API-Antwort wie CheckLatest)
            string json = FetchJson(LatestApiUrl);
            string url = AssetUrl(json, asset);
            if (url is null)
                throw new Exception($"Asset {asset} fehlt im Release");

            SetPhase(Phase.Downloading, asset);
            string work = Path.Combine(AppContext.BaseDirectory, "update");
            Directory.CreateDirectory(work);
            tmp = Path.Combine(work, asset + ".part");
            long bytes = DownloadFile(url, tmp, (d, t) =>
            {
                lock (_lock) { _progressDone = d; _progressTotal = t; }
            });
            if (bytes <= 0 || !File.Exists(tmp))
                throw new Exception("Download fehlgeschlagen");

            // 2) Prüfsumme aus dem SHA256SUMS-Asset (fehlend = überspringen,
            //    falsche = Abbruch). Die Zip selbst prüft Schritt 3 beim
            //    Öffnen (ZipFile wirft bei defekten Einträgen).
            SetPhase(Phase.Verifying, "");
            string expected = ExpectedSha(json, asset);
            string actual = Sha256File(tmp);
            if (expected is not null
                && !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new Exception("SHA256-Prüfsumme stimmt nicht überein");

            // 3) Entpacken in ein NEUES Versionsverzeichnis — die laufende
            //    Installation bleibt an Ort und Stelle (Windows würde die
            //    eigene, besetzte Exe nie ersetzen können)
            SetPhase(Phase.Extracting, "");
            string root = InstallRoot();
            if (root is null)
                throw new Exception("kein schreibbares Verzeichnis für die Installation");
            string target = Path.Combine(root, $"AoE_Evolutions_{tag}");
            if (Directory.Exists(target))
                Directory.Delete(target, true);
            ExtractZip(tmp, target);

            string exe = FindAppHost(target);
            if (exe is null)
                throw new Exception("Apphost (AgeOfEvolutions) nicht in der Zip gefunden");

            // 4) Neue Version starten
            SetPhase(Phase.Launching, exe);
            lock (_lock) _installedExe = exe;
            StartApp(exe, Path.GetDirectoryName(exe));

            SetPhase(Phase.Done, exe);
        }
        catch (Exception e)
        {
            SetPhase(Phase.Failed, e.Message);
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _updating, 0);
            if (tmp is not null)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }

    private void SetPhase(Phase p, string msg)
    {
        lock (_lock) { _phase = p; _phaseMsg = msg ?? ""; }
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

    /// <summary>
    /// Installationswurzververzeichnis: die laufende Installation
    /// (AppContext.BaseDirectory), falls schreibbar; sonst das
    /// User-Verzeichnis (%LOCALAPPDATA%\AgeOfEvolutions / ~/.local/share/
    /// AgeOfEvolutions). Null, wenn keines schreibbar ist.
    /// </summary>
    public static string InstallRoot()
    {
        string baseDir = AppContext.BaseDirectory;
        if (Writable(baseDir))
            return baseDir;
        string fallback;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgeOfEvolutions");
        else
            fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "AgeOfEvolutions");
        try
        {
            Directory.CreateDirectory(fallback);
            return Writable(fallback) ? fallback : null;
        }
        catch
        {
            return null;
        }
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
            using var src = entry.Open();
            using var dst = File.Create(full);
            src.CopyTo(dst);
        }
    }

    /// <summary>Apphost in einem entpackten Release suchen (erste Treffer),
    /// sonst null.</summary>
    public static string FindAppHost(string dir)
    {
        if (!Directory.Exists(dir))
            return null;
        string name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "AgeOfEvolutions.exe"
            : "AgeOfEvolutions";
        foreach (var f in Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories))
            return f;
        return null;
    }

    /// <summary>
    /// Die Zip-Datei für diese Plattform (Name im GitHub-Release), oder null,
    /// wenn es für OS+Architektur kein Release-Asset gibt (z. B. Windows
    /// ARM, Linux ARM — die 5 RIDs: win-x64-DesktopGL, win-x64-WindowsDX,
    /// osx-arm64, osx-x64, linux-x64). Windows 64-Bit bekommt Standardmäßig
    /// die DesktopGL-Zip; WindowsDX wäre die Alternative.
    /// </summary>
    public static string PlatformZip(Version v)
    {
        string ver = $"{v.Major}.{v.Minor}.{v.Build}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? $"AoE_Evolutions_v{ver}_win-x64-desktopgl.zip"
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
    /// Die Menü-Statuszeile: immer mit der aktuellen Version, danach der
    /// Zustand der Update-Prüfung bzw. des laufenden Klick-Updates. Bei
    /// UpdateAvailable ist die Zeile im Menü klickbar (BeginUpdate).
    /// </summary>
    public string StatusText()
    {
        Status s;
        Phase phase;
        Version latest;
        string msg;
        long done, total;
        lock (_lock)
        {
            s = _status;
            phase = _phase;
            latest = _latest;
            msg = _phaseMsg;
            done = _progressDone;
            total = _progressTotal;
        }
        string neu = latest is null ? "?" : Tag(latest);
        switch (s)
        {
            case Status.UpdateAvailable:
                switch (phase)
                {
                    case Phase.Downloading:
                        return total > 0
                            ? $"Version {CurrentVersionText} — lade {neu} … {Mb(done)} / {Mb(total)} MB"
                            : $"Version {CurrentVersionText} — lade {neu} … {Mb(done)} MB";
                    case Phase.Verifying:
                        return $"Version {CurrentVersionText} — prüfe Prüfsumme von {neu} …";
                    case Phase.Extracting:
                        return $"Version {CurrentVersionText} — installiere {neu} …";
                    case Phase.Launching:
                        return $"Version {CurrentVersionText} — starte {neu} …";
                    case Phase.Done:
                        return $"Version {neu} installiert und gestartet — klicken schließt diese (alte) Version";
                    case Phase.Failed:
                        return $"Version {CurrentVersionText} — Update fehlgeschlagen ({msg}) — klicken = erneut";
                    default:
                        return $"Version {CurrentVersionText} — Update {neu} verfügbar: klicken — lädt und installiert automatisch";
                }
            case Status.UpToDate:
                return $"Version {CurrentVersionText} — aktuell";
            case Status.Checking:
                return $"Version {CurrentVersionText} — wird geprüft …";
            case Status.Unavailable:
                return $"Version {CurrentVersionText} — Update-Prüfung nicht möglich (offline?)";
            default:
                return $"Version {CurrentVersionText}";
        }
    }

    private static string Mb(long bytes)
        => (bytes / 1048576.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

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
    /// (.inflight → Move, damit ein Abbruch keine halbe Datei hinterlässt).</summary>
    private static long DefaultDownloadFile(string url, string target, Action<long, long> progress)
    {
        using var resp = _dl.GetAsync(url).GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();
        string tmp = target + ".inflight";
        try
        {
            using var src = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using var dst = File.Create(tmp);
            long done = 0;
            long total = resp.Content.Headers.ContentLength ?? 0;
            long lastReport = 0;
            var buf = new byte[1 << 20];
            int n;
            while ((n = src.Read(buf, 0, buf.Length)) > 0)
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
