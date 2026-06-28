using System.Collections.Generic;
using System.Linq;
using Ludo.Core;

Console.WriteLine("=== Ludo.Core test suite ===\n");

// ---------------------------------------------------------------- geometry
T.Run("geometry: ring sizes, final progress & safe counts", () =>
{
    var cfg = GameConfig.Default();
    (int n, int ring)[] cases = { (4, 52), (6, 78), (8, 104), (10, 130) };
    foreach (var (n, ring) in cases)
    {
        var g = new BoardGeometry(n, cfg);
        T.Eq(g.Ring, ring, $"ring for N={n}");
        T.Eq(g.FinalHomeProgress, 56, $"final progress N={n}");
        T.Eq(g.SafeCount, 2 * n, $"safe count N={n}");
    }
});

T.Run("geometry: start cells & cell resolution", () =>
{
    var g = new BoardGeometry(4, GameConfig.Default());
    T.Eq(g.StartIndex(0), 0); T.Eq(g.StartIndex(1), 13); T.Eq(g.StartIndex(2), 26); T.Eq(g.StartIndex(3), 39);
    T.Eq(g.Resolve(0, 0).Kind, CellKind.Ring);
    T.Eq(g.Resolve(0, 0).RingIndex, 0);
    T.Eq(g.Resolve(1, 0).RingIndex, 13);
    var home = g.Resolve(0, 56);
    T.Eq(home.Kind, CellKind.Home, "progress 56 is home");
    T.True(g.IsHome(56) && !g.IsHome(55), "home boundary");
});

// ---------------------------------------------------------------- unlock
T.Run("unlock requires a 6", () =>
{
    var s = MatchFactory.Create(4, GameMode.FreeForAll, id: "U");
    var seat = s.SeatAt(0)!;                                  // all 4 tokens in base
    T.Eq(RulesEngine.GetLegalMoves(s, seat, 3).Count, 0, "no move on a 3");
    var six = RulesEngine.GetLegalMoves(s, seat, 6);
    T.Eq(six.Count, 4, "all four base tokens can unlock on a 6");
    foreach (var m in six) T.True(m.IsUnlock && m.ToProgress == 0, "unlock lands on start");
});

// ---------------------------------------------------------------- exact finish
T.Run("exact finish required; overshoot illegal", () =>
{
    var s = MatchFactory.Create(2, GameMode.FreeForAll, id: "E");
    var seat = s.SeatAt(0)!;
    seat.Tokens[0].Progress = 54;                              // two from home (56)
    for (int i = 1; i < 4; i++) seat.Tokens[i].Progress = 56;  // others home
    T.Eq(RulesEngine.GetLegalMoves(s, seat, 3).Count, 0, "3 overshoots -> no move");
    var two = RulesEngine.GetLegalMoves(s, seat, 2);
    T.Eq(two.Count, 1, "2 reaches home exactly");
    T.Eq(two[0].ToProgress, 56);
});

// ---------------------------------------------------------------- capture
T.Run("capture sends victim to base and grants a bonus", () =>
{
    var s = MatchFactory.Create(4, GameMode.FreeForAll, id: "C");
    var a = s.SeatAt(0)!; var b = s.SeatAt(1)!;
    a.Tokens[0].Progress = 20;                                 // global ring cell 20 (non-safe)
    b.Tokens[0].Progress = 1;
    var move = new Move(b.BoardPos, 0, 1, 7, false);           // b: 1 -> 7  => global (13+7)=20
    var r = RulesEngine.ApplyMove(s, b, move, 6);
    T.Eq(r.Captures.Count, 1, "exactly one capture");
    T.Eq(a.Tokens[0].Progress, -1, "victim back in base");
    T.True(r.GrantsBonus, "capture grants a bonus turn");
});

T.Run("no capture on a safe cell", () =>
{
    var s = MatchFactory.Create(4, GameMode.FreeForAll, id: "S");
    var a = s.SeatAt(0)!; var b = s.SeatAt(1)!;
    a.Tokens[0].Progress = 8;                                  // global 8 = a safe star cell
    b.Tokens[0].Progress = 41;
    var move = new Move(b.BoardPos, 0, 41, 47, false);         // global (13+47)%52 = 8
    var r = RulesEngine.ApplyMove(s, b, move, 6);
    T.Eq(r.Captures.Count, 0, "safe cell: no capture");
    T.Eq(a.Tokens[0].Progress, 8, "victim untouched on safe cell");
});

// ---------------------------------------------------------------- blocks
T.Run("opponent block stops landing", () =>
{
    var s = MatchFactory.Create(4, GameMode.FreeForAll, id: "B");
    var a = s.SeatAt(0)!; var b = s.SeatAt(1)!;
    b.Tokens[0].Progress = 5; b.Tokens[1].Progress = 5;        // block at global (13+5)=18
    a.Tokens[0].Progress = 12;                                 // 12 + 6 = 18 -> blocked
    var moves = RulesEngine.GetLegalMoves(s, a, 6);
    foreach (var m in moves) T.True(m.TokenId != 0, "token 0 cannot land on the block");
    T.True(RulesEngine.IsOpponentBlock(s, a, 18), "cell 18 is a block");
});

