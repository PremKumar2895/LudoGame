namespace Ludo.Core
{
    /// <summary>Up to 10 distinct seat colours (classic 4 first).</summary>
    public enum PlayerColor
    {
        Red = 0, Green = 1, Yellow = 2, Blue = 3, Purple = 4,
        Orange = 5, Teal = 6, Pink = 7, Lime = 8, Cyan = 9
    }

    public enum GameMode { FreeForAll, Teams }

    /// <summary>Board sizes we ship dedicated art for. N = seat count.</summary>
    public enum BoardType { Classic4 = 4, Hex6 = 6, Oct8 = 8, Dec10 = 10 }

    public enum MatchPhase { Lobby, InProgress, Finished }

    /// <summary>Where a resolved cell physically lives.</summary>
    public enum CellKind { Base, Ring, Home }

    public enum ConnectionState { Online, Lagging, Reconnecting, Disconnected, Bot }
}
