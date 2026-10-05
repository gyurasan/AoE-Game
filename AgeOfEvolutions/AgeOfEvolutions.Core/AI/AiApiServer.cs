using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using AoE.Core.Ai;
using AoE.Core.Entities;
using AgeOfEvolutions.Core.Screens;
using Age = AoE.Core.Economy.Age;

namespace AgeOfEvolutions.Core.AI;

/// <summary>
/// Ein lokaler HTTP-Server für die KI-Steuerung des Spiels.
///
/// Nur auf 127.0.0.1 — läuft nicht ins Netz, sondern für lokale Steuerungen
/// (ein LLM, ein Skript, die eingebaute KI als externe Instanz).
///
/// Endpunkte:
///   GET  /state?owner=1        Weltzustand (Snapshot) als JSON
///   POST /order?owner=1        Befehl — JSON-Body, z. B.
///        {"action":"gather","unit":0,"x":10,"y":22}
///        {"action":"build","type":"Farm","x":30,"y":33,"builders":[2,3]}
///        {"action":"train","villagers":2}
///        {"action":"age"}
///        {"action":"assign","unit":0,"building":1}
///
/// Antworten JSON: 200 = angenommen (wirkt ab nächstem Spiel-Frame),
/// 400 = Fehleingabe, 500 = Serverfehler.
/// </summary>
public sealed class AiApiServer : IDisposable
{
    public const int DefaultPort = 8080;

    private readonly RTSGameplayScreen _screen;
    private readonly string _prefix;
    private readonly HttpListener _listener;
    private readonly Thread _thread;
    private volatile bool _stop;

    public AiApiServer(RTSGameplayScreen screen, int port = DefaultPort)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port muss 1..65535 sein.");

