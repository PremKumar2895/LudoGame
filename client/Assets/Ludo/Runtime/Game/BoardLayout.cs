using UnityEngine;
using Ludo.Core;

namespace LudoGame
{
    /// <summary>
    /// Maps the parametric board to 2D world positions. Two looks, same engine cells:
    ///   • N ≤ 4  → the CLASSIC CROSS (familiar Ludo plus-board, 4 corner yards, centre home).
    ///   • N ≥ 6  → a RADIAL polygon (ring as a circle, home columns as spokes) — scales to any size.
    /// Both expose Ring(i)/HomeCell(s,d)/BaseSlot(s,t)/BasePad(s)/World(...) so the renderer is identical.
    /// </summary>
    public sealed class BoardLayout
    {
        public BoardGeometry Geo { get; }
        public float RingRadius { get; private set; }
        public float CellSize { get; private set; }
        public float ViewHalfExtent { get; private set; }   // half-size the camera should frame
        public Vector2 Center { get; private set; }
        public bool IsCross { get; private set; }

        private Vector2[] _ring;         // [Ring]
        private Vector2[][] _home;       // [N][HomeColumnLen]
        private Vector2[][] _base;       // [N][tokensPerSeat] token slots
        private Vector2[] _basePad;      // [N] yard centres

        public BoardLayout(BoardGeometry geo, int tokensPerSeat, float ringRadius = 4.2f)
        {
            Geo = geo;
            Center = Vector2.zero;
            IsCross = geo.SeatCount <= 4;
            if (IsCross) BuildCross();
            else BuildRadial(ringRadius);
        }

        public Vector2 Ring(int i) => _ring[((i % _ring.Length) + _ring.Length) % _ring.Length];
        public Vector2 HomeCell(int boardPos, int depth) => _home[boardPos][Mathf.Clamp(depth, 0, _home[boardPos].Length - 1)];
        public Vector2 BaseSlot(int boardPos, int tokenId) => _base[boardPos][((tokenId % _base[boardPos].Length) + _base[boardPos].Length) % _base[boardPos].Length];
        public Vector2 BasePad(int boardPos) => _basePad[boardPos];

        /// <summary>World position of a token at a given progress (uses the engine's own cell resolution).</summary>
        public Vector2 World(int boardPos, int progress, int tokenId)
        {
            if (progress < 0) return BaseSlot(boardPos, tokenId);
            var cell = Geo.Resolve(boardPos, progress);
            if (cell.Kind == CellKind.Ring) return Ring(cell.RingIndex);
            if (cell.Kind == CellKind.Home) return HomeCell(cell.Seat, cell.HomeDepth);
            return BaseSlot(boardPos, tokenId);
        }

        // ---------------------------------------------------------------- radial (N ≥ 6)

        private void BuildRadial(float ringRadius)
        {
            RingRadius = ringRadius;
            CellSize = 2f * Mathf.PI * ringRadius / Geo.Ring;
            ViewHalfExtent = ringRadius * 1.62f;

            _ring = new Vector2[Geo.Ring];
            for (int i = 0; i < Geo.Ring; i++)
            {
                float ang = Mathf.Deg2Rad * (90f - 360f * i / Geo.Ring); // start at top, clockwise
                _ring[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ringRadius;
            }

            int n = Geo.SeatCount, L = Geo.HomeColumnLen;
            _home = new Vector2[n][]; _base = new Vector2[n][]; _basePad = new Vector2[n];
            for (int s = 0; s < n; s++)
            {
                int he = (Geo.StartIndex(s) + Geo.HomeEntryOffset) % Geo.Ring; // home-column entry cell
                _home[s] = new Vector2[L];
                for (int d = 0; d < L; d++) _home[s][d] = Vector2.Lerp(_ring[he], Center, (d + 1f) / (L + 1f));

                // base aligned with the home column (not the start cell) so each seat's colour reads as one wedge
                Vector2 baseCenter = _ring[he] * 1.28f;
                _basePad[s] = baseCenter;
                Vector2 outward = _ring[he].normalized;
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

        // ---------------------------------------------------------------- classic cross (N ≤ 4)

        // 52-cell track loop in 15×15 grid coords (col,row), top-left origin. Index 13·s is seat s's start
        // and (13·s+HomeEntryOffset) lands on that seat's arm-tip, feeding cleanly into its centre home lane.
        private static readonly int[,] CrossCells =
        {
            {6,1},{6,2},{6,3},{6,4},{6,5},
            {5,6},{4,6},{3,6},{2,6},{1,6},{0,6},
            {0,7},{0,8},
            {1,8},{2,8},{3,8},{4,8},{5,8},
            {6,9},{6,10},{6,11},{6,12},{6,13},{6,14},
            {7,14},{8,14},
            {8,13},{8,12},{8,11},{8,10},{8,9},
            {9,8},{10,8},{11,8},{12,8},{13,8},{14,8},
            {14,7},{14,6},
            {13,6},{12,6},{11,6},{10,6},{9,6},
            {8,5},{8,4},{8,3},{8,2},{8,1},{8,0},
            {7,0},
            {6,0},
        };

        private void BuildCross()
        {
            const float cs = 0.62f;
            CellSize = cs;
            RingRadius = 7f * cs;
            ViewHalfExtent = 7f * cs + 0.5f;

            Vector2 W(float col, float row) => new Vector2((col - 7f) * cs, (7f - row) * cs);

            _ring = new Vector2[Geo.Ring]; // == 52
            for (int i = 0; i < 52; i++) _ring[i] = W(CrossCells[i, 0], CrossCells[i, 1]);

            // each seat's 6-cell home column (arm centre lane, tip → centre)
            var home = new[]
            {
                new[] { new Vector2(7,1), new Vector2(7,2), new Vector2(7,3), new Vector2(7,4), new Vector2(7,5), new Vector2(7,6) },   // seat0 top
                new[] { new Vector2(1,7), new Vector2(2,7), new Vector2(3,7), new Vector2(4,7), new Vector2(5,7), new Vector2(6,7) },   // seat1 left
                new[] { new Vector2(7,13),new Vector2(7,12),new Vector2(7,11),new Vector2(7,10),new Vector2(7,9), new Vector2(7,8) },   // seat2 bottom
                new[] { new Vector2(13,7),new Vector2(12,7),new Vector2(11,7),new Vector2(10,7),new Vector2(9,7), new Vector2(8,7) },   // seat3 right
            };
            var yard = new[] { new Vector2(2.5f, 2.5f), new Vector2(2.5f, 11.5f), new Vector2(11.5f, 11.5f), new Vector2(11.5f, 2.5f) };

            int n = Geo.SeatCount, L = Geo.HomeColumnLen;
            _home = new Vector2[n][]; _base = new Vector2[n][]; _basePad = new Vector2[n];
            for (int s = 0; s < n; s++)
            {
                _home[s] = new Vector2[L];
                for (int d = 0; d < L && d < home[s].Length; d++) _home[s][d] = W(home[s][d].x, home[s][d].y);

                _basePad[s] = W(yard[s].x, yard[s].y);
                _base[s] = new[]
                {
                    W(yard[s].x - 1f, yard[s].y - 1f), W(yard[s].x + 1f, yard[s].y - 1f),
                    W(yard[s].x - 1f, yard[s].y + 1f), W(yard[s].x + 1f, yard[s].y + 1f),
                };
            }
        }
    }
}
