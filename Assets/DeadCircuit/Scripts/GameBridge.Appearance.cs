using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class GameBridge
    {
        public const string AppearancePreferenceKey = "DeadCircuit.PlayerAppearance";

        public int AppearanceCode { get; private set; } = AppearanceCatalog.DefaultCode;
        public bool CanEditAppearance => !IsRunning && !connecting && !leaving;

        public bool TrySetAppearance(int code)
        {
            if (!CanEditAppearance || !AppearanceCatalog.IsValid(code)) return false;
            AppearanceCode = code;
            PlayerPrefs.SetInt(AppearancePreferenceKey, code);
            PlayerPrefs.Save();
            return true;
        }

        void LoadAppearancePreference()
        {
            int saved = PlayerPrefs.GetInt(AppearancePreferenceKey, AppearanceCatalog.DefaultCode);
            AppearanceCode = AppearanceCatalog.IsValid(saved) ? saved : AppearanceCatalog.DefaultCode;
        }
    }
}
