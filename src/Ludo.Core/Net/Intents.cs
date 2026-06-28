namespace Ludo.Core
{
    /// <summary>What a client may ask the authoritative server to do.</summary>
    public enum IntentType : byte { Roll = 0, MoveToken = 1 }

    /// <summary>A client→server message. The server validates it before applying anything.</summary>
    public readonly struct PlayerIntent
    {
        public readonly IntentType Type;
        public readonly int SeatBoardPos;
        public readonly int TokenId;

        public PlayerIntent(IntentType type, int seatBoardPos, int tokenId = -1)
        {
            Type = type; SeatBoardPos = seatBoardPos; TokenId = tokenId;
        }

        public static PlayerIntent Roll(int seatBoardPos) => new PlayerIntent(IntentType.Roll, seatBoardPos);
        public static PlayerIntent Move(int seatBoardPos, int tokenId) => new PlayerIntent(IntentType.MoveToken, seatBoardPos, tokenId);

        public override string ToString() => Type == IntentType.Roll ? $"Roll(seat {SeatBoardPos})" : $"Move(seat {SeatBoardPos}, token {TokenId})";
    }
}
