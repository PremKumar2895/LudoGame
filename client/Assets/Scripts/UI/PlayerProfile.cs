// Local player identity (name) persisted on the device. The first, SDK-free step toward M6 accounts;
// later this syncs to / is replaced by a Firebase-backed profile.
using UnityEngine;

namespace LudoGame
{
    public static class PlayerProfile
    {
        private const string Key = "ludo.playerName";
        private static string _name;

        public static string Name
        {
            get
            {
                if (_name == null)
                {
                    _name = PlayerPrefs.GetString(Key, "");
                    if (string.IsNullOrWhiteSpace(_name))
                    {
                        _name = "Player" + Random.Range(1000, 9999);
                        PlayerPrefs.SetString(Key, _name);
                    }
                }
                return _name;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                _name = value.Trim();
                if (_name.Length > 14) _name = _name.Substring(0, 14);
                PlayerPrefs.SetString(Key, _name);
            }
        }
    }
}
