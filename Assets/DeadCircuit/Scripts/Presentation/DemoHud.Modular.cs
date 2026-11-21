using System.Text;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        TextMeshProUGUI modularMachineState;
        static readonly BuildKind[] BlueprintBuildings = { BuildKind.LargeSteamFurnace, BuildKind.LargeAssemblyPlant, BuildKind.LargeOreProcessor };
        RectTransform blueprintMenu, blueprintBuildingMenu, blueprintBuildingChoices, blueprintModuleChoices, blueprintChoiceControls;
        readonly UiButton[] blueprintBuildingButtons = new UiButton[3];
        UiButton openBlueprintBuildings;
        UnityEngine.UI.ScrollRect blueprintChoiceScroll, modularPaletteScroll;
        UiButton blueprintScrollLeft, blueprintScrollRight;
        TextMeshProUGUI blueprintChoicePosition;
        bool blueprintBuildingsOpen, blueprintScrollDragging;
        RectTransform modularPalette, modularPaletteBody, modularContinueRow, modularUnloadControls;
        TextMeshProUGUI modularPaletteTitle, modularPaletteSummary, modularPaletteCost, modularPaletteIssue, modularPaletteHelp;
        readonly UiButton[] modularRoleButtons = new UiButton[8];
        UiButton modularPaletteFold, modularNewBlueprint, modularErase, modularContinue, modularUnload;
        TextMeshProUGUI modularUnloadHint;
        bool modularPaletteCollapsed;

        // Read-only QA hooks reflect the visible roots, including parent HUD visibility.
        public bool BlueprintBuildingsMenuOpen => blueprintBuildingMenu != null && blueprintBuildingMenu.gameObject.activeInHierarchy;
        public bool BlueprintModulesMenuOpen => modularPalette != null && modularPalette.gameObject.activeInHierarchy;
        public bool BlueprintModulesMenuCollapsed => BlueprintModulesMenuOpen && modularPaletteCollapsed;
        public BuildKind? BlueprintMenuBuilding => BlueprintModulesMenuOpen ? bridge.BuildSelection : null;
        public UnityEngine.UI.ScrollRect BlueprintChoiceScroll => blueprintChoiceScroll;

        void MakeBlueprintBuildingMenu()
        {
            // Survival HUD occupies y=134..238 in every status; reserve a 14px gap below it.
            openBlueprintBuildings = Button("Open Blueprint Buildings", game, "建筑蓝图", 18, 252, 202, 36, OpenBlueprintBuildings, true);
            SetButtonArt(openBlueprintBuildings, "ui_create", 22);
            openBlueprintBuildings.button.gameObject.SetActive(false);
            // One persistent outer panel and selection viewport serve BOTH stages.
            blueprintMenu = Box("Blueprint Menu", game, 0, 184, 286, 454, HudSurface, true);
            TopRight(blueprintMenu, 18, 184, 286, 454);
            PanelRule(blueprintMenu, 286, 40);
            AddPanelFrame(blueprintMenu);
            blueprintBuildingMenu = NewRect("Blueprint Building Menu", blueprintMenu);
            Stretch(blueprintBuildingMenu);
            SetHudArt(MakeHudArt("Blueprint Buildings Icon", blueprintBuildingMenu, 12, 11, 20), "ui_create");
            Label("Blueprint Buildings Title", blueprintBuildingMenu, "建筑蓝图", 40, 8, 168, 26, 18, Accent, FontStyles.Bold);
            Button("Close Blueprint Buildings", blueprintBuildingMenu, "关闭", 230, 8, 44, 26, CancelBuild);
            Label("Blueprint Buildings Step", blueprintBuildingMenu, "01 选择建筑  >  02 选择模块", 12, 48, 262, 24, 14, Accent);
            Label("Blueprint Buildings Hint", blueprintBuildingMenu, "左右滑动选建筑，再选择内部模块。", 12, 74, 262, 24, 13, Muted);
            MakeBlueprintChoiceScroll();
            blueprintBuildingChoices = MakeBlueprintChoiceContent("Blueprint Building Choices", BlueprintBuildings.Length, 220);
            for (int i = 0; i < BlueprintBuildings.Length; i++)
            {
                BuildKind kind = BlueprintBuildings[i];
                UiButton choice = Button("Blueprint Building " + kind, blueprintBuildingChoices, "", 6 + i * 228, 6, 220, 84, () => SelectBlueprintBuilding(kind));
                SetButtonArt(choice, "ui_create", 28);
                choice.label.alignment = TextAlignmentOptions.MidlineLeft;
                choice.label.fontSize = choice.label.fontSizeMax = 15;
                choice.label.fontSizeMin = 12;
                blueprintBuildingButtons[i] = choice;
            }
            Label("Blueprint Buildings Details Heading", blueprintBuildingMenu, "建筑说明 · 上下滚动", 12, 262, 262, 20, 12, Muted);
            RectTransform details = ScrollContent("Blueprint Buildings Details Scroll View", blueprintBuildingMenu, 8, 286, 270, 156, out var detailsScroll);
            GuardBlueprintScrollDrag(detailsScroll);
            SetFlowText(FlowLabel("Blueprint Buildings Footer", details, "", 242, 120, 13, Ink),
                "先选建筑，再逐格规划模块。未解锁建筑会显示研究条件。\n\n画蓝图免费；先放控制器，再补其他模块。退出绘制后用 E 逐格投料。\n\n返回或关闭只停止绘图，已放蓝图保留。");
            blueprintBuildingMenu.gameObject.SetActive(false);
        }

        void MakeBlueprintChoiceScroll()
        {
            RectTransform root = Box("Blueprint Choice Scroll View", blueprintMenu, 8, 120, 270, 96, Recessed, true);
            blueprintChoiceScroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            blueprintChoiceScroll.horizontal = true;
            blueprintChoiceScroll.vertical = false;
            blueprintChoiceScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            blueprintChoiceScroll.scrollSensitivity = 26f;
            RectTransform viewport = Box("Viewport", root, 0, 0, 270, 96, Color.white, true);
            Stretch(viewport);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            blueprintChoiceScroll.viewport = viewport;
            blueprintChoiceScroll.onValueChanged.AddListener(_ => RefreshBlueprintChoiceNavigation());
            GuardBlueprintScrollDrag(blueprintChoiceScroll);
            blueprintChoiceControls = NewRect("Blueprint Choice Navigation", blueprintMenu);
            Place(blueprintChoiceControls, 0, 222, 286, 28);
            blueprintScrollLeft = Button("Blueprint Scroll Left", blueprintChoiceControls, "←", 8, 0, 36, 28, () => ScrollBlueprintChoices(-1));
            blueprintScrollRight = Button("Blueprint Scroll Right", blueprintChoiceControls, "→", 242, 0, 36, 28, () => ScrollBlueprintChoices(1));
            blueprintChoicePosition = Label("Blueprint Scroll Position", blueprintChoiceControls, "左右拖动", 48, 0, 190, 28, 12, Muted);
            blueprintChoicePosition.alignment = TextAlignmentOptions.Center;
        }

        RectTransform MakeBlueprintChoiceContent(string name, int count, float cardWidth)
        {
            RectTransform content = NewRect(name, blueprintChoiceScroll.viewport);
            Place(content, 0, 0, count * cardWidth + (count - 1) * 8 + 12, 96);
            content.gameObject.SetActive(false);
            return content;
        }

        void GuardBlueprintScrollDrag(UnityEngine.UI.ScrollRect scroll)
        {
            // Keep a UI drag captured even when the pointer leaves the panel over the world.
            var trigger = scroll.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var begin = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.BeginDrag };
            begin.callback.AddListener(_ => blueprintScrollDragging = true);
            trigger.triggers.Add(begin);
            var end = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.EndDrag };
            end.callback.AddListener(_ => { blueprintScrollDragging = false; GuardBlueprintUi(); });
            trigger.triggers.Add(end);
        }

        void ResetBlueprintChoiceScroll()
        {
            if (blueprintChoiceScroll == null || blueprintChoiceScroll.content == null) return;
            blueprintChoiceScroll.StopMovement();
            blueprintChoiceScroll.content.anchoredPosition = Vector2.zero;
            blueprintChoiceScroll.horizontalNormalizedPosition = 0f;
        }

        void SetBlueprintChoiceStage(bool buildings, bool modules)
        {
            bool expanded = buildings || modules && !modularPaletteCollapsed;
            blueprintMenu.gameObject.SetActive(buildings || modules);
            blueprintBuildingMenu.gameObject.SetActive(buildings);
            modularPalette.gameObject.SetActive(modules);
            blueprintBuildingChoices.gameObject.SetActive(buildings);
            blueprintModuleChoices.gameObject.SetActive(modules);
            blueprintChoiceScroll.gameObject.SetActive(expanded);
            blueprintChoiceControls.gameObject.SetActive(expanded);
            if (!expanded && blueprintScrollDragging)
            {
                blueprintScrollDragging = false;
                GuardBlueprintUi();
            }
            RectTransform next = buildings ? blueprintBuildingChoices : blueprintModuleChoices;
            if ((buildings || modules) && blueprintChoiceScroll.content != next)
            {
                blueprintChoiceScroll.StopMovement();
                blueprintChoiceScroll.content = next;
                ResetBlueprintChoiceScroll();
            }
            // Only the explicit fold action changes the height; both stages use 286 x 454.
            TopRight(blueprintMenu, 18, 184, 286, modules && modularPaletteCollapsed ? 120 : 454);
            RefreshBlueprintChoiceNavigation();
        }

        void RefreshBlueprintChoiceNavigation()
        {
            if (blueprintChoiceScroll == null || blueprintChoiceScroll.content == null || blueprintChoicePosition == null) return;
            float maximum = Mathf.Max(0f, blueprintChoiceScroll.content.rect.width - blueprintChoiceScroll.viewport.rect.width);
            float offset = Mathf.Clamp(-blueprintChoiceScroll.content.anchoredPosition.x, 0f, maximum);
            float step = blueprintBuildingsOpen ? 228f : 240f;
            int pages = Mathf.CeilToInt(maximum / step) + 1;
            int page = Mathf.Clamp(Mathf.RoundToInt(offset / step), 0, pages - 1);
            Set(blueprintChoicePosition, (blueprintBuildingsOpen ? "建筑 " : "模块 ") + (page + 1) + "/" + pages + " 页 · 左右拖动");
            blueprintScrollLeft.button.interactable = offset > .5f;
            blueprintScrollRight.button.interactable = offset < maximum - .5f;
        }

        void ScrollBlueprintChoices(int direction)
        {
            if (!CanUseBlueprintMenus() || AnyPanelOpen() || blueprintChoiceScroll.content == null) return;
            float maximum = Mathf.Max(0f, blueprintChoiceScroll.content.rect.width - blueprintChoiceScroll.viewport.rect.width);
            float step = blueprintBuildingsOpen ? 228f : 240f;
            float offset = Mathf.Clamp(-blueprintChoiceScroll.content.anchoredPosition.x, 0f, maximum);
            int page = Mathf.RoundToInt(offset / step);
            int lastPage = Mathf.CeilToInt(maximum / step);
            float target = Mathf.Min(maximum, Mathf.Clamp(page + direction, 0, lastPage) * step);
            blueprintChoiceScroll.StopMovement();
            blueprintChoiceScroll.horizontalNormalizedPosition = maximum > 0f ? target / maximum : 0f;
            GuardBlueprintUi();
            RefreshBlueprintChoiceNavigation();
        }

        bool CanUseBlueprintMenus() => bridge != null && bridge.IsRunning && bridge.World != null && bridge.LocalPlayer != null &&
            bridge.LocalPlayer.connected && bridge.LocalPlayer.hp > 0f && ModularMachineCatalog.HasWrench(bridge.LocalPlayer);

        void GuardBlueprintUi()
        {
            // A page can disappear on pointer-up; consume the bridge's next input read too.
            guardThroughFrame = Time.frameCount + 1;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
        }

        void OpenBlueprintBuildings()
        {
            if (!CanUseBlueprintMenus() || AnyPanelOpen()) return;
            bridge.CancelPlayerControl();
            bridge.BuildSelection = null;
            bridge.ModularEraseMode = false;
            blueprintBuildingsOpen = true;
            buildTrayOpen = false;
            modularPaletteCollapsed = false;
            toolsTray.gameObject.SetActive(false);
            Set(toolsToggle.label, "菜单");
            HideInspector();
            ResetBlueprintChoiceScroll();
            GuardBlueprintUi();
            RefreshAll();
        }

        void SelectBlueprintBuilding(BuildKind kind)
        {
            if (!BlueprintBuildingsMenuOpen || !CanUseBlueprintMenus() || !TechCatalog.IsUnlocked(bridge.World, kind)) return;
            SelectBuild((int)kind);
        }

        void ShowModularModulePage()
        {
            blueprintBuildingsOpen = false;
            modularPaletteCollapsed = false;
            ResetBlueprintChoiceScroll();
            ResetScroll(modularPaletteScroll);
            GuardBlueprintUi();
        }

        void CloseBlueprintMenus()
        {
            blueprintBuildingsOpen = false;
            blueprintScrollDragging = false;
            modularPaletteCollapsed = false;
            if (blueprintMenu != null) blueprintMenu.gameObject.SetActive(false);
            if (blueprintChoiceScroll != null) blueprintChoiceScroll.StopMovement();
            if (blueprintBuildingMenu != null) blueprintBuildingMenu.gameObject.SetActive(false);
            if (modularPalette != null) modularPalette.gameObject.SetActive(false);
            if (openBlueprintBuildings != null) openBlueprintBuildings.button.gameObject.SetActive(false);
        }

        void MakeModularPalette()
        {
            MakeBlueprintBuildingMenu();
            // Stages have no surface or frame of their own; the shared viewport stays put.
            modularPalette = NewRect("Modular Blueprint Palette", blueprintMenu);
            Stretch(modularPalette);
            Label("Modular Palette Heading", modularPalette, "02 选择模块", 12, 8, 164, 26, 17, Accent, FontStyles.Bold);
            modularPaletteFold = Button("Fold Modular Palette", modularPalette, "收起", 178, 8, 44, 26, () =>
            {
                modularPaletteCollapsed = !modularPaletteCollapsed;
                GuardBlueprintUi();
                RefreshAll();
            });
            Button("Exit Modular Blueprint", modularPalette, "退出", 230, 8, 44, 26, CancelBuild);
            modularPaletteTitle = Label("Modular Palette Building", modularPalette, "", 12, 44, 262, 34, 13, Accent);
            UiButton back = Button("Blueprint Back To Buildings", modularPalette, "返回建筑", 8, 82, 270, 30, OpenBlueprintBuildings);
            SetButtonArt(back, "ui_back", 18);
            modularPaletteBody = NewRect("Modular Palette Body", modularPalette);
            Place(modularPaletteBody, 0, 258, 286, 196);
            blueprintModuleChoices = MakeBlueprintChoiceContent("Blueprint Module Choices", modularRoleButtons.Length, 112);
            string[] roleArt = { "ui_settings", "ui_create", "ui_craft", "ui_inventory", "ui_back", "ui_technology", "ui_repair", "ui_refresh" };
            for (int index = 0; index < modularRoleButtons.Length; index++)
            {
                MachineModuleRole role = ModularMachinePresentation.PaletteRoles[index];
                UiButton choice = Button("Modular Role " + role, blueprintModuleChoices, "", 6 + index * 120, 6, 112, 84, () => SelectModularPaletteRole(role));
                SetHudArt(MakeHudArt("Module Icon", choice.button.transform, 40, 10, 32), roleArt[index]);
                Place(choice.label.rectTransform, 4, 54, 104, 24);
                choice.label.fontSize = choice.label.fontSizeMax = 13;
                choice.label.fontSizeMin = 12;
                choice.label.textWrappingMode = TextWrappingModes.NoWrap;
                modularRoleButtons[index] = choice;
            }
            Label("Modular Details Scroll Hint", modularPaletteBody, "材料与说明 · 上下滚动", 12, 38, 262, 18, 12, Muted);
            RectTransform content = ScrollContent("Modular Palette Scroll View", modularPaletteBody, 8, 60, 270, 60, out modularPaletteScroll);
            GuardBlueprintScrollDrag(modularPaletteScroll);
            modularPaletteSummary = FlowLabel("Modular Palette Requirements", content, "", 246, 40, 13, Ink);
            modularPaletteCost = FlowLabel("Modular Selected Role Cost", content, "", 246, 64, 13, Ink);
            modularPaletteIssue = Label("Modular Formation Issue", modularPaletteBody, "", 12, 0, 262, 34, 12, Amber);
            modularPaletteHelp = FlowLabel("Modular Drawing Help", content, "", 246, 88, 12, Muted);
            modularNewBlueprint = Button("New Modular Blueprint", modularPaletteBody, "新蓝图", 8, 126, 130, 28, () =>
            {
                if (!CanUseModularPalette()) return;
                bridge.SelectModularBuilding(bridge.BuildSelection.Value);
                ShowModularModulePage();
                RefreshAll();
            });
            modularErase = Button("Toggle Modular Erase", modularPaletteBody, "擦图：关", 146, 126, 132, 28, () =>
            {
                if (!CanUseModularPalette() || !ModularMachinePresentation.CanErase(bridge.World, bridge.ActiveModularBlueprint)) return;
                bridge.ModularEraseMode = !bridge.ModularEraseMode;
                GuardBlueprintUi();
                RefreshAll();
            });
            Button("Finish Modular Drawing", modularPaletteBody, "退出绘制 · E 逐格投料", 8, 160, 270, 28, CancelBuild);
            modularPalette.gameObject.SetActive(false);
            blueprintMenu.gameObject.SetActive(false);
        }

        bool CanUseModularPalette() => CanUseBlueprintMenus() &&
            !AnyPanelOpen() && bridge.BuildSelection.HasValue && ModularMachineCatalog.IsModular(bridge.BuildSelection.Value) &&
            ModularMachineCatalog.HasWrench(bridge.LocalPlayer);

        void SelectModularPaletteRole(MachineModuleRole role)
        {
            if (!CanUseModularPalette()) return;
            BuildingState active = bridge.ActiveModularBlueprint;
            if (active != null && !ModularMachinePresentation.CanErase(bridge.World, active)) return;
            if (active == null && role != MachineModuleRole.Controller || active != null && role == MachineModuleRole.Controller) return;
            if (active != null && role != MachineModuleRole.Casing && ModularMachinePresentation.RoleCount(active, role) > 0) return;
            bridge.SelectModularRole(role);
            bridge.ModularEraseMode = false;
            GuardBlueprintUi();
            RefreshAll();
        }

        void RefreshModularPalette(WorldState world, PlayerState player)
        {
            bool selected = bridge.BuildSelection.HasValue && ModularMachineCatalog.IsModular(bridge.BuildSelection.Value);
            bool wasOpen = blueprintBuildingsOpen || modularPalette.gameObject.activeSelf;
            bool eligible = CanUseBlueprintMenus();
            if (!eligible)
            {
                if (selected) bridge.BuildSelection = null;
                bridge.ModularEraseMode = false;
                CloseBlueprintMenus();
                if ((selected || wasOpen) && bridge.IsRunning && player.hp > 0f && !ModularMachineCatalog.HasWrench(player))
                    Notify("绘制已停止：请手持耐久正常的扳手。已画蓝图保留。", true);
                return;
            }
            if (bridge.BuildSelection.HasValue) blueprintBuildingsOpen = false;
            bool unobstructed = !AnyPanelOpen();
            bool showBuildings = blueprintBuildingsOpen && unobstructed;
            bool show = selected && unobstructed && !showBuildings;
            SetBlueprintChoiceStage(showBuildings, show);
            openBlueprintBuildings.button.gameObject.SetActive(unobstructed && !showBuildings && !selected);
            openBlueprintBuildings.button.interactable = eligible;
            if (showBuildings)
            {
                // Browsing is presentation only: no selection, command or blueprint is created.
                bridge.BuildSelection = null;
                selected = false;
                for (int i = 0; i < BlueprintBuildings.Length; i++)
                {
                    BuildKind option = BlueprintBuildings[i];
                    bool unlocked = TechCatalog.IsUnlocked(world, option);
                    string requirement = world.era < TechCatalog.RequiredEra(option) ?
                        "需" + TechCatalog.EraName(TechCatalog.RequiredEra(option)) : PrimitiveCatalog.BuildRequirement(world, option);
                    if (!unlocked && string.IsNullOrEmpty(requirement)) requirement = SteamCatalog.BuildRequirement(world, option);
                    Set(blueprintBuildingButtons[i].label, DemoCatalog.BuildName(option) +
                        "\n<size=12>" + (unlocked ? "已解锁" : "未解锁") + " · " + EnergyName(TechCatalog.EnergyTier(option)) +
                        "\n" + (unlocked ? "选择模块  >" : requirement) + "</size>");
                    blueprintBuildingButtons[i].button.interactable = unlocked;
                    blueprintBuildingButtons[i].label.color = unlocked ? Ink : Muted;
                }
            }
            if (!show) return;
            modularPaletteBody.gameObject.SetActive(!modularPaletteCollapsed);
            Set(modularPaletteFold.label, modularPaletteCollapsed ? "展开" : "收起");
            BuildKind kind = bridge.BuildSelection.Value;
            BuildingState active = bridge.ActiveModularBlueprint;
            if (active != null && bridge.ModularRoleSelection == MachineModuleRole.Controller && !bridge.ModularEraseMode)
                bridge.SelectModularRole(MachineModuleRole.Casing);
            Set(modularPaletteTitle, "建筑蓝图 > " + DemoCatalog.BuildName(kind) + " > 模块");
            int count = active == null ? 0 : ModularMachineCatalog.ModuleCount(active);
            bool editable = active == null || ModularMachinePresentation.CanErase(world, active);
            SetFlowText(modularPaletteSummary, (active == null ? "新蓝图 · 先放控制器，等待房主确认。" :
                "蓝图 #" + active.id + " · " + count + " / 32 格 · " + DemoCatalog.Width(active) + "×" + DemoCatalog.Height(active)) +
                "\n七种职责各 1，机壳至少 2；四邻连通。");
            for (int i = 0; i < modularRoleButtons.Length; i++)
            {
                var role = ModularMachinePresentation.PaletteRoles[i];
                int placed = ModularMachinePresentation.RoleCount(active, role);
                bool available = editable && (active == null ? role == MachineModuleRole.Controller :
                    role != MachineModuleRole.Controller && count < ModularMachineCatalog.MaxModules && (role == MachineModuleRole.Casing || placed == 0));
                bool chosen = !bridge.ModularEraseMode && bridge.ModularRoleSelection == role;
                string roleTitle = role == MachineModuleRole.Process && kind == BuildKind.LargeSteamFurnace ? "炉腔" : ModularMachinePresentation.RoleTitle(kind, role);
                Set(modularRoleButtons[i].label, (chosen ? "● " : "") + roleTitle + " " + placed + (role == MachineModuleRole.Casing ? "/2+" : "/1"));
                modularRoleButtons[i].button.interactable = available;
                Tint(modularRoleButtons[i], chosen ? Selected : Raised);
                modularRoleButtons[i].label.color = chosen ? Accent : available ? Ink : Muted;
            }
            SetFlowText(modularPaletteCost, bridge.ModularEraseMode ? "擦图模式：左键擦除当前格模块。已付建材按服务器规则返还。" :
                "选中：" + ModularMachinePresentation.RoleTitle(kind, bridge.ModularRoleSelection) + " · " + ModularOrientation(bridge.ModularRoleSelection, bridge.BuildRotation) +
                "\n本格建设费：" + MaterialText(ModularMachineCatalog.ModuleCost(kind, bridge.ModularRoleSelection)) + "\n画蓝图免费；退出后逐格分批投料。" +
                (active == null ? "" : "\n已规划总量（不预付）：" + MaterialText(ModularMachinePresentation.PlannedCost(active))));
            string issue = active == null ? "尚未建立蓝图：只预览一个控制器格。" : ModularMachineCatalog.FormationIssue(active);
            Set(modularPaletteIssue, !editable ? "绘制锁定：先清空库存、在途与生产进度。" :
                active == null ? "先放控制器 · 画蓝图不预付建材" :
                "形成检查：" + (string.IsNullOrEmpty(issue) ? "职责齐全，结构已连通" : issue) +
                "\n已规划 " + count + " / 32 格 · 已建 " + ModularMachinePresentation.CompletedModules(active) + " 格");
            SetFlowText(modularPaletteHelp, "左键逐格画当前模块，R 只旋转当前模块接口。先画完形状，再退出用 E 对准各格投料。" +
                "\n输入 / 能源箭头朝内接收，输出 / 排气箭头朝外；外侧接口格不可被本机占用。" +
                "\n擦图需库存、在途、加工进度均为空；多人可继续画同一机器，房主重检。" +
                (active == null ? "" : "\n形成检查：" + (string.IsNullOrEmpty(issue) ? "通过" : issue)));
            bool eraseAllowed = active != null && ModularMachinePresentation.CanErase(world, active);
            modularErase.button.interactable = eraseAllowed;
            if (!eraseAllowed) bridge.ModularEraseMode = false;
            Set(modularErase.label, bridge.ModularEraseMode ? "擦图：开" : "擦图：关");
            Tint(modularErase, bridge.ModularEraseMode ? Selected : Raised);
            modularNewBlueprint.button.interactable = ModularMachineCatalog.HasWrench(player);
        }

        void MakeModularMachineControls(RectTransform content)
        {
            modularMachineState = FlowLabel("Modular Machine Construction", content, "", 260, 120, 13, Ink);
            modularUnloadControls = PrecisionColumn("Modular Controller Actions", content, 260);
            modularUnloadHint = FlowLabel("Modular Unload Explanation", modularUnloadControls, "", 260, 88, 12, Muted);
            RectTransform unloadRow = FlowRow("Modular Unload Row", modularUnloadControls, 260, 38);
            modularUnload = Button("Unload Modular Machine", unloadRow, "停机清空 / 准备续画", 0, 0, 254, 34, SendModularUnload);
            modularUnloadControls.gameObject.SetActive(false);
            modularContinueRow = FlowRow("Continue Modular Blueprint Row", content, 260, 38);
            modularContinue = Button("Continue Modular Blueprint", modularContinueRow, "继续画图 / 增补机壳", 0, 0, 254, 34, ContinueModularBlueprint);
            modularContinueRow.gameObject.SetActive(false);
            modularMachineState.gameObject.SetActive(false);
        }

        void RefreshModularBuild(PlayerState player, BuildKind kind)
        {
            SetFlowText(buildTitle, DemoCatalog.BuildName(kind) + " · " + (bridge.ModularEraseMode ? "擦除单格" :
                ModularMachinePresentation.RoleTitle(kind, bridge.ModularRoleSelection) + " · " + ModularOrientation(bridge.ModularRoleSelection, bridge.BuildRotation)) + " · R 旋转本模块");
            SetFlowText(buildCost, "本格建设费：" + MaterialText(ModularMachineCatalog.ModuleCost(kind, bridge.ModularRoleSelection)) +
                " · 左键画图 · 右键退出后 E 投料");
            buildHelp.gameObject.SetActive(false);
        }

        void ContinueModularBlueprint()
        {
            BuildingState building = InspectedBuilding();
            if (building == null || !ModularMachineCatalog.IsModular(building.kind)) return;
            if (!ModularMachineCatalog.HasWrench(bridge.LocalPlayer))
            { Notify("继续画图需要装备可用扳手并激活工具槽。", true); return; }
            if (!ModularMachinePresentation.CanErase(bridge.World, building))
            { Notify("继续画图前须清空整机库存、在途物品和生产进度。", true); return; }
            bridge.EditModularBlueprint(building);
            ShowModularModulePage();
            guardThroughFrame = Time.frameCount + 1;
            HideInspector();
            RefreshAll();
        }

        bool ModularUnloadReady(WorldState world, PlayerState player, BuildingState building, out string reason)
        {
            reason = "";
            if (!bridge.IsRunning || world == null || player == null || building == null || !ModularMachineCatalog.IsModular(building.kind))
                reason = "设备尚未就绪。";
            else if (!player.connected || player.hp <= 0f || player.interactCooldown > 0f) reason = "等待角色与操作冷却就绪。";
            else if (!ModularMachineCatalog.HasWrench(player)) reason = "需装备可用扳手并激活工具槽。";
            else if (!TechCatalog.IsUnlocked(world, building.kind)) reason = "需先解锁该大型机械研究。";
            else if (ModularMachineCatalog.Role(building, ModularMachineCatalog.ModuleAt(building, inspectedX, inspectedY)) != MachineModuleRole.Controller)
                reason = "请对准控制器格操作。";
            else if (!ModularMachinePresentation.InReach(world, player, inspectedX, inspectedY, building)) reason = "请靠近控制器 2.4 格内并保持视线畅通。";
            else if (world.cargo != null && world.cargo.Exists(c => c.fromId == building.id || c.toId == building.id)) reason = "尚有在途物品，等待全部到达后再操作。";
            if (reason.Length > 0) return false;
            bool stocked = false;
            if (!InventoryWeight.CanFit(player, InventoryWeight.MaterialsGrams(building.items)))
            { reason = "背包共享载重不足，须能一次接收全部库存（上限 120 kg）。"; return false; }
            for (int item = 0; item < building.items.Length; item++)
                stocked |= building.items[item] > 0;
            if (!stocked && building.progress <= 0f)
            { reason = "库存与进度已清空，可直接继续画图。"; return false; }
            return true;
        }

        void SendModularUnload()
        {
            BuildingState building = InspectedBuilding();
            if (!ModularUnloadReady(bridge.World, bridge.LocalPlayer, building, out string reason))
            { Notify(reason, true); return; }
            bridge.Send(new PlayerCommand { action = "moduleunload", buildingId = building.id, x = inspectedX, y = inspectedY });
            Notify("已发送停机清空请求，等待房主确认后可继续画图。");
            refreshAt = 0f;
        }

        // Called after the standard recipe/energy inspector: finished machines keep those panels.
        void RefreshModularInspector(WorldState world, PlayerState player, BuildingState building)
        {
            bool modular = building != null && ModularMachineCatalog.IsModular(building.kind);
            modularMachineState.gameObject.SetActive(modular);
            modularContinueRow.gameObject.SetActive(modular);
            modularUnloadControls.gameObject.SetActive(false);
            if (!modular) return;
            modularContinue.button.interactable = player.connected && player.hp > 0f && ModularMachineCatalog.HasWrench(player) &&
                ModularMachinePresentation.CanErase(world, building);
            int module = ModularMachineCatalog.ModuleAt(building, inspectedX, inspectedY);
            if (module < 0) return;
            bool complete = ModularMachineCatalog.IsComplete(building);
            bool cellComplete = ModularMachineCatalog.ModuleComplete(building, module);
            var role = ModularMachineCatalog.Role(building, module);
            if (role == MachineModuleRole.Controller)
            {
                modularUnloadControls.gameObject.SetActive(true);
                bool unloadReady = ModularUnloadReady(world, player, building, out string unloadReason);
                modularUnload.button.interactable = unloadReady;
                SetFlowText(modularUnloadHint, "取消当前加工并取回全部原料、成品与维护料；在途到达后再操作。" +
                    "只损耗本批已花时间，已用维护不退。\n" + (unloadReady ? "可停机清空，之后继续画图。" : unloadReason), 88);
            }
            bool wrench = ModularMachineCatalog.HasWrench(player);
            bool ready = bridge.IsRunning && player.connected && player.hp > 0f && player.interactCooldown <= 0f &&
                ModularMachinePresentation.InReach(world, player, inspectedX, inspectedY, building) && MapExploration.IsVisible(world, player, inspectedX, inspectedY);
            int[] remaining = ModularMachinePresentation.Remaining(building, module);
            ItemKind maintenance = ModularMachineCatalog.MaintenanceItem(building.kind);
            int finished = ModularMachinePresentation.CompletedModules(building);
            var info = new StringBuilder(512);
            info.Append("当前格 #").Append(module + 1).Append(" · ").Append(ModularMachineCatalog.RoleName(building, module))
                .Append("  ").Append(CellLocationText(world, inspectedX, inspectedY))
                .Append(cellComplete ? "\n■ 已建成" : "\n＋ 待建 / 分批投入")
                .Append(" · 已建 / 已规划 ").Append(finished).Append('/').Append(ModularMachineCatalog.ModuleCount(building)).Append(" 格")
                .Append("\n形成检查：").Append(string.IsNullOrEmpty(ModularMachineCatalog.FormationIssue(building)) ? "结构连通、职责齐全" : ModularMachineCatalog.FormationIssue(building));
            if (!cellComplete)
                info.Append("\n本格尚缺：").Append(MaterialText(remaining, player.items, true))
                    .Append("\n数字为背包 / 尚缺；每次只投入背包中已有的本格建材。");
            else info.Append("\n本格尚缺：无");
            info.Append("\n全机尚缺：").Append(complete ? "无" : MaterialText(ModularMachineCatalog.RequiredItems(building)))
                .Append("\n维护：剩余 ").Append(building.modularService).Append(" 批 · 库存 ")
                .Append(DemoCatalog.ItemName(maintenance)).Append('×').Append(Count(building.items, (int)maintenance))
                .Append("\n状态：").Append(ModularMachineCatalog.Status(world, building));
            if (building.kind == BuildKind.LargeSteamFurnace && ModularMachinePresentation.FindRole(building, MachineModuleRole.Exhaust) >= 0)
                info.Append("\n排气外格：").Append(CellLocationText(world, ModularMachineCatalog.ExhaustX(building), ModularMachineCatalog.ExhaustY(building)))
                    .Append(ModularMachineCatalog.ExhaustClear(world, building) ? " · 净空正常，保持畅通" : " · 阻塞，移走外格障碍 / 设备后才能运行");
            else if (building.kind != BuildKind.LargeSteamFurnace) info.Append("\n通风舱为被动通风，不模拟热工排气压力。");
            if (complete)
            {
                info.Append("\n动力：").Append(EnergyName(TechCatalog.EnergyTier(building.kind)))
                    .Append(" · 供能 ").Append((building.powerRatio * 100f).ToString("0")).Append('%');
                info.Append("\n能源箭头外格：").Append(CellLocationText(world, ModularMachineCatalog.EnergyInputX(building), ModularMachineCatalog.EnergyInputY(building)))
                    .Append(building.kind == BuildKind.LargeSteamFurnace ? " · 须放匹配蒸汽管" : " · 须放匹配电杆")
                    .Append("；电杆范围覆盖不能替代此格。");
                RecipeDefinition recipe = MachineRecipe(building);
                if (building.networkId <= 0) info.Append("\n能源未接通：匹配蒸汽管 / 电杆须在能源箭头外格。");
                else if (building.powerDemand > 0 && building.powerRatio < .99f) info.Append("\n供能不足：检查电网 / 蒸汽供给与负载。");
                if (recipe != null && !ModularMachinePresentation.OutputHasSpace(world, building, recipe))
                    info.Append("\n输出堵塞：下一批产物将超过设备库存上限，请从输出总线取出。");
                if (recipe != null)
                {
                    var missing = new int[DemoCatalog.ItemCount];
                    for (int i = 0; i < missing.Length; i++) missing[i] = Mathf.Max(0, Count(recipe.inputs, i) - Count(building.items, i));
                    info.Append("\n本批原料尚缺：").Append(MaterialText(missing));
                }
            }
            SetFlowText(modularMachineState, info.ToString(), 120);
            modularMachineState.color = complete ? Ink : Amber;
            SetFlowText(machineHint, (ready ? "操作范围与视线就绪。" : "请靠近当前格 2.4 格内，保持视线畅通并等待操作冷却。") +
                "\n" + (!wrench ? "施工 / 维护需手持耐久 > 0 的扳手（装备并激活工具槽）。\n" : "") + ModularRoleHint(role, complete, cellComplete));
            bool hasMaterial = false;
            for (int i = 0; i < remaining.Length; i++) if (remaining[i] > 0 && Count(player.items, i) > 0) { hasMaterial = true; break; }
            bool damaged = building.hp < WorldSimulator.BuildingMaxHealth(building.kind);
            bool productionPort = complete && ModularPortHasWork(world, player, building, role);
            interactButton.button.interactable = ready && (!complete ? !cellComplete && wrench && hasMaterial :
                productionPort && (role != MachineModuleRole.Maintenance || wrench));
            Set(interactButton.label, !complete ? (cellComplete ? "本格已建" : "本格施工") :
                role == MachineModuleRole.Input ? "投入原料" : role == MachineModuleRole.Output ? "取出产物" :
                role == MachineModuleRole.Maintenance ? damaged ? "结构修复" : "补维护料" : "非物料口");
            repairButton.button.interactable = ready && wrench && damaged && Count(player.items, (int)ItemKind.SteelPlate) > 0;
            dismantleButton.button.interactable = ready && wrench && ModularRefundFits(world, player, building);
            if (damaged) SetFlowText(machineHint, machineHint.text + "\n结构受损：手持扳手，用钢板×1 修复整机；损坏时停机。");
            if (!ModularMachinePresentation.CanErase(world, building)) SetFlowText(machineHint, machineHint.text + "\n继续画图前须清空库存、在途和生产进度。");
            if (!ModularRefundFits(world, player, building)) SetFlowText(machineHint, machineHint.text + "\n背包空间不足，暂不能完整收回建材和库存。");
            if (!complete)
            {
                machineTitle.color = Amber;
                SetFlowText(machineInfo, "蓝图 / 分格施工 · 耐久 " + Mathf.CeilToInt(building.hp) + " / " +
                    WorldSimulator.BuildingMaxHealth(building.kind).ToString("0") + "\n各职责齐全、四邻连通、接口朝外且全部格付清后才供能、物流与生产。");
                SetFlowText(machineContents, "施工建材单独计入各格，不是生产库存。");
                machineRecipe.gameObject.SetActive(false);
                machineRecipeFlow.gameObject.SetActive(false);
                recipeButton.button.gameObject.SetActive(false);
            }
        }

        bool ModularPortHasWork(WorldState world, PlayerState player, BuildingState building, MachineModuleRole role)
        {
            RecipeDefinition recipe = MachineRecipe(building);
            if (role == MachineModuleRole.Maintenance)
            {
                if (building.hp < WorldSimulator.BuildingMaxHealth(building.kind)) return Count(player.items, (int)ItemKind.SteelPlate) > 0;
                int item = (int)ModularMachineCatalog.MaintenanceItem(building.kind);
                return Count(player.items, item) > 0 && Count(building.items, item) + ModularMachinePresentation.Incoming(world, building, item) < WorldSimulator.BufferLimit;
            }
            if (role == MachineModuleRole.Input && recipe != null)
                for (int item = 0; item < DemoCatalog.ItemCount; item++)
                    if (Count(recipe.inputs, item) > 0 && Count(player.items, item) > 0 &&
                        Count(building.items, item) + ModularMachinePresentation.Incoming(world, building, item) < WorldSimulator.BufferLimit) return true;
            if (role == MachineModuleRole.Output)
                foreach (var option in TechCatalog.Recipes)
                {
                    if (option.building != building.kind) continue;
                    for (int item = 0; item < DemoCatalog.ItemCount; item++)
                        if (Count(option.outputs, item) > 0 && (recipe == null || Count(recipe.inputs, item) == 0) &&
                            Count(building.items, item) > 0 && InventoryWeight.Allowance(player, InventoryWeight.Grams((ItemKind)item)) > 0) return true;
                }
            return false;
        }

        static bool ModularRefundFits(WorldState world, PlayerState player, BuildingState building)
        {
            var refund = (int[])building.items.Clone();
            if (building.modularModules != null) foreach (var module in building.modularModules)
                if (module?.paid != null) for (int item = 0; item < Mathf.Min(refund.Length, module.paid.Length); item++) refund[item] += module.paid[item];
            if (world.cargo != null) foreach (var cargo in world.cargo) if (cargo.fromId == building.id || cargo.toId == building.id) refund[cargo.item]++;
            return InventoryWeight.CanFit(player, InventoryWeight.MaterialsGrams(refund));
        }

        static string ModularRoleHint(MachineModuleRole role, bool complete, bool cellComplete)
        {
            if (!complete) return cellComplete ? "此格已建好；其他＋格可继续施工。若缺职责 / 断连 / 接口朝内，请继续画图补齐或擦除后重画。未成形不自动物流。" :
                "左键 / E 或「本格施工」只补当前格建材，可分批投入。施工不会投入生产原料。";
            switch (role)
            {
                case MachineModuleRole.Input: return "输入总线：左键 / E 投入当前配方原料；从此格外侧接入物流。";
                case MachineModuleRole.Output: return "输出总线：左键 / E 取出产物；单出口，最多 1 件在途，沿此格箭头输出物流。";
                case MachineModuleRole.Maintenance: return "维护舱：左键 / E 优先修复受损结构（钢板×1），完好时手动补维护耗材；需可用扳手，每份支持 8 批。输入总线仅按配方收料，同名维护料若为配方原料除外。";
                case MachineModuleRole.Energy: return "能源舱：匹配蒸汽管 / 电杆放在能源箭头外格，电杆覆盖范围不能替代；不接收原料。";
                case MachineModuleRole.Controller: return "控制器：可显式停机清空后继续画图；查看或切换配方。生产原料请投输入总线。";
                case MachineModuleRole.Process: return "工艺核心：显示加工状态；生产原料请投输入总线。";
                case MachineModuleRole.Exhaust: return "排气 / 通风舱：不是生产投料口。蒸汽炉的排气箭头外格须保持净空；电气机器为被动通风。";
                default: return "机壳：结构模块，不是输入口；请对准输入、输出或维护舱操作。";
            }
        }

        static string ModularOrientation(MachineModuleRole role, int rotation)
        {
            int side = (rotation + (role == MachineModuleRole.Energy ? 2 : 0)) & 3;
            return (role == MachineModuleRole.Input || role == MachineModuleRole.Energy ? "入口外格 " :
                role == MachineModuleRole.Output || role == MachineModuleRole.Exhaust ? "出口外格 " : "朝向 ") + Directions[side];
        }
    }
}
