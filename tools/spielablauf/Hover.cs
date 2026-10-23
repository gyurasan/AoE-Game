using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using Resource = AoE.Core.Entities.Resource;
using CoreVillager = AoE.Core.Entities.Villager;

/// <summary>
/// Gruppe hover (C17, Nutzer 2026-10-23): „wenn die Maus über einem Objekt ist, müsste das Objekt
/// leicht hervorgehoben werden, zum Beispiel etwas heller." HoverUnder prüft, was der Linksklick an
/// der Stelle täte - dieselbe Reihenfolge wie LeftClick. Die Abläufe: eigene Figur, Gegner nur mit
/// Auswahl, Gebäude ohne Auswahl, fremdes Gebäude mit Auswahl, Reparatur, Baum nur mit
/// Dorfbewohner, Raubtier nur mit Auswahl, Setzmodus hebt nichts, und HandleRtsInput setzt das
/// Feld _hover für die Zeichenschicht.
/// </summary>
static class Hover
{
    public static void Pruefen(int karte, List<string> verstoesse)
    {
        try
        {
            TileMap.TestNoFog = true;
            EigeneFigur(karte, verstoesse);
            Gegner(karte, verstoesse);
            Gebaeude(karte, verstoesse);
            Reparatur(karte, verstoesse);
            Baum(karte, verstoesse);
            Raubtier(karte, verstoesse);
            Setzmodus(karte, verstoesse);
            KlickSetztFeld(karte, verstoesse);
        }
        finally
        {
            TileMap.TestNoFog = false;
        }
    }

    static Welt NeueWelt()
    {
        var w = new Welt();
        w.Set("cameraZoom", 1f);
        w.Set("cameraPosition", Vector2.Zero);
        // Der Bildschirm ist headless nicht gesetzt; ohne Maße fiele jeder Klick „über der Leiste".
        w.Set("screenBounds", new Rectangle(0, 0, 2048, 2048));
        return w;
    }

    // --- Reflexionshelfer für das Rückgabestruct HoverTarget ---------------------

    static object RuefHover(Welt w, Vector2 worldPos) => w.Call("HoverUnder", worldPos);

    static string Art(object hover) => (string)hover.GetType().GetField("Kind").GetValue(hover).ToString();

    static object Einheit(object hover) => hover.GetType().GetField("Unit").GetValue(hover);

    static object GebaeudeObjekt(object hover) => hover.GetType().GetField("Building").GetValue(hover);

    static (int X, int Y) Kachel(object hover)
        => ((int)hover.GetType().GetField("X").GetValue(hover),
            (int)hover.GetType().GetField("Y").GetValue(hover));

    // --- Abläufe -------------------------------------------------------------------

