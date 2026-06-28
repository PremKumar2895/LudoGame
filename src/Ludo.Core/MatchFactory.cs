using System;
using System.Collections.Generic;

namespace Ludo.Core
{
    public sealed class PlayerSlot
    {
        public string PlayerId = "";
        public string Name = "";
        public bool IsBot;
    }

    /// <summary>Builds a ready-to-play match. FFA: 2..MaxPlayers. Teams: a full board (4, 6, 8 or 10).</summary>
    public static class MatchFactory
    {
        public static MatchState Create(int playerCount, GameMode mode, GameConfig? config = null,
                                        IList<PlayerSlot>? players = null, string id = "M1")
        {
            var cfg = config ?? GameConfig.Default();
            if (playerCount < 2 || playerCount > cfg.MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(playerCount), $"2..{cfg.MaxPlayers} players supported");

            var boardType = BoardGeometry.BoardTypeFor(playerCount);
            int boardN = (int)boardType;

            if (mode == GameMode.Teams && playerCount != boardN)
                throw new ArgumentException($"Team mode needs a full board: 4, 6, 8 or 10 players (got {playerCount}).");

            var geo = new BoardGeometry(boardN, cfg);
            var state = new MatchState(geo, cfg) { Id = id, Mode = mode, BoardType = boardType };

            int[] positions = AssignPositions(playerCount, boardN);
            for (int i = 0; i < playerCount; i++)
            {
                int pos = positions[i];
                var seat = new Seat(pos, cfg.TokensPerSeat)
                {
                    Color = (PlayerColor)pos,
                    TeamId = mode == GameMode.Teams ? pos % 2 : -1
                };
                if (players != null && i < players.Count)
                {
                    seat.PlayerId = players[i].PlayerId;
                    seat.Name = string.IsNullOrEmpty(players[i].Name) ? $"P{pos}" : players[i].Name;
                    seat.IsBot = players[i].IsBot;
                    seat.Connection = players[i].IsBot ? ConnectionState.Bot : ConnectionState.Online;
                }
                else
                {
                    seat.Name = $"Bot{pos}"; seat.IsBot = true; seat.Connection = ConnectionState.Bot;
                }
                state.Seats.Add(seat);
            }

            state.Seats.Sort((a, b) => a.BoardPos.CompareTo(b.BoardPos));
            state.Phase = MatchPhase.InProgress;
            return state;
        }

        /// <summary>Seat players around the board; partial counts are spread, 2-player sits opposite.</summary>
        private static int[] AssignPositions(int playerCount, int boardN)
        {
            if (playerCount == boardN)
            {
                var all = new int[boardN];
                for (int i = 0; i < boardN; i++) all[i] = i;
                return all;
            }
            if (playerCount == 2 && boardN == 4) return new[] { 0, 2 };
            var pos = new int[playerCount];
            for (int i = 0; i < playerCount; i++) pos[i] = i;
            return pos;
        }
    }
}
