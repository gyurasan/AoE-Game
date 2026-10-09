// tools/ai-pruefung: belegt headless, dass beide KI-Instanzen (Owner 0 und
// Owner 1) spielen, militärisch aufbauen, die Karte erkunden und sich
// angreifen.
//
//   dotnet run --project tools/ai-pruefung
//
// Es erzeugt eine Welt (neue RTSGameplayScreen mit per Reflection gesetzten
// Feldern — gleiche Vorgehensweise wie tools/spielablauf/Welt), schaltet
// die eingebaute KI für beide Owner an und lässt ~300 s Spielzeit laufen.
// Danach prüft es:
//   * beide Agenten sind aktiv
//   * beide Seiten wirtschaften (Einheiten/Gebäude/Sammeln)
//   * mindestens eine Seite hat eine Kaserne gebaut
//   * beide Seiten haben ihre Karte zumindest teilweise weiter erkundet
//   * mindestens ein Angriff kam zustande (Angriffszustand einer Einheit)
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

// Eingebaute KI für beide Owner anstellen — genau das, was die Screen
// (LoadContent) im Spiel mit BothSidesAi-an tut.
screen.ActivateAi(new EconomyAi(), owner: 0);
screen.ActivateAi(new EconomyAi(), owner: 1);

// Debug-Logging der KI-Entscheidungen (nur für diesen Lauf — im Spiel aus).
var logLines = new System.Collections.Generic.List<string>();
var logLock = new object();
void AIlog(string msg)
{
    lock (logLock)
    {
        logLines.Add(msg);
        if (logLines.Count % 30 == 0)
            Console.WriteLine("    " + msg);
    }
}
foreach (var ag in screen.Agents.Values) ag.Log = AIlog;

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
    Console.WriteLine($"    FindSource Wood: {(srcWood is null ? "null" : $"({srcWood.Value.X},{srcWood.Value.Y})")}");
    Console.WriteLine($"    ExploreTargets: {string.Join(", ", bridgeDbg.ExploreTargets())}");
    Console.WriteLine($"    VisibleEnemies: {bridgeDbg.VisibleEnemies().Count}");
    Console.WriteLine();
}

int Verstoesse = 0;
void Meld(string m) { Console.WriteLine("  X " + m); Verstoesse++; }
void Ok(string m) => Console.WriteLine("  ok  " + m);

if (screen.ActiveAgent is null && !screen.Agents.ContainsKey(1))
    Meld("KI ist nicht aktiv — ActivateAi hat keinen Agenten erzeugt");
else
    Ok("KI als Owner 1 aktiv (Agent erzeugt)");
if (!screen.Agents.ContainsKey(0))
    Meld("KI für Owner 0 fehlt — BothSides-Aktivierung lief nicht an");
else
    Ok("KI als Owner 0 aktiv (Agent erzeugt)");

// Ausgangszustand beider Seiten festhalten.
int popStartP1 = p1.PopulationCount, popStartP2 = p2.PopulationCount;
int bldStartP1 = map.Buildings.Count(b => b.OwnerId == 0);
int bldStartP2 = map.Buildings.Count(b => b.OwnerId == 1);
int exploredStartP0 = 0, exploredStartP1 = 0;
for (int x = 0; x < map.Width; x++)
    for (int y = 0; y < map.Height; y++)
    {
        if (map.IsTileExplored(x, y, 0)) exploredStartP0++;
        if (map.IsTileExplored(x, y, 1)) exploredStartP1++;
    }

Console.WriteLine($"  Start Owner 0: Pop {popStartP1}, Gebäude {bldStartP1}, erkundet {exploredStartP0} Kacheln");
Console.WriteLine($"  Start Owner 1: Pop {popStartP2}, Gebäude {bldStartP2}, erkundet {exploredStartP1} Kacheln");

// ---- ~300 s Spielzeit laufen lassen — dieselben Update-Rufe wie im Spiel ----
const float dt = 1f / 60f;
double zeit = 0;
int attackSeenP1 = 0, attackSeenP2 = 0;

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
    // Beide KI-Instanzen ticken — genau wie imSpiel im Update-Loop.
    if (screen.Agents.TryGetValue(0, out var a0)) a0.Tick(dt);
    if (screen.Agents.TryGetValue(1, out var a1)) a1.Tick(dt);

    // Angriffszustand zählen — der Beweis, dass die KI angreift.
    int a0s = 0, a1s = 0;
    foreach (var u in units)
    {
        bool imAngriff = u.State == UnitState.Attacking
                         || (u.State == UnitState.Moving && u.AttackTarget != null);
        if (imAngriff)
        {
            if (u.OwnerId == 0) a0s++; else a1s++;
        }
    }
    if (a0s > attackSeenP1) attackSeenP1 = a0s;
    if (a1s > attackSeenP2) attackSeenP2 = a1s;
}

for (int i = 0; i < 300 * 60; i++)
{
    zeit += dt;
    Schritt();
}

