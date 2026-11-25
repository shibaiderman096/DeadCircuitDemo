#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadCircuit
{
    public sealed partial class DemoRuntimeChecks
    {
        IEnumerator CheckSettingsControls()
        {
            const string inventoryPreference = "DeadCircuit.Binding.Inventory";
            bool hadInventoryPreference = PlayerPrefs.HasKey(inventoryPreference);
            int savedInventoryPreference = PlayerPrefs.GetInt(inventoryPreference);
            Key originalKey = DemoKeyBindings.Get(DemoBinding.Inventory);
            Keyboard originalKeyboard = Keyboard.current;
            InputDevice[] originalDevices = InputSystem.devices.ToArray();
            bool[] originalEnabled = originalDevices.Select(device => device.enabled).ToArray();
            var inputSettings = InputSystem.settings;
            var originalBackground = inputSettings.backgroundBehavior;
            bool originalRunInBackground = Application.runInBackground;
            Keyboard keyboard = null;

            // Keep references to inactive HUD controls; this check runs before a world starts.
            Button openSettings = Rect("Open Settings").GetComponent<Button>();
            Button controlsTab = Rect("Settings Tab 操作").GetComponent<Button>();
            Button generalTab = Rect("Settings Tab 游戏").GetComponent<Button>();
            Button inventoryButton = Rect("Rebind Inventory").GetComponent<Button>();
            TextMeshProUGUI bindingLabel = inventoryButton.GetComponentInChildren<TextMeshProUGUI>(true);
            TextMeshProUGUI inventoryHint = Rect("Open Inventory").GetComponentInChildren<TextMeshProUGUI>(true);
            TextMeshProUGUI notice = Rect("Key Binding Feedback").GetComponent<TextMeshProUGUI>();

            bool PreferenceUnchanged() => PlayerPrefs.HasKey(inventoryPreference) == hadInventoryPreference &&
                (!hadInventoryPreference || PlayerPrefs.GetInt(inventoryPreference) == savedInventoryPreference);
            bool LabelsMatch(Key key) => bindingLabel.text == DemoKeyBindings.Display(key) &&
                inventoryHint.text == "背包 " + DemoKeyBindings.Display(key);

            try
            {
                Require(GameBridge.Instance != null && !GameBridge.Instance.IsRunning && Active("Settings Panel"),
                    "settings QA begins at the open home settings");
                Application.runInBackground = true;
                inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                for (int i = 0; i < originalDevices.Length; i++)
                    if (originalEnabled[i]) InputSystem.DisableDevice(originalDevices[i], true);
                keyboard = InputSystem.AddDevice<Keyboard>("Demo QA Settings Keyboard");
                keyboard.MakeCurrent();
                yield return null;

                RequireSettingsQaPage(0);
                RequireSettingsQaButtons("Settings Tab 游戏", "Settings Tab 操作", "Settings Tab 画面",
                    "Close Settings", "Reset Game Settings", "Open Appearance");
                Click("Settings Tab 操作");
                yield return null;
                RequireSettingsQaPage(1);

                // Drive nested helpers here so an assertion in one unwinds our finally too.
                for (IEnumerator step = ClickScrolled("Rebind Inventory"); step.MoveNext();) yield return step.Current;
                RequireSettingsQaButtons("Rebind Inventory");
                Require(bindingLabel.text == "按下按键…", "inventory row starts key capture");
                for (IEnumerator step = Press(Key.Escape); step.MoveNext();) yield return step.Current;
                RequireSettingsQaPage(1);
                Require(PreferenceUnchanged() && bindingLabel.text == DemoKeyBindings.Display(originalKey) &&
                    notice.text.Contains("已取消"), "Escape cancels capture without closing settings or changing the binding");

                Click("Rebind Inventory");
                Require(bindingLabel.text == "按下按键…", "inventory capture restarts");
                Click("Rebind Inventory");
                yield return null;
                Require(PreferenceUnchanged() && bindingLabel.text == DemoKeyBindings.Display(originalKey) &&
                    notice.text.Contains("已取消"), "clicking the same row cancels capture");

                Click("Rebind Inventory");
                Require(bindingLabel.text == "按下按键…", "capture is pending before switching pages");
                Click("Settings Tab 画面");
                yield return null;
                RequireSettingsQaPage(2);
                Require(PreferenceUnchanged() && bindingLabel.text == DemoKeyBindings.Display(originalKey) &&
                    notice.text.Contains("已取消"), "switching settings pages cancels capture");
                // A menu QA must not resize the Editor or the user's display. Inspect this page only.
                RequireSettingsQaButtons("Apply Display Settings", "Previous 分辨率", "Next 分辨率",
                    "Previous 窗口模式", "Next 窗口模式", "Previous 画质", "Next 画质",
                    "Previous 垂直同步", "Next 垂直同步", "Previous 帧率上限", "Next 帧率上限");
                Require(!Active("Display Confirmation"), "graphics page does not start a display preview");

                var bindings = (DemoBinding[])Enum.GetValues(typeof(DemoBinding));
                Key conflictKey = bindings.Where(binding => binding != DemoBinding.Inventory)
                    .Select(DemoKeyBindings.Get).FirstOrDefault(key => key != Key.Escape && key != Key.Enter &&
                        key != Key.NumpadEnter && key != Key.Space);
                // Prefer I, but respect custom bindings; letters plus F keys outnumber the actions.
                Key freeKey = new[] { Key.I }.Concat(((Key[])Enum.GetValues(typeof(Key))).Where(key =>
                    (key >= Key.A && key <= Key.Z) || (key >= Key.F1 && key <= Key.F12)))
                    .FirstOrDefault(key => !bindings.Any(binding => DemoKeyBindings.KeysOverlap(DemoKeyBindings.Get(binding), key)));
                Require(conflictKey != Key.None && freeKey != Key.None, "settings QA has a conflicting and an unused test key");

                Click("Settings Tab 操作");
                yield return null;
                RequireSettingsQaPage(1);
                for (IEnumerator step = ClickScrolled("Rebind Inventory"); step.MoveNext();) yield return step.Current;
                RequireSettingsQaButtons("Rebind Inventory");
                for (IEnumerator step = Press(conflictKey); step.MoveNext();) yield return step.Current;
                Require(PreferenceUnchanged() && DemoKeyBindings.Get(DemoBinding.Inventory) == originalKey &&
                    bindingLabel.text == "按下按键…" && notice.text.Contains("按键已分配给"),
                    "conflicting key is rejected while capture stays pending");
                for (IEnumerator step = Press(freeKey); step.MoveNext();) yield return step.Current;
                Require(PlayerPrefs.HasKey(inventoryPreference) && PlayerPrefs.GetInt(inventoryPreference) == (int)freeKey &&
                    DemoKeyBindings.Get(DemoBinding.Inventory) == freeKey && LabelsMatch(freeKey),
                    "successful rebind updates the preference, controls row and HUD shortcut label");

                Click("Close Settings");
                yield return null;
                Require(!Active("Settings Panel") && Active("Start Page"), "settings returns to the home menu");
                Click("Open Settings");
                yield return null;
                RequireSettingsQaPage(0);
                Require(PlayerPrefs.GetInt(inventoryPreference) == (int)freeKey && LabelsMatch(freeKey),
                    "saved inventory binding and labels survive reopening settings");
            }
            finally
            {
                try
                {
                    // Restore only the single key this QA can write, including original absence.
                    if (hadInventoryPreference) PlayerPrefs.SetInt(inventoryPreference, savedInventoryPreference);
                    else PlayerPrefs.DeleteKey(inventoryPreference);
                    PlayerPrefs.Save();
                }
                finally
                {
                    try
                    {
                        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                    }
                    finally
                    {
                        inputSettings.backgroundBehavior = originalBackground;
                        Application.runInBackground = originalRunInBackground;
                        for (int i = 0; i < originalDevices.Length; i++)
                        {
                            InputDevice device = originalDevices[i];
                            if (!device.added || device.enabled == originalEnabled[i]) continue;
                            if (originalEnabled[i]) InputSystem.EnableDevice(device);
                            else InputSystem.DisableDevice(device, true);
                        }
                        if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();

                        // Refresh through existing UI callbacks after restoring the original keyboard layout.
                        // Opening settings first also cancels a capture left pending by a failed assertion.
                        openSettings.onClick.Invoke();
                        controlsTab.onClick.Invoke();
                        inventoryButton.onClick.Invoke();
                        inventoryButton.onClick.Invoke();
                        generalTab.onClick.Invoke();
                    }
                }
            }

            yield return null;
            Require(PreferenceUnchanged() && DemoKeyBindings.Get(DemoBinding.Inventory) == originalKey && LabelsMatch(originalKey),
                "restored inventory preference and both shortcut labels agree");
            Require(InputSystem.settings.backgroundBehavior == originalBackground &&
                Application.runInBackground == originalRunInBackground && (keyboard == null || !keyboard.added) &&
                originalDevices.Select((device, index) => !device.added || device.enabled == originalEnabled[index]).All(value => value),
                "settings QA restores its virtual device, background behavior and original device enabled states");
            RequireSettingsQaPage(0);
            RequireSettingsQaButtons("Open Appearance", "Close Settings");
            Debug.Log("DEMO_SETTINGS_QA_PASS");
        }

        static void RequireSettingsQaPage(int selected)
        {
            Require(Active("Settings Panel") && Active("Settings Card") && Active("Start Page") &&
                !Active("Appearance Panel"), "home settings remain open");
            string[] pages = { "Settings Page 游戏", "Settings Page 操作", "Settings Page 画面" };
            for (int i = 0; i < pages.Length; i++)
                Require(Active(pages[i]) == (i == selected), "settings page selection: " + pages[i]);
            RequireSettingsQaButtons("Settings Tab 游戏", "Settings Tab 操作", "Settings Tab 画面", "Close Settings");
        }

        static void RequireSettingsQaButtons(params string[] names)
        {
            Canvas.ForceUpdateCanvases();
            foreach (string name in names)
            {
                Button button = FindButton(name);
                Require(button.IsInteractable() && PointerTargetsButton(button), "settings button receives pointer hits: " + name);
            }
        }
    }
}
#endif
