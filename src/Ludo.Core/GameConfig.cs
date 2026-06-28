namespace Ludo.Core
{
    /// <summary>
    /// All gameplay tunables in one place (mirrors the "gameplay" block of config/ludo.config.json).
    /// Defaults reproduce classic Ludo on the 4-seat board and scale cleanly to 6/8/10 seats.
    /// </summary>
    public sealed class GameConfig
    {
        // Board / counts
        public int MaxPlayers = 10;
        public int TokensPerSeat = 4;
        public int RingCellsPerArm = 13;     // RING = RingCellsPerArm * seatCount

        // Movement
        public int[] UnlockRolls = { 6 };    // rolls that free a token from base
        public int HomeEntryOffset = 50;     // ring cells travelled before turning into the home column
        public int HomeColumnLen = 6;        // private home cells incl. final home
        public int SafeStarOffset = 8;       // 2nd safe cell per seat = START + this
        public bool ExactFinish = true;      // must land exactly on home

        // Bonus turns
        public bool BonusOnSix = true;
        public bool BonusOnCapture = true;
        public bool BonusOnHome = true;
        public bool ThreeSixCancels = true;  // 3 consecutive 6s forfeits the turn

        // Blockades
        public bool BlocksEnabled = true;
        public bool BlockBlocksPassage = true;

        // Teams
        public bool TeamSharedTurns = true;
        public bool TeamBlocks = false;

        // Flow / timing
        public bool AutoMoveSingle = true;   // auto-apply the only legal move
        public int TurnTimeoutSec = 60;
        public int TurnWarnSec = 10;
        public bool PitySixEnabled = false;

        public static GameConfig Default() => new GameConfig();
    }
}
