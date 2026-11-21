#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DeadCircuit
{
    public sealed partial class DemoRuntimeChecks
    {
        // Opt-in memory fixture: actual Input System -> bridge -> simulator -> HUD.
        // No normal archive, PlayerPrefs or network host is opened.
        IEnumerator CheckModularMachines(GameBridge bridge)
        {
            bridge.enabled = false;
            var state = new WorldState
            {
                seed = 73149, ground = new string('4', 4096), biomes = new string('0', 4096),
                generation = new WorldGenerationSettings { seed = 73149, chunked = false },
                era = TechEra.LowVoltage, nextWaveAt = 10000, steamResearch = 255, lvResearch = 255,
                steamCoked = 6, steamRefractory = 8, steamPigIron = 6, steamSteel = 4,
                steamPlates = 4, steamFittings = 4, steamDriven = 4,
                lvWire = 12, lvInsulators = 8, lvCable = 8, lvCoils = 4, lvMotors = 2,
                lvRelays = 4, lvCircuits = 6, lvElectric = 8, lvAutomated = 4, lvSorted = 4, lvProtected = 4
            };
            var sim = new WorldSimulator(state);
            var player = sim.AddPlayer("modular-qa", "模块检查");
            Array.Clear(player.items, 0, player.items.Length);
            player.gear.Clear(); player.toolId = player.weaponId = 0;
            Array.Clear(player.armorIds, 0, player.armorIds.Length);
            player.x = 25; player.y = 29;
            SetQaField(bridge, "profileId", player.id);
            SetQaField(bridge, "simulator", sim);
            SetQaField(bridge, "<World>k__BackingField", sim.State);
            SetQaField(bridge, "<IsHost>k__BackingField", true);
            SetQaField(bridge, "<IsRunning>k__BackingField", true);
            // Foreground OS devices must not replace the scripted current mouse mid-sample.
            // This changes only this opt-in Player process, never desktop device settings.
            var physicalDevices = InputSystem.devices.Where(device => device is Keyboard || device is Mouse).ToArray();
            foreach (var device in physicalDevices) InputSystem.DisableDevice(device);
            var keyboard = InputSystem.AddDevice<Keyboard>("Modular QA Keyboard");
            qaMouse = InputSystem.AddDevice<Mouse>("Modular QA Mouse");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var read = typeof(GameBridge).GetMethod("ReadInputDevices", flags);
            var clock = typeof(GameBridge).GetField("inputClock", flags);
            var hud = bridge.GetComponent<DemoHud>();
            var refreshHud = typeof(DemoHud).GetMethod("RefreshAll", flags);
            yield return null; yield return null; yield return null;
            var view = bridge.GetComponent<DemoWorldView>();
            var renderView = typeof(DemoWorldView).GetMethod("LateUpdate", flags);
            // The coroutine samples after a yield; freeze automatic camera motion between
            // screen projection and bridge consumption, then run the unchanged view explicitly.
            // Normal gameplay already reads input before LateUpdate in the same frame.
            view.enabled = false;
            // OnDisable hides the view; keep its render roots active while sampling manually.
            typeof(DemoWorldView).GetMethod("SetViewEnabled", flags).Invoke(view, new object[] { true });

            IEnumerator Sample(float x, float y, Key[] keys = null, bool left = false, bool right = false, int ticks = 1, float scrollY = 0f)
            {
                for (int n = 0; n < ticks; n++)
                {
                    renderView.Invoke(view, null);
                    var camera = view.WorldCamera;
                    Vector3 point = camera.WorldToScreenPoint(new Vector3(x, y, 0));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys ?? Array.Empty<Key>()));
                    InputSystem.QueueStateEvent(qaMouse, new MouseState { position = new Vector2(point.x, point.y),
                        scroll = new Vector2(0, scrollY),
                        buttons = (ushort)((left ? 1 : 0) | (right ? 2 : 0)) });
                    yield return null;
                    clock.SetValue(bridge, .05f);
                    if (left && n == 0)
                        Debug.Log("DEMO_MODULAR_QA_INPUT target=" + x + "," + y + " actual=" + bridge.MouseWorld +
                            " mouse=" + Mouse.current?.name + " pressed=" + Mouse.current?.leftButton.wasPressedThisFrame +
                            " blocked=" + hud.CapturesInput + " role=" + bridge.ModularRoleSelection +
                            " blueprint=" + bridge.ModularBlueprintId + " cooldown=" + player.interactCooldown);
                    read.Invoke(bridge, new object[] { true });
                    sim.Step(.05f);
                    renderView.Invoke(view, null);
                }
            }

            IEnumerator DragBlueprintChoices(bool buildings)
            {
                var scroll = hud.BlueprintChoiceScroll;
                Canvas.ForceUpdateCanvases();
                Require(scroll != null && scroll.horizontal && !scroll.vertical &&
                    scroll.content.rect.width > scroll.viewport.rect.width + 1, "both levels use a horizontally overflowing choice strip");
                scroll.StopMovement(); scroll.horizontalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases(); yield return null;
                int planned = sim.State.buildings.Count;
                var corners = new Vector3[4]; scroll.viewport.GetWorldCorners(corners);
                Vector3 middle = (corners[0] + corners[2]) * .5f;
                Vector3 a = middle + Vector3.right * (corners[2].x - corners[0].x) * .22f;
                Vector3 b = middle - Vector3.right * (corners[2].x - corners[0].x) * .22f;
                Vector3 ChoiceWorld(Vector3 point) => view.WorldCamera.ScreenToWorldPoint(new Vector3(point.x, point.y, -view.WorldCamera.transform.position.z));
                var target = ChoiceWorld(a); yield return Sample(target.x, target.y, left: true);
                for (int step = 1; step <= 8; step++)
                { target = ChoiceWorld(Vector3.Lerp(a, b, step / 8f)); yield return Sample(target.x, target.y, left: true); }
                target = ChoiceWorld(b); yield return Sample(target.x, target.y);
                float afterLeft = scroll.horizontalNormalizedPosition;
                Require(afterLeft > .02f, "dragging choices left reveals later cards");
                scroll.StopMovement();
                yield return Sample(target.x, target.y, left: true);
                for (int step = 1; step <= 8; step++)
                { target = ChoiceWorld(Vector3.Lerp(b, a, step / 8f)); yield return Sample(target.x, target.y, left: true); }
                target = ChoiceWorld(a); yield return Sample(target.x, target.y);
                Require(scroll.horizontalNormalizedPosition < afterLeft - .01f, "dragging choices right returns toward earlier cards");
                Require(sim.State.buildings.Count == planned && (buildings ? hud.BlueprintBuildingsMenuOpen && !bridge.BuildSelection.HasValue : hud.BlueprintModulesMenuOpen),
                    "sliding cards does not select a building or paint the world");
                scroll.StopMovement(); scroll.horizontalNormalizedPosition = 0; refreshHud.Invoke(hud, null);
                Click("Blueprint Scroll Right"); refreshHud.Invoke(hud, null);
                float afterArrow = scroll.horizontalNormalizedPosition;
                Require(afterArrow > .01f, "right navigation button advances the shared strip");
                Click("Blueprint Scroll Left"); refreshHud.Invoke(hud, null);
                Require(scroll.horizontalNormalizedPosition < afterArrow, "left navigation button reverses the shared strip");
                scroll.StopMovement(); scroll.horizontalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            }

            // Deliberately asymmetric 3x4 design, with two real holes and an extra casing.
            // The player chooses every cell and port direction; no whole-machine stencil is used.
            var layout = new[] {
                (MachineModuleRole.Casing, 1, 0, 0), (MachineModuleRole.Input, -1, 0, 0),
                (MachineModuleRole.Process, 0, 1, 0), (MachineModuleRole.Output, 1, 1, 0),
                (MachineModuleRole.Energy, 0, -1, 1), (MachineModuleRole.Maintenance, -1, 1, 0),
                (MachineModuleRole.Exhaust, 1, 2, 1), (MachineModuleRole.Casing, 0, 2, 0),
                (MachineModuleRole.Casing, -1, 2, 0)
            };
            var kinds = new[] { BuildKind.LargeSteamFurnace, BuildKind.LargeAssemblyPlant, BuildKind.LargeOreProcessor };
            void StandBeside(int x, int y)
            {
                foreach (var offset in new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up,
                    Vector2.left * 2, Vector2.right * 2, Vector2.down * 2, Vector2.up * 2 })
                {
                    int px = x + (int)offset.x, py = y + (int)offset.y;
                    if (sim.IsBlocked(px, py)) continue;
                    player.x = px; player.y = py; return;
                }
                throw new InvalidOperationException("No exterior QA approach to module.");
            }
            for (int machine = 0; machine < kinds.Length; machine++)
            {
                int x = 27 + machine * 7, y = 29;
                StandBeside(x, y);
                if (machine == 0)
                {
                    bridge.Send(new PlayerCommand { action = "build", arg = (int)kinds[machine], x = x, y = y });
                    Require(sim.State.buildings.Count == 0, "authority rejects controller without held wrench");
                    for (int tick = 0; tick < 8; tick++) sim.Step(.05f);
                    var wrench = new GearState { id = sim.State.nextId++, kind = GearKind.Wrench, durability = 100 };
                    player.gear.Add(wrench);
                    bridge.SelectModularBuilding(kinds[machine]);
                    Require(!bridge.BuildSelection.HasValue, "carried but unequipped wrench does not enter module drawing");
                    bridge.Send(new PlayerCommand { action = "equip", arg = wrench.id });
                    bridge.Send(new PlayerCommand { action = "switch", arg = 1 });
                    Require(ModularMachineCatalog.HasWrench(player), "equipped active tool slot holds wrench");
                }
                yield return Sample(x, y, ticks: 8);
                refreshHud.Invoke(hud, null);
                Click("Open Blueprint Buildings");
                refreshHud.Invoke(hud, null);
                Require(Active("Blueprint Building Menu") && !Active("Modular Blueprint Palette"),
                    "blueprint entry opens only level one building choices");
                Require(!bridge.BuildSelection.HasValue, "level one does not activate a world placement brush");
                var sharedMenu = Rect("Blueprint Menu").GetEntityId();
                RequireBlueprintPanelFits("Blueprint Menu");
                RequireBlueprintEntryDoesNotOverlap();
                if (machine == 0)
                {
                    float menuX = player.x, menuY = player.y;
                    int plannedBeforeMenu = sim.State.buildings.Count;
                    yield return Sample(x, y, new[] { Key.D }, left: true, ticks: 4);
                    Require(player.x == menuX && player.y == menuY && sim.State.buildings.Count == plannedBeforeMenu,
                        "level one consumes movement and world clicks while choosing a building");
                    yield return Sample(x, y);
                    yield return DragBlueprintChoices(true);
                    yield return Capture("blueprint-building-menu");
                    Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                    yield return new WaitForSecondsRealtime(.4f);
                    Canvas.ForceUpdateCanvases(); refreshHud.Invoke(hud, null);
                    RequireBlueprintPanelFits("Blueprint Menu");
                    RequireBlueprintEntryDoesNotOverlap();
                    yield return Capture("blueprint-building-menu-1280");
                    Screen.SetResolution(1440, 900, FullScreenMode.Windowed);
                    yield return new WaitForSecondsRealtime(.4f);
                }
                EnsureBlueprintChoiceVisible("Blueprint Building " + kinds[machine]);
                Click("Blueprint Building " + kinds[machine]);
                yield return Sample(x, y, ticks: 8);
                refreshHud.Invoke(hud, null);
                Require(Active("Modular Blueprint Palette") && !Active("Blueprint Building Menu"),
                    "choosing building replaces level one with its module page");
                Require(Rect("Blueprint Menu").GetEntityId().Equals(sharedMenu), "building selection updates level two in the same existing UI instance");
                RequireBlueprintPanelFits("Blueprint Menu");
                if (machine == 0)
                {
                    yield return DragBlueprintChoices(false);
                    Click("Fold Modular Palette");
                    refreshHud.Invoke(hud, null);
                    Require(hud.BlueprintModulesMenuCollapsed && !hud.BlueprintChoiceScroll.gameObject.activeInHierarchy,
                        "second level folds its module body without changing the selected building");
                    Click("Fold Modular Palette");
                    refreshHud.Invoke(hud, null);
                    Require(!hud.BlueprintModulesMenuCollapsed && hud.BlueprintChoiceScroll.gameObject.activeInHierarchy, "module page expands again");
                    Click("Blueprint Back To Buildings");
                    refreshHud.Invoke(hud, null);
                    Require(Active("Blueprint Building Menu") && !bridge.BuildSelection.HasValue && sim.State.buildings.Count == 0,
                        "back returns to building choices without placing or deleting anything");
                    Require(Rect("Blueprint Menu").GetEntityId().Equals(sharedMenu), "return keeps the same fixed blueprint window");
                    EnsureBlueprintChoiceVisible("Blueprint Building " + kinds[machine]);
                    Click("Blueprint Building " + kinds[machine]);
                    yield return Sample(x, y, ticks: 8);
                    refreshHud.Invoke(hud, null);
                    Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                    yield return new WaitForSecondsRealtime(.4f);
                    Canvas.ForceUpdateCanvases(); refreshHud.Invoke(hud, null);
                    RequireBlueprintPanelFits("Blueprint Menu");
                    RequireBlueprintBottomCardFits();
                    yield return Capture("blueprint-module-menu-1280");
                    var details = (UnityEngine.UI.ScrollRect)QaField(hud, "modularPaletteScroll");
                    var detailCorners = new Vector3[4]; details.viewport.GetWorldCorners(detailCorners);
                    Vector3 screenMiddle = (detailCorners[0] + detailCorners[2]) * .5f;
                    Vector3 worldMiddle = view.WorldCamera.ScreenToWorldPoint(new Vector3(screenMiddle.x, screenMiddle.y, -view.WorldCamera.transform.position.z));
                    float scrollBefore = details.verticalNormalizedPosition;
                    yield return Sample(worldMiddle.x, worldMiddle.y, ticks: 3, scrollY: -120);
                    Require(details.verticalNormalizedPosition < scrollBefore - .01f, "module detail viewport responds to actual scripted mouse wheel");
                    details.verticalNormalizedPosition = 0;
                    Canvas.ForceUpdateCanvases();
                    Rect detailBounds = RelativeBounds(details.viewport, details.content);
                    Require(Mathf.Abs(detailBounds.yMin - details.viewport.rect.yMin) < 1f, "scroll reaches the end of the module explanation without permanent cropping");
                    yield return Capture("blueprint-module-details-1280");
                    details.verticalNormalizedPosition = 1;
                    Screen.SetResolution(1440, 900, FullScreenMode.Windowed);
                    yield return new WaitForSecondsRealtime(.4f);
                }
                Require(FindButton("Modular Role Controller").interactable && !FindButton("Modular Role Casing").interactable,
                    "new drawing palette requests its controller first");
                int[] before = (int[])player.items.Clone();
                yield return Sample(x, y, left: true);
                yield return Sample(x, y, ticks: 8);
                var building = bridge.ActiveModularBlueprint;
                Require(building != null, "controller creates editable blueprint group: " + player.notice);
                Require(ModularMachineCatalog.ModuleX(building, 0) == x && ModularMachineCatalog.ModuleY(building, 0) == y,
                    "bridge projects the controller onto the requested world cell");
                Require(ModularMachineCatalog.ModuleCount(building) == 1 &&
                    ModularMachineCatalog.Role(building, 0) == MachineModuleRole.Controller,
                    "controller places exactly one module, never a fixed 3x3 template");
                Require(player.items.SequenceEqual(before), "drawing controller does not prepay materials");
                Require(!sim.IsBlocked(x + 1, y), "undrawn neighboring cell remains free");
                if (machine == 0) yield return Capture("modular-controller-and-palette");
                for (int slot = 0; slot < layout.Length; slot++)
                {
                    var cell = layout[slot]; int mx = x + cell.Item2, my = y + cell.Item3;
                    StandBeside(mx, my);
                    refreshHud.Invoke(hud, null);
                    EnsureBlueprintChoiceVisible("Modular Role " + cell.Item1);
                    Click("Modular Role " + cell.Item1); bridge.BuildRotation = cell.Item4;
                    Require(bridge.ModularRoleSelection == cell.Item1, "module palette selects requested role");
                    yield return Sample(mx, my, ticks: 8);
                    if (machine == 0 && cell.Item1 == MachineModuleRole.Energy)
                    {
                        bridge.BuildRotation = 0;
                        yield return Sample(mx, my, new[] { Key.R });
                        Require(bridge.BuildRotation == 1, "R rotates individual energy module");
                        yield return Sample(mx, my);
                    }
                    if (cell.Item1 == MachineModuleRole.Input || cell.Item1 == MachineModuleRole.Output ||
                        cell.Item1 == MachineModuleRole.Energy || cell.Item1 == MachineModuleRole.Exhaust)
                    {
                        var preview = GameObject.Find("Modular Blueprint Preview");
                        Require(preview != null, "selected port has a single-cell world preview");
                        var previewFrame = preview.transform.Find("Modular Machine Group Frame").GetComponent<LineRenderer>();
                        Require(!previewFrame.enabled, "cursor preview does not draw a second whole-building frame");
                        var socket = preview.transform.Find("Modular External Port Cell");
                        int outward = (cell.Item4 + (cell.Item1 == MachineModuleRole.Input || cell.Item1 == MachineModuleRole.Energy ? 2 : 0)) & 3;
                        Require(socket != null && Mathf.Abs(socket.position.x - mx - DemoCatalog.DirectionX(outward)) < .001f &&
                            Mathf.Abs(socket.position.y - my - DemoCatalog.DirectionY(outward)) < .001f,
                            "port preview marks the same external cell as authority: " + cell.Item1);
                        var arrow = preview.transform.Find("Modular Cell Preview " + cell.Item1 + "/Module Port Arrow");
                        Require(arrow != null && Mathf.Abs(Mathf.DeltaAngle(arrow.eulerAngles.z, cell.Item4 * 90)) < .001f,
                            "port flow arrow matches its independently selected direction");
                    }
                    yield return Sample(mx, my, left: true);
                    yield return Sample(mx, my, ticks: 8);
                    int index = ModularMachineCatalog.ModuleAt(building, mx, my);
                    Require(index >= 0 && ModularMachineCatalog.Role(building, index) == cell.Item1 &&
                        ModularMachineCatalog.ModuleRotation(building, index) == cell.Item4,
                        "mouse draws freely chosen module and direction: " + cell.Item1 + " " + player.notice);
                    Require(ModularMachineCatalog.ModuleCount(building) == slot + 2, "each click adds just one selected module");
                }
                Require(ModularMachineCatalog.ModuleCount(building) == 10, "free layout permits extra casing beyond nine modules");
                RequireModularVisualCells(building);
                RequireBlueprintBottomCardFits();
                Require(!DemoCatalog.Contains(building, x + 1, y - 1) && !sim.IsBlocked(x + 1, y - 1),
                    "hole inside module bounding box stays unreserved and traversable");
                if (machine == 0)
                {
                    bridge.ModularEraseMode = true;
                    StandBeside(x - 1, y + 2);
                    yield return Sample(x - 1, y + 2, ticks: 8);
                    yield return Sample(x - 1, y + 2, left: true);
                    yield return Sample(x - 1, y + 2, ticks: 8);
                    Require(ModularMachineCatalog.ModuleAt(building, x - 1, y + 2) < 0, "erase mode removes just the clicked blueprint cell");
                    RequireModularVisualCells(building);
                    bridge.SelectModularRole(MachineModuleRole.Casing);
                    yield return Sample(x - 1, y + 2, left: true);
                    yield return Sample(x - 1, y + 2, ticks: 8);
                    Require(ModularMachineCatalog.ModuleCount(building) == 10, "redrawing a cell keeps same blueprint group");
                    yield return Capture("modular-free-layout-preview");
                }
                yield return Sample(x, y, right: true);
                Require(!bridge.BuildSelection.HasValue, "right click exits drawing and retains the blueprint");
                yield return Sample(x, y, ticks: 8);
                var required = ModularMachineCatalog.RequiredItems(building);
                for (int item = 0; item < required.Length; item++) player.items[item] += required[item];
                for (int module = 0; module < ModularMachineCatalog.ModuleCount(building); module++)
                {
                    int mx = ModularMachineCatalog.ModuleX(building, module), my = ModularMachineCatalog.ModuleY(building, module);
                    StandBeside(mx, my);
                    yield return Sample(mx, my, ticks: 8);
                    yield return Sample(mx, my, new[] { Key.E });
                    yield return Sample(mx, my, ticks: 8);
                    Require(ModularMachineCatalog.ModuleComplete(building, module),
                        "E builds the independently drawn cell: " + kinds[machine] + "/" + module + " " + player.notice);
                    if (machine == 0 && module == 3) yield return Capture("modular-partial-construction");
                }
                Require(ModularMachineCatalog.IsComplete(building), "free connected layout forms machine: " +
                    ModularMachineCatalog.FormationIssue(building));
                RequireModularVisualCells(building);
                Require(ModularMachineCatalog.RequiredItems(building).All(n => n == 0), "formed machine has no unpaid materials");
                Require(!building.powered && building.progress == 0, "formed but disconnected machine does not run");
            }
            yield return SetQaInspector(true);
            player.x = 34; player.y = 27;
            yield return Sample(34, 29, ticks: 10);
            yield return Capture("modular-complete-inspector");
            float originalZoom = view.WorldCamera.orthographicSize;
            foreach (float zoom in new[] { 16f, 6f })
            {
                view.WorldCamera.orthographicSize = zoom; renderView.Invoke(view, null);
                yield return null;
                RequireModularVisualCells(sim.State.buildings[1]);
                if (zoom == 16f) yield return Capture("blueprint-building-frames-zoom16");
            }
            view.WorldCamera.orthographicSize = originalZoom; renderView.Invoke(view, null);
            var existingBlueprint = sim.State.buildings[1];
            refreshHud.Invoke(hud, null);
            Click("Continue Modular Blueprint");
            refreshHud.Invoke(hud, null);
            Require(hud.BlueprintModulesMenuOpen && bridge.ModularBlueprintId == existingBlueprint.id,
                "inspector continues an existing machine directly in its second-level module page");
            Click("Blueprint Back To Buildings");
            refreshHud.Invoke(hud, null);
            Require(hud.BlueprintBuildingsMenuOpen && sim.State.buildings.Count == 3 &&
                ModularMachineCatalog.IsComplete(existingBlueprint), "back preserves all existing paid blueprint modules");
            Click("Close Blueprint Buildings");
            refreshHud.Invoke(hud, null);
            Require(!hud.BlueprintBuildingsMenuOpen && !hud.BlueprintModulesMenuOpen && !bridge.BuildSelection.HasValue,
                "closing building menu leaves both levels and the world brush inactive");
            var snapshot = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(sim.State));
            var restored = new WorldSimulator(snapshot);
            Require(restored.State.buildings.Count == 3 && restored.State.buildings.TrueForAll(ModularMachineCatalog.IsComplete),
                "Unity JSON and validated restore retain all three freely drawn machines");
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(qaMouse);
            foreach (var device in physicalDevices) if (device.added) InputSystem.EnableDevice(device);
            SetQaField(bridge, "<IsRunning>k__BackingField", false);
            SetQaField(bridge, "<IsHost>k__BackingField", false);
            Debug.Log("DEMO_MODULAR_QA_PASS held_wrench same_panel_hierarchy horizontal_drag_both_levels navigation_buttons menu_back responsive_ui connected_modules group_frame free_layout sparse_holes extra_casing individual_rotation erase per_cell_e three_machines json_restore");
        }

        static void EnsureBlueprintChoiceVisible(string buttonName)
        {
            var scroll = GameBridge.Instance.GetComponent<DemoHud>().BlueprintChoiceScroll;
            Canvas.ForceUpdateCanvases();
            var button = FindButton(buttonName).GetComponent<RectTransform>();
            Require(button.IsChildOf(scroll.content), "choice belongs to the active shared strip: " + buttonName);
            Rect bounds = RelativeBounds(scroll.content, button);
            float overflow = scroll.content.rect.width - scroll.viewport.rect.width;
            scroll.StopMovement();
            scroll.horizontalNormalizedPosition = overflow <= 0 ? 0 : Mathf.Clamp01((bounds.center.x - scroll.content.rect.xMin - scroll.viewport.rect.width * .5f) / overflow);
            Canvas.ForceUpdateCanvases();
            Rect visible = RelativeBounds(scroll.viewport, button);
            Require(visible.center.x >= scroll.viewport.rect.xMin - 1 && visible.center.x <= scroll.viewport.rect.xMax + 1,
                "choice can be reached inside the horizontal viewport: " + buttonName);
        }

        static void RequireBlueprintPanelFits(string name)
        {
            var panel = Rect(name);
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            Require(corners.All(point => point.x >= -1 && point.y >= -1 && point.x <= Screen.width + 1 && point.y <= Screen.height + 1),
                "blueprint panel stays within the active window: " + name);
        }

        static void RequireBlueprintEntryDoesNotOverlap()
        {
            var entry = Rect("Open Blueprint Buildings");
            var canvas = entry.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            Rect bounds = RelativeBounds(canvas, entry);
            Require(!bounds.Overlaps(RelativeBounds(canvas, Rect("Vitals"))) &&
                !bounds.Overlaps(RelativeBounds(canvas, Rect("Survival HUD"))),
                "blueprint entry has its own space below existing survival cards");
        }

        static void RequireBlueprintBottomCardFits()
        {
            Canvas.ForceUpdateCanvases();
            var scroll = Rect("Build Details Scroll View").GetComponent<UnityEngine.UI.ScrollRect>();
            Require(scroll != null && scroll.content.rect.height <= scroll.viewport.rect.height + 1f,
                "compact current-module cost and controls fit the bottom card without partial lines");
        }

        static void RequireModularVisualCells(BuildingState building)
        {
            var owner = GameObject.Find("Modular Machine " + building.id);
            Require(owner != null && owner.activeInHierarchy, "modular world render root is actually visible");
            var view = GameBridge.Instance.GetComponent<DemoWorldView>();
            var ownerVisual = ((IDictionary)QaField(view, "buildings"))[building.id];
            var visual = QaField(ownerVisual, "modules");
            var cells = (IList)QaField(visual, "cells");
            var frame = (LineRenderer)QaField(visual, "frame");
            Require(frame != null && frame.enabled && frame.loop && frame.positionCount == 4 && frame.gameObject.activeInHierarchy,
                "machine uses one active closed group rectangle");
            float worldPixel = 2f * view.WorldCamera.orthographicSize / Mathf.Max(1, view.WorldCamera.pixelHeight);
            Require(frame.startWidth >= worldPixel && frame.startColor.a >= .5f, "group outline remains at least one visible pixel across camera zoom");
            float minX = building.modularModules.Min(module => module.x) - .5f;
            float maxX = building.modularModules.Max(module => module.x) + .5f;
            float minY = building.modularModules.Min(module => module.y) - .5f;
            float maxY = building.modularModules.Max(module => module.y) + .5f;
            var points = Enumerable.Range(0, frame.positionCount).Select(index =>
                frame.useWorldSpace ? frame.GetPosition(index) : frame.transform.TransformPoint(frame.GetPosition(index))).ToArray();
            Require(points.Min(point => point.x) < minX && points.Max(point => point.x) > maxX &&
                points.Min(point => point.y) < minY && points.Max(point => point.y) > maxY,
                "group frame encloses all actual modules with external padding");
            Require(owner.GetComponentsInChildren<LineRenderer>().Count(line => line.name == "Modular Machine Group Frame" && line.enabled) == 1,
                "pooled machine does not duplicate its group outline");
            for (int module = 0; module < ModularMachineCatalog.ModuleCount(building); module++)
            {
                var role = ModularMachineCatalog.Role(building, module);
                var cell = owner.transform.Find("Modular Modules/Modular Cell " + module + " " + role);
                Require(cell != null && cell.gameObject.activeInHierarchy, "planned module has an active world cell");
                Require(Mathf.Abs(cell.position.x - ModularMachineCatalog.ModuleX(building, module)) < .001f &&
                    Mathf.Abs(cell.position.y - ModularMachineCatalog.ModuleY(building, module)) < .001f,
                    "module renderer occupies its actual freely selected cell");
                var plate = cell.Find("Module Plate").GetComponent<SpriteRenderer>();
                Require(plate.enabled && plate.sprite != null && plate.color.a > 0, "module has visible world geometry");
                int mask = ModularVisualLayout.ConnectionMask(building, module);
                Require((int)QaField(cells[module], "connectionMask") == mask, "rendered module tracks actual same-group neighbours");
                foreach (var side in new[] { 1, 2 })
                {
                    var join = (SpriteRenderer)QaField(cells[module], side == 1 ? "joinEast" : "joinNorth");
                    Require(join.enabled == ((mask & side) != 0), "textured connector visibility matches cardinal adjacency");
                    if (!join.enabled) continue;
                    Require(join.sprite != null && join.color.a > 0, "connected modules have a visible authored texture");
                    float boundary = side == 1 ? cell.position.x + .5f : cell.position.y + .5f;
                    Require(side == 1 ? join.bounds.min.x < boundary && join.bounds.max.x > boundary :
                        join.bounds.min.y < boundary && join.bounds.max.y > boundary,
                        "metal connector physically spans the shared module boundary");
                }
            }
        }
    }
}
#endif
