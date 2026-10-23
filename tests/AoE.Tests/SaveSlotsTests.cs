using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AgeOfEvolutions.Core.Data;
using AoE.Core.Economy;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Mehrere Spielstand-Plätze: die Kopf-Lesung (<see cref="SaveGameStore.ReadHeader"/>),
/// die Liste (neuester zuerst, kaputzte Stände übersprungen, Fremddateien ignoriert)
/// und die Nummerierung des nächsten freien Platzes. Alle Tests arbeiten in einem
/// eigenen TempOrdner - der echte Ordner neben den Einstellungen bleibt unberührt.
/// </summary>
public class SaveSlotsTests
{
    /// <summary>Ein Ordner unter %TEMP%, der sich beim Dispositional wieder löscht.</summary>
    private sealed class TempOrdner : IDisposable
    {
        public readonly string Pfad = Path.Combine(Path.GetTempPath(),
            "aoe_slots_" + Guid.NewGuid().ToString("N"));

        public TempOrdner() => Directory.CreateDirectory(Pfad);

        public string Datei(string name) => Path.Combine(Pfad, name);

        public void Dispose()
        {
            try { Directory.Delete(Pfad, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Ein kleiner, lesbarer Spielstand: Speicherzeit, Spielzeit, Zeitalter.</summary>
    private static SaveGame Stand(DateTime savedAt, double gameSeconds, Age age) => new()
    {
        Version = SaveGame.CURRENT_VERSION,
        SavedAt = savedAt,
        GameSeconds = gameSeconds,
        Players =
        {
            new PlayerSave { Id = 0, Name = "Spieler", Civilization = "Briten", Ages = new AgeProgressState(age, null, 0f) },
        },
    };

    private static void Schreibe(string pfad, SaveGame stand) => new SaveGameStore(pfad).Save(stand);

    // --- ReadHeader -------------------------------------------------------------

    [Fact]
    public void ReadHeader_LiestSpeicherzeitSpielzeitUndZeitalter()
    {
        using var ordner = new TempOrdner();
        var pfad = ordner.Datei(SaveGameStore.FILE_NAME);
        var savedAt = new DateTime(2026, 10, 22, 21, 14, 0);
        Schreibe(pfad, Stand(savedAt, 1234.0, Age.Castle));

        var slot = SaveGameStore.ReadHeader(pfad);

        Assert.Equal(pfad, slot.FilePath);
        Assert.Equal(savedAt, slot.SavedAt);
        Assert.Equal(1234.0, slot.GameSeconds);
        Assert.Equal(Age.Castle, slot.Age);
    }

    [Fact]
    public void ReadHeader_FehlendeStandardwerte_SindNullUndDunkleZeit()
    {
        using var ordner = new TempOrdner();
        var pfad = ordner.Datei(SaveGameStore.FILE_NAME);
        // GameSeconds 0 und Ages.Current Dark werden als Standardwerte weggespart.
        Schreibe(pfad, Stand(new DateTime(2026, 10, 22, 8, 0, 0), 0, Age.Dark));

        var slot = SaveGameStore.ReadHeader(pfad);

        Assert.Equal(0.0, slot.GameSeconds);
        Assert.Equal(Age.Dark, slot.Age);
    }

    [Fact]
    public void ReadHeader_OhneId_GiltDerEintragOhneAlsSpielerNull()
    {
        using var ordner = new TempOrdner();
        var pfad = ordner.Datei(SaveGameStore.FILE_NAME);
        // Id 0 ist Standardwert und fehlt im JSON; der Gegner (Id 1) steht zuerst.
        File.WriteAllText(pfad, """
            {"SavedAt":"2026-10-01T10:00:00","Players":[
              {"Id":1,"Ages":{"Current":"Imperial"}},
              {"Ages":{"Current":"Feudal"}}]}
            """);

        var slot = SaveGameStore.ReadHeader(pfad);

        Assert.Equal(Age.Feudal, slot.Age);
    }

    [Fact]
    public void ReadHeader_OhneSpieler_OderOhneSpeicherzeit_OderKaputt_IstUngueltig()
    {
        using var ordner = new TempOrdner();

        var ohneSpieler = ordner.Datei("spielstand-2.json");
        File.WriteAllText(ohneSpieler, """{"SavedAt":"2026-10-01T10:00:00","Players":[]}""");
        Assert.Throws<InvalidDataException>(() => SaveGameStore.ReadHeader(ohneSpieler));

        var ohneZeit = ordner.Datei("spielstand-3.json");
        File.WriteAllText(ohneZeit, """{"Players":[{}]}""");
        Assert.Throws<InvalidDataException>(() => SaveGameStore.ReadHeader(ohneZeit));

        var kaputt = ordner.Datei("spielstand-4.json");
        File.WriteAllText(kaputt, "{");
        Assert.Throws<InvalidDataException>(() => SaveGameStore.ReadHeader(kaputt));
    }

    [Fact]
    public void ReadHeader_FehlendeDatei_GibtFileNotFound()
    {
        using var ordner = new TempOrdner();
        Assert.Throws<FileNotFoundException>(() => SaveGameStore.ReadHeader(ordner.Datei("spielstand.json")));
    }

    // --- Label -------------------------------------------------------------------

    [Fact]
    public void Label_NenntZeitZeitalterUndMinuten()
    {
        var slot = new SaveSlot("x", new DateTime(2026, 10, 22, 21, 14, 0), 2230.0, Age.Castle);

        Assert.Equal("22.10.2026 21:14 · Ritterzeit · 37 min", slot.Label);
    }

    // --- ListSaves ----------------------------------------------------------------

    [Fact]
    public void ListSaves_FehlenderOrdner_IstLeer()
        => Assert.Empty(SaveGameStore.ListSaves(Path.Combine(Path.GetTempPath(), "aoe_gibt_es_nicht_" + Guid.NewGuid().ToString("N"))));

    [Fact]
    public void ListSaves_LeererOrdner_IstLeer()
    {
        using var ordner = new TempOrdner();
        Assert.Empty(SaveGameStore.ListSaves(ordner.Pfad));
    }

    [Fact]
    public void ListSaves_FindetAllePlaetzeNeuesterZuerst()
    {
        using var ordner = new TempOrdner();
        var alt = ordner.Datei(SaveGameStore.FILE_NAME);
        var neu = ordner.Datei("spielstand-2.json");
        Schreibe(alt, Stand(new DateTime(2026, 10, 20, 10, 0, 0), 60, Age.Dark));
        Schreibe(neu, Stand(new DateTime(2026, 10, 22, 18, 30, 0), 120, Age.Feudal));

        var plaetze = SaveGameStore.ListSaves(ordner.Pfad);

        Assert.Equal(new[] { neu, alt }, plaetze.Select(p => p.FilePath));
        Assert.Equal(Age.Feudal, plaetze[0].Age);
    }

    [Fact]
    public void ListSaves_UeberspringtKaputteUndIgnoriertFremddateien()
    {
        using var ordner = new TempOrdner();
        var heil = ordner.Datei(SaveGameStore.FILE_NAME);
        Schreibe(heil, Stand(new DateTime(2026, 10, 20, 10, 0, 0), 60, Age.Dark));

        File.WriteAllText(ordner.Datei("spielstand-9.json"), "{");                       // kaputt
        File.WriteAllText(ordner.Datei("spielstand.json.tmp"), "{");                      // Schreibrest
        File.WriteAllText(ordner.Datei("einstellungen.json"), "{}");                      // falscher Name
        File.WriteAllText(ordner.Datei("spielstand-x.json"), "{}");                       // keine Ziffer
        Directory.CreateDirectory(ordner.Datei("spielstand-7.json"));                      // ein Ordner

        var plaetze = SaveGameStore.ListSaves(ordner.Pfad);

        Assert.Equal(new[] { heil }, plaetze.Select(p => p.FilePath));
    }

    [Fact]
    public void ListSaves_GrosseKleinUndNummer1_ZaehlenAuch()
    {
        using var ordner = new TempOrdner();
        var nummerEins = ordner.Datei("spielstand-1.json");
        var gross = ordner.Datei("SPIELSTAND-2.JSON");
        Schreibe(nummerEins, Stand(new DateTime(2026, 10, 20, 10, 0, 0), 60, Age.Dark));
        Schreibe(gross, Stand(new DateTime(2026, 10, 21, 10, 0, 0), 60, Age.Dark));

        var plaetze = SaveGameStore.ListSaves(ordner.Pfad);

        Assert.Equal(2, plaetze.Count);
        Assert.Contains(plaetze, p => p.FilePath == nummerEins);
        Assert.Contains(plaetze, p => p.FilePath == gross);
    }

    // --- NextSavePath ---------------------------------------------------------------

    [Fact]
    public void NextSavePath_OhneAlteDatei_IstDerErstePlatz()
    {
        using var ordner = new TempOrdner();
        Assert.Equal(ordner.Datei(SaveGameStore.FILE_NAME), SaveGameStore.NextSavePath(ordner.Pfad));
    }

    [Fact]
    public void NextSavePath_NimmtDieKleinsteFreieNummer()
    {
        using var ordner = new TempOrdner();
        var erst = ordner.Datei(SaveGameStore.FILE_NAME);
        var zweit = ordner.Datei("spielstand-2.json");
        var stand = Stand(new DateTime(2026, 10, 22, 10, 0, 0), 60, Age.Dark);

        // Erst ist noch frei (das prüft der Test oben), also beginnt die Zählung hier bei erst.
        Assert.Equal(erst, SaveGameStore.NextSavePath(ordner.Pfad));
        Schreibe(erst, stand);
        Assert.Equal(zweit, SaveGameStore.NextSavePath(ordner.Pfad));
        Schreibe(zweit, stand);
        Assert.Equal(ordner.Datei("spielstand-3.json"), SaveGameStore.NextSavePath(ordner.Pfad));

        // Die kleinste freie Nummer zählt von vorn: ist 2 gelöscht, ist wieder 2 frei - auch wenn 3 schon liegt.
        Schreibe(ordner.Datei("spielstand-3.json"), stand);
        File.Delete(zweit);
        Assert.Equal(zweit, SaveGameStore.NextSavePath(ordner.Pfad));
    }

    [Fact]
    public void NextSavePath_LegtNichtsAn()
    {
        using var ordner = new TempOrdner();
        var pfad = SaveGameStore.NextSavePath(Path.Combine(ordner.Pfad, "neu"));

        Assert.Equal(Path.Combine(ordner.Pfad, "neu", SaveGameStore.FILE_NAME), pfad);
        Assert.False(Directory.Exists(Path.Combine(ordner.Pfad, "neu")));
    }

    /// <summary>
    /// Ein TempOrdner, der zusätzlich der Spielstand-Ordner wird (AOE_SPEICHER_ORDNER),
    /// damit die Platz-Statik ohne den echten Ordner neben den Einstellungen läuft.
    /// </summary>
    private sealed class SpeicherOrdner : IDisposable
    {
        private readonly string alterWert;
        public readonly string Pfad = Path.Combine(Path.GetTempPath(),
            "aoe_loeschen_" + Guid.NewGuid().ToString("N"));

        public SpeicherOrdner()
        {
            alterWert = Environment.GetEnvironmentVariable("AOE_SPEICHER_ORDNER");
            Directory.CreateDirectory(Pfad);
            Environment.SetEnvironmentVariable("AOE_SPEICHER_ORDNER", Pfad);
        }

        public string Datei(string name) => Path.Combine(Pfad, name);

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("AOE_SPEICHER_ORDNER", alterWert);
            try { Directory.Delete(Pfad, recursive: true); } catch (IOException) { }
        }
    }

    // --- DeleteSlot -------------------------------------------------------------

    [Fact]
    public void DeleteSlot_LoeschtDenPlatzUndDieNummerWirdFrei()
    {
        using var ordner = new SpeicherOrdner();
        var erst = ordner.Datei(SaveGameStore.FILE_NAME);
        var zweit = ordner.Datei("spielstand-2.json");
        Schreibe(erst, Stand(new DateTime(2026, 10, 20, 10, 0, 0), 60, Age.Dark));
        Schreibe(zweit, Stand(new DateTime(2026, 10, 21, 10, 0, 0), 60, Age.Dark));

        SaveGameStore.DeleteSlot(zweit);

        Assert.False(File.Exists(zweit));
        Assert.Equal(new[] { erst }, SaveGameStore.ListSaves().Select(p => p.FilePath));
        // Die gelöschte Nummer 2 ist die kleinste freie und wird wieder benutzt.
        Assert.Equal(zweit, SaveGameStore.NextSavePath());
    }

    [Fact]
    public void DeleteSlot_AusserhalbDesSpielstandOrdners_IstKeinPlatz()
    {
        using var ordner = new SpeicherOrdner();
        // Auch eine Datei mit Platname, aber in einem fremden Ordner - sie bleibt heil.
        var fremd = Path.Combine(Path.GetTempPath(), "aoe_fremd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fremd);
        try
        {
            var pfad = Path.Combine(fremd, SaveGameStore.FILE_NAME);
            Schreibe(pfad, Stand(new DateTime(2026, 10, 20, 10, 0, 0), 60, Age.Dark));

            Assert.Throws<ArgumentException>(() => SaveGameStore.DeleteSlot(pfad));
            Assert.True(File.Exists(pfad));
        }
        finally
        {
            Directory.Delete(fremd, recursive: true);
        }
    }

    [Fact]
    public void DeleteSlot_FalscherName_IstKeinPlatz()
    {
        using var ordner = new SpeicherOrdner();
        var pfad = ordner.Datei("einstellungen.json");
        File.WriteAllText(pfad, "{}");

        Assert.Throws<ArgumentException>(() => SaveGameStore.DeleteSlot(pfad));
        Assert.True(File.Exists(pfad));
    }

    [Fact]
    public void DeleteSlot_FehlendeDatei_IstFileNotFound()
    {
        using var ordner = new SpeicherOrdner();
        Assert.Throws<FileNotFoundException>(() => SaveGameStore.DeleteSlot(ordner.Datei("spielstand-2.json")));
    }
}
