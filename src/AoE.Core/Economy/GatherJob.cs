using AoE.Core.Entities;

namespace AoE.Core.Economy;

/// <summary>
/// Phasen des Dorfbewohner-Kreislaufs aus der Spezifikation:
/// hinlaufen → sammeln bis Traglast 10 → zur nächsten Abgabestelle → zurück.
/// </summary>
public enum GatherPhase
{
    /// <summary>Unterwegs zur Quelle.</summary>
    ToSource,

    /// <summary>Steht an der Quelle und sammelt.</summary>
    Gathering,

    /// <summary>Unterwegs zur Abgabestelle.</summary>
    ToDropOff,

    /// <summary>Auftrag beendet — der Dorfbewohner ist untätig.</summary>
    Done
}

/// <summary>
/// Was ein Sammelauftrag von der Karte wissen muss. Das Spiel setzt es über
/// seine Kachelkarte um, die Tests über ein kleines Wörterbuch. Alle
/// Positionen sind Kachelkoordinaten.
/// </summary>
public interface IGatherWorld
{
    /// <summary>
    /// Restmenge von <paramref name="resource"/> auf der Kachel; 0, wenn dort
    /// nichts mehr oder eine andere Ressource liegt.
    /// </summary>
    int AmountAt(Position cell, Resource resource);

    /// <summary>
    /// Entnimmt höchstens <paramref name="amount"/> und gibt zurück, wie viel
    /// tatsächlich entnommen wurde. Leert sich die Kachel, verschwindet die Quelle.
    /// </summary>
    int Harvest(Position cell, Resource resource, int amount);

    /// <summary>
    /// Nächste Kachel mit Restmenge größer 0 von <paramref name="resource"/>,
    /// höchstens <paramref name="maxDistance"/> Kacheln entfernt, oder null.
    /// </summary>
    Position? FindNearestSource(Position from, Resource resource, int maxDistance);

    /// <summary>
    /// Die Kachel, auf die ein Dorfbewohner des Spielers zum Abliefern von
    /// <paramref name="resource"/> läuft — bei der nächstgelegenen passenden
    /// Abgabestelle —, oder null, wenn es keine gibt.
    /// </summary>
    Position? FindNearestDropOff(Position from, int ownerId, Resource resource);
}

/// <summary>
/// Sammelauftrag eines Dorfbewohners — reine Logik ohne MonoGame.
///
/// Das Spiel bewegt die Einheit zu <see cref="Destination"/>, meldet die
/// Ankunft mit <see cref="Arrive"/> und ruft während des Sammelns jeden Frame
/// <see cref="Update"/>. Den Rest — Traglast, Abgabe, Rückkehr, Suche nach
/// der nächsten gleichen Quelle — regelt der Auftrag selbst. Genau das ist
/// die „automatische Fortsetzung" der Spezifikation: der Spieler klickt einmal.
/// </summary>
public sealed class GatherJob
{
    /// <summary>Traglast eines Dorfbewohners laut Spezifikation.</summary>
    public const int CARRY_CAPACITY = 10;

    /// <summary>
    /// So weit (in Kacheln) sucht ein Dorfbewohner nach der nächsten gleichen
    /// Quelle, wenn seine erschöpft ist.
    /// </summary>
    public const int SEARCH_RADIUS = 8;

    /// <summary>
    /// Neuer Auftrag: der Dorfbewohner läuft zuerst zur Quelle. Die mitgebrachte
    /// Traglast wird auf 0 bis CARRY_CAPACITY begrenzt.
    /// </summary>
    public GatherJob(int ownerId, Resource resource, Position source, int carrying = 0)
    {
        Carrying = Math.Clamp(carrying, 0, CARRY_CAPACITY);
        OwnerId = ownerId;
        Resource = resource;
        Source = source;
        Phase = GatherPhase.ToSource;
    }

    public int OwnerId { get; }

    public Resource Resource { get; }

    /// <summary>Die Quelle, an der gesammelt wird oder zu der er gerade läuft.</summary>
    public Position Source { get; private set; }

    /// <summary>Die Abgabestelle, zu der er gerade läuft; sonst null.</summary>
    public Position? DropOff { get; private set; }

    public GatherPhase Phase { get; private set; }

    /// <summary>Aktuelle Traglast.</summary>
    public int Carrying { get; private set; }

    /// <summary>
    /// Wohin das Spiel die Einheit gerade bewegen soll: <see cref="Source"/> in
    /// <see cref="GatherPhase.ToSource"/>, <see cref="DropOff"/> in
    /// <see cref="GatherPhase.ToDropOff"/>, sonst null.
    /// </summary>
    public Position? Destination => Phase switch
    {
        GatherPhase.ToSource => Source,
        GatherPhase.ToDropOff => DropOff,
        _ => null
    };

    /// <summary>
    /// Sammelrate in Einheiten pro Sekunde, Richtwerte der Spezifikation:
    /// Nahrung 0,33 (Schafe), Holz 0,39, Gold 0,38, Stein 0,36, sonst 0.
    /// </summary>
    public static float RateOf(Resource resource) => resource switch
    {
        Resource.Food => 0.33f,
        Resource.Wood => 0.39f,
        Resource.Gold => 0.38f,
        Resource.Stone => 0.36f,
        _ => 0f
    };

