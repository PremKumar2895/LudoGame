using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>
    /// Parametric Ludo board. ONE definition for every size:
    ///   RING = ringCellsPerArm * seatCount  (52 / 78 / 104 / 130 for N = 4 / 6 / 8 / 10)
    /// Each seat travels HomeEntryOffset ring cells, then a private home column of HomeColumnLen cells.
    /// With HomeEntryOffset = 50 every seat travels the same 56 steps regardless of board size.
    /// </summary>
    public sealed class BoardGeometry
    {
        public int SeatCount { get; }            // N
        public int Ring { get; }                 // shared ring length
        public int CellsPerArm { get; }
        public int HomeEntryOffset { get; }      // last ring progress index (token is on the ring at 0..HomeEntryOffset)
        public int HomeColumnLen { get; }
        public int FinalHomeProgress { get; }    // progress value that means HOME

        private readonly HashSet<int> _safe;

        public BoardGeometry(int seatCount, GameConfig cfg)
        {
            SeatCount = seatCount;
            CellsPerArm = cfg.RingCellsPerArm;
            Ring = CellsPerArm * seatCount;
            HomeEntryOffset = cfg.HomeEntryOffset;
            HomeColumnLen = cfg.HomeColumnLen;
            FinalHomeProgress = HomeEntryOffset + HomeColumnLen;

            _safe = new HashSet<int>();
            for (int s = 0; s < seatCount; s++)
            {
                _safe.Add(StartIndex(s));
                _safe.Add((StartIndex(s) + cfg.SafeStarOffset) % Ring);
            }
        }

        public int StartIndex(int boardPos) => (CellsPerArm * boardPos) % Ring;

        /// <summary>Global ring cell for a seat's on-ring progress (0..HomeEntryOffset).</summary>
        public int RingIndexFor(int boardPos, int progress) => (StartIndex(boardPos) + progress) % Ring;

        public bool IsSafe(int ringIndex) => _safe.Contains(ringIndex);
        public int SafeCount => _safe.Count;

        public bool IsOnRing(int progress) => progress >= 0 && progress <= HomeEntryOffset;
        public bool IsHome(int progress) => progress >= FinalHomeProgress;

        public CellRef Resolve(int boardPos, int progress)
        {
            if (progress < 0) return CellRef.Base(boardPos);
            if (progress <= HomeEntryOffset) return CellRef.Ring(RingIndexFor(boardPos, progress));
            return CellRef.Home(boardPos, progress - (HomeEntryOffset + 1));
        }

        public static BoardType BoardTypeFor(int playerCount)
        {
            if (playerCount <= 4) return BoardType.Classic4;
            if (playerCount <= 6) return BoardType.Hex6;
            if (playerCount <= 8) return BoardType.Oct8;
            return BoardType.Dec10;
        }
    }
}