// ---------------------------------------------------------------- three sixes
T.Run("three consecutive sixes forfeits the turn", () =>
{
    var s = MatchFactory.Create(2, GameMode.FreeForAll, id: "T");
    var seat = s.SeatAt(0)!;                                   // current seat
    seat.Tokens[0].Progress = 10;
    for (int i = 1; i < 4; i++) seat.Tokens[i].Progress = 56;  // only token0 can move (auto-move)
    var ctrl = new MatchController(s, new ScriptedDiceRoller(1, 6, 6, 6), new AutoPlayAI());
    ctrl.PlayTurn();
    T.Eq(seat.Tokens[0].Progress, 22, "moved twice (10->16->22), third six cancelled");
    T.Eq(s.Phase, MatchPhase.InProgress);
});

// ---------------------------------------------------------------- teams
T.Run("teammates do not capture each other", () =>
{
    var s = MatchFactory.Create(4, GameMode.Teams, id: "TM");  // teams by parity: pos0 & pos2 = team0
    var p0 = s.SeatAt(0)!; var p2 = s.SeatAt(2)!;
    T.Eq(p0.TeamId, p2.TeamId, "0 and 2 are teammates");
    p0.Tokens[0].Progress = 20;                                // global 20
    p2.Tokens[0].Progress = 40;
    var move = new Move(p2.BoardPos, 0, 40, 46, false);        // global (26+46)%52 = 20
    var r = RulesEngine.ApplyMove(s, p2, move, 6);
    T.Eq(r.Captures.Count, 0, "no capture of a teammate");
    T.Eq(p0.Tokens[0].Progress, 20, "teammate token untouched");
});

T.Run("teams reject odd counts; FFA rejects out-of-range", () =>
{
    T.Throws(() => MatchFactory.Create(5, GameMode.Teams), "teams need a full board");
    T.Throws(() => MatchFactory.Create(1, GameMode.FreeForAll), "min 2");
    T.Throws(() => MatchFactory.Create(11, GameMode.FreeForAll), "max 10");
});

// ---------------------------------------------------------------- full games
T.Run("every FFA size completes with a full ranking", () =>
{
    foreach (int n in new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 })
        for (int seed = 0; seed < 5; seed++)
        {
            var st = MatchFactory.Create(n, GameMode.FreeForAll, id: $"F{n}.{seed}");
            new MatchController(st, new SeededDiceRoller(seed * 31 + n), new AutoPlayAI()).RunToCompletion();
            T.Eq(st.Phase, MatchPhase.Finished, $"FFA n={n} seed={seed} finished");
            T.Eq(st.FinishOrder.Count, n, $"FFA n={n} ranked everyone");
        }
});

T.Run("every team size completes with a winning team", () =>
{
    foreach (int n in new[] { 4, 6, 8, 10 })
        for (int seed = 0; seed < 5; seed++)
        {
            var st = MatchFactory.Create(n, GameMode.Teams, id: $"T{n}.{seed}");
            new MatchController(st, new SeededDiceRoller(seed * 17 + n), new AutoPlayAI()).RunToCompletion();
            T.Eq(st.Phase, MatchPhase.Finished, $"Teams n={n} seed={seed} finished");
            T.True(st.WinningTeam.HasValue, $"Teams n={n} seed={seed} has a winner");
        }
});

// ---------------------------------------------------------------- networking core (Phase 3)
T.Run("snapshot round-trips through serialization", () =>
{
    var s = MatchFactory.Create(6, GameMode.FreeForAll, id: "SNAP");
    s.SeatAt(0)!.Tokens[0].Progress = 20;
    s.SeatAt(2)!.Tokens[1].Progress = 56;
    s.LastRoll = 4; s.TurnNumber = 9;
    var snap = MatchSnapshot.Capture(s, TurnSub.AwaitingMove, new[] { new Move(0, 0, 14, 18, false) });
    var bytes = snap.Serialize();
    var back = MatchSnapshot.Deserialize(bytes);
    T.True(bytes.SequenceEqual(back.Serialize()), "byte round-trip");
    T.Eq(back.BoardN, (byte)6);
    T.Eq(back.Seats.Count, 6);
    T.Eq(back.Seats[0].Tokens[0], (sbyte)20);
    T.Eq(back.MovableTokens.Count, 1);
});

T.Run("authoritative server completes an all-bot match", () =>
{
    var s = MatchFactory.Create(4, GameMode.FreeForAll, id: "SRV");
    int snaps = 0;
    var server = new AuthoritativeMatchServer(s, new SeededDiceRoller(5), new AutoPlayAI(), botThinkSeconds: 0f);
    server.OnSnapshot += _ => snaps++;
    server.Start();
    server.RunHeadless();
    T.Eq(s.Phase, MatchPhase.Finished, "match finished");
    T.Eq(s.FinishOrder.Count, 4, "ranked everyone");
    T.True(snaps > 10, "emitted snapshots");
});

