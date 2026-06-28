using System.Collections.Generic;
using System.IO;

namespace Ludo.Core
{
    public enum TurnSub : byte { AwaitingRoll = 0, AwaitingMove = 1 }

    public sealed class SeatSnapshot
    {
        public int BoardPos;
        public byte Color;
        public sbyte TeamId;     // -1 in FFA
        public bool IsBot;
        public byte Connection;  // ConnectionState
        public sbyte FinishRank; // -1 = not finished
        public sbyte[] Tokens = new sbyte[4]; // per-token progress (-1..56)
    }

    /// <summary>
    /// Compact, serializable view of a match — the server→client wire format. Small enough (≤ a few
    /// hundred bytes even at 10 players) to broadcast on every change for a turn-based game.
    /// </summary>
    public sealed class MatchSnapshot
    {
        public const byte Version = 1;

        public string MatchId = "";
        public byte Mode;             // GameMode
        public byte BoardN;
        public byte Phase;            // MatchPhase
        public byte CurrentSeat;      // board pos whose turn it is
        public sbyte LastRoll;        // -1 = none yet
        public byte ConsecutiveSixes;
        public int TurnNumber;
        public sbyte WinningTeam;     // -1 = none
        public byte Sub;              // TurnSub
        public List<SeatSnapshot> Seats = new List<SeatSnapshot>();
        public List<byte> FinishOrder = new List<byte>();
        public List<byte> MovableTokens = new List<byte>(); // current seat's movable token ids (AwaitingMove)

        public static MatchSnapshot Capture(MatchState s, TurnSub sub, IReadOnlyList<Move> pending)
        {
            var snap = new MatchSnapshot
            {
                MatchId = s.Id,
                Mode = (byte)s.Mode,
                BoardN = (byte)s.Geo.SeatCount,
                Phase = (byte)s.Phase,
                CurrentSeat = (byte)s.Current.BoardPos,
                LastRoll = (sbyte)(s.LastRoll == 0 ? -1 : s.LastRoll),
                ConsecutiveSixes = (byte)s.ConsecutiveSixes,
                TurnNumber = s.TurnNumber,
                WinningTeam = (sbyte)(s.WinningTeam ?? -1),
                Sub = (byte)sub
            };
            foreach (var seat in s.Seats)
            {
                var ss = new SeatSnapshot
                {
                    BoardPos = seat.BoardPos,
                    Color = (byte)seat.Color,
                    TeamId = (sbyte)seat.TeamId,
                    IsBot = seat.IsBot,
                    Connection = (byte)seat.Connection,
                    FinishRank = (sbyte)(seat.FinishRank ?? -1)
                };
                for (int i = 0; i < seat.Tokens.Length && i < 4; i++) ss.Tokens[i] = (sbyte)seat.Tokens[i].Progress;
                snap.Seats.Add(ss);
            }
            foreach (var p in s.FinishOrder) snap.FinishOrder.Add((byte)p);
            if (pending != null) foreach (var m in pending) snap.MovableTokens.Add((byte)m.TokenId);
            return snap;
        }

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Version);
                w.Write(MatchId ?? "");
                w.Write(Mode); w.Write(BoardN); w.Write(Phase); w.Write(CurrentSeat);
                w.Write(LastRoll); w.Write(ConsecutiveSixes); w.Write(TurnNumber);
                w.Write(WinningTeam); w.Write(Sub);
                w.Write((byte)Seats.Count);
                foreach (var s in Seats)
                {
                    w.Write((byte)s.BoardPos); w.Write(s.Color); w.Write(s.TeamId);
                    w.Write(s.IsBot); w.Write(s.Connection); w.Write(s.FinishRank);
                    for (int i = 0; i < 4; i++) w.Write(s.Tokens[i]);
                }
                w.Write((byte)FinishOrder.Count); foreach (var b in FinishOrder) w.Write(b);
                w.Write((byte)MovableTokens.Count); foreach (var b in MovableTokens) w.Write(b);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static MatchSnapshot Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                r.ReadByte(); // version
                var snap = new MatchSnapshot
                {
                    MatchId = r.ReadString(),
                    Mode = r.ReadByte(),
                    BoardN = r.ReadByte(),
                    Phase = r.ReadByte(),
                    CurrentSeat = r.ReadByte(),
                    LastRoll = r.ReadSByte(),
                    ConsecutiveSixes = r.ReadByte(),
                    TurnNumber = r.ReadInt32(),
                    WinningTeam = r.ReadSByte(),
                    Sub = r.ReadByte()
                };
                int seatCount = r.ReadByte();
                for (int i = 0; i < seatCount; i++)
                {
                    var s = new SeatSnapshot
                    {
                        BoardPos = r.ReadByte(),
                        Color = r.ReadByte(),
                        TeamId = r.ReadSByte(),
                        IsBot = r.ReadBoolean(),
                        Connection = r.ReadByte(),
                        FinishRank = r.ReadSByte()
                    };
                    for (int t = 0; t < 4; t++) s.Tokens[t] = r.ReadSByte();
                    snap.Seats.Add(s);
                }
                int fo = r.ReadByte(); for (int i = 0; i < fo; i++) snap.FinishOrder.Add(r.ReadByte());
                int mv = r.ReadByte(); for (int i = 0; i < mv; i++) snap.MovableTokens.Add(r.ReadByte());
                return snap;
            }
        }

        /// <summary>Reconstruct a MatchState mirror from this snapshot — used client-side to render the synced game.</summary>
        public MatchState ToState(GameConfig cfg)
        {
            var geo = new BoardGeometry(BoardN, cfg);
            var s = new MatchState(geo, cfg)
            {
                Id = MatchId,
                Mode = (GameMode)Mode,
                BoardType = (BoardType)BoardN,
                Phase = (MatchPhase)Phase,
                LastRoll = LastRoll < 0 ? 0 : LastRoll,
                ConsecutiveSixes = ConsecutiveSixes,
                TurnNumber = TurnNumber,
                WinningTeam = WinningTeam < 0 ? (int?)null : WinningTeam
            };
            foreach (var ss in Seats)
            {
                var seat = new Seat(ss.BoardPos, cfg.TokensPerSeat)
                {
                    Color = (PlayerColor)ss.Color,
                    TeamId = ss.TeamId,
                    IsBot = ss.IsBot,
                    Connection = (ConnectionState)ss.Connection,
                    FinishRank = ss.FinishRank < 0 ? (int?)null : ss.FinishRank
                };
                for (int i = 0; i < seat.Tokens.Length && i < ss.Tokens.Length; i++)
                    seat.Tokens[i].Progress = ss.Tokens[i];
                s.Seats.Add(seat);
            }
            s.Seats.Sort((a, b) => a.BoardPos.CompareTo(b.BoardPos));
            for (int i = 0; i < s.Seats.Count; i++)
                if (s.Seats[i].BoardPos == CurrentSeat) { s.CurrentIndex = i; break; }
            foreach (var p in FinishOrder) s.FinishOrder.Add(p);
            return s;
        }
    }
}
