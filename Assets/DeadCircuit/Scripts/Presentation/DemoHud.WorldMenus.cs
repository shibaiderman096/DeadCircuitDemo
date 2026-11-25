using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    // Menu presentation and preferences. Creation/loading are delegated to GameBridge.
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
        const float WorldPreviewDebounce = .25f;
        static readonly WorldPreset[] CreationPresets = { WorldPreset.Balanced, WorldPreset.Wilderness, WorldPreset.Urban };
        readonly List<WorldMenuRow> worldRows = new List<WorldMenuRow>();
        readonly UiButton[] presetButtons = new UiButton[3];
        readonly UiButton[] densityButtons = new UiButton[3];
        readonly RectTransform[] menuPages = new RectTransform[4];
        RectTransform menuFrame, worldListContent, settingsPanel, settingsCard, toolsTray, buildTray;
        RectTransform deleteWorldDialog;
        CanvasGroup menuInteraction;
        UnityEngine.UI.ScrollRect worldListScroll;
        UnityEngine.UI.Slider volumeSlider, effectsSlider, scaleSlider;
        TMP_InputField worldNameField;
        TextMeshProUGUI worldPreviewStatus, worldListEmpty, selectedWorldDetails, selectedWorldSummary;
        WorldTerrainPreviewImage worldPreview;
        TextMeshProUGUI volumeValue, effectsValue, scaleValue, lanStatus;
        UiButton createWorldButton, loadSelectedWorld, inspectToggle, toolsToggle, buildToggle;
        UiButton settingsAppearanceButton;
        TextMeshProUGUI deleteWorldDetails;
        UiButton deleteSelectedWorld, cancelDeleteWorld;
        MenuPage menuPage, createReturnPage = MenuPage.Home;
        WorldPreset worldPreset = WorldPreset.Balanced;
        int cityDensity = 1;
        int? emptyWorldSeed;
        string selectedSaveId = "";
        string pendingDeleteSaveId;
        bool buildTrayOpen, inspectorEnabled, menuRequestPending;
        float nextMenuRequestAt;
        float worldPreviewAt = float.PositiveInfinity;

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
            MenuHeading(page, "Create World Heading", "新建世界", MenuPage.Create);
            Label("World Name Label", page, "世界名称", 28, 88, 620, 24, 16, Muted);
            worldNameField = Input("World Name", page, "新的世界", "为这个世界命名", 28, 118, 620, 44, 40);
            Label("Seed Label", page, "种子 · 留空随机，也可输入数字或文字", 28, 178, 620, 24, 16, Muted);
            seedField = Input("World Seed", page, "", "输入数字或文字，留空随机", 28, 208, 490, 44, 128);
            seedField.characterValidation = TMP_InputField.CharacterValidation.None;
            UiButton randomSeed = Button("Random World Seed", page, "随机", 532, 208, 116, 44, RandomizeCreationSeed);
            SetButtonArt(randomSeed, "ui_random", 24);
            Label("World Preset Label", page, "地貌预设", 28, 272, 620, 24, 16, Muted);
            string[] presets = { "均衡", "荒野", "城市" };
            for (int i = 0; i < presets.Length; i++)
            {
                int index = i;
                presetButtons[i] = Button("World Preset " + CreationPresets[i], page, presets[i], 28 + i * 212, 306, 196, 46,
                    () => { worldPreset = CreationPresets[index]; RefreshCreationOptions(); });
            }
            Label("City Density Label", page, "城市密度", 28, 374, 620, 24, 16, Muted);
            string[] densities = { "稀疏", "适中", "密集" };
            for (int i = 0; i < densities.Length; i++)
            {
                int index = i;
                densityButtons[i] = Button("City Density " + i, page, densities[i], 28 + i * 212, 408, 196, 44,
                    () => { cityDensity = index; RefreshCreationOptions(); });
            }
            MakeWorldTerrainPreview(page);
            createWorldButton = Button("Create World", page, "创建世界", 680, 480, 412, 48, CreateConfiguredWorld, true);
            SetButtonArt(createWorldButton, "ui_new_world", 28);
            Label("Create World Notice", page, "创建后由你担任房主；已有进度请从「读取世界」继续。", 28, 480, 620, 48, 15, Muted);
            seedField.onValueChanged.AddListener(_ =>
            {
                worldPreviewAt = Time.unscaledTime + WorldPreviewDebounce;
                Set(worldPreviewStatus, "正在更新…");
            });
            RefreshCreationOptions();
        }

        void MakeWorldTerrainPreview(RectTransform page)
        {
            RectTransform card = Box("World Terrain Preview Card", page, 680, 92, 412, 360, Recessed);
            Label("World Terrain Preview Heading", card, "地貌预览", 18, 7, 142, 34, 22, Accent);
            worldPreviewStatus = Label("World Terrain Preview Status", card, "", 164, 9, 230, 28, 15, Muted);
            worldPreviewStatus.alignment = TextAlignmentOptions.MidlineRight;
            RectTransform frame = Box("Terrain Map Edge", card, 28, 39, 356, 268, Edge);
            RectTransform map = NewRect("Terrain Map", frame);
            Stretch(map, 2, 2, 2, 2);
            worldPreview = map.gameObject.AddComponent<WorldTerrainPreviewImage>();
            worldPreview.raycastTarget = false;

            var biomes = WorldGenerationCatalog.Biomes;
            for (int i = 0; i <= biomes.Count; i++)
            {
                bool water = i == biomes.Count;
                string name = water ? "水域" : biomes[i].name;
                int x = 18 + (i % 6) * 63;
                int top = 312 + (i / 6) * 21;
                Color32 color = water ? WorldTerrainPreviewImage.WaterColor : WorldTerrainPreviewImage.BiomeColor(biomes[i].kind);
                Box("Terrain Legend Swatch " + name, card, x, top + 4, 11, 11, color);
                Label("Terrain Legend " + name, card, name, x + 15, top, 48, 19, 14, Ink);
            }
        }

        void MakeLoadPage(RectTransform page)
        {
            MenuHeading(page, "Load World Heading", "单人游戏", MenuPage.Load);
            Label("World List Heading", page, "本地存档", 28, 82, 460, 32, 20, Ink, FontStyles.Bold);
            UiButton refreshWorlds = Button("Refresh Worlds", page, "刷新", 486, 82, 156, 34, RequestWorldList);
            SetButtonArt(refreshWorlds, "ui_refresh", 22);
            worldListContent = ScrollContent("World List Scroll View", page, 28, 128, 614, 310, out worldListScroll);
            worldListEmpty = FlowLabel("World List Empty", worldListContent, "暂无本地存档。点击「新建世界」创建。", 580, 80, 17, Muted);
            RectTransform details = Box("Selected World Card", page, 668, 82, 424, 352, Recessed);
            Label("Selected World Heading", details, "存档详情", 20, 16, 384, 32, 22, Accent);
            RectTransform content = ScrollContent("Selected World Scroll View", details, 14, 64, 396, 274, out _);
            selectedWorldDetails = FlowLabel("Selected World Details", content, "选择左侧存档，查看世界详情。", 368, 100, 17, Ink);
            selectedWorldSummary = FlowLabel("Selected World Summary", content, "", 368, 80, 15, Muted);
            loadSelectedWorld = Button("Load Selected World", page, "读取世界", 668, 454, 258, 48, LoadSelectedWorld, true);
            SetButtonArt(loadSelectedWorld, "ui_load_world", 28);
            deleteSelectedWorld = Button("Delete Selected World", page, "删除存档", 938, 454, 154, 48, OpenDeleteWorldDialog);
            deleteSelectedWorld.label.color = Danger;
            deleteSelectedWorld.button.interactable = false;
            UiButton newWorld = Button("New Game", page, "新建世界", 28, 454, 614, 48, StartNew);
            SetButtonArt(newWorld, "ui_new_world", 28);
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
            Label("Delete World Warning", card, "已保存的世界进度将永久删除，无法恢复。", 28, 178, 504, 28, 16, Muted);
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
            MenuHeading(page, "LAN Heading", "局域网联机", MenuPage.Lan);
            Label("Name Label", page, "角色名", 28, 92, 596, 26, 16, Muted);
            nameField = Input("Player Name", page, bridge.PlayerName, "幸存者", 28, 126, 596, 46, 12);
            Label("IP Label", page, "房主 IPv4 地址 · UDP 7777", 28, 194, 596, 26, 16, Muted);
            ipField = Input("Host IPv4", page, "", "例如 192.168.1.8", 28, 228, 596, 46, 15);
            UiButton join = Button("Join Game", page, "加入世界", 28, 304, 286, 48, Join, true);
            SetButtonArt(join, "ui_join", 28);
            UiButton cancel = Button("Cancel Connection", page, "取消连接", 330, 304, 294, 48, () => CallBridge(bridge.Leave));
            SetButtonArt(cancel, "ui_cancel", 28);
            UiButton host = Button("Start Host", page, "新建世界", 28, 388, 596, 48, StartNew);
            SetButtonArt(host, "ui_network", 28);
            Label("Host Existing World Hint", page, "由你担任房主：新建世界，或返回开始菜单读取已有世界。", 28, 456, 596, 40, 15, Muted);
            RectTransform details = Box("LAN Connection Card", page, 660, 92, 432, 420, Recessed);
            Label("LAN Status Heading", details, "连接状态", 20, 18, 392, 32, 22, Accent);
            lanStatus = Label("LAN Connection Status", details, "", 20, 76, 392, 126, 17, Ink);
            lanStatus.alignment = TextAlignmentOptions.TopLeft;
            menuAddress = Label("LAN Address", details, "", 20, 230, 392, 162, 16, Muted);
            menuAddress.alignment = TextAlignmentOptions.TopLeft;
        }

        void MenuHeading(RectTransform page, string name, string title, MenuPage currentPage)
        {
            Label(name, page, title, 28, 14, 824, 38, 27, Ink, FontStyles.Bold);
            UiButton back = Button(page.name + " Back", page, "返回", 944, 16, 148, 36,
                () => ShowMenuPage(currentPage == MenuPage.Create ? createReturnPage : MenuPage.Home));
            SetButtonArt(back, "ui_back", 22);
        }

        void ShowMenuPage(MenuPage page, bool refresh = true)
        {
            CloseDeleteWorldDialog();
            menuPage = page;
            for (int i = 0; i < menuPages.Length; i++) menuPages[i].gameObject.SetActive(i == (int)page);
            menuStatus.alignment = TextAlignmentOptions.TopLeft;
            menuStatus.gameObject.SetActive(page != MenuPage.Home);
            menuInteraction.interactable = !settingsPanel.gameObject.activeSelf;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            guardThroughFrame = Time.frameCount + 1;
            if (page == MenuPage.Create) RefreshWorldTerrainPreview(seedField.text.Trim());
            else worldPreviewAt = float.PositiveInfinity;
            if (refresh && page == MenuPage.Load) RequestWorldList();
        }

        void StartNew()
        {
            createReturnPage = menuPage;
            ShowMenuPage(MenuPage.Create);
        }
        void Load() => ShowMenuPage(MenuPage.Load);

        void RefreshCreationOptions()
        {
            for (int i = 0; i < 3; i++)
            {
                Tint(presetButtons[i], worldPreset == CreationPresets[i] ? Selected : Raised);
                presetButtons[i].label.color = worldPreset == CreationPresets[i] ? Accent : Ink;
                Tint(densityButtons[i], cityDensity == i ? Selected : Raised);
                densityButtons[i].label.color = cityDensity == i ? Accent : Ink;
            }
        }

        int ResolveCreationSeed(string seedText)
        {
            if (seedText.Length != 0) return WorldSeed.Resolve(seedText);
            if (!emptyWorldSeed.HasValue) emptyWorldSeed = WorldSeed.Random();
            return emptyWorldSeed.Value;
        }

        int RefreshWorldTerrainPreview(string seedText)
        {
            int seed = ResolveCreationSeed(seedText);
            worldPreview.Draw(seed);
            worldPreviewAt = float.PositiveInfinity;
            Set(worldPreviewStatus, (seedText.Length == 0 ? "随机种子 " : "种子 ") + seed.ToString(CultureInfo.InvariantCulture));
            return seed;
        }

        void RandomizeCreationSeed()
        {
            int previous = ResolveCreationSeed(seedField.text.Trim());
            int seed;
            do { seed = WorldSeed.Random(); } while (seed == previous);
            emptyWorldSeed = seed;
            string seedText = seed.ToString(CultureInfo.InvariantCulture);
            seedField.SetTextWithoutNotify(seedText);
            RefreshWorldTerrainPreview(seedText);
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
                // Finish a pending preview with the same input snapshot sent to the generator.
                int seed = RefreshWorldTerrainPreview(seedText);
                var settings = new WorldGenerationSettings
                {
                    worldName = string.IsNullOrWhiteSpace(worldNameField.text) ? "新的世界" : worldNameField.text.Trim(),
                    seedText = seedText,
                    seed = seed,
                    preset = worldPreset,
                    cityDensity = cityDensity,
                    chunked = true
                };
                bridge.PlayerName = ReadName();
                CallBridge(() => bridge.CreateWorld(settings));
            }
            catch (Exception error) { Notify("创建世界失败：" + error.Message, true); }
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
                Notify("读取存档列表失败：" + error.Message, true);
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
            SetFlowText(worldListEmpty, "暂无可读取的本地存档。点击「新建世界」创建。", 80);
            // Never silently switch the requested save when refresh removes a selection.
            if (selected == null) selectedSaveId = "";
            loadSelectedWorld.button.interactable = selected != null;
            deleteSelectedWorld.button.interactable = selected != null && bridge.CanDeleteWorld;
            SetFlowText(selectedWorldDetails, selected == null ? "选择左侧存档，查看世界详情。" :
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
            catch (Exception error) { Notify("读取世界失败：" + error.Message, true); }
            finally { menuRequestPending = false; }
        }

        void RefreshMenu()
        {
            if (Time.unscaledTime >= saveCheckAt && menuPage == MenuPage.Load) RequestWorldList();
            if (menuPage == MenuPage.Create && Time.unscaledTime >= worldPreviewAt)
                RefreshWorldTerrainPreview(seedField.text.Trim());
            string status = CurrentLocalFeedback() ? Clean(localFeedback) : Clean(bridge.Status);
            Color color = CurrentLocalFeedback() ? localFeedbackColor : StatusColor(bridge.Status);
            Set(menuStatus, status);
            menuStatus.color = color;
            Set(lanStatus, status);
            lanStatus.color = color;
            Set(menuAddress, "本机 IPv4 地址\n" + Clean(bridge.HostIP) + "\n\n请连接同一局域网 · UDP 7777\n加入时填写房主的 IPv4 地址。");
            createWorldButton.button.interactable = !menuRequestPending && Time.unscaledTime >= nextMenuRequestAt;
        }

        void MakeSettings(RectTransform root)
        {
            settingsPanel = Backdrop("Settings Panel", root, new Color32(4, 12, 8, 230));
            RectTransform card = Box("Settings Card", settingsPanel, 0, 0, 1000, 660, Panel, true);
            settingsCard = card;
            Center(card, 1000, 660);
            PanelRule(card, 1000, 68);
            Label("Settings Heading", card, "游戏设置", 28, 16, 720, 38, 28, Ink, FontStyles.Bold);
            UiButton closeSettings = Button("Close Settings", card, "返回", 858, 20, 114, 34, CloseSettings);
            SetButtonArt(closeSettings, "ui_back", 20);
            settingsCloseButton = closeSettings.button;
            BuildSettingsPages(card);
            settingsAppearanceButton = Button("Open Appearance", settingsPages[0], "角色外观", 8, 432, 220, 44, OpenAppearance);
            SetButtonArt(settingsAppearanceButton, "ui_equip", 24);
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
            if (appearancePanel != null) appearancePanel.gameObject.SetActive(false);
            if (settingsCard != null) settingsCard.gameObject.SetActive(true);
            SetSettingsPage(0);
            if (settingsAppearanceButton != null) settingsAppearanceButton.button.gameObject.SetActive(!bridge.IsRunning);
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
            CancelBindingCapture();
            RevertDisplayPreview();
            PlayerPrefs.Save();
            if (appearancePanel != null) appearancePanel.gameObject.SetActive(false);
            if (settingsCard != null) settingsCard.gameObject.SetActive(true);
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
            toolsToggle = Button("HUD Tools Toggle", dock, "菜单", 0, 0, 220, 40, () =>
            {
                bool open = !toolsTray.gameObject.activeSelf;
                toolsTray.gameObject.SetActive(open);
                Set(toolsToggle.label, open ? "收起" : "菜单");
            });
            SetButtonArt(toolsToggle, "ui_settings", 26);
            toolsTray = Box("HUD Tools Tray", dock, 0, -278, 220, 268, Panel, true);
            UiButton technologyShortcut = Button("Technology Button", toolsTray, "科技 T", 8, 8, 204, 36, () => SetPanel(technology, true));
            SetButtonArt(technologyShortcut, "ui_technology", 24);
            UiButton inventoryShortcut = Button("Open Inventory", toolsTray, "背包 B", 8, 50, 204, 36, () => SetPanel(inventory, true));
            SetButtonArt(inventoryShortcut, "ui_inventory", 24);
            growthButton = Button("Open Growth", toolsTray, "成长 Tab", 8, 92, 204, 36, () => SetGrowth(true));
            SetButtonArt(growthButton, "ui_growth", 24);
            inspectToggle = Button("Inspect Toggle", toolsTray, "检查：关", 8, 134, 204, 36, () =>
            {
                inspectorEnabled = !inspectorEnabled;
                Set(inspectToggle.label, inspectorEnabled ? "检查：开" : "检查：关");
                if (!inspectorEnabled) HideInspector();
                toolsTray.gameObject.SetActive(false);
                Set(toolsToggle.label, "工具");
            });
            SetButtonArt(inspectToggle, "ui_inspect", 24);
            UiButton helpShortcut = Button("Open Help", toolsTray, "帮助 H", 8, 176, 98, 36, () => SetPanel(help, true));
            SetButtonArt(helpShortcut, "ui_help", 20);
            UiButton sessionShortcut = Button("Open Session", toolsTray, "菜单 Esc", 114, 176, 98, 36, () => SetPanel(session, true));
            SetButtonArt(sessionShortcut, "ui_session", 20);
            Label("HUD Tools Hint", toolsTray, "开启设备详情后，指向设备查看", 8, 222, 204, 32, 12, Muted);
            toolsTray.gameObject.SetActive(false);
        }
    }

    sealed class WorldTerrainPreviewImage : UnityEngine.UI.RawImage
    {
        const int MapWidth = 128, MapHeight = 96, SampleStep = 9;
        internal static readonly Color32 WaterColor = new Color32(45, 100, 124, 255);
        static readonly Color32[] BiomeColors =
        {
            new Color32(147, 162, 93, 255),  // Meadow
            new Color32(91, 132, 76, 255),   // Deciduous forest
            new Color32(51, 97, 74, 255),    // Coniferous forest
            new Color32(194, 158, 101, 255), // Desert
            new Color32(89, 134, 126, 255),  // Wetland
            new Color32(151, 164, 167, 255), // Tundra
            new Color32(91, 99, 60, 255),    // Swamp
            new Color32(216, 226, 229, 255), // Snow mountain
            new Color32(187, 119, 135, 255), // Sakura grove
            new Color32(113, 166, 145, 255)  // River valley
        };

        Color32[] pixels;
        Texture2D previewTexture;
        int renderedSeed;

        internal static Color32 BiomeColor(BiomeKind biome) => BiomeColors[(int)biome];

        internal void Draw(int seed)
        {
            if (previewTexture != null && renderedSeed == seed) return;
            if (pixels == null) pixels = new Color32[MapWidth * MapHeight];
            for (int py = 0; py < MapHeight; py++)
            {
                int y = (py - MapHeight / 2) * SampleStep;
                for (int px = 0; px < MapWidth; px++)
                {
                    int x = (px - MapWidth / 2) * SampleStep;
                    ChunkTerrain.SampleContinuous(seed, x, y, out char ground, out char biome);
                    pixels[px + py * MapWidth] = ground == '2' ? WaterColor : BiomeColor((BiomeKind)(biome - '0'));
                }
            }

            if (previewTexture == null)
            {
                previewTexture = new Texture2D(MapWidth, MapHeight, TextureFormat.RGBA32, false)
                {
                    name = "World Terrain Preview",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture = previewTexture;
            }
            previewTexture.SetPixels32(pixels);
            previewTexture.Apply(false, false);
            renderedSeed = seed;
        }

        protected override void OnDestroy()
        {
            if (previewTexture != null) Destroy(previewTexture);
            base.OnDestroy();
        }
    }
}
