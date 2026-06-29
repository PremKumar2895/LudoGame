// Renders the online match from server snapshots and turns the local player's taps into intents.
#if FUSION2
using System.Collections.Generic;
using UnityEngine;
using Ludo.Core;
using LudoGame.UI;

namespace LudoGame.Net
{
    public sealed class OnlineGameView : MonoBehaviour
    {
        private LudoNetSession _session;
        private GameConfig _cfg;
        private BoardLayout _layout;
        private BoardView _view;
        private Transform _root;
        private Camera _cam;

        private MatchSnapshot _last;
        private bool _built;
        private string _msg = "Connecting…";

        // animation state
        private bool _firstRender = true;
        private readonly Dictionary<int, int[]> _shown = new Dictionary<int, int[]>(); // seat -> displayed progress per token
        private sbyte _diceFace = -1;
        private float _rollSettleAt;
        private bool _finished;
        private string _winnerMsg = "";

        private void Awake() { _cfg = GameplayConfigLoader.Load(); }

        private void Update()
        {
            if (_session == null)
            {
                _session = LudoNetSession.Instance;
                if (_session != null)
                {
                    _session.OnSnapshot += OnSnap;
                    if (_session.LastSnapshot != null) OnSnap(_session.LastSnapshot);
                }
                return;
            }
            if (_built && (_lastScreen.x != Screen.width || _lastScreen.y != Screen.height)) FrameCamera();
            HandleInput();
        }

        private void OnDestroy()
        {
            if (_session != null) _session.OnSnapshot -= OnSnap;
        }

        private void OnSnap(MatchSnapshot snap)
        {
            _last = snap;
            if (!_built) Build(snap);
            Render(snap);
        }

        private void Build(MatchSnapshot snap)
        {
            var mirror = snap.ToState(_cfg);
            _layout = new BoardLayout(mirror.Geo, _cfg.TokensPerSeat);
            SetupCamera();
            _root = new GameObject("OnlineBoard").transform;
            _view = new BoardView();
            _view.Build(mirror, _layout, _root);
            _built = true;
        }

        private void Render(MatchSnapshot snap)
        {
            // dice tumble: when the roll changes, spin the face briefly before settling
            if (snap.LastRoll > 0 && snap.LastRoll != _diceFace)
            {
                _diceFace = snap.LastRoll;
                _rollSettleAt = Time.unscaledTime + 0.45f;
                if (!_firstRender) Sfx.Roll();
            }

            foreach (var ss in snap.Seats)
            {
                if (!_view.Tokens.TryGetValue(ss.BoardPos, out var arr)) continue;
                if (!_shown.TryGetValue(ss.BoardPos, out var shown))
                {
                    shown = new int[arr.Length];
                    for (int i = 0; i < shown.Length; i++) shown[i] = int.MinValue;
                    _shown[ss.BoardPos] = shown;
                }

                for (int t = 0; t < arr.Length && t < ss.Tokens.Length; t++)
                {
                    arr[t].Highlight(false);
                    int now = ss.Tokens[t];

                    if (_firstRender || shown[t] == int.MinValue)
                    {
                        arr[t].SetInstant(_layout, now);                                  // first sight: place instantly
                    }
                    else if (now != shown[t])
                    {
                        arr[t].StopAllCoroutines();
                        if (now < shown[t])
                        {
                            Vfx.Burst(arr[t].transform.position, BoardColors.For(ss.BoardPos), 1.1f); // capture flash
                            Sfx.Capture();
                            arr[t].StartCoroutine(arr[t].ToBaseRoutine(_layout));                     // captured → knockback home
                        }
                        else
                        {
                            if (ss.BoardPos == _session.LocalSeat) Sfx.Move();
                            arr[t].StartCoroutine(arr[t].MoveRoutine(_layout, shown[t], now));         // advanced → hop (handles unlock)
                        }
                    }
                    shown[t] = now;
                }
            }
            _firstRender = false;

            bool myTurn = snap.CurrentSeat == _session.LocalSeat && snap.Phase == (byte)MatchPhase.InProgress;
            if (myTurn && snap.Sub == (byte)TurnSub.AwaitingMove && _view.Tokens.TryGetValue(_session.LocalSeat, out var mine))
                foreach (var id in snap.MovableTokens)
                    if (id < mine.Length) mine[id].Highlight(true);

            _msg = snap.Phase == (byte)MatchPhase.Finished
                ? "Game over."
                : myTurn
                    ? (snap.Sub == (byte)TurnSub.AwaitingRoll ? "Your turn — TAP to roll" : "TAP a highlighted token")
                    : $"Seat {snap.CurrentSeat} is playing…";

            if (snap.Phase == (byte)MatchPhase.Finished && !_finished)
            {
                _finished = true;
                Vfx.Confetti(_layout.Center, 24);
                Sfx.Win();
                _winnerMsg = snap.Mode == (byte)GameMode.Teams
                    ? $"Team {(char)('A' + Mathf.Max(0, (int)snap.WinningTeam))} wins!"
                    : (snap.FinishOrder.Count > 0 ? $"Seat {snap.FinishOrder[0]} wins!" : "Game over!");
            }
        }

