using System.Linq;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;
using DataMap = AgeOfEvolutions.Core.Data.TileMap;
using DataPlayer = AgeOfEvolutions.Core.Data.Player;
using MapSettings = AgeOfEvolutions.Core.Data.MapSettings;
using MapSize = AgeOfEvolutions.Core.Data.MapSize;
using SaveGameMapper = AgeOfEvolutions.Core.Data.SaveGameMapper;
using SaveGameStore = AgeOfEvolutions.Core.Data.SaveGameStore;
using UnitType = AgeOfEvolutions.Core.Data.UnitType;

namespace AoE.Tests;

/// <summary>
/// Die Arbeit vor einer Flucht (Unit.ResumeJob, Unit.ResumeSite) übersteht Speichern und Laden -
/// sonst stand ein Dorfbewohner, der beim Speichern floh, geladen untätig am Fluchtpunkt
/// (gefunden 2026-11-13 über die Gruppe speichern, seit Raubtiere Flucht auslösen). Den
/// Fluchtzustand selbst prüft tools/spielablauf raubflucht (Fall Spielstand).
/// </summary>
public class FluchtSpielstandTests
{
    private static DataMap Karte() => new(40, 30, 32, MapSettings.ForSize(MapSize.Test));

    private static (DataMap Geladen, AgeOfEvolutions.Core.Data.SaveGame Stand) Durch(DataMap karte)
    {
        var stand = SaveGameMapper.Capture(karte, new DataPlayer(0, "Spieler", "Briten"), new DataPlayer(1, "Gegner", "Azteken"), MapSize.Test);
        // über JSON, wie auf der Platte
        var json = SaveGameStore.Serialize(stand);
        var (geladen, _, _) = SaveGameMapper.Restore(SaveGameStore.Deserialize(json));
        return (geladen, stand);
    }

    [Fact]
    public void GemerkterSammelauftragUndBaustelleKommenZurueck()
    {
        var karte = Karte();
        var haus = karte.AddBuilding(4, 4, "Haus", 0, 2);
        var dorf = karte.AddUnit(UnitType.Villager, 12, 12, 0);
        dorf.ResumeJob = new GatherJob(0, Resource.Wood, new Position(3, 9), 7);
        dorf.ResumeSite = haus;

        var (geladen, stand) = Durch(karte);
        int i = karte.Units.IndexOf(dorf);
        Assert.NotNull(stand.Units[i].ResumeJob);
        Assert.Equal(karte.Buildings.IndexOf(haus), stand.Units[i].ResumeSite);

        var zurueck = geladen.Units[i];
        Assert.NotNull(zurueck.ResumeJob);
        Assert.Equal(Resource.Wood, zurueck.ResumeJob.Resource);
        Assert.Equal(3, zurueck.ResumeJob.Source.X);
        Assert.Equal(9, zurueck.ResumeJob.Source.Y);
        Assert.Equal(7, zurueck.ResumeJob.Carrying);
        Assert.Same(geladen.Buildings[karte.Buildings.IndexOf(haus)], zurueck.ResumeSite);
    }

    [Fact]
    public void OhneFluchtBleibtAllesLeer()
    {
        var karte = Karte();
        var dorf = karte.AddUnit(UnitType.Villager, 12, 12, 0);
        var (geladen, stand) = Durch(karte);
        int i = karte.Units.IndexOf(dorf);
        Assert.Null(stand.Units[i].ResumeJob);
        Assert.Null(stand.Units[i].ResumeSite);
        Assert.False(stand.Units[i].Fleeing);
        Assert.Null(geladen.Units[i].ResumeJob);
        Assert.Null(geladen.Units[i].ResumeSite);
    }

    [Fact]
    public void AlterStandOhneFelderLaedtOhneErinnerung()
    {
        var karte = Karte();
        karte.AddUnit(UnitType.Villager, 12, 12, 0);
        var stand = SaveGameMapper.Capture(karte, new DataPlayer(0, "Spieler", "Briten"), new DataPlayer(1, "Gegner", "Azteken"), MapSize.Test);
        var json = SaveGameStore.Serialize(stand)
            .Replace("\"ResumeJob\":null,", "").Replace("\"ResumeSite\":null,", "").Replace("\"Fleeing\":false,", "");
        var (geladen, _, _) = SaveGameMapper.Restore(SaveGameStore.Deserialize(json));
        Assert.All(geladen.Units, u => Assert.Null(u.ResumeJob));
    }

    [Fact]
    public void VerweisAufEinVerschwundenesGebaeudeWirdNull()
    {
        var karte = Karte();
        var dorf = karte.AddUnit(UnitType.Villager, 12, 12, 0);
        var stand = SaveGameMapper.Capture(karte, new DataPlayer(0, "Spieler", "Briten"), new DataPlayer(1, "Gegner", "Azteken"), MapSize.Test);
        stand.Units[karte.Units.IndexOf(dorf)].ResumeSite = 99;
        var (geladen, _, _) = SaveGameMapper.Restore(stand);
        Assert.Null(geladen.Units.First().ResumeSite);
    }
}
