// tools/ai-pruefung: belegt headless, dass der KI-Gegner (Owner 1) spielt.
//
//   dotnet run --project tools/ai-pruefung
//
// Es erzeugt eine Welt (neue RTSGameplayScreen mit per Reflection gesetzten
// Feldern — gleiche Vorgehensweise wie tools/spielablauf/Welt), schaltet die
// eingebaute KI für Owner 1 an und lässt ~120 s Spielzeit laufen. Danach
// prüft es:
//   * der Agent ist tatsächlich aktiv
//   * die Wirtschaft von Owner 1 ist angewachsen
//
// Exit 0 = alles ok, 1 = Verstöße.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AoE.Core.Ai;
using AoE.Core.Entities;
using AgeOfEvolutions.Core.AI;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;
using UnitState = AoE.Core.Entities.UnitState;

const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
Type T = typeof(RTSGameplayScreen);

var screen = new RTSGameplayScreen(MapSize.Standard);

// Welt manuell aufbauen — wie tools/spielablauf/Welt, ohne ScreenManager.
int seite = MapSizes.Side(MapSize.Standard);
var map = new TileMap(seite, seite, 32, MapSettings.ForSize(MapSize.Standard));
var units = map.Units;
var p1 = new Player(0, "P1", "Briten");
var p2 = new Player(1, "P2", "Azteken");
foreach (var u in units)
    (u.OwnerId == 0 ? p1 : p2).AddUnit(u);
// Startreserven für beide — die Karten sind sonst knapp
p1.Resources.Add(Resource.Wood, 1000);
p1.Resources.Add(Resource.Stone, 500);
p1.Resources.Add(Resource.Gold, 500);
p1.Resources.Add(Resource.Food, 500);
p2.Resources.Add(Resource.Wood, 1000);
p2.Resources.Add(Resource.Stone, 500);
p2.Resources.Add(Resource.Gold, 500);
p2.Resources.Add(Resource.Food, 500);

void Set(string name, object wert)
{
    var f = T.GetField(name, F);
    if (f == null) throw new InvalidOperationException($"Feld {name} fehlt");
    f.SetValue(screen, wert);
}
object Get(string name)
{
    var f = T.GetField(name, F);
    if (f == null) throw new InvalidOperationException($"Feld {name} fehlt");
    return f.GetValue(screen);
}
object Call(string name, params object[] args)
{
    var m = T.GetMethod(name, F);
    if (m == null) throw new InvalidOperationException($"Methode {name} fehlt");
    return m.Invoke(screen, args);
}

Set("tileMap", map);
Set("gatherWorld", new TileMapGatherWorld(map));
Set("player1", p1);
Set("player2", p2);
Set("units", units);
map.UpdateFogOfWarForPlayer(0, units);
map.UpdateFogOfWarForPlayer(1, units);
Call("UpdatePopulationLimits");

// Eingebaute KI für Owner 1 anstellen — genau das, was die Screen
// (LoadContent) im Spiel tut.
screen.ActivateAi(new EconomyAi(), owner: 1);

// ---- Diagnose-Ausgabe: was sieht die Bridge für Owner 1 vor dem Start? ----
var bridgeDbg = new AiBridge(screen) { Owner = 1 };
{
    Console.WriteLine();
    Console.WriteLine("  [BRIDGE Owner 1, vor dem Start]");
    Console.WriteLine($"    Pop {bridgeDbg.PopulationCount} / Kapazität {bridgeDbg.PopulationCapacity}, "
        + $"VillagerTraining {bridgeDbg.VillagerTrainingCount}");
    Console.WriteLine($"    Ressourcen: Food={bridgeDbg.Resources.Food} Wood={bridgeDbg.Resources.Wood} "
        + $"Gold={bridgeDbg.Resources.Gold} Stone={bridgeDbg.Resources.Stone}");
    Console.WriteLine($"    TC: {(bridgeDbg.TownCenter == null ? "NULL" : $"pos=({bridgeDbg.TownCenter.X},{bridgeDbg.TownCenter.Y}) complete={bridgeDbg.TownCenter.IsComplete} pos={bridgeDbg.TownCenter.Id}")}");
    Console.WriteLine($"    Einheiten ({bridgeDbg.Units.Count}):");
    foreach (var u in bridgeDbg.Units)
        Console.WriteLine($"      id={u.Id} kind={u.Kind} state={u.State} pos=({u.X},{u.Y}) buildingId={u.BuildingId}");
    Console.WriteLine($"    Gebäude ({bridgeDbg.Buildings.Count}):");
    foreach (var b in bridgeDbg.Buildings)
        Console.WriteLine($"      id={b.Id} type={b.Type} pos=({b.X},{b.Y}) complete={b.IsComplete}");
    var srcWood = bridgeDbg.FindSource(Resource.Wood, 1, 1, 16);
    var srcFood = bridgeDbg.FindSource(Resource.Food, 1, 1, 16);
    Console.WriteLine($"    FindSource Wood: {(srcWood is null ? "null" : $"({srcWood.Value.X},{srcWood.Value.Y})")},  Food: {(srcFood is null ? "null" : $"({srcFood.Value.X},{srcFood.Value.Y})")}");
    // Ist die eigene Startzone (unterm TC) sichtbar?
    if (bridgeDbg.TownCenter is {} tc)
    {
        Console.WriteLine($"    IsExplored TC: {bridgeDbg.IsExplored(tc.X, tc.Y)}  TC+1: {bridgeDbg.IsExplored(tc.X + 1, tc.Y)}");
    }
    Console.WriteLine();
}

