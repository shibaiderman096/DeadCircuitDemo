using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        enum InventoryDropKind { Cell, List, Equipment, Pack, Personal }

        sealed class InventoryDropTarget
        {
            public InventoryDropKind kind;
            public InventoryDragHandle handle;
            public InventoryCell cell;
            public int tab, slot;
            public UnityEngine.UI.Image feedback;
        }

        sealed class InventoryDragItem
        {
            public PackItemKind kind;
            public int id, tab, equippedSlot, backpackId, seed;
            public long tick;
            public bool equipped;
            public GearKind? gearKind;
            public string player;
            public InventoryDragHandle source;
        }

        readonly List<string>[] inventoryManualOrder = { new List<string>(), new List<string>(), new List<string>() };
        readonly List<InventoryDropTarget> inventoryDropTargets = new List<InventoryDropTarget>();
        readonly Dictionary<UiButton, UnityEngine.UI.Image> inventoryDurability = new Dictionary<UiButton, UnityEngine.UI.Image>();
        readonly Dictionary<UiButton, UnityEngine.UI.Image> inventoryActiveRims = new Dictionary<UiButton, UnityEngine.UI.Image>();
        readonly List<RaycastResult> inventoryDragRaycasts = new List<RaycastResult>();
        InventoryDragItem inventoryDragItem;
        InventoryDragHandle inventoryHoverHandle;
        RectTransform inventoryDragGhost, inventoryTooltip;
        UnityEngine.UI.Image inventoryDragGhostIcon;
        TextMeshProUGUI inventoryTooltipText, inventoryDragCaption;
        Vector2 inventoryPointerPosition;
        Camera inventoryPointerCamera;
        int inventoryDragTab;

        static GearKind EmptyArmorIcon(int slot) => slot == 0 ? GearKind.ClothHood : slot == 1 ? GearKind.LeatherJacket :
            slot == 2 ? GearKind.WorkPants : GearKind.WorkBoots;

        void SetInventorySocketArt(UiButton socket, string key, float size)
        {
            SetButtonArt(socket, key, size);
            socket.label.text = "";
            socket.label.gameObject.SetActive(false);
            RectTransform icon = socket.icon.rectTransform;
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(.5f, .5f);
            icon.anchoredPosition = new Vector2(0, 2);
            icon.sizeDelta = new Vector2(size, size);
        }

        void PaintInventorySocket(UiButton socket, GearState gear, string emptyKey, bool unarmed = false)
        {
            Set(socket.label, "");
            SetHudArt(socket.icon, gear == null ? emptyKey : "gear_" + (int)gear.kind);
            socket.icon.color = gear == null ? new Color(1, 1, 1, unarmed ? .82f : .28f) :
                gear.durability <= 0 && !EquipmentCatalog.IsBackpack(gear.kind) ? InventoryRed : Color.white;
            PaintInventoryDurability(socket, gear);
        }

        void PaintInventoryDurability(UiButton widget, GearState gear)
        {
            if (!inventoryDurability.TryGetValue(widget, out UnityEngine.UI.Image fill))
            {
                RectTransform track = Box("Inventory Durability Track", widget.button.transform, 0, 0, 0, 4, InventoryInset);
                track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 0);
                track.pivot = new Vector2(.5f, 0); track.offsetMin = new Vector2(7, 5); track.offsetMax = new Vector2(-7, 9);
                RectTransform bar = Box("Inventory Durability Fill", track, 0, 0, 0, 4, InventoryCyan);
                Stretch(bar);
                fill = bar.GetComponent<UnityEngine.UI.Image>();
                inventoryDurability.Add(widget, fill);
            }
            bool visible = gear != null && !EquipmentCatalog.IsBackpack(gear.kind);
            fill.transform.parent.gameObject.SetActive(visible);
            if (!visible) return;
            float ratio = Mathf.Clamp01(gear.durability / (float)Mathf.Max(1, EquipmentCatalog.Gear(gear.kind).maxDurability));
            fill.rectTransform.anchorMax = new Vector2(ratio, 1);
            fill.color = ratio <= .15f ? Danger : ratio <= .35f ? Amber : InventoryCyan;
        }

        void SetInventorySocketActive(UiButton widget, bool active)
        {
            if (!inventoryActiveRims.TryGetValue(widget, out UnityEngine.UI.Image rim))
            {
                rim = MakeInventoryOutline(widget.button.transform, "Inventory Active Rim", InventoryBlue, 2);
                inventoryActiveRims.Add(widget, rim);
            }
            rim.gameObject.SetActive(active);
        }

        UnityEngine.UI.Image MakeInventoryOutline(Transform parent, string name, Color color, float inset)
        {
            RectTransform rect = NewRect(name, parent);
            Stretch(rect, inset, inset, inset, inset);
            var border = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            border.color = Color.clear;
            border.raycastTarget = false;
            // Four strips leave the item itself legible and never intercept pointer events.
            for (int edge = 0; edge < 4; edge++)
            {
                RectTransform line = Box("Edge " + edge, rect, 0, 0, 0, 0, color);
                bool vertical = edge < 2;
                line.anchorMin = vertical ? new Vector2(edge, 0) : new Vector2(0, edge - 2);
                line.anchorMax = vertical ? new Vector2(edge, 1) : new Vector2(1, edge - 2);
                line.pivot = new Vector2(.5f, .5f);
                line.sizeDelta = vertical ? new Vector2(2, 0) : new Vector2(0, 2);
                line.anchoredPosition = Vector2.zero;
            }
            return border;
        }

        void MakeInventoryDragAndDrop(RectTransform card)
        {
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                BindInventoryEquipmentDrag(equipmentSlots[i], slot);
            }
            for (int i = 0; i < 4; i++) BindInventoryEquipmentDrag(armorSlots[i], i + 3);
            BindInventoryEquipmentDrag(backpackSlot, 7);
            BindInventoryListDrop(materialScroll, 0);
            BindInventoryListDrop(gearScroll, 1);
            BindInventoryListDrop(partScroll, 2);

            RectTransform pack = Box("Inventory Pack Drop Zone", card, 794, 576, 194, 30, InventoryCellIdle, true);
            Label("Pack Drop Label", pack, "放入背包", 8, 0, 178, 30, 13, InventoryCyan).alignment = TextAlignmentOptions.Center;
            BindInventoryDrop(pack.gameObject, InventoryDropKind.Pack);
            RectTransform personal = Box("Inventory Personal Drop Zone", card, 996, 576, 194, 30, InventorySocketIdle, true);
            inventorySelection = Label("Equipment Selection", personal, "取回随身", 8, 0, 178, 30, 13, InventoryBlue);
            inventorySelection.alignment = TextAlignmentOptions.Center;
            BindInventoryDrop(personal.gameObject, InventoryDropKind.Personal);

            inventoryDragGhost = Box("Inventory Drag Ghost", inventory, 0, 0, 72, 72, new Color32(24, 38, 44, 232));
            var ghostGroup = inventoryDragGhost.gameObject.AddComponent<CanvasGroup>();
            ghostGroup.blocksRaycasts = false; ghostGroup.interactable = false;
            inventoryDragGhostIcon = MakeHudArt("Inventory Drag Icon", inventoryDragGhost, 7, 3, 58);
            inventoryDragCaption = Label("Inventory Drag Caption", inventoryDragGhost, "", -80, 76, 232, 46, 13, Ink);
            inventoryDragCaption.alignment = TextAlignmentOptions.Top;
            inventoryDragGhost.gameObject.SetActive(false);
            inventoryTooltip = Box("Inventory Hover Tooltip", inventory, 0, 0, 304, 94, new Color32(9, 15, 20, 247));
            inventoryTooltip.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            inventoryTooltipText = Label("Inventory Hover Text", inventoryTooltip, "", 10, 7, 284, 80, 14, Ink);
            inventoryTooltipText.alignment = TextAlignmentOptions.TopLeft;
            inventoryTooltip.gameObject.SetActive(false);
            InventoryDragHandle lifetime = inventory.gameObject.AddComponent<InventoryDragHandle>();
            lifetime.tick = TickInventoryDrag;
            lifetime.disabled = CancelInventoryDrag;
        }

        InventoryDropTarget BindInventoryDrop(GameObject owner, InventoryDropKind kind, int tab = -1, int slot = -1, InventoryCell cell = null)
        {
            var handle = owner.GetComponent<InventoryDragHandle>() ?? owner.AddComponent<InventoryDragHandle>();
            var target = new InventoryDropTarget { kind = kind, handle = handle, tab = tab, slot = slot, cell = cell };
            target.feedback = MakeInventoryOutline(owner.transform, "Inventory Drop Feedback", InventoryCyan, 3);
            target.feedback.gameObject.SetActive(false);
            inventoryDropTargets.Add(target);
            handle.drop = data => DropInventoryItem(target, data);
            handle.enter = data =>
            {
                inventoryPointerPosition = data.position; inventoryPointerCamera = data.enterEventCamera;
                if (target.kind == InventoryDropKind.List) return;
                inventoryHoverHandle = handle;
                if (inventoryDragItem == null) ShowInventoryTooltip(target);
                else PaintInventoryDropFeedback(target);
            };
            handle.exit = data =>
            {
                if (inventoryHoverHandle == handle)
                {
                    inventoryHoverHandle = null;
                    if (inventoryTooltip != null) inventoryTooltip.gameObject.SetActive(false);
                }
            };
            handle.disabled = () =>
            {
                if (inventoryDragItem?.source == handle) CancelInventoryDrag();
                if (inventoryHoverHandle == handle)
                {
                    inventoryHoverHandle = null;
                    if (inventoryTooltip != null) inventoryTooltip.gameObject.SetActive(false);
                }
            };
            return target;
        }

        void BindInventoryCellDrag(InventoryCell cell, int tab)
        {
            InventoryDropTarget target = BindInventoryDrop(cell.widget.button.gameObject, InventoryDropKind.Cell, tab, cell: cell);
            target.handle.begin = data => BeginInventoryDrag(target, data);
            target.handle.drag = DragInventoryItem;
            target.handle.end = data => CancelInventoryDrag();
        }

        void BindInventoryEquipmentDrag(UiButton button, int slot)
        {
            InventoryDropTarget target = BindInventoryDrop(button.button.gameObject, InventoryDropKind.Equipment, slot: slot);
            target.handle.begin = data => BeginInventoryDrag(target, data);
            target.handle.drag = DragInventoryItem;
            target.handle.end = data => CancelInventoryDrag();
        }

        void BindInventoryListDrop(UnityEngine.UI.ScrollRect scroll, int tab)
        {
            // A drop-only handle preserves the ScrollRect's own empty-space drag handlers.
            BindInventoryDrop(scroll.gameObject, InventoryDropKind.List, tab);
        }

        static int InventorySlotGearId(PlayerState player, int slot) => player == null ? 0 :
            slot == 0 ? player.weaponId : slot == 1 ? player.toolId : slot == 7 ? player.backpackId :
            slot >= 3 && slot <= 6 ? Count(player.armorIds, slot - 3) : 0;

        static int InventoryGearSlot(GearKind kind) => EquipmentCatalog.IsBackpack(kind) ? 7 :
            EquipmentCatalog.ArmorSlot(kind) >= 0 ? 3 + EquipmentCatalog.ArmorSlot(kind) : EquipmentCatalog.Gear(kind).tool ? 1 : 0;

        static PackItemKind InventoryMaterialPackKind(InventoryCell cell) => cell.supply == InventorySupply.Food ? PackItemKind.Food :
            cell.supply == InventorySupply.Seed ? PackItemKind.Seed : cell.supply == InventorySupply.Water ?
            (cell.id == 0 ? PackItemKind.RawWater : PackItemKind.CleanWater) : PackItemKind.Material;

        bool BeginInventoryDrag(InventoryDropTarget source, PointerEventData data)
        {
            CancelInventoryDrag();
            PlayerState player = bridge.LocalPlayer;
            if (!InventoryDragReady(player) || !source.handle.gameObject.activeInHierarchy) return false;
            int tab = source.kind == InventoryDropKind.Equipment ? 1 : source.tab;
            int id = source.kind == InventoryDropKind.Equipment ? InventorySlotGearId(player, source.slot) : source.cell?.id ?? -1;
            GearState gear = tab == 1 ? EquipmentCatalog.Find(player, id) : null;
            if (tab == 1 && gear == null) return false;
            var item = new InventoryDragItem
            {
                kind = tab == 1 ? PackItemKind.Gear : tab == 2 ? PackItemKind.Part : InventoryMaterialPackKind(source.cell),
                id = id, tab = tab, gearKind = gear?.kind, player = player.id, source = source.handle,
                equipped = gear != null && InventoryWeight.IsEquipped(player, gear), equippedSlot = gear == null ? -1 : InventoryGearSlot(gear.kind),
                backpackId = player.backpackId, seed = bridge.World.seed, tick = bridge.World.tick
            };
            if (!InventoryDragQuantity(player, item).HasValue) return false;
            inventoryDragItem = item; inventoryDragTab = inventoryTab;
            data.eligibleForClick = false;
            ActiveInventoryScroll()?.StopMovement();
            inventoryPointerPosition = data.position; inventoryPointerCamera = data.pressEventCamera;
            inventoryDragGhostIcon.sprite = gear != null ? DemoArt.GetIcon("gear_" + (int)gear.kind) : source.cell.widget.icon.sprite;
            inventoryDragGhostIcon.color = gear != null ? Color.white : source.cell.widget.icon.color;
            inventoryDragGhostIcon.enabled = inventoryDragGhostIcon.sprite != null;
            inventoryDragGhost.gameObject.SetActive(true);
            inventoryDragGhost.SetAsLastSibling();
            inventoryDragCaption.text = "拖到目标；空白处松开取消";
            PositionInventoryOverlay(inventoryDragGhost, inventoryPointerPosition, inventoryPointerCamera, new Vector2(16, -16));
            return true;
        }

        bool InventoryDragReady(PlayerState player) => isActiveAndEnabled && canvas != null && canvas.enabled &&
            inventory != null && inventory.gameObject.activeInHierarchy && bridge.IsRunning && bridge.World != null && player != null && player.connected && player.hp > 0f;

        (int total, int packed, int grams)? InventoryDragQuantity(PlayerState player, InventoryDragItem item)
        {
            if (player == null || item == null) return null;
            BackpackInventoryState bag = InventoryWeight.EquippedPack(player)?.backpackInventory;
            int total, packed, grams;
            switch (item.kind)
            {
                case PackItemKind.Gear:
                    GearState gear = EquipmentCatalog.Find(player, item.id);
                    if (gear == null || gear.kind != item.gearKind) return null;
                    return (1, bag?.gearIds?.Contains(item.id) == true ? 1 : 0, InventoryWeight.GearGrams(gear));
                case PackItemKind.Part:
                    total = Count(player.parts, item.id); packed = Count(bag?.parts, item.id); grams = InventoryWeight.Grams((PartKind)item.id); break;
                case PackItemKind.Food:
                    total = Count(player.needs?.foods, item.id); packed = Count(bag?.foods, item.id); grams = InventoryWeight.Grams((FoodKind)item.id); break;
                case PackItemKind.Seed:
                    total = Count(player.needs?.seeds, item.id); packed = Count(bag?.seeds, item.id); grams = InventoryWeight.Grams((CropKind)item.id); break;
                case PackItemKind.RawWater:
                    total = player.needs?.waterRaw ?? 0; packed = bag?.waterRaw ?? 0; grams = InventoryWeight.WaterGrams; break;
                case PackItemKind.CleanWater:
                    total = player.needs?.waterClean ?? 0; packed = bag?.waterClean ?? 0; grams = InventoryWeight.WaterGrams; break;
                default:
                    total = Count(player.items, item.id); packed = Count(bag?.items, item.id); grams = InventoryWeight.Grams((ItemKind)item.id); break;
            }
            return total > 0 ? (total, packed, grams) : null;
        }

        bool ValidateInventoryDrag()
        {
            if (inventoryDragItem == null) return false;
            PlayerState player = bridge.LocalPlayer;
            InventoryDragItem item = inventoryDragItem;
            bool valid = InventoryDragReady(player) && item.player == player.id && inventoryDragTab == inventoryTab &&
                bridge.World.seed == item.seed && bridge.World.tick >= item.tick && item.source != null && item.source.gameObject.activeInHierarchy &&
                InventoryDragQuantity(player, item).HasValue;
            if (valid && item.kind == PackItemKind.Gear)
            {
                GearState gear = EquipmentCatalog.Find(player, item.id);
                valid = InventoryWeight.IsEquipped(player, gear) == item.equipped &&
                    (!item.equipped || InventorySlotGearId(player, item.equippedSlot) == item.id);
            }
            if (!valid) CancelInventoryDrag();
            return valid;
        }

        void DragInventoryItem(PointerEventData data)
        {
            data.eligibleForClick = false;
            if (!ValidateInventoryDrag()) return;
            inventoryPointerPosition = data.position; inventoryPointerCamera = data.pressEventCamera;
            PositionInventoryOverlay(inventoryDragGhost, data.position, inventoryPointerCamera, new Vector2(16, -16));
            PaintInventoryDropFeedback(InventoryTargetAt(data.pointerCurrentRaycast.gameObject));
        }

        InventoryDropTarget InventoryTargetAt(GameObject hit)
        {
            for (Transform current = hit == null ? null : hit.transform; current != null && current.IsChildOf(inventory); current = current.parent)
            {
                InventoryDragHandle handle = current.GetComponent<InventoryDragHandle>();
                if (handle == null) continue;
                InventoryDropTarget target = inventoryDropTargets.Find(candidate => candidate.handle == handle);
                if (target != null) return target;
            }
            return null;
        }

        void TickInventoryDrag()
        {
            if (!InventoryDragReady(bridge.LocalPlayer)) { CancelInventoryDrag(); return; }
            if (Mouse.current != null) inventoryPointerPosition = Mouse.current.position.ReadValue();
            if (inventoryDragItem != null)
            {
                if (!ValidateInventoryDrag()) return;
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame ||
                    Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) { CancelInventoryDrag(); return; }
                PositionInventoryOverlay(inventoryDragGhost, inventoryPointerPosition, inventoryPointerCamera, new Vector2(16, -16));
                if (eventSystem != null)
                {
                    inventoryDragRaycasts.Clear();
                    eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = inventoryPointerPosition }, inventoryDragRaycasts);
                    PaintInventoryDropFeedback(inventoryDragRaycasts.Count == 0 ? null : InventoryTargetAt(inventoryDragRaycasts[0].gameObject));
                }
            }
            else if (inventoryTooltip.gameObject.activeSelf)
                PositionInventoryOverlay(inventoryTooltip, inventoryPointerPosition, inventoryPointerCamera, new Vector2(18, -18));
        }

        void PaintInventoryDropFeedback(InventoryDropTarget hovered)
        {
            foreach (InventoryDropTarget target in inventoryDropTargets)
            {
                bool active = inventoryDragItem != null && target.handle != null && target.handle.gameObject.activeInHierarchy;
                bool legal = active && InventoryDropAction(target, out _, out _);
                bool show = active && (legal || target == hovered);
                target.feedback.gameObject.SetActive(show);
                if (show)
                {
                    Color color = legal ? target == hovered ? InventoryCyan : new Color(.31f, .58f, .61f, .65f) : InventoryRed;
                    foreach (UnityEngine.UI.Image edge in target.feedback.GetComponentsInChildren<UnityEngine.UI.Image>())
                        if (edge != target.feedback) edge.color = color;
                }
            }
            if (inventoryDragItem == null) return;
            string hint = "拖到目标；空白处松开取消";
            bool allowed = hovered != null && InventoryDropAction(hovered, out _, out hint);
            if (hovered == null) hint = "空白处松开取消";
            Set(inventoryDragCaption, hint);
            inventoryDragCaption.color = hovered == null ? Ink : allowed ? InventoryCyan : InventoryRed;
        }

        bool InventoryDropAction(InventoryDropTarget target, out string action, out string hint)
        {
            action = null; hint = "此处不能放置该物品";
            InventoryDragItem item = inventoryDragItem;
            PlayerState player = bridge.LocalPlayer;
            if (item == null || target == null || !InventoryDragReady(player) || target.handle == null || !target.handle.gameObject.activeInHierarchy) return false;
            if (target.kind == InventoryDropKind.Equipment && target.slot != 7)
            {
                if (item.kind != PackItemKind.Gear || InventoryGearSlot(item.gearKind.Value) != target.slot) return false;
                return InventoryCanEquip(player, item, target.slot, out action, out hint);
            }
            if (target.kind == InventoryDropKind.Equipment && target.slot == 7 && item.kind == PackItemKind.Gear && EquipmentCatalog.IsBackpack(item.gearKind.Value))
                return InventoryCanEquip(player, item, 7, out action, out hint);
            if (target.kind == InventoryDropKind.Pack || target.kind == InventoryDropKind.Personal || target.kind == InventoryDropKind.Equipment && target.slot == 7)
            {
                if (item.equipped) { hint = "请先把装备拖回未装备列表卸下"; return false; }
                if (item.kind == PackItemKind.Gear && EquipmentCatalog.IsBackpack(item.gearKind.Value)) { hint = "背包不能嵌套"; return false; }
                var quantity = InventoryDragQuantity(player, item);
                GearState pack = InventoryWeight.EquippedPack(player);
                if (pack?.backpackInventory == null || pack.id != item.backpackId || !quantity.HasValue) { hint = "需要同一个已装备背包"; return false; }
                bool put = target.kind != InventoryDropKind.Personal;
                var value = quantity.Value;
                if (put ? value.total <= value.packed : value.packed <= 0) { hint = put ? "该物品已全部在包内" : "该物品尚未放入背包"; return false; }
                long packed = InventoryWeight.BackpackContentsGrams(pack.backpackInventory, player.gear);
                long space = put ? InventoryWeight.CapacityGrams(player) - InventoryWeight.BaseCapacityGrams - packed :
                    InventoryWeight.BaseCapacityGrams - (InventoryWeight.CarriedGrams(player) - packed);
                if (space < value.grams) { hint = put ? "包内空间不足" : "随身空间不足"; return false; }
                action = put ? "packput" : "packtake"; hint = put ? "放入背包 · 按余量转移整堆" : "取回随身 · 按余量转移整堆"; return true;
            }
            if (target.kind == InventoryDropKind.List || target.kind == InventoryDropKind.Cell)
            {
                if (item.equipped)
                {
                    if (target.tab != 1) { hint = "拖到未装备列表卸下"; return false; }
                    GearState gear = EquipmentCatalog.Find(player, item.id);
                    bool backpack = EquipmentCatalog.IsBackpack(gear.kind);
                    if (!backpack && !InventoryWeight.CanFit(player, InventoryWeight.GearGrams(gear))) { hint = "剩余负重不足，无法卸下"; return false; }
                    action = "unequip"; hint = backpack ? "放下背包 · 整包及包内物品将落在地上" : "卸下至未装备列表"; return true;
                }
                if (target.tab != item.tab) return false;
                if (target.cell != null && InventoryCellKey(target.cell, target.tab) == InventoryDragKey(item)) { hint = "当前物品"; return false; }
                int sourceIndex = visibleCells.FindIndex(cell => InventoryCellKey(cell, item.tab) == InventoryDragKey(item));
                int targetIndex = target.cell == null ? -1 : visibleCells.IndexOf(target.cell);
                action = "reorder";
                hint = target.cell == null ? "移动到本页末尾" : sourceIndex < targetIndex ? "移动到此物品之后" : "移动到此物品之前";
                return true;
            }
            return false;
        }

        bool InventoryCanEquip(PlayerState player, InventoryDragItem item, int slot, out string action, out string hint)
        {
            action = null; hint = "已在此装备位";
            if (InventorySlotGearId(player, slot) == item.id) return false;
            GearState gear = EquipmentCatalog.Find(player, item.id);
            if (gear == null) return false;
            GearState previous = EquipmentCatalog.Find(player, InventorySlotGearId(player, slot));
            if (slot != 7 && previous != null && !InventoryWeight.CanFit(player, (long)InventoryWeight.GearGrams(previous) - InventoryWeight.GearGrams(gear)))
            { hint = "替换后超重"; return false; }
            action = "equip";
            hint = slot == 7 && previous != null ? "替换背包 · 原包及内容将落在地上" : previous == null ? "装备到此位置" : "替换此装备";
            return true;
        }

        void DropInventoryItem(InventoryDropTarget target, PointerEventData data)
        {
            if (inventoryDragItem == null) return;
            data.eligibleForClick = false;
            if (data.pointerDrag != inventoryDragItem.source.gameObject || !ValidateInventoryDrag()) return;
            if (!InventoryDropAction(target, out string action, out string hint))
            {
                CancelInventoryDrag();
                Notify(hint);
                return;
            }
            InventoryDragItem item = inventoryDragItem;
            if (action == "reorder") ReorderInventoryItem(item, target);
            CancelInventoryDrag();
            if (action == "reorder") RefreshAll();
            else Send(action, item.id, action == "packput" || action == "packtake" ? (int?)item.kind : null);
        }

        void CancelInventoryDrag()
        {
            inventoryDragItem = null;
            inventoryHoverHandle = null;
            if (inventoryDragGhost != null) inventoryDragGhost.gameObject.SetActive(false);
            if (inventoryTooltip != null) inventoryTooltip.gameObject.SetActive(false);
            foreach (InventoryDropTarget target in inventoryDropTargets)
                if (target.feedback != null) target.feedback.gameObject.SetActive(false);
        }

        static string InventoryCellKey(InventoryCell cell, int tab) => tab == 1 ? "g:" + cell.id : tab == 2 ? "p:" + cell.id :
            "m:" + (int)InventoryMaterialPackKind(cell) + ":" + cell.id;
        static string InventoryDragKey(InventoryDragItem item) => item.tab == 1 ? "g:" + item.id : item.tab == 2 ? "p:" + item.id : "m:" + (int)item.kind + ":" + item.id;

        int CompareInventoryManualOrder(InventoryCell a, InventoryCell b)
        {
            List<string> order = inventoryManualOrder[inventoryTab];
            if (order.Count == 0) return 0;
            int ai = order.IndexOf(InventoryCellKey(a, inventoryTab)), bi = order.IndexOf(InventoryCellKey(b, inventoryTab));
            if (ai < 0) ai = int.MaxValue;
            if (bi < 0) bi = int.MaxValue;
            return ai.CompareTo(bi);
        }

        void ReorderInventoryItem(InventoryDragItem item, InventoryDropTarget target)
        {
            // Preserve hidden-category positions as well as the visible ordering. New items use the preset order.
            List<string> order = inventoryManualOrder[item.tab];
            foreach (InventoryCell cell in visibleCells)
            {
                string key = InventoryCellKey(cell, item.tab);
                if (!order.Contains(key)) order.Add(key);
            }
            string source = InventoryDragKey(item);
            int targetIndex = target.cell == null ? order.Count : order.IndexOf(InventoryCellKey(target.cell, item.tab));
            order.Remove(source);
            // Using the target's pre-removal index puts a forward drag after it, and a backward drag before it.
            int index = target.cell == null ? order.Count : targetIndex;
            order.Insert(index < 0 ? order.Count : Mathf.Clamp(index, 0, order.Count), source);
        }

        void ShowInventoryTooltip(InventoryDropTarget target)
        {
            if (inventoryTooltip == null || !InventoryDragReady(bridge.LocalPlayer)) return;
            PlayerState player = bridge.LocalPlayer;
            string text;
            if (target.kind == InventoryDropKind.Equipment)
            {
                GearState gear = EquipmentCatalog.Find(player, InventorySlotGearId(player, target.slot));
                string slot = target.slot == 0 ? "主武器" : target.slot == 1 ? "副手工具" : target.slot == 2 ? "徒手" :
                    target.slot == 7 ? "背包位" : InventoryArmorNames[target.slot - 3];
                text = slot + (gear == null ? target.slot == 2 ? " · 点击切换" : " · 空位\n拖入匹配装备；点击筛选" :
                    " · " + EquipmentCatalog.GearNames[(int)gear.kind] + "\n" + (EquipmentCatalog.IsBackpack(gear.kind) ?
                    "拖回未装备列表会将整包及内容放到地上" : "耐久 " + GearDurability(gear) + " · 拖回未装备列表卸下"));
            }
            else if (target.kind == InventoryDropKind.Cell)
            {
                InventoryCell cell = target.cell;
                GearState gear = target.tab == 1 ? EquipmentCatalog.Find(player, cell.id) : null;
                text = cell.title + (gear == null ? " · ×" + cell.quantity : "  #" + gear.id + "\n" +
                    (EquipmentCatalog.IsBackpack(gear.kind) ? "总容量 " + BackpackCapacityText(gear.kind) : "耐久 " + GearDurability(gear))) +
                    "\n点击查看详情；拖动可整理或放入目标";
            }
            else if (target.kind == InventoryDropKind.Pack) text = "放入背包\n将未装备物品拖到这里；按包内余量尽可能转移整堆。背包不能嵌套。";
            else if (target.kind == InventoryDropKind.Personal) text = "取回随身\n将包内物品拖到这里；按随身余量尽可能转移整堆。卸装请拖到未装备列表。";
            else return;
            Set(inventoryTooltipText, text);
            inventoryTooltip.gameObject.SetActive(true);
            inventoryTooltip.SetAsLastSibling();
            PositionInventoryOverlay(inventoryTooltip, inventoryPointerPosition, inventoryPointerCamera, new Vector2(18, -18));
        }

        void PositionInventoryOverlay(RectTransform overlay, Vector2 screen, Camera camera, Vector2 offset)
        {
            if (overlay == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(inventory, screen, camera, out Vector2 local)) return;
            Rect bounds = inventory.rect;
            Vector2 position = local + offset;
            float leftInset = overlay == inventoryDragGhost ? 88 : 8;
            float width = overlay == inventoryDragGhost ? 152 : overlay.rect.width;
            position.x = Mathf.Clamp(position.x, bounds.xMin + leftInset, bounds.xMax - width - 8);
            float height = overlay == inventoryDragGhost ? overlay.rect.height + 52 : overlay.rect.height;
            position.y = Mathf.Clamp(position.y, bounds.yMin + height + 8, bounds.yMax - 8);
            overlay.anchorMin = overlay.anchorMax = new Vector2(.5f, .5f);
            overlay.pivot = new Vector2(0, 1);
            // Local points use the parent's pivot; centered anchors use its rectangle center.
            overlay.anchoredPosition = position - bounds.center;
        }
    }
}
