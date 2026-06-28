using UnityEngine;
using Ludo.Core;

namespace LudoGame
{
    /// <summary>Pastel palette from the design. Seat colours indexed to match PlayerColor.</summary>
    public static class BoardColors
    {
        private static readonly Color[] Seats =
        {
            Hex("FF8FA3"), // 0 Red
            Hex("57D295"), // 1 Green
            Hex("FFC04D"), // 2 Yellow
            Hex("8FB8FF"), // 3 Blue
            Hex("C58CF0"), // 4 Purple
            Hex("FF9A4D"), // 5 Orange
            Hex("4FD0C0"), // 6 Teal
            Hex("FF8FD0"), // 7 Pink
            Hex("B6E36B"), // 8 Lime
            Hex("6BD6FF"), // 9 Cyan
        };

        public static Color For(PlayerColor c) => Seats[(int)c % Seats.Length];
        public static Color For(int idx) => Seats[((idx % Seats.Length) + Seats.Length) % Seats.Length];

        public static readonly Color Background = Hex("EFE9FF");
        public static readonly Color Cell = Hex("FFFFFF");
        public static readonly Color Safe = Hex("FFE7C2");
        public static readonly Color Center = Hex("9A7FFF");
        public static readonly Color Outline = Hex("2A2340");

        public static Color Light(Color c, float t = 0.55f) => Color.Lerp(c, Color.white, t);

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var col);
            return col;
        }
    }
}
