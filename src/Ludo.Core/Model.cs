using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>A resolved physical board cell. Ring cells collide by RingIndex; Home cells are private per seat.</summary>
    public readonly struct CellRef
    {
        public readonly CellKind Kind;
        public readonly int RingIndex;   // valid when Kind == Ring
        public readonly int Seat;        // valid when Kind == Base/Home
        public readonly int HomeDepth;   // valid when Kind == Home (0..HomeColumnLen-1; last = final home)

        private CellRef(CellKind kind, int ringIndex, int seat, int homeDepth)
        {
            Kind = kind; RingIndex = ringIndex; Seat = seat; HomeDepth = homeDepth;
        }

        public static CellRef Base(int seat) => new CellRef(CellKind.Base, -1, seat, -1);
        public static CellRef Ring(int ringIndex) => new CellRef(CellKind.Ring, ringIndex, -1, -1);
        public static CellRef Home(int seat, int depth) => new CellRef(CellKind.Home, -1, seat, depth);

        public override string ToString() =>
            Kind == CellKind.Ring ? $"R{RingIndex}" :
            Kind == CellKind.Home ? $"H{Seat}:{HomeDepth}" : $"Base{Seat}";
    }

    /// <summary>One playing piece. Progress: -1 = in base; 0..HomeEntryOffset = on ring; then home column; final = home.</summary>
    public sealed class Token
    {
        public int Id;
        public int Progress = -1;
        public bool InBase => Progress < 0;
    }

    public sealed class Seat
    {
        public int BoardPos;             // 0..N-1 position on the board (drives colour, start cell, home column)
        public PlayerColor Color;
        public int TeamId = -1;          // -1 in FFA
        public bool IsBot;
        public string PlayerId = "";
        public string Name = "";
        public Token[] Tokens;
        public int? FinishRank;          // null until all tokens home
        public ConnectionState Connection = ConnectionState.Online;

        public Seat(int boardPos, int tokensPerSeat)
        {
            BoardPos = boardPos;
            Tokens = new Token[tokensPerSeat];
            for (int i = 0; i < tokensPerSeat; i++) Tokens[i] = new Token { Id = i };
        }

        public bool AllHome(BoardGeometry g)
        {
            foreach (var t in Tokens) if (t.Progress < g.FinalHomeProgress) return false;
            return true;
        }

        /// <summary>Tokens that have left base but not yet reached home.</summary>
        public int TokensOnBoard(BoardGeometry g)
        {
            int c = 0;
            foreach (var t in Tokens) if (t.Progress >= 0 && t.Progress < g.FinalHomeProgress) c++;
            return c;
        }
    }

    /// <summary>A single legal move of one token.</summary>
    public readonly struct Move
    {
        public readonly int SeatBoardPos;
        public readonly int TokenId;
        public readonly int FromProgress;
        public readonly int ToProgress;
        public readonly bool IsUnlock;

        public Move(int seatBoardPos, int tokenId, int from, int to, bool isUnlock)
        {
            SeatBoardPos = seatBoardPos; TokenId = tokenId; FromProgress = from; ToProgress = to; IsUnlock = isUnlock;
        }

        public override string ToString() =>
            IsUnlock ? $"unlock t{TokenId}->0" : $"t{TokenId} {FromProgress}->{ToProgress}";
    }

    public sealed class MoveResult
    {
        public Move Move;
        public readonly List<(int seat, int token)> Captures = new List<(int, int)>();
        public bool ReachedHome;
        public bool GrantsBonus;
        public bool SeatFinished;
    }
}