T.Run("server validates intents (seat, token, phase)", () =>
{
    var players = new List<PlayerSlot> { new PlayerSlot { Name = "You", IsBot = false }, new PlayerSlot { Name = "Bot", IsBot = true } };
    var s = MatchFactory.Create(2, GameMode.FreeForAll, null, players, "VAL");
    var server = new AuthoritativeMatchServer(s, new ScriptedDiceRoller(3, 6), new AutoPlayAI(), botThinkSeconds: 0f);
    server.Start();
    T.True(server.Sub == TurnSub.AwaitingRoll, "starts awaiting roll");
    T.True(!server.Submit(PlayerIntent.Move(0, 0)), "cannot move before rolling");
    T.True(!server.Submit(PlayerIntent.Roll(1)), "cannot act for another seat");
    T.True(server.Submit(PlayerIntent.Roll(0)), "valid roll accepted");
    T.True(server.Sub == TurnSub.AwaitingMove, "now awaiting a token choice");
    T.True(!server.Submit(PlayerIntent.Move(0, 9)), "invalid token rejected");
    T.True(server.Submit(PlayerIntent.Move(0, 0)), "valid token accepted");
    T.Eq(s.SeatAt(0)!.Tokens[0].Progress, 0, "token unlocked to start");
});

T.Run("human timeout auto-plays and the match still completes", () =>
{
    var players = new List<PlayerSlot> { new PlayerSlot { Name = "AFK", IsBot = false }, new PlayerSlot { Name = "Bot", IsBot = true } };
    var s = MatchFactory.Create(2, GameMode.FreeForAll, null, players, "AFK");
    int autoCount = 0;
    var server = new AuthoritativeMatchServer(s, new SeededDiceRoller(11), new AutoPlayAI(), turnTimeoutSec: 60f, botThinkSeconds: 0f);
    server.OnAutoPlayed += (_, human) => { if (human) autoCount++; };
    server.Start();
    server.RunHeadless();
    T.Eq(s.Phase, MatchPhase.Finished, "completed via auto-play");
    T.True(autoCount > 0, "human was auto-played at least once");
});

T.Run("promoting the current seat to human resets its turn timer (no instant timeout)", () =>
{
    // Online flow: seat 0 begins the match as a bot, then the host promotes it to human mid-turn.
    var s = MatchFactory.Create(2, GameMode.FreeForAll, id: "PROMO"); // all-bot by default
    int humanTimeouts = 0;
    var server = new AuthoritativeMatchServer(s, new SeededDiceRoller(7), new AutoPlayAI(),
                                              turnTimeoutSec: 60f, botThinkSeconds: 0.5f);
    server.OnAutoPlayed += (_, human) => { if (human) humanTimeouts++; };
    server.Start();                       // seat 0 begins its turn as a bot (0.5s think timer)

    s.SeatAt(0)!.IsBot = false;           // controller flips to human...
    server.OnSeatChanged(0);              // ...and the timer is refreshed to the human timeout

    server.Tick(1f);                      // longer than bot-think, far shorter than the 60s human timeout
    T.Eq(humanTimeouts, 0, "freshly-promoted human is not instantly timed out");
    T.True(!server.Finished, "match still in progress, awaiting the human");
    T.True(server.Sub == TurnSub.AwaitingRoll, "still awaiting the human's roll");
});

T.Run("snapshot rebuilds a MatchState mirror (for client rendering)", () =>
{
    var s = MatchFactory.Create(6, GameMode.Teams, id: "MIR");
    s.SeatAt(0)!.Tokens[2].Progress = 33;
    s.SeatAt(3)!.Tokens[0].Progress = 56;
    var snap = MatchSnapshot.Capture(s, TurnSub.AwaitingRoll, null);
    var mirror = snap.ToState(GameConfig.Default());
    T.Eq(mirror.Geo.SeatCount, 6);
    T.Eq(mirror.Seats.Count, 6);
    T.Eq(mirror.SeatAt(0)!.Tokens[2].Progress, 33);
    T.Eq(mirror.SeatAt(3)!.Tokens[0].Progress, 56);
    T.Eq(mirror.Mode, GameMode.Teams);
});

return T.Summary();

// ============================================================ tiny harness
static class T
{
    static int _pass, _fail;

    public static void Run(string name, Action body)
    {
        try { body(); _pass++; Console.WriteLine($"  PASS  {name}"); }
        catch (Exception ex) { _fail++; Console.WriteLine($"  FAIL  {name}\n          -> {ex.Message}"); }
    }

    public static void True(bool cond, string msg = "")
    { if (!cond) throw new Exception("expected true. " + msg); }

    public static void Eq(object? actual, object? expected, string msg = "")
    { if (!Equals(actual, expected)) throw new Exception($"expected <{expected}> but got <{actual}>. {msg}"); }

    public static void Throws(Action a, string msg = "")
    { try { a(); } catch { return; } throw new Exception("expected an exception. " + msg); }

    public static int Summary()
    {
        Console.WriteLine($"\n{_pass} passed, {_fail} failed.");
        return _fail == 0 ? 0 : 1;
    }
}
