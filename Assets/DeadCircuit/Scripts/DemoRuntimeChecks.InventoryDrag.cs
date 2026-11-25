#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace DeadCircuit
{
    public sealed partial class DemoRuntimeChecks
    {
        IEnumerator CheckInventoryDrag(GameBridge bridge)
        {
            InputDevice[] physicalDevices = InputSystem.devices.Where(device => device is Keyboard || device is Mouse).ToArray();
            bool[] wasEnabled = physicalDevices.Select(device => device.enabled).ToArray();
            var backgroundBehavior = InputSystem.settings.backgroundBehavior;
            int width = Screen.width, height = Screen.height;
            FullScreenMode displayMode = Screen.fullScreenMode;
            Mouse previousQaMouse = qaMouse;
            qaMouse = null;
            try
            {
                yield return CheckInventoryDragScenario(bridge);
            }
            finally
            {
                if (qaMouse != null && qaMouse.added) InputSystem.RemoveDevice(qaMouse);
                qaMouse = previousQaMouse != null && previousQaMouse.added ? previousQaMouse : null;
                for (int i = 0; i < physicalDevices.Length; i++)
                {
                    if (!physicalDevices[i].added || physicalDevices[i].enabled == wasEnabled[i]) continue;
                    if (wasEnabled[i]) InputSystem.EnableDevice(physicalDevices[i]);
                    else InputSystem.DisableDevice(physicalDevices[i]);
                }
                InputSystem.settings.backgroundBehavior = backgroundBehavior;
                if (Screen.width != width || Screen.height != height || Screen.fullScreenMode != displayMode)
                    Screen.SetResolution(width, height, displayMode);
            }
        }

        IEnumerator CheckInventoryDragScenario(GameBridge bridge)
        {
            bridge.StartHost(73149, false);
            Require(bridge.IsRunning, "inventory drag QA starts an isolated world");
            var player = bridge.LocalPlayer;
            // UI behavior is checked on the existing safe QA plot, away from hostile wildlife.
            PrepareQaPlot(bridge.World, true);
            player.x = 30; player.y = 26;
            Array.Clear(player.items, 0, player.items.Length);
            player.items[(int)ItemKind.Wood] = 4;
            player.items[(int)ItemKind.Stone] = 3;
            player.parts[(int)PartKind.RedDot] = 1;
            GearState Give(GearKind kind)
            {
                var gear = new GearState { id = bridge.World.nextId++, kind = kind, durability = EquipmentCatalog.Gear(kind).maxDurability };
                player.gear.Add(gear);
                return gear;
            }
            var weapon = Give(GearKind.Knife);
            var tool = Give(GearKind.StoneAxe);
            var hood = Give(GearKind.ClothHood);
            var pack = Give(GearKind.GrassBackpack);
            var physicalDevices = InputSystem.devices.Where(device => device is Keyboard || device is Mouse).ToArray();
            foreach (var device in physicalDevices) InputSystem.DisableDevice(device);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            qaMouse = InputSystem.AddDevice<Mouse>("Inventory drag QA Mouse");
            yield return new WaitForSecondsRealtime(.5f);
            yield return ClickHudTool("Open Inventory");
            RequireInventoryLayout();
            var inventoryCard = Rect("Inventory Card");
            Require(!InventoryOwnBounds(inventoryCard, Rect("Inventory Scroll View")).Overlaps(InventoryOwnBounds(inventoryCard, Rect("Inventory Personal Drop Zone"))),
                "transfer zones do not cover the scrollable item grid");
            foreach (string name in new[] { "Weapon Equipment Slot", "Tool Equipment Slot", "Unarmed Equipment Slot",
                "Armor Slot 0", "Armor Slot 1", "Armor Slot 2", "Armor Slot 3", "Backpack Equipment Slot" })
            {
                var slot = FindButton(name);
                Require(slot.GetComponentsInChildren<Image>().Any(i => i.sprite != null && i.enabled && !i.raycastTarget), "empty equipment socket has an icon: " + name);
                Require(!slot.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.isActiveAndEnabled && !string.IsNullOrWhiteSpace(t.text)), "equipment socket uses icons without text: " + name);
            }
            Require(FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "inventory has exactly one EventSystem");
            yield return InventoryMouse(InventoryPointer(Rect("Weapon Equipment Slot")), false);
            Require(Active("Inventory Hover Tooltip") && Rect("Inventory Hover Text").GetComponent<TextMeshProUGUI>().text.Contains("主武器"),
                "hover explains the icon-only equipment socket");
            Require(Rect("Inventory Panel").rect.Contains(InventoryOwnBounds(Rect("Inventory Panel"), Rect("Inventory Hover Tooltip")).center),
                "hover tooltip is positioned inside the actual canvas");
            Click("Inventory Tab Equipment");
            yield return InventoryDrag("Gear Slot " + tool.id, "Weapon Equipment Slot");
            Require(player.weaponId == 0 && player.toolId == 0, "wrong equipment socket refuses tool without mutating ownership");
            yield return InventoryDrag("Gear Slot " + weapon.id, "Weapon Equipment Slot", captureDrag: true);
            Require(player.weaponId == weapon.id, "real pointer drag equips weapon through authority");
            yield return InventoryDrag("Gear Slot " + tool.id, "Tool Equipment Slot");
            Require(player.toolId == tool.id && player.weaponId == weapon.id, "real pointer drag equips tool independently");
            yield return InventoryDrag("Gear Slot " + hood.id, "Armor Slot 0");
            Require(player.armorIds[0] == hood.id, "real pointer drag wears matching armor");
            yield return InventoryDrag("Gear Slot " + pack.id, "Backpack Equipment Slot");
            Require(player.backpackId == pack.id, "real pointer drag equips physical backpack");
            int count = player.gear.Count;
            yield return InventoryDrag("Weapon Equipment Slot", null);
            Require(player.weaponId == weapon.id && player.gear.Count == count, "drop outside inventory cancels without losing equipment");
            Click("Inventory Tab Materials");
            yield return null;
            var wood = FindButton("Inventory Item " + (int)ItemKind.Wood);
            var stone = FindButton("Inventory Item " + (int)ItemKind.Stone);
            Require(!wood.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.isActiveAndEnabled && t.text.Contains(DemoCatalog.ItemNames[(int)ItemKind.Wood])),
                "material cell displays icon and count rather than its name");
            int beforeOrder = wood.transform.GetSiblingIndex().CompareTo(stone.transform.GetSiblingIndex());
            yield return InventoryDrag(wood.name, stone.name);
            Require(wood.transform.GetSiblingIndex().CompareTo(stone.transform.GetSiblingIndex()) != beforeOrder, "drag reorders material icons");
            Require(player.items[(int)ItemKind.Wood] == 4 && player.items[(int)ItemKind.Stone] == 3, "visual ordering preserves stack quantities");
            yield return InventoryDrag(wood.name, "Backpack Equipment Slot");
            Require(pack.backpackInventory.items[(int)ItemKind.Wood] == 4 && player.items[(int)ItemKind.Wood] == 4, "drag moves entire available stack into worn pack without duplication");
            yield return InventoryDrag(wood.name, "Inventory Personal Drop Zone");
            Require(pack.backpackInventory.items[(int)ItemKind.Wood] == 0 && player.items[(int)ItemKind.Wood] == 4, "drag retrieves packed stack into personal inventory");
            yield return InventoryDrag(wood.name, "Inventory Pack Drop Zone");
            Require(pack.backpackInventory.items[(int)ItemKind.Wood] == 4, "dedicated backpack drop zone packs the stack");
            yield return InventoryDrag(wood.name, null);
            Require(pack.backpackInventory.items[(int)ItemKind.Wood] == 4, "invalid material drop preserves bag allocation");
            Click("Inventory Tab Parts");
            yield return InventoryDrag("Part Slot " + (int)PartKind.RedDot, "Backpack Equipment Slot");
            Require(pack.backpackInventory.parts[(int)PartKind.RedDot] == 1 && player.parts[(int)PartKind.RedDot] == 1, "drag packs attachments without duplicating them");
            Click("Inventory Tab Equipment");
            yield return InventoryDrag("Weapon Equipment Slot", "Equipment Scroll View", new Vector2(.87f, .12f));
            Require(player.weaponId == 0 && player.gear.Count == count && player.gear.Contains(weapon), "drag equipped weapon into list unequips without losing identity");
            yield return InventoryDrag("Gear Slot " + weapon.id, "Weapon Equipment Slot", duringDrag: () => Click("Close Inventory"));
            Require(!Active("Inventory Panel") && player.weaponId == 0 && player.gear.Count == count, "closing panel during drag cancels the pending equip");
            yield return ClickHudTool("Open Inventory");
            Require(Active("Inventory Panel"), "reopen after interrupted drag: hp=" + player.hp + " connected=" + player.connected + " running=" + bridge.IsRunning);
            Click("Inventory Tab Equipment");
            yield return InventoryDrag("Gear Slot " + weapon.id, "Weapon Equipment Slot", duringDrag: () => Click("Inventory Tab Materials"));
            Require(player.weaponId == 0, "switching inventory tab during drag cancels the pending equip");
            Click("Inventory Tab Equipment");
            yield return Capture("inventory-icons-drag");
            Click("Inventory Tab Materials");
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.5f);
            var corners = new Vector3[4];
            inventoryCard.GetWorldCorners(corners);
            Require(corners[0].x >= -1 && corners[0].y >= -1 && corners[2].x <= Screen.width + 1 && corners[2].y <= Screen.height + 1,
                "inventory card remains inside the smaller screen");
            yield return Capture("inventory-icons-1280");
            Click("Close Inventory");
            Debug.Log("DEMO_INVENTORY_DRAG_QA_PASS icon-only wrong-slot equip armor backpack cancel reorder stack part unequip close tab resolutions");
        }

        static Vector2 InventoryPointer(RectTransform target, Vector2? fraction = null)
        {
            var at = fraction ?? new Vector2(.5f, .5f);
            Vector3 local = new Vector3(Mathf.Lerp(target.rect.xMin, target.rect.xMax, at.x), Mathf.Lerp(target.rect.yMin, target.rect.yMax, at.y));
            return RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(local));
        }

        static Rect InventoryOwnBounds(RectTransform parent, RectTransform target)
        {
            // Masked scroll content deliberately extends outside the visible viewport.
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 min = parent.InverseTransformPoint(corners[0]);
            Vector3 max = parent.InverseTransformPoint(corners[2]);
            return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static IEnumerator InventoryMouse(Vector2 position, bool down)
        {
            InputSystem.QueueStateEvent(qaMouse, new MouseState { position = position, buttons = (ushort)(down ? 1 : 0) });
            yield return null;
            yield return null;
        }

        IEnumerator InventoryDrag(string source, string destination, Vector2? destinationFraction = null, Action duringDrag = null, bool captureDrag = false)
        {
            var button = FindButton(source);
            var scroll = button.GetComponentInParent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            if (scroll != null)
            {
                scroll.StopMovement();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, button.transform);
                var view = scroll.viewport.rect;
                float shift = bounds.min.y < view.yMin ? view.yMin - bounds.min.y : bounds.max.y > view.yMax ? view.yMax - bounds.max.y : 0;
                scroll.content.anchoredPosition += new Vector2(0, shift);
                yield return null;
                Canvas.ForceUpdateCanvases();
            }
            Require(PointerTargetsButton(button), "drag source receives raycasts: " + source);
            var sourceHandle = button.GetComponent<InventoryDragHandle>();
            Require(sourceHandle != null, "drag source has pointer handlers: " + source);
            var originalBegin = sourceHandle.begin;
            sourceHandle.begin = data =>
            {
                bool started = originalBegin?.Invoke(data) == true;
                Debug.Log("DEMO_INVENTORY_DRAG_BEGIN " + source + " started=" + started + " focused=" + Application.isFocused);
                return started;
            };
            Vector2 from = InventoryPointer((RectTransform)button.transform);
            Vector2 to = destination == null ? new Vector2(3, 3) : InventoryPointer(Rect(destination), destinationFraction);
            yield return InventoryMouse(from, false);
            yield return InventoryMouse(from, true);
            for (int step = 1; step <= 8; step++)
            {
                yield return InventoryMouse(Vector2.Lerp(from, to, step / 8f), true);
                if (step == 4 && captureDrag)
                {
                    Require(Active("Inventory Drag Ghost"), "drag ghost follows the real pointer");
                    var ghostBounds = InventoryOwnBounds(Rect("Inventory Panel"), Rect("Inventory Drag Ghost"));
                    Require(Rect("Inventory Panel").rect.Contains(ghostBounds.center), "drag ghost is positioned inside the actual canvas");
                    yield return Capture("inventory-drag-feedback");
                }
                if (step == 4) duringDrag?.Invoke();
            }
            yield return InventoryMouse(to, false);
            yield return new WaitForSecondsRealtime(.3f);
            sourceHandle.begin = originalBegin;
            Debug.Log("DEMO_INVENTORY_DRAG_END " + source + " -> " + destination + " inventory=" + Active("Inventory Panel"));
        }
    }
}
#endif
