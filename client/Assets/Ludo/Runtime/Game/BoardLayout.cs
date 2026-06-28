using UnityEngine;
using Ludo.Core;

namespace LudoGame
{
    /// <summary>
    /// Maps the parametric board (any N = 4/6/8/10) to 2D world positions using a clean RADIAL layout:
    /// the shared ring is a circle; each seat's home column is a spoke inward to the centre; bases sit
    /// just outside near each start cell. Works for every size without bespoke art.
    /// </summary>
    public sealed class BoardLayout
    {
        public readonly BoardGeometry Geo;
        public readonly float RingRadius;
        public readonly float CellSize;
        public readonly Vector2 Center;

        private readonly Vector2[] _ring;            // [Ring]
        private readonly Vector2[][] _home;          // [N][HomeColumnLen]
        private readonly Vector2[][] _base;          // [N][tokensPerSeat]

        public BoardLayout(BoardGeometry geo, int tokensPerSeat, float ringRadius = 4.2f)
        {
            Geo = geo;
            RingRadius = ringRadius;
            Center = Vector2.zero;
            CellSize = 2f * Mathf.PI * ringRadius / geo.Ring;

            _ring = new Vector2[geo.Ring];
            for (int i = 0; i < geo.Ring; i++)
            {
                float ang = Mathf.Deg2Rad * (90f - 360f * i / geo.Ring); // start at top, go clockwise
                _ring[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ringRadius;
            }

            int n = geo.SeatCount, L = geo.HomeColumnLen;
            _home = new Vector2[n][];
            _base = new Vector2[n][];
            for (int s = 0; s < n; s++)
            {
                int he = (geo.StartIndex(s) + geo.HomeEntryOffset) % geo.Ring;
                _home[s] = new Vector2[L];
                for (int d = 0; d < L; d++)
                    _home[s][d] = Vector2.Lerp(_ring[he], Center, (d + 1f) / (L + 1f));

                int start = geo.StartIndex(s);
                Vector2 baseCenter = _ring[start] * 1.28f;
                Vector2 outward = _ring[start].normalized;
                Vector2 tangent = new Vector2(-outward.y, outward.x);
                float g = CellSize * 0.9f;
                _base[s] = new[]
                {
                    baseCenter + (-tangent - outward) * g * 0.5f,
                    baseCenter + ( tangent - outward) * g * 0.5f,
                    baseCenter + (-tangent + outward) * g * 0.5f,
                    baseCenter + ( tangent + outward) * g * 0.5f,
                };
            }
        }

        public Vector2 Ring(int i) => _ring[((i % _ring.Length) + _ring.Length) % _ring.Length];
        public Vector2 HomeCell(int boardPos, int depth) => _home[boardPos][depth];
        public Vector2 BaseSlot(int boardPos, int tokenId) => _base[boardPos][tokenId % _base[boardPos].Length];

        /// <summary>World position of a token at a given progress (uses the engine's own cell resolution).</summary>
        public Vector2 World(int boardPos, int progress, int tokenId)
        {
            if (progress < 0) return BaseSlot(boardPos, tokenId);
            var cell = Geo.Resolve(boardPos, progress);
            if (cell.Kind == CellKind.Ring) return Ring(cell.RingIndex);
            if (cell.Kind == CellKind.Home) return HomeCell(cell.Seat, cell.HomeDepth);
            return BaseSlot(boardPos, tokenId);
        }
    }
}
