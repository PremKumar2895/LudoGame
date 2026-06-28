using System;
using System.IO;
using UnityEngine;
using Ludo.Core;

namespace LudoGame
{
    /// <summary>
    /// Loads the secrets-free gameplay tunables (StreamingAssets/ludo.gameplay.json) into a
    /// Ludo.Core.GameConfig. Service IDs/secrets are NOT here — they come from the per-SDK
    /// settings (Firebase google-services.json, Photon settings, etc.).
    /// </summary>
    public static class GameplayConfigLoader
    {
        public const string FileName = "ludo.gameplay.json";

        public static GameConfig Load()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, FileName);
                // Editor/standalone: direct file read. (Android keeps StreamingAssets in the APK —
                // we'll switch to an async UnityWebRequest/Addressables load in the client phase.)
                if (File.Exists(path))
                {
                    var dto = JsonUtility.FromJson<GameplayDto>(File.ReadAllText(path));
                    if (dto != null) return dto.ToConfig();
                }
                else
                {
                    Debug.LogWarning($"[Ludo] {FileName} not found at {path}; using engine defaults.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] gameplay config load failed: " + e.Message + " — using defaults.");
            }
            return GameConfig.Default();
        }

        [Serializable]
        private class GameplayDto
        {
            public int maxPlayers = 10;
            public int tokensPerSeat = 4;
            public int ringCellsPerArm = 13;
            public int[] unlockRolls = { 6 };
            public int homeEntryOffset = 50;
            public int homeColumnLen = 6;
            public int safeStarOffset = 8;
            public bool exactFinish = true;
            public bool bonusOnSix = true;
            public bool bonusOnCapture = true;
            public bool bonusOnHome = true;
            public bool threeSixCancels = true;
            public bool blocksEnabled = true;
            public bool blockBlocksPassage = true;
            public bool teamSharedTurns = true;
            public bool teamBlocks = false;
            public bool autoMoveSingle = true;
            public int turnTimeoutSec = 60;
            public int turnWarnSec = 10;
            public bool pitySixEnabled = false;

            public GameConfig ToConfig() => new GameConfig
            {
                MaxPlayers = maxPlayers,
                TokensPerSeat = tokensPerSeat,
                RingCellsPerArm = ringCellsPerArm,
                UnlockRolls = (unlockRolls != null && unlockRolls.Length > 0) ? unlockRolls : new[] { 6 },
                HomeEntryOffset = homeEntryOffset,
                HomeColumnLen = homeColumnLen,
                SafeStarOffset = safeStarOffset,
                ExactFinish = exactFinish,
                BonusOnSix = bonusOnSix,
                BonusOnCapture = bonusOnCapture,
                BonusOnHome = bonusOnHome,
                ThreeSixCancels = threeSixCancels,
                BlocksEnabled = blocksEnabled,
                BlockBlocksPassage = blockBlocksPassage,
                TeamSharedTurns = teamSharedTurns,
                TeamBlocks = teamBlocks,
                AutoMoveSingle = autoMoveSingle,
                TurnTimeoutSec = turnTimeoutSec,
                TurnWarnSec = turnWarnSec,
                PitySixEnabled = pitySixEnabled
            };
        }
    }
}
