using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>
    /// Heuristic move chooser. Powers practice bots, empty-seat fill, and the 60s idle/timeout auto-play.
    /// Priorities: finish &gt; capture &gt; escape danger &gt; unlock &gt; advance/safe.
    /// </summary>
    public sealed class AutoPlayAI : IMovePolicy
    {
        public Move Choose(MatchState s, Seat seat, int die, IReadOnlyList<Move> legal)
        {
            Move best = legal[0];
            int bestScore = int.MinValue;
            foreach (var m in legal)
            {
                int score = Score(s, seat, m);
                if (score > bestScore) { bestScore = score; best = m; }
            }
            return best;
        }

        private int Score(MatchState s, Seat seat, Move m)
        {
            var g = s.Geo;
            int score = m.ToProgress; // baseline: prefer advancing further

            if (m.ToProgress == g.FinalHomeProgress) score += 1000;          // finish a token

            if (g.IsOnRing(m.ToProgress))
            {
                int idx = g.RingIndexFor(seat.BoardPos, m.ToProgress);
                if (!g.IsSafe(idx) && OpponentTokenAt(s, seat, idx, out int victimProgress))
                    score += 500 + victimProgress;                           // capture (prefer advanced victim)
                if (g.IsSafe(idx)) score += 40;                              // land on safe
            }

            if (m.IsUnlock) score += 120;                                    // get a token out

            if (!m.IsUnlock && g.IsOnRing(m.FromProgress) && InDanger(s, seat, m.FromProgress))
                score += 200;                                                // escape capture

            return score;
        }

        private static bool OpponentTokenAt(MatchState s, Seat seat, int ringIdx, out int victimProgress)
        {
            var g = s.Geo;
            victimProgress = 0;
            foreach (var t in s.Seats)
            {
                if (t == seat || s.SameTeam(t, seat)) continue;
                foreach (var tok in t.Tokens)
                    if (g.IsOnRing(tok.Progress) && g.RingIndexFor(t.BoardPos, tok.Progress) == ringIdx)
                    { victimProgress = tok.Progress; return true; }
            }
            return false;
        }

        private static bool InDanger(MatchState s, Seat seat, int progress)
        {
            var g = s.Geo;
            int myIdx = g.RingIndexFor(seat.BoardPos, progress);
            if (g.IsSafe(myIdx)) return false;
            foreach (var t in s.Seats)
            {
                if (t == seat || s.SameTeam(t, seat)) continue;
                foreach (var tok in t.Tokens)
                {
                    if (!g.IsOnRing(tok.Progress)) continue;
                    int oppIdx = g.RingIndexFor(t.BoardPos, tok.Progress);
                    int dist = (myIdx - oppIdx + g.Ring) % g.Ring; // forward distance opp -> me
                    if (dist >= 1 && dist <= 6) return true;
                }
            }
            return false;
        }
    }
}