    // 1. Die eigene Figur unter dem Zeiger wird gehoben - auch ohne jede Auswahl.
    static void EigeneFigur(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Hover eigene Figur";
        int vorher = verstoesse.Count;
        var w = NeueWelt();
        var v = w.Dorfbewohner().First();
        var hover = RuefHover(w, v.Position - new Vector2(0, 10));
        if (Art(hover) != "Unit" || !ReferenceEquals(Einheit(hover), v))
            verstoesse.Add($"{wer}: über der eigenen Figur kommt kein Unit-Treffer an ({Art(hover)})");
        // Ein Punkt mitten auf freier Wiese trifft nichts
        var (fx, fy) = FreieWiese(w);
        if (Art(RuefHover(w, w.Map.GridToWorld(new Vector2(fx, fy)))) != "None")
            verstoesse.Add($"{wer}: über freier Wiese ohne Auswahl wird etwas gehoben");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}");
    }

    // 2. Der Gegner wird nur mit Auswahl gehoben (ohne wäre der Klick kein Angriff).
    static void Gegner(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Hover Gegner";
        int vorher = verstoesse.Count;
        var w = NeueWelt();
        var e = w.Map.Units.First(u => u.OwnerId != 0);
        var (gx, gy) = FreieWiese(w, e);
        e.Position = w.Map.GridToWorld(new Vector2(gx, gy));
        if (Art(RuefHover(w, e.Position)) != "None")
            verstoesse.Add($"{wer}: ohne Auswahl wird der Gegner gehoben");
        w.Waehle(new List<Unit> { w.Dorfbewohner().First() });
        var hover = RuefHover(w, e.Position - new Vector2(0, 10));
        if (Art(hover) != "Unit" || !ReferenceEquals(Einheit(hover), e))
            verstoesse.Add($"{wer}: mit Auswahl kommt kein Gegner-Treffer an ({Art(hover)})");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}");
    }

    // 3. Ohne Auswahl hebt das Gebäude darunter; mit Auswahl nur das fremde (Angriff).
    static void Gebaeude(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Hover Gebäude";
        int vorher = verstoesse.Count;
        var w = NeueWelt();
        var eigen = w.Map.Buildings.First(b => b.OwnerId == 0 && b.IsComplete);
        var fremd = w.Map.Buildings.First(b => b.OwnerId != 0 && b.IsComplete);
        if (Art(RuefHover(w, Punkt(eigen, w))) is not "Building" ||
            !ReferenceEquals(GebaeudeObjekt(RuefHover(w, Punkt(eigen, w))), eigen))
            verstoesse.Add($"{wer}: ohne Auswahl wird das eigene Gebäude nicht gehoben");
        // Mit Auswahl: das eigene (unbeschädigte) Gebäude nicht, das fremde schon
        w.Waehle(new List<Unit> { w.Dorfbewohner().First() });
        if (Art(RuefHover(w, Punkt(eigen, w))) != "None")
            verstoesse.Add($"{wer}: mit Auswahl wird das eigene unbeschädigte Gebäude gehoben");
        if (Art(RuefHover(w, Punkt(fremd, w))) is not "Building" ||
            !ReferenceEquals(GebaeudeObjekt(RuefHover(w, Punkt(fremd, w))), fremd))
            verstoesse.Add($"{wer}: mit Auswahl wird das fremde Gebäude nicht gehoben (Angriffsklick)");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}");
    }

    // 4. Das eigene beschädigte Gebäude hebt mit Dorfbewohnern in der Auswahl (Reparatur, C15).
    static void Reparatur(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Hover Reparatur";
        int vorher = verstoesse.Count;
        var w = NeueWelt();
        var b = w.Map.Buildings.First(x => x.OwnerId == 0 && x.IsComplete);
        b.Health = b.MaxHealth / 2;
        w.Waehle(new List<Unit> { w.Dorfbewohner().First() });
        if (Art(RuefHover(w, Punkt(b, w))) is not "Building" ||
            !ReferenceEquals(GebaeudeObjekt(RuefHover(w, Punkt(b, w))), b))
            verstoesse.Add($"{wer}: das beschädigte eigene Gebäude wird mit Dorfbewohnern nicht gehoben");
        // Nur Soldaten in der Auswahl: repariert werden kann nicht, der Klick wäre ein Laufbefehl
        var miliz = w.Map.Units.FirstOrDefault(u => u.OwnerId == 0 && u.Core is not CoreVillager);
        if (miliz != null)
        {
            w.Waehle(new List<Unit> { miliz });
            if (Art(RuefHover(w, Punkt(b, w))) != "None")
                verstoesse.Add($"{wer}: ohne Dorfbewohner in der Auswahl wird das reparierbare Gebäude gehoben");
        }
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}");
    }

    // 5. Der Baum hebt nur mit Dorfbewohner in der Auswahl (Sammelauftrag), Soldaten nicht.
    static void Baum(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Hover Baum";
        int vorher = verstoesse.Count;
        var w = NeueWelt();
        (int x, int y) baum = FindeBaum(w);
        var mitte = w.Map.GridToWorld(new Vector2(baum.x, baum.y));
        w.Waehle(new List<Unit> { w.Dorfbewohner().First() });
        var hover = RuefHover(w, mitte);
        if (Art(hover) != "Tile" || Kachel(hover) != (baum.x, baum.y))
            verstoesse.Add($"{wer}: über dem Baum({baum.x},{baum.y}) kommt kein Kachel-Treffer an ({Art(hover)})");
        var miliz = w.Map.Units.FirstOrDefault(u => u.OwnerId == 0 && u.Core is not CoreVillager);
        if (miliz != null)
        {
            w.Waehle(new List<Unit> { miliz });
            if (Art(RuefHover(w, mitte)) != "None")
                verstoesse.Add($"{wer}: ohne Dorfbewohner in der Auswahl wird der Baum gehoben");
        }
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}");
    }

    // 6. Das Raubtier hebt nur mit Auswahl (Angriffsklick, RT).
    static void Raubtier(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Hover Raubtier";
        int vorher = verstoesse.Count;
        var w = NeueWelt();
        var (bx, by) = FreieWiese(w);
        if (!w.Map.PlacePredator(FoodSource.Bear, bx, by))
        {
            Console.WriteLine($"  ok  {wer}: PlacePredator lehnt ab - übersprungen");
            return;
        }
        var mitte = w.Map.GridToWorld(new Vector2(bx, by));
        if (Art(RuefHover(w, mitte)) != "None")
            verstoesse.Add($"{wer}: ohne Auswahl wird das Raubtier gehoben");
        w.Waehle(new List<Unit> { w.Dorfbewohner().First() });
        var hover = RuefHover(w, mitte);
        if (Art(hover) != "Tile" || Kachel(hover) != (bx, by))
            verstoesse.Add($"{wer}: über dem Bären ({bx},{by}) kommt kein Kachel-Treffer an ({Art(hover)})");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}");
    }

    // 7. Im Setzmodus hebt der Zeiger nichts - der Klick setzt ein Gebäude.
    static void Setzmodus(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Hover Setzmodus";
        int vorher = verstoesse.Count;
        var w = NeueWelt();
        var v = w.Dorfbewohner().First();
        w.Set("placing", (AoE.Core.Entities.BuildingType?)AoE.Core.Entities.BuildingType.House);
        if (Art(RuefHover(w, v.Position)) != "None")
            verstoesse.Add($"{wer}: im Setzmodus wird trotzdem gehoben");
        w.Set("placing", null);
        if (Art(RuefHover(w, v.Position - new Vector2(0, 10))) != "Unit")
            verstoesse.Add($"{wer}: nach dem Setzmodus hebt die eigene Figur nicht mehr");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}");
    }

    // 8. HandleRtsInput setzt das Feld _hover, aus dem die Zeichenschicht hebt.
    static void KlickSetztFeld(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Hover Klick";
        int vorher = verstoesse.Count;
        var w = NeueWelt();
        var v = w.Dorfbewohner().First();
        // Die Figur auf die erste freie Wiese (die liegt oben links - fern von Minimap
        // und Leistensaum). Der Klickpunkt liegt da, wo sie gezeichnet wird
        // (ClampCamera kann eine selbst gesetzte Kamera zurückholen - also nicht
        // umrechnen, sondern WorldToScreen nehmen).
        var (sx, sy) = FreieWiese(w, v);
        v.Position = w.Map.GridToWorld(new Vector2(sx, sy));
        var schirm = (Vector2)w.Call("WorldToScreen", v.Position);
        w.Klick(new Point((int)schirm.X, (int)schirm.Y - 10));
        object feld = w.Get("_hover");
        if (Art(feld) != "Unit" || !ReferenceEquals(Einheit(feld), v))
            verstoesse.Add($"{wer}: nach dem Klick auf die eigene Figur steht in _hover {Art(feld)}");
        // Über der Leiste hebt nichts: ein Punkt im oberen Randstreifen
        w.Klick(new Point(200, 10));
        if (Art(w.Get("_hover")) != "None")
            verstoesse.Add($"{wer}: über der Leiste steht etwas in _hover");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}");
    }

    // --- Helfer --------------------------------------------------------------------

    /// <summary>Punkt auf einem Gebäude: mittlere Grundflächenkachel, kurz über der Unterkante.</summary>
    static Vector2 Punkt(AgeOfEvolutions.Core.Data.Building b, Welt w)
        => new Vector2((b.X + b.Width / 2f) * w.Map.TileSize,
                       (b.Y + b.Height) * w.Map.TileSize - 8f);

    /// <summary>Eine begehbare Wiese ohne Ressource, Feld, Gebäude und Tier, außerhalb jeder Einheit.</summary>
    static (int X, int Y) FreieWiese(Welt w, Unit ausgenommen = null)
    {
        for (int y = 8; y < w.Map.Height - 8; y++)
            for (int x = 8; x < w.Map.Width - 8; x++)
            {
                if (w.Map.GetTile(x, y) is not { } t) continue;
                if (t.Type != TileType.Grassland || t.ResourceType != null || t.Food != FoodSource.None
                    || t.Farm || !string.IsNullOrEmpty(t.Building) || !w.Map.IsWalkable(x, y))
                    continue;
                var mitte = w.Map.GridToWorld(new Vector2(x, y));
                if (w.Map.Units.Any(u => !ReferenceEquals(u, ausgenommen)
                                         && Vector2.Distance(u.Position, mitte) < 2 * w.Map.TileSize))
                    continue;
                return (x, y);
            }
        throw new InvalidOperationException("keine freie Wiese gefunden");
    }

    /// <summary>Eine Waldkachel mit Holzvorrat.</summary>
    static (int x, int y) FindeBaum(Welt w)
    {
        for (int y = 1; y < w.Map.Height - 1; y++)
            for (int x = 1; x < w.Map.Width - 1; x++)
                if (w.Map.GetTile(x, y) is { } t && t.Type == TileType.Forest
                    && t.ResourceType == Resource.Wood && t.ResourceAmount > 0)
                    return (x, y);
        throw new InvalidOperationException("kein Baum mit Holzvorrat gefunden");
    }
}
