using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        readonly RectTransform[] settingsPages = new RectTransform[3];
        readonly UiButton[] settingsTabs = new UiButton[3];
        readonly Dictionary<DemoBinding, UiButton> bindingButtons = new Dictionary<DemoBinding, UiButton>();
        RectTransform displayConfirmation;
        UnityEngine.UI.ScrollRect bindingScroll;
        TextMeshProUGUI bindingNotice, graphicsResolution, graphicsMode, graphicsQuality, graphicsVSync, graphicsFrameRate;
        TextMeshProUGUI displayConfirmationText;
        TextMeshProUGUI controlsHelpText;
        DemoBinding? awaitingBinding;
        Vector2Int[] availableResolutions = Array.Empty<Vector2Int>();
        readonly FullScreenMode[] availableDisplayModes =
        { FullScreenMode.Windowed, FullScreenMode.FullScreenWindow, FullScreenMode.MaximizedWindow };
        readonly int[] availableFrameRates = { 30, 60, 90, 120, 144, 0 };
        int resolutionIndex, displayModeIndex, qualityIndex, frameRateIndex;
        int previousWidth, previousHeight;
        FullScreenMode previousDisplayMode;
        float displayConfirmationDeadline;

        void BuildSettingsPages(RectTransform card)
        {
            string[] names = { "游戏", "操作", "画面" };
            for (int i = 0; i < settingsTabs.Length; i++)
            {
                int index = i;
                settingsTabs[i] = Button("Settings Tab " + names[i], card, names[i], 28 + i * 154, 82, 142, 38,
                    () => SetSettingsPage(index), i == 0);
                settingsPages[i] = NewRect("Settings Page " + names[i], card);
                Place(settingsPages[i], 24, 132, 952, 508);
            }
            BuildGeneralSettings(settingsPages[0]);
            BuildKeySettings(settingsPages[1]);
            BuildGraphicsSettings(settingsPages[2]);
            SetSettingsPage(0);
        }

        void BuildGeneralSettings(RectTransform page)
        {
            Label("Master Volume Label", page, "主音量", 8, 10, 440, 30, 19, Ink);
            volumeValue = Label("Master Volume Value", page, "", 820, 10, 110, 30, 18, Accent);
            volumeSlider = PreferenceSlider("Master Volume", page, 8, 50, 922, 0f, 1f,
                Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, .7f)), ApplyVolume);
            Label("Effects Volume Label", page, "音效音量", 8, 112, 440, 30, 19, Ink);
            effectsValue = Label("Effects Volume Value", page, "", 820, 112, 110, 30, 18, Accent);
            effectsSlider = PreferenceSlider("Effects Volume", page, 8, 152, 922, 0f, 1f,
                Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsPreference, .8f)), ApplyEffectsVolume);
            Label("UI Scale Label", page, "界面缩放", 8, 214, 440, 30, 19, Ink);
            scaleValue = Label("UI Scale Value", page, "", 820, 214, 110, 30, 18, Accent);
            scaleSlider = PreferenceSlider("UI Scale", page, 8, 254, 922, .8f, 1f,
                Mathf.Clamp(PlayerPrefs.GetFloat(ScalePreference, 1f), .8f, 1f), ApplyUiScale);
            Label("UI Scale Hint", page, "80–100% · 调整文字与按钮的大小", 8, 312, 922, 28, 15, Muted);
            UiButton reset = Button("Reset Game Settings", page, "恢复全部默认", 8, 376, 220, 44, ResetGameSettings);
            SetButtonArt(reset, "ui_refresh", 24);
            Label("Settings Persistence", page, "设置自动保存在本机", 260, 382, 670, 32, 15, Muted);
            ApplyVolume(volumeSlider.value);
            ApplyEffectsVolume(effectsSlider.value);
            ApplyUiScale(scaleSlider.value);
        }

        void BuildKeySettings(RectTransform page)
        {
            RectTransform content = ScrollContent("Key Bindings Scroll", page, 0, 0, 952, 462, out bindingScroll);
            bindingNotice = Label("Key Binding Feedback", page, "点击按键开始修改；冲突的按键不能重复分配。",
                8, 468, 928, 32, 15, Muted);
            AddBindingGroup(content, "移动与战斗", new[]
            { DemoBinding.MoveUp, DemoBinding.MoveLeft, DemoBinding.MoveDown, DemoBinding.MoveRight,
                DemoBinding.Sprint, DemoBinding.Aim, DemoBinding.Force, DemoBinding.Work, DemoBinding.Fight });
            AddBindingGroup(content, "交互与物品", new[]
            { DemoBinding.Interact, DemoBinding.Dismantle, DemoBinding.Repair, DemoBinding.ReloadRotate,
                DemoBinding.SwitchGear, DemoBinding.Eat, DemoBinding.Craft, DemoBinding.Save });
            AddBindingGroup(content, "界面", new[]
            { DemoBinding.Inventory, DemoBinding.Technology, DemoBinding.Growth, DemoBinding.Help,
                DemoBinding.Survival, DemoBinding.Back });
            AddBindingGroup(content, "建造快捷栏", new[]
            { DemoBinding.Hotbar1, DemoBinding.Hotbar2, DemoBinding.Hotbar3, DemoBinding.Hotbar4,
                DemoBinding.Hotbar5, DemoBinding.Hotbar6, DemoBinding.Hotbar7, DemoBinding.Hotbar8,
                DemoBinding.Hotbar9, DemoBinding.Hotbar0 });
            bindingScroll.verticalNormalizedPosition = 1f;
        }

        void AddBindingGroup(Transform content, string title, DemoBinding[] bindings)
        {
            FlowLabel("Key Group " + title, content, title, 910, 30, 18, Accent);
            foreach (DemoBinding binding in bindings)
            {
                RectTransform row = FlowRow("Key Row " + binding, content, 910, 42);
                Label("Action", row, DemoKeyBindings.Name(binding), 8, 2, 670, 36, 16, Ink);
                UiButton button = Button("Rebind " + binding, row, DemoKeyBindings.Display(DemoKeyBindings.Get(binding)),
                    712, 2, 188, 36, () => BeginBindingCapture(binding));
                bindingButtons[binding] = button;
            }
        }

        void BuildGraphicsSettings(RectTransform page)
        {
            RefreshAvailableResolutions();
            displayModeIndex = FindDisplayMode((FullScreenMode)PlayerPrefs.GetInt(
                DemoGraphicsSettings.DisplayModePreference, (int)Screen.fullScreenMode));
            qualityIndex = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            frameRateIndex = FindFrameRate(PlayerPrefs.GetInt(DemoGraphicsSettings.FrameRatePreference, 60));
            graphicsResolution = MakeChoiceRow(page, "分辨率", 8, 12, CycleResolution);
            graphicsMode = MakeChoiceRow(page, "窗口模式", 8, 90, CycleDisplayMode);
            graphicsQuality = MakeChoiceRow(page, "画质", 8, 168, CycleQuality);
            graphicsVSync = MakeChoiceRow(page, "垂直同步", 8, 246, _ => ToggleVSync());
            graphicsFrameRate = MakeChoiceRow(page, "帧率上限", 8, 324, CycleFrameRate);
            UiButton applyDisplay = Button("Apply Display Settings", page, "应用分辨率与窗口模式",
                8, 410, 300, 44, BeginDisplayPreview, true);
            SetButtonArt(applyDisplay, "ui_settings", 22);
            Label("Display Hint", page, "显示模式将在确认后保存；15 秒内未确认会自动恢复。",
                326, 414, 604, 36, 15, Muted);
            BuildDisplayConfirmation();
            RefreshGraphicsLabels();
        }

        TextMeshProUGUI MakeChoiceRow(RectTransform page, string title, float x, float top, Action<int> cycle)
        {
            Label("Graphics Label " + title, page, title, x, top, 270, 48, 18, Ink);
            Button("Previous " + title, page, "<", 366, top + 5, 44, 38, () => cycle(-1));
            TextMeshProUGUI value = Label("Value " + title, page, "", 424, top + 5, 300, 38, 17, Accent);
            value.alignment = TextAlignmentOptions.Center;
            Button("Next " + title, page, ">", 738, top + 5, 44, 38, () => cycle(1));
            return value;
        }

        void BuildDisplayConfirmation()
        {
            displayConfirmation = Backdrop("Display Confirmation", settingsPanel, new Color32(4, 12, 8, 232));
            RectTransform card = Box("Display Confirmation Card", displayConfirmation, 0, 0, 540, 244, Panel, true);
            Center(card, 540, 244);
            PanelRule(card, 540, 62);
            Label("Display Confirmation Heading", card, "确认显示设置", 24, 14, 492, 34, 23, Ink, FontStyles.Bold);
            displayConfirmationText = Label("Display Confirmation Text", card, "", 24, 82, 492, 66, 17, Amber);
            Button("Keep Display Settings", card, "保留设置", 24, 174, 230, 42, KeepDisplayPreview, true);
            Button("Revert Display Settings", card, "恢复原设置", 286, 174, 230, 42, RevertDisplayPreview);
            displayConfirmation.gameObject.SetActive(false);
        }

        void SetSettingsPage(int index)
        {
            index = Mathf.Clamp(index, 0, settingsPages.Length - 1);
            for (int i = 0; i < settingsPages.Length; i++)
            {
                settingsPages[i].gameObject.SetActive(i == index);
                Color tint = i == index ? Selected : Raised;
                settingsTabs[i].image.color = tint;
                UnityEngine.UI.ColorBlock colors = settingsTabs[i].button.colors;
                colors.normalColor = tint;
                colors.selectedColor = tint;
                colors.highlightedColor = Color.Lerp(tint, Ink, .12f);
                settingsTabs[i].button.colors = colors;
            }
        }

        bool UpdateSettingsInput(Keyboard keyboard)
        {
            if (settingsPanel == null || !settingsPanel.gameObject.activeSelf) return false;
            if (awaitingBinding.HasValue)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    awaitingBinding = null;
                    Set(bindingNotice, "已取消改键。");
                }
                else if (keyboard != null) CaptureBindingKey(keyboard);
                return true;
            }
            if (displayConfirmation.gameObject.activeSelf)
            {
                if (Time.unscaledTime >= displayConfirmationDeadline) RevertDisplayPreview();
                else if (DemoKeyBindings.WasPressed(keyboard, DemoBinding.Back)) RevertDisplayPreview();
                else Set(displayConfirmationText, "请在 " + Mathf.CeilToInt(displayConfirmationDeadline - Time.unscaledTime) +
                    " 秒内确认；超时将自动恢复。");
                return true;
            }
            if (DemoKeyBindings.WasPressed(keyboard, DemoBinding.Back)) CloseSettings();
            return true;
        }

        void BeginBindingCapture(DemoBinding binding)
        {
            if (awaitingBinding == binding)
            {
                awaitingBinding = null;
                UpdateBindingButton(binding);
                Set(bindingNotice, "已取消改键。");
                return;
            }
            awaitingBinding = binding;
            foreach (DemoBinding key in bindingButtons.Keys) UpdateBindingButton(key);
            Set(bindingButtons[binding].label, "按下按键…");
            Set(bindingNotice, "正在修改：" + DemoKeyBindings.Name(binding) + " · Esc 取消");
        }

        void CaptureBindingKey(Keyboard keyboard)
        {
            foreach (Key key in Enum.GetValues(typeof(Key)))
            {
                if (!DemoKeyBindings.IsBindable(key) || !keyboard[key].wasPressedThisFrame) continue;
                DemoBinding binding = awaitingBinding.Value;
                if (!DemoKeyBindings.TrySet(binding, key, out DemoBinding conflict))
                {
                    Set(bindingNotice, "按键已分配给“" + DemoKeyBindings.Name(conflict) + "”。请选择其他按键。");
                    return;
                }
                awaitingBinding = null;
                PlayerPrefs.Save();
                UpdateBindingButton(binding);
                RefreshControlsHelp();
                Set(bindingNotice, DemoKeyBindings.Name(binding) + " 已设置为 " + DemoKeyBindings.Display(key) + "。");
                return;
            }
        }

        void UpdateBindingButton(DemoBinding binding)
        {
            if (!bindingButtons.TryGetValue(binding, out UiButton button)) return;
            Set(button.label, awaitingBinding == binding ? "按下按键…" :
                DemoKeyBindings.Display(DemoKeyBindings.Get(binding)));
        }

        string KeyHint(DemoBinding binding) => DemoKeyBindings.Hint(binding);

        string BuildControlsHelpText()
        {
            string KeyName(DemoBinding binding) => DemoKeyBindings.Display(DemoKeyBindings.Get(binding));
            return "移动 " + KeyName(DemoBinding.MoveUp) + " / " + KeyName(DemoBinding.MoveLeft) + " / " +
                KeyName(DemoBinding.MoveDown) + " / " + KeyName(DemoBinding.MoveRight) + " · 冲刺 " + KeyName(DemoBinding.Sprint) + "\n" +
                "左键移动、拾取或操作目标 · " + KeyName(DemoBinding.Interact) + " 执行交互 · 右键次要操作" + "\n" +
                "按住 " + KeyName(DemoBinding.Work) + " 就近工作 · 按住 " + KeyName(DemoBinding.Fight) + " 自动战斗 · " +
                KeyName(DemoBinding.Force) + " + 战斗 / 制作执行强制操作" + "\n" +
                KeyName(DemoBinding.Aim) + " 精确瞄准并按住左键攻击 · 鼠标滚轮缩放" + "\n" +
                "建造：左键放蓝图 · " + KeyName(DemoBinding.Interact) + " 投料 · 右键取消 · " +
                KeyName(DemoBinding.ReloadRotate) + " 旋转 / 装填 · 建造快捷栏 " + HotbarKeyList() + "\n" +
                KeyName(DemoBinding.SwitchGear) + " 切换装备 · " + KeyName(DemoBinding.Eat) + " 进食 · " +
                KeyName(DemoBinding.Dismantle) + " 拆除 · " + KeyName(DemoBinding.Repair) + " 维修 · " +
                KeyName(DemoBinding.Craft) + " 快捷制作" + "\n" +
                KeyName(DemoBinding.Inventory) + " 背包 · " + KeyName(DemoBinding.Technology) + " 科技 · " +
                KeyName(DemoBinding.Growth) + " 成长 · " + KeyName(DemoBinding.Survival) + " 生存手册 · " +
                KeyName(DemoBinding.Help) + " 帮助" + "\n" +
                KeyName(DemoBinding.Back) + " 关闭界面 / 游戏菜单 · " + KeyName(DemoBinding.Save) + " 手动保存";
        }

        void RefreshControlsHelp()
        {
            if (controlsHelpText != null) SetFlowText(controlsHelpText, BuildControlsHelpText(), 172);
        }

        string HotbarKeyList() => string.Join(" / ",
            KeyHint(DemoBinding.Hotbar1), KeyHint(DemoBinding.Hotbar2), KeyHint(DemoBinding.Hotbar3),
            KeyHint(DemoBinding.Hotbar4), KeyHint(DemoBinding.Hotbar5), KeyHint(DemoBinding.Hotbar6),
            KeyHint(DemoBinding.Hotbar7), KeyHint(DemoBinding.Hotbar8), KeyHint(DemoBinding.Hotbar9), KeyHint(DemoBinding.Hotbar0));

        void ResetGameSettings()
        {
            if (displayConfirmation != null && displayConfirmation.gameObject.activeSelf) RevertDisplayPreview();
            volumeSlider.value = .7f;
            effectsSlider.value = .8f;
            scaleSlider.value = 1f;
            DemoGraphicsSettings.Reset();
            RefreshAvailableResolutions();
            displayModeIndex = FindDisplayMode(FullScreenMode.Windowed);
            resolutionIndex = FindResolution(1440, 900);
            qualityIndex = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            frameRateIndex = FindFrameRate(60);
            foreach (DemoBinding binding in bindingButtons.Keys) UpdateBindingButton(binding);
            RefreshGraphicsLabels();
            Set(bindingNotice, "全部设置已恢复默认。");
        }

        void RefreshAvailableResolutions()
        {
            var options = new List<Vector2Int>();
            foreach (Resolution mode in Screen.resolutions)
            {
                if (mode.width < 800 || mode.height < 600) continue;
                if (options.Exists(item => item.x == mode.width && item.y == mode.height)) continue;
                options.Add(new Vector2Int(mode.width, mode.height));
            }
            if (!options.Exists(item => item.x == Screen.width && item.y == Screen.height))
                options.Add(new Vector2Int(Screen.width, Screen.height));
            options.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            availableResolutions = options.ToArray();
            resolutionIndex = FindResolution(Screen.width, Screen.height);
            int savedWidth = PlayerPrefs.GetInt(DemoGraphicsSettings.WidthPreference, -1);
            int savedHeight = PlayerPrefs.GetInt(DemoGraphicsSettings.HeightPreference, -1);
            if (savedWidth > 0 && savedHeight > 0) resolutionIndex = FindResolution(savedWidth, savedHeight);
        }

        int FindResolution(int width, int height)
        {
            for (int i = 0; i < availableResolutions.Length; i++)
                if (availableResolutions[i].x == width && availableResolutions[i].y == height) return i;
            return Mathf.Clamp(resolutionIndex, 0, Mathf.Max(0, availableResolutions.Length - 1));
        }

        int FindDisplayMode(FullScreenMode mode)
        {
            for (int i = 0; i < availableDisplayModes.Length; i++) if (availableDisplayModes[i] == mode) return i;
            return 0;
        }

        int FindFrameRate(int cap)
        {
            for (int i = 0; i < availableFrameRates.Length; i++) if (availableFrameRates[i] == cap) return i;
            return 1;
        }

        void CycleResolution(int direction)
        {
            if (availableResolutions.Length == 0) return;
            resolutionIndex = (resolutionIndex + direction + availableResolutions.Length) % availableResolutions.Length;
            RefreshGraphicsLabels();
        }

        void CycleDisplayMode(int direction)
        {
            displayModeIndex = (displayModeIndex + direction + availableDisplayModes.Length) % availableDisplayModes.Length;
            RefreshGraphicsLabels();
        }

        void CycleQuality(int direction)
        {
            int count = QualitySettings.names.Length;
            if (count == 0) return;
            qualityIndex = (qualityIndex + direction + count) % count;
            QualitySettings.SetQualityLevel(qualityIndex, true);
            PlayerPrefs.SetInt(DemoGraphicsSettings.QualityPreference, qualityIndex);
            RefreshGraphicsLabels();
        }

        void ToggleVSync()
        {
            QualitySettings.vSyncCount = QualitySettings.vSyncCount == 0 ? 1 : 0;
            PlayerPrefs.SetInt(DemoGraphicsSettings.VSyncPreference, QualitySettings.vSyncCount);
            DemoGraphicsSettings.ApplyFrameRate();
            RefreshGraphicsLabels();
        }

        void CycleFrameRate(int direction)
        {
            frameRateIndex = (frameRateIndex + direction + availableFrameRates.Length) % availableFrameRates.Length;
            PlayerPrefs.SetInt(DemoGraphicsSettings.FrameRatePreference, availableFrameRates[frameRateIndex]);
            DemoGraphicsSettings.ApplyFrameRate();
            RefreshGraphicsLabels();
        }

        void RefreshGraphicsLabels()
        {
            if (graphicsResolution == null) return;
            Vector2Int resolution = availableResolutions.Length == 0 ? new Vector2Int(Screen.width, Screen.height) :
                availableResolutions[Mathf.Clamp(resolutionIndex, 0, availableResolutions.Length - 1)];
            Set(graphicsResolution, resolution.x + " × " + resolution.y);
            FullScreenMode mode = availableDisplayModes[Mathf.Clamp(displayModeIndex, 0, availableDisplayModes.Length - 1)];
            Set(graphicsMode, mode == FullScreenMode.Windowed ? "窗口" :
                mode == FullScreenMode.MaximizedWindow ? "最大化窗口" : "无边框全屏");
            Set(graphicsQuality, QualitySettings.names.Length == 0 ? "不可用" :
                QualitySettings.names[Mathf.Clamp(qualityIndex, 0, QualitySettings.names.Length - 1)]);
            Set(graphicsVSync, QualitySettings.vSyncCount > 0 ? "开启" : "关闭");
            int cap = availableFrameRates[Mathf.Clamp(frameRateIndex, 0, availableFrameRates.Length - 1)];
            Set(graphicsFrameRate, cap == 0 ? "不限制" : cap + " FPS");
        }

        void BeginDisplayPreview()
        {
            if (availableResolutions.Length == 0) return;
            previousWidth = Screen.width;
            previousHeight = Screen.height;
            previousDisplayMode = Screen.fullScreenMode;
            Vector2Int target = availableResolutions[Mathf.Clamp(resolutionIndex, 0, availableResolutions.Length - 1)];
            FullScreenMode mode = availableDisplayModes[Mathf.Clamp(displayModeIndex, 0, availableDisplayModes.Length - 1)];
            Screen.SetResolution(target.x, target.y, mode);
            displayConfirmationDeadline = Time.unscaledTime + 15f;
            displayConfirmation.gameObject.SetActive(true);
            displayConfirmation.SetAsLastSibling();
            Set(displayConfirmationText, "请在 15 秒内确认；超时将自动恢复。");
        }

        void KeepDisplayPreview()
        {
            if (availableResolutions.Length == 0) return;
            Vector2Int target = availableResolutions[Mathf.Clamp(resolutionIndex, 0, availableResolutions.Length - 1)];
            FullScreenMode mode = availableDisplayModes[Mathf.Clamp(displayModeIndex, 0, availableDisplayModes.Length - 1)];
            DemoGraphicsSettings.SaveDisplay(target.x, target.y, mode);
            PlayerPrefs.Save();
            displayConfirmation.gameObject.SetActive(false);
        }

        void RevertDisplayPreview()
        {
            if (displayConfirmation == null || !displayConfirmation.gameObject.activeSelf) return;
            Screen.SetResolution(previousWidth, previousHeight, previousDisplayMode);
            displayConfirmation.gameObject.SetActive(false);
            displayModeIndex = FindDisplayMode(previousDisplayMode);
            RefreshAvailableResolutions();
            RefreshGraphicsLabels();
        }
    }
}
