using UnityEngine;

namespace Mixtape.Core
{
    /// <summary>
    /// Thin wrapper over PlayerPrefs used to persist <see cref="PlayerData"/> as JSON.
    /// Swap the backing store later (file/cloud) without touching the rest of the game.
    /// </summary>
    public static class SaveSystem
    {
        private const string Key = "mixtape.save.v1";

        public static PlayerData Load()
        {
            if (!PlayerPrefs.HasKey(Key))
                return PlayerData.CreateDefault();

            var json = PlayerPrefs.GetString(Key);
            if (string.IsNullOrEmpty(json))
                return PlayerData.CreateDefault();

            try
            {
                var data = JsonUtility.FromJson<PlayerData>(json);
                return data != null ? data.Validated() : PlayerData.CreateDefault();
            }
            catch
            {
                return PlayerData.CreateDefault();
            }
        }

        public static void Save(PlayerData data)
        {
            if (data == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
