using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>The full authoritative state of one match. Seats are stored in turn order (by board position).</summary>
    public sealed class MatchState
    {
        public string Id = "";
        public GameMode Mode;
        public BoardType BoardType;
        public BoardGeometry Geo;
        public GameConfig Config;

        public List<Seat> Seats = new List<Seat>();   // active seats only, ordered by BoardPos
        public int CurrentIndex;                       // index into Seats whose turn it is
        public MatchPhase Phase = MatchPhase.Lobby;

        public int LastRoll;
        public int ConsecutiveSixes;
        public int TurnNumber;

        public List<int> FinishOrder = new List<int>(); // seat BoardPos in the order they finished
        public int? WinningTeam;

        public MatchState(BoardGeometry geo, GameConfig config) { Geo = geo; Config = config; }

        public Seat Current => Seats[CurrentIndex];

        public bool SameTeam(Seat a, Seat b) => Mode == GameMode.Teams && a.TeamId == b.TeamId;

        public Seat? SeatAt(int boardPos)
        {
            foreach (var s in Seats) if (s.BoardPos == boardPos) return s;
            return null;
        }
    }
}
