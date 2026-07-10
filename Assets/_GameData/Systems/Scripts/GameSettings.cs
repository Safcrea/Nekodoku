using System;
using UnityEngine;

namespace Meowdoku
{
    public static class GameSettings
    {
        private const string MusicEnabledKey = "Nekodoku.Settings.MusicEnabled";
        private const string SfxEnabledKey = "Nekodoku.Settings.SfxEnabled";
        private const string HapticsEnabledKey = "Nekodoku.Settings.HapticsEnabled";

        public static event Action<bool> MusicEnabledChanged;
        public static event Action<bool> SfxEnabledChanged;
        public static event Action<bool> HapticsEnabledChanged;

        public static bool MusicEnabled
        {
            get => GetBool(MusicEnabledKey, true);
            set => SetBool(MusicEnabledKey, value, MusicEnabledChanged);
        }

        public static bool SfxEnabled
        {
            get => GetBool(SfxEnabledKey, true);
            set => SetBool(SfxEnabledKey, value, SfxEnabledChanged);
        }

        public static bool HapticsEnabled
        {
            get => GetBool(HapticsEnabledKey, true);
            set => SetBool(HapticsEnabledKey, value, HapticsEnabledChanged);
        }

        private static bool GetBool(string key, bool defaultValue)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
        }

        private static void SetBool(string key, bool value, Action<bool> changed)
        {
            if (GetBool(key, true) == value)
            {
                return;
            }

            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            changed?.Invoke(value);
        }
    }
}
