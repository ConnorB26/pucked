using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Networking
{
    /// <summary>
    /// Simple data for a player's lobby/profile identity.
    /// Stored locally via PlayerPrefs and sent to the host when joining.
    /// </summary>
    [Serializable]
    public struct PlayerProfileData
    {
        public string displayName;
        public Color color;

        public PlayerProfileData(string name, Color color)
        {
            displayName = name;
            this.color = color;
        }
    }

    /// <summary>
    /// Local-only helper for saving/loading the current player's profile.
    /// Used in the main menu before we ever connect to Relay.
    /// </summary>
    public static class LocalPlayerProfile
    {
        private const string NameKey = "Puckd_Profile_Name";
        private const string ColorRKey = "Puckd_Profile_Color_R";
        private const string ColorGKey = "Puckd_Profile_Color_G";
        private const string ColorBKey = "Puckd_Profile_Color_B";

        public static PlayerProfileData LoadOrDefault()
        {
            var name = PlayerPrefs.GetString(NameKey, $"Player {Random.Range(1, 999)}");
            var r = PlayerPrefs.GetFloat(ColorRKey, 0.2f);
            var g = PlayerPrefs.GetFloat(ColorGKey, 0.7f);
            var b = PlayerPrefs.GetFloat(ColorBKey, 1.0f);

            return new PlayerProfileData(name, new Color(r, g, b));
        }

        public static void Save(PlayerProfileData data)
        {
            PlayerPrefs.SetString(NameKey, data.displayName);
            PlayerPrefs.SetFloat(ColorRKey, data.color.r);
            PlayerPrefs.SetFloat(ColorGKey, data.color.g);
            PlayerPrefs.SetFloat(ColorBKey, data.color.b);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Static registry so that the lobby can pass final player profiles
    /// into the game scene. NetworkGameManager reads from here.
    /// </summary>
    public static class MatchPlayerRegistry
    {
        private static readonly Dictionary<ulong, PlayerProfileData> Profiles = new();

        public static void Clear()
        {
            Profiles.Clear();
        }

        public static void SetProfile(ulong clientId, PlayerProfileData profile)
        {
            Profiles[clientId] = profile;
        }

        public static bool TryGetProfile(ulong clientId, out PlayerProfileData profile)
        {
            return Profiles.TryGetValue(clientId, out profile);
        }

        public static IReadOnlyDictionary<ulong, PlayerProfileData> GetAll()
        {
            return Profiles;
        }

        /// <summary>
        /// Bulk-load when the host is about to start a match.
        /// </summary>
        public static void SetAll(Dictionary<ulong, PlayerProfileData> source)
        {
            Profiles.Clear();
            foreach (var kvp in source)
                Profiles[kvp.Key] = kvp.Value;
        }
    }
}