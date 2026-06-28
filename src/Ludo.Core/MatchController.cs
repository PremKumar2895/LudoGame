using System;
using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>Strategy for picking among legal moves (AI, bot, or a human's tapped choice).</summary>
    public interface IMovePolicy
    {
        Move Choose(MatchState state, Seat seat, int die, IReadOnlyList<Move> legal);
    }

    public sealed class RandomMovePolicy : IMovePolicy
    {
        private readonly Random _rng;
        public RandomMovePolicy(int seed) => _rng = new Random(seed);
        public Move Choose(MatchState state, Seat seat, int die, IReadOnlyList<Move> legal) => legal[_rng.Next(legal.Count)];
    }

    /// <summary>Drives turn order, dice, bonus turns, the three-sixes rule, and win detection.</summary>
    public sealed class MatchController
    {
        public MatchState State { get; }
        private readonly IDiceRoller _dice;
        private readonly IMovePolicy _policy;
        public Action<string>? Log;

        public MatchController(MatchState state, IDiceRoller dice, IMovePolicy policy)
        {
            State = state; _dice = dice; _policy = policy;
            if (State.Phase == MatchPhase.Lobby) State.Phase = MatchPhase.InProgress;
        }

        /// <summary>Plays one seat's full turn (including bonus rolls), then advances to the next seat.</summary>
        public void PlayTurn()
        {
            var s = State;
            if (s.Phase != MatchPhase.InProgress) return;

            var seat = s.Current;
            s.TurnNumber++;
            s.ConsecutiveSixes = 0;

            int safety = 0;
            while (true)
            {
                if (++safety > 200) break; // guard against pathological loops

                int die = _dice.Roll();
                s.LastRoll = die;
                if (die == 6) s.ConsecutiveSixes++; else s.ConsecutiveSixes = 0;

                if (die == 6 && s.ConsecutiveSixes >= 3 && s.Config.ThreeSixCancels)
                {
                    Log?.Invoke($"  seat {seat.BoardPos}: three sixes — forfeit");
                    break;
                }

                var moves = RulesEngine.GetLegalMoves(s, seat, die);
                if (moves.Count == 0)
                {
                    Log?.Invoke($"  seat {seat.BoardPos} rolled {die}: no move");
                    if (die == 6 && s.ConsecutiveSixes < 3) continue; // a 6 grants another roll
                    break;
                }

                Move chosen = (moves.Count == 1 && s.Config.AutoMoveSingle)
                    ? moves[0]
                    : _policy.Choose(s, seat, die, moves);

                var r = RulesEngine.ApplyMove(s, seat, chosen, die);
                Log?.Invoke($"  seat {seat.BoardPos} rolled {die}: {chosen}"
                            + (r.Captures.Count > 0 ? $" KICK x{r.Captures.Count}" : "")
                            + (r.ReachedHome ? " HOME" : "")
                            + (r.SeatFinished ? " *FINISHED*" : ""));

                RulesEngine.CheckWin(s);
                if (s.Phase == MatchPhase.Finished) return;

                if (r.GrantsBonus && s.ConsecutiveSixes < 3) continue;
                break;
            }

            RulesEngine.AdvanceToNextSeat(s);
        }

        public void RunToCompletion(int maxTurns = 200000)
        {
            int t = 0;
            while (State.Phase == MatchPhase.InProgress && t++ < maxTurns) PlayTurn();
        }
    }
}
