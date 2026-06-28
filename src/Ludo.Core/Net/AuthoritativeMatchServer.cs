using System;
using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>
    /// Server-authoritative, intent-driven turn engine for online play. The HOST owns one instance:
    /// it rolls the dice, validates every intent against RulesEngine, enforces the turn timer + auto-play,
    /// and raises a MatchSnapshot after each change for clients to render. Transport-agnostic — the Photon
    /// (or any) adapter just pipes RPCs ↔ Submit() and broadcasts the snapshots.
    /// </summary>
    public sealed class AuthoritativeMatchServer
    {
        public MatchState State { get; }
        private readonly IDiceRoller _dice;
        private readonly IMovePolicy _bot;
        private readonly float _turnTimeout;
        private readonly float _botThink;

        private TurnSub _sub = TurnSub.AwaitingRoll;
        private readonly List<Move> _pending = new List<Move>();
        private float _timer;

        /// <summary>Raised after every state change. The host broadcasts this to all clients.</summary>
        public event Action<MatchSnapshot>? OnSnapshot;
        /// <summary>Raised when the server auto-plays for a seat (bot turn or human timeout). (seat, wasHumanTimeout)</summary>
        public event Action<int, bool>? OnAutoPlayed;

        public TurnSub Sub => _sub;
        public IReadOnlyList<Move> Pending => _pending;
        public bool Finished => State.Phase == MatchPhase.Finished;

        public AuthoritativeMatchServer(MatchState state, IDiceRoller dice, IMovePolicy botPolicy,
                                        float turnTimeoutSec = 60f, float botThinkSeconds = 0.5f)
        {
            State = state; _dice = dice; _bot = botPolicy;
            _turnTimeout = turnTimeoutSec; _botThink = botThinkSeconds;
        }

        public void Start()
        {
            if (State.Phase == MatchPhase.Lobby) State.Phase = MatchPhase.InProgress;
            BeginTurn();
        }

        public MatchSnapshot Snapshot() => MatchSnapshot.Capture(State, _sub, _pending);

        /// <summary>
        /// Notify the server that a seat's controller flipped bot↔human (a player joined or dropped).
        /// If that seat is currently on turn, its timer is re-evaluated so a freshly-promoted human isn't
        /// instantly auto-played on the leftover bot-think timer (and a dropped seat hands off promptly).
        /// </summary>
        public void OnSeatChanged(int boardPos)
        {
            if (!Finished && State.Current.BoardPos == boardPos) ResetTimer();
        }

        /// <summary>A client submits an intent for the current human seat. Returns false if rejected.</summary>
        public bool Submit(PlayerIntent intent)
        {
            if (Finished) return false;
            var seat = State.Current;
            if (intent.SeatBoardPos != seat.BoardPos || seat.IsBot) return false;

            if (_sub == TurnSub.AwaitingRoll && intent.Type == IntentType.Roll) { Roll(false); return true; }
            if (_sub == TurnSub.AwaitingMove && intent.Type == IntentType.MoveToken)
            {
                foreach (var m in _pending)
                    if (m.TokenId == intent.TokenId) { Apply(m); return true; }
            }
            return false;
        }

        /// <summary>Advances the clock. Drives paced bot turns and human-timeout auto-play.</summary>
        public void Tick(float dt)
        {
            if (Finished) return;
            _timer -= dt;
            if (_timer > 0f) return;

            var seat = State.Current;
            if (seat.IsBot)
            {
                // One paced sub-action per expiry so clients see bot moves at a watchable rhythm.
                if (_sub == TurnSub.AwaitingRoll) Roll(true); else AutoMove();
            }
            else
            {
                // Human timed out: auto-play their entire turn at once, then move on.
                OnAutoPlayed?.Invoke(seat.BoardPos, true);
                int startSeat = seat.BoardPos, guard = 0;
                while (!Finished && State.Current.BoardPos == startSeat && guard++ < 100)
                {
                    if (_sub == TurnSub.AwaitingRoll) Roll(true); else AutoMove();
                }
            }
        }

        public void RunHeadless(int maxSteps = 500000)
        {
            int n = 0;
            while (!Finished && n++ < maxSteps) Tick(1_000_000f);
        }

        // ---- internals ----

        private void BeginTurn()
        {
            _sub = TurnSub.AwaitingRoll;
            State.ConsecutiveSixes = 0;
            _pending.Clear();
            ResetTimer();
            Emit();
        }

        private void ResetTimer() => _timer = State.Current.IsBot ? _botThink : _turnTimeout;

        private void Roll(bool auto)
        {
            var seat = State.Current;
            int die = _dice.Roll();
            State.LastRoll = die;
            if (die == 6) State.ConsecutiveSixes++; else State.ConsecutiveSixes = 0;

            if (die == 6 && State.ConsecutiveSixes >= 3 && State.Config.ThreeSixCancels) { EndTurn(); return; }

            var moves = RulesEngine.GetLegalMoves(State, seat, die);
            if (moves.Count == 0)
            {
                Emit();
                if (die == 6 && State.ConsecutiveSixes < 3) { _sub = TurnSub.AwaitingRoll; ResetTimer(); }
                else EndTurn();
                return;
            }
            if (moves.Count == 1 && State.Config.AutoMoveSingle) { Apply(moves[0]); return; }

            _pending.Clear();
            _pending.AddRange(moves);
            _sub = TurnSub.AwaitingMove;
            ResetTimer();
            Emit();
            if (auto) { } // (kept for symmetry / future telemetry)
        }

        private void AutoMove()
        {
            if (_pending.Count == 0) { EndTurn(); return; }
            var move = _bot.Choose(State, State.Current, State.LastRoll, _pending);
            Apply(move);
        }

        private void Apply(Move move)
        {
            var seat = State.Current;
            var r = RulesEngine.ApplyMove(State, seat, move, State.LastRoll);
            RulesEngine.CheckWin(State);
            Emit();
            if (State.Phase == MatchPhase.Finished) return;

            if (r.GrantsBonus && State.ConsecutiveSixes < 3)
            {
                _sub = TurnSub.AwaitingRoll;
                _pending.Clear();
                ResetTimer();
                Emit();
            }
            else EndTurn();
        }

        private void EndTurn()
        {
            RulesEngine.AdvanceToNextSeat(State);
            if (State.Phase == MatchPhase.Finished) { Emit(); return; }
            BeginTurn();
        }

        private void Emit() => OnSnapshot?.Invoke(Snapshot());
    }
}