    /// <summary>
    /// Das Spiel meldet: die Einheit hat <see cref="Destination"/> erreicht.
    ///
    /// In <see cref="GatherPhase.ToSource"/>: Liegt an der Quelle noch etwas,
    /// beginnt das Sammeln. Sonst wird wie bei einer erschöpften Quelle neu
    /// gesucht (siehe <see cref="Update"/>).
    ///
    /// In <see cref="GatherPhase.ToDropOff"/>: Die ganze Traglast wird
    /// abgeliefert — sie ist der Rückgabewert, das Spiel schreibt sie dem
    /// Spieler gut. Danach geht es zurück zur Quelle, solange sie etwas hat,
    /// sonst zur nächsten gleichen Quelle im <see cref="SEARCH_RADIUS"/> um die
    /// alte; gibt es keine, ist der Auftrag erledigt.
    ///
    /// In den anderen Phasen passiert nichts. Rückgabe dort und bei
    /// <see cref="GatherPhase.ToSource"/>: 0.
    /// </summary>
    public int Arrive(IGatherWorld world)
    {
        switch (Phase)
        {
            case GatherPhase.ToSource:
                if (world.AmountAt(Source, Resource) > 0)
                {
                    Phase = GatherPhase.Gathering;
                    _progress = 0;
                }
                else
                {
                    FindNextSource(world);
                }
                return 0;

            case GatherPhase.ToDropOff:
                int delivered = Carrying;
                Carrying = 0;
                DropOff = null;
                if (world.AmountAt(Source, Resource) > 0)
                    Phase = GatherPhase.ToSource;
                else
                    FindNextSource(world);
                return delivered;

            default:
                return 0;
        }
    }

    /// <summary>
    /// Sammelt, solange der Auftrag in <see cref="GatherPhase.Gathering"/> ist;
    /// in allen anderen Phasen passiert nichts.
    ///
    /// Gesammelt wird mit <see cref="RateOf"/> Einheiten pro Sekunde. Bruchteile
    /// werden über die Aufrufe hinweg angesammelt, entnommen wird in ganzen
    /// Einheiten über <see cref="IGatherWorld.Harvest"/> — nie über die
    /// Traglast hinaus, auch nicht bei großem <paramref name="dt"/>.
    ///
    /// Ist die Traglast voll, geht es zur nächsten Abgabestelle, gesucht von
    /// der Quelle aus; gibt es keine, ist der Auftrag erledigt und die
    /// Traglast bleibt.
    ///
    /// Ist die Quelle erschöpft, bevor die Traglast voll ist, geht es zur
    /// nächsten gleichen Quelle im <see cref="SEARCH_RADIUS"/> um die alte —
    /// die Traglast bleibt, gesammelt wird dort weiter. Gibt es keine, wird
    /// abgeliefert, was er trägt; trägt er nichts, ist der Auftrag erledigt.
    /// </summary>
    public void Update(float dt, IGatherWorld world)
    {
        if (Phase != GatherPhase.Gathering)
            return;

        _progress += dt * RateOf(Resource);

        // Ganze Einheiten entnehmen, nie über die Traglast hinaus.
        while (_progress >= 1 && Carrying < CARRY_CAPACITY)
        {
            int taken = world.Harvest(Source, Resource, 1);
            if (taken == 0)
                break;
            Carrying += taken;
            _progress -= 1;
        }

        // Volle Traglast geht vor erschöpfter Quelle.
        if (Carrying >= CARRY_CAPACITY)
            GoToDropOff(world);
        else if (world.AmountAt(Source, Resource) == 0)
            FindNextSource(world);
    }

    /// <summary>Angesammelte Bruchteile des Sammelns über die Aufrufe hinweg.</summary>
    private float _progress;

    /// <summary>
    /// Zur nächsten Abgabestelle laufen; gibt es keine, ist der Auftrag
    /// erledigt und die Traglast bleibt.
    /// </summary>
    private void GoToDropOff(IGatherWorld world)
    {
        DropOff = world.FindNearestDropOff(Source, OwnerId, Resource);
        Phase = DropOff is null ? GatherPhase.Done : GatherPhase.ToDropOff;
    }

    /// <summary>
    /// Nächste gleiche Quelle im <see cref="SEARCH_RADIUS"/> um die alte
    /// ansteuern; gibt es keine, wird abgeliefert, was er trägt — trägt er
    /// nichts, ist der Auftrag erledigt.
    /// </summary>
    private void FindNextSource(IGatherWorld world)
    {
        var next = world.FindNearestSource(Source, Resource, SEARCH_RADIUS);
        if (next is not null)
        {
            Source = next.Value;
            Phase = GatherPhase.ToSource;
        }
        else if (Carrying > 0)
        {
            GoToDropOff(world);
        }
        else
        {
            Phase = GatherPhase.Done;
        }
    }

    /// <summary>
    /// Das Spiel meldet: <see cref="Destination"/> ist nicht erreichbar. Der
    /// Auftrag ist damit erledigt; die Traglast bleibt.
    /// </summary>
    public void Unreachable()
    {
        Phase = GatherPhase.Done;
        DropOff = null;
    }
}
