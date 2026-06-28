using System.Collections.Generic;
using UnityEngine;
using Ludo.Core;

namespace LudoGame
{
    /// <summary>Builds the procedural board (ring, home spokes, bases, centre) and the token views.</summary>
    public sealed class BoardView
    {
        public readonly Dictionary<int, TokenView[]> Tokens = new Dictionary<int, TokenView[]>();

        public void Build(MatchState state, BoardLayout layout, Transform root)
        {
            var geo = layout.Geo;
            float cs = layout.CellSize;

            for (int i = 0; i < geo.Ring; i++)
                Spawn(root, "ring" + i, layout.Ring(i), geo.IsSafe(i) ? BoardColors.Safe : BoardColors.Cell, cs * 0.78f, 1);

            for (int s = 0; s < geo.SeatCount; s++)
            {
                Color col = BoardColors.For(s);
                Spawn(root, "basepad" + s, layout.Ring(geo.StartIndex(s)) * 1.28f, BoardColors.Light(col, 0.62f), cs * 2.7f, 0);
                for (int d = 0; d < geo.HomeColumnLen; d++)
                    Spawn(root, $"home{s}.{d}", layout.HomeCell(s, d), BoardColors.Light(col, 0.32f), cs * 0.72f, 1);
            }

            Spawn(root, "center", layout.Center, BoardColors.Center, cs * 1.7f, 2);

            float tokenSize = cs * 0.62f;
            foreach (var seat in state.Seats)
            {
                var arr = new TokenView[seat.Tokens.Length];
                for (int t = 0; t < seat.Tokens.Length; t++)
                {
                    var go = new GameObject($"token_{seat.BoardPos}_{t}");
                    go.transform.SetParent(root, false);
                    var tv = go.AddComponent<TokenView>();
                    tv.Init(seat.BoardPos, t, BoardColors.For(seat.Color), tokenSize);
                    tv.SetInstant(layout, seat.Tokens[t].Progress);
                    arr[t] = tv;
                }
                Tokens[seat.BoardPos] = arr;
            }
        }

        private static void Spawn(Transform root, string name, Vector2 pos, Color color, float size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle(); sr.color = color; sr.sortingOrder = order;
        }
    }
}
