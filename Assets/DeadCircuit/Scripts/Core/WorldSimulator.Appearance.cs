namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void SetAppearance(PlayerState player, int code)
        {
            if (AppearanceCatalog.IsValid(code)) player.appearance = code;
        }

        private static int CopyAppearance(int code, int saveVersion)
        {
            // Pre-v14 saves never owned this field, including unexpected JSON values.
            if (saveVersion < 14) return AppearanceCatalog.DefaultCode;
            SaveCheck(AppearanceCatalog.IsValid(code), "player appearance");
            return code;
        }
    }
}
