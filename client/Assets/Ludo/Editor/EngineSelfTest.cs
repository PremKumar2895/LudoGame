using UnityEditor;
using UnityEngine;
using Ludo.Core;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Menu: "Ludo > Run Engine Self-Test". Runs without entering Play mode — instant proof the
    /// engine compiles and works inside Unity across all board sizes.
    /// </summary>
    public static class EngineSelfTest
    {
        [MenuItem("Ludo/Run Engine Self-Test")]
        public static void Run()
        {
            int pass = 0, fail = 0;
            void Check(string name, bool ok)
            {
                if (ok) { pass++; Debug.Log($"  PASS  {name}"); }
                else { fail++; Debug.LogError($"  FAIL  {name}"); }
            }

            var cfg = GameplayConfigLoader.Load();
            Check("config loaded (maxPlayers 2..16)", cfg.MaxPlayers is >= 2 and <= 16);
            Check("ring(4) = 52", new BoardGeometry(4, cfg).Ring == 52);
            Check("ring(10) = 130", new BoardGeometry(10, cfg).Ring == 130);

            foreach (int n in new[] { 2, 4, 6, 8, 10 })
            {
                var m = MatchFactory.Create(n, GameMode.FreeForAll, cfg);
                new MatchController(m, new SeededDiceRoller(n * 7 + 1), new AutoPlayAI()).RunToCompletion();
                Check($"{n}p FFA completes", m.Phase == MatchPhase.Finished && m.FinishOrder.Count == n);
            }

            var t = MatchFactory.Create(10, GameMode.Teams, cfg);
            new MatchController(t, new SeededDiceRoller(99), new AutoPlayAI()).RunToCompletion();
            Check("10p Teams (5v5) completes with a winner", t.Phase == MatchPhase.Finished && t.WinningTeam.HasValue);

            Debug.Log($"[Ludo] Engine self-test: {pass} passed, {fail} failed.");
        }
    }
}