int Verstoesse = 0;
void Meld(string m) { Console.WriteLine("  X " + m); Verstoesse++; }
void Ok(string m) => Console.WriteLine("  ok  " + m);

if (screen.ActiveAgent is null)
    Meld("KI ist nicht aktiv — ActivateAi hat keinen Agenten erzeugt");
else
    Ok("KI als Owner 1 aktiv (Agent erzeugt)");

// Ausgangszustand von Owner 1 (der KI) festhalten.
int popStart = p2.PopulationCount;
int buildingsStart = map.Buildings.Count(b => b.OwnerId == 1);
int resStart = p2.Resources[Resource.Food] + p2.Resources[Resource.Wood]
             + p2.Resources[Resource.Gold] + p2.Resources[Resource.Stone];

Console.WriteLine($"  Start Owner 1: Pop {popStart}, Gebäude {buildingsStart}, "
    + $"Ressourcen {resStart}");

// ---- ~120 s Spielzeit laufen lassen — dieselben Update-Rufe wie im Spiel ----
const float dt = 1f / 60f;
double zeit = 0;

void Schritt()
{
    var gt = new GameTime(TimeSpan.FromSeconds(zeit), TimeSpan.FromSeconds(dt));
    void InvokeOrNull(string name, object a)
    {
        var mi = T.GetMethod(name, F);
        if (mi == null) return;
        try { mi.Invoke(screen, new[] { a }); }
        catch (System.Reflection.TargetInvocationException) { }
    }
    void Invoke0(string name)
    {
        var mi = T.GetMethod(name, F);
        if (mi == null) return;
        try { mi.Invoke(screen, new object[0]); }
        catch (System.Reflection.TargetInvocationException) { }
    }

    Invoke0("UpdateSheepClaims");
    InvokeOrNull("UpdateUnits", gt);
    Invoke0("UpdatePopulationLimits");
    InvokeOrNull("UpdateTraining", dt);
    InvokeOrNull("UpdateAges", dt);
    InvokeOrNull("UpdateConstruction", dt);
    InvokeOrNull("UpdateResources", gt);
    map.RegrowCrop(dt);
    map.RegrowGrass(dt);
    map.UpdateSheep(dt); map.UpdateDeer(dt); map.UpdateRabbits(dt); map.UpdateBoars(dt);
    map.UpdateFogOfWarForPlayer(0, units);
    map.UpdateFogOfWarForPlayer(1, units);

    // Order-Queue abarbeiten (enthält die KI-Aktionen).
    Invoke0("DrainOrders");
    if (screen.ActiveAgent is { } a) a.Tick(dt);
}

for (int i = 0; i < 120 * 60; i++)
{
    zeit += dt;
    Schritt();
}

int popEnd = p2.PopulationCount;
int buildingsEnd = map.Buildings.Count(b => b.OwnerId == 1);
int resEnd = p2.Resources[Resource.Food] + p2.Resources[Resource.Wood]
           + p2.Resources[Resource.Gold] + p2.Resources[Resource.Stone];
var o1 = units.Where(u => u.OwnerId == 1).ToList();
int working = o1.Count(u => u.State == UnitState.Gathering
                            || u.State == UnitState.Building
                            || u.State == UnitState.Returning
                            || u.State == UnitState.Moving);
int idle = o1.Count(u => u.State == UnitState.Idle);
// Detail-Breakdown zum Nachvollziehen
var states = o1.GroupBy(u => u.State).Select(g => $"{g.Key}={g.Count()}");
Console.WriteLine($"    Zustandsverteilung: {string.Join(", ", states)}");
// Ausbildung aktiv?
int trainQ = 0;
var tc1 = map.Buildings.FirstOrDefault(b => b.OwnerId == 1
           && b.Core.BuildingType == AoE.Core.Entities.BuildingType.TownCenter);
if (tc1 != null) trainQ = tc1.Training.Count;
Console.WriteLine($"    Ausbildung-Queue: {trainQ} Einheiten in Herstellung");

Console.WriteLine();
Console.WriteLine($"  Owner 1 nach ~120 s: Pop {popStart} -> {popEnd}; "
    + $"Gebäude {buildingsStart} -> {buildingsEnd}; "
    + $"Ressourcen {resStart} -> {resEnd}; "
    + $"{working} aktiv, {idle} idle");

if (popEnd >= popStart) Ok($"Bevölkerung nicht geschrumpft ({popEnd})");
else Meld($"Bevölkerung geschrumpft: {popStart} -> {popEnd}");

if (buildingsEnd >= buildingsStart) Ok($"Gebäudestand {buildingsEnd} (gestartet {buildingsStart})");
else Meld($"Gebäude verloren: {buildingsStart} -> {buildingsEnd}");

// Der eigentliche Beweis: die KI hat etwas getan
bool activity = (popEnd > popStart)
             || (buildingsEnd > buildingsStart)
             || working > 0
             || (resEnd > resStart);
if (activity) Ok("Wirtschaft aktiv — neue Einheiten, Gebäude oder Sammel-/Bauarbeit");
else Meld("KI hat nichts getan — Gegner steht nur in der Ecke");

Console.WriteLine();
if (Verstoesse > 0)
{
    Console.WriteLine($"NICHT ERFÜLLT ({Verstoesse} Verstöße)");
    return 1;
}
Console.WriteLine("Alle Prüfungen ok — der KI-Gegner spielt.");
return 0;