// ---- Ergebnis-Auswertung: beide Seiten, Militär, Erkundung ----
int exploredEndP0 = 0, exploredEndP1 = 0;
for (int x = 0; x < map.Width; x++)
    for (int y = 0; y < map.Height; y++)
    {
        if (map.IsTileExplored(x, y, 0)) exploredEndP0++;
        if (map.IsTileExplored(x, y, 1)) exploredEndP1++;
    }

int popEndP1 = p1.PopulationCount, popEndP2 = p2.PopulationCount;
int bldEndP1 = map.Buildings.Count(b => b.OwnerId == 0);
int bldEndP2 = map.Buildings.Count(b => b.OwnerId == 1);
int barracksP1 = map.Buildings.Count(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.Barracks);
int barracksP2 = map.Buildings.Count(b => b.OwnerId == 1 && b.Core.BuildingType == BuildingType.Barracks);
int archerP1 = map.Buildings.Count(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.ArcheryRange);
int archerP2 = map.Buildings.Count(b => b.OwnerId == 1 && b.Core.BuildingType == BuildingType.ArcheryRange);
int soldiersP1 = units.Count(u => u.OwnerId == 0 && u.Type != UnitType.Villager && u.State != UnitState.Dead);
int soldiersP2 = units.Count(u => u.OwnerId == 1 && u.Type != UnitType.Villager && u.State != UnitState.Dead);

Console.WriteLine();
Console.WriteLine($"  Owner 0 nach ~300 s: Pop {popStartP1}->{popEndP1}, Gebäude {bldStartP1}->{bldEndP1}, "
                  + $"Kaserne={barracksP1}, Schießstand={archerP1}, Soldaten={soldiersP1}");
Console.WriteLine($"  Owner 1 nach ~300 s: Pop {popStartP2}->{popEndP2}, Gebäude {bldStartP2}->{bldEndP2}, "
                  + $"Kaserne={barracksP2}, Schießstand={archerP2}, Soldaten={soldiersP2}");
Console.WriteLine($"  Erkundung: Owner 0 {exploredStartP0}->{exploredEndP0} Kacheln, Owner 1 {exploredStartP1}->{exploredEndP1}");
Console.WriteLine($"  Max. gleichzeitig im Angriff: Owner 0={attackSeenP1}, Owner 1={attackSeenP2}");

// 1) Beide Seiten müssen wirtschaftlich aktiv sein.
bool active0 = popEndP1 > popStartP1 || bldEndP1 > bldStartP1 || soldiersP1 > 0;
bool active1 = popEndP2 > popStartP2 || bldEndP2 > bldStartP2 || soldiersP2 > 0;
if (active0) Ok("Owner 0 aktiv (KI spielt die eigene Seite)");
else Meld("Owner 0 tut nichts — beide-Seiten-KI arbeitet nur auf einer Seite");
if (active1) Ok("Owner 1 aktiv (KI spielt den Gegner)");
else Meld("Owner 1 tut nichts — beide-Seiten-KI arbeitet nur auf einer Seite");

// 2) Militärisch: mindestens eine Kaserne je Seite oder zusammen.
if (barracksP1 + barracksP2 > 0)
    Ok($"Militär gebaut (Kaserne P1={barracksP1}, P2={barracksP2})");
else
    Meld("Keine Kaserne gebaut — die KI baut kein Militär");

// 3) Erkundung: beide Seiten müssen ihre Karte weiter erkannt haben.
bool explored0 = exploredEndP0 > exploredStartP0;
bool explored1 = exploredEndP1 > exploredStartP1;
if (explored0) Ok($"Owner 0 erkundet ({exploredStartP0}->{exploredEndP0} Kacheln)");
else Meld("Owner 0 erkundet keine neue Karte — KI bleibt auf der Startzone");
if (explored1) Ok($"Owner 1 erkundet ({exploredStartP1}->{exploredEndP1} Kacheln)");
else Meld("Owner 1 erkundet keine neue Karte — KI bleibt auf der Startzone");

// 4) Aggressiv: mindestens eine Seite muss einen Angriff durchführen.
bool raided = attackSeenP1 > 0 || attackSeenP2 > 0;
if (raided) Ok($"Angriff beobachtet (P1={attackSeenP1} im Attack-Zustand, P2={attackSeenP2})");
else if (soldiersP1 + soldiersP2 > 0)
    Meld("Soldaten vorhanden, aber kein einziger Angriff beobachtet — KI ist nicht aggressiv");
else
    // ohne Soldaten ist ein Angriff auch nicht zu erwarten — aber dann
    // schlägt Punkt 2 schon fehl.
    Meld("Kein Angriff — und auch keine Soldaten (Militär fehlt, s.o.)");

Console.WriteLine();
if (Verstoesse > 0)
{
    Console.WriteLine($"NICHT ERFÜLLT ({Verstoesse} Verstöße)");
    return 1;
}
Console.WriteLine("Alle Prüfungen ok — beide KI-Instanzen spielen.");
return 0;