        _prefix = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_prefix);
        _thread = new Thread(Loop) { IsBackground = true, Name = "AiApi" };
    }

    public string Prefix => _prefix;
    public bool IsRunning => !_stop && _thread.IsAlive;

    public void Start()
    {
        try { _listener.Start(); }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"API kann nicht auf {_prefix} lauschen: {ex.Message}", ex);
        }
        _thread.Start();
        System.Diagnostics.Debug.WriteLine($"[AI-API] Lauscht auf {_prefix}");
    }

    public void Stop()
    {
        _stop = true;
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
    }

    public void Dispose() => Stop();

    // ------------------- Loop -------------------

    private void Loop()
    {
        while (!_stop && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = _listener.GetContext(); }
            catch { return; }

            try { Handle(ctx); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI-API] Fehler: {ex.Message}");
                try { Send(ctx, 500, new { error = "Interner Fehler", detail = ex.Message }); }
                catch { }
            }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        // RawUrl trägt die Query-String mit (z. B. "/state?owner=1") — für den
        // Pfad-Vergleich nur die reine Wegkomponente nehmen.
        string raw = ctx.Request.RawUrl ?? "/";
        int q = raw.IndexOf('?');
        string path = q >= 0 ? raw[..q] : raw;
        string method = ctx.Request.HttpMethod ?? "GET";

        if (method == "GET")
        {
            if (path == "/" || path == "/index") HandleIndex(ctx);
            else if (path == "/state" || path == "/state/") HandleState(ctx, GetOwner(ctx.Request));
            else Send(ctx, 404, new { error = "Nicht gefunden",
                hilfe = "GET /state?owner=1  oder  POST /order?owner=1" });
            return;
        }

        if (method == "POST" && (path == "/order" || path == "/order/"))
        {
            HandleOrder(ctx, GetOwner(ctx.Request));
            return;
        }

        Send(ctx, method == "GET" || method == "POST" ? 404 : 405,
             new { error = "Pfad oder Methode nicht erlaubt" });
    }

    // ------------------- Handlers -------------------

    private static int GetOwner(HttpListenerRequest req)
    {
        var s = req.QueryString["owner"];
        if (!string.IsNullOrEmpty(s) && int.TryParse(s, out int o)) return o;
        return 1; // standardmäßig der KI-Gegner
    }

    private void HandleIndex(HttpListenerContext ctx)
    {
        Send(ctx, 200, new
        {
            name = "AoE KI-API",
            endpoints = new[]
            {
                "GET  /state?owner=1   Weltzustand als JSON",
                "POST /order?owner=1   Befehl senden",
            },
            beispiel = new
            {
                cmd = "POST /order?owner=1",
                body = new { action = "gather", unit = 0, x = 10, y = 22 },
            },
        });
    }

    private void HandleState(HttpListenerContext ctx, int owner)
    {
        var s = new AiBridge(_screen) { Owner = owner };
        Send(ctx, 200, new StateDto
        {
            owner        = s.Owner,
            width        = s.Width,
            height       = s.Height,
            resources    = Res(s.Resources),
            age          = s.Age.ToString().ToLowerInvariant(),
            ageTarget    = s.AgeTarget?.ToString().ToLowerInvariant(),
            ageProgress  = s.AgeProgress,
            popCount     = s.PopulationCount,
            popCap       = s.PopulationCapacity,
            villageTrain = s.VillagerTrainingCount,
            townCenter   = s.TownCenter == null ? null : Bld(s.TownCenter),
            units        = s.Units.Select(Unt).ToList(),
            buildings    = s.Buildings.Select(Bld).ToList(),
        });
    }

    private void HandleOrder(HttpListenerContext ctx, int owner)
    {
        using var reader = new System.IO.StreamReader(ctx.Request.InputStream, Encoding.UTF8);
        string body = reader.ReadToEnd();

        OrderBody? order;
        try { order = JsonSerializer.Deserialize<OrderBody>(body); }
        catch (Exception ex) { Send(ctx, 400, new { error = "JSON fehlerhaft", detail = ex.Message }); return; }
        if (order is null || string.IsNullOrEmpty(order.Action))
        {
            Send(ctx, 400, new { error = "Feld 'action' fehlt." });
            return;
        }

        // Validierung vor jeder Queue-Uebernahme.
        switch (order.Action.ToLowerInvariant())
        {
            case "gather" or "move":
                if (order.Unit is null) { Send(ctx, 400, new { error = "'unit' fehlt." }); return; }
                if (order.X is null || order.Y is null) { Send(ctx, 400, new { error = "'x' und 'y' fehlen." }); return; }
                break;
            case "build":
                if (order.Type is null || !Enum.TryParse<BuildingType>(order.Type, true, out _))
                { Send(ctx, 400, new { error = "'type' fehlt oder unbekanntes Gebäude." }); return; }
                if (order.X is null || order.Y is null) { Send(ctx, 400, new { error = "'x' und 'y' fehlen." }); return; }
                break;
            case "assign":
                if (order.Unit is null) { Send(ctx, 400, new { error = "'unit' fehlt." }); return; }
                if (order.Building is null) { Send(ctx, 400, new { error = "'building' fehlt." }); return; }
                break;
            case "train":
                int n = order.Villagers ?? 1;
                if (n < 1 || n > 10) { Send(ctx, 400, new { error = "'villagers' muss 1..10 sein." }); return; }
                break;
            case "age" or "ai-enable" or "ai-disable":
                break;
            default:
                Send(ctx, 400, new
                {
                    error = $"Unbekannte Aktion '{order.Action}'.",
                    erlaubt = new[] { "gather", "move", "build", "train", "age", "assign", "ai-enable", "ai-disable" },
                });
                return;
        }

        // Die Bridge ruft die Screen-Methoden; EnqueueOrder schiebt sie in
        // die Order-Queue, die im nächsten Update-Frame (Spiel-Thread) läuft.
        var a = new AiBridge(_screen) { Owner = owner };

        switch (order.Action.ToLowerInvariant())
        {
            case "gather":
                _screen.EnqueueOrder(() => a.Gather(order.Unit!.Value, order.X!.Value, order.Y!.Value));
                break;
            case "move":
                _screen.EnqueueOrder(() => a.Move(order.Unit!.Value, order.X!.Value, order.Y!.Value));
                break;
            case "build":
                var btype = Enum.Parse<BuildingType>(order.Type!, true);
                var ids = order.Builders ?? Array.Empty<int>();
                _screen.EnqueueOrder(() => a.Build(btype, order.X!.Value, order.Y!.Value, ids));
                break;
            case "train":
                int cnt = order.Villagers ?? 1;
                for (int i = 0; i < cnt; i++) _screen.EnqueueOrder(() => a.TrainVillager());
                break;
            case "age":
                _screen.EnqueueOrder(() => a.AdvanceAge());
                break;
            case "assign":
                _screen.EnqueueOrder(() => a.AssignBuilder(order.Unit!.Value, order.Building!.Value));
                break;
            case "ai-enable":
                var name = (order.AiType ?? "economy").ToLowerInvariant();
                if (name != "economy") { Send(ctx, 400, new { error = $"Nur 'economy' bekannt, nicht '{name}'." }); return; }
                _screen.EnqueueOrder(() => _screen.ActivateAi(new EconomyAi(), owner: owner));
                break;
            case "ai-disable":
                _screen.EnqueueOrder(() => _screen.ActivateAi(null, owner: owner));
                break;
        }

        Send(ctx, 200, new { ok = true, message = "übernommen — wirkt ab nächstem Spiel-Frame" });
    }

    // ------------------- DTOs -------------------

    private static Dictionary<string, int> Res(ResourceVector v) => new()
    {
        ["food"]  = v.Food,
        ["wood"]  = v.Wood,
        ["gold"]  = v.Gold,
        ["stone"] = v.Stone,
    };

    private static UnitDto Unt(AoE.Core.Ai.UnitSnapshot u)
    {
        string? at = null;
        int? atX = null;
        int? atY = null;
        if (u.Gathering is { } g)
        {
            at = g.Item3.ToString().ToLowerInvariant();
            atX = g.Item1;
            atY = g.Item2;
        }
        return new UnitDto
        {
            id         = u.Id,
            kind       = u.Kind.ToString().ToLowerInvariant(),
            state      = u.State.ToString().ToLowerInvariant(),
            x          = u.X,
            y          = u.Y,
            hp         = u.Health,
            maxHp      = u.MaxHealth,
            gathering  = at,
            gatheringX = atX,
            gatheringY = atY,
            buildingId = u.BuildingId,
        };
    }

    private static BuildingDto Bld(AoE.Core.Ai.BuildingSnapshot b) => new()
    {
        id       = b.Id,
        type     = b.Type.ToString().ToLowerInvariant(),
        x        = b.X,
        y        = b.Y,
        size     = b.Width,
        complete = b.IsComplete,
        progress = b.ConstructionProgress,
        builders = b.ActiveBuilders,
        hp       = b.Health,
        maxHp    = b.MaxHealth,
    };

    private sealed class StateDto
    {
        public int owner { get; init; }
        public int width { get; init; }
        public int height { get; init; }
        public Dictionary<string, int> resources { get; init; } = new();
        public string age { get; init; } = "";
        public string? ageTarget { get; init; }
        public double ageProgress { get; init; }
        public int popCount { get; init; }
        public int popCap { get; init; }
        public int villageTrain { get; init; }
        public BuildingDto? townCenter { get; init; }
        public List<UnitDto> units { get; init; } = new();
        public List<BuildingDto> buildings { get; init; } = new();
    }

    private sealed class UnitDto
    {
        public int id { get; init; }
        public string? kind { get; init; }
        public string? state { get; init; }
        public int x { get; init; }
        public int y { get; init; }
        public int hp { get; init; }
        public int maxHp { get; init; }
        public string? gathering { get; init; }   // Ressource beim Sammeln, sonst null
        public int? gatheringX { get; init; }
        public int? gatheringY { get; init; }
        public int? buildingId { get; init; }     // Baustelle oder null
    }

    private sealed class BuildingDto
    {
        public int id { get; init; }
        public string? type { get; init; }
        public int x { get; init; }
        public int y { get; init; }
        public int size { get; init; }
        public bool complete { get; init; }
        public double progress { get; init; }
        public int builders { get; init; }
        public int hp { get; init; }
        public int maxHp { get; init; }
    }

    // ------------------- Request-Body -------------------

    /// <summary>Body für <c>POST /order</c>. Nur <see cref="Action"/> ist Pflicht.</summary>
    public sealed class OrderBody
    {
        public string? Action { get; set; }
        public int?    Unit   { get; set; }
        public int?    X      { get; set; }
        public int?    Y      { get; set; }
        public int?    Building { get; set; }
        public string? Type     { get; set; }
        public int?    Villagers { get; set; }
        public int[]?  Builders  { get; set; }
        public string? AiType    { get; set; }
        public int?    MaxDistance { get; set; }
    }

    // ------------------- Antwort -------------------

    private void Send(HttpListenerContext ctx, int code, object body)
    {
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body));
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = payload.Length;
        try { ctx.Response.OutputStream.Write(payload, 0, payload.Length); }
        finally { ctx.Response.OutputStream.Close(); }
    }
}
