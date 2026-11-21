using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        // Stable QA surface: actual sparse world positions, never a rectangular template.
        // Visual.modules.cells[index].root, named "Modular Cell <index> <role>".
        sealed class ModularCellVisual
        {
            public Transform root;
            public SpriteRenderer plate, mechanism, inset, arrow, glow, progressBack, progress;
            public LineRenderer outline;
            public TextMeshPro marker;
        }

        sealed class ModularMachineVisual
        {
            public Transform root;
            public readonly List<ModularCellVisual> cells = new List<ModularCellVisual>();
            public TextMeshPro caption, socketLabel;
            public LineRenderer socket;
        }

        ModularMachineVisual modularPreview;

        TextMeshPro ModularLabel(string name, Transform parent, float width, float height, float fontSize)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var label = root.AddComponent<TextMeshPro>();
            label.font = DemoHud.DefaultFont;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            label.rectTransform.sizeDelta = new Vector2(width, height);
            label.raycastTarget = false;
            return label;
        }

        LineRenderer ModularCellOutline(string name, Transform parent)
        {
            var line = NewLine(name, 0, .018f, false);
            line.transform.SetParent(parent, false);
            line.useWorldSpace = false;
            line.positionCount = 5;
            line.SetPositions(new[] { new Vector3(-.47f,-.47f,0), new Vector3(.47f,-.47f,0),
                new Vector3(.47f,.47f,0), new Vector3(-.47f,.47f,0), new Vector3(-.47f,-.47f,0) });
            return line;
        }

        ModularMachineVisual NewModularMachine(Transform parent, string name)
        {
            var visual = new ModularMachineVisual { root = NewRoot(name, parent) };
            visual.caption = ModularLabel("Modular Machine Hover", visual.root, 4f, .7f, 2f);
            visual.socket = ModularCellOutline("Modular External Port Cell", visual.root);
            visual.socketLabel = ModularLabel("Modular External Port Label", visual.root, 1.18f, .62f, 1.55f);
            return visual;
        }

        ModularCellVisual NewModularCell(ModularMachineVisual machine, int index)
        {
            var cell = new ModularCellVisual { root = NewRoot("Modular Cell " + index, machine.root) };
            cell.plate = NewSprite("Module Plate", cell.root, 0);
            cell.mechanism = NewSprite("Module Mechanism", cell.root, 1);
            cell.inset = NewSprite("Module Inset", cell.root, 2);
            cell.arrow = NewSprite("Module Port Arrow", cell.root, 3);
            cell.glow = NewSprite("Module Running FX", cell.root, 4);
            cell.progressBack = NewSprite("Module Construction Track", cell.root, 5);
            cell.progress = NewSprite("Module Construction Progress", cell.root, 6);
            cell.outline = ModularCellOutline("Module Cell Outline", cell.root);
            cell.marker = ModularLabel("Module Role And Construction", cell.root, .88f, .24f, 1.75f);
            cell.marker.transform.localPosition = new Vector3(0, -.32f, 0);
            machine.cells.Add(cell);
            return cell;
        }

        void DrawModularMachine(Visual owner, BuildingState state)
        {
            owner.root.name = "Modular Machine " + state.id;
            owner.body.enabled = owner.shadow.enabled = owner.detail.enabled = owner.arrow.enabled = owner.lamp.enabled = owner.cargo.enabled = false;
            if (owner.modules == null) owner.modules = NewModularMachine(owner.root.transform, "Modular Modules");
            var visual = owner.modules;
            visual.root.gameObject.SetActive(true);
            int count = ModularMachineCatalog.ModuleCount(state);
            bool running = ModularMachinePresentation.Processing(observedWorld, state);
            Vector2 mouse = bridge.MouseWorld;
            int mx = Mathf.FloorToInt(mouse.x + .5f), my = Mathf.FloorToInt(mouse.y + .5f);
            bool ui = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            int hover = !ui && !bridge.BuildSelection.HasValue ? ModularMachineCatalog.ModuleAt(state, mx, my) : -1;
            bool editing = bridge.BuildSelection.HasValue && ModularMachineCatalog.IsModular(bridge.BuildSelection.Value) && bridge.ModularBlueprintId == state.id;
            for (int i = 0; i < count; i++)
            {
                var cell = i < visual.cells.Count ? visual.cells[i] : NewModularCell(visual, i);
                var role = ModularMachineCatalog.Role(state, i);
                bool built = ModularMachineCatalog.ModuleComplete(state, i);
                cell.root.name = "Modular Cell " + i + " " + role;
                DrawModularCell(cell, state.kind, role, ModularMachineCatalog.ModuleRotation(state, i),
                    ModularMachineCatalog.ModuleX(state, i), ModularMachineCatalog.ModuleY(state, i), built,
                    false, true, false, built ? 0f : ModularMachinePresentation.ConstructionRatio(state, i), running, hover == i || editing);
            }
            for (int i = count; i < visual.cells.Count; i++) visual.cells[i].root.gameObject.SetActive(false);
            visual.caption.gameObject.SetActive(hover >= 0);
            visual.socket.enabled = false;
            visual.socketLabel.gameObject.SetActive(false);
            if (hover >= 0)
            {
                var role = ModularMachineCatalog.Role(state, hover);
                visual.caption.transform.position = new Vector3(mx, my + .89f, 0);
                SetModularLabel(visual.caption, ModularMachineCatalog.RoleName(state, hover) +
                    (ModularMachineCatalog.ModuleComplete(state, hover) ? " · ■已建" : " · ＋待建"), Color.white, 10020);
                if (IsModularPort(role))
                    DrawModularSocket(visual, state.kind, role, ModularMachineCatalog.ModuleRotation(state, hover), mx, my,
                        role != MachineModuleRole.Exhaust || ModularMachineCatalog.ExhaustClear(observedWorld, state));
            }
            Health(owner, state.hp, state.hp < owner.maxHp - .01f, new Vector2(0, DemoCatalog.Height(state) * .5f + .16f), 1.54f);
        }

        void DrawModularPreview(WorldState world, PlayerState player, BuildKind kind, int x, int y)
        {
            if (modularPreview == null) modularPreview = NewModularMachine(effectsRoot, "Modular Blueprint Preview");
            modularPreview.root.gameObject.SetActive(true);
            var cell = modularPreview.cells.Count == 0 ? NewModularCell(modularPreview, 0) : modularPreview.cells[0];
            var active = bridge.ActiveModularBlueprint;
            bool erase = bridge.ModularEraseMode;
            int module = active == null ? -1 : ModularMachineCatalog.ModuleAt(active, x, y);
            var role = erase && module >= 0 ? ModularMachineCatalog.Role(active, module) : bridge.ModularRoleSelection;
            int rotation = erase && module >= 0 ? ModularMachineCatalog.ModuleRotation(active, module) : bridge.BuildRotation & 3;
            bool valid = CanModularPreview(world, player, kind, x, y, out string reason);
            cell.root.name = "Modular Cell Preview " + role;
            DrawModularCell(cell, kind, role, rotation, x, y, false, true, valid, erase, 0f, false, true);
            // Exactly one cell belongs to the cursor preview, regardless of group size.
            for (int i = 1; i < modularPreview.cells.Count; i++) modularPreview.cells[i].root.gameObject.SetActive(false);
            modularPreview.caption.gameObject.SetActive(true);
            modularPreview.caption.transform.position = new Vector3(x, y + .94f, 0);
            SetModularLabel(modularPreview.caption, valid ? erase ? "擦除当前格 · 左键" :
                ModularMachinePresentation.RoleTitle(kind, role) + " · 蓝图免费 · R 旋转" : reason, valid ? Color.white : Danger, 10020);
            modularPreview.socket.enabled = false;
            modularPreview.socketLabel.gameObject.SetActive(false);
            if (!erase && IsModularPort(role)) DrawModularSocket(modularPreview, kind, role, rotation, x, y, true);
        }

        void HideModularPreview()
        {
            if (modularPreview != null) modularPreview.root.gameObject.SetActive(false);
        }

        static bool IsModularPort(MachineModuleRole role) => role == MachineModuleRole.Input || role == MachineModuleRole.Output ||
            role == MachineModuleRole.Energy || role == MachineModuleRole.Exhaust;

        void DrawModularSocket(ModularMachineVisual visual, BuildKind kind, MachineModuleRole role, int rotation, int x, int y, bool clear)
        {
            // Input and energy arrows point inward; their external connection is behind them.
            int outward = (rotation + (role == MachineModuleRole.Energy || role == MachineModuleRole.Input ? 2 : 0)) & 3;
            Vector3 position = new Vector3(x + DemoCatalog.DirectionX(outward), y + DemoCatalog.DirectionY(outward), 0);
            visual.socket.transform.position = position;
            visual.socket.enabled = true;
            visual.socket.sortingOrder = 10006;
            visual.socket.startColor = visual.socket.endColor = clear ? new Color(.94f,.75f,.40f,.78f) : Danger;
            visual.socketLabel.gameObject.SetActive(true);
            visual.socketLabel.transform.position = position;
            string label = role == MachineModuleRole.Energy ? kind == BuildKind.LargeSteamFurnace ? "蒸汽管\n接入此格" : "匹配电杆\n放在此格" :
                role == MachineModuleRole.Exhaust ? kind == BuildKind.LargeSteamFurnace ? clear ? "排气净空\n保持此格" : "排气阻塞\n清空此格" : "被动通风" :
                role == MachineModuleRole.Input ? "配方原料\n从此格入" : "单出口\n向此格出";
            SetModularLabel(visual.socketLabel, label, clear ? Color.white : Danger, 10021);
        }

        void DrawModularCell(ModularCellVisual cell, BuildKind kind, MachineModuleRole role, int rotation, int x, int y,
            bool built, bool preview, bool valid, bool erase, float ratio, bool running, bool focused)
        {
            cell.root.gameObject.SetActive(true);
            cell.root.position = new Vector3(x, y, 0);
            int order = preview ? 10004 : Order(y);
            float alpha = preview ? .46f : built ? 1f : .24f;
            Color frame = preview && (!valid || erase) ? Danger : focused ? Amber : built ? new Color(.65f,.75f,.72f) : new Color(.49f,.77f,.82f);
            frame.a = built ? .72f : .8f;
            cell.plate.sprite = Machine(BuildKind.Wall, 0);
            cell.plate.sortingOrder = order;
            cell.plate.transform.localScale = Vector3.one * .94f;
            cell.plate.color = new Color(.53f,.61f,.60f, alpha * (role == MachineModuleRole.Casing ? 1f : .62f));
            ConfigureModularMechanism(cell, kind, role, rotation, alpha, order + 1);
            cell.outline.enabled = preview || !built || focused;
            cell.outline.sortingOrder = order + 4;
            cell.outline.startWidth = cell.outline.endWidth = focused ? .035f : .018f;
            cell.outline.startColor = cell.outline.endColor = frame;
            string mark = erase ? "擦×" : ModularMachinePresentation.Mark(role) + (preview && !valid ? "×" : built ? "■" : "＋");
            SetModularLabel(cell.marker, mark, built ? Color.white : frame, order + 8);
            cell.arrow.enabled = IsModularPort(role) && !erase;
            if (cell.arrow.enabled)
            {
                bool inward = role == MachineModuleRole.Input || role == MachineModuleRole.Energy;
                int flow = rotation & 3;
                Vector2 dir = new Vector2(DemoCatalog.DirectionX(flow), DemoCatalog.DirectionY(flow));
                cell.arrow.sprite = Art("arrow");
                cell.arrow.sortingOrder = order + 5;
                cell.arrow.transform.localPosition = dir * (inward ? -.40f : .40f);
                cell.arrow.transform.localRotation = Quaternion.Euler(0, 0, flow * 90);
                cell.arrow.transform.localScale = Vector3.one * .30f;
                cell.arrow.color = new Color(1f,.83f,.49f, built ? 1f : .70f);
            }
            cell.glow.enabled = !preview && running && role == MachineModuleRole.Process;
            if (cell.glow.enabled)
            {
                cell.glow.sprite = Art("glow");
                cell.glow.sortingOrder = order + 3;
                cell.glow.transform.localScale = Vector3.one * .65f;
                cell.glow.color = new Color(1f,.65f,.24f,.20f + Mathf.Sin(visualTime * 6f) * .025f);
            }
            cell.progress.enabled = cell.progressBack.enabled = ratio > 0f && !preview;
            if (cell.progress.enabled)
            {
                cell.progressBack.sprite = cell.progress.sprite = Art("pixel");
                cell.progressBack.sortingOrder = order + 6; cell.progress.sortingOrder = order + 7;
                cell.progressBack.transform.localPosition = new Vector3(0, -.43f, 0);
                cell.progressBack.transform.localScale = new Vector3(.20f,.012f,1f);
                cell.progressBack.color = new Color(.06f,.11f,.11f,1f);
                cell.progress.transform.localPosition = new Vector3((ratio - 1f) * .30f, -.43f, 0);
                cell.progress.transform.localScale = new Vector3(.20f * ratio,.012f,1f);
                cell.progress.color = Amber;
            }
        }

        bool CanModularPreview(WorldState world, PlayerState player, BuildKind kind, int x, int y, out string reason)
        {
            reason = "";
            if (!ModularMachineCatalog.HasWrench(player)) { reason = "需手持可用扳手并激活工具槽"; return false; }
            BuildingState active = bridge.ActiveModularBlueprint;
            if (bridge.ModularEraseMode)
            {
                if (active == null || ModularMachineCatalog.ModuleAt(active, x, y) < 0) reason = "请对准当前蓝图组的模块格";
                else if (!ModularMachinePresentation.CanErase(world, active)) reason = "先清空库存、在途与加工进度";
                else
                {
                    int index = ModularMachineCatalog.ModuleAt(active, x, y);
                    if (ModularMachineCatalog.Role(active, index) == MachineModuleRole.Controller && ModularMachineCatalog.ModuleCount(active) > 1)
                        reason = "请先擦除其他模块，再擦控制器";
                    else if (!ModularMachinePresentation.ModuleRefundFits(player, active, index)) reason = "背包空间不足，无法退还本格建材";
                }
            }
            else if (!PrimitiveCatalog.IsBuildUnlocked(world, kind)) reason = "大型机械研究尚未解锁";
            else if (active != null && !ModularMachinePresentation.CanErase(world, active)) reason = "先清空库存、在途与加工进度再画图";
            else if (active == null && bridge.ModularRoleSelection != MachineModuleRole.Controller) reason = "先放控制器并等待房主确认";
            else if (active != null && bridge.ModularRoleSelection == MachineModuleRole.Controller) reason = "本组已有控制器；另开机器请点新蓝图";
            else if (active != null && bridge.ModularRoleSelection != MachineModuleRole.Casing &&
                ModularMachinePresentation.RoleCount(active, bridge.ModularRoleSelection) > 0) reason = "此职责已放置；请选择其他模块";
            else if (active != null && ModularMachineCatalog.ModuleCount(active) >= ModularMachineCatalog.MaxModules) reason = "已达到模块数量上限";
            else if (active == null && world.buildings.Count >= WorldSimulator.MaxBuildings) reason = "设备数量已达上限";
            else if (!WorldTiles.Inside(world, x, y) || Ground(x, y) < 0 || Ground(x, y) == 2) reason = "模块须放在已知陆地格";
            else if (FloorWorld.IsProtectedCell(world, x, y)) reason = "此格是楼梯 / 出入口保护格";
            else if (occupied.Contains(WorldTiles.Key(x, y)) || blockedNodes.Contains(WorldTiles.Key(x, y))) reason = "此格已有模块、设备或资源";
            else if (world.generatorVersion < WorldGeneration.NaturalOpeningVersion && Mathf.Abs(x - world.width / 2) <= 1 &&
                Mathf.Abs(y - (world.height / 2 + 3)) <= 1) reason = "救援区须保持畅通";
            if (reason.Length > 0) return false;
            if (!bridge.ModularEraseMode)
            {
                if (world.players != null) foreach (var p in world.players) if (p.hp > 0 && Mathf.Abs(p.x - x) < .8f && Mathf.Abs(p.y - y) < .8f) reason = "此格有人物";
                if (world.zombies != null) foreach (var z in world.zombies) if (Mathf.Abs(z.x - x) < .8f && Mathf.Abs(z.y - y) < .8f) reason = "此格有生物";
                if (world.survival != null && world.survival.enabled)
                {
                    foreach (var plot in world.survival.plots) if (plot.x == x && plot.y == y) reason = "此格已有农田";
                    foreach (var creature in world.survival.creatures) if (Mathf.Abs(creature.x - x) < .8f && Mathf.Abs(creature.y - y) < .8f) reason = "此格有生物";
                }
                if (active != null)
                {
                    int minX = Mathf.Min(active.x, x), minY = Mathf.Min(active.y, y);
                    int maxX = Mathf.Max(active.x + DemoCatalog.Width(active) - 1, x), maxY = Mathf.Max(active.y + DemoCatalog.Height(active) - 1, y);
                    if (maxX - minX + 1 > ModularMachineCatalog.MaxSpan || maxY - minY + 1 > ModularMachineCatalog.MaxSpan) reason = "超出本组最大跨度 12×12";
                }
            }
            if (reason.Length > 0) return false;
            if (active != null && (ModularPlanFloor(world, x, y) != ModularPlanFloor(world, active.x, active.y) ||
                ModularPlanFloor(world, Mathf.FloorToInt(player.x + .5f), Mathf.FloorToInt(player.y + .5f)) != ModularPlanFloor(world, active.x, active.y)))
            { reason = "施工者与模块须处于同一楼层"; return false; }
            if (new Vector2(x - player.x, y - player.y).sqrMagnitude > 2.4f * 2.4f) reason = "请靠近当前格 2.4 格内";
            else if (!PreviewLineClear(player.x, player.y, x, y, active)) reason = "当前格视线受阻";
            return reason.Length == 0;
        }

        static int ModularPlanFloor(WorldState world, int x, int y) => ChunkStreaming.Enabled(world) &&
            WorldTiles.TryChunk(world, WorldTiles.FloorDiv(x, 16), WorldTiles.FloorDiv(y, 16), out var chunk) ? chunk.interiorSiteId : 0;

        void ConfigureModularMechanism(ModularCellVisual cell, BuildKind kind, MachineModuleRole role, int rotation, float alpha, int order)
        {
            BuildKind source;
            switch (role)
            {
                case MachineModuleRole.Controller: source = BuildKind.CircuitAssembler; break;
                case MachineModuleRole.Energy: source = kind == BuildKind.LargeSteamFurnace ? BuildKind.SteamPipe : BuildKind.CircuitBreaker; break;
                case MachineModuleRole.Maintenance: source = BuildKind.MachineTool; break;
                case MachineModuleRole.Input: case MachineModuleRole.Output: source = BuildKind.Belt; break;
                case MachineModuleRole.Process: source = kind == BuildKind.LargeSteamFurnace ? BuildKind.SteamFurnace : kind == BuildKind.LargeAssemblyPlant ? BuildKind.PrecisionAssembler : BuildKind.ElectricCrusher; break;
                case MachineModuleRole.Exhaust: source = BuildKind.Chimney; break;
                default: source = BuildKind.Wall; break;
            }
            cell.mechanism.sprite = Machine(source, 0);
            cell.mechanism.enabled = role != MachineModuleRole.Casing;
            cell.mechanism.sortingOrder = order;
            cell.mechanism.transform.localScale = Vector3.one * .78f;
            cell.mechanism.transform.localRotation = Quaternion.Euler(0, 0, (rotation & 3) * 90);
            cell.mechanism.color = new Color(1,1,1,alpha);
            bool port = role == MachineModuleRole.Input || role == MachineModuleRole.Output;
            cell.inset.enabled = port || role == MachineModuleRole.Casing;
            cell.inset.sprite = port ? Machine(BuildKind.Chest, 0) : Art("item_" + (int)ItemKind.SteelPlate);
            cell.inset.sortingOrder = order + 1;
            cell.inset.transform.localScale = Vector3.one * (port ? .38f : .30f);
            cell.inset.transform.localPosition = new Vector3(0, .08f, 0);
            cell.inset.color = new Color(1,1,1,alpha);
        }

        static void SetModularLabel(TextMeshPro label, string value, Color color, int order)
        {
            if (label.text != value)
            {
                if (label.font != null) label.font.TryAddCharacters(value, true);
                label.text = value;
            }
            label.color = color;
            label.GetComponent<MeshRenderer>().sortingOrder = order;
        }
    }
}
