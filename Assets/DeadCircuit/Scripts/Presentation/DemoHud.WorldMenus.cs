using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    // Navigation and preferences only. Creation/loading are delegated to GameBridge.
    public sealed partial class DemoHud
    {
        enum MenuPage { Home, Create, Load, Lan }

        sealed class WorldMenuRow
        {
            public string saveId;
            public UiButton widget;
        }

        const string VolumePreference = "DeadCircuit.MasterVolume";
        const string EffectsPreference = "DeadCircuit.EffectsVolume";
        const string ScalePreference = "DeadCircuit.UI.Scale";
        readonly List<WorldMenuRow> worldRows = new List<WorldMenuRow>();
        readonly UiButton[] presetButtons = new UiButton[3];
        readonly UiButton[] densityButtons = new UiButton[3];
        readonly RectTransform[] menuPages = new RectTransform[4];
        RectTransform menuFrame, worldListContent, settingsPanel, toolsTray, buildTray;
        RectTransform deleteWorldDialog;
        CanvasGroup menuInteraction;
        UnityEngine.UI.ScrollRect worldListScroll;
        UnityEngine.UI.Slider volumeSlider, effectsSlider, scaleSlider;
        TMP_InputField worldNameField;
        TextMeshProUGUI creationSummary, worldListEmpty, selectedWorldDetails, selectedWorldSummary;
        TextMeshProUGUI volumeValue, effectsValue, scaleValue, lanStatus;
        UiButton createWorldButton, loadSelectedWorld, inspectToggle, toolsToggle, buildToggle;
        TextMeshProUGUI deleteWorldDetails;
        UiButton deleteSelectedWorld, cancelDeleteWorld;
        MenuPage menuPage;
        WorldPreset worldPreset = WorldPreset.Balanced;
        int cityDensity = 1;
        string selectedSaveId = "";
        string pendingDeleteSaveId;
        bool buildTrayOpen, inspectorEnabled, menuRequestPending;
        float nextMenuRequestAt;

        // Implement these in another DemoHud partial when real systems are connected.
        // Additional modals must participate in close/input capture; hidden hooks add no fake UI.
        partial void CreateAdditionalPanels(RectTransform root);
        partial void RefreshAdditionalPanels(WorldState world, PlayerState player);
        partial void CloseAdditionalPanels();
        partial void HasAdditionalPanelOpen(ref bool open);

        void MakeMenu(RectTransform root)
        {
            menu = Backdrop("Main Menu", root, Recessed);
            menuInteraction = menu.gameObject.AddComponent<CanvasGroup>();
            MakeTitlePanorama(menu);
            menuPages[0] = NewRect("Start Page", menu);
            Stretch(menuPages[0]);
            menuFrame = NewRect("World Menu Frame", menu);
            Center(menuFrame, 1120, 640);
            string[] names = { "Create World Page", "Load World Page", "LAN Page" };
            for (int i = 1; i < menuPages.Length; i++)
            {
                menuPages[i] = Box(names[i - 1], menuFrame, 0, 0, 1120, 560, Panel, true);
                PanelRule(menuPages[i], 1120, 66);
            }
            MakeStartPage(menuPages[0]);
            MakeCreatePage(menuPages[1]);
            MakeLoadPage(menuPages[2]);
            MakeLanPage(menuPages[3]);
            menuStatus = Label("Connection Status", menuFrame, "", 24, 576, 1072, 48, 15, Ink);
            menuStatus.alignment = TextAlignmentOptions.TopLeft;
            MakeSettings(root);
            MakeDeleteWorldDialog(root);
            ShowMenuPage(MenuPage.Home, false);
        }

        void MakeCreatePage(RectTransform page)
        {
            MenuHeading(page, "Create World Heading", "创建世界");
            Label("World Name Label", page, "世界名称", 28, 88, 620, 24, 16, Muted);
            worldNameField = Input("World Name", page, "新的世界", "为这个世界命名", 28, 118, 620, 44, 40);
            Label("Seed Label", page, "种子 · 留空随机，也可输入数字或文字", 28, 178, 620, 24, 16, Muted);
            seedField = Input("World Seed", page, "", "随机 / 数字 / 文字", 28, 208, 490, 44, 128);
            seedField.characterValidation = TMP_InputField.CharacterValidation.None;
            Button("Random World Seed", page, "随机", 532, 208, 116, 44, () =>
            {
                seedField.text = WorldSeed.Random().ToString(CultureInfo.InvariantCulture);
                RefreshCreationSummary();
            });
            Label("World Preset Label", page, "地貌预设", 28, 272, 620, 24, 16, Muted);
            string[] presets = { "均衡", "荒野", "城市" };
            WorldPreset[] values = { WorldPreset.Balanced, WorldPreset.Wilderness, WorldPreset.Urban };
            for (int i = 0; i < presets.Length; i++)
            {
                int index = i;
                presetButtons[i] = Button("World Preset " + values[i], page, presets[i], 28 + i * 212, 306, 196, 46,
                    () => { worldPreset = values[index]; RefreshCreationSummary(); });
            }
            Label("City Density Label", page, "城市密度", 28, 374, 620, 24, 16, Muted);
            string[] densities = { "稀疏", "适中", "密集" };
            for (int i = 0; i < densities.Length; i++)
            {
                int index = i;
                densityButtons[i] = Button("City Density " + i, page, densities[i], 28 + i * 212, 408, 196, 44,
                    () => { cityDensity = index; RefreshCreationSummary(); });
            }
            RectTransform summaryCard = Box("World Configuration Card", page, 680, 92, 412, 360, Recessed);
            Label("World Configuration Heading", summaryCard, "配置摘要", 20, 16, 372, 32, 22, Accent);
            RectTransform content = ScrollContent("World Configuration Scroll View", summaryCard, 14, 62, 384, 280, out _);
            creationSummary = FlowLabel("World Configuration Summary", content, "", 356, 240, 17, Ink);
            createWorldButton = Button("Create World", page, "创建世界并主持", 680, 480, 412, 48, CreateConfiguredWorld, true);
            Label("Create World Notice", page, "创建独立世界；已有存档从「读取世界」进入。", 28, 480, 620, 48, 15, Muted);
            worldNameField.onValueChanged.AddListener(_ => RefreshCreationSummary());
            seedField.onValueChanged.AddListener(_ => RefreshCreationSummary());
            RefreshCreationSummary();
        }

        void MakeLoadPage(RectTransform page)
        {
            MenuHeading(page, "Load World Heading", "读取世界");
            Label("World List Heading", page, "本地存档", 28, 82, 460, 32, 20, Ink, FontStyles.Bold);
            Button("Refresh Worlds", page, "刷新列表", 486, 82, 156, 34, RequestWorldList);
            worldListContent = ScrollContent("World List Scroll View", page, 28, 128, 614, 388, out worldListScroll);
            worldListEmpty = FlowLabel("World List Empty", worldListContent, "暂无存档，请返回创建世界。", 580, 80, 17, Muted);
            RectTransform details = Box("Selected World Card", page, 668, 82, 424, 352, Recessed);
            Label("Selected World Heading", details, "存档详情", 20, 16, 384, 32, 22, Accent);
            RectTransform content = ScrollContent("Selected World Scroll View", details, 14, 64, 396, 274, out _);
            selectedWorldDetails = FlowLabel("Selected World Details", content, "选择左侧存档以查看详情。", 368, 100, 17, Ink);
            selectedWorldSummary = FlowLabel("Selected World Summary", content, "", 368, 80, 15, Muted);
            loadSelectedWorld = Button("Load Selected World", page, "读取选中世界", 668, 454, 258, 48, LoadSelectedWorld, true);
            deleteSelectedWorld = Button("Delete Selected World", page, "删除存档", 938, 454, 154, 48, OpenDeleteWorldDialog);
            deleteSelectedWorld.label.color = Danger;
            deleteSelectedWorld.button.interactable = false;
        }

        void MakeDeleteWorldDialog(RectTransform root)
        {
            deleteWorldDialog = Backdrop("Delete World Dialog", root, new Color32(4, 12, 8, 230));
            RectTransform card = Box("Delete World Card", deleteWorldDialog, 0, 0, 560, 278, Panel, true);
            Center(card, 560, 278);
            PanelRule(card, 560, 66);
            Label("Delete World Heading", card, "删除存档", 28, 16, 504, 38, 27, Danger, FontStyles.Bold);
            deleteWorldDetails = Label("Delete World Details", card, "", 28, 80, 504, 96, 18, Ink);
            deleteWorldDetails.alignment = TextAlignmentOptions.TopLeft;
            Label("Delete World Warning", card, "此世界的进度将永久删除。", 28, 178, 504, 28, 16, Muted);
            cancelDeleteWorld = Button("Cancel World Delete", card, "取消", 28, 216, 246, 42, CloseDeleteWorldDialog);
            UiButton confirm = Button("Confirm World Delete", card, "确认删除", 286, 216, 246, 42, ConfirmDeleteWorld);
            confirm.label.color = Danger;
            deleteWorldDialog.gameObject.SetActive(false);
        }

        void OpenDeleteWorldDialog()
        {
            if (menuPage != MenuPage.Load || menuRequestPending || deleteWorldDialog.gameObject.activeSelf || !bridge.CanDeleteWorld) return;
            WorldSaveInfo world = bridge.Worlds.Find(w => w.saveId == selectedSaveId);
            if (world == null) return;

            pendingDeleteSaveId = world.saveId;
            Set(deleteWorldDetails, Clean(string.IsNullOrWhiteSpace(world.name) ? world.saveId : world.name) +
                "\n种子：" + world.seed.ToString(CultureInfo.InvariantCulture) + "\n修改时间：" + Clean(world.modified));
            deleteWorldDialog.SetAsLastSibling();
            deleteWorldDialog.gameObject.SetActive(true);
            menuInteraction.interactable = false;
            menuInteraction.blocksRaycasts = false;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(cancelDeleteWorld.button.gameObject);
            DemoAudio.UiOpen();
        }

        void CloseDeleteWorldDialog()
        {
            if (!deleteWorldDialog.gameObject.activeSelf) return;
            pendingDeleteSaveId = null;
            deleteWorldDialog.gameObject.SetActive(false);
            menuInteraction.interactable = !settingsPanel.gameObject.activeSelf;
            menuInteraction.blocksRaycasts = true;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            DemoAudio.UiClose();
        }

        void ConfirmDeleteWorld()
        {
            string requested = pendingDeleteSaveId;
            if (string.IsNullOrEmpty(requested) || !deleteWorldDialog.gameObject.activeSelf) return;
            CloseDeleteWorldDialog();

            try
            {
                bridge.DeleteWorld(requested);
                Notify("已删除存档。");
            }
            catch (Exception error) { Notify("删除存档失败：" + error.Message, true); }
            RequestWorldList();
        }

        void MakeLanPage(RectTransform page)
        {
            MenuHeading(page, "LAN Heading", "局域网协作");
            Label("Name Label", page, "角色名", 28, 92, 596, 26, 16, Muted);
            nameField = Input("Player Name", page, bridge.PlayerName, "幸存者", 28, 126, 596, 46, 12);
            Label("IP Label", page, "主机 IPv4 · UDP 7777", 28, 194, 596, 26, 16, Muted);
            ipField = Input("Host IPv4", page, "", "例如 192.168.1.8", 28, 228, 596, 46, 15);
            Button("Join Game", page, "连接主机", 28, 304, 286, 48, Join, true);
            Button("Cancel Connection", page, "取消连接", 330, 304, 294, 48, () => CallBridge(bridge.Leave));
            Button("Start Host", page, "创建世界作为主机", 28, 388, 596, 48, StartNew);
            Label("Host Existing World Hint", page, "继续已有世界：返回后选择「读取世界」。", 28, 456, 596, 40, 15, Muted);
            RectTransform details = Box("LAN Connection Card", page, 660, 92, 432, 420, Recessed);
            Label("LAN Status Heading", details, "连接状态", 20, 18, 392, 32, 22, Accent);
            lanStatus = Label("LAN Connection Status", details, "", 20, 76, 392, 126, 17, Ink);
            lanStatus.alignment = TextAlignmentOptions.TopLeft;
            menuAddress = Label("LAN Address", details, "", 20, 230, 392, 162, 16, Muted);
            menuAddress.alignment = TextAlignmentOptions.TopLeft;
        }

        void MenuHeading(RectTransform page, string name, string title)
        {
            Label(name, page, title, 28, 14, 824, 38, 27, Ink, FontStyles.Bold);
            Button(page.name + " Back", page, "返回", 944, 16, 148, 36, () => ShowMenuPage(MenuPage.Home));
        }

        void ShowMenuPage(MenuPage page, bool refresh = true)
        {
            CloseDeleteWorldDialog();
            menuPage = page;
            for (int i = 0; i < menuPages.Length; i++) menuPages[i].gameObject.SetActive(i == (int)page);
            menuStatus.alignment = page == MenuPage.Home ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
            menuInteraction.interactable = !settingsPanel.gameObject.activeSelf;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            guardThroughFrame = Time.frameCount + 1;
            if (page == MenuPage.Create) RefreshCreationSummary();
            if (refresh && (page == MenuPage.Home || page == MenuPage.Load)) RequestWorldList();
        }

        void StartNew() => ShowMenuPage(MenuPage.Create);
        void Load() => ShowMenuPage(MenuPage.Load);

        void RefreshCreationSummary()
        {
            if (creationSummary == null) return;
            string name = worldNameField.text.Trim();
            string seedText = seedField.text.Trim();
            string seed = seedText.Length == 0 ? "随机（创建时确定）" : WorldSeed.Resolve(seedText).ToString(CultureInfo.InvariantCulture);
            string preset = worldPreset == WorldPreset.Wilderness ? "荒野" : worldPreset == WorldPreset.Urban ? "城市" : "均衡";
            string density = cityDensity == 0 ? "稀疏" : cityDensity == 2 ? "密集" : "适中";
            SetFlowText(creationSummary, "世界：" + Clean(name.Length == 0 ? "新的世界" : name) +
                "\n\n种子：" + seed + "\n\n地貌：" + preset + "\n城市密度：" + density +
                "\n\n地图：近似无限（16×16格/区块）\n探索：随移动逐块生成\n季节：7天一季\n小地图：探索迷雾", 230);
            WorldPreset[] values = { WorldPreset.Balanced, WorldPreset.Wilderness, WorldPreset.Urban };
            for (int i = 0; i < 3; i++)
            {
                Tint(presetButtons[i], worldPreset == values[i] ? Selected : Raised);
                presetButtons[i].label.color = worldPreset == values[i] ? Accent : Ink;
                Tint(densityButtons[i], cityDensity == i ? Selected : Raised);
                densityButtons[i].label.color = cityDensity == i ? Accent : Ink;
            }
        }

        bool BeginMenuRequest()
        {
            if (bridge.IsRunning || menuRequestPending || Time.unscaledTime < nextMenuRequestAt) return false;
            menuRequestPending = true;
            nextMenuRequestAt = Time.unscaledTime + 1f;
            return true;
        }

        void CreateConfiguredWorld()
        {
            if (!BeginMenuRequest()) return;
            try
            {
                string seedText = seedField.text.Trim();
                var settings = new WorldGenerationSettings
                {
                    worldName = string.IsNullOrWhiteSpace(worldNameField.text) ? "新的世界" : worldNameField.text.Trim(),
                    seedText = seedText,
                    seed = seedText.Length == 0 ? WorldSeed.Random() : WorldSeed.Resolve(seedText),
                    preset = worldPreset,
                    cityDensity = cityDensity,
                    chunked = true
                };
                bridge.PlayerName = ReadName();
                CallBridge(() => bridge.CreateWorld(settings));
            }
            catch (Exception error) { Notify("创建失败：" + error.Message, true); }
            finally { menuRequestPending = false; }
        }

        void RequestWorldList()
        {
            // Listing validates full saves. Refresh on navigation or explicit request, never each HUD tick.
            saveCheckAt = float.PositiveInfinity;
            try
            {
                bridge.RefreshWorldList();
                PaintWorldList();
            }
            catch (Exception error)
            {
                foreach (WorldMenuRow row in worldRows) row.widget.button.transform.parent.gameObject.SetActive(false);
                worldListEmpty.gameObject.SetActive(true);
                SetFlowText(worldListEmpty, "无法读取存档列表，请点击刷新重试。", 80);
                loadSelectedWorld.button.interactable = false;
                deleteSelectedWorld.button.interactable = false;
                Set(menuSave, "存档列表读取失败；可进入读取页重试。");
                Notify("读取列表失败：" + error.Message, true);
            }
        }

        void PaintWorldList()
        {
            List<WorldSaveInfo> worlds = bridge.Worlds;
            int count = 0;
            WorldSaveInfo selected = null;
            if (worlds != null)
            {
                foreach (WorldSaveInfo world in worlds)
                {
                    if (world == null || string.IsNullOrWhiteSpace(world.saveId)) continue;
                    if (count >= worldRows.Count)
                    {
                        var row = new WorldMenuRow();
                        RectTransform holder = FlowRow("World Save Row " + count, worldListContent, 584, 102);
                        row.widget = Button("World Save Select " + count, holder, "", 0, 0, 584, 100, () => SelectWorld(row.saveId));
                        Stretch(row.widget.label.rectTransform, 14, 9, 14, 9);
                        row.widget.label.alignment = TextAlignmentOptions.MidlineLeft;
                        row.widget.label.fontSizeMax = 16;
                        worldRows.Add(row);
                    }
                    WorldMenuRow entry = worldRows[count++];
                    entry.saveId = world.saveId;
                    entry.widget.button.name = "World Save Select " + world.saveId;
                    entry.widget.button.transform.parent.name = "World Save Row " + world.saveId;
                    entry.widget.button.transform.parent.gameObject.SetActive(true);
                    entry.widget.button.gameObject.SetActive(true);
                    Set(entry.widget.label, Clean(string.IsNullOrWhiteSpace(world.name) ? world.saveId : world.name) +
                        "\n种子 " + world.seed.ToString(CultureInfo.InvariantCulture) + "\n修改时间：" + Clean(world.modified));
                    bool active = string.Equals(selectedSaveId, world.saveId, StringComparison.Ordinal);
                    Tint(entry.widget, active ? Selected : Raised);
                    entry.widget.label.color = active ? Accent : Ink;
                    if (active) selected = world;
                }
            }
            for (int i = count; i < worldRows.Count; i++) worldRows[i].widget.button.transform.parent.gameObject.SetActive(false);
            worldListEmpty.gameObject.SetActive(count == 0);
            SetFlowText(worldListEmpty, "暂无可读取的本地存档，请返回创建世界。", 80);
            Set(menuSave, count == 0 ? "暂无本地存档" : "本地世界 " + count + " 个 · 进入读取页选择");
            // Never silently switch the requested save when refresh removes a selection.
            if (selected == null) selectedSaveId = "";
            loadSelectedWorld.button.interactable = selected != null;
            deleteSelectedWorld.button.interactable = selected != null && bridge.CanDeleteWorld;
            SetFlowText(selectedWorldDetails, selected == null ? "选择左侧存档以查看详情。" :
                Clean(selected.name) + "\n\n种子：" + selected.seed.ToString(CultureInfo.InvariantCulture) + "\n修改时间：" + Clean(selected.modified), 100);
            SetFlowText(selectedWorldSummary, selected == null ? "" : Clean(selected.summary), 32);
        }

        void SelectWorld(string saveId)
        {
            selectedSaveId = saveId;
            PaintWorldList();
        }

        void LoadSelectedWorld()
        {
            if (string.IsNullOrEmpty(selectedSaveId) || !BeginMenuRequest()) return;
            try
            {
                string requested = selectedSaveId;
                bridge.RefreshWorldList();
                PaintWorldList();
                if (string.IsNullOrEmpty(selectedSaveId) || requested != selectedSaveId)
                { Notify("所选存档已不存在，请重新选择。", true); return; }
                bridge.PlayerName = ReadName();
                CallBridge(() => bridge.LoadWorld(requested));
            }
            catch (Exception error) { Notify("读取失败：" + error.Message, true); }
            finally { menuRequestPending = false; }
        }

        void RefreshMenu()
        {
            if (Time.unscaledTime >= saveCheckAt && (menuPage == MenuPage.Home || menuPage == MenuPage.Load)) RequestWorldList();
            string status = CurrentLocalFeedback() ? Clean(localFeedback) : Clean(bridge.Status);
            Color color = CurrentLocalFeedback() ? localFeedbackColor : StatusColor(bridge.Status);
            Set(menuStatus, status);
            menuStatus.color = color;
            Set(lanStatus, status);
            lanStatus.color = color;
            Set(menuAddress, "本机 IPv4\n" + Clean(bridge.HostIP) + "\n\n同一局域网 · UDP 7777\n加入时填写房主的 IPv4 地址。");
            createWorldButton.button.interactable = !menuRequestPending && Time.unscaledTime >= nextMenuRequestAt;
        }

        void MakeSettings(RectTransform root)
        {
            settingsPanel = Backdrop("Settings Panel", root, new Color32(4, 12, 8, 230));
            RectTransform card = Box("Settings Card", settingsPanel, 0, 0, 720, 540, Panel, true);
            Center(card, 720, 540);
            PanelRule(card, 720, 68);
            Label("Settings Heading", card, "设置", 28, 16, 500, 38, 28, Ink, FontStyles.Bold);
            settingsCloseButton = Button("Close Settings", card, "返回", 574, 20, 118, 34, CloseSettings).button;
            Label("Master Volume Label", card, "主音量", 28, 96, 450, 30, 19, Ink);
            volumeValue = Label("Master Volume Value", card, "", 580, 96, 112, 30, 18, Accent);
            volumeSlider = PreferenceSlider("Master Volume", card, 28, 140, 664, 0f, 1f,
                Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, .7f)), ApplyVolume);
            Label("Effects Volume Label", card, "音效音量", 28, 202, 450, 30, 19, Ink);
            effectsValue = Label("Effects Volume Value", card, "", 580, 202, 112, 30, 18, Accent);
            effectsSlider = PreferenceSlider("Effects Volume", card, 28, 246, 664, 0f, 1f,
                Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsPreference, .8f)), ApplyEffectsVolume);
            Label("UI Scale Label", card, "界面比例", 28, 308, 450, 30, 19, Ink);
            scaleValue = Label("UI Scale Value", card, "", 580, 308, 112, 30, 18, Accent);
            scaleSlider = PreferenceSlider("UI Scale", card, 28, 352, 664, .8f, 1f,
                Mathf.Clamp(PlayerPrefs.GetFloat(ScalePreference, 1f), .8f, 1f), ApplyUiScale);
            Label("UI Scale Hint", card, "80–100% · 保留 1280 × 720 的完整布局区域", 28, 404, 664, 30, 15, Muted);
            Button("Reset UI Settings", card, "恢复默认设置", 28, 466, 220, 44, () =>
            { volumeSlider.value = .7f; effectsSlider.value = .8f; scaleSlider.value = 1f; });
            Label("Settings Persistence", card, "设置自动保存在本机", 356, 470, 336, 32, 15, Muted);
            ApplyVolume(volumeSlider.value);
            ApplyEffectsVolume(effectsSlider.value);
            ApplyUiScale(scaleSlider.value);
            settingsPanel.gameObject.SetActive(false);
        }

        UnityEngine.UI.Slider PreferenceSlider(string name, Transform parent, float x, float top, float width,
            float minimum, float maximum, float value, UnityEngine.Events.UnityAction<float> changed)
        {
            RectTransform root = Box(name, parent, x, top, width, 42, Recessed, true);
            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            RectTransform track = Box("Track", root, 12, 18, width - 24, 6, Edge);
            RectTransform fill = Box("Fill", track, 0, 0, width - 24, 6, Accent);
            Stretch(fill);
            RectTransform handleArea = NewRect("Handle Area", root);
            Stretch(handleArea, 12, 0, 12, 0);
            RectTransform handle = Box("Handle", handleArea, 0, 8, 18, 26, Ink, true);
            handle.anchorMin = handle.anchorMax = new Vector2(0, .5f);
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.value = value;
            slider.onValueChanged.AddListener(changed);
            slider.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            return slider;
        }

        void ApplyVolume(float value)
        {
            PlayerPrefs.SetFloat(VolumePreference, value);
            Set(volumeValue, Mathf.RoundToInt(value * 100) + "%");
        }

        void ApplyEffectsVolume(float value)
        {
            PlayerPrefs.SetFloat(EffectsPreference, value);
            Set(effectsValue, Mathf.RoundToInt(value * 100) + "%");
        }

        void ApplyUiScale(float value)
        {
            // Expand plus a reference area >=1280x720 keeps every fixed card inside the canvas.
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution = new Vector2(1280, 720) / value;
            PlayerPrefs.SetFloat(ScalePreference, value);
            Set(scaleValue, Mathf.RoundToInt(value * 100) + "%");
        }

        void OpenSettings()
        {
            if (bridge.IsRunning) SetPanel(settingsPanel, true);
            else if (!settingsPanel.gameObject.activeSelf)
            {
                settingsPanel.gameObject.SetActive(true);
                menuInteraction.interactable = false;
                if (eventSystem != null) eventSystem.SetSelectedGameObject(settingsCloseButton.gameObject);
                DemoAudio.UiOpen();
            }
            settingsPanel.SetAsLastSibling();
            guardThroughFrame = Time.frameCount + 1;
        }

        void CloseSettings()
        {
            PlayerPrefs.Save();
            bool wasOpen = settingsPanel.gameObject.activeSelf;
            settingsPanel.gameObject.SetActive(false);
            menuInteraction.interactable = true;
            if (wasOpen) DemoAudio.UiClose();
            if (bridge.IsRunning) SetPanel(session, true);
            else if (eventSystem != null) eventSystem.SetSelectedGameObject(titleSettingsButton.gameObject);
        }

        void PanelRule(RectTransform parent, float width, float top)
        {
            Box("Panel Header Rule", parent, 0, top, width, 1, Edge);
            Box("Panel Header Accent", parent, 0, 0, 4, top, Accent);
        }

        void MakeHudTools()
        {
            RectTransform dock = NewRect("HUD Shortcuts", game);
            dock.anchorMin = dock.anchorMax = dock.pivot = new Vector2(1, 0);
            dock.anchoredPosition = new Vector2(-18, 18);
            dock.sizeDelta = new Vector2(220, 40);
            toolsToggle = Button("HUD Tools Toggle", dock, "工具 / 菜单", 0, 0, 220, 40, () =>
            {
                bool open = !toolsTray.gameObject.activeSelf;
                toolsTray.gameObject.SetActive(open);
                Set(toolsToggle.label, open ? "收起工具" : "工具 / 菜单");
            });
            toolsTray = Box("HUD Tools Tray", dock, 0, -278, 220, 268, Panel, true);
            Button("Technology Button", toolsTray, "科技 / 建造 T", 8, 8, 204, 36, () => SetPanel(technology, true));
            Button("Open Inventory", toolsTray, "背包 B", 8, 50, 204, 36, () => SetPanel(inventory, true));
            growthButton = Button("Open Growth", toolsTray, "成长 Tab", 8, 92, 204, 36, () => SetGrowth(true));
            inspectToggle = Button("Inspect Toggle", toolsTray, "设备检查：关", 8, 134, 204, 36, () =>
            {
                inspectorEnabled = !inspectorEnabled;
                Set(inspectToggle.label, inspectorEnabled ? "设备检查：开" : "设备检查：关");
                if (!inspectorEnabled) HideInspector();
                toolsTray.gameObject.SetActive(false);
                Set(toolsToggle.label, "工具 / 菜单");
            });
            Button("Open Help", toolsTray, "帮助 H", 8, 176, 98, 36, () => SetPanel(help, true));
            Button("Open Session", toolsTray, "菜单 Esc", 114, 176, 98, 36, () => SetPanel(session, true));
            Label("HUD Tools Hint", toolsTray, "检查开启后，指向设备查看详情", 8, 222, 204, 32, 12, Muted);
            toolsTray.gameObject.SetActive(false);
        }
    }
}
