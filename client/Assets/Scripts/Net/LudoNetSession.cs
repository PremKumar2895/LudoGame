// Ludo Royale — Photon Fusion 2 transport (Phase 3). Gated on FUSION2.
// Host owns the authoritative server; clients send intents and receive snapshots.
#if FUSION2
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using LudoGame.UI;
using LudoCore = Ludo.Core;

namespace LudoGame.Net
{
    public sealed class LudoNetSession : NetworkBehaviour
    {
        public static LudoNetSession Instance { get; private set; }

        /// <summary>Fires on every client (and host) with the latest authoritative snapshot.</summary>
        public event Action<LudoCore.MatchSnapshot> OnSnapshot;
        public LudoCore.MatchSnapshot LastSnapshot { get; private set; }

        /// <summary>The board seat this local instance controls (assigned by the host as players join).</summary>
        public int LocalSeat { get; private set; }

        // Set by the lobby before the host spawns this object.
        public static int StartPlayers = 4;
        public static bool StartTeams = false;

        private LudoCore.AuthoritativeMatchServer _server;          // host-only
        private readonly Dictionary<PlayerRef, int> _playerSeats = new Dictionary<PlayerRef, int>();
        private int _nextSeat;

        // resilience bookkeeping (host)
        private readonly HashSet<PlayerRef> _present = new HashSet<PlayerRef>();
        private readonly List<PlayerRef> _left = new List<PlayerRef>();
        private readonly Queue<int> _vacated = new Queue<int>(); // board positions freed by disconnects, reclaimed on rejoin
        // connection monitoring (client)
        private bool _wasConnected = true;
        private float _lastRttCheck;

        public override void Spawned()
        {
            Instance = this;

            if (Object.HasStateAuthority)
            {
                var cfg = LudoGame.GameplayConfigLoader.Load();
                var mode = StartTeams ? LudoCore.GameMode.Teams : LudoCore.GameMode.FreeForAll;

                // Start everyone as a bot; real players are promoted to seats as they join.
                var state = LudoCore.MatchFactory.Create(StartPlayers, mode, cfg, null, "ONLINE");

                var dice = new LudoCore.SeededDiceRoller(UnityEngine.Random.Range(1, 999999));
                _server = new LudoCore.AuthoritativeMatchServer(state, dice, new LudoCore.AutoPlayAI(),
                                                                turnTimeoutSec: cfg.TurnTimeoutSec, botThinkSeconds: 0.6f);
                _server.OnSnapshot += HandleServerSnapshot;
                _server.OnAutoPlayed += (seat, humanTimeout) =>
                {
                    if (humanTimeout) Notify($"Seat {seat} timed out — auto-played", NoticeKind.Warning);
                };
                _server.Start();

                foreach (var p in Runner.ActivePlayers) AssignSeat(p); // host + anyone already in
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || _server == null) return;

            // Snapshot active players once — ActivePlayers can mutate mid-enumeration on a disconnect.
            _present.Clear();
            try { foreach (var p in Runner.ActivePlayers) _present.Add(p); }
            catch { return; } // reconcile on the next tick

            foreach (var p in _present) AssignSeat(p);   // pick up newly-joined players

            // detect players who left → hand their seat to a bot so the match continues
            _left.Clear();
            foreach (var kv in _playerSeats) if (!_present.Contains(kv.Key)) _left.Add(kv.Key);
            foreach (var p in _left)
            {
                int pos = _playerSeats[p];
                var seat = FindSeat(pos);
                if (seat != null) { seat.IsBot = true; seat.Connection = LudoCore.ConnectionState.Disconnected; _vacated.Enqueue(pos); }
                _server.OnSeatChanged(pos); // bot takes over promptly on its turn
                _playerSeats.Remove(p);
                Notify($"Seat {pos} left — bot taking over", NoticeKind.Warning);
            }

            if (!_server.Finished) _server.Tick(Runner.DeltaTime);
        }

