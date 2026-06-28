using System.Text.Json;
using Ludo.Core;

namespace Ludo.Sim;

/// <summary>
/// Loads the SINGLE config file (config/ludo.config.json) into a GameConfig and reports which
/// external services are configured. Blank service IDs => local/offline mode.
/// In Unity this same JSON is read (via Newtonsoft/JsonUtility) and mapped the same way.
/// </summary>
public sealed class AppConfig
{
    public GameConfig Gameplay { get; private set; } = GameConfig.Default();
    public string AppName { get; private set; } = "Ludo Royale";
    public string Environment { get; private set; } = "development";
    public List<(string service, bool configured)> ServiceStatus { get; } = new();

    public static string? FindConfigPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var p = Path.Combine(dir.FullName, "config", "ludo.config.json");
            if (File.Exists(p)) return p;
            dir = dir.Parent;
        }
        return null;
    }

    public static AppConfig Load(string path)
    {
        var cfg = new AppConfig();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        if (root.TryGetProperty("app", out var app))
        {
            cfg.AppName = app.Str("name", cfg.AppName);
            cfg.Environment = app.Str("environment", cfg.Environment);
        }

        if (root.TryGetProperty("gameplay", out var gp))
        {
            var g = cfg.Gameplay;
            g.MaxPlayers = gp.Int("maxPlayers", g.MaxPlayers);
            g.TokensPerSeat = gp.Int("tokensPerSeat", g.TokensPerSeat);
            g.RingCellsPerArm = gp.Int("ringCellsPerArm", g.RingCellsPerArm);
            g.HomeEntryOffset = gp.Int("homeEntryOffset", g.HomeEntryOffset);
            g.HomeColumnLen = gp.Int("homeColumnLen", g.HomeColumnLen);
            g.SafeStarOffset = gp.Int("safeStarOffset", g.SafeStarOffset);
            g.ExactFinish = gp.Bool("exactFinish", g.ExactFinish);
            g.BonusOnSix = gp.Bool("bonusOnSix", g.BonusOnSix);
            g.BonusOnCapture = gp.Bool("bonusOnCapture", g.BonusOnCapture);
            g.BonusOnHome = gp.Bool("bonusOnHome", g.BonusOnHome);
            g.ThreeSixCancels = gp.Bool("threeSixCancels", g.ThreeSixCancels);
            g.BlocksEnabled = gp.Bool("blocksEnabled", g.BlocksEnabled);
            g.BlockBlocksPassage = gp.Bool("blockBlocksPassage", g.BlockBlocksPassage);
            g.TeamSharedTurns = gp.Bool("teamSharedTurns", g.TeamSharedTurns);
            g.TeamBlocks = gp.Bool("teamBlocks", g.TeamBlocks);
            g.AutoMoveSingle = gp.Bool("autoMoveSingle", g.AutoMoveSingle);
            g.TurnTimeoutSec = gp.Int("turnTimeoutSec", g.TurnTimeoutSec);
            g.TurnWarnSec = gp.Int("turnWarnSec", g.TurnWarnSec);
            g.PitySixEnabled = gp.Bool("pitySixEnabled", g.PitySixEnabled);
            if (gp.TryGetProperty("unlockRolls", out var ur) && ur.ValueKind == JsonValueKind.Array)
            {
                var list = new List<int>();
                foreach (var e in ur.EnumerateArray()) if (e.ValueKind == JsonValueKind.Number) list.Add(e.GetInt32());
                if (list.Count > 0) g.UnlockRolls = list.ToArray();
            }
        }

        if (root.TryGetProperty("services", out var sv))
        {
            void Check(string label, string node, string key)
            {
                bool ok = sv.TryGetProperty(node, out var n) && n.ValueKind == JsonValueKind.Object
                          && !string.IsNullOrWhiteSpace(n.Str(key, ""));
                cfg.ServiceStatus.Add((label, ok));
            }
            Check("Photon (online multiplayer)", "photon", "fusionAppId");
            Check("Agora (voice chat)", "agora", "appId");
            Check("Firebase (accounts + logging)", "firebase", "projectId");
            Check("Google Sign-In", "googleSignIn", "webClientId");
        }
        return cfg;
    }
}

internal static class JsonExt
{
    public static string Str(this JsonElement e, string name, string def)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : def;
    public static int Int(this JsonElement e, string name, int def)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
    public static bool Bool(this JsonElement e, string name, bool def)
        => e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : def;
}
