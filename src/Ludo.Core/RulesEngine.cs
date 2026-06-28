using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>
    /// Pure, deterministic rules. No graphics, no networking, no randomness of its own.
    /// The same code runs on the authoritative server (truth) and the client (prediction/validation).
    /// </summary>
    public static class RulesEngine
    {
        /// <summary>All legal moves for <paramref name="seat"/> given a die value.</summary>
        public static List<Move> GetLegalMoves(MatchState s, Seat seat, int die)
        {
            var g = s.Geo;
            int final = g.FinalHomeProgress;
            var moves = new List<Move>();

            foreach (var tok in seat.Tokens)
            {
                if (tok.Progress < 0)
                {
                    // In base: may leave only on an unlock roll. Entry cell (own start) is safe → always allowed.
                    if (Contains(s.Config.UnlockRolls, die))
                        moves.Add(new Move(seat.BoardPos, tok.Id, -1, 0, true));
                    continue;
                }

                if (tok.Progress >= final) continue; // already home

                int to = tok.Progress + die;
                if (to > final) continue;            // overshoot illegal (ExactFinish)
                if (to == final) { moves.Add(new Move(seat.BoardPos, tok.Id, tok.Progress, to, false)); continue; }
                if (MoveBlocked(s, seat, tok.Progress, to)) continue;

                moves.Add(new Move(seat.BoardPos, tok.Id, tok.Progress, to, false));
            }

            return moves;
        }

        /// <summary>Applies a move, resolving captures, bonus turns and finishing. Mutates state.</summary>
        public static MoveResult ApplyMove(MatchState s, Seat seat, Move m, int die)
        {
            var g = s.Geo;
            var res = new MoveResult { Move = m };

            var tok = seat.Tokens[m.TokenId];
            tok.Progress = m.ToProgress;

            // Captures: only on a non-safe ring cell.
            if (g.IsOnRing(m.ToProgress))
            {
                int idx = g.RingIndexFor(seat.BoardPos, m.ToProgress);
                if (!g.IsSafe(idx))
                {
                    foreach (var t in s.Seats)
                    {
                        if (t == seat || s.SameTeam(t, seat)) continue;
                        foreach (var ot in t.Tokens)
                        {
                            if (g.IsOnRing(ot.Progress) && g.RingIndexFor(t.BoardPos, ot.Progress) == idx)
                            {
                                ot.Progress = -1; // sent home (the "kick-off")
                                res.Captures.Add((t.BoardPos, ot.Id));
                            }
                        }
                    }
                }
            }

            res.ReachedHome = (m.ToProgress == g.FinalHomeProgress);

            bool six = die == 6;
            res.GrantsBonus =
                (six && s.Config.BonusOnSix) ||
                (res.Captures.Count > 0 && s.Config.BonusOnCapture) ||
                (res.ReachedHome && s.Config.BonusOnHome);

            if (!seat.FinishRank.HasValue && seat.AllHome(g))
            {
                s.FinishOrder.Add(seat.BoardPos);
                seat.FinishRank = s.FinishOrder.Count;
                res.SeatFinished = true;
            }

            return res;
        }

        /// <summary>Moves the turn to the next active, unfinished seat (or ends the match). Shared by server and client.</summary>
        public static void AdvanceToNextSeat(MatchState s)
        {
            if (s.Phase == MatchPhase.Finished) return;
            int n = s.Seats.Count;
            for (int k = 1; k <= n; k++)
            {
                int idx = (s.CurrentIndex + k) % n;
                if (!s.Seats[idx].FinishRank.HasValue) { s.CurrentIndex = idx; s.ConsecutiveSixes = 0; return; }
            }
            s.Phase = MatchPhase.Finished; // all seats finished
        }

        /// <summary>Detects the end condition (FFA full ranking, or a team all-home) and sets Phase/WinningTeam.</summary>
        public static void CheckWin(MatchState s)
        {
            if (s.Mode == GameMode.Teams)
            {
                var totals = new Dictionary<int, int>();
                var done = new Dictionary<int, int>();
                foreach (var seat in s.Seats)
                {
                    totals[seat.TeamId] = (totals.TryGetValue(seat.TeamId, out var tt) ? tt : 0) + 1;
                    if (seat.FinishRank.HasValue)
                        done[seat.TeamId] = (done.TryGetValue(seat.TeamId, out var dd) ? dd : 0) + 1;
                }
                foreach (var kv in totals)
                {
                    done.TryGetValue(kv.Key, out var d);
                    if (d == kv.Value) { s.WinningTeam = kv.Key; s.Phase = MatchPhase.Finished; return; }
                }
            }
            else
            {
                int active = s.Seats.Count, finished = 0;
                foreach (var seat in s.Seats) if (seat.FinishRank.HasValue) finished++;
                if (finished >= active - 1)
                {
                    foreach (var seat in s.Seats)
                        if (!seat.FinishRank.HasValue)
                        {
                            s.FinishOrder.Add(seat.BoardPos);
                            seat.FinishRank = s.FinishOrder.Count;
                        }
                    s.Phase = MatchPhase.Finished;
                }
            }
        }

        // ---- helpers ----

        private static bool MoveBlocked(MatchState s, Seat seat, int from, int to)
        {
            if (!s.Config.BlocksEnabled) return false;
            var g = s.Geo;
            int last = to < g.HomeEntryOffset ? to : g.HomeEntryOffset;
            for (int p = from + 1; p <= last; p++)
            {
                int idx = g.RingIndexFor(seat.BoardPos, p);
                if (IsOpponentBlock(s, seat, idx))
                {
                    bool landing = (p == to);
                    if (s.Config.BlockBlocksPassage || landing) return true;
                }
            }
            return false;
        }

        /// <summary>True if a non-teammate seat has 2+ tokens on a (non-safe) ring cell.</summary>
        public static bool IsOpponentBlock(MatchState s, Seat movingSeat, int ringIdx)
        {
            var g = s.Geo;
            if (g.IsSafe(ringIdx)) return false;
            foreach (var t in s.Seats)
            {
                if (t == movingSeat || s.SameTeam(t, movingSeat)) continue;
                int c = 0;
                foreach (var tok in t.Tokens)
                {
                    if (g.IsOnRing(tok.Progress) && g.RingIndexFor(t.BoardPos, tok.Progress) == ringIdx)
                    {
                        if (++c >= 2) return true;
                    }
                }
            }
            return false;
        }

        private static bool Contains(int[] arr, int v)
        {
            for (int i = 0; i < arr.Length; i++) if (arr[i] == v) return true;
            return false;
        }
    }
}
