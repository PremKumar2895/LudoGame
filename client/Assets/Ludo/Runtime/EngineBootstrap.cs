using System.Linq;
using UnityEngine;
using Ludo.Core;

namespace LudoGame
{
    /// <summary>
    /// Drop this on a GameObject in a scene and press Play to prove the engine runs inside Unity.
    /// Plays a full 4-player AI game and logs the result to the Console. (Rendering comes next.)
    /// </summary>
    public sealed class EngineBootstrap : MonoBehaviour
    {
        [Tooltip("2..10 for Free-for-all; 4/6/8/10 for Teams.")]
        public int players = 4;
        public bool teams = false;
        public int seed = 7;

        private void Start()
        {
            var cfg = GameplayConfigLoader.Load();
            Debug.Log($"[Ludo] Engine online. maxPlayers={cfg.MaxPlayers}, homeEntryOffset={cfg.HomeEntryOffset}.");

            var mode = teams ? GameMode.Teams : GameMode.FreeForAll;
            var match = MatchFactory.Create(players, mode, cfg, id: "UNITY");
            var ctrl = new MatchController(match, new SeededDiceRoller(seed), new AutoPlayAI());
            ctrl.RunToCompletion();

            string result = match.Mode == GameMode.Teams
                ? $"winning team = {match.WinningTeam}"
                : "finish order (board pos) = " + string.Join(" > ", match.FinishOrder);
            Debug.Log($"[Ludo] {players}p {(teams ? "Teams" : "FFA")} finished in {match.TurnNumber} turns. {result}");
        }
    }
}

// recompile trigger 1782618079
