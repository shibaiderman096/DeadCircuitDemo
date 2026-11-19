using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        sealed class InventoryCell
        {
            public int id, category, quantity;
            public string title;
            public UiButton widget;
        }

        sealed class EquipmentRecipeRow
        {
            public int kind;
            public bool part;
            public UiButton craft;
            public TextMeshProUGUI details;
        }

        readonly List<InventoryCell> materialCells = new List<InventoryCell>();
        readonly List<InventoryCell> gearCells = new List<InventoryCell>();
        readonly List<InventoryCell> partCells = new List<InventoryCell>();
        readonly List<InventoryCell> visibleCells = new List<InventoryCell>();
        readonly List<EquipmentRecipeRow> equipmentRecipes = new List<EquipmentRecipeRow>();
        readonly UiButton[] inventoryTabs = new UiButton[4];
        readonly UiButton[] equipmentSlots = new UiButton[3];
        readonly UiButton[] armorSlots = new UiButton[4];
        readonly List<UiButton> ammoButtons = new List<UiButton>();
        readonly List<ItemKind> inventoryAmmo = new List<ItemKind>();
        readonly List<UiButton> installButtons = new List<UiButton>();
        readonly List<UiButton> detachButtons = new List<UiButton>();
        readonly List<TextMeshProUGUI> attachmentLabels = new List<TextMeshProUGUI>();
        readonly List<GameObject> gearDetailObjects = new List<GameObject>();
        readonly List<GameObject> gunDetailObjects = new List<GameObject>();
        readonly List<GameObject> armorDetailObjects = new List<GameObject>();
        RectTransform materialGrid, gearGrid, partGrid, inventoryRecipes;
        UnityEngine.UI.ScrollRect materialScroll, gearScroll, partScroll, craftingScroll, itemDetailScroll;
        TextMeshProUGUI equipmentSummary, inventorySummary, inventoryEmpty, inventorySelection;
        TextMeshProUGUI itemTitle, itemMeta, itemUsage, itemStats, equipmentHint, armorSummary;
        UiButton sortInventory, filterInventory, equipSelected, unequipSelected, repairGear, reloadGear, unloadGear, salvageGear;
        int inventoryTab, inventorySort, inventoryFilter, selectedMaterial, selectedGear = -1, selectedPart;
        // Keep identities rather than indices or snapshot references, even when sorting and syncing.
        int inventoryDetailKind, inventorySelectionRevision, paintedSelectionRevision = -1;
        string inventoryPlayer = "";

        void MakeEquipmentSummary()
        {
            // Full equipment statistics belong in the inventory, not a permanent HUD strip.
            RectTransform strip = Box("Equipment HUD", game, 0, 0, 420, 32, HudSurface);
            strip.anchorMin = strip.anchorMax = strip.pivot = new Vector2(0, 0);
            strip.anchoredPosition = new Vector2(18, 18);
            strip.sizeDelta = new Vector2(420, 32);
            equipmentSummary = Label("Current Equipment Summary", strip, "", 10, 0, 400, 32, 14, Ink);
            equipmentSummary.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void MakeInventory(RectTransform root)
        {
            inventory = Backdrop("Inventory Panel", root, new Color32(4, 12, 8, 225));
            RectTransform card = Box("Inventory Card", inventory, 0, 0, 1160, 688, Panel, true);
            Center(card, 1160, 688);
            PanelRule(card, 1160, 58);
            Box("Equipment Slots Surface", card, 14, 62, 1132, 104, Recessed);
            Box("Inventory Browser Surface", card, 14, 208, 564, 368, Recessed);
            Box("Inventory Detail Surface", card, 584, 208, 562, 368, Recessed);
            Box("Inventory Footer Rule", card, 22, 616, 1116, 1, Edge);
            Label("Inventory Heading", card, "随身背包 / 装备工坊", 22, 16, 830, 36, 25, Ink, FontStyles.Bold);
            MakeAppearanceButton(card);
            Button("Close Inventory", card, "关闭 B / Esc", 1012, 18, 126, 32, () => SetPanel(inventory, false));
            equipmentSlots[0] = Button("Weapon Equipment Slot", card, "武器位", 22, 66, 408, 44, () => BrowseEquipmentSlot(0));
            equipmentSlots[1] = Button("Tool Equipment Slot", card, "工具位", 438, 66, 408, 44, () => BrowseEquipmentSlot(1));
            equipmentSlots[2] = Button("Unarmed Equipment Slot", card, "徒手", 854, 66, 94, 44, () => InventoryCommand("switch", 2));
            inventorySelection = Label("Equipment Selection", card, "", 962, 64, 176, 48, 13, Accent);
            for (int i = 0; i < armorSlots.Length; i++)
            {
                int slot = i;
                armorSlots[i] = Button("Armor Slot " + i, card, "", 22 + i * 232, 118, 224, 42, () => BrowseArmorSlot(slot));
                armorSlots[i].label.fontSizeMax = 14;
                armorSlots[i].label.fontSizeMin = 12;
            }
            armorSummary = Label("Armor Summary", card, "", 962, 116, 176, 48, 13, Accent);
            string[] names = { "Materials", "Equipment", "Parts", "Craft" };
            string[] titles = { "材料", "装备", "配件", "制作" };
            for (int i = 0; i < inventoryTabs.Length; i++)
            {
                int tab = i;
                inventoryTabs[i] = Button("Inventory Tab " + names[i], card, titles[i], 22 + i * 104, 170, 96, 34, () => BrowseInventoryTab(tab));
            }
            sortInventory = Button("Inventory Sort", card, "排序：分类", 438, 170, 132, 34,
                () => { inventorySort = (inventorySort + 1) % 3; RefreshAll(); });
            inventorySummary = Label("Inventory Summary", card, "", 22, 214, 344, 28, 14, Muted);
            filterInventory = Button("Inventory Category Filter", card, "分类：全部", 374, 214, 196, 30, CycleInventoryFilter);
            Label("Inventory Detail Heading", card, "选中详情 · 滚轮查看属性与装配", 592, 214, 546, 28, 15, Accent);
            materialGrid = MakeInventoryGrid("Inventory Scroll View", card, out materialScroll);
            gearGrid = MakeInventoryGrid("Equipment Scroll View", card, out gearScroll);
            partGrid = MakeInventoryGrid("Parts Scroll View", card, out partScroll);
            inventoryRecipes = ScrollContent("Craft Scroll View", card, 22, 252, 548, 316, out craftingScroll);
            inventoryEmpty = Label("Inventory Empty", card, "", 40, 270, 496, 96, 16, Muted);
            for (int i = 0; i < DemoCatalog.ItemNames.Length; i++)
            {
                int item = i;
                materialCells.Add(MakeInventoryCell("Inventory Item " + i, materialGrid, i, DemoCatalog.ItemNames[i], MaterialCategory(i),
                    () => { selectedMaterial = item; SelectInventoryDetail(0); }));
            }
            for (int i = 0; i < EquipmentCatalog.PartNames.Length; i++)
            {
                int part = i;
                partCells.Add(MakeInventoryCell("Part Slot " + i, partGrid, i, EquipmentCatalog.PartNames[i], (int)EquipmentCatalog.Part((PartKind)i).slot + 1,
                    () => { selectedPart = part; SelectInventoryDetail(2); }));
            }
            MakeInventoryRecipes();
            MakeItemDetails(card);
            eatButton = Button("Eat", card, "吃食 F", 22, 580, 130, 34, () => Send("eat"));
            workbenchHint = Label("Workbench Requirement", card, "", 166, 574, 972, 44, 13, Muted);
            inventoryNotice = Label("Inventory Feedback", card, "", 22, 622, 1116, 50, 13, Amber);
            inventory.gameObject.SetActive(false);
        }

        RectTransform MakeInventoryGrid(string name, Transform parent, out UnityEngine.UI.ScrollRect scroll)
        {
            RectTransform content = ScrollContent(name, parent, 22, 252, 548, 316, out scroll, true);
            return content;
        }

        InventoryCell MakeInventoryCell(string name, Transform parent, int id, string title, int category, Action select)
        {
            UiButton widget = Button(name, parent, title, 0, 0, 166, 84, select);
            widget.label.alignment = TextAlignmentOptions.TopLeft;
            widget.label.fontSizeMax = 14;
            widget.label.fontSizeMin = 12;
            Stretch(widget.label.rectTransform, 10, 8, 8, 6);
            Box("Category Accent", widget.button.transform, 0, 0, 3, 84, category == 3 ? Amber : Accent);
            return new InventoryCell { id = id, title = title, category = category, widget = widget };
        }

        void MakeInventoryRecipes()
        {
            var handcraft = new List<RecipeDefinition>
            {
                new RecipeDefinition { id = 0, name = "手工燃料", era = TechEra.Primitive, building = BuildKind.Workbench, duration = .5f,
                    inputs = DemoCatalog.Materials(ItemKind.Wood, 3), outputs = DemoCatalog.Materials(ItemKind.Fuel, 2) },
                new RecipeDefinition { id = 1, name = "手工弹药", era = TechEra.Primitive, building = BuildKind.Workbench, duration = .5f,
                    inputs = DemoCatalog.Materials(ItemKind.Iron, 1), outputs = DemoCatalog.Materials(ItemKind.Ammo, 4) }
            };
            foreach (RecipeDefinition recipe in TechCatalog.Recipes)
                if (recipe.building == BuildKind.Workbench && recipe.id > 1) handcraft.Add(recipe);
            foreach (RecipeDefinition recipe in handcraft)
            {
                RecipeDefinition entry = recipe;
                RectTransform row = FlowRow("Craft Recipe " + entry.id, inventoryRecipes, 520, 112);
                UiButton craft = Button(entry.id == 0 ? "Craft Fuel" : entry.id == 1 ? "Craft Ammo" : "Craft Recipe Button " + entry.id,
                    row, entry.name, 2, 6, 134, 66, () => CraftRecipe(entry.id));
                manualRecipes.Add(new RecipeRow { recipe = entry, craft = craft, details = RecipeDetails(row) });
            }
            for (int i = 0; i < EquipmentCatalog.GearNames.Length; i++) MakeEquipmentRecipe(i, false);
            for (int i = 0; i < EquipmentCatalog.PartNames.Length; i++) MakeEquipmentRecipe(i, true);
        }

        TextMeshProUGUI RecipeDetails(RectTransform row)
        {
            TextMeshProUGUI details = Label("Craft Recipe Details", row, "", 148, 4, 362, 104, 13, Ink);
            details.enableAutoSizing = false;
            details.alignment = TextAlignmentOptions.TopLeft;
            return details;
        }

        void MakeEquipmentRecipe(int kind, bool part)
        {
            string prefix = part ? "Part Craft " : "Gear Craft ";
            RectTransform row = FlowRow(prefix + kind + " Row", inventoryRecipes, 520, 112);
            UiButton craft = Button(prefix + kind, row, "", 2, 6, 134, 66, () => CraftEquipment(kind, part));
            equipmentRecipes.Add(new EquipmentRecipeRow { kind = kind, part = part, craft = craft, details = RecipeDetails(row) });
        }

        void MakeItemDetails(RectTransform card)
        {
            RectTransform content = ScrollContent("Inventory Details Scroll View", card, 592, 252, 546, 316, out itemDetailScroll);
            itemTitle = FlowLabel("Selected Item Name", content, "", 518, 36, 22, Accent);
            itemMeta = FlowLabel("Selected Item Quantity", content, "", 518, 54, 14, Muted);
            itemUsage = FlowLabel("Selected Item Usage", content, "", 518, 64, 14, Ink);
            itemStats = FlowLabel("Selected Gear Stats", content, "", 518, 108, 15, Ink);
            gearDetailObjects.Add(itemStats.gameObject);
            RectTransform actions = FlowRow("Gear Actions", content, 518, 38);
            equipSelected = Button("Equip Selected Gear", actions, "装备并切换", 0, 0, 166, 36, EquipSelectedGear, true);
            repairGear = Button("Repair Gear", actions, "维修装备", 174, 0, 166, 36, () => InventoryCommand("gearrepair", selectedGear));
            salvageGear = Button("Salvage Gear", actions, "回收装备", 348, 0, 168, 36, () => InventoryCommand("gearsalvage", selectedGear));
            salvageGear.label.color = Amber;
            gearDetailObjects.Add(actions.gameObject);
            RectTransform unequipRow = FlowRow("Armor Unequip Actions", content, 518, 36);
            unequipSelected = Button("Unequip Selected Gear", unequipRow, "脱下所选防具", 0, 0, 516, 34, () => InventoryCommand("unequip", selectedGear));
            armorDetailObjects.Add(unequipRow.gameObject);
            TextMeshProUGUI salvageWarning = FlowLabel("Salvage Gear Warning", content,
                "回收会永久移除此装备，返还半价材料、全部配件和弹匣内弹药；任一库存装不下时整笔拒绝。", 518, 50, 13, Amber);
            gearDetailObjects.Add(salvageWarning.gameObject);
            TextMeshProUGUI heading = FlowLabel("Ammo Selection Heading", content, "下次装填弹种 · 先卸弹才能更换已装弹种", 518, 32, 14, Accent);
            gunDetailObjects.Add(heading.gameObject);
            RectTransform ammoRow = FlowRow("Ammo Selection", content, 518, 48);
            for (int i = 0; i < DemoCatalog.ItemNames.Length; i++)
            {
                ItemKind ammo = (ItemKind)i;
                if (!EquipmentCatalog.IsAmmo(ammo)) continue;
                int index = inventoryAmmo.Count;
                inventoryAmmo.Add(ammo);
                UiButton button = Button("Select Ammo " + i, ammoRow, "", index * 104, 0, 100, 46, () => InventoryCommand("ammoselect", (int)ammo));
                button.label.fontSizeMax = 12;
                ammoButtons.Add(button);
            }
            gunDetailObjects.Add(ammoRow.gameObject);
            RectTransform reloadRow = FlowRow("Magazine Actions", content, 518, 38);
            reloadGear = Button("Reload Gear", reloadRow, "装填 R", 0, 0, 254, 36, () => InventoryCommand("reload"));
            unloadGear = Button("Unload Gear", reloadRow, "卸下弹药", 262, 0, 254, 36, () => InventoryCommand("unload", selectedGear));
            gunDetailObjects.Add(reloadRow.gameObject);
            heading = FlowLabel("Attachment Heading", content, "已装配件 · 空槽可直接安装", 518, 32, 15, Accent);
            gunDetailObjects.Add(heading.gameObject);
            for (int i = 0; i < EquipmentCatalog.SlotNames.Length; i++)
            {
                int slot = i;
                RectTransform row = FlowRow("Attachment Slot " + i, content, 518, 34);
                attachmentLabels.Add(Label("Attachment Slot State " + i, row, "", 0, 0, 392, 32, 14, Ink));
                detachButtons.Add(Button("Detach Slot " + i, row, "卸下", 402, 0, 114, 32, () => InventoryCommand("detach", selectedGear, slot)));
                gunDetailObjects.Add(row.gameObject);
            }
            heading = FlowLabel("Install Parts Heading", content, "安装配件 · 先选择目标装备，已占用槽请先卸下", 518, 32, 14, Accent);
            gunDetailObjects.Add(heading.gameObject);
            for (int i = 0; i < EquipmentCatalog.PartNames.Length; i += 2)
            {
                RectTransform row = FlowRow("Install Parts Row " + i / 2, content, 518, 40);
                for (int j = i; j < Mathf.Min(i + 2, EquipmentCatalog.PartNames.Length); j++)
                {
                    int part = j;
                    UiButton button = Button("Attach Part " + j, row, "", (j - i) * 262, 0, 254, 36, () => InventoryCommand("attach", selectedGear, part));
                    button.label.fontSizeMax = 13;
                    installButtons.Add(button);
                }
                gunDetailObjects.Add(row.gameObject);
            }
            equipmentHint = FlowLabel("Equipment Action Requirement", content, "", 518, 64, 13, Muted);
            gearDetailObjects.Add(equipmentHint.gameObject);
        }

        void BrowseInventoryTab(int tab)
        {
            inventoryTab = tab;
            inventoryFilter = 0;
            if (tab < 3) inventoryDetailKind = tab;
            inventorySelectionRevision++;
            ResetScroll(ActiveInventoryScroll());
            RefreshAll();
        }

        UnityEngine.UI.ScrollRect ActiveInventoryScroll() => inventoryTab == 0 ? materialScroll : inventoryTab == 1 ? gearScroll : inventoryTab == 2 ? partScroll : craftingScroll;

        void CycleInventoryFilter()
        {
            int count = inventoryTab == 0 ? 5 : inventoryTab == 1 ? 4 : inventoryTab == 2 ? EquipmentCatalog.SlotNames.Length + 1 : 4;
            inventoryFilter = (inventoryFilter + 1) % count;
            ResetScroll(ActiveInventoryScroll());
            RefreshAll();
        }

        void SelectInventoryDetail(int kind)
        {
            inventoryDetailKind = kind;
            inventorySelectionRevision++;
            RefreshAll();
        }

        void BrowseEquipmentSlot(int slot)
        {
            PlayerState player = bridge.LocalPlayer;
            if (player == null) return;
            int id = slot == 0 ? player.weaponId : player.toolId;
            if (EquipmentCatalog.Find(player, id) == null) return;
            selectedGear = id;
            inventoryDetailKind = 1;
            inventorySelectionRevision++;
            InventoryCommand("switch", slot);
            RefreshAll();
        }

        void BrowseArmorSlot(int slot)
        {
            PlayerState player = bridge.LocalPlayer;
            int id = player == null ? 0 : Count(player.armorIds, slot);
            if (EquipmentCatalog.Find(player, id) == null)
            {
                BrowseInventoryTab(1);
                inventoryFilter = 3;
                RefreshAll();
                return;
            }
            selectedGear = id;
            inventoryDetailKind = 1;
            inventorySelectionRevision++;
            RefreshAll();
        }

        void RefreshEquipmentSummary(PlayerState player)
        {
            GearState current = EquipmentCatalog.Equipped(player);
            if (current == null) { Set(equipmentSummary, "徒手 · Q 切换 · B 背包"); return; }
            WeaponStats stats = EquipmentCatalog.Stats(current);
            string magazine = stats.magazine > 0 ? " · " + current.magazine + "/" + stats.magazine : "";
            string state = player.reloadRemaining > 0f ? " · 装填 " + player.reloadRemaining.ToString("0.0") + "s" :
                current.durability <= 15 ? " · 低耐久" : "";
            Set(equipmentSummary, EquipmentCatalog.GearNames[(int)current.kind] + magazine + state);
            equipmentSummary.color = current.durability <= 15 ? Amber : Ink;
        }

        void RefreshInventory(WorldState world, PlayerState player)
        {
            if (inventoryPlayer != player.id)
            {
                inventoryPlayer = player.id;
                selectedGear = EquipmentCatalog.Equipped(player)?.id ?? -1;
                selectedMaterial = selectedPart = inventoryDetailKind = inventoryTab = inventoryFilter = 0;
                inventorySelectionRevision++;
                ResetScroll(materialScroll);
            }
            GearState target = EquipmentCatalog.Find(player, selectedGear);
            if (target == null && selectedGear > 0) { selectedGear = -1; inventorySelectionRevision++; }
            bool ready = player.hp > 0f && player.connected;
            string[] armorNames = { "头部", "躯干", "腿部", "脚部" };
            for (int i = 0; i < armorSlots.Length; i++)
            {
                GearState armor = EquipmentCatalog.Find(player, Count(player.armorIds, i));
                Set(armorSlots[i].label, armorNames[i] + " · " + (armor == null ? "空槽 / 浏览防具" : EquipmentCatalog.GearNames[(int)armor.kind] + "\n耐久 " + armor.durability + "%"));
                armorSlots[i].button.interactable = ready;
                Tint(armorSlots[i], armor != null && armor.id == selectedGear ? Selected : Raised);
                armorSlots[i].label.color = armor != null && armor.durability <= 0 ? Danger : Ink;
                SetButtonArt(armorSlots[i], armor == null ? null : "gear_" + (int)armor.kind, 28);
            }
            Set(armorSummary, "总护甲 " + EquipmentCatalog.ArmorRating(player).ToString("0.#") + "\n移动 -" + (EquipmentCatalog.MovementPenalty(player) * 100f).ToString("0.#") + "%");
            for (int i = 0; i < equipmentSlots.Length; i++)
            {
                GearState gear = EquipmentCatalog.Find(player, i == 0 ? player.weaponId : player.toolId);
                string name = i == 2 ? "徒手" : (i == 0 ? "武器位  " : "工具位  ") + (gear == null ? "空" : EquipmentCatalog.GearNames[(int)gear.kind]);
                Set(equipmentSlots[i].label, name + (player.activeSlot == i ? "  [当前]" : ""));
                equipmentSlots[i].button.interactable = ready && (i == 2 || gear != null);
                Tint(equipmentSlots[i], player.activeSlot == i ? Selected : Raised);
                if (i < 2) SetButtonArt(equipmentSlots[i], gear == null ? null : "gear_" + (int)gear.kind, 30);
            }
            Set(inventorySelection, "当前：" + (player.activeSlot == 0 ? "武器" : player.activeSlot == 1 ? "工具" : "徒手") +
                "\n选中：" + (target == null ? "无装备" : EquipmentCatalog.GearNames[(int)target.kind] + " #" + target.id));
            for (int i = 0; i < inventoryTabs.Length; i++) Tint(inventoryTabs[i], inventoryTab == i ? Selected : Raised);
            materialScroll.gameObject.SetActive(inventoryTab == 0);
            gearScroll.gameObject.SetActive(inventoryTab == 1);
            partScroll.gameObject.SetActive(inventoryTab == 2);
            craftingScroll.gameObject.SetActive(inventoryTab == 3);
            sortInventory.button.interactable = inventoryTab != 3;
            Set(sortInventory.label, "排序：" + (inventorySort == 0 ? "分类" : inventorySort == 1 ? "名称" : "数量"));
            Set(filterInventory.label, "分类：" + InventoryFilterName());
            bool bench = EquipmentCatalog.HasWorkbench(world, player);
            Set(workbenchHint, bench ? "工作台就绪 · 制作、维修、装配与回收均由主机验证时代、材料和库存空间。" :
                PrimitiveCatalog.Enabled(world) ? "按 T 研究原始工艺；制作 / 维修 / 装配 / 回收需工作台 2.4 格内且视线畅通。" :
                "制作 / 维修 / 装配 / 回收需工作台 2.4 格内且视线畅通；燃料 / 普通弹药可随身制作。");
            workbenchHint.color = bench ? Accent : Muted;
            RefreshInventoryCells(player);
            if (inventoryTab == 3) RefreshInventoryRecipes(world, player, bench);
            RefreshItemDetails(world, player, target, bench);
            if (paintedSelectionRevision != inventorySelectionRevision)
            {
                paintedSelectionRevision = inventorySelectionRevision;
                ResetScroll(itemDetailScroll);
            }
        }

        void RefreshInventoryCells(PlayerState player)
        {
            foreach (InventoryCell cell in materialCells)
            {
                cell.quantity = Count(player.items, cell.id);
                Set(cell.widget.label, cell.title + "\n× " + cell.quantity + "\n<size=11>" + MaterialCategoryName(cell.category) + "</size>");
                cell.widget.label.color = cell.quantity > 0 ? Ink : Muted;
                SetButtonArt(cell.widget, "item_" + cell.id);
                cell.widget.icon.color = cell.id == (int)ItemKind.Stone ? new Color(.75f,.79f,.75f) :
                    cell.id == (int)ItemKind.Clay ? new Color(1f,.55f,.32f) :
                    cell.id == (int)ItemKind.Charcoal ? new Color(.38f,.42f,.39f) :
                    cell.id == (int)ItemKind.Brick ? new Color(1f,.48f,.30f) : Color.white;
            }
            if (player.gear != null) foreach (GearState gear in player.gear)
            {
                if (gear == null) continue;
                InventoryCell cell = gearCells.Find(c => c.id == gear.id);
                if (cell == null)
                {
                    int id = gear.id;
                    cell = MakeInventoryCell("Gear Slot " + id, gearGrid, id, EquipmentCatalog.GearNames[(int)gear.kind],
                        GearCategory(gear.kind), () => { selectedGear = id; SelectInventoryDetail(1); });
                    gearCells.Add(cell);
                }
                cell.title = EquipmentCatalog.GearNames[(int)gear.kind];
                cell.category = GearCategory(gear.kind);
                cell.quantity = 1;
                string status = IsWorn(player, gear) ? "防具已穿戴" : player.weaponId == gear.id ? "武器位" : player.toolId == gear.id ? "工具位" : "背包";
                Set(cell.widget.label, cell.title + "  #" + gear.id + "\n耐久 " + gear.durability + "%" +
                    (EquipmentCatalog.IsGun(gear.kind) ? " · " + gear.magazine + "/" + EquipmentCatalog.Stats(gear).magazine : "") + "\n<size=11>" + status + "</size>");
                cell.widget.label.color = gear.durability <= 0 ? Danger : Ink;
                SetButtonArt(cell.widget, "gear_" + (int)gear.kind);
            }
            foreach (InventoryCell cell in partCells)
            {
                cell.quantity = Count(player.parts, cell.id);
                Set(cell.widget.label, cell.title + "\n× " + cell.quantity + "\n<size=11>" + EquipmentCatalog.SlotNames[cell.category - 1] + "</size>");
                cell.widget.label.color = cell.quantity > 0 ? Ink : Muted;
                SetButtonArt(cell.widget, "part_" + cell.id);
            }
            if (inventoryTab == 3) { inventoryEmpty.gameObject.SetActive(false); return; }
            List<InventoryCell> cells = inventoryTab == 0 ? materialCells : inventoryTab == 1 ? gearCells : partCells;
            visibleCells.Clear();
            int owned = 0;
            foreach (InventoryCell cell in cells)
            {
                bool exists = inventoryTab != 1 || EquipmentCatalog.Find(player, cell.id) != null;
                bool visible = exists && (inventoryFilter == 0 || cell.category == inventoryFilter);
                cell.widget.button.gameObject.SetActive(visible);
                if (exists && cell.quantity > 0) owned++;
                if (!visible) continue;
                visibleCells.Add(cell);
                bool selected = inventoryDetailKind == inventoryTab && cell.id == (inventoryTab == 0 ? selectedMaterial : inventoryTab == 1 ? selectedGear : selectedPart);
                Tint(cell.widget, selected ? Selected : Raised);
            }
            visibleCells.Sort((a, b) =>
            {
                int comparison = inventorySort == 0 ? a.category.CompareTo(b.category) : inventorySort == 1 ? string.Compare(a.title, b.title, StringComparison.Ordinal) : b.quantity.CompareTo(a.quantity);
                return comparison != 0 ? comparison : a.id.CompareTo(b.id);
            });
            for (int i = 0; i < visibleCells.Count; i++)
                if (visibleCells[i].widget.button.transform.GetSiblingIndex() != i) visibleCells[i].widget.button.transform.SetSiblingIndex(i);
            Set(inventorySummary, "持有 " + owned + (inventoryTab == 1 ? " 件装备" : " 类") + " · 显示 " + visibleCells.Count + " / " + (inventoryTab == 1 ? (player.gear == null ? 0 : player.gear.Count) : cells.Count));
            inventoryEmpty.gameObject.SetActive(visibleCells.Count == 0);
            Set(inventoryEmpty, inventoryTab == 1 ? "暂无此类装备。\n切换「制作」查看装备门槛与成本。" : "此分类暂无物品。");
        }

        void RefreshItemDetails(WorldState world, PlayerState player, GearState target, bool bench)
        {
            bool hasGear = inventoryDetailKind != 0 && target != null;
            bool gun = hasGear && EquipmentCatalog.IsGun(target.kind);
            bool armor = hasGear && EquipmentCatalog.IsArmor(target.kind);
            foreach (GameObject item in gearDetailObjects) item.SetActive(hasGear);
            foreach (GameObject item in gunDetailObjects) item.SetActive(gun);
            foreach (GameObject item in armorDetailObjects) item.SetActive(armor);
            if (inventoryDetailKind == 0)
            {
                Set(itemTitle, DemoCatalog.ItemNames[selectedMaterial]);
                SetFlowText(itemMeta, "数量：" + Count(player.items, selectedMaterial) + " / " + WorldSimulator.InventoryLimit + " · " + MaterialCategoryName(MaterialCategory(selectedMaterial)));
                SetFlowText(itemUsage, MaterialUsage(selectedMaterial));
                return;
            }
            if (inventoryDetailKind == 2)
            {
                PartDefinition part = EquipmentCatalog.Part((PartKind)selectedPart);
                Set(itemTitle, part.name);
                SetFlowText(itemMeta, "散件数量：" + Count(player.parts, selectedPart) + " · 插槽：" + EquipmentCatalog.SlotNames[(int)part.slot] + " · " + TechCatalog.EraName(part.era));
                SetFlowText(itemUsage, part.description + "\n装配目标：" + (target == null ? "请先在装备页选择一件装备。" : EquipmentCatalog.GearNames[(int)target.kind] + " #" + target.id) +
                    "\n先在装备页选中目标，再在此页点击对应安装按钮。");
            }
            else
            {
                Set(itemTitle, target == null ? "尚未选择装备" : EquipmentCatalog.GearNames[(int)target.kind] + "  #" + target.id);
                SetFlowText(itemMeta, target == null ? "在左侧选择装备；制作页可查看全部装备和条件。" : "数量：1 · 耐久：" + target.durability + " / " + EquipmentCatalog.Gear(target.kind).maxDurability +
                    " · " + (IsWorn(player, target) ? "防具已穿戴" : player.weaponId == target.id ? "已装备武器位" : player.toolId == target.id ? "已装备工具位" : "存放于背包"));
                SetFlowText(itemUsage, target == null ? "回收后会清除选择，避免误操作其他装备。" : GearUsage(target.kind));
            }
            if (!hasGear) return;
            WeaponStats stats = EquipmentCatalog.Stats(target);
            GearDefinition definition = EquipmentCatalog.Gear(target.kind);
            bool active = armor ? IsWorn(player, target) : EquipmentCatalog.Equipped(player)?.id == target.id;
            bool alive = player.hp > 0f && player.connected;
            bool ready = alive && player.interactCooldown <= 0f;
            bool loading = player.reloadRemaining > 0f;
            string combat = "伤害 " + stats.damage.ToString("0.#") + (stats.pellets > 1 ? " × " + stats.pellets + " 弹丸" : "") +
                " · 射程 " + stats.range.ToString("0.##") + " 格\n" +
                (gun ? "射速 " : "动作频率 ") + (1f / Mathf.Max(.01f, stats.interval)).ToString("0.##") + " /秒 · 间隔 " + stats.interval.ToString("0.##") + " 秒";
            if (gun) combat += "\n精度散布 ±" + (stats.spread * Mathf.Rad2Deg).ToString("0.##") + "°（越低越准） · 后坐 " + (stats.recoil * Mathf.Rad2Deg).ToString("0.##") +
                "°\n弹匣 " + target.magazine + " / " + stats.magazine + " · 装填 " + stats.reload.ToString("0.##") + " 秒\n已装：" + (target.magazine > 0 ? EquipmentCatalog.AmmoName(target.loadedAmmo) : "空") +
                " · 下次：" + EquipmentCatalog.AmmoName(ReloadAmmo(player, target)) + " · 备弹 " + Count(player.items, (int)ReloadAmmo(player, target));
            if (armor)
            {
                GearState worn = EquipmentCatalog.Find(player, Count(player.armorIds, EquipmentCatalog.ArmorSlot(target.kind)));
                float previousArmor = worn == null || worn.durability <= 0 ? 0f : EquipmentCatalog.Gear(worn.kind).armor;
                float effectiveArmor = target.durability <= 0 ? 0f : definition.armor;
                float previousPenalty = worn == null || worn.durability <= 0 ? 0f : EquipmentCatalog.Gear(worn.kind).movePenalty;
                float effectivePenalty = target.durability <= 0 ? 0f : definition.movePenalty;
                combat = "防具护甲 " + effectiveArmor.ToString("0.#") + (target.durability <= 0 ? "（已损坏，无防御）" : "") +
                    " · 移动 -" + (effectivePenalty * 100f).ToString("0.#") + "%\n同槽已穿：" + (worn == null ? "空" : EquipmentCatalog.GearNames[(int)worn.kind] + " · 耐久 " + worn.durability + "%") +
                    "\n替换护甲差 " + (effectiveArmor - previousArmor).ToString("+0.#;-0.#;0") +
                    " · 移动惩罚差 " + ((effectivePenalty - previousPenalty) * 100f).ToString("+0.#;-0.#;0") + "%" +
                    "\n全身有效护甲 " + EquipmentCatalog.ArmorRating(player).ToString("0.#") + " · 总移动惩罚 " + (EquipmentCatalog.MovementPenalty(player) * 100f).ToString("0.#") + "%（上限25%）" +
                    "\n防具独立穿戴；不切换当前武器或工具。";
            }
            SetFlowText(itemStats, combat + (armor ? "" : "\n属性已计入配件；命中与成长修正由核心结算。"));
            equipSelected.button.interactable = alive && !active;
            Set(equipSelected.label, active ? armor ? "已穿戴" : "当前已启用" : armor ? "穿戴所选防具" : definition.tool ? "装备 / 切至工具" : "装备 / 切至武器");
            unequipSelected.button.interactable = alive && armor && active;
            bool stone = target.kind == GearKind.StoneAxe || target.kind == GearKind.StonePickaxe;
            bool repairMaterials = stone ? Count(player.items, (int)ItemKind.Wood) >= 1 && Count(player.items, (int)ItemKind.Ore) >= 1 :
                Count(player.items, (int)ItemKind.Iron) >= 2 && Count(player.items, (int)ItemKind.Scrap) >= 1;
            repairGear.button.interactable = ready && bench && target.durability < definition.maxDurability && repairMaterials;
            Set(repairGear.label, stone ? "维修 +25 · 木1 矿1" : "维修 +25 · 铁2 废1");
            bool salvageSpace = HasSalvageSpace(player, target);
            salvageGear.button.interactable = ready && bench && salvageSpace;
            Set(salvageGear.label, !salvageSpace ? "回收返还空间不足" : "回收装备");
            SetFlowText(equipmentHint, (loading ? "装填中：" + player.reloadRemaining.ToString("0.0") + " 秒；切换装备或操作装备将取消装填。\n" : "") +
                (bench ? "工作台就绪。" : "维修 / 装配 / 回收需可见工作台。") + "维修恢复 25 耐久；装填作用于当前武器，弹种切换前需卸弹。");
            if (!gun) return;
            ItemKind nextAmmo = ReloadAmmo(player, target);
            bool selectedCompatible = EquipmentCatalog.AmmoCompatible(target.kind, player.selectedAmmo);
            bool sameAmmo = target.magazine == 0 || target.loadedAmmo == player.selectedAmmo;
            reloadGear.button.interactable = ready && active && !loading && target.durability > 0 && target.magazine < stats.magazine && selectedCompatible && sameAmmo && Count(player.items, (int)nextAmmo) > 0;
            Set(reloadGear.label, loading && player.reloadingGearId == target.id ? "装填中 " + player.reloadRemaining.ToString("0.0") + "s" : !active ? "先装备为当前武器" : !sameAmmo ? "先卸弹再换弹种" : "装填 R");
            unloadGear.button.interactable = ready && target.magazine > 0 && Count(player.items, (int)target.loadedAmmo) + target.magazine <= WorldSimulator.InventoryLimit;
            for (int i = 0; i < ammoButtons.Count; i++)
            {
                ItemKind ammo = inventoryAmmo[i];
                ammoButtons[i].button.interactable = alive && active && target.magazine == 0 && EquipmentCatalog.AmmoCompatible(target.kind, ammo);
                Set(ammoButtons[i].label, EquipmentCatalog.AmmoName(ammo) + "\n×" + Count(player.items, (int)ammo));
                Tint(ammoButtons[i], player.selectedAmmo == ammo ? Selected : Raised);
            }
            for (int i = 0; i < detachButtons.Count; i++)
            {
                int value = AttachedPart(target, i);
                Set(attachmentLabels[i], EquipmentCatalog.SlotNames[i] + "：" + (value < 0 ? "空" : EquipmentCatalog.PartNames[value]));
                bool capacity = i != (int)AttachmentSlot.Magazine || target.magazine <= definition.magazine;
                detachButtons[i].button.interactable = ready && bench && value >= 0 && capacity && Count(player.parts, value) < WorldSimulator.InventoryLimit;
                Set(detachButtons[i].label, !capacity ? "先卸弹" : "卸下");
            }
            for (int i = 0; i < installButtons.Count; i++)
            {
                PartDefinition part = EquipmentCatalog.Part((PartKind)i);
                bool compatible = EquipmentCatalog.PartCompatible(target.kind, (PartKind)i);
                bool empty = AttachedPart(target, (int)part.slot) < 0;
                installButtons[i].button.interactable = ready && bench && world.era >= part.era && compatible && empty && Count(player.parts, i) > 0;
                Set(installButtons[i].label, part.name + " ×" + Count(player.parts, i) + (!compatible ? " · 不兼容" : !empty ? " · 槽已占" : " · 安装"));
                Tint(installButtons[i], inventoryDetailKind == 2 && selectedPart == i ? Selected : Raised);
            }
        }

        static bool HasSalvageSpace(PlayerState player, GearState gear)
        {
            int[] cost = EquipmentCatalog.Gear(gear.kind).cost;
            for (int i = 0; i < DemoCatalog.ItemCount; i++)
            {
                int returned = Count(cost, i) / 2 + (gear.magazine > 0 && (int)gear.loadedAmmo == i ? gear.magazine : 0);
                if (returned > WorldSimulator.InventoryLimit - Count(player.items, i)) return false;
            }
            for (int i = 0; i < EquipmentCatalog.PartNames.Length; i++)
            {
                int returned = 0;
                if (gear.attachments != null) foreach (int part in gear.attachments) if (part == i) returned++;
                if (returned > WorldSimulator.InventoryLimit - Count(player.parts, i)) return false;
            }
            return true;
        }

        void RefreshInventoryRecipes(WorldState world, PlayerState player, bool bench)
        {
            int count = 0;
            foreach (RecipeRow row in manualRecipes)
            {
                bool visible = inventoryFilter == 0 || inventoryFilter == 1;
                row.craft.button.transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;
                count++;
                RecipeDefinition recipe = row.recipe;
                bool era = PrimitiveCatalog.IsRecipeUnlocked(world, recipe), needsBench = recipe.id > 1;
                bool materials = CanAfford(player.items, recipe.inputs), space = HasRecipeSpace(player.items, recipe);
                string state = !era ? PrimitiveCatalog.RecipeRequirement(world, recipe) : needsBench && !bench ? "需工作台 / 视线" : !materials ? "缺少材料" : !space ? "背包已满" : player.interactCooldown > 0f ? "操作冷却中" : "点击制作";
                row.craft.button.interactable = player.hp > 0f && player.connected && era && (!needsBench || bench) && materials && space && player.interactCooldown <= 0f;
                Set(row.craft.label, recipe.name + "\n" + state);
                SetRecipeText(row.details, "输入：" + MaterialText(recipe.inputs, player.items, true) + "\n产出：" + MaterialText(recipe.outputs) +
                    "\n" + TechCatalog.EraName(recipe.era) + (needsBench ? " · 工作台" : " · 随身") + " · " + state);
            }
            foreach (EquipmentRecipeRow row in equipmentRecipes)
            {
                bool visible = inventoryFilter == 0 || inventoryFilter == (row.part ? 3 : 2);
                row.craft.button.transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;
                count++;
                PartDefinition part = row.part ? EquipmentCatalog.Part((PartKind)row.kind) : null;
                GearDefinition gear = row.part ? null : EquipmentCatalog.Gear((GearKind)row.kind);
                string name = row.part ? part.name : gear.name;
                TechEra era = row.part ? part.era : gear.era;
                int[] cost = row.part ? part.cost : gear.cost;
                bool primitive = row.part || PrimitiveCatalog.IsGearUnlocked(world, (GearKind)row.kind);
                bool canCraft = primitive && (row.part ? EquipmentCatalog.CanCraftPart(world, player, (PartKind)row.kind) : EquipmentCatalog.CanCraft(world, player, (GearKind)row.kind));
                string reason = world.era < era ? "需" + TechCatalog.EraName(era) : !primitive ? "需原始工艺：" +
                    PrimitiveCatalog.Definition(gear.kind == GearKind.Knife ? PrimitiveTech.Bloomery : PrimitiveTech.Tools).name :
                    !bench ? "需工作台 / 视线" : !CanAfford(player.items, cost) ? "缺少材料" : !canCraft ? "背包容量 / 状态受限" : player.interactCooldown > 0f ? "操作冷却中" : "点击制作";
                row.craft.button.interactable = canCraft && player.interactCooldown <= 0f;
                Set(row.craft.label, name + "\n" + reason);
                SetRecipeText(row.details, "成本：" + MaterialText(cost, player.items, true) + "\n" + TechCatalog.EraName(era) + " · 工作台 · " + reason +
                    "\n" + (row.part ? EquipmentCatalog.SlotNames[(int)part.slot] + " · " + part.description : EquipmentCatalog.IsArmor(gear.kind) ? "防具位 · " + GearUsage(gear.kind) : gear.tool ? "工具位 · " + GearUsage(gear.kind) : "武器位 · " + GearUsage(gear.kind)));
            }
            Set(inventorySummary, "配方 " + count + " 项 · 持有 / 需求");
        }

        static void SetRecipeText(TextMeshProUGUI details, string value)
        {
            Set(details, value);
            float height = Mathf.Max(112, details.GetPreferredValues(value, 362, float.PositiveInfinity).y + 12);
            details.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 8);
            details.transform.parent.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
        }

        void CraftEquipment(int kind, bool part)
        {
            if (bridge.World == null || bridge.LocalPlayer == null || inventoryTab != 3) return;
            bool allowed = part ? EquipmentCatalog.CanCraftPart(bridge.World, bridge.LocalPlayer, (PartKind)kind) : EquipmentCatalog.CanCraft(bridge.World, bridge.LocalPlayer, (GearKind)kind);
            if (!part) allowed &= PrimitiveCatalog.IsGearUnlocked(bridge.World, (GearKind)kind);
            if (allowed && bridge.LocalPlayer.interactCooldown <= 0f) InventoryCommand(part ? "partcraft" : "gearcraft", kind);
        }

        void EquipSelectedGear()
        {
            InventoryCommand("equip", selectedGear);
        }

        void InventoryCommand(string action, int arg = 0, int? detail = null)
        {
            if (!inventory.gameObject.activeSelf || bridge.World == null || bridge.LocalPlayer == null || bridge.LocalPlayer.hp <= 0f) return;
            GearState target = EquipmentCatalog.Find(bridge.LocalPlayer, selectedGear);
            RefreshItemDetails(bridge.World, bridge.LocalPlayer, target, EquipmentCatalog.HasWorkbench(bridge.World, bridge.LocalPlayer));
            UiButton gate = action == "equip" ? equipSelected : action == "unequip" ? unequipSelected : action == "reload" ? reloadGear : action == "unload" ? unloadGear : action == "gearrepair" ? repairGear : action == "gearsalvage" ? salvageGear :
                action == "attach" && detail.HasValue ? installButtons[detail.Value] : action == "detach" && detail.HasValue ? detachButtons[detail.Value] : null;
            if (gate != null && (target == null || !gate.button.interactable)) return;
            if (action == "ammoselect")
            {
                int index = inventoryAmmo.IndexOf((ItemKind)arg);
                if (target == null || index < 0 || !ammoButtons[index].button.interactable) return;
            }
            Send(action, arg, detail);
        }

        static int AttachedPart(GearState gear, int slot) => gear?.attachments != null && slot >= 0 && slot < gear.attachments.Length ? gear.attachments[slot] : -1;
        static ItemKind ReloadAmmo(PlayerState player, GearState gear) => EquipmentCatalog.AmmoCompatible(gear.kind, player.selectedAmmo) ? player.selectedAmmo : EquipmentCatalog.Gear(gear.kind).ammo;
        static int GearCategory(GearKind kind) => EquipmentCatalog.IsArmor(kind) ? 3 : EquipmentCatalog.Gear(kind).tool ? 2 : 1;
        static bool IsWorn(PlayerState player, GearState gear)
        {
            int slot = gear == null ? -1 : EquipmentCatalog.ArmorSlot(gear.kind);
            return slot >= 0 && Count(player.armorIds, slot) == gear.id;
        }

        string InventoryFilterName()
        {
            if (inventoryFilter == 0) return "全部";
            if (inventoryTab == 0) return MaterialCategoryName(inventoryFilter);
            if (inventoryTab == 1) return inventoryFilter == 1 ? "武器" : inventoryFilter == 2 ? "工具" : "防具";
            if (inventoryTab == 2) return EquipmentCatalog.SlotNames[inventoryFilter - 1];
            return inventoryFilter == 1 ? "手工" : inventoryFilter == 2 ? "装备" : "配件";
        }

        static int MaterialCategory(int item)
        {
            if (EquipmentCatalog.IsAmmo((ItemKind)item)) return 3;
            if (item == (int)ItemKind.Food || item == (int)ItemKind.Fuel || item == (int)ItemKind.Charcoal || item >= (int)ItemKind.NuclearFuel && item <= (int)ItemKind.NuclearWaste) return 4;
            if (item == (int)ItemKind.Wood || item == (int)ItemKind.Ore || item == (int)ItemKind.CopperOre || item == (int)ItemKind.UraniumOre || item == (int)ItemKind.TinOre || item == (int)ItemKind.Stone || item == (int)ItemKind.Clay) return 1;
            return 2;
        }

        static string MaterialCategoryName(int category) => category == 1 ? "原料" : category == 2 ? "加工材料" : category == 3 ? "弹药" : "补给 / 能源";

        static string GearUsage(GearKind kind)
        {
            if (EquipmentCatalog.IsArmor(kind)) return "防具 · 独立装备格；耐久耗尽后无防御。可在工作台维修或回收。";
            string usage;
            switch (kind)
            {
                case GearKind.StoneAxe: case GearKind.SteelAxe: return "工具 · 伐木与近战；使用消耗耐久。";
                case GearKind.StonePickaxe: case GearKind.SteelPickaxe: return "工具 · 采矿与近战；使用消耗耐久。";
                case GearKind.Wrench: return "工具 · 设备维护与近战；使用消耗耐久。";
                case GearKind.Knife: usage = "近战武器 · 不消耗弹药；使用消耗耐久。"; break;
                default: usage = "远程武器 · 先装备、选择兼容弹种并装填；右键瞄准，R 装填。"; break;
            }
            string description = EquipmentCatalog.Gear(kind).description;
            return description.Length == 0 ? usage : description + "\n" + usage;
        }

        static string MaterialUsage(int item)
        {
            ItemKind kind = (ItemKind)item;
            if (EquipmentCatalog.IsAmmo(kind))
            {
                var compatible = new List<string>();
                for (int i = 0; i < EquipmentCatalog.GearNames.Length; i++)
                    if (EquipmentCatalog.AmmoCompatible((GearKind)i, kind)) compatible.Add(EquipmentCatalog.GearNames[i]);
                string caliber = kind == ItemKind.Ammo ? "对应9×19mm手枪弹；机枪塔沿用此弹药。" :
                    kind == ItemKind.RifleAmmo ? "对应5.56×45mm步枪弹。" :
                    kind == ItemKind.Shell ? "对应12号霰弹。" :
                    kind == ItemKind.PrecisionAmmo ? "对应7.62×51mm狙击步枪弹。" :
                    "穿甲弹库存按通用类别管理，使用时对应各步枪口径。";
                return "弹药用途：" + string.Join("、", compatible) + "。\n" + caliber +
                    "\n在装备详情中选择弹种并装填；切换已装弹种前先卸弹。";
            }
            if (kind == ItemKind.Food) return "食物补充饱食度；点击下方「吃食 F」或关闭面板后按 F。";
            if (kind == ItemKind.Fuel) return "供燃料发电、锅炉与石炉使用；也用于炼钢和化工。";
            if (kind == ItemKind.Stone) return "原始石料。采集石料堆获得，用于工具、窑炉及建造工艺；按 T 查看研究成本。";
            if (kind == ItemKind.Clay) return "黏土。采集黏土矿床获得；陶窑投入黏土 4、木炭 1，产出砖 4。";
            if (kind == ItemKind.Charcoal) return "木炭。炭窑投入木材 4，产出木炭 2；用于陶窑烧砖与原始冶金。";
            if (kind == ItemKind.Brick) return "砖。陶窑以黏土和木炭烧制，用于建筑及工艺研究；生产验证以实际产出计数。";
            if (kind == ItemKind.NuclearWaste) return "反应堆产物。及时取出并妥善储存，避免设备库存阻塞。";
            var uses = new List<string>();
            foreach (RecipeDefinition recipe in TechCatalog.Recipes) if (Count(recipe.inputs, item) > 0 && !uses.Contains(recipe.name)) uses.Add(recipe.name);
            bool construction = false, equipment = false;
            for (int i = 0; i < DemoCatalog.BuildNames.Length; i++) if (Count(DemoCatalog.BuildCost((BuildKind)i), item) > 0) { construction = true; break; }
            for (int i = 0; i < EquipmentCatalog.GearNames.Length; i++) if (Count(EquipmentCatalog.Gear((GearKind)i).cost, item) > 0) { equipment = true; break; }
            if (!equipment) for (int i = 0; i < EquipmentCatalog.PartNames.Length; i++) if (Count(EquipmentCatalog.Part((PartKind)i).cost, item) > 0) { equipment = true; break; }
            return (construction ? "建造材料。" : "工业材料。") + (equipment ? "可制作装备 / 配件。" : "") +
                (uses.Count > 0 ? "\n配方：" + string.Join("、", uses) : "") + "\n制作页查看精确门槛与持有 / 需求；T 查看科技和建造。";
        }
    }
}
