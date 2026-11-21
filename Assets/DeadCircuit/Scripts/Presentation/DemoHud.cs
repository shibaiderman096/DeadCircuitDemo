using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DeadCircuit
{
    /// <summary>Presentation only. World mutations go through GameBridge.Send.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed partial class DemoHud : MonoBehaviour
    {
        static readonly Color Ink = new Color32(241, 235, 220, 255);
        static readonly Color Muted = new Color32(171, 188, 177, 255);
        static readonly Color Panel = new Color32(19, 36, 29, 253);
        static readonly Color HudSurface = new Color32(16, 31, 26, 235);
        static readonly Color Raised = new Color32(36, 58, 47, 255);
        static readonly Color Accent = new Color32(130, 219, 215, 255);
        static readonly Color Selected = new Color32(31, 77, 72, 255);
        static readonly Color Edge = new Color32(62, 87, 73, 255);
        static readonly Color Recessed = new Color32(11, 24, 19, 255);
        static readonly Color Amber = new Color32(242, 187, 98, 255);
        static readonly Color Danger = new Color32(244, 128, 113, 255);
        static readonly string[] Directions = { "东 →", "北 ↑", "西 ←", "南 ↓" };
        static readonly string[] Nodes = { "树木", "铁矿脉", "废料堆", "补给箱", "障碍墙", "铜矿脉", "锡矿脉", "铀矿脉", "石料堆", "黏土矿床", "煤矿露头", "石灰石矿床", "耐火黏土矿床", "油源", "石英矿床" };
        static readonly string[] AttributeHelp =
        {
            "强化近战与采集能力", "每点生命上限 +15", "每点体力上限 +8、移速 +0.09",
            "强化远程作战能力", "每点经验收益 +5%、修理量 +2"
        };
        static readonly string[] EraDescriptions =
        {
            "采集木材、石材与黏土，研究工具、烧炭和制陶。完成冶金与建造任务后，即可研究水力。",
            "在岸边建造水车，用传动轴连接机械设备。加工矿石、青铜与机械零件，为蒸汽工业准备材料。",
            "将燃烧室、锅炉、取水泵与烟囱相邻建造，用蒸汽管为设备供能。蒸汽引擎可将蒸汽转为机械动力。",
            "低压电气化：建立低压发电与输电网络，制造电路，让采矿、冶炼与装配自动运行。",
            "勘探油藏，搭建采油、炼油与化工产线。生产柴油、聚合物与润滑油，用变压器向低压电网供电。",
            "提纯硅材，制造电子器件、控制模块与耐热合金。建立高压电网，逐级降压为其他产线供电。",
            "加工铀矿，制造核燃料。启用反应堆控制台，接通冷却循环和核废料专用仓，留意温度与停堆状态。"
        };

        // One dynamic asset for every label, also available to the world view.
        static TMP_FontAsset defaultFont;
        public static TMP_FontAsset DefaultFont
        {
            get
            {
                if (defaultFont != null) return defaultFont;
                defaultFont = Resources.Load<TMP_FontAsset>("Fonts/DemoChinese SDF");
                if (defaultFont != null) return defaultFont;
                Font source = Resources.Load<Font>("Fonts/DemoChinese");
                if (source == null)
                {
                    defaultFont = TMP_Settings.defaultFontAsset;
                    if (defaultFont != null) return defaultFont;
                    source = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    Debug.LogWarning("废土回路：缺少 Fonts/DemoChinese 中文字体，暂用后备字体。");
                }
                defaultFont = TMP_FontAsset.CreateFontAsset(source);
                if (defaultFont != null)
                {
                    defaultFont.name = "DeadCircuit / Default Font (Runtime)";
                    defaultFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    defaultFont.isMultiAtlasTexturesEnabled = true;
                    defaultFont.hideFlags = HideFlags.DontSave;
                }
                return defaultFont;
            }
        }

        sealed class UiButton
        {
            public UnityEngine.UI.Button button;
            public UnityEngine.UI.Image image;
            public UnityEngine.UI.Image icon;
            public TextMeshProUGUI label;
        }

        sealed class RecipeRow
        {
            public RecipeDefinition recipe;
            public UiButton craft;
            public TextMeshProUGUI details;
        }

        GameBridge bridge;
        Canvas canvas;
        UnityEngine.UI.GraphicRaycaster raycaster;
        EventSystem eventSystem;
        GameObject ownedEventSystem;
        PointerEventData pointer;
        readonly List<RaycastResult> hits = new List<RaycastResult>(16);
        readonly StringBuilder text = new StringBuilder(256);
        RectTransform menu, game, inventory, growth, session, help, technology, death, machinePanel, buildDetails;
        TMP_InputField nameField, seedField, ipField;
        TextMeshProUGUI menuStatus, menuAddress, menuSave, playerTitle, clockText, waveText;
        TextMeshProUGUI networkText, feedback, gameStatus, objectives, buildTitle, buildCost, buildHelp;
        TextMeshProUGUI machineTitle, machineInfo, machineContents, machineHint;
        TextMeshProUGUI growthSummary, attributeHeading, skillHeading, growthNotice, deathInfo;
        TextMeshProUGUI inventoryNotice, sessionNotice, helpNotice, helpProgress;
        TextMeshProUGUI eraText, technologyTitle, technologyDescription, researchMaterials, researchRequirement, technologyNotice, workbenchHint;
        TextMeshProUGUI machineRecipe, machineRecipeFlow, machineSafety;
        readonly TextMeshProUGUI[] vitalTexts = new TextMeshProUGUI[3];
        readonly RectTransform[] vitalFills = new RectTransform[3];
        readonly UiButton[] slots = new UiButton[10];
        readonly UiButton[] attributeButtons = new UiButton[5];
        readonly TextMeshProUGUI[] attributeValues = new TextMeshProUGUI[5];
        readonly UiButton[,] skillButtons = new UiButton[3, 3];
        readonly int[][] costs = new int[DemoCatalog.BuildNames.Length][];
        readonly UiButton[] eraButtons = new UiButton[7];
        readonly UiButton[] technologyBuildButtons = new UiButton[DemoCatalog.BuildNames.Length];
        readonly RectTransform[] technologyBuildRows = new RectTransform[DemoCatalog.BuildNames.Length];
        readonly TextMeshProUGUI[] technologyBuildCosts = new TextMeshProUGUI[DemoCatalog.BuildNames.Length];
        readonly List<RecipeRow> manualRecipes = new List<RecipeRow>();
        UnityEngine.UI.ScrollRect technologyScroll, machineScroll;
        UiButton researchButton, recipeButton;
        int selectedEra = 1, observedEra = -1;
        UiButton loadButton, saveButton, growthButton, respawnButton;
        UiButton eatButton, interactButton, repairButton, dismantleButton;
        float refreshAt, saveCheckAt, feedbackUntil, statusUntil, playerFeedbackUntil, worldFeedbackUntil;
        float lastPlayerNoticeExpiry = -1f, lastWorldNoticeExpiry = -1f;
        string localFeedback = "";
        string lastBridgeStatus = "", lastPlayerNotice = "", lastWorldNotice = "";
        Color localFeedbackColor;
        bool initialized, wasRunning, localFeedbackError;
        int guardThroughFrame = -1, inspectedX = -1, inspectedY = -1, inspectedId = -1;

        /// <summary>Text entry, modals, or actual interactive UI hits consume world input.</summary>
        public bool CapturesInput
        {
            get
            {
                if (!initialized || !isActiveAndEnabled || canvas == null || !canvas.enabled) return false;
                if (bridge == null || !bridge.IsRunning || Time.frameCount <= guardThroughFrame) return true;
                if (bridge.LocalPlayer == null || bridge.LocalPlayer.hp <= 0f || AnyPanelOpen()) return true;
                return TextInputFocused() || PointerHitsUi();
            }
        }

        public void Init(GameBridge owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (initialized) { bridge = owner; return; }
            bridge = owner;
            for (int i = 0; i < costs.Length; i++) costs[i] = DemoCatalog.BuildCost((BuildKind)i);
            EnsureEventSystem();
            RectTransform root = NewRect("DeadCircuit / Runtime UI", transform);
            canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            // Preserve at least the 1280x720 layout area on both taller and wider screens.
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            raycaster = root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            MakeMenu(root);
            MakeGame(root);
            MakeInventory(root);
            MakeAppearance(root);
            MakeTechnology(root);
            MakeGrowth(root);
            MakeSession(root);
            MakeHelp(root);
            MakeDeath(root);
            CreateAdditionalPanels(root);
            game.gameObject.SetActive(false);
            ClosePanels();
            buildDetails.gameObject.SetActive(false);
            machinePanel.gameObject.SetActive(false);
            death.gameObject.SetActive(false);
            ApplyIndustrialUiTheme(root);
            initialized = true;
            RefreshAll();
        }

        void EnsureEventSystem()
        {
            eventSystem = EventSystem.current;
            if (eventSystem == null) eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                ownedEventSystem = new GameObject("DeadCircuit / EventSystem", typeof(EventSystem));
                ownedEventSystem.transform.SetParent(transform, false);
                eventSystem = ownedEventSystem.GetComponent<EventSystem>();
            }
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
            foreach (BaseInputModule other in eventSystem.GetComponents<BaseInputModule>())
                if (other != module) other.enabled = false;
            module.enabled = true;
            module.deselectOnBackgroundClick = true;
            pointer = new PointerEventData(eventSystem);
        }

        void Update()
        {
            if (!initialized || bridge == null) return;
            bool running = bridge.IsRunning;
            PlayerState player = bridge.LocalPlayer;
            var keyboard = Keyboard.current;
            if (!running && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (deleteWorldDialog.gameObject.activeSelf) CloseDeleteWorldDialog();
                else if (settingsPanel.gameObject.activeSelf) CloseSettings();
                else if (appearancePanel != null && appearancePanel.gameObject.activeSelf) CloseAppearance();
                else ShowMenuPage(MenuPage.Home);
            }
            if (running && player != null && player.hp > 0f && keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (AnyPanelOpen()) SetPanel(null, false);
                    else if (bridge.BuildSelection.HasValue) CancelBuild();
                    else SetPanel(session, true);
                }
                else if (keyboard.bKey.wasPressedThisFrame && inventory.gameObject.activeSelf)
                {
                    // Closing an already-open inventory must remain available even if a menu input retained focus.
                    SetPanel(inventory, false);
                }
                else if (!TextInputFocused())
                {
                    if (keyboard.bKey.wasPressedThisFrame) SetPanel(inventory, !inventory.gameObject.activeSelf);
                    else if (keyboard.tKey.wasPressedThisFrame) SetPanel(technology, !technology.gameObject.activeSelf);
                    else if (keyboard.tabKey.wasPressedThisFrame) SetGrowth(!growth.gameObject.activeSelf);
                    else if (keyboard.hKey.wasPressedThisFrame) SetPanel(help, !help.gameObject.activeSelf);
                    else if (keyboard.jKey.wasPressedThisFrame) ToggleSurvival();
                }
            }
            if (running && !AnyPanelOpen() && bridge.BuildSelection.HasValue &&
                Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) CancelBuild();
            if (running != wasRunning || (player != null && (player.hp <= 0f) != death.gameObject.activeSelf))
            {
                guardThroughFrame = Time.frameCount + 1;
                RefreshAll();
            }
            if (Time.unscaledTime >= refreshAt)
            {
                refreshAt = Time.unscaledTime + .1f;
                RefreshAll();
            }
            TickAppearancePreview();
        }

        void MakeGame(RectTransform root)
        {
            game = NewRect("In Game HUD", root);
            Stretch(game);
            RectTransform vitals = Box("Vitals", game, 18, 18, 242, 108, HudSurface);
            playerTitle = Label("Player", vitals, "", 12, 4, 218, 24, 15, Ink, FontStyles.Bold);
            Vital(vitals, 0, "Health", 32, new Color32(54, 111, 80, 255));
            Vital(vitals, 1, "Hunger", 55, new Color32(122, 94, 46, 255));
            Vital(vitals, 2, "Stamina", 78, new Color32(43, 100, 105, 255));
            RectTransform world = Box("World Status", game, 0, 18, 228, 60, HudSurface);
            // DemoWorldView owns the top-right minimap; reserve its existing 144px square.
            TopRight(world, 180, 18, 228, 60);
            clockText = Label("Day Night", world, "", 12, 4, 204, 24, 16, Ink, FontStyles.Bold);
            waveText = Label("Wave Countdown", world, "", 12, 31, 204, 24, 16, Amber, FontStyles.Bold);
            feedback = Label("World Feedback", game, "", 0, 90, 540, 72, 16, Amber);
            TopCenter(feedback.rectTransform, 90, 540, 72);
            feedback.alignment = TextAlignmentOptions.Top;
            MakeInspector();
            buildDetails = Box("Build Details", game, 0, 0, 640, 112, Panel, true);
            BottomCenter(buildDetails, 140, 640, 112);
            RectTransform buildContent = ScrollContent("Build Details Scroll View", buildDetails, 8, 8, 624, 96, out _);
            buildTitle = FlowLabel("Build Description", buildContent, "", 596, 28, 16, Accent);
            buildCost = FlowLabel("Build Cost", buildContent, "", 596, 28, 14, Ink);
            buildHelp = FlowLabel("Build Function", buildContent, "", 596, 40, 14, Muted);
            buildDetails.gameObject.SetActive(false);
            buildTray = Box("Build Hotbar", game, 0, 0, 640, 64, HudSurface);
            BottomCenter(buildTray, 64, 640, 64);
            var layout = buildTray.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i] = Button("Build Slot " + (i + 1), buildTray, "", 0, 0, 70, 52, () => SelectHotbar(index));
                var itemLayout = slots[i].button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                itemLayout.minWidth = 52;
                itemLayout.flexibleWidth = 1;
                slots[i].label.fontSizeMax = slots[i].label.fontSize = 14;
            }
            buildToggle = Button("HUD Build Toggle", game, "建造 1–0", 0, 0, 180, 36, () =>
            {
                buildTrayOpen = !(buildTrayOpen || bridge.BuildSelection.HasValue);
                if (!buildTrayOpen) CancelBuild();
                RefreshAll();
            });
            SetButtonArt(buildToggle, "ui_create", 24);
            BottomCenter(buildToggle.button.GetComponent<RectTransform>(), 18, 180, 36);
            buildTray.gameObject.SetActive(false);
            MakeEquipmentSummary();
            MakeHudTools();
        }

        void MakeTechnology(RectTransform root)
        {
            technology = Backdrop("Technology Panel", root, new Color32(4, 12, 8, 210));
            RectTransform card = Box("Technology Card", technology, 0, 0, 1040, 608, Panel, true);
            Center(card, 1040, 608);
            PanelRule(card, 1040, 64);
            Label("Technology Heading", card, "科技研究", 22, 16, 760, 38, 25, Ink, FontStyles.Bold);
            UiButton closeTechnology = Button("Close Technology", card, "返回", 902, 20, 116, 32, () => SetPanel(technology, false));
            SetButtonArt(closeTechnology, "ui_back", 20);
            for (int i = 0; i < eraButtons.Length; i++)
            {
                int era = i;
                eraButtons[i] = Button("Technology Era " + i, card, "", 22, 78 + i * 58, 180, 50, () => BrowseEra(era));
                eraButtons[i].label.fontSizeMax = 16;
            }
            Label("Technology Navigation Hint", card, "选择时代查看科技与设备\n完成前置时代后才能研究", 22, 502, 180, 64, 14, Muted);
            RectTransform content = ScrollContent("Technology Scroll View", card, 226, 78, 792, 424, out technologyScroll);
            technologyTitle = FlowLabel("Technology Era Title", content, "", 768, 34, 22, Accent);
            technologyDescription = FlowLabel("Technology Description", content, "", 768, 56, 16, Ink);
            FlowLabel("Research Materials Heading", content, "研究材料 · 持有 / 所需", 768, 28, 17, Accent);
            researchMaterials = FlowLabel("Research Materials", content, "", 768, 52, 15, Ink);
            researchRequirement = FlowLabel("Research Requirement", content, "", 768, 48, 15, Muted);
            MakePrimitiveTechnology(content);
            FlowLabel("Era Buildings Heading", content, "建造目录 · 点击已解锁设备开始建造", 768, 28, 17, Accent);
            for (int i = 0; i < technologyBuildButtons.Length; i++)
            {
                int kind = i;
                RectTransform row = FlowRow("Technology Building Row " + i, content, 768, 88);
                technologyBuildRows[i] = row;
                technologyBuildButtons[i] = Button("Technology Build " + i, row, "", 4, 8, 170, 64, () => SelectTechnologyBuild(kind));
                technologyBuildCosts[i] = Label("Building Materials", row, "", 188, 3, 564, 80, 14, Ink);
                technologyBuildCosts[i].enableAutoSizing = false;
                technologyBuildCosts[i].alignment = TextAlignmentOptions.TopLeft;
            }
            researchButton = Button("Research Button", card, "", 778, 520, 240, 48, Research, true);
            SetButtonArt(researchButton, "ui_research", 28);
            technologyNotice = Label("Technology Feedback", card, "", 226, 518, 532, 58, 14, Amber);
            Label("Technology Footer", card, "滚轮 / 拖动右侧内容查看全部设备 · 世界继续运行", 226, 578, 792, 22, 13, Muted);
            technology.gameObject.SetActive(false);
        }

        void MakeSession(RectTransform root)
        {
            session = Backdrop("Session Modal", root, new Color32(4, 12, 8, 210));
            RectTransform card = Box("Session Card", session, 0, 0, 740, 620, Panel, true);
            Center(card, 740, 620);
            PanelRule(card, 740, 68);
            Label("Session Heading", card, "游戏菜单", 28, 16, 500, 38, 27, Ink, FontStyles.Bold);
            UiButton closeSession = Button("Close Session", card, "返回", 594, 20, 118, 34, () => SetPanel(session, false));
            SetButtonArt(closeSession, "ui_back", 20);
            RectTransform content = ScrollContent("Session Scroll View", card, 28, 86, 684, 298, out _);
            networkText = FlowLabel("Network", content, "", 656, 72, 17, Ink);
            eraText = FlowLabel("Current Technology Era", content, "", 656, 28, 17, Accent);
            objectives = FlowLabel("Objectives Text", content, "", 656, 80, 17, Ink);
            gameStatus = FlowLabel("Session Status", content, "", 656, 72, 16, Muted);
            saveButton = Button("Save World", card, "保存 F5", 28, 404, 330, 46, () => CallBridge(bridge.Save), true);
            SetButtonArt(saveButton, "ui_save", 28);
            UiButton sessionSettings = Button("Session Settings", card, "设置", 382, 404, 330, 46, OpenSettings);
            SetButtonArt(sessionSettings, "ui_settings", 28);
            UiButton returnToMenu = Button("Return to Menu", card, "返回主菜单", 28, 466, 684, 44, () => CallBridge(bridge.Leave));
            SetButtonArt(returnToMenu, "ui_back", 28);
            Label("Session Hint", card, "游戏不会暂停 · 房主定期自动保存；离开前请确认存档成功。", 28, 522, 684, 28, 14, Muted);
            sessionNotice = Label("Session Feedback", card, "", 28, 562, 684, 44, 15, Amber);
            session.gameObject.SetActive(false);
        }

        void MakeHelp(RectTransform root)
        {
            help = Backdrop("Help Panel", root, new Color32(4, 12, 8, 210));
            RectTransform card = Box("Help Card", help, 0, 0, 900, 608, Panel, true);
            Center(card, 900, 608);
            PanelRule(card, 900, 68);
            Label("Help Heading", card, "操作与生存指南", 28, 16, 690, 38, 27, Ink, FontStyles.Bold);
            UiButton closeHelp = Button("Close Help", card, "返回", 756, 20, 116, 34, () => SetPanel(help, false));
            SetButtonArt(closeHelp, "ui_back", 20);
            RectTransform content = ScrollContent("Help Scroll View", card, 28, 88, 844, 420, out _);
            FlowLabel("Controls Title", content, "操作速查", 816, 30, 20, Accent);
            FlowLabel("Controls Text", content,
                "WASD 移动 · Shift 冲刺 · 滚轮缩放 · E 交互 / 采集\n左键攻击 / 放置 · 右键瞄准（建造时取消）\nR 装填（建造时旋转）· Q 切换武器 / 工具\n1–0 建造快捷栏 · T 科技研究 · X 拆除 · G 修理\nF 进食 · C 制作燃料 · Ctrl+C 制作弹药 · B 背包 / 装备 / 制作\nTab 成长 · J 生存 · H 帮助 · Esc 关闭界面 / 游戏菜单 · F5 保存", 816, 172, 18, Ink);
            FlowLabel("Tutorial Title", content, "从原始到核工业", 816, 30, 20, Accent);
            FlowLabel("Production Tutorial", content,
                "按 T 打开科技研究。采集木材、石材与黏土，研究工具、烧炭和制陶。\n用炭窑烧制木炭，再用陶窑烧制陶砖，逐步研究冶金工艺。\n原始 → 水力 → 蒸汽 → 低压 → 中压 → 高压 → 核工业，按顺序研究。\n传动轴传机械动力，管道传蒸汽；电网匹配电压，核反应堆需冷却。", 816, 124, 18, Ink);
            helpProgress = FlowLabel("Tutorial Progress", content, "", 816, 76, 16, Muted);
            FlowLabel("HUD Expansion Help", content, "点击底部展开建造栏。右下角菜单可打开其他界面，也可开启设备详情。", 816, 62, 17, Ink);
            FlowLabel("Panel Hint", content, "打开界面时游戏不会暂停。按 Esc 关闭当前界面；再次按下可取消建造或打开游戏菜单。", 816, 56, 16, Amber);
            helpNotice = Label("Help Feedback", card, "", 28, 530, 844, 56, 15, Amber);
            help.gameObject.SetActive(false);
        }

        void MakeInspector()
        {
            machinePanel = Box("Mouse Inspector", game, 0, 184, 304, 390, HudSurface, true);
            TopRight(machinePanel, 18, 184, 304, 390);
            PanelRule(machinePanel, 304, 40);
            machineTitle = Label("Inspection Title", machinePanel, "", 12, 8, 226, 28, 17, Accent, FontStyles.Bold);
            Button("Close Inspector", machinePanel, "收起", 246, 8, 46, 28, () =>
            {
                inspectorEnabled = false;
                Set(inspectToggle.label, "检查：关");
                HideInspector();
            });
            RectTransform content = ScrollContent("Machine Scroll View", machinePanel, 10, 44, 284, 260, out machineScroll);
            // Safety stays at the top of the viewport, even when inventory needs scrolling.
            machineSafety = FlowLabel("Machine Safety", content, "", 260, 52, 14, Amber);
            machineCluster = FlowLabel("Machine Steam Cluster", content, "", 260, 28, 13, Ink);
            machineCluster.gameObject.SetActive(false);
            MakeLowVoltageMachineControls(content);
            MakePetrochemistryMachineControls(content);
            MakePrecisionMachineControls(content);
            machineInfo = FlowLabel("Machine State", content, "", 260, 90, 13, Ink);
            machineRecipe = FlowLabel("Machine Recipe", content, "", 260, 28, 14, Accent);
            machineRecipeFlow = FlowLabel("Machine Recipe Flow", content, "", 260, 60, 13, Ink);
            machineContents = FlowLabel("Machine Contents", content, "", 260, 48, 13, Ink);
            machineHint = FlowLabel("Machine Hint", content, "", 260, 62, 12, Muted);
            recipeButton = Button("Cycle Machine Recipe", machinePanel, "切换配方", 12, 312, 280, 30, CycleRecipe);
            interactButton = Button("Interact Machine", machinePanel, "交互 E", 12, 350, 86, 30, () => SendInspected("interact"));
            repairButton = Button("Repair Machine", machinePanel, "修理 G", 109, 350, 86, 30, () => SendInspected("repair"));
            dismantleButton = Button("Dismantle Machine", machinePanel, "拆除 X", 206, 350, 86, 30, () => SendInspected("dismantle"));
            SetButtonArt(recipeButton, "ui_craft", 20);
            SetButtonArt(interactButton, "ui_equip", 18);
            SetButtonArt(repairButton, "ui_repair", 18);
            SetButtonArt(dismantleButton, "ui_dismantle", 18);
            machinePanel.gameObject.SetActive(false);
        }

        void MakeGrowth(RectTransform root)
        {
            growth = Backdrop("Growth Modal", root, new Color32(4, 12, 8, 225));
            RectTransform card = Box("Growth Card", growth, 0, 0, 1080, 606, Panel, true);
            Center(card, 1080, 606);
            PanelRule(card, 1080, 100);
            Label("Growth Title", card, "幸存者成长", 24, 18, 750, 40, 28, Ink, FontStyles.Bold);
            growthSummary = Label("Growth Summary", card, "", 24, 66, 920, 26, 16, Muted);
            UiButton closeGrowth = Button("Close Growth", card, "返回", 930, 24, 126, 34, () => SetGrowth(false));
            SetButtonArt(closeGrowth, "ui_back", 20);
            attributeHeading = Label("Attribute Points", card, "", 24, 110, 336, 28, 19, Accent, FontStyles.Bold);
            skillHeading = Label("Skill Points", card, "", 384, 110, 672, 28, 19, Accent, FontStyles.Bold);
            for (int i = 0; i < attributeButtons.Length; i++)
            {
                int index = i;
                RectTransform row = Box("Attribute " + i, card, 24, 154 + i * 73, 336, 63, Raised);
                Label("Name", row, DemoCatalog.AttributeNames[i], 12, 7, 132, 25, 18, Ink, FontStyles.Bold);
                Label("Effect", row, AttributeHelp[i], 12, 37, 270, 19, 12, Muted);
                attributeValues[i] = Label("Level", row, "", 148, 8, 98, 24, 16, Accent);
                attributeButtons[i] = Button("Upgrade Attribute", row, "+", 280, 10, 44, 42, () => Send("attribute", index));
            }
            // Each branch stores its learned depth in skills[branch]. All three
            // tiers use arg=branch; the tier is a prerequisite, not a command arg.
            for (int branch = 0; branch < 3; branch++)
            {
                int branchIndex = branch;
                RectTransform column = Box("Skill Branch " + branch, card, 384 + branch * 228, 154, 216, 355, Raised);
                Label("Branch Name", column, DemoCatalog.SkillNames[branch], 12, 10, 192, 26, 18, Ink, FontStyles.Bold);
                Label("Branch Description", column, DemoCatalog.SkillDescriptions[branch], 12, 43, 192, 43, 13, Muted);
                for (int tier = 0; tier < 3; tier++)
                {
                    int tierIndex = tier;
                    if (tier > 0) Box("Prerequisite Link", column, 106, 152 + (tier - 1) * 80, 3, 17, Muted);
                    skillButtons[branch, tier] = Button("Tier " + (tier + 1), column, "",
                        12, 91 + tier * 80, 192, 62, () => UpgradeSkill(branchIndex, tierIndex));
                    skillButtons[branch, tier].label.fontSize = 14;
                }
            }
            Label("Growth Rules", card, "每项属性上限为10，提升1点消耗1属性点。技能须依次学习，另需技能点；第1、2、3阶分别需要角色等级1、3、5。",
                24, 529, 1032, 24, 13, Muted);
            growthNotice = Label("Growth Feedback", card, "", 24, 559, 1032, 36, 15, Amber);
        }

        void MakeDeath(RectTransform root)
        {
            death = Backdrop("Death Modal", root, new Color32(9, 19, 13, 235));
            RectTransform card = Box("Rescue Card", death, 0, 0, 570, 340, Panel, true);
            Center(card, 570, 340);
            PanelRule(card, 570, 90);
            Label("Death Heading", card, "回路中断", 28, 26, 514, 52, 36, Danger, FontStyles.Bold);
            deathInfo = Label("Death Details", card, "", 28, 100, 514, 108, 18, Ink);
            respawnButton = Button("Respawn", card, "复活", 28, 235, 244, 48, () => Send("respawn"), true);
            SetButtonArt(respawnButton, "ui_back", 28);
            UiButton leaveAfterDeath = Button("Leave After Death", card, "返回主菜单", 290, 235, 252, 48, () => CallBridge(bridge.Leave));
            SetButtonArt(leaveAfterDeath, "ui_quit", 28);
            Label("Death Hint", card, "游戏不会暂停 · 复活后保留背包、等级与技能", 28, 300, 514, 24, 14, Muted);
        }

        void RefreshAll()
        {
            if (!initialized || bridge == null) return;
            bool running = bridge.IsRunning;
            if (wasRunning != running)
            {
                CloseDeleteWorldDialog();
                inspectedId = inspectedX = inspectedY = -1;
                localFeedback = "";
                lastBridgeStatus = lastPlayerNotice = lastWorldNotice = "";
                statusUntil = playerFeedbackUntil = worldFeedbackUntil = 0f;
                observedEra = -1;
                lastPlayerNoticeExpiry = lastWorldNoticeExpiry = -1f;
                ClosePanels();
                buildTrayOpen = inspectorEnabled = false;
                toolsTray.gameObject.SetActive(false);
                Set(toolsToggle.label, "菜单");
                Set(inspectToggle.label, "检查：关");
                if (!running) ShowMenuPage(MenuPage.Home);
                if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            }
            wasRunning = running;
            menu.gameObject.SetActive(!running);
            game.gameObject.SetActive(running);
            PlayerState player = bridge.LocalPlayer;
            WorldState world = bridge.World;
            bool dead = running && player != null && player.hp <= 0f;
            death.gameObject.SetActive(dead);
            if (dead)
            {
                ClosePanels();
                buildDetails.gameObject.SetActive(false);
                machinePanel.gameObject.SetActive(false);
            }
            else if (!running)
            {
                buildDetails.gameObject.SetActive(false);
                machinePanel.gameObject.SetActive(false);
            }
            if (eventSystem != null) eventSystem.sendNavigationEvents = !running || dead || AnyPanelOpen();
            if (!running) { RefreshMenu(); return; }
            if (world == null || player == null)
            {
                buildDetails.gameObject.SetActive(false);
                machinePanel.gameObject.SetActive(false);
                Set(feedback, "正在同步世界与角色，请稍候…");
                SetFlowText(gameStatus, Clean(bridge.Status));
                return;
            }
            RefreshPlayer(world, player);
            RefreshWorld(world);
            RefreshBuild(player);
            RefreshInspector(world, player);
            RefreshFeedback(world, player);
            RefreshAdditionalPanels(world, player);
            if (inventory.gameObject.activeSelf) RefreshInventory(world, player);
            if (technology.gameObject.activeSelf) RefreshTechnology(world, player);
            if (growth.gameObject.activeSelf) RefreshGrowth(player);
            if (dead)
            {
                float delay = Mathf.Max(0f, 5f - (world.elapsed - player.lastDamageAt));
                respawnButton.button.interactable = delay <= 0f;
                Set(respawnButton.label, delay > 0f ? "等待复活 · " + Mathf.CeilToInt(delay) + " 秒" : "复活");
                Set(deathInfo, "累计倒下：" + player.deaths + "\n" +
                    (delay > 0f ? "复活冷却中，请稍候。" : "复活后将在安全位置落脚。") +
                    (player.noticeUntil > world.elapsed ? "\n" + Clean(player.notice) : ""));
            }
        }

        void RefreshPlayer(WorldState world, PlayerState player)
        {
            Set(playerTitle, Clean(player.name) + "  ·  Lv." + player.level);
            SetVital(0, "生命", player.hp, WorldSimulator.PlayerMaxHealth(player));
            SetVital(1, "饱食", player.hunger, 100f);
            SetVital(2, "体力", player.stamina, WorldSimulator.PlayerMaxStamina(player));
            int points = player.attributePoints + player.skillPoints;
            Set(growthButton.label, "成长 Tab" + (points > 0 ? " ·" + points : ""));
            growthButton.label.color = player.attributePoints + player.skillPoints > 0 ? Accent : Ink;
            growthButton.button.interactable = player.hp > 0f;
            RefreshEquipmentSummary(player);
            eatButton.button.interactable = player.hp > 0f && player.hunger < 99f && Count(player.items, 6) > 0;
            int era = Mathf.Clamp((int)world.era, 0, eraButtons.Length - 1);
            if (observedEra != era)
            {
                observedEra = era;
                selectedEra = DefaultTechnologyEra(world);
                ResetScroll(technologyScroll);
            }
            string goal = NextEraGoal(world, player);
            Set(eraText, "科技：" + TechCatalog.EraName(world.era) + "  ·  T 查看 / 建造");
            SetFlowText(objectives, (PrimitiveCatalog.Enabled(world) && era == 0 ? "原始工艺 · 按 T 查看研究条件" : era < 6 ? "下一时代 · " + TechCatalog.EraName((TechEra)(era + 1)) : "核工业运行") + "\n" + goal);
            SetFlowText(helpProgress, "当前：" + TechCatalog.EraName(world.era) + "  ·  " + goal + "\n击杀 " + world.kills + "  ·  制造 " + world.manufactured);
        }

        void RefreshWorld(WorldState world)
        {
            float dayLength = Mathf.Max(1f, world.dayLength);
            float phase = Mathf.Repeat(world.elapsed, dayLength) / dayLength;
            int day = Mathf.FloorToInt(world.elapsed / dayLength) + 1;
            // Match DemoWorldView's ambient cycle rather than invent a different dusk.
            float night = Mathf.Clamp01((Mathf.Cos((phase + .5f) * Mathf.PI * 2f) + .12f) * 1.5f);
            string daytime = night > .15f ? "夜晚" : "白昼";
            Set(clockText, "第 " + day.ToString("00") + " 日  ·  " + daytime);
            int remaining = Mathf.CeilToInt(Mathf.Max(0f, world.nextWaveAt - world.elapsed));
            Set(waveText, remaining == 0 ? "第 " + (world.wave + 1) + " 波即将来袭" : "第 " + (world.wave + 1) + " 波  ·  " + (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00"));
            waveText.color = remaining <= 30 ? Danger : Amber;
            int connected = 0;
            foreach (PlayerState peer in world.players) if (peer.connected) connected++;
            SetFlowText(networkText, (bridge.IsHost ? "房主" : "队员") + " · " + connected + "/" + WorldSimulator.MaxPlayers + " 人 · 丧尸 " + world.zombies.Count +
                "\n本机 IPv4：" + Clean(bridge.HostIP) + "  ·  UDP 7777");
            saveButton.button.interactable = bridge.IsHost;
            Set(saveButton.label, bridge.IsHost ? "保存 F5" : "仅房主可保存");
            SetFlowText(gameStatus, Clean(bridge.Status) + RoomLocation(world, bridge.LocalPlayer));
            gameStatus.color = StatusColor(bridge.Status);
            if (lastBridgeStatus != bridge.Status)
            {
                lastBridgeStatus = bridge.Status;
                statusUntil = Time.unscaledTime + 5f;
            }
        }

        void RefreshBuild(PlayerState player)
        {
            WorldState world = bridge.World;
            int selected = bridge.BuildSelection.HasValue ? (int)bridge.BuildSelection.Value : -1;
            BuildKind[] hotbar = TechCatalog.Hotbar(world.era);
            for (int i = 0; i < slots.Length; i++)
            {
                bool valid = i < hotbar.Length && (int)hotbar[i] >= 0 && (int)hotbar[i] < costs.Length;
                int kind = valid ? (int)hotbar[i] : -1;
                bool unlocked = valid && PrimitiveCatalog.IsBuildUnlocked(world, hotbar[i]);
                bool affordable = valid && CanAfford(player.items, costs[kind]);
                Set(slots[i].label, ((i + 1) % 10) + "\n" + (valid ? DemoCatalog.BuildName(hotbar[i]) + (unlocked ? "" : "\n" + PrimitiveCatalog.BuildRequirement(world, hotbar[i])) : "空"));
                Tint(slots[i], valid && selected == kind ? Selected : Raised);
                slots[i].label.color = valid && selected == kind ? Accent : unlocked && affordable ? Ink : Muted;
                slots[i].button.interactable = unlocked && player.hp > 0f && !AnyPanelOpen();
            }
            bool available = player.hp > 0f && !AnyPanelOpen();
            buildTray.gameObject.SetActive(available && (buildTrayOpen || selected >= 0));
            buildToggle.button.gameObject.SetActive(available);
            Set(buildToggle.label, buildTrayOpen || selected >= 0 ? "收起" : "建造 1–0");
            bool show = selected >= 0 && selected < costs.Length && player.hp > 0f && !AnyPanelOpen();
            buildDetails.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }
            int rotation = ((bridge.BuildRotation % 4) + 4) % 4;
            SetFlowText(buildTitle, DemoCatalog.BuildNames[selected] + "  ·  " + DemoCatalog.Width((BuildKind)selected, rotation) + "×" + DemoCatalog.Height((BuildKind)selected, rotation) +
                " 格  ·  朝" + Directions[rotation] + "  [R 旋转]  ·  右键取消");
            text.Clear();
            for (int i = 0; i < costs[selected].Length; i++)
            {
                if (costs[selected][i] <= 0) continue;
                if (text.Length > 0) text.Append("   ");
                bool enough = Count(player.items, i) >= costs[selected][i];
                text.Append(enough ? "<color=#A6D9BB>" : "<color=#F48071>");
                text.Append(DemoCatalog.ItemNames[i]).Append(' ').Append(Count(player.items, i)).Append('/').Append(costs[selected][i]).Append("</color>");
            }
            SetFlowText(buildCost, text.ToString());
            SetFlowText(buildHelp, EnergyName(TechCatalog.EnergyTier((BuildKind)selected)) + " · " +
                DemoCatalog.BuildDescriptions[selected] + "  ·  靠近后左键放置");
        }

        void RefreshTechnology(WorldState world, PlayerState player)
        {
            int current = (int)world.era;
            TechEra target = (TechEra)selectedEra;
            for (int i = 0; i < eraButtons.Length; i++)
            {
                string state = i == current ? "当前" : i < current ? "已解锁" : "待解锁";
                Set(eraButtons[i].label, TechCatalog.EraName((TechEra)i) + "  ·  " + state);
                Tint(eraButtons[i], i == selectedEra ? Selected : Raised);
                eraButtons[i].label.color = i == selectedEra ? Accent : i <= current ? Ink : Muted;
            }
            Set(technologyTitle, TechCatalog.EraName(target) + (selectedEra == current ? " · 当前时代" : selectedEra < current ? " · 已解锁" : " · 待解锁"));
            SetFlowText(technologyDescription, EraDescriptions[selectedEra], 56);
            bool unlocked = selectedEra <= current;
            bool primitive = selectedEra == 0 && PrimitiveCatalog.Enabled(world);
            SetFlowText(researchMaterials, selectedEra == 0 ? primitive ? "各项工艺需单独研究，并消耗背包材料。生产进度由全队累计。" : "原始工艺已开放，无需额外研究。" : MaterialText(TechCatalog.ResearchCost(target), player.items, true), 28);
            string requirement = unlocked ? "此时代已解锁。完成对应工艺研究后，即可选择设备建造。" :
                selectedEra != current + 1 ? "前置：先完成" + TechCatalog.EraName((TechEra)(selectedEra - 1)) + "研究，不可越级。" :
                EraResearchRequirement(world, target);
            if (primitive) requirement = "查看各项工艺的前置研究、所需材料和生产进度。满足条件后，点击研究。";
            SetFlowText(researchRequirement, string.IsNullOrEmpty(requirement) ? "研究条件已满足。" : requirement, 30);
            researchRequirement.color = unlocked ? Accent : string.IsNullOrEmpty(requirement) ? Accent : Amber;
            bool canResearch = selectedEra == current + 1 && player.connected && player.hp > 0f && player.interactCooldown <= 0f &&
                string.IsNullOrEmpty(EraResearchRequirement(world, target)) && TechCatalog.CanResearch(world, player, target);
            researchButton.button.interactable = canResearch;
            Set(researchButton.label, primitive ? "选择下方工艺研究" : unlocked ? "该时代已解锁" : selectedEra != current + 1 ? "请先完成前一时代" : canResearch ? "研究" + TechCatalog.EraName(target) : "研究条件未满足");
            RefreshPrimitiveTechnology(world, player);
            for (int i = 0; i < technologyBuildRows.Length; i++)
            {
                BuildKind kind = (BuildKind)i;
                bool visible = (int)TechCatalog.RequiredEra(kind) == selectedEra;
                if (technologyBuildRows[i].gameObject.activeSelf != visible) technologyBuildRows[i].gameObject.SetActive(visible);
                if (!visible) continue;
                bool available = PrimitiveCatalog.IsBuildUnlocked(world, kind);
                technologyBuildButtons[i].button.interactable = available && player.hp > 0f;
                string buildRequirement = available ? "选择建造" : PrimitiveCatalog.BuildRequirement(world, kind);
                Set(technologyBuildButtons[i].label, DemoCatalog.BuildName(kind) + "\n" + (available ? "选择建造" : "工艺未解锁"));
                technologyBuildButtons[i].label.color = available ? Accent : Muted;
                Set(technologyBuildCosts[i], DemoCatalog.Width(kind) + "×" + DemoCatalog.Height(kind) + " 格 · " + MaterialText(costs[i], player.items, true) + "\n" +
                    EnergyName(TechCatalog.EnergyTier(kind)) + " · " + DemoCatalog.BuildDescriptions[i] + (available ? "" : "\n" + buildRequirement));
                float height = Mathf.Max(88, technologyBuildCosts[i].GetPreferredValues(technologyBuildCosts[i].text, 564, float.PositiveInfinity).y + 12);
                technologyBuildCosts[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 8);
                technologyBuildRows[i].GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            }
        }

        void RefreshInspector(WorldState world, PlayerState player)
        {
            var mouse = Mouse.current;
            if (!inspectorEnabled || AnyPanelOpen() || player.hp <= 0f || bridge.BuildSelection.HasValue || mouse == null)
            {
                HideInspector();
                return;
            }
            bool overInspector = machinePanel.gameObject.activeSelf &&
                RectTransformUtility.RectangleContainsScreenPoint(machinePanel, mouse.position.ReadValue());
            if (!overInspector)
            {
                if (PointerHitsUi()) { HideInspector(); return; }
                Vector2 target = bridge.MouseWorld;
                inspectedX = Mathf.FloorToInt(target.x + .5f);
                inspectedY = Mathf.FloorToInt(target.y + .5f);
            }
            BuildingState building = null;
            foreach (BuildingState candidate in world.buildings)
                if (candidate.hp > 0 && DemoCatalog.Contains(candidate, inspectedX, inspectedY)) { building = candidate; break; }
            NodeState node = null;
            if (building == null)
                foreach (NodeState candidate in world.nodes)
                    if (candidate.hp > 0 && candidate.stock > 0 && candidate.x == inspectedX && candidate.y == inspectedY) { node = candidate; break; }
            if (building == null && node == null) { HideInspector(); return; }
            int newId = building != null ? building.id : -1;
            if (inspectedId != newId) ResetScroll(machineScroll);
            machinePanel.gameObject.SetActive(true);
            inspectedId = building != null ? building.id : -1;
            interactButton.button.gameObject.SetActive(building != null);
            repairButton.button.gameObject.SetActive(building != null);
            dismantleButton.button.gameObject.SetActive(building != null);
            recipeButton.button.gameObject.SetActive(false);
            bool inRange = (new Vector2(player.x - inspectedX, player.y - inspectedY)).sqrMagnitude <= 2.4f * 2.4f && player.hp > 0f;
            RefreshLowVoltageMachineControls(world, player, building, inRange);
            RefreshPrecisionMachineControls(world, player, building);
            if (RefreshPetrochemistryMachineControls(world, player, building, node, inRange)) return;
            interactButton.button.interactable = building != null && inRange;
            repairButton.button.interactable = building != null && inRange && building.hp < WorldSimulator.BuildingMaxHealth(building.kind) &&
                Count(player.items, building.kind == BuildKind.Wall || building.kind == BuildKind.Door ? 0 : 3) > 0;
            dismantleButton.button.interactable = building != null && inRange;
            if (building != null)
            {
                int tier = building.kind == BuildKind.Transformer && building.setting == 1 ? 3 : TechCatalog.EnergyTier(building.kind);
                bool needsPower = building.powerDemand > 0 || DemoCatalog.Demand(building.kind) > 0;
                PowerNetworkState grid = world.powerNetworks?.Find(n => n.id == building.networkId);
                string unit = tier < 2 ? " 功率" : " kW";
                string power = "动力：" + EnergyName(tier);
                if (tier >= 0)
                    power += "\n供给 / 需求 " + building.powerGeneration.ToString("0.#") + " / " + building.powerDemand.ToString("0.#") + unit +
                        (needsPower ? " · 动力供给 " + (building.powerRatio * 100f).ToString("0") + "%" : "");
                if (grid != null) power += "\n" + EnergyName(grid.tier) + "网 " + grid.id + " · " +
                    grid.generation.ToString("0.#") + " / " + grid.demand.ToString("0.#") + (grid.tier < 2 ? " 功率" : " kW");
                Set(machineTitle, InspectedMachineName(building) + "  " + CellLocationText(world, building.x, building.y));
                machineTitle.color = needsPower && building.powerRatio < .99f ? Amber : Accent;
                SetFlowText(machineInfo, "耐久 " + Mathf.CeilToInt(building.hp) + " / " + WorldSimulator.BuildingMaxHealth(building.kind).ToString("0") +
                    " · " + DemoCatalog.Width(building) + "×" + DemoCatalog.Height(building) + "格\n" + power + "\n" + MachineFlow(building));
                RefreshSteamCluster(world, building);
                int moving = world.cargo == null ? 0 : world.cargo.FindAll(c => c.fromId == building.id || c.toId == building.id).Count;
                SetFlowText(machineContents, InventoryText(building.items) + (moving > 0 ? "\n运输中的物品 " + moving + " 件" : ""));
                SetFlowText(machineHint, (inRange ? "操作范围：2.4 格，视线须畅通。" : "距离过远，请靠近设备。") +
                    "\n" + InspectedMachineDescription(building));
                Set(interactButton.label, building.kind == BuildKind.Door ? (building.setting == 1 ? "关门 E" : "开门 E") : "交互 E");
                RecipeDefinition recipe = MachineRecipe(building);
                RecipeDefinition configured = building.kind == BuildKind.Workbench ? null : TechCatalog.SelectedRecipe(building);
                bool reactor = building.kind == BuildKind.Reactor;
                bool reactorConsole = building.kind == BuildKind.ReactorControlConsole;
                bool transformer = building.kind == BuildKind.Transformer;
                machineRecipe.gameObject.SetActive(configured != null || transformer);
                machineRecipeFlow.gameObject.SetActive(configured != null || reactor);
                machineSafety.gameObject.SetActive(reactor || reactorConsole || transformer || building.kind == BuildKind.SteamTurbine);
                machineSafety.color = Amber;
                if (recipe != null)
                {
                    SetFlowText(machineRecipe, "配方：" + recipe.name, 28);
                    SetFlowText(machineRecipeFlow, "输入：" + MaterialText(recipe.inputs, building.items, true) + "\n产出：" + MaterialText(recipe.outputs) +
                        "\n进度 " + building.progress.ToString("0.0") + " / " + recipe.duration.ToString("0.0") + " 秒");
                }
                else if (configured != null)
                {
                    SetFlowText(machineRecipe, "当前配方：" + configured.name + " · 工艺未解锁", 28);
                    SetFlowText(machineRecipeFlow, PrimitiveCatalog.RecipeRequirement(world, configured));
                }
                int recipes = MachineRecipeCount(building.kind, world);
                bool chooseRecipe = recipes > 1 || recipes == 1 && recipe == null;
                recipeButton.button.gameObject.SetActive(chooseRecipe || transformer || reactorConsole);
                recipeButton.button.interactable = inRange && (reactorConsole ? CanConfigureReactorConsole(world, player) :
                    transformer ? CanSwitchTransformer(world, building) : chooseRecipe);
                Set(recipeButton.label, reactorConsole ? (building.setting == 1 ? "停用控制台" : "启用控制台") :
                    transformer ? (building.setting == 0 ? "切换：高压 → 中压" : "切换：中压 → 低压") : "切换配方（" + recipes + " 种）");
                if (reactorConsole)
                {
                    bool externalPower = building.powered && building.powerRatio >= 1f;
                    SetFlowText(machineSafety, (building.setting == 1 ? "控制台已启用" : "控制台已停用") +
                        "\n外部供电：" + (externalPower ? "充足" : "不足") +
                        "\n建成默认停用；须与反应堆接入同一高压(HV)电网，由外部电源足额供电。");
                    machineSafety.color = building.setting == 1 && externalPower ? Accent : Amber;
                }
                if (transformer)
                {
                    SetFlowText(machineRecipe, building.setting == 0 ? "变压：中压 → 低压" : "变压：高压 → 中压", 28);
                    SetFlowText(machineSafety, !CanSwitchTransformer(world, building) ? "研究高压电网后可切换高压 → 中压。" : "输入端与输出端须分别接入对应电压的电网。");
                }
                if (building.kind == BuildKind.SteamTurbine)
                    SetFlowText(machineSafety, "将蒸汽转为低压电力。\n蒸汽管接输入端，低压电网接输出端。");
                if (reactor)
                {
                    int coolant = Count(building.items, (int)ItemKind.Coolant);
                    bool cooling = coolant > 0 || building.progress > 0f;
                    bool nuclearFuel = Count(building.items, (int)ItemKind.NuclearFuel) > 0 || building.fuel > 0f;
                    bool running = building.powerGeneration > 0f;
                    string state = building.tripped ? "已停堆 · 正在降温" : running ? "运行中" :
                        !cooling ? "停机 · 缺少冷却液" : !nuclearFuel ? "待机 · 缺少核燃料" : "待机 · 检查控制台和出料连接";
                    SetFlowText(machineRecipeFlow, "输入：核燃料、冷却液\n产出：高压电力、核废料、热冷却液" +
                        "\n箭头正前方：直连核废料专用仓。" +
                        "\n面朝箭头时的右侧：用热冷却液管道接到靠近水源的冷却塔，再用独立的冷却液管道接回反应堆。" +
                        "\n冷却塔需中压(MV)供电和工艺水。" +
                        "\n启用同一高压(HV)电网中的反应堆控制台，确保控制台由外部电源足额供电。");
                    SetFlowText(machineSafety, "温度 " + building.temperature.ToString("0") + "℃ · " + state +
                        "\n冷却液 " + coolant + " 单位 · 剩余冷却时间 " + building.progress.ToString("0.0") + " 秒" +
                        "\n堆内核废料 " + Count(building.items, (int)ItemKind.NuclearWaste) + " 件" +
                        "\n达到 900℃时自动停堆，降至 300℃后可重新运行。");
                    machineSafety.color = building.tripped || building.temperature >= 700f || !running && !cooling ? Danger : running ? Accent : Amber;
                }
                return;
            }
            machineRecipe.gameObject.SetActive(false);
            machineRecipeFlow.gameObject.SetActive(false);
            machineSafety.gameObject.SetActive(false);
            machineCluster.gameObject.SetActive(false);
            machineTitle.color = Accent;
            Set(machineTitle, Nodes[(int)node.kind] + "  " + CellLocationText(world, inspectedX, inspectedY));
            int resourceBiome = WorldTiles.Biome(world, node.x, node.y) - '0';
            string biomeName = resourceBiome >= 0 && resourceBiome < WorldGenerationCatalog.Biomes.Count
                ? WorldGenerationCatalog.Biomes[resourceBiome].name : "未知";
            SetFlowText(machineInfo, "所在群系：" + biomeName + "\n资源余量：" + node.stock);
            SetFlowText(machineContents, "资源耐久：" + Mathf.CeilToInt(node.hp) + "\n采集工具：" + ResourceToolText(node.kind));
            SetFlowText(machineHint, ResourceCollectionHint(world, player, node));
        }

        void HideInspector()
        {
            machinePanel.gameObject.SetActive(false);
            inspectedId = inspectedX = inspectedY = -1;
        }

        void RefreshGrowth(PlayerState player)
        {
            Set(growthSummary, "Lv." + player.level + "  ·  经验 " + player.xp + " / " + WorldSimulator.XpToNextLevel(player.level) +
                "  ·  每次升级获得 2 属性点与 1 技能点");
            Set(attributeHeading, "属性强化  /  可用点数 " + player.attributePoints);
            Set(skillHeading, "专精树  /  可用技能点 " + player.skillPoints);
            for (int i = 0; i < attributeButtons.Length; i++)
            {
                int rank = Count(player.attributes, i);
                Set(attributeValues[i], rank + " / 10");
                attributeButtons[i].button.interactable = player.attributePoints > 0 && rank < 10 && player.hp > 0f;
                Set(attributeButtons[i].label, rank >= 10 ? "满" : "+");
            }
            for (int branch = 0; branch < 3; branch++)
            {
                int rank = Count(player.skills, branch);
                for (int tier = 0; tier < 3; tier++)
                {
                    int requiredLevel = 1 + tier * 2;
                    bool learned = rank > tier;
                    bool next = rank == tier;
                    bool levelMet = player.level >= requiredLevel;
                    UiButton node = skillButtons[branch, tier];
                    node.button.interactable = !learned && next && levelMet && player.skillPoints > 0 && player.hp > 0f;
                    string state = learned ? "已掌握" : !next ? "前置：先学习上一阶" :
                        !levelMet ? "需角色等级 " + requiredLevel : player.skillPoints <= 0 ? "需要 1 技能点" : "学习本阶 · 1 技能点";
                    Set(node.label, "第 " + (tier + 1) + " 阶  ·  Lv." + requiredLevel + "\n" + state);
                    Tint(node, learned ? new Color32(42, 86, 67, 255) : next && levelMet ? new Color32(84, 65, 38, 255) : Panel);
                    node.label.color = learned ? Accent : next && levelMet ? Amber : Muted;
                }
            }
        }

        void RefreshFeedback(WorldState world, PlayerState player)
        {
            float now = Time.unscaledTime;
            if (lastPlayerNotice != player.notice || lastPlayerNoticeExpiry != player.noticeUntil)
            {
                lastPlayerNotice = player.notice;
                lastPlayerNoticeExpiry = player.noticeUntil;
                playerFeedbackUntil = now + (IsErrorFeedback(player.notice) ? 5f : 3.5f);
            }
            if (lastWorldNotice != world.announcement || lastWorldNoticeExpiry != world.announcementUntil)
            {
                lastWorldNotice = world.announcement;
                lastWorldNoticeExpiry = world.announcementUntil;
                worldFeedbackUntil = now + 5f;
            }
            bool notice = player.noticeUntil > world.elapsed && now < playerFeedbackUntil &&
                !string.IsNullOrWhiteSpace(player.notice) && !IsIntroNotice(player.notice);
            bool local = CurrentLocalFeedback();
            bool status = now < statusUntil && !string.IsNullOrWhiteSpace(bridge.Status);
            bool announcement = world.announcementUntil > world.elapsed && now < worldFeedbackUntil &&
                !string.IsNullOrWhiteSpace(world.announcement) && !IsIntroNotice(world.announcement);
            string message = "";
            Color color = Amber;
            // One message only: errors, player action results, local results, then world events.
            if (local && localFeedbackError) { message = localFeedback; color = Danger; }
            else if (status && IsErrorFeedback(bridge.Status)) { message = bridge.Status; color = Danger; }
            else if (notice && IsErrorFeedback(player.notice)) { message = player.notice; color = Danger; }
            else if (notice) message = player.notice;
            else if (local) { message = localFeedback; color = localFeedbackColor; }
            else if (status && bridge.Status.Contains("已保存")) { message = bridge.Status; color = Accent; }
            else if (announcement) message = world.announcement;
            message = Clean(message);
            bool showWorldFeedback = !AnyPanelOpen() && player.hp > 0f;
            string worldFeedback = showWorldFeedback ? message : "";
            if (showWorldFeedback && Mouse.current != null && !CapturesInput)
            {
                string stair = StairTargetHint(world, player);
                if (stair.Length > 0) worldFeedback = message.Length == 0 ? stair : message + "\n" + stair;
            }
            Set(feedback, worldFeedback);
            feedback.color = color;
            Set(inventoryNotice, inventory.gameObject.activeSelf ? message : "");
            inventoryNotice.color = color;
            Set(sessionNotice, session.gameObject.activeSelf && message != Clean(bridge.Status) ? message : "");
            sessionNotice.color = color;
            Set(helpNotice, help.gameObject.activeSelf ? message : "");
            helpNotice.color = color;
            Set(growthNotice, growth.gameObject.activeSelf ?
                (message.Length > 0 ? message : "点击亮起的技能学习。属性与技能分别消耗对应点数。") : "");
            growthNotice.color = color;
            Set(technologyNotice, technology.gameObject.activeSelf ?
                (message.Length > 0 ? message : "研究由全队共享；只消耗发起者背包中的材料。") : "");
            technologyNotice.color = color;
        }

        string StairTargetHint(WorldState world, PlayerState player)
        {
            Vector2 point = bridge.MouseWorld;
            int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
            if (!FloorWorld.TryStair(world, x, y, out var stair)) return "";
            if ((new Vector2(player.x - x, player.y - y)).sqrMagnitude > 2.4f * 2.4f)
                return "靠近楼梯 · 通往" + FloorWorld.LevelName(stair.targetLevel);
            return (stair.Glyph == '<' ? "E 上楼 · " : "E 下楼 · ") + FloorWorld.LevelName(stair.targetLevel);
        }

        static bool IsIntroNotice(string value) => !string.IsNullOrEmpty(value) &&
            (value.StartsWith("示范产线：", StringComparison.Ordinal) ||
             value.StartsWith("原始开局：", StringComparison.Ordinal) ||
             value.StartsWith("原始时代：", StringComparison.Ordinal) ||
             value.StartsWith("补齐 (", StringComparison.Ordinal) || value.StartsWith("补(34,30)", StringComparison.Ordinal));

        static bool IsErrorFeedback(string value) => !string.IsNullOrEmpty(value) &&
            (value.Contains("失败") || value.Contains("超时") || value.Contains("断开") || value.Contains("无法") ||
             value.Contains("不可") || value.Contains("不足") || value.Contains("已满") || value.Contains("尚未") ||
             value.Contains("需携带") || value.Contains("需供电") || value.Contains("需紧邻") ||
             value.Contains("下一层需") || value.Contains("人物占据") || value.Contains("只有房主") ||
             value.Contains("暂时无法"));

        void Join()
        {
            string value = ipField.text.Trim();
            if (!IPAddress.TryParse(value, out IPAddress address) || address.AddressFamily != AddressFamily.InterNetwork || value.Split('.').Length != 4)
            { Notify("请输入完整主机 IPv4，例如 192.168.1.8；端口固定为 UDP 7777。", true); return; }
            bridge.PlayerName = ReadName();
            CallBridge(() => bridge.Join(address.ToString(), bridge.PlayerName));
        }

        string ReadName()
        {
            string value = nameField.text.Trim();
            return string.IsNullOrWhiteSpace(value) ? "幸存者" : value;
        }

        void SelectBuild(int index)
        {
            if (!bridge.IsRunning || AnyPanelOpen() || bridge.LocalPlayer == null || bridge.LocalPlayer.hp <= 0f) return;
            if (index < 0 || index >= costs.Length || !PrimitiveCatalog.IsBuildUnlocked(bridge.World, (BuildKind)index)) return;
            bridge.BuildSelection = (BuildKind)index;
            guardThroughFrame = Time.frameCount + 1;
            RefreshAll();
        }

        void SelectHotbar(int slot)
        {
            if (bridge.World == null) return;
            BuildKind[] hotbar = TechCatalog.Hotbar(bridge.World.era);
            if (slot >= 0 && slot < hotbar.Length) SelectBuild((int)hotbar[slot]);
        }

        void SelectTechnologyBuild(int kind)
        {
            if (!technology.gameObject.activeSelf || !PrimitiveCatalog.IsBuildUnlocked(bridge.World, (BuildKind)kind)) return;
            SetPanel(technology, false);
            SelectBuild(kind);
        }

        void BrowseEra(int era)
        {
            selectedEra = Mathf.Clamp(era, 0, eraButtons.Length - 1);
            ResetScroll(technologyScroll);
            RefreshAll();
        }

        void Research()
        {
            if (!technology.gameObject.activeSelf || bridge.World == null || bridge.LocalPlayer == null) return;
            if (selectedEra == (int)TechEra.HighVoltage && bridge.World.era >= TechEra.HighVoltage)
            {
                ResearchPrecision(bridge.World, bridge.LocalPlayer);
                return;
            }
            if (selectedEra != (int)bridge.World.era + 1 || bridge.LocalPlayer.interactCooldown > 0f ||
                !string.IsNullOrEmpty(EraResearchRequirement(bridge.World, (TechEra)selectedEra)) ||
                !TechCatalog.CanResearch(bridge.World, bridge.LocalPlayer, (TechEra)selectedEra)) return;
            Send("research", selectedEra);
        }

        void CraftRecipe(int id)
        {
            if (!inventory.gameObject.activeSelf || bridge.World == null || bridge.LocalPlayer == null) return;
            RecipeRow row = manualRecipes.Find(r => r.recipe.id == id);
            PlayerState player = bridge.LocalPlayer;
            if (row == null || !PrimitiveCatalog.IsRecipeUnlocked(bridge.World, row.recipe) || player.interactCooldown > 0f ||
                !CanAfford(player.items, row.recipe.inputs) || !HasRecipeSpace(player.items, row.recipe)) return;
            if (id > 1 && !NearWorkbench(bridge.World, player)) { Notify("请靠近工作台 2.4 格内并保持视线畅通。", true); return; }
            Send("craft", id);
        }

        bool CanConfigureReactorConsole(WorldState world, PlayerState player) =>
            world != null && world.era >= TechEra.Nuclear && bridge.IsRunning &&
            player != null && player.connected && player.hp > 0f && player.interactCooldown <= 0f;

        void CycleRecipe()
        {
            BuildingState building = InspectedBuilding();
            if (building == null) return;
            if (building.kind == BuildKind.ReactorControlConsole)
            {
                if (building.hp > 0f && CanConfigureReactorConsole(bridge.World, bridge.LocalPlayer))
                    SendInspected("configure", building.setting == 1 ? 0 : 1);
                return;
            }
            if (building.kind == BuildKind.Transformer)
            {
                if (CanSwitchTransformer(bridge.World, building)) SendInspected("recipe", building.setting == 0 ? 1 : 0);
                return;
            }
            var options = new List<RecipeDefinition>();
            foreach (RecipeDefinition recipe in TechCatalog.Recipes)
                if (recipe.building == building.kind && recipe.building != BuildKind.Workbench && PrimitiveCatalog.IsRecipeUnlocked(bridge.World, recipe)) options.Add(recipe);
            if (options.Count == 0) return;
            RecipeDefinition current = MachineRecipe(building);
            if (options.Count == 1 && current != null && current.id == options[0].id) return;
            int index = current == null ? -1 : options.FindIndex(r => r.id == current.id);
            SendInspected("recipe", options[(index + 1) % options.Count].id);
        }

        bool AnyPanelOpen()
        {
            bool open = inventory.gameObject.activeSelf || growth.gameObject.activeSelf ||
                session.gameObject.activeSelf || help.gameObject.activeSelf || technology.gameObject.activeSelf || settingsPanel.gameObject.activeSelf;
            HasAdditionalPanelOpen(ref open);
            if (appearancePanel != null && appearancePanel.gameObject.activeSelf) open = true;
            return open;
        }

        void ClosePanels(bool includeSettings = true)
        {
            inventory.gameObject.SetActive(false);
            if (appearancePanel != null) appearancePanel.gameObject.SetActive(false);
            growth.gameObject.SetActive(false);
            session.gameObject.SetActive(false);
            help.gameObject.SetActive(false);
            technology.gameObject.SetActive(false);
            toolsTray.gameObject.SetActive(false);
            Set(toolsToggle.label, "菜单");
            if (includeSettings)
            {
                if (settingsPanel.gameObject.activeSelf) PlayerPrefs.Save();
                settingsPanel.gameObject.SetActive(false);
            }
            CloseAdditionalPanels();
        }

        void SetGrowth(bool open) => SetPanel(growth, open);

        void SetPanel(RectTransform panel, bool open)
        {
            if (!bridge.IsRunning || bridge.World == null || bridge.LocalPlayer == null || bridge.LocalPlayer.hp <= 0f) open = false;
            bool hadPanel = AnyPanelOpen();
            bool samePanel = panel != null && panel.gameObject.activeSelf;
            ClosePanels();
            toolsTray.gameObject.SetActive(false);
            Set(toolsToggle.label, "菜单");
            if (open && panel != null)
            {
                bridge.BuildSelection = null;
                buildTrayOpen = false;
                if (panel == technology)
                {
                    selectedEra = DefaultTechnologyEra(bridge.World);
                    ResetScroll(technologyScroll);
                }
                panel.gameObject.SetActive(true);
            }
            if (hadPanel && (!open || panel == null || !samePanel)) DemoAudio.UiClose();
            if (open && panel != null && !samePanel) DemoAudio.UiOpen();
            HideInspector();
            guardThroughFrame = Time.frameCount + 1;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            RefreshAll();
        }

        void CancelBuild()
        {
            bridge.BuildSelection = null;
            guardThroughFrame = Time.frameCount + 1;
            RefreshAll();
        }

        void UpgradeSkill(int branch, int tier)
        {
            PlayerState player = bridge.LocalPlayer;
            if (player == null || Count(player.skills, branch) != tier || player.level < 1 + tier * 2 || player.skillPoints <= 0) return;
            Send("skill", branch);
        }

        BuildingState InspectedBuilding()
        {
            if (bridge.World == null || inspectedId < 0 || AnyPanelOpen() || bridge.BuildSelection.HasValue ||
                !machinePanel.gameObject.activeSelf) return null;
            // Resolve against the latest snapshot before sending the cached cell.
            foreach (BuildingState building in bridge.World.buildings)
                if (building.id == inspectedId && DemoCatalog.Contains(building, inspectedX, inspectedY))
                    return building;
            return null;
        }

        void SendInspected(string action, int arg = 0)
        {
            if (InspectedBuilding() == null || bridge.LocalPlayer == null ||
                new Vector2(bridge.LocalPlayer.x - inspectedX, bridge.LocalPlayer.y - inspectedY).sqrMagnitude > 2.4f * 2.4f) return;
            Send(action, arg, inspectedX, inspectedY);
        }

        void Send(string action, int arg = 0, int? x = null, int? y = null)
        {
            if (!bridge.IsRunning || bridge.LocalPlayer == null) return;
            PlayerState player = bridge.LocalPlayer;
            if (player.hp <= 0f && action != "respawn") return;
            Vector2 aim = bridge.MouseWorld - new Vector2(player.x, player.y);
            try
            {
                bridge.Send(new PlayerCommand
                {
                    action = action, arg = arg, x = x ?? Mathf.FloorToInt(player.x + .5f), y = y ?? Mathf.FloorToInt(player.y + .5f),
                    rotation = ((bridge.BuildRotation % 4) + 4) % 4, aimX = aim.x, aimY = aim.y
                });
                refreshAt = 0f;
                if (action == "skill" || action == "attribute") Notify("正在更新角色能力…");
            }
            catch (Exception error) { Notify("操作失败：" + error.Message, true); }
        }

        void CallBridge(Action action)
        {
            localFeedback = "";
            try { action(); }
            catch (Exception error) { Notify("操作失败：" + error.Message, true); }
            guardThroughFrame = Time.frameCount + 1;
            saveCheckAt = 0f;
            RefreshAll();
        }

        void Notify(string message, bool error = false)
        {
            localFeedback = message;
            localFeedbackError = error;
            localFeedbackColor = error ? Danger : Accent;
            feedbackUntil = Time.unscaledTime + 5f;
            refreshAt = 0f;
        }

        bool CurrentLocalFeedback() => !string.IsNullOrEmpty(localFeedback) && Time.unscaledTime < feedbackUntil;

        bool TextInputFocused()
        {
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null) return false;
            TMP_InputField field = eventSystem.currentSelectedGameObject.GetComponentInParent<TMP_InputField>();
            return field != null && field.isFocused;
        }

        bool PointerHitsUi()
        {
            if (raycaster == null || eventSystem == null || Mouse.current == null) return false;
            pointer.Reset();
            pointer.position = Mouse.current.position.ReadValue();
            hits.Clear();
            // Include other canvases, notably the world view's clickable minimap.
            // Raycast now rather than use the input module's previous-frame result.
            eventSystem.RaycastAll(pointer, hits);
            return hits.Count > 0;
        }

        static string MachineFlow(BuildingState building)
        {
            switch (building.kind)
            {
                case BuildKind.Generator: case BuildKind.DieselGenerator: case BuildKind.HighGenerator:
                case BuildKind.CombustionChamber: case BuildKind.StoneFurnace:
                    return "剩余燃烧时间 " + building.fuel.ToString("0.0") + " 秒 · E 补燃料";
                case BuildKind.Boiler: return building.modularBoiler ? "由相邻的燃烧室、取水泵与烟囱提供运行条件" :
                    "一体式锅炉 · 剩余燃烧时间 " + building.fuel.ToString("0.0") + " 秒 · E 补燃料";
                case BuildKind.Miner: return "相邻矿脉 → 对应原矿";
                case BuildKind.SteamMiner: return "蒸汽驱动 · 采集相邻矿床中的资源";
                case BuildKind.Belt: return "物料 → 箭头方向的相邻设备";
                case BuildKind.Turret: return "装入弹药后自动攻击敌人";
                case BuildKind.Chest: return "接收物料 → E 取出库存";
                case BuildKind.PowerPole: case BuildKind.MediumPole: case BuildKind.HighPole: return "同电压连线 7 格 · 覆盖 4 格";
                case BuildKind.SolarPanel: return building.powerGeneration > 0 ? "日照发电 · 夜间停机" : "夜间待机 · 无需燃料";
                case BuildKind.Wall: return "阻挡敌人 · 修理消耗木材";
                case BuildKind.Door: return building.setting == 1 ? "门已打开 · E 关门 · 可通行" : "门已关闭 · E 开门";
                case BuildKind.Chimney: return "为相邻的锅炉机组排烟";
                case BuildKind.FeedwaterPump: return "为相邻的锅炉机组供水 · 须靠近水面";
                case BuildKind.SteamEngine: return "蒸汽输入 → 机械动力 · 沿朝向连接传动轴";
                case BuildKind.Workbench: return "B 打开手工配方 · 靠近后制作";
                case BuildKind.CharcoalKiln: return "木材×4 → 木炭×2 · E 投料 / 取出";
                case BuildKind.ClayKiln: return "黏土×4 + 木炭×1 → 陶砖×4 · E 投料 / 取出";
                case BuildKind.Waterwheel: return "近水驱动 · 接触传动轴输出动力";
                case BuildKind.Shaft: return "接触传递机械动力";
                case BuildKind.SteamPipe: return "接触传递蒸汽动力";
                case BuildKind.Reactor: return "满足启动条件后自动运行 · E 补核燃料 / 冷却液";
                case BuildKind.ReactorControlConsole: return "在设备详情中启用或停用控制台";
                case BuildKind.NuclearWasteVault: return "自动接收核废料 · 无法手动收发";
                default: return "E 投入材料 / 取出产物";
            }
        }

        static string EnergyName(int tier) => tier == 0 ? "机械动力" : tier == 1 ? "蒸汽动力" :
            tier == 2 ? "低压电力" : tier == 3 ? "中压电力" : tier == 4 ? "高压电力" : "无需动力";

        RecipeDefinition MachineRecipe(BuildingState building)
        {
            if (building.kind == BuildKind.Workbench) return null;
            RecipeDefinition recipe = TechCatalog.SelectedRecipe(building);
            return recipe != null && recipe.building == building.kind && PrimitiveCatalog.IsRecipeUnlocked(bridge.World, recipe) ? recipe : null;
        }

        static int MachineRecipeCount(BuildKind kind, WorldState world)
        {
            if (kind == BuildKind.Workbench) return 0;
            int count = 0;
            foreach (RecipeDefinition recipe in TechCatalog.Recipes) if (recipe.building == kind && PrimitiveCatalog.IsRecipeUnlocked(world, recipe)) count++;
            return count;
        }

        static bool NearWorkbench(WorldState world, PlayerState player)
        {
            // Core also checks line of sight; it remains authoritative on command receipt.
            foreach (BuildingState building in world.buildings)
            {
                if (building.kind != BuildKind.Workbench || building.hp <= 0f) continue;
                float dx = player.x - DemoCatalog.CenterX(building), dy = player.y - DemoCatalog.CenterY(building);
                if (dx * dx + dy * dy <= 2.4f * 2.4f) return true;
            }
            return false;
        }

        static bool HasRecipeSpace(int[] items, RecipeDefinition recipe)
        {
            for (int i = 0; i < DemoCatalog.ItemNames.Length; i++)
                if (Count(items, i) - Count(recipe.inputs, i) + Count(recipe.outputs, i) > WorldSimulator.InventoryLimit) return false;
            return true;
        }

        static string MaterialText(int[] materials, int[] owned = null, bool compare = false)
        {
            var result = new StringBuilder(128);
            for (int i = 0; i < DemoCatalog.ItemNames.Length; i++)
            {
                int amount = Count(materials, i);
                if (amount <= 0) continue;
                if (result.Length > 0) result.Append("  ·  ");
                if (compare) result.Append(Count(owned, i) >= amount ? "<color=#A6D9BB>" : "<color=#F48071>");
                result.Append(DemoCatalog.ItemNames[i]).Append(' ');
                if (compare) result.Append(Count(owned, i)).Append('/');
                result.Append(amount);
                if (compare) result.Append("</color>");
            }
            return result.Length == 0 ? "无需材料" : result.ToString();
        }

        static string NextEraGoal(WorldState world, PlayerState player)
        {
            if (world.era == TechEra.Primitive)
            {
                if (PrimitiveCatalog.Enabled(world))
                {
                    string primitive = PrimitiveGoal(world, player);
                    if (!string.IsNullOrEmpty(primitive)) return primitive;
                }
                if (!HasBuilding(world, BuildKind.Workbench)) return "采集木材，建造工作台";
                if (!HasBuilding(world, BuildKind.StoneFurnace)) return "建造石炉，补充燃料并冶炼铁矿";
            }
            else if (world.era == TechEra.Water)
            {
                if (!HasBuilding(world, BuildKind.Waterwheel)) return "在岸边建造水车";
                if (!HasBuilding(world, BuildKind.MechanicalCrusher)) return "用传动轴连接水车与机械粉碎机";
            }
            else if (world.era == TechEra.Steam)
            {
                if (!HasBuilding(world, BuildKind.Boiler)) return "在岸边搭建锅炉机组：燃烧室、锅炉、取水泵与烟囱";
                if (!HasBuilding(world, BuildKind.SteamFurnace)) return "用蒸汽管连接锅炉与蒸汽高炉";
            }
            else if (world.era == TechEra.LowVoltage && !HasBuilding(world, BuildKind.CircuitAssembler)) return "建立低压电网，制造电路";
            else if (world.era == TechEra.MediumVoltage && !HasBuilding(world, BuildKind.ChemicalPlant)) return "接通中压化工厂，制造聚合物";
            else if (world.era == TechEra.HighVoltage)
            {
                if (!PrecisionCatalog.HasResearch(world, PrecisionTech.Grid)) return "按 T 查看高压工艺与高压电网前置";
                if (!HasBuilding(world, BuildKind.PrecisionAssembler)) return "接通高压精密装配机";
                string acceptance = PrecisionCatalog.NuclearRequirement(world);
                if (!string.IsNullOrEmpty(acceptance)) return acceptance;
            }
            else if (world.era == TechEra.Nuclear) return "监控冷却、温度与核废料";
            TechEra next = (TechEra)((int)world.era + 1);
            int[] cost = TechCatalog.ResearchCost(next);
            for (int i = 0; i < cost.Length; i++)
            {
                if (Count(player.items, i) >= cost[i]) continue;
                if (i == (int)ItemKind.Bronze) return "冶炼铜锭与锡锭，在工作台制作青铜";
                if (i == (int)ItemKind.Steel) return "用蒸汽高炉将生铁与石灰石炼成钢";
                return "准备研究材料：" + DemoCatalog.ItemNames[i] + " " + Count(player.items, i) + "/" + cost[i];
            }
            string missing = EraResearchRequirement(world, next);
            return string.IsNullOrEmpty(missing) ? "条件已满足，按 T 研究下一时代" : missing;
        }

        string InventoryText(int[] items)
        {
            text.Clear();
            int count = 0;
            for (int i = 0; i < DemoCatalog.ItemNames.Length; i++)
            {
                if (Count(items, i) <= 0) continue;
                if (count > 0) text.Append(count % 2 == 0 ? "\n" : "  ·  ");
                text.Append(DemoCatalog.ItemNames[i]).Append(' ').Append(Count(items, i));
                count++;
            }
            return count == 0 ? "设备库存：空" : text.ToString();
        }

        static bool HasBuilding(WorldState world, BuildKind kind)
        {
            foreach (BuildingState building in world.buildings)
                if (building.kind == kind && building.hp > 0f) return true;
            return false;
        }

        static int Count(int[] values, int index) => values != null && index >= 0 && index < values.Length ? values[index] : 0;
        static bool CanAfford(int[] items, int[] cost)
        {
            for (int i = 0; i < cost.Length; i++) if (Count(items, i) < cost[i]) return false;
            return true;
        }

        void SetVital(int index, string title, float value, float maximum)
        {
            Vector2 anchor = vitalFills[index].anchorMax;
            anchor.x = Mathf.Clamp01(value / Mathf.Max(1f, maximum));
            vitalFills[index].anchorMax = anchor;
            Set(vitalTexts[index], title + "   " + Mathf.CeilToInt(Mathf.Max(0f, value)) + " / " + Mathf.CeilToInt(maximum));
        }

        void Vital(RectTransform parent, int index, string name, int top, Color color)
        {
            RectTransform track = Box(name + " Track", parent, 12, top, 218, 19, Raised);
            RectTransform fill = Box(name + " Fill", track, 0, 0, 218, 19, color);
            Stretch(fill);
            vitalFills[index] = fill;
            vitalTexts[index] = Label(name + " Value", track, "", 6, 0, 206, 19, 12, Ink);
        }

        RectTransform NewRect(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return item.GetComponent<RectTransform>();
        }

        RectTransform Backdrop(string name, Transform parent, Color color)
        {
            RectTransform rect = Box(name, parent, 0, 0, 1280, 720, color, true);
            Stretch(rect);
            return rect;
        }

        RectTransform Box(string name, Transform parent, float x, float top, float width, float height, Color color, bool block = false)
        {
            RectTransform rect = NewRect(name, parent);
            Place(rect, x, top, width, height);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = block;
            return rect;
        }

        RectTransform ScrollContent(string name, Transform parent, float x, float top, float width, float height,
            out UnityEngine.UI.ScrollRect scroll, bool inventoryGrid = false)
        {
            RectTransform root = Box(name, parent, x, top, width, height, Recessed, true);
            scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 26f;
            RectTransform viewport = Box("Viewport", root, 0, 0, width - 16, height, Color.white, true);
            Stretch(viewport, 0, 0, 16, 0);
            var mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;
            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            if (inventoryGrid)
            {
                var layout = content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
                layout.padding = new RectOffset(6, 6, 6, 6);
                layout.cellSize = new Vector2((width - 16f - 12f - 8f) * .5f, 84);
                layout.spacing = new Vector2(8, 8);
                layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = 2;
                layout.childAlignment = TextAnchor.UpperLeft;
            }
            else
            {
                var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                layout.padding = new RectOffset(6, 6, 6, 6);
                layout.spacing = 8;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            RectTransform track = Box("Scrollbar", root, width - 12, 4, 10, height - 8, Raised, true);
            RectTransform sliding = NewRect("Sliding Area", track);
            Stretch(sliding);
            RectTransform handle = Box("Handle", sliding, 0, 0, 10, 40, Muted, true);
            Stretch(handle);
            var scrollbar = track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            scrollbar.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent;
            return content;
        }

        RectTransform FlowRow(string name, Transform parent, float width, float height)
        {
            RectTransform row = NewRect(name, parent);
            Place(row, 0, 0, width, height);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            return row;
        }

        TextMeshProUGUI FlowLabel(string name, Transform parent, string value, float width, float height, float size, Color color)
        {
            TextMeshProUGUI label = Label(name, parent, value, 0, 0, width, height, size, color);
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            return label;
        }

        static void SetFlowText(TextMeshProUGUI label, string value, float minimum = 24f)
        {
            Set(label, value);
            float width = Mathf.Max(100f, label.rectTransform.rect.width);
            float height = Mathf.Max(minimum, label.GetPreferredValues(value, width, float.PositiveInfinity).y + 8);
            label.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
        }

        static void ResetScroll(UnityEngine.UI.ScrollRect scroll)
        {
            if (scroll == null) return;
            scroll.StopMovement();
            scroll.content.anchoredPosition = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;
        }

        TextMeshProUGUI Label(string name, Transform parent, string value, float x, float top, float width, float height,
            float size, Color color, FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = NewRect(name, parent);
            Place(rect, x, top, width, height);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = DefaultFont;
            label.fontSize = size;
            // CJK face metrics need more vertical room than Latin UI defaults.
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(12f, size * .88f);
            label.fontSizeMax = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            label.raycastTarget = false;
            if (!string.IsNullOrEmpty(value) && DefaultFont != null)
                DefaultFont.TryAddCharacters(value, true);
            label.text = value;
            return label;
        }

        UiButton Button(string name, Transform parent, string value, float x, float top, float width, float height, Action action, bool primary = false)
        {
            RectTransform rect = Box(name, parent, x, top, width, height, primary ? Selected : Raised, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            TextMeshProUGUI label = Label("Label", rect, value, 5, 0, width - 10, height, height <= 36 ? 14 : 17, primary ? Accent : Ink);
            Stretch(label.rectTransform, 5, 0, 5, 0);
            label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(() => { DemoAudio.UiClick(); action(); });
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            var widget = new UiButton { button = button, image = rect.GetComponent<UnityEngine.UI.Image>(), label = label };
            Tint(widget, widget.image.color);
            if (menuPages == null || menuPages.Length == 0 || !rect.IsChildOf(menuPages[0]))
                AddButtonFrame(button);
            return widget;
        }

        UnityEngine.UI.Image MakeHudArt(string name, Transform parent, float x, float top, float size)
        {
            RectTransform rect = NewRect(name, parent);
            Place(rect, x, top, size, size);
            var icon = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;
            return icon;
        }

        static void SetHudArt(UnityEngine.UI.Image icon, string key)
        {
            Sprite sprite = string.IsNullOrEmpty(key) ? null : DemoArt.GetIcon(key);
            if (icon.sprite != sprite) icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        void SetButtonArt(UiButton widget, string key, float size = 32)
        {
            if (widget.icon == null)
            {
                widget.icon = MakeHudArt("Item Icon", widget.button.transform, 10, 0, size);
                RectTransform rect = widget.icon.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
                rect.pivot = new Vector2(0, .5f);
                rect.anchoredPosition = new Vector2(10, 0);
            }
            SetHudArt(widget.icon, key);
            Vector2 min = widget.label.rectTransform.offsetMin;
            Vector2 max = widget.label.rectTransform.offsetMax;
            min.x = widget.icon.enabled ? size + 20 : 10;
            max.x = -8;
            widget.label.rectTransform.offsetMin = min;
            widget.label.rectTransform.offsetMax = max;
        }

        TMP_InputField Input(string name, Transform parent, string value, string placeholder, float x, float top, float width, float height, int limit)
        {
            RectTransform rect = Box(name, parent, x, top, width, height, Recessed, true);
            TMP_InputField field = rect.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            RectTransform viewport = NewRect("Text Area", rect);
            Stretch(viewport, 10, 4, 10, 4);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            TextMeshProUGUI content = Label("Text", viewport, "", 0, 0, width - 20, height - 8, 18, Ink);
            Stretch(content.rectTransform);
            content.richText = false;
            content.textWrappingMode = TextWrappingModes.NoWrap;
            content.overflowMode = TextOverflowModes.Overflow;
            TextMeshProUGUI hint = Label("Placeholder", viewport, placeholder, 0, 0, width - 20, height - 8, 18, Muted);
            Stretch(hint.rectTransform);
            hint.richText = false;
            field.textViewport = viewport;
            field.textComponent = content;
            field.placeholder = hint;
            field.fontAsset = DefaultFont;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = limit;
            field.customCaretColor = true;
            field.caretColor = Accent;
            field.selectionColor = new Color(Accent.r, Accent.g, Accent.b, .25f);
            field.text = value ?? "";
            return field;
        }

        static void Tint(UiButton widget, Color normal)
        {
            var colors = widget.button.colors;
            if (colors.normalColor == normal) return;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Accent, .20f);
            colors.pressedColor = Color.Lerp(normal, Accent, .35f);
            colors.selectedColor = Color.Lerp(normal, Accent, .24f);
            colors.disabledColor = Color.Lerp(normal, Recessed, .48f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .08f;
            widget.image.color = Color.white;
            widget.button.colors = colors;
        }

        static void Place(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        static void Center(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }

        static void TopRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void TopCenter(RectTransform rect, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void BottomCenter(RectTransform rect, float bottom, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, bottom);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void Set(TMP_Text label, string value) { if (label.text != value) label.text = value; }
        static string Clean(string value) => (value ?? "").Replace('<', '‹').Replace('>', '›');
        static Color StatusColor(string value)
        {
            if (string.IsNullOrEmpty(value)) return Muted;
            return value.Contains("失败") || value.Contains("超时") || value.Contains("断开") || value.Contains("无法") || value.Contains("尚未") ? Danger : Ink;
        }

        void OnEnable() { if (canvas != null) canvas.enabled = true; }
        void OnDisable() { if (canvas != null) canvas.enabled = false; }
        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
            // DefaultFont remains shared with the world view until play ends.
        }
    }
}
