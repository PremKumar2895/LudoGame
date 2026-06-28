using Ludo.Core;
using Ludo.Sim;

string? path = AppConfig.FindConfigPath();
AppConfig app = path != null ? AppConfig.Load(path) : new AppConfig();

Console.WriteLine($"=== {app.AppName} — local engine simulator ===");
Console.WriteLine($"config : {path ?? "(none found; using defaults)"}");
Console.WriteLine($"env    : {app.Environment}   maxPlayers: {app.Gameplay.MaxPlayers}");
Console.WriteLine("services:");
foreach (var (svc, ok) in app.ServiceStatus)
    Console.WriteLine($"   [{(ok ? "x" : " ")}] {svc}{(ok ? "" : "   (blank -> local/offline)")}");
bool anyOnline = app.ServiceStatus.Exists(s => s.configured);
Console.WriteLine(anyOnline
    ? "mode   : some services configured."
    : "mode   : LOCAL / OFFLINE (full engine + bots, no accounts needed).");
Console.WriteLine();

// ---- 1) short demo game ----
Console.WriteLine("--- Demo: 4-player FFA vs bots (first 18 log lines) ---");
var demo = MatchFactory.Create(4, GameMode.FreeForAll, app.Gameplay, id: "DEMO");
int shown = 0;
var ctrl = new MatchController(demo, new SeededDiceRoller(7), new AutoPlayAI())
{
    Log = msg => { if (shown++ < 18) Console.WriteLine(msg); }
};
ctrl.RunToCompletion();
Console.WriteLine($"  ...finished in {demo.TurnNumber} turns. Finish order (board pos): {string.Join(" > ", demo.FinishOrder)}");
Console.WriteLine();

// ---- 2) stability sweep across every board size & mode ----
Console.WriteLine("--- Stability sweep: 300 AI games per configuration ---");
Console.WriteLine($"{"configuration",-22}{"games",6}{"done%",7}{"avgTurns",10}{"min",6}{"max",6}{"avgKicks",10}");

void Sweep(string label, int players, GameMode mode)
{
    const int games = 300;
    int done = 0, mn = int.MaxValue, mx = 0;
    long turns = 0, kicks = 0;
    for (int i = 0; i < games; i++)
    {
        var st = MatchFactory.Create(players, mode, app.Gameplay, id: $"S{i}");
        int k = 0;
        var c = new MatchController(st, new SeededDiceRoller(1000 + i), new AutoPlayAI())
        {
            Log = m => { if (m.Contains("KICK")) k++; }
        };
        c.RunToCompletion();
        if (st.Phase == MatchPhase.Finished) done++;
        turns += st.TurnNumber; kicks += k;
        mn = Math.Min(mn, st.TurnNumber);
        mx = Math.Max(mx, st.TurnNumber);
    }
    Console.WriteLine($"{label,-22}{games,6}{100.0 * done / games,6:0}%{(double)turns / games,10:0.0}{mn,6}{mx,6}{(double)kicks / games,10:0.0}");
}

Sweep("2p FFA (classic)", 2, GameMode.FreeForAll);
Sweep("4p FFA (classic)", 4, GameMode.FreeForAll);
Sweep("6p FFA (hex)", 6, GameMode.FreeForAll);
Sweep("8p FFA (oct)", 8, GameMode.FreeForAll);
Sweep("10p FFA (decagon)", 10, GameMode.FreeForAll);
Sweep("4p Teams 2v2", 4, GameMode.Teams);
Sweep("6p Teams 3v3", 6, GameMode.Teams);
Sweep("8p Teams 4v4", 8, GameMode.Teams);
Sweep("10p Teams 5v5", 10, GameMode.Teams);

Console.WriteLine();
Console.WriteLine("If done% = 100 across the board, the engine completes every size & mode without errors.");