        // Assign a joining player a seat: reclaim a disconnect-vacated seat first (preserves their tokens), else the next open one.
        private void AssignSeat(PlayerRef player)
        {
            if (_playerSeats.ContainsKey(player)) return;
            LudoCore.Seat seat = null;
            if (_vacated.Count > 0) seat = FindSeat(_vacated.Dequeue());
            if (seat == null && _nextSeat < _server.State.Seats.Count) seat = _server.State.Seats[_nextSeat++];
            if (seat == null) return; // table full

            seat.IsBot = false;
            seat.Connection = LudoCore.ConnectionState.Online;
            _playerSeats[player] = seat.BoardPos;
            _server.OnSeatChanged(seat.BoardPos); // refresh timer so the new human isn't instantly timed out
            RPC_AssignSeat(player, seat.BoardPos);
            Notify($"Player joined — seat {seat.BoardPos}", NoticeKind.Success);
        }

        private LudoCore.Seat FindSeat(int boardPos)
        {
            foreach (var s in _server.State.Seats) if (s.BoardPos == boardPos) return s;
            return null;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AssignSeat(PlayerRef player, int seat)
        {
            if (player == Runner.LocalPlayer) LocalSeat = seat;
        }

        // ---- notifications (host broadcasts; everyone shows a toast) ----
        private void Notify(string msg, NoticeKind kind)
        {
            if (Object != null && Object.HasStateAuthority) RPC_Notice(msg, (byte)kind);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Notice(string msg, byte kind) => NotificationCenter.Push(msg, (NoticeKind)kind);

        // ---- client-side connectivity + lag monitoring ----
        private void Update()
        {
            if (Runner == null || Object == null || Object.HasStateAuthority) return; // clients only

            bool connected = Runner.IsConnectedToServer;
            if (_wasConnected && !connected) NotificationCenter.Push("Connection lost — reconnecting…", NoticeKind.Warning, 6f);
            else if (!_wasConnected && connected) NotificationCenter.Push("Reconnected", NoticeKind.Success);
            _wasConnected = connected;

            if (connected && Time.unscaledTime - _lastRttCheck > 2f)
            {
                _lastRttCheck = Time.unscaledTime;
                double rttMs = Runner.GetPlayerRtt(Runner.LocalPlayer) * 1000.0;
                if (rttMs > 300.0) NotificationCenter.Push($"Lag spike — {rttMs:F0} ms", NoticeKind.Warning, 2f);
            }
        }

        private void HandleServerSnapshot(LudoCore.MatchSnapshot snap)
        {
            LastSnapshot = snap;
            OnSnapshot?.Invoke(snap);        // host renders directly
            RPC_Snapshot(snap.Serialize());   // clients render via RPC
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
        private void RPC_Snapshot(byte[] data)
        {
            var snap = LudoCore.MatchSnapshot.Deserialize(data);
            LastSnapshot = snap;
            OnSnapshot?.Invoke(snap);
        }

        // ---- input: local player submits an intent ----
        public void SubmitRoll(int seat) => SubmitIntent((byte)LudoCore.IntentType.Roll, seat, -1);
        public void SubmitMove(int seat, int tokenId) => SubmitIntent((byte)LudoCore.IntentType.MoveToken, seat, tokenId);

        private void SubmitIntent(byte type, int seat, int tokenId)
        {
            if (Object.HasStateAuthority) ApplyIntent(type, seat, tokenId); // host: straight to the server
            else RPC_SubmitIntent(type, seat, tokenId);                     // client: send to host
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SubmitIntent(byte type, int seat, int tokenId) => ApplyIntent(type, seat, tokenId);

        private void ApplyIntent(byte type, int seat, int tokenId)
        {
            if (_server == null) return;
            var intent = (LudoCore.IntentType)type == LudoCore.IntentType.Roll
                ? LudoCore.PlayerIntent.Roll(seat)
                : LudoCore.PlayerIntent.Move(seat, tokenId);
            _server.Submit(intent); // silently rejected if illegal / not their turn
        }
    }

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

        private async void StartGame(GameMode gameMode)
        {
            if (_busy) return;
            _busy = true;

            LudoNetSession.StartPlayers = players;
            LudoNetSession.StartTeams = teams;

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

        private static readonly int[] TeamCounts = { 4, 6, 8, 10 }; // team mode needs a full board

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 270, 235), GUI.skin.box);
            GUILayout.Label("Ludo Royale — Online (Fusion)");
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
