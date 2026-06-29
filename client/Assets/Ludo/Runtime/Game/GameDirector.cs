using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ludo.Core;

namespace LudoGame
{
    /// <summary>
    /// Add this to one GameObject and press Play. You control seat 0; the rest are bots.
    /// Tap anywhere to roll on your turn, then tap a highlighted token to move it.
    /// Drives the tested Ludo.Core engine — locally now, the same engine the server will run online.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        [Header("Match")]
        [Range(2, 10)] public int players = 4;
        public bool teams = false;
        public int seed = 0;

        [Header("Pacing (seconds)")]
        public float botRollDelay = 0.45f;
        public float botMoveDelay = 0.35f;

        private MatchState _state;
        private BoardLayout _layout;
        private BoardView _view;
        private IDiceRoller _dice;
        private AutoPlayAI _ai;
        private Camera _cam;
        private int _humanPos;

        private enum Await { None, Roll, Move }
        private Await _await = Await.None;
        private bool _rollRequested;
        private int _pickedToken = -1;
        private Move _picked;
        private readonly Dictionary<int, Move> _moveByToken = new Dictionary<int, Move>();

        private int _displayDie;
        private string _message = "";
        private string _banner = "";

        private void Start()
        {
            var cfg = GameplayConfigLoader.Load();
            players = Mathf.Clamp(players, 2, cfg.MaxPlayers);

            var mode = teams ? GameMode.Teams : GameMode.FreeForAll;
            if (mode == GameMode.Teams && players != 4 && players != 6 && players != 8 && players != 10)
            { mode = GameMode.FreeForAll; teams = false; }

            var slots = new List<PlayerSlot>();
            for (int i = 0; i < players; i++)
                slots.Add(new PlayerSlot { Name = i == 0 ? "You" : "Bot " + i, IsBot = i != 0 });

            _state = MatchFactory.Create(players, mode, cfg, slots);
            _humanPos = _state.Seats[0].BoardPos;
            _dice = new SeededDiceRoller(seed == 0 ? Random.Range(1, 999999) : seed);
            _ai = new AutoPlayAI();

            _layout = new BoardLayout(_state.Geo, cfg.TokensPerSeat);
            SetupCamera();
            var root = new GameObject("Board").transform;
            _view = new BoardView();
            _view.Build(_state, _layout, root);

            StartCoroutine(GameLoop());
        }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = _layout.ViewHalfExtent;
            _cam.transform.position = new Vector3(0, 0, -10);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = BoardColors.Background;
        }

        private void Update()
        {
            if (_cam == null || !Input.GetMouseButtonDown(0)) return;
            if (_await == Await.Roll) { _rollRequested = true; }
            else if (_await == Await.Move)
            {
                Vector2 wp = _cam.ScreenToWorldPoint(Input.mousePosition);
                var hit = Physics2D.OverlapPoint(wp);
                if (hit != null)
                {
                    var tv = hit.GetComponent<TokenView>();
                    if (tv != null && tv.BoardPos == _humanPos) _pickedToken = tv.TokenId;
                }
            }
        }

        private IEnumerator GameLoop()
        {
            while (_state.Phase == MatchPhase.InProgress)
                yield return TurnFor(_state.Current);

            _banner = _state.Mode == GameMode.Teams
                ? $"Team {_state.WinningTeam} wins!"
                : (_state.FinishOrder.Count > 0 && _state.FinishOrder[0] == _humanPos ? "You win!" : $"Seat {(_state.FinishOrder.Count > 0 ? _state.FinishOrder[0] : -1)} wins!");
            _message = "Game over.";
        }

        private IEnumerator TurnFor(Seat seat)
        {
            _state.ConsecutiveSixes = 0;
            bool human = seat.BoardPos == _humanPos && !seat.IsBot;

            while (true)
            {
                if (human)
                {
                    _message = "Your turn — TAP to roll";
                    _await = Await.Roll; _rollRequested = false;
                    while (!_rollRequested) yield return null;
                    _await = Await.None;
                }
                else
                {
                    _message = $"{seat.Name} is playing…";
                    yield return new WaitForSeconds(botRollDelay);
                }

                int die = _dice.Roll();
                yield return DiceAnim(die);

                if (die == 6) _state.ConsecutiveSixes++; else _state.ConsecutiveSixes = 0;
                if (die == 6 && _state.ConsecutiveSixes >= 3 && _state.Config.ThreeSixCancels)
                { _message = "Three sixes — turn lost!"; yield return new WaitForSeconds(0.5f); break; }

                var moves = RulesEngine.GetLegalMoves(_state, seat, die);
                if (moves.Count == 0)
                {
                    _message = $"Rolled {die} — no move";
                    yield return new WaitForSeconds(0.4f);
                    if (die == 6 && _state.ConsecutiveSixes < 3) continue;
                    break;
                }

                Move chosen;
                if (human)
                {
                    if (moves.Count == 1 && _state.Config.AutoMoveSingle) chosen = moves[0];
                    else { yield return PickMove(seat, moves); chosen = _picked; }
                }
                else
                {
                    chosen = _ai.Choose(_state, seat, die, moves);
                    yield return new WaitForSeconds(botMoveDelay);
                }

                var tv = _view.Tokens[seat.BoardPos][chosen.TokenId];
                var result = RulesEngine.ApplyMove(_state, seat, chosen, die);
                yield return tv.MoveRoutine(_layout, chosen.FromProgress, chosen.ToProgress);

                foreach (var cap in result.Captures)
                {
                    _message = "Kick-off!";
                    yield return _view.Tokens[cap.seat][cap.token].ToBaseRoutine(_layout);
                }

                RulesEngine.CheckWin(_state);
                if (_state.Phase == MatchPhase.Finished) yield break;

                if (result.GrantsBonus && _state.ConsecutiveSixes < 3) continue;
                break;
            }

            RulesEngine.AdvanceToNextSeat(_state);
        }

        private IEnumerator PickMove(Seat seat, List<Move> moves)
        {
            _moveByToken.Clear();
            foreach (var m in moves) _moveByToken[m.TokenId] = m;
            foreach (var kv in _moveByToken) _view.Tokens[seat.BoardPos][kv.Key].Highlight(true);

            _message = "TAP a highlighted token";
            _await = Await.Move; _pickedToken = -1;
            while (true)
            {
                if (_pickedToken >= 0 && _moveByToken.TryGetValue(_pickedToken, out var mv)) { _picked = mv; break; }
                _pickedToken = -1;
                yield return null;
            }
            _await = Await.None;
            foreach (var kv in _moveByToken) _view.Tokens[seat.BoardPos][kv.Key].Highlight(false);
        }

        private IEnumerator DiceAnim(int finalDie)
        {
            float end = Time.time + 0.45f;
            while (Time.time < end) { _displayDie = Random.Range(1, 7); yield return new WaitForSeconds(0.05f); }
            _displayDie = finalDie;
            yield return new WaitForSeconds(0.15f);
        }

        private void OnGUI()
        {
            var title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            var msg = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(14, 8, 700, 28), $"Ludo Royale — {players}p {(teams ? "Teams" : "FFA")} (you control seat {_humanPos})", title);
            GUI.Label(new Rect(14, 40, 700, 26), _message, msg);

            var die = new GUIStyle(GUI.skin.box) { fontSize = 34, fontStyle = FontStyle.Bold };
            GUI.Box(new Rect(14, 72, 72, 72), _displayDie > 0 ? _displayDie.ToString() : "–", die);

            if (!string.IsNullOrEmpty(_banner))
            {
                var b = new GUIStyle(GUI.skin.box) { fontSize = 30, fontStyle = FontStyle.Bold };
                GUI.Box(new Rect(Screen.width / 2 - 170, Screen.height / 2 - 55, 340, 110), _banner, b);
            }
        }
    }
}
