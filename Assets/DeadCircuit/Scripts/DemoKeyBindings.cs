using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCircuit
{
    internal enum DemoBinding
    {
        MoveUp, MoveLeft, MoveDown, MoveRight, Sprint, Aim, Force, Work, Fight,
        Interact, Dismantle, Repair, ReloadRotate, SwitchGear, Eat, Craft,
        Inventory, Technology, Growth, Help, Survival, Save, Back,
        Hotbar1, Hotbar2, Hotbar3, Hotbar4, Hotbar5, Hotbar6, Hotbar7, Hotbar8, Hotbar9, Hotbar0
    }

    internal static class DemoKeyBindings
    {
        const string PreferencePrefix = "DeadCircuit.Binding.";
        static readonly DemoBinding[] All = (DemoBinding[])Enum.GetValues(typeof(DemoBinding));

        public static string Name(DemoBinding binding)
        {
            switch (binding)
            {
                case DemoBinding.MoveUp: return "向上移动";
                case DemoBinding.MoveLeft: return "向左移动";
                case DemoBinding.MoveDown: return "向下移动";
                case DemoBinding.MoveRight: return "向右移动";
                case DemoBinding.Sprint: return "冲刺";
                case DemoBinding.Aim: return "精确瞄准";
                case DemoBinding.Force: return "强制操作修饰键";
                case DemoBinding.Work: return "持续工作";
                case DemoBinding.Fight: return "持续战斗";
                case DemoBinding.Interact: return "交互";
                case DemoBinding.Dismantle: return "拆除";
                case DemoBinding.Repair: return "建筑维修";
                case DemoBinding.ReloadRotate: return "装填 / 旋转";
                case DemoBinding.SwitchGear: return "切换装备";
                case DemoBinding.Eat: return "进食";
                case DemoBinding.Craft: return "快捷制作";
                case DemoBinding.Inventory: return "背包";
                case DemoBinding.Technology: return "科技";
                case DemoBinding.Growth: return "成长";
                case DemoBinding.Help: return "帮助";
                case DemoBinding.Survival: return "生存手册";
                case DemoBinding.Save: return "手动保存";
                case DemoBinding.Back: return "关闭 / 游戏菜单";
                default:
                    int slot = binding == DemoBinding.Hotbar0 ? 10 : (int)binding - (int)DemoBinding.Hotbar1 + 1;
                    return "建造快捷栏 " + (slot == 10 ? "0" : slot.ToString());
            }
        }

        public static Key Default(DemoBinding binding)
        {
            switch (binding)
            {
                case DemoBinding.MoveUp: return Key.W;
                case DemoBinding.MoveLeft: return Key.A;
                case DemoBinding.MoveDown: return Key.S;
                case DemoBinding.MoveRight: return Key.D;
                case DemoBinding.Sprint: return Key.LeftShift;
                case DemoBinding.Aim: return Key.LeftAlt;
                case DemoBinding.Force: return Key.LeftCtrl;
                case DemoBinding.Work: return Key.Space;
                case DemoBinding.Fight: return Key.F;
                case DemoBinding.Interact: return Key.E;
                case DemoBinding.Dismantle: return Key.X;
                case DemoBinding.Repair: return Key.G;
                case DemoBinding.ReloadRotate: return Key.R;
                case DemoBinding.SwitchGear: return Key.Q;
                case DemoBinding.Eat: return Key.V;
                case DemoBinding.Craft: return Key.C;
                case DemoBinding.Inventory: return Key.B;
                case DemoBinding.Technology: return Key.T;
                case DemoBinding.Growth: return Key.Tab;
                case DemoBinding.Help: return Key.H;
                case DemoBinding.Survival: return Key.J;
                case DemoBinding.Save: return Key.F5;
                case DemoBinding.Back: return Key.Escape;
                case DemoBinding.Hotbar1: return Key.Digit1;
                case DemoBinding.Hotbar2: return Key.Digit2;
                case DemoBinding.Hotbar3: return Key.Digit3;
                case DemoBinding.Hotbar4: return Key.Digit4;
                case DemoBinding.Hotbar5: return Key.Digit5;
                case DemoBinding.Hotbar6: return Key.Digit6;
                case DemoBinding.Hotbar7: return Key.Digit7;
                case DemoBinding.Hotbar8: return Key.Digit8;
                case DemoBinding.Hotbar9: return Key.Digit9;
                default: return Key.Digit0;
            }
        }

        public static Key Get(DemoBinding binding)
        {
            int value = PlayerPrefs.GetInt(PreferencePrefix + binding, (int)Default(binding));
            return Enum.IsDefined(typeof(Key), value) && IsBindable((Key)value) ? (Key)value : Default(binding);
        }

        public static bool IsPressed(Keyboard keyboard, DemoBinding binding) => IsPressed(keyboard, Get(binding));

        public static bool IsPressed(Keyboard keyboard, Key key)
        {
            if (keyboard == null) return false;
            Key paired = PairedModifier(key);
            return keyboard[key].isPressed || (paired != Key.None && keyboard[paired].isPressed);
        }

        public static bool WasPressed(Keyboard keyboard, DemoBinding binding) => WasPressed(keyboard, Get(binding));

        public static bool WasPressed(Keyboard keyboard, Key key)
        {
            if (keyboard == null) return false;
            Key paired = PairedModifier(key);
            return keyboard[key].wasPressedThisFrame || (paired != Key.None && keyboard[paired].wasPressedThisFrame);
        }

        static Key PairedModifier(Key key)
        {
            switch (key)
            {
                case Key.LeftShift: return Key.RightShift;
                case Key.RightShift: return Key.LeftShift;
                case Key.LeftAlt: return Key.RightAlt;
                case Key.RightAlt: return Key.LeftAlt;
                case Key.LeftCtrl: return Key.RightCtrl;
                case Key.RightCtrl: return Key.LeftCtrl;
                default: return Key.None;
            }
        }

        internal static bool KeysOverlap(Key first, Key second) =>
            first == second || PairedModifier(first) == second || PairedModifier(second) == first;

        public static string Display(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard == null ? key.ToString() : keyboard[key].displayName;
        }

        public static string Hint(DemoBinding binding) => Display(Get(binding));

        public static bool TrySet(DemoBinding binding, Key key, out DemoBinding conflict)
        {
            conflict = default;
            if (!IsBindable(key)) return false;
            foreach (DemoBinding other in All)
            {
                if (other == binding || !KeysOverlap(Get(other), key)) continue;
                conflict = other;
                return false;
            }
            PlayerPrefs.SetInt(PreferencePrefix + binding, (int)key);
            return true;
        }

        public static bool IsBindable(Key key) => key != Key.None &&
            !string.Equals(key.ToString(), "IMESelected", StringComparison.Ordinal);

        public static void Reset()
        {
            foreach (DemoBinding binding in All) PlayerPrefs.DeleteKey(PreferencePrefix + binding);
        }
    }

    internal static class DemoGraphicsSettings
    {
        public const string QualityPreference = "DeadCircuit.Graphics.Quality";
        public const string VSyncPreference = "DeadCircuit.Graphics.VSync";
        public const string FrameRatePreference = "DeadCircuit.Graphics.FrameRate";
        public const string WidthPreference = "DeadCircuit.Graphics.Width";
        public const string HeightPreference = "DeadCircuit.Graphics.Height";
        public const string DisplayModePreference = "DeadCircuit.Graphics.DisplayMode";
        static int startupQuality = -1;

        public static void ApplySaved()
        {
            if (startupQuality < 0) startupQuality = QualitySettings.GetQualityLevel();
            int quality = PlayerPrefs.GetInt(QualityPreference, -1);
            if (quality >= 0 && quality < QualitySettings.names.Length) QualitySettings.SetQualityLevel(quality, true);
            QualitySettings.vSyncCount = Mathf.Clamp(PlayerPrefs.GetInt(VSyncPreference, 0), 0, 1);
            ApplyFrameRate();

            if (!PlayerPrefs.HasKey(WidthPreference) || !PlayerPrefs.HasKey(HeightPreference)) return;
            int width = Mathf.Clamp(PlayerPrefs.GetInt(WidthPreference), 800, 7680);
            int height = Mathf.Clamp(PlayerPrefs.GetInt(HeightPreference), 600, 4320);
            FullScreenMode mode = ReadDisplayMode();
            Screen.SetResolution(width, height, mode);
        }

        public static FullScreenMode ReadDisplayMode()
        {
            int saved = PlayerPrefs.GetInt(DisplayModePreference, (int)FullScreenMode.Windowed);
            FullScreenMode mode = (FullScreenMode)saved;
            return mode == FullScreenMode.Windowed || mode == FullScreenMode.FullScreenWindow ||
                mode == FullScreenMode.MaximizedWindow ? mode : FullScreenMode.Windowed;
        }

        public static void ApplyFrameRate()
        {
            int cap = PlayerPrefs.GetInt(FrameRatePreference, 60);
            Application.targetFrameRate = QualitySettings.vSyncCount > 0 || cap <= 0 ? -1 : Mathf.Clamp(cap, 30, 240);
        }

        public static void SaveDisplay(int width, int height, FullScreenMode mode)
        {
            PlayerPrefs.SetInt(WidthPreference, width);
            PlayerPrefs.SetInt(HeightPreference, height);
            PlayerPrefs.SetInt(DisplayModePreference, (int)mode);
        }

        public static void Reset()
        {
            DemoKeyBindings.Reset();
            PlayerPrefs.DeleteKey(QualityPreference);
            PlayerPrefs.DeleteKey(VSyncPreference);
            PlayerPrefs.DeleteKey(FrameRatePreference);
            PlayerPrefs.DeleteKey(WidthPreference);
            PlayerPrefs.DeleteKey(HeightPreference);
            PlayerPrefs.DeleteKey(DisplayModePreference);
            QualitySettings.SetQualityLevel(startupQuality < 0 ? QualitySettings.GetQualityLevel() : startupQuality, true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Screen.SetResolution(1440, 900, FullScreenMode.Windowed);
            PlayerPrefs.Save();
        }
    }
}