        private void HandleInput()
        {
            if (_last == null || _cam == null || !Input.GetMouseButtonDown(0)) return;
            if (_last.CurrentSeat != _session.LocalSeat || _last.Phase != (byte)MatchPhase.InProgress) return;

            if (_last.Sub == (byte)TurnSub.AwaitingRoll)
            {
                _session.SubmitRoll(_session.LocalSeat);
            }
            else if (_last.Sub == (byte)TurnSub.AwaitingMove)
            {
                Vector2 wp = _cam.ScreenToWorldPoint(Input.mousePosition);
                var hit = Physics2D.OverlapPoint(wp);
                if (hit != null)
                {
                    var tv = hit.GetComponent<TokenView>();
                    if (tv != null && tv.BoardPos == _session.LocalSeat)
                        _session.SubmitMove(_session.LocalSeat, tv.TokenId);
                }
            }
        }

        private Vector2Int _lastScreen;

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.transform.position = new Vector3(0, 0, -10);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = BoardColors.Background;
            FrameCamera();
        }

        // Size the orthographic view so the whole board fits on ANY aspect — portrait phone,
        // tablet, or desktop. Vertical half-extent = orthoSize; horizontal = orthoSize * aspect.
        private void FrameCamera()
        {
            if (_cam == null || _layout == null) return;
            float half = _layout.ViewHalfExtent * 1.12f;             // board extent + margin for HUD/roster
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            _cam.orthographicSize = half / Mathf.Min(1f, aspect);    // fit width on portrait, height on landscape
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
        }

        private void OnGUI()
        {
            UIScale.Apply();
            var title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(272, 12, 700, 26), _session != null ? $"Online — you are seat {_session.LocalSeat}" : "Online", title);
            GUI.Label(new Rect(272, 40, 700, 26), _msg, new GUIStyle(GUI.skin.label) { fontSize = 16 });
            if (_last != null && _last.LastRoll > 0)
            {
                int face = Time.unscaledTime < _rollSettleAt
                    ? 1 + (Mathf.FloorToInt(Time.unscaledTime / 0.06f) % 6) // tumble through faces ~17/s
                    : _last.LastRoll;
                GUI.Box(new Rect(272, 70, 64, 64), face.ToString(), new GUIStyle(GUI.skin.box) { fontSize = 28, fontStyle = FontStyle.Bold });
            }

            if (_finished)
            {
                const float w = 360f, h = 86f;
                float x = (UIScale.Width - w) * 0.5f, y = UIScale.Height * 0.30f;
                var prev = GUI.color;
                GUI.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);
                GUI.Box(new Rect(x, y, w, h), GUIContent.none);
                GUI.color = prev;
                var bs = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                bs.normal.textColor = new Color(1f, 0.86f, 0.30f);
                GUI.Label(new Rect(x, y, w, h), _winnerMsg, bs);
            }

            DrawRoster();
        }

        // Bottom roster strip — one chip per seat (colour, who, team / finish rank); the current turn glows, your seat is outlined.
        private void DrawRoster()
        {
            if (_last == null || _last.Seats.Count == 0) return;
            bool teamMode = _last.Mode == (byte)GameMode.Teams;
            int n = _last.Seats.Count;
            const float cw = 60f, ch = 38f, gap = 6f;
            float total = n * cw + (n - 1) * gap;
            float sx = Mathf.Max(8f, (UIScale.Width - total) * 0.5f);
            float sy = UIScale.Height - ch - 10f;

            var chip = new GUIStyle(GUI.skin.box) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            chip.normal.textColor = BoardColors.Outline;
            var prev = GUI.color;

            foreach (var ss in _last.Seats)
            {
                var col = BoardColors.For(ss.BoardPos);
                bool cur = ss.BoardPos == _last.CurrentSeat && _last.Phase == (byte)MatchPhase.InProgress;
                bool mine = _session != null && ss.BoardPos == _session.LocalSeat;
                bool done = ss.FinishRank >= 0;

                if (mine) { GUI.color = BoardColors.Outline; GUI.Box(new Rect(sx - 3, sy - 3, cw + 6, ch + 6), GUIContent.none); }
                if (cur) { GUI.color = Color.white; GUI.Box(new Rect(sx - 2, sy - 2, cw + 4, ch + 4), GUIContent.none); }

                GUI.color = done ? BoardColors.Light(col, 0.6f) : (cur ? col : BoardColors.Light(col, 0.25f));
                string nm = _session != null ? _session.SeatName(ss.BoardPos) : null;
                string who = !string.IsNullOrEmpty(nm) ? nm : (ss.IsBot ? "Bot" : "P" + ss.BoardPos);
                if (who.Length > 9) who = who.Substring(0, 8) + "…";
                string sub = done ? $"#{ss.FinishRank + 1}" : (teamMode ? "Team " + (char)('A' + ss.TeamId) : "");
                GUI.Box(new Rect(sx, sy, cw, ch), $"{who}\n{sub}", chip);

                sx += cw + gap;
            }
            GUI.color = prev;
        }
    }
}
#endif
