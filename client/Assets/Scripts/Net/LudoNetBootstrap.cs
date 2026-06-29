// Ludo Royale — create/join lobby (OnGUI). In its OWN file so the MonoBehaviour script reference
// stays stable across reimports (Unity drops references for classes whose file name doesn't match).
#if FUSION2
using Fusion;
using UnityEngine;
using LudoGame.UI;

namespace LudoGame.Net
{
    /// <summary>Minimal create/join lobby (OnGUI). Add to a GameObject; the host spawns the session prefab.</summary>
    public sealed class LudoNetBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkObject _sessionPrefab; // assigned by the editor setup script
        public int players = 4;
        public bool teams = false;

        private NetworkRunner _runner;
        private string _room = "LUDO1";
        private string _status = "";
        private bool _busy; // guards against double-clicking Host/Join mid-connect

        private static readonly int[] TeamCounts = { 4, 6, 8, 10 }; // team mode needs a full board

        private async void StartGame(GameMode gameMode)
        {
            if (_busy) return;
            _busy = true;

            LudoNetSession.StartPlayers = players;
            LudoNetSession.StartTeams = teams;
            LudoNetSession.LocalName = PlayerProfile.Name;

            _runner = GetComponent<NetworkRunner>();
            if (_runner == null) _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            _status = $"{gameMode} joining '{_room}'…";

            var result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = _room,
                PlayerCount = players,
            });

            _status = result.Ok ? $"{gameMode} connected." : $"Failed: {result.ShutdownReason}";

            if (result.Ok)
            {
                NotificationCenter.Push(gameMode == GameMode.Host ? $"Hosting room {_room}" : $"Joined room {_room}", NoticeKind.Success);
                if (gameMode == GameMode.Host && _sessionPrefab != null) _runner.Spawn(_sessionPrefab);
            }
            else
            {
                NotificationCenter.Push($"Connection failed: {result.ShutdownReason}", NoticeKind.Error, 6f);
                _busy = false; // allow a retry on failure
            }
        }

        private void OnGUI()
        {
            UIScale.Apply();
            GUILayout.BeginArea(new Rect(12, 12, 270, 270), GUI.skin.box);
            GUILayout.Label("Ludo Royale — Online (Fusion)");
            GUILayout.Label("Your name:");
            PlayerProfile.Name = GUILayout.TextField(PlayerProfile.Name);
            GUILayout.Label("Room code:");
            _room = GUILayout.TextField(_room);

            bool t = GUILayout.Toggle(teams, "Teams (even seats vs odd)");
            if (t != teams) { teams = t; if (teams) players = NearestTeamCount(players); }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Players", GUILayout.Width(52));
            if (teams)
            {
                foreach (int n in TeamCounts)
                    if (GUILayout.Toggle(players == n, n.ToString(), GUI.skin.button)) players = n;
            }
            else
            {
                players = Mathf.Clamp(int.TryParse(GUILayout.TextField(players.ToString(), GUILayout.Width(40)), out var p) ? p : players, 2, 10);
                GUILayout.Label("(2–10)");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            if (GUILayout.Button("Host")) StartGame(GameMode.Host);
            if (GUILayout.Button("Join")) StartGame(GameMode.Client);
            GUILayout.Label(_status);
            GUILayout.EndArea();
        }

        private static int NearestTeamCount(int p)
        {
            int best = 4, bestD = int.MaxValue;
            foreach (int n in TeamCounts) { int d = Mathf.Abs(n - p); if (d < bestD) { bestD = d; best = n; } }
            return best;
        }
    }
}
#endif
