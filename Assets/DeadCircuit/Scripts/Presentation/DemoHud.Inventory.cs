using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        enum InventorySupply { Material, Food, Seed, Water }
        // Values are passed in PlayerCommand.x for packput and packtake.
        enum PackItemKind { Material = 0, Part = 1, Food = 2, Seed = 3, RawWater = 4, CleanWater = 5, Gear = 6 }

        sealed class InventoryCell
        {
            public int id, category, quantity;
            public string title;
            public InventorySupply supply;
            public GearKind? gearKind;
            public UiButton widget;
        }

        sealed class EquipmentRecipeRow
        {
            public int kind;
            public bool part;
            public UiButton craft;
            public UiButton research;
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
        GameObject gearActions, unequipActions, packTransferActions;
        const int BackpackFilter = 4;
        const float InventoryPreviewMaxScale = 256f;
        static readonly Color InventoryGraphite = new Color32(23, 28, 33, 255);
        static readonly Color InventoryInset = new Color32(17, 22, 26, 255);
        static readonly Color InventoryCyan = new Color32(137, 219, 226, 255);
        static readonly Color InventoryBlue = new Color32(146, 196, 241, 255);
        static readonly Color InventoryRed = new Color32(240, 148, 143, 255);
        static readonly Color InventoryCellIdle = new Color32(30, 46, 51, 255);
        static readonly Color InventoryCellSelected = new Color32(43, 84, 93, 255);
        static readonly Color InventorySocketIdle = new Color32(32, 49, 69, 255);
        static readonly Color InventorySocketSelected = new Color32(53, 83, 115, 255);
        static readonly Color InventoryArmorIdle = new Color32(64, 37, 42, 255);
        static readonly Color InventoryArmorSelected = new Color32(102, 48, 52, 255);
        static readonly Color InventoryCraftIdle = new Color32(62, 48, 30, 255);
        static readonly string[] InventoryArmorNames = { "头部", "躯干", "腿部", "脚部" };
        readonly SurvivorLayer[] inventoryPreviewLayers = new SurvivorLayer[SurvivorVisuals.Capacity];
        readonly UnityEngine.UI.Image[] inventoryPreviewImages = new UnityEngine.UI.Image[SurvivorVisuals.Capacity];
        RectTransform materialGrid, gearGrid, partGrid, inventoryRecipes;
        RectTransform inventoryPreviewOrigin, inventoryFootRule;
        UnityEngine.UI.ScrollRect materialScroll, gearScroll, partScroll, craftingScroll, itemDetailScroll;
        TextMeshProUGUI equipmentSummary, inventorySummary, inventoryEmpty, inventorySelection;
        TextMeshProUGUI inventoryPreviewEmpty, inventoryWeightValue, craftSummary;
        UnityEngine.UI.Image inventoryWeightFill;
        UiButton craftFilterButton, storeItem, backpackSlot, packPut, packTake;
        TextMeshProUGUI itemTitle, itemMeta, itemUsage, itemStats, equipmentHint, armorSummary, salvageWarning;
        UiButton sortInventory, filterInventory, equipSelected, unequipSelected, repairGear, reloadGear, unloadGear, salvageGear, itemResearch;
        int inventoryTab, inventorySort, inventoryFilter, craftFilter, selectedMaterial = -1, selectedGear = -1, selectedPart = -1;
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
            inventory = Backdrop("Inventory Panel", root, new Color32(6, 9, 12, 231));
            RectTransform card = Box("Inventory Card", inventory, 0, 0, 1216, 688, InventoryGraphite, true);
            Center(card, 1216, 688);
            PanelRule(card, 1216, 58);
            Label("Inventory Heading", card, "背包与装备", 24, 12, 350, 36, 25, Ink, FontStyles.Bold);
            backpackSlot = Button("Backpack Equipment Slot", card, "背包位 · 空", 394, 10, 244, 40, BrowseBackpackSlot);
            SetButtonArt(backpackSlot, "ui_inventory", 28);
            backpackSlot.label.fontSizeMax = 14;
            backpackSlot.label.fontSizeMin = 12;
            inventoryWeightValue = Label("Inventory Weight Value", card, "", 650, 10, 392, 28, 17, InventoryCyan, FontStyles.Bold);
            RectTransform track = Box("Inventory Weight Track", card, 650, 43, 392, 6, InventoryInset);
            RectTransform fill = Box("Inventory Weight Fill", track, 0, 0, 392, 6, InventoryCyan);
            Stretch(fill);
            inventoryWeightFill = fill.GetComponent<UnityEngine.UI.Image>();

            UiButton closeInventory = Button("Close Inventory", card, "返回", 1070, 16, 120, 32, () => SetPanel(inventory, false));
            SetButtonArt(closeInventory, "ui_back", 20);

            Box("Inventory Browser Surface", card, 14, 70, 318, 546, InventoryInset);
            MakeInventoryCraftColumn(card);
            RectTransform preview = Box("Character Preview Surface", card, 342, 70, 430, 334, InventoryInset);
            MakeInventorySurvivor(preview);
            Box("Inventory Detail Surface", card, 342, 412, 430, 204, InventoryInset);
            Label("Inventory Detail Heading", card, "物品详情", 356, 418, 402, 24, 16, InventoryCyan, FontStyles.Bold);
            MakeItemDetails(card);
            Box("Equipment Slots Surface", card, 782, 70, 420, 130, InventoryInset);
            MakeInventoryEquipmentSlots(card);
            Box("Inventory Backpack Surface", card, 782, 208, 420, 408, InventoryInset);
            MakeInventoryBackpack(card);

            Box("Inventory Footer Rule", card, 24, 622, 1166, 1, Edge);
            eatButton = Button("Eat", card, "进食 V", 24, 630, 130, 34, () => Send("eat"));
            SetButtonArt(eatButton, "ui_eat", 22);
            workbenchHint = Label("Workbench Requirement", card, "", 170, 626, 1020, 23, 13, Muted);
            inventoryNotice = Label("Inventory Feedback", card, "", 170, 650, 1020, 26, 14, Amber);
            inventory.gameObject.SetActive(false);
        }

        void MakeInventoryCraftColumn(RectTransform card)
        {
            Label("Inventory Craft Heading", card, "快捷制作", 26, 78, 190, 30, 17, Amber, FontStyles.Bold);
            inventoryTabs[3] = Button("Inventory Tab Craft", card, "制作", 226, 78, 94, 30, () => BrowseInventoryTab(3));
            SetButtonArt(inventoryTabs[3], "ui_craft", 20);
            Tint(inventoryTabs[3], InventoryCraftIdle);
            craftFilterButton = Button("Craft Category Filter", card, "配方：全部", 26, 114, 150, 30, () =>
            {
                craftFilter = (craftFilter + 1) % 4;
                ResetScroll(craftingScroll);
                RefreshAll();
            });
            Tint(craftFilterButton, InventoryCraftIdle);
            craftSummary = Label("Inventory Craft Summary", card, "", 184, 114, 134, 30, 12, Muted);
            inventoryRecipes = ScrollContent("Craft Scroll View", card, 24, 150, 298, 456, out craftingScroll);
            StyleInventoryScroll(craftingScroll, Amber);
            MakeInventoryRecipes();
        }

        void MakeInventorySurvivor(RectTransform preview)
        {
            Label("Character Preview Heading", preview, "幸存者", 14, 8, 228, 28, 17, Ink, FontStyles.Bold);
            Label("Armor Slots Heading", preview, "穿戴", 318, 8, 98, 28, 17, InventoryRed, FontStyles.Bold);
            RectTransform stage = Box("Inventory Preview Stage", preview, 120, 44, 190, 258, InventoryGraphite);
            stage.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            inventoryFootRule = Box("Inventory Preview Foot Rule", stage, 24, 238, 142, 1, Edge);
            inventoryPreviewEmpty = Label("Inventory Preview Empty", stage, "角色信息同步中", 12, 101, 166, 56, 15, Muted);
            inventoryPreviewEmpty.alignment = TextAlignmentOptions.Center;
            inventoryPreviewOrigin = NewRect("Inventory Preview Origin", stage);
            inventoryPreviewOrigin.anchorMin = inventoryPreviewOrigin.anchorMax = new Vector2(.5f, 0f);
            inventoryPreviewOrigin.pivot = new Vector2(.5f, 0f);
            inventoryPreviewOrigin.anchoredPosition = new Vector2(0f, 20f);
            inventoryPreviewOrigin.sizeDelta = Vector2.zero;
            for (int i = 0; i < inventoryPreviewImages.Length; i++)
            {
                RectTransform layer = NewRect("Inventory Survivor Layer " + i, inventoryPreviewOrigin);
                layer.anchorMin = layer.anchorMax = Vector2.zero;
                var image = layer.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.type = UnityEngine.UI.Image.Type.Simple;
                image.useSpriteMesh = false;
                image.raycastTarget = false;
                image.enabled = false;
                inventoryPreviewImages[i] = image;
            }
            for (int i = 0; i < armorSlots.Length; i++)
            {
                int slot = i;
                armorSlots[i] = Button("Armor Slot " + i, preview, "", i < 2 ? 14 : 318, 55 + i % 2 * 119, 98, 106, () => BrowseArmorSlot(slot));
                armorSlots[i].label.fontSizeMax = 14;
                armorSlots[i].label.fontSizeMin = 12;
            }
            armorSummary = Label("Armor Summary", preview, "", 14, 303, 402, 24, 13, InventoryRed);
        }

        void MakeInventoryEquipmentSlots(RectTransform card)
        {
            Label("Equipment Slots Heading", card, "随身装备", 794, 78, 142, 24, 17, InventoryBlue, FontStyles.Bold);
            inventorySelection = Label("Equipment Selection", card, "", 940, 76, 250, 28, 13, Muted);
            inventorySelection.alignment = TextAlignmentOptions.MidlineRight;
            equipmentSlots[0] = Button("Weapon Equipment Slot", card, "主武器", 794, 108, 292, 38, () => BrowseEquipmentSlot(0));
            equipmentSlots[1] = Button("Tool Equipment Slot", card, "副手工具", 794, 152, 292, 38, () => BrowseEquipmentSlot(1));
            equipmentSlots[2] = Button("Unarmed Equipment Slot", card, "徒手", 1094, 108, 96, 82, () => InventoryCommand("switch", 2));
            for (int i = 0; i < 2; i++)
            {
                equipmentSlots[i].label.fontSizeMax = 14;
                equipmentSlots[i].label.fontSizeMin = 12;
            }
        }

        void MakeInventoryBackpack(RectTransform card)
        {
            Label("Inventory Browser Heading", card, "背包物品", 794, 215, 150, 26, 17, InventoryCyan, FontStyles.Bold);
            inventorySummary = Label("Inventory Summary", card, "", 952, 215, 238, 26, 13, Muted);
            inventorySummary.alignment = TextAlignmentOptions.MidlineRight;
            string[] names = { "Materials", "Equipment", "Parts" };
            string[] titles = { "材料 / 补给", "未装备", "配件" };
            string[] icons = { "ui_inventory", "ui_equip", "ui_attach" };
            for (int i = 0; i < names.Length; i++)
            {
                int tab = i;
                inventoryTabs[i] = Button("Inventory Tab " + names[i], card, titles[i], 794 + i * 135, 244, 126, 30, () => BrowseInventoryTab(tab));
                SetButtonArt(inventoryTabs[i], icons[i], 20);
            }
            sortInventory = Button("Inventory Sort", card, "排序：分类", 794, 281, 161, 31,
                () => { inventorySort = (inventorySort + 1) % 3; RefreshAll(); });
            SetButtonArt(sortInventory, "ui_refresh", 20);
            Tint(sortInventory, InventoryCellIdle);
            filterInventory = Button("Inventory Category Filter", card, "分类：全部", 963, 281, 227, 31, CycleInventoryFilter);
            SetButtonArt(filterInventory, "ui_inspect", 18);
            Tint(filterInventory, InventoryCellIdle);
            materialGrid = MakeInventoryGrid("Inventory Scroll View", card, out materialScroll);
            gearGrid = MakeInventoryGrid("Equipment Scroll View", card, out gearScroll);
            partGrid = MakeInventoryGrid("Parts Scroll View", card, out partScroll);
            inventoryEmpty = Label("Inventory Empty", card, "", 802, 412, 380, 72, 15, Muted);
            inventoryEmpty.alignment = TextAlignmentOptions.Center;
            for (int i = 0; i < DemoCatalog.ItemNames.Length; i++)
                AddInventoryMaterial("Inventory Item " + i, i, DemoCatalog.ItemNames[i], MaterialCategory(i), InventorySupply.Material, "item_" + i);
            for (int i = 0; i < SurvivalCatalog.FoodCount; i++)
                AddInventoryMaterial("Inventory Food " + i, i, SurvivalCatalog.Food(i).name, 4, InventorySupply.Food, "food_" + i);
            for (int i = 0; i < SurvivalCatalog.CropCount; i++)
                AddInventoryMaterial("Inventory Seed " + i, i, SurvivalCatalog.Crop(i).name + "种子", 5, InventorySupply.Seed, "ui_seed");
            AddInventoryMaterial("Inventory Raw Water", 0, "原水", 4, InventorySupply.Water, "ui_water");
            AddInventoryMaterial("Inventory Clean Water", 1, "净水", 4, InventorySupply.Water, "ui_water");
            for (int i = 0; i < EquipmentCatalog.PartNames.Length; i++)
            {
                int part = i;
                InventoryCell cell = MakeInventoryCell("Part Slot " + i, partGrid, i, EquipmentCatalog.PartNames[i], (int)EquipmentCatalog.Part((PartKind)i).slot + 1,
                    () => { selectedPart = part; SelectInventoryDetail(2); });
                SetInventoryCellArt(cell, "part_" + i);
                partCells.Add(cell);
            }
        }

        static void StyleInventoryScroll(UnityEngine.UI.ScrollRect scroll, Color accent)
        {
            scroll.GetComponent<UnityEngine.UI.Image>().color = InventoryInset;
            scroll.verticalScrollbar.targetGraphic.color = accent;
        }

        RectTransform MakeInventoryGrid(string name, Transform parent, out UnityEngine.UI.ScrollRect scroll)
        {
            const float width = 396f;
            RectTransform content = ScrollContent(name, parent, 794, 319, width, 289, out scroll, true);
            var layout = content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            layout.cellSize = new Vector2((width - 16 - 12 - 16) / 3f, 84);
            layout.constraintCount = 3;
            StyleInventoryScroll(scroll, InventoryCyan);
            return content;
        }

        void AddInventoryMaterial(string name, int id, string title, int category, InventorySupply supply, string icon)
        {
            int index = materialCells.Count;
            InventoryCell cell = MakeInventoryCell(name, materialGrid, id, title, category,
                () => { selectedMaterial = index; SelectInventoryDetail(0); });
            cell.supply = supply;
            SetInventoryCellArt(cell, icon);
            cell.widget.icon.color = supply != InventorySupply.Material ? Color.white : id == (int)ItemKind.Stone ? new Color(.75f, .79f, .75f) :
                id == (int)ItemKind.Clay ? new Color(1f, .55f, .32f) : id == (int)ItemKind.Charcoal ? new Color(.38f, .42f, .39f) :
                id == (int)ItemKind.Brick ? new Color(1f, .48f, .30f) : Color.white;
            materialCells.Add(cell);
        }

        InventoryCell MakeInventoryCell(string name, Transform parent, int id, string title, int category, Action select)
        {
            UiButton widget = Button(name, parent, "", 0, 0, 117, 84, select);
            widget.label.alignment = TextAlignmentOptions.TopLeft;
            widget.label.fontSizeMax = 14;
            widget.label.fontSizeMin = 12;
            return new InventoryCell { id = id, title = title, category = category, widget = widget };
        }

        void SetInventoryCellArt(InventoryCell cell, string icon)
        {
            SetButtonArt(cell.widget, icon, 28);
            RectTransform rect = cell.widget.icon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(6, -5);
            Stretch(cell.widget.label.rectTransform, 8, 35, 5, 3);
        }

        void MakeInventoryRecipes()
        {
            MakeEquipmentRecipe((int)GearKind.StoneAxe, false);
            MakeEquipmentRecipe((int)GearKind.StonePickaxe, false);
            MakeEquipmentRecipe((int)GearKind.GrassBackpack, false);
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
                RectTransform row = FlowRow("Craft Recipe " + entry.id, inventoryRecipes, 270, 112);
                UiButton craft = Button(entry.id == 0 ? "Craft Fuel" : entry.id == 1 ? "Craft Ammo" : "Craft Recipe Button " + entry.id,
                    row, entry.name, 2, 6, 108, 66, () => CraftRecipe(entry.id));
                SetButtonArt(craft, "ui_craft", 18);
                Tint(craft, InventoryCraftIdle);
                UiButton research = Button("View Research Recipe " + entry.id, row, "查看研究", 2, 76, 108, 28,
                    () => OpenResearch(RecipeResearch(entry)));
                SetButtonArt(research, "ui_research", 16);
                manualRecipes.Add(new RecipeRow { recipe = entry, craft = craft, research = research, details = RecipeDetails(row) });
            }
            for (int i = 0; i < EquipmentCatalog.GearNames.Length; i++)
                if (!IsPortableGearRecipe((GearKind)i)) MakeEquipmentRecipe(i, false);
            for (int i = 0; i < EquipmentCatalog.PartNames.Length; i++) MakeEquipmentRecipe(i, true);
        }

        TextMeshProUGUI RecipeDetails(RectTransform row)
        {
            TextMeshProUGUI details = Label("Craft Recipe Details", row, "", 118, 4, 148, 104, 12, Ink);
            details.enableAutoSizing = false;
            details.alignment = TextAlignmentOptions.TopLeft;
            return details;
        }

        void MakeEquipmentRecipe(int kind, bool part)
        {
            string prefix = part ? "Part Craft " : "Gear Craft ";
            RectTransform row = FlowRow(prefix + kind + " Row", inventoryRecipes, 270, 112);
            UiButton craft = Button(prefix + kind, row, "", 2, 6, 108, 66, () => CraftEquipment(kind, part));
            SetButtonArt(craft, part ? "ui_attach" : "gear_" + kind, 18);
            if (!part) TintBackpackIcon(craft, (GearKind)kind);
            Tint(craft, InventoryCraftIdle);
            ResearchRef research = part ? EquipmentResearchCatalog.PartResearch((PartKind)kind) : GearResearchReference((GearKind)kind);
            UiButton view = Button("View Research " + prefix + kind, row, "查看研究", 2, 76, 108, 28,
                () => OpenResearch(research));
            SetButtonArt(view, "ui_research", 16);
            view.button.gameObject.SetActive(research != null);
            equipmentRecipes.Add(new EquipmentRecipeRow { kind = kind, part = part, craft = craft, research = view, details = RecipeDetails(row) });
        }

        static ResearchRef RecipeResearch(RecipeDefinition recipe)
        {
            if (recipe?.outputs == null) return null;
            for (int i = 0; i < recipe.outputs.Length; i++)
                if (recipe.outputs[i] > 0)
                {
                    ResearchRef research = EquipmentResearchCatalog.AmmoResearch((ItemKind)i);
                    if (research != null) return research;
                }
            return null;
        }

        static ResearchRef GearResearchReference(GearKind kind) => kind == GearKind.RadiationSuit
            ? new ResearchRef { domain = ResearchDomain.Industry, era = (int)TechEra.Nuclear, id = (int)NuclearTech.RadiationProtection }
            : EquipmentResearchCatalog.GearResearch(kind);

        ResearchRef SelectedInventoryResearch()
        {
            if (inventoryDetailKind == 0)
            {
                InventoryCell cell = SelectedInventoryMaterial;
                return cell != null && cell.supply == InventorySupply.Material ? EquipmentResearchCatalog.AmmoResearch((ItemKind)cell.id) : null;
            }
            if (inventoryDetailKind == 2) return selectedPart < 0 ? null : EquipmentResearchCatalog.PartResearch((PartKind)selectedPart);
            GearState gear = EquipmentCatalog.Find(bridge.LocalPlayer, selectedGear);
            return gear == null ? null : GearResearchReference(gear.kind);
        }

        void MakeItemDetails(RectTransform card)
        {
            RectTransform content = ScrollContent("Inventory Details Scroll View", card, 352, 444, 410, 166, out itemDetailScroll);
            StyleInventoryScroll(itemDetailScroll, InventoryCyan);
            itemTitle = FlowLabel("Selected Item Name", content, "", 382, 36, 20, Accent);
            itemMeta = FlowLabel("Selected Item Quantity", content, "", 382, 54, 13, Muted);
            RectTransform transfer = FlowRow("Backpack Transfer Actions", content, 382, 38);
            packTransferActions = transfer.gameObject;
            packPut = Button("Put Selected Item in Backpack", transfer, "放入背包", 0, 0, 187, 36, () => MoveSelectedPackItem(true));
            packTake = Button("Take Selected Item from Backpack", transfer, "取回随身", 195, 0, 187, 36, () => MoveSelectedPackItem(false));
            SetButtonArt(packPut, "ui_inventory", 20);
            SetButtonArt(packTake, "ui_detach", 20);
            Tint(packPut, InventoryCellIdle);
            Tint(packTake, InventoryCellIdle);
            storeItem = Button("Store Nearby Item", content, "存入近处箱子", 0, 0, 180, 34, StoreSelectedInventoryItem);
            SetButtonArt(storeItem, "ui_inventory", 20);
            Tint(storeItem, InventoryCellIdle);
            storeItem.button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34;
            RectTransform actions = FlowRow("Gear Actions", content, 382, 38);
            gearActions = actions.gameObject;
            equipSelected = Button("Equip Selected Gear", actions, "装备并切换", 0, 0, 122, 36, EquipSelectedGear, true);
            repairGear = Button("Repair Gear", actions, "维修装备", 130, 0, 122, 36, () => InventoryCommand("gearrepair", selectedGear));
            salvageGear = Button("Salvage Gear", actions, "回收装备", 260, 0, 122, 36, () => InventoryCommand("gearsalvage", selectedGear));
            SetButtonArt(equipSelected, "ui_equip", 22);
            SetButtonArt(repairGear, "ui_repair", 22);
            SetButtonArt(salvageGear, "ui_salvage", 22);
            salvageGear.label.color = Amber;
            gearDetailObjects.Add(actions.gameObject);
            RectTransform unequipRow = FlowRow("Armor Unequip Actions", content, 382, 36);
            unequipSelected = Button("Unequip Selected Gear", unequipRow, "脱下防具", 0, 0, 382, 34, () => InventoryCommand("unequip", selectedGear));
            SetButtonArt(unequipSelected, "ui_detach", 22);
            unequipActions = unequipRow.gameObject;
            itemUsage = FlowLabel("Selected Item Usage", content, "", 382, 64, 13, Ink);
            itemResearch = Button("View Selected Item Research", content, "查看制造研究", 0, 0, 180, 32,
                () => OpenResearch(SelectedInventoryResearch()));
            itemResearch.button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 32;
            itemStats = FlowLabel("Selected Gear Stats", content, "", 382, 108, 14, Ink);
            gearDetailObjects.Add(itemStats.gameObject);
            salvageWarning = FlowLabel("Salvage Gear Warning", content,
                "回收永久拆解，返还半数材料、全部配件与弹匣弹药；空间不足则取消。", 382, 50, 12, Amber);
            gearDetailObjects.Add(salvageWarning.gameObject);
            TextMeshProUGUI heading = FlowLabel("Ammo Selection Heading", content, "装填弹种 · 更换前先卸下现有弹药", 382, 32, 13, Accent);
            gunDetailObjects.Add(heading.gameObject);
            RectTransform ammoRow = FlowRow("Ammo Selection", content, 382, 48);
            for (int i = 0; i < DemoCatalog.ItemNames.Length; i++)
            {
                ItemKind ammo = (ItemKind)i;
                if (!EquipmentCatalog.IsAmmo(ammo)) continue;
                int index = inventoryAmmo.Count;
                inventoryAmmo.Add(ammo);
                UiButton button = Button("Select Ammo " + i, ammoRow, "", index * 77, 0, 74, 46, () => InventoryCommand("ammoselect", (int)ammo));
                button.label.fontSizeMax = 12;
                SetButtonArt(button, "ui_reload", 22);
                ammoButtons.Add(button);
            }
            gunDetailObjects.Add(ammoRow.gameObject);
            RectTransform reloadRow = FlowRow("Magazine Actions", content, 382, 38);
            reloadGear = Button("Reload Gear", reloadRow, "装填 R", 0, 0, 187, 36, () => InventoryCommand("reload"));
            unloadGear = Button("Unload Gear", reloadRow, "卸下弹药", 200, 0, 187, 36, () => InventoryCommand("unload", selectedGear));
            SetButtonArt(reloadGear, "ui_reload", 22);
            SetButtonArt(unloadGear, "ui_detach", 22);
            gunDetailObjects.Add(reloadRow.gameObject);
            heading = FlowLabel("Attachment Heading", content, "已装配件 · 空槽可直接安装", 382, 32, 13, Accent);
            gunDetailObjects.Add(heading.gameObject);
            for (int i = 0; i < EquipmentCatalog.SlotNames.Length; i++)
            {
                int slot = i;
                RectTransform row = FlowRow("Attachment Slot " + i, content, 382, 34);
                attachmentLabels.Add(Label("Attachment Slot State " + i, row, "", 0, 0, 274, 32, 13, Ink));
                UiButton detach = Button("Detach Slot " + i, row, "卸下", 276, 0, 106, 32, () => InventoryCommand("detach", selectedGear, slot));
                SetButtonArt(detach, "ui_detach", 18);
                detachButtons.Add(detach);
                gunDetailObjects.Add(row.gameObject);
            }
            heading = FlowLabel("Install Parts Heading", content, "安装配件 · 先选装备，更换前先卸下原配件", 382, 32, 13, Accent);
            gunDetailObjects.Add(heading.gameObject);
            for (int i = 0; i < EquipmentCatalog.PartNames.Length; i += 2)
            {
                RectTransform row = FlowRow("Install Parts Row " + i / 2, content, 382, 40);
                for (int j = i; j < Mathf.Min(i + 2, EquipmentCatalog.PartNames.Length); j++)
                {
                    int part = j;
                    UiButton button = Button("Attach Part " + j, row, "", (j - i) * 192, 0, 190, 36, () => InventoryCommand("attach", selectedGear, part));
                    button.label.fontSizeMax = 13;
                    SetButtonArt(button, "ui_attach", 22);
                    installButtons.Add(button);
                }
                gunDetailObjects.Add(row.gameObject);
            }
            equipmentHint = FlowLabel("Equipment Action Requirement", content, "", 382, 64, 12, Muted);
            gearDetailObjects.Add(equipmentHint.gameObject);
        }

        void BrowseInventoryTab(int tab)
        {
            if (tab == 3)
            {
                craftFilter = 0;
                ResetScroll(craftingScroll);
                RefreshAll();
                craftFilterButton.button.Select();
                return;
            }
            inventoryTab = tab;
            inventoryFilter = 0;
            inventoryDetailKind = tab;
            inventorySelectionRevision++;
            ResetScroll(ActiveInventoryScroll());
            RefreshAll();
        }

        UnityEngine.UI.ScrollRect ActiveInventoryScroll() => inventoryTab == 0 ? materialScroll : inventoryTab == 1 ? gearScroll : partScroll;

        void CycleInventoryFilter()
        {
            int count = inventoryTab == 0 ? 6 : inventoryTab == 1 ? BackpackFilter + 1 : EquipmentCatalog.SlotNames.Length + 1;
            inventoryFilter = (inventoryFilter + 1) % count;
            ResetScroll(ActiveInventoryScroll());
            RefreshAll();
        }

        InventoryCell SelectedInventoryMaterial => selectedMaterial >= 0 ? materialCells[selectedMaterial] : null;

        void StoreSelectedInventoryItem()
        {
            InventoryCell cell = SelectedInventoryMaterial;
            if (inventory.gameObject.activeSelf && inventoryDetailKind == 0 && CanStoreInventoryItem(bridge.LocalPlayer, cell))
                Send("storeitem", cell.id);
        }

        static bool CanStoreInventoryItem(PlayerState player, InventoryCell cell) =>
            cell != null && cell.supply == InventorySupply.Material && cell.id != (int)ItemKind.NuclearWaste &&
            player != null && player.connected && player.hp > 0 && player.interactCooldown <= 0 && Count(player.items, cell.id) > 0;

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
            if (EquipmentCatalog.Find(player, id) == null)
            {
                BrowseInventoryTab(1);
                inventoryFilter = slot + 1;
                RefreshAll();
                return;
            }
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

        void BrowseBackpackSlot()
        {
            PlayerState player = bridge.LocalPlayer;
            if (player == null) return;
            inventoryTab = inventoryDetailKind = 1;
            inventoryFilter = BackpackFilter;
            selectedGear = InventoryWeight.EquippedPack(player)?.id ?? -1;
            inventorySelectionRevision++;
            ResetScroll(gearScroll);
            RefreshAll();
        }

        void RefreshEquipmentSummary(PlayerState player)
        {
            GearState current = EquipmentCatalog.Equipped(player);
            if (current == null) { Set(equipmentSummary, "徒手 · Q 切换 · B 背包"); return; }
            WeaponStats stats = EquipmentCatalog.Stats(current);
            string magazine = stats.magazine > 0 ? " · " + current.magazine + "/" + stats.magazine : "";
            string state = player.reloadRemaining > 0f ? " · 装填 " + player.reloadRemaining.ToString("0.0") + "秒" :
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
                selectedMaterial = selectedPart = -1;
                inventoryDetailKind = inventoryTab = inventoryFilter = craftFilter = 0;
                ResetScroll(craftingScroll);
                inventorySelectionRevision++;
                ResetScroll(materialScroll);
            }
            GearState target = EquipmentCatalog.Find(player, selectedGear);
            if (target == null && selectedGear > 0) { selectedGear = -1; inventorySelectionRevision++; }
            bool ready = player.hp > 0f && player.connected;
            RefreshInventoryWeight(player);
            GearState backpack = InventoryWeight.EquippedPack(player);
            Set(backpackSlot.label, "背包位 · " + (backpack == null ? "空" : EquipmentCatalog.GearNames[(int)backpack.kind]));
            backpackSlot.icon.enabled = backpack == null;
            backpackSlot.button.interactable = ready;
            backpackSlot.label.color = backpack == null ? InventoryCyan : Ink;
            bool packSelected = backpack == null ? inventoryTab == 1 && inventoryFilter == BackpackFilter && target == null : backpack.id == selectedGear;
            Tint(backpackSlot, inventoryDetailKind == 1 && packSelected ? InventoryCellSelected : InventoryCellIdle);
            for (int i = 0; i < armorSlots.Length; i++)
            {
                GearState armor = EquipmentCatalog.Find(player, Count(player.armorIds, i));
                UiButton socket = armorSlots[i];
                string details = armor == null ? "选择防具" : EquipmentCatalog.GearNames[(int)armor.kind] + "\n耐久 " + GearDurability(armor);
                Set(socket.label, InventoryArmorNames[i] + "\n" + details);
                Stretch(socket.label.rectTransform, 4, armor == null ? 5 : 41, 4, 4);
                socket.button.interactable = ready;
                Tint(socket, armor != null && armor.id == selectedGear ? InventoryArmorSelected : InventoryArmorIdle);
                socket.label.color = armor == null ? InventoryRed : armor.durability <= 0 ? Danger : Ink;
            }
            Set(armorSummary, "总护甲 " + EquipmentCatalog.ArmorRating(player).ToString("0.#") + "  ·  移速 -" + (EquipmentCatalog.MovementPenalty(player) * 100f).ToString("0.#") + "%");
            int activeSlot = EquipmentCatalog.Equipped(player) == null ? 2 : player.activeSlot;
            for (int i = 0; i < equipmentSlots.Length; i++)
            {
                GearState gear = i == 2 ? null : EquipmentCatalog.Find(player, i == 0 ? player.weaponId : player.toolId);
                UiButton socket = equipmentSlots[i];
                string name = i == 2 ? "徒手" : (i == 0 ? "主武器" : "副手工具") + " · " + (gear == null ? "空" : EquipmentCatalog.GearNames[(int)gear.kind]);
                if (activeSlot == i) name += i == 2 ? "\n使用中" : "  [当前]";
                Set(socket.label, name);
                Stretch(socket.label.rectTransform, gear == null ? 8 : 50, 0, 8, 0);
                socket.label.color = gear == null ? InventoryBlue : gear.durability <= 0 ? Danger : Ink;
                socket.button.interactable = ready;
                Tint(socket, activeSlot == i ? InventorySocketSelected : InventorySocketIdle);
            }
            Set(inventorySelection, "当前：" + (activeSlot == 0 ? "武器" : activeSlot == 1 ? "工具" : "徒手"));
            for (int i = 0; i < 3; i++) Tint(inventoryTabs[i], inventoryTab == i ? InventoryCellSelected : InventoryCellIdle);
            materialScroll.gameObject.SetActive(inventoryTab == 0);
            gearScroll.gameObject.SetActive(inventoryTab == 1);
            partScroll.gameObject.SetActive(inventoryTab == 2);
            Set(sortInventory.label, "排序：" + (inventorySort == 0 ? "分类" : inventorySort == 1 ? "名称" : "数量"));
            Set(filterInventory.label, "分类：" + InventoryFilterName());
            bool bench = EquipmentCatalog.HasWorkbench(world, player);
            Set(workbenchHint, bench ? "工作台可用；制作还需科技、材料与库存空间。" :
                 PrimitiveCatalog.Enabled(world) ? "石斧、石镐、草绳背包与燃料可随身制作；其余需靠近工作台。" :
                "制作、维修、装配与回收需工作台；燃料可随身制作。");
            workbenchHint.color = bench ? Accent : Muted;
            RefreshInventoryCells(player);
            RefreshInventoryRecipes(world, player, bench);
            RefreshItemDetails(world, player, target, bench);
            if (paintedSelectionRevision != inventorySelectionRevision)
            {
                paintedSelectionRevision = inventorySelectionRevision;
                ResetScroll(itemDetailScroll);
            }
            PaintInventoryPreview(player);
        }

        void PaintInventoryPreview(PlayerState player)
        {
            if (inventoryPreviewOrigin == null) return;
            int appearance = player != null && AppearanceCatalog.IsValid(player.appearance) ? player.appearance : AppearanceCatalog.DefaultCode;
            int count = Mathf.Clamp(SurvivorVisuals.Sample(player, appearance, 3, 0f, false, true,
                inventoryPreviewLayers, true), 0, inventoryPreviewImages.Length);
            float scale = FitInventoryPreview(count);
            bool visible = false;
            for (int i = 0; i < inventoryPreviewImages.Length; i++)
            {
                UnityEngine.UI.Image image = inventoryPreviewImages[i];
                if (i >= count)
                {
                    image.enabled = false;
                    continue;
                }
                SurvivorLayer layer = inventoryPreviewLayers[i];
                Sprite sprite = layer.sprite;
                if (sprite == null || sprite.pixelsPerUnit <= 0f || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
                {
                    image.enabled = false;
                    continue;
                }
                image.sprite = sprite;
                image.color = layer.color;
                RectTransform rect = image.rectTransform;
                Vector2 size = sprite.rect.size;
                rect.pivot = new Vector2(sprite.pivot.x / size.x, sprite.pivot.y / size.y);
                rect.sizeDelta = size * (scale * layer.scale / sprite.pixelsPerUnit);
                rect.anchoredPosition = layer.position * scale;
                rect.localRotation = Quaternion.Euler(0f, 0f, layer.angle);
                rect.localScale = new Vector3(1f, layer.flipY ? -1f : 1f, 1f);
                image.enabled = true;
                visible = true;
            }
            inventoryPreviewEmpty.gameObject.SetActive(!visible);
        }

        float FitInventoryPreview(int count)
        {
            RectTransform stage = inventoryPreviewOrigin.parent as RectTransform;
            if (stage == null || count == 0) return InventoryPreviewMaxScale;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < count; i++)
            {
                SurvivorLayer layer = inventoryPreviewLayers[i];
                Sprite sprite = layer.sprite;
                if (sprite == null || sprite.pixelsPerUnit <= 0f) continue;
                Quaternion rotation = Quaternion.Euler(0f, 0f, layer.angle);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 point = new Vector2((corner & 1) * sprite.rect.width - sprite.pivot.x,
                        ((corner >> 1) & 1) * sprite.rect.height - sprite.pivot.y) * (layer.scale / sprite.pixelsPerUnit);
                    if (layer.flipY) point.y = -point.y;
                    point = layer.position + (Vector2)(rotation * point);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
            }
            if (float.IsInfinity(minimum.x)) return InventoryPreviewMaxScale;
            const float padding = 12f;
            Vector2 available = stage.rect.size - Vector2.one * (padding * 2f);
            Vector2 size = maximum - minimum;
            float scale = Mathf.Min(InventoryPreviewMaxScale, available.x / Mathf.Max(.01f, size.x),
                available.y / Mathf.Max(.01f, size.y));
            inventoryPreviewOrigin.anchoredPosition = new Vector2(
                Mathf.Clamp(0f, -stage.rect.width * .5f + padding - minimum.x * scale, stage.rect.width * .5f - padding - maximum.x * scale),
                Mathf.Clamp(20f, padding - minimum.y * scale, stage.rect.height - padding - maximum.y * scale));
            inventoryFootRule.anchoredPosition = new Vector2(24f, inventoryPreviewOrigin.anchoredPosition.y - stage.rect.height);
            return scale;
        }

        void RefreshInventoryWeight(PlayerState player)
        {
            long grams = InventoryWeight.CarriedGrams(player);
            int capacity = InventoryWeight.CapacityGrams(player);
            bool full = grams >= capacity;
            Set(inventoryWeightValue, "负重  " + InventoryKilograms(grams) + " / " + InventoryKilograms(capacity) + " kg" + (full ? "  ·  已满载" : ""));
            Color color = full ? Danger : grams >= capacity * .9f ? Amber : InventoryCyan;
            inventoryWeightValue.color = color;
            inventoryWeightFill.color = color;
            inventoryWeightFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(grams / (float)capacity), 1);
        }

        static string InventoryKilograms(long grams) => (grams / 1000m).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        static string InventoryMass(long grams) => grams < 1000 ? grams + " g" : InventoryKilograms(grams) + " kg";
        static string StackMass(int unitGrams, int quantity) => "单件 " + InventoryMass(unitGrams) + "  ·  整堆 " + InventoryMass((long)unitGrams * quantity);

        static int InventorySupplyQuantity(PlayerState player, InventoryCell cell)
        {
            switch (cell.supply)
            {
                case InventorySupply.Food: return Count(player.needs?.foods, cell.id);
                case InventorySupply.Seed: return Count(player.needs?.seeds, cell.id);
                case InventorySupply.Water: return (cell.id == 0 ? player.needs?.waterRaw : player.needs?.waterClean) ?? 0;
                default: return Count(player.items, cell.id);
            }
        }

        static int InventorySupplyGrams(InventoryCell cell)
        {
            switch (cell.supply)
            {
                case InventorySupply.Food: return InventoryWeight.Grams((FoodKind)cell.id);
                case InventorySupply.Seed: return InventoryWeight.Grams((CropKind)cell.id);
                case InventorySupply.Water: return InventoryWeight.WaterGrams;
                default: return InventoryWeight.Grams((ItemKind)cell.id);
            }
        }

        (PackItemKind kind, int id, int total, int packed, int grams)? SelectedPackItem(PlayerState player,
            GearState target, BackpackInventoryState contents)
        {
            if (inventoryDetailKind == 0 && SelectedInventoryMaterial is InventoryCell cell)
            {
                PackItemKind kind;
                int packed;
                switch (cell.supply)
                {
                    case InventorySupply.Food: kind = PackItemKind.Food; packed = Count(contents?.foods, cell.id); break;
                    case InventorySupply.Seed: kind = PackItemKind.Seed; packed = Count(contents?.seeds, cell.id); break;
                    case InventorySupply.Water:
                        kind = cell.id == 0 ? PackItemKind.RawWater : PackItemKind.CleanWater;
                        packed = (cell.id == 0 ? contents?.waterRaw : contents?.waterClean) ?? 0;
                        break;
                    default: kind = PackItemKind.Material; packed = Count(contents?.items, cell.id); break;
                }
                return (kind, cell.id, InventorySupplyQuantity(player, cell), packed, InventorySupplyGrams(cell));
            }
            if (inventoryDetailKind == 2 && selectedPart >= 0)
                return (PackItemKind.Part, selectedPart, Count(player.parts, selectedPart), Count(contents?.parts, selectedPart), InventoryWeight.Grams((PartKind)selectedPart));
            if (inventoryDetailKind == 1 && target != null && !InventoryWeight.IsEquipped(player, target))
                return (PackItemKind.Gear, target.id, 1, contents?.gearIds?.Contains(target.id) == true ? 1 : 0, InventoryWeight.GearGrams(target));
            return null;
        }

        void RefreshPackTransfer(PlayerState player, GearState target, BackpackInventoryState contents,
            (PackItemKind kind, int id, int total, int packed, int grams)? selected)
        {
            bool visible = contents != null && selected.HasValue && selected.Value.total > 0 &&
                (selected.Value.kind != PackItemKind.Gear || !EquipmentCatalog.IsBackpack(target.kind));
            packTransferActions.SetActive(visible);
            if (!visible)
            {
                packPut.button.interactable = packTake.button.interactable = false;
                return;
            }

            var item = selected.Value;
            long packedGrams = InventoryWeight.BackpackContentsGrams(contents, player.gear);
            bool fitsInPack = item.grams <= InventoryWeight.CapacityGrams(player) - InventoryWeight.BaseCapacityGrams - packedGrams;
            bool fitsOnPerson = item.grams <= InventoryWeight.BaseCapacityGrams - (InventoryWeight.CarriedGrams(player) - packedGrams);
            bool ready = player.connected && player.hp > 0;
            packPut.button.interactable = ready && item.total > item.packed && fitsInPack;
            packTake.button.interactable = ready && item.packed > 0 && fitsOnPerson;
            Set(packPut.label, item.total > item.packed && !fitsInPack ? "包内空间不足" : "放入背包");
            Set(packTake.label, item.packed > 0 && !fitsOnPerson ? "随身空间不足" : "取回随身");
        }

        void MoveSelectedPackItem(bool put)
        {
            PlayerState player = bridge.LocalPlayer;
            if (player == null || !inventory.gameObject.activeSelf) return;
            GearState target = EquipmentCatalog.Find(player, selectedGear);
            BackpackInventoryState contents = InventoryWeight.EquippedPack(player)?.backpackInventory;
            var selected = SelectedPackItem(player, target, contents);
            RefreshPackTransfer(player, target, contents, selected);
            if (selected.HasValue && (put ? packPut : packTake).button.interactable)
                Send(put ? "packput" : "packtake", selected.Value.id, (int)selected.Value.kind);
        }

        static string PackSplit(int total, int packed) => "随身 " + (total - packed) + " · 包内 " + packed;

        void SelectInventoryGear(int id)
        {
            PlayerState player = bridge.LocalPlayer;
            if (player == null) return;
            if (id == player.weaponId) { BrowseEquipmentSlot(0); return; }
            if (id == player.toolId) { BrowseEquipmentSlot(1); return; }
            if (id == player.backpackId) { BrowseBackpackSlot(); return; }
            selectedGear = id;
            SelectInventoryDetail(1);
        }

        UiButton GearInventorySocket(PlayerState player, GearState gear)
        {
            if (gear.id == player.weaponId) return equipmentSlots[0];
            if (gear.id == player.toolId) return equipmentSlots[1];
            if (gear.id == player.backpackId) return backpackSlot;
            int slot = EquipmentCatalog.ArmorSlot(gear.kind);
            return slot >= 0 && Count(player.armorIds, slot) == gear.id ? armorSlots[slot] : null;
        }

        void MountGearInventoryCell(InventoryCell cell, UiButton socket, bool armor)
        {
            // Move the existing button into the slot as its icon. This keeps gear IDs unique and selectable after equipping.
            var rect = (RectTransform)cell.widget.button.transform;
            Transform parent = socket == null ? gearGrid : socket.button.transform;
            if (rect.parent != parent)
            {
                rect.SetParent(parent, false);
                cell.widget.label.gameObject.SetActive(socket == null);
                if (socket == null) Place(rect, 0, 0, 117, 84);
                else Place(rect, armor ? 31 : 8, armor ? 6 : socket == backpackSlot ? 4 : 3, 36, 32);
            }
            cell.widget.icon.rectTransform.anchoredPosition = socket == null ? new Vector2(6, -5) : new Vector2(4, -2);
        }

        void RefreshInventoryCells(PlayerState player)
        {
            foreach (InventoryCell cell in gearCells) cell.quantity = 0;
            if (player.gear != null) foreach (GearState gear in player.gear)
            {
                if (gear == null) continue;
                InventoryCell cell = gearCells.Find(c => c.id == gear.id);
                if (cell == null)
                {
                    int id = gear.id;
                    cell = MakeInventoryCell("Gear Slot " + id, gearGrid, id, EquipmentCatalog.GearNames[(int)gear.kind],
                        GearCategory(gear.kind), () => SelectInventoryGear(id));
                    gearCells.Add(cell);
                }
                if (cell.gearKind != gear.kind)
                {
                    cell.gearKind = gear.kind;
                    cell.title = EquipmentCatalog.GearNames[(int)gear.kind];
                    cell.category = GearCategory(gear.kind);
                    SetInventoryCellArt(cell, "gear_" + (int)gear.kind);
                    TintBackpackIcon(cell.widget, gear.kind);
                }
                bool equipped = InventoryWeight.IsEquipped(player, gear);
                UiButton socket = equipped ? GearInventorySocket(player, gear) : null;
                MountGearInventoryCell(cell, socket, EquipmentCatalog.IsArmor(gear.kind));
                if (socket != null)
                {
                    cell.widget.button.gameObject.SetActive(true);
                    cell.widget.button.interactable = socket.button.interactable;
                    Tint(cell.widget, socket.button.colors.normalColor);
                }
                cell.quantity = equipped ? 0 : 1;
                bool bag = EquipmentCatalog.IsBackpack(gear.kind);
                Set(cell.widget.label, cell.title + "\n" + (bag ? BackpackCapacityText(gear.kind) : "耐久 " + GearDurability(gear)));
                cell.widget.label.color = !bag && gear.durability <= 0 ? Danger : Ink;
            }
            foreach (InventoryCell cell in gearCells)
                if (cell.quantity == 0 && EquipmentCatalog.Find(player, cell.id) == null)
                    cell.widget.button.gameObject.SetActive(false);

            List<InventoryCell> cells = inventoryTab == 0 ? materialCells : inventoryTab == 1 ? gearCells : partCells;
            visibleCells.Clear();
            int owned = 0;
            foreach (InventoryCell cell in cells)
            {
                if (inventoryTab == 1 && cell.widget.button.transform.parent != gearGrid) continue;
                if (inventoryTab == 0) cell.quantity = InventorySupplyQuantity(player, cell);
                if (inventoryTab == 2) cell.quantity = Count(player.parts, cell.id);
                bool visible = cell.quantity > 0 && (inventoryFilter == 0 || cell.category == inventoryFilter);
                cell.widget.button.gameObject.SetActive(visible);
                if (cell.quantity > 0) owned++;
                if (!visible) continue;
                visibleCells.Add(cell);
                if (inventoryTab != 1) Set(cell.widget.label, cell.title + "\n×" + cell.quantity);
                cell.widget.button.interactable = true;
                bool selected = inventoryDetailKind == inventoryTab && (inventoryTab == 0 ? cell == SelectedInventoryMaterial : cell.id == (inventoryTab == 1 ? selectedGear : selectedPart));
                Tint(cell.widget, selected ? InventoryCellSelected : InventoryCellIdle);
            }
            visibleCells.Sort((a, b) =>
            {
                int comparison = inventorySort == 0 ? a.category.CompareTo(b.category) : inventorySort == 1 ? string.Compare(a.title, b.title, StringComparison.Ordinal) : b.quantity.CompareTo(a.quantity);
                if (comparison == 0) comparison = a.supply.CompareTo(b.supply);
                return comparison != 0 ? comparison : a.id.CompareTo(b.id);
            });
            for (int i = 0; i < visibleCells.Count; i++)
                if (visibleCells[i].widget.button.transform.GetSiblingIndex() != i) visibleCells[i].widget.button.transform.SetSiblingIndex(i);
            Set(inventorySummary, "显示 " + visibleCells.Count + " / " + owned + (inventoryTab == 1 ? " 件" : " 类"));
            inventoryEmpty.gameObject.SetActive(visibleCells.Count == 0);
            Set(inventoryEmpty, inventoryTab == 1 ? "没有符合分类的未装备物品。" : "此分类暂无物品。");
        }

        void RefreshItemDetails(WorldState world, PlayerState player, GearState target, bool bench)
        {
            itemResearch.button.gameObject.SetActive(false);
            InventoryCell material = inventoryDetailKind == 0 ? SelectedInventoryMaterial : null;
            storeItem.button.gameObject.SetActive(material != null && material.supply == InventorySupply.Material);
            GearState wornPack = InventoryWeight.EquippedPack(player);
            BackpackInventoryState contents = wornPack?.backpackInventory;
            var packItem = SelectedPackItem(player, target, contents);
            RefreshPackTransfer(player, target, contents, packItem);
            bool backpack = target != null && EquipmentCatalog.IsBackpack(target.kind);
            bool hasGear = target != null && (inventoryDetailKind == 1 || inventoryDetailKind == 2 && selectedPart >= 0 && !backpack);
            bool gun = hasGear && EquipmentCatalog.IsGun(target.kind);
            bool equipped = hasGear && InventoryWeight.IsEquipped(player, target);
            bool armor = hasGear && EquipmentCatalog.IsArmor(target.kind);
            foreach (GameObject item in gearDetailObjects) item.SetActive(hasGear);
            foreach (GameObject item in gunDetailObjects) item.SetActive(gun);
            repairGear.button.gameObject.SetActive(!backpack);
            RectTransform equipRect = (RectTransform)equipSelected.button.transform;
            float equipWidth = backpack ? 252 : 122;
            if (equipRect.sizeDelta.x != equipWidth) equipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, equipWidth);
            unequipActions.SetActive(equipped && inventoryDetailKind == 1);
            if (inventoryDetailKind == 0)
            {
                RefreshInventoryMaterialDetail(world, player, material, packItem?.packed ?? 0);
                return;
            }
            if (inventoryDetailKind == 2)
            {
                PartDefinition part = selectedPart < 0 ? null : EquipmentCatalog.Part((PartKind)selectedPart);
                if (part == null)
                {
                    Set(itemTitle, "尚未选择配件");
                    SetFlowText(itemMeta, "在右侧选择配件。", 24);
                    SetFlowText(itemUsage, "先选武器，再选择配件可查看安装操作。", 24);
                }
                else
                {
                    int quantity = Count(player.parts, selectedPart);
                    ResearchRef research = EquipmentResearchCatalog.PartResearch((PartKind)selectedPart);
                    Set(itemTitle, part.name);
                    SetFlowText(itemMeta, PackSplit(quantity, packItem?.packed ?? 0) + "  ·  插槽：" + EquipmentCatalog.SlotNames[(int)part.slot] + "\n" + StackMass(InventoryWeight.Grams((PartKind)selectedPart), quantity));
                    SetFlowText(itemUsage, part.description + "\n装配目标：" + (target == null || backpack ? "请先选择一件武器。" : EquipmentCatalog.GearNames[(int)target.kind] + " #" + target.id) +
                        "\n制作研究：" + (EquipmentResearchCatalog.Known(world, research) ? "已解锁" : "缺少" + EquipmentResearchCatalog.Missing(research)));
                    itemResearch.button.gameObject.SetActive(research != null);
                }
            }
            else
            {
                ResearchRef research = target == null ? null : GearResearchReference(target.kind);
                Set(itemTitle, target == null ? "尚未选择装备" : EquipmentCatalog.GearNames[(int)target.kind] + "  #" + target.id);
                if (backpack)
                {
                    RefreshBackpackDetails(player, target, wornPack, bench);
                    itemResearch.button.gameObject.SetActive(research != null);
                    return;
                }
                SetFlowText(itemMeta, target == null ? (inventoryFilter == BackpackFilter && inventoryTab == 1 ? "在右侧选择背包。" : "在右侧选择装备。") :
                    "耐久 " + GearDurability(target) + "\n含弹药与配件 " + InventoryMass(InventoryWeight.GearGrams(target)) +
                    (equipped ? "  ·  已装备，不计入负重" : "  ·  " + PackSplit(1, packItem?.packed ?? 0)));
                SetFlowText(itemUsage, target == null ? "在左侧查看装备配方和制作条件。" : GearUsage(target.kind) +
                    (research == null ? "" : "\n持有后可正常装备与使用；制造研究：" +
                        (EquipmentResearchCatalog.Known(world, research) ? "已解锁" : "缺少" + EquipmentResearchCatalog.Missing(research))));
                itemResearch.button.gameObject.SetActive(research != null);
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
                (gun ? "射速 " : "动作频率 ") + (1f / Mathf.Max(.01f, stats.interval)).ToString("0.##") + " 次/秒 · 间隔 " + stats.interval.ToString("0.##") + " 秒";
            if (gun) combat += "\n散布 ±" + (stats.spread * Mathf.Rad2Deg).ToString("0.##") + "° · 越低越准 · 后坐力 " + (stats.recoil * Mathf.Rad2Deg).ToString("0.##") +
                "°\n弹匣 " + target.magazine + " / " + stats.magazine + " · 装填 " + stats.reload.ToString("0.##") + " 秒\n已装：" + (target.magazine > 0 ? EquipmentCatalog.AmmoName(target.loadedAmmo) : "空") +
                " · 下次：" + EquipmentCatalog.AmmoName(ReloadAmmo(player, target)) + " · 备弹 " + Count(player.items, (int)ReloadAmmo(player, target));
            if (armor)
            {
                GearState worn = EquipmentCatalog.Find(player, Count(player.armorIds, EquipmentCatalog.ArmorSlot(target.kind)));
                float previousArmor = worn == null || worn.durability <= 0 ? 0f : EquipmentCatalog.Gear(worn.kind).armor;
                float effectiveArmor = target.durability <= 0 ? 0f : definition.armor;
                float previousPenalty = worn == null || worn.durability <= 0 ? 0f : EquipmentCatalog.Gear(worn.kind).movePenalty;
                float effectivePenalty = target.durability <= 0 ? 0f : definition.movePenalty;
                combat = "防具护甲 " + effectiveArmor.ToString("0.#") + (target.durability <= 0 ? " · 已损坏，无防御" : "") +
                    " · 移速 -" + (effectivePenalty * 100f).ToString("0.#") + "%\n当前同部位防具：" + (worn == null ? "空" : EquipmentCatalog.GearNames[(int)worn.kind] + " · 耐久 " + GearDurability(worn)) +
                    "\n替换后护甲变化 " + (effectiveArmor - previousArmor).ToString("+0.#;-0.#;0") +
                    " · 移速惩罚变化 " + ((effectivePenalty - previousPenalty) * 100f).ToString("+0.#;-0.#;0") + "%" +
                    "\n全身有效护甲 " + EquipmentCatalog.ArmorRating(player).ToString("0.#") + " · 总移速惩罚 " + (EquipmentCatalog.MovementPenalty(player) * 100f).ToString("0.#") + "% · 上限25%" +
                    "\n防具独立穿戴；不切换当前武器或工具。";
                if (target.kind == GearKind.RadiationSuit)
                    combat += "\n辐射减免 " + (target.durability > 0 ? "80%" : "0% · 耐久耗尽") +
                        " · 需穿戴在躯干位生效；暴露作业持续磨损。\n制作研究：核作业防护。完好时仍有20%残余暴露，请留意累计暴露并及时撤离。";
            }
            SetFlowText(itemStats, combat + (armor ? "" : "\n属性已计入配件加成；命中效果与成长加成在攻击时计算。"));
            int slot = EquipmentCatalog.ArmorSlot(target.kind);
            int previousId = slot >= 0 ? Count(player.armorIds, slot) : definition.tool ? player.toolId : player.weaponId;
            GearState previous = EquipmentCatalog.Find(player, previousId);
            long swapGrams = previous == target ? 0 : (long)InventoryWeight.GearGrams(previous) - InventoryWeight.GearGrams(target);
            bool swapFits = InventoryWeight.CanFit(player, swapGrams);
            equipSelected.button.interactable = alive && !active && swapFits;
            Set(equipSelected.label, active ? armor ? "已穿戴" : "使用中" : !swapFits ? "替换后超重" : armor ? "穿戴防具" : definition.tool ? "切换至此工具" : "切换至此武器");
            bool unequipFits = !equipped || InventoryWeight.CanFit(player, InventoryWeight.GearGrams(target));
            unequipSelected.button.interactable = alive && equipped && unequipFits;
            Set(unequipSelected.label, unequipFits ? armor ? "脱下防具" : "卸下至背包" : "剩余负重不足，无法卸下");
            bool stone = target.kind == GearKind.StoneAxe || target.kind == GearKind.StonePickaxe;
            bool repairMaterials = stone ? Count(player.items, (int)ItemKind.Wood) >= 1 && Count(player.items, (int)ItemKind.Stone) >= 1 :
                Count(player.items, (int)ItemKind.Iron) >= 2 && Count(player.items, (int)ItemKind.Scrap) >= 1;
            repairGear.button.interactable = ready && bench && target.durability < definition.maxDurability && repairMaterials;
            int repairAmount = Mathf.Clamp(definition.maxDurability - target.durability, 0, 25);
            Set(repairGear.label, "维修 +" + repairAmount + (stone ? "\n木材×1 石材×1" : "\n铁锭×2 废料×1"));
            bool salvageSpace = HasSalvageSpace(player, target);
            salvageGear.button.interactable = ready && bench && salvageSpace;
            Set(salvageGear.label, !salvageSpace ? "回收空间不足" : "回收装备");
            SetFlowText(equipmentHint, (loading ? "装填中：" + player.reloadRemaining.ToString("0.0") + " 秒；切换装备或操作装备将取消装填。\n" : "") +
                (bench ? "工作台可用。" : "维修、装配与回收需在工作台 2.4 格内，且视线畅通。") + "每次维修最多恢复 25 耐久，不超过装备上限 " + definition.maxDurability + "；装填仅作用于当前武器，换弹种前请先卸弹。");
            if (!gun) return;
            ItemKind nextAmmo = ReloadAmmo(player, target);
            bool selectedCompatible = EquipmentCatalog.AmmoCompatible(target.kind, player.selectedAmmo);
            bool sameAmmo = target.magazine == 0 || target.loadedAmmo == player.selectedAmmo;
            reloadGear.button.interactable = ready && active && !loading && target.durability > 0 && target.magazine < stats.magazine && selectedCompatible && sameAmmo && Count(player.items, (int)nextAmmo) > 0;
            Set(reloadGear.label, loading && player.reloadingGearId == target.id ? "装填中 " + player.reloadRemaining.ToString("0.0") + "秒" : !active ? "请先装备并使用" : !sameAmmo ? "先卸弹再换弹种" : "装填 R");
            bool unloadFits = !equipped || target.magazine == 0 || InventoryWeight.CanFit(player, (long)target.magazine * InventoryWeight.Grams(target.loadedAmmo));
            unloadGear.button.interactable = ready && target.magazine > 0 && unloadFits && Count(player.items, (int)target.loadedAmmo) + target.magazine <= WorldSimulator.InventoryLimit;
            Set(unloadGear.label, unloadFits ? "卸下弹药" : "卸弹后超重");
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
                bool detachFits = value < 0 || !equipped || InventoryWeight.CanFit(player, InventoryWeight.Grams((PartKind)value));
                detachButtons[i].button.interactable = ready && bench && value >= 0 && capacity && detachFits && Count(player.parts, value) < WorldSimulator.InventoryLimit;
                Set(detachButtons[i].label, !capacity ? "先卸弹" : !detachFits ? "卸下后超重" : "卸下");
            }
            for (int i = 0; i < installButtons.Count; i++)
            {
                PartDefinition part = EquipmentCatalog.Part((PartKind)i);
                bool compatible = EquipmentCatalog.PartCompatible(target.kind, (PartKind)i);
                bool empty = AttachedPart(target, (int)part.slot) < 0;
                installButtons[i].button.interactable = ready && bench && world.era >= part.era && compatible && empty && Count(player.parts, i) > 0;
                Set(installButtons[i].label, part.name + " ×" + Count(player.parts, i) + (!compatible ? " · 不适用" : !empty ? " · 槽位已满" : " · 安装"));
                Tint(installButtons[i], inventoryDetailKind == 2 && selectedPart == i ? Selected : Raised);
            }
        }

        void RefreshBackpackDetails(PlayerState player, GearState target, GearState wornPack, bool bench)
        {
            bool worn = target.id == wornPack?.id;
            bool alive = player.connected && player.hp > 0;
            bool swapping = !worn && wornPack != null;
            SetFlowText(itemMeta, "总容量 " + BackpackCapacityText(target.kind) + "  ·  自重 " + InventoryMass(InventoryWeight.GearGrams(target)) + "\n" +
                (worn ? "已穿戴 · 已装 " + InventoryMass(InventoryWeight.BackpackContentsGrams(target.backpackInventory, player.gear)) : PackSplit(1, 0)));
            gearActions.SetActive(!worn);
            salvageWarning.gameObject.SetActive(!worn);
            equipSelected.button.interactable = alive && !worn && InventoryWeight.CanEquipBackpack(player, target);
            Set(equipSelected.label, swapping ? "换包 · 旧包连同内容落地" : "背上背包");
            unequipSelected.button.interactable = alive && worn;
            Set(unequipSelected.label, "卸下到地面");

            bool salvageSpace = !worn && HasSalvageSpace(player, target);
            salvageGear.button.interactable = alive && bench && player.interactCooldown <= 0f && salvageSpace;
            Set(salvageGear.label, salvageSpace ? "回收装备" : "回收空间不足");
            SetFlowText(itemUsage, worn ? "卸下后，包内物品会留在落地的背包里。" : GearUsage(target.kind));
            SetFlowText(itemStats, "随身容量 " + InventoryMass(InventoryWeight.BaseCapacityGrams) + " · 包内容量 " +
                InventoryMass(EquipmentCatalog.BackpackCapacityGrams(target.kind) - InventoryWeight.BaseCapacityGrams));
            SetFlowText(equipmentHint, worn || bench ? "" : "回收需在工作台 2.4 格内，且视线畅通。", 0);
        }

        void RefreshInventoryMaterialDetail(WorldState world, PlayerState player, InventoryCell cell, int packed)
        {
            if (cell == null)
            {
                Set(itemTitle, "尚未选择物品");
                SetFlowText(itemMeta, "在右侧选择材料或补给，查看数量和重量。", 24);
                SetFlowText(itemUsage, "", 0);
                return;
            }
            int quantity = InventorySupplyQuantity(player, cell);
            Set(itemTitle, cell.title);
            SetFlowText(itemMeta, PackSplit(quantity, packed) + "  ·  " + MaterialCategoryName(cell.category) + "\n" + StackMass(InventorySupplyGrams(cell), quantity));
            if (cell.supply != InventorySupply.Material)
            {
                SetFlowText(itemUsage, cell.supply == InventorySupply.Seed ? "在生存界面的种植页播种。" :
                    cell.supply == InventorySupply.Water ? "在生存界面饮用或煮水。喝原水会增加感染。" : "在生存界面选择食用或烹饪。", 24);
                return;
            }
            storeItem.button.interactable = CanStoreInventoryItem(player, cell);
            ResearchRef research = EquipmentResearchCatalog.AmmoResearch((ItemKind)cell.id);
            string rule = research == null ? "" : EquipmentResearchCatalog.Known(world, research)
                ? "制造研究已解锁" : "制造缺少研究：" + EquipmentResearchCatalog.Missing(research);
            SetFlowText(itemUsage, MaterialUsage(cell.id) + (research == null ? "" : "\n弹药可正常使用与装填；" + rule));
            itemResearch.button.gameObject.SetActive(research != null);
        }

        static bool HasSalvageSpace(PlayerState player, GearState gear)
        {
            int[] cost = EquipmentCatalog.Gear(gear.kind).cost;
            long deltaGrams = InventoryWeight.IsEquipped(player, gear) ? 0L : -InventoryWeight.GearGrams(gear);
            for (int i = 0; i < DemoCatalog.ItemCount; i++)
            {
                int returned = Count(cost, i) / 2 + (gear.magazine > 0 && (int)gear.loadedAmmo == i ? gear.magazine : 0);
                if (returned > WorldSimulator.InventoryLimit - Count(player.items, i)) return false;
                if (returned > 0) deltaGrams += (long)returned * InventoryWeight.Grams((ItemKind)i);
            }
            for (int i = 0; i < EquipmentCatalog.PartNames.Length; i++)
            {
                int returned = 0;
                if (gear.attachments != null) foreach (int part in gear.attachments) if (part == i) returned++;
                if (returned > WorldSimulator.InventoryLimit - Count(player.parts, i)) return false;
                if (returned > 0) deltaGrams += (long)returned * InventoryWeight.Grams((PartKind)i);
            }
            return InventoryWeight.CanFit(player, deltaGrams);
        }

        void RefreshInventoryRecipes(WorldState world, PlayerState player, bool bench)
        {
            int count = 0;
            foreach (RecipeRow row in manualRecipes)
            {
                bool visible = craftFilter == 0 || craftFilter == 1;
                row.craft.button.transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;
                count++;
                RecipeDefinition recipe = row.recipe;
                bool era = PrimitiveCatalog.IsRecipeUnlocked(world, recipe), needsBench = recipe.id > 0;
                bool materials = CanAfford(player.items, recipe.inputs), space = HasRecipeSpace(player, recipe);
                string state = !era ? PrimitiveCatalog.RecipeRequirement(world, recipe) : needsBench && !bench ? "需工作台" : !materials ? "缺材料" : !space ? "负重不足" : player.interactCooldown > 0f ? "冷却中" : "可制作";
                row.craft.button.interactable = player.hp > 0f && player.connected && era && (!needsBench || bench) && materials && space && player.interactCooldown <= 0f;
                Set(row.craft.label, recipe.name);
                ResearchRef research = RecipeResearch(recipe);
                row.research.button.gameObject.SetActive(research != null);
                string evidence = research == null ? "" : EquipmentResearchCatalog.Evidence(world, research);
                SetRecipeText(row.details, MaterialText(recipe.inputs, player.items, true) + " → " + MaterialText(recipe.outputs) +
                    "\n" + TechCatalog.EraName(recipe.era) + (needsBench ? " · 工作台" : " · 随身") + " · " + state +
                    (string.IsNullOrEmpty(evidence) || evidence == "已满足" ? "" : "\n实产 " + evidence));
            }
            foreach (EquipmentRecipeRow row in equipmentRecipes)
            {
                bool portable = !row.part && IsPortableGearRecipe((GearKind)row.kind);
                bool visible = craftFilter == 0 || craftFilter == (row.part ? 3 : 2) || craftFilter == 1 && portable;
                row.craft.button.transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;
                count++;
                PartDefinition part = row.part ? EquipmentCatalog.Part((PartKind)row.kind) : null;
                GearDefinition gear = row.part ? null : EquipmentCatalog.Gear((GearKind)row.kind);
                string name = row.part ? part.name : gear.name;
                TechEra era = row.part ? part.era : gear.era;
                int[] cost = row.part ? part.cost : gear.cost;
                ResearchRef research = row.part ? EquipmentResearchCatalog.PartResearch((PartKind)row.kind) : GearResearchReference((GearKind)row.kind);
                string requirement = row.part ? EquipmentCatalog.PartCraftRequirement(world, player, (PartKind)row.kind) :
                    EquipmentCatalog.CraftRequirement(world, player, (GearKind)row.kind);
                bool canCraft = requirement.Length == 0;
                string reason = requirement;
                if (reason.Length == 0) reason = player.interactCooldown > 0f ? "冷却中" : "可制作";
                row.craft.button.interactable = canCraft && player.interactCooldown <= 0f;
                Set(row.craft.label, name);
                row.research.button.gameObject.SetActive(research != null);
                string evidence = research == null ? "" : EquipmentResearchCatalog.Evidence(world, research);
                string station = portable ? "随身" : "工作台";
                string purpose = row.part ? EquipmentCatalog.SlotNames[(int)part.slot] + " · " + part.description : GearRecipePurpose(gear);
                SetRecipeText(row.details, MaterialText(cost, player.items, true) + "\n" + TechCatalog.EraName(era) + " · " + station + " · " + reason +
                    (string.IsNullOrEmpty(evidence) || evidence == "已满足" ? "" : "\n实产 " + evidence) +
                    "\n" + purpose);
            }
            Set(craftFilterButton.label, "配方：" + (craftFilter == 0 ? "全部" : craftFilter == 1 ? "手工" : craftFilter == 2 ? "装备" : "配件"));
            Set(craftSummary, count + " 项 · 持有/需求");
        }

        static void SetRecipeText(TextMeshProUGUI details, string value)
        {
            if (details.text == value) return;
            Set(details, value);
            float height = Mathf.Max(112, details.GetPreferredValues(value, details.rectTransform.rect.width, float.PositiveInfinity).y + 12);
            details.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 8);
            details.transform.parent.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
        }

        void CraftEquipment(int kind, bool part)
        {
            if (bridge.World == null || bridge.LocalPlayer == null || !inventory.gameObject.activeSelf) return;
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
        static bool IsPortableGearRecipe(GearKind kind) => kind == GearKind.GrassBackpack || EquipmentCatalog.IsHandcraftedTool(kind);
        static string BackpackCapacityText(GearKind kind) => InventoryMass(EquipmentCatalog.BackpackCapacityGrams(kind));
        static string GearRecipePurpose(GearDefinition gear)
        {
            if (EquipmentCatalog.IsBackpack(gear.kind)) return "背包位 · 总容量 " + BackpackCapacityText(gear.kind);
            string slot = EquipmentCatalog.IsArmor(gear.kind) ? "防具位" : gear.tool ? "工具位" : "武器位";
            return slot + " · " + GearUsage(gear.kind);
        }

        static void TintBackpackIcon(UiButton button, GearKind kind)
        {
            button.icon.color = Color.white;
        }

        static int GearCategory(GearKind kind)
        {
            if (EquipmentCatalog.IsBackpack(kind)) return BackpackFilter;
            return EquipmentCatalog.IsArmor(kind) ? 3 : EquipmentCatalog.Gear(kind).tool ? 2 : 1;
        }
        static string GearDurability(GearState gear) => gear.durability + " / " + EquipmentCatalog.Gear(gear.kind).maxDurability;
        static bool IsWorn(PlayerState player, GearState gear)
        {
            int slot = gear == null ? -1 : EquipmentCatalog.ArmorSlot(gear.kind);
            return slot >= 0 && Count(player.armorIds, slot) == gear.id;
        }

        string InventoryFilterName()
        {
            if (inventoryFilter == 0) return "全部";
            if (inventoryTab == 0) return MaterialCategoryName(inventoryFilter);
            if (inventoryTab == 1) return inventoryFilter == BackpackFilter ? "背包" : inventoryFilter == 1 ? "武器" : inventoryFilter == 2 ? "工具" : "防具";
            return EquipmentCatalog.SlotNames[inventoryFilter - 1];
        }

        static int MaterialCategory(int item)
        {
            if (EquipmentCatalog.IsAmmo((ItemKind)item)) return 3;
            if (item == (int)ItemKind.Food || item == (int)ItemKind.Fuel || item == (int)ItemKind.Charcoal || item >= (int)ItemKind.NuclearFuel && item <= (int)ItemKind.NuclearWaste) return 4;
            if (item == (int)ItemKind.Wood || item == (int)ItemKind.Ore || item == (int)ItemKind.CopperOre || item == (int)ItemKind.UraniumOre || item == (int)ItemKind.TinOre || item == (int)ItemKind.Stone || item == (int)ItemKind.Clay) return 1;
            return 2;
        }

        static string MaterialCategoryName(int category) => category == 1 ? "原料" : category == 2 ? "加工材料" : category == 3 ? "弹药" : category == 4 ? "补给 / 能源" : "种子";

        static string GearUsage(GearKind kind)
        {
            if (EquipmentCatalog.IsBackpack(kind)) return EquipmentCatalog.Gear(kind).description;
            if (kind == GearKind.RadiationSuit)
                return "躯干防具 · 需核作业防护研究；耐久有效时辐射暴露−80%，耗尽即失效。";
            if (EquipmentCatalog.IsArmor(kind)) return "防具 · 耐久耗尽即失效。";
            string usage;
            switch (kind)
            {
                case GearKind.StoneAxe: case GearKind.SteelAxe: return "工具 · 采树木与补给箱，也可近战。";
                case GearKind.StonePickaxe: case GearKind.SteelPickaxe: return "工具 · 采矿石、石材与黏土，也可近战。";
                case GearKind.Wrench: return "工具 · 采废料堆，也可维护设备与近战。";
                case GearKind.Knife: usage = "近战武器 · 不耗弹药。"; break;
                default: usage = "远程武器 · 装填后射击；Alt 精确瞄准，R 装填。"; break;
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
                string caliber = kind == ItemKind.Ammo ? "9×19mm 手枪弹，也可供机枪塔使用。" :
                    kind == ItemKind.RifleAmmo ? "5.56×45mm 步枪弹。" :
                    kind == ItemKind.Shell ? "12号霰弹。" :
                    kind == ItemKind.PrecisionAmmo ? "7.62×51mm 狙击步枪弹。" :
                    "通用穿甲弹，可供兼容此弹种的步枪使用。";
                return "弹药用途：" + string.Join("、", compatible) + "。\n" + caliber +
                    "\n在装备详情中选择弹种并装填；切换已装弹种前先卸弹。";
            }
            if (kind == ItemKind.Food) return "补充饱食度。点击下方「进食 V」，或关闭面板后按 V。";
            if (kind == ItemKind.Branch || kind == ItemKind.LooseStone || kind == ItemKind.DryGrass)
                return "可空手从地面拾取。按 T → 原始 → 工艺与建造，可随身制作石斧和石镐。";
            if (kind == ItemKind.Fuel) return "供燃料发电、锅炉与石炉使用；也用于炼钢和化工。";
            if (kind == ItemKind.Stone) return "采集石材堆获得。用于工具、窑炉与建造工艺；按 T 查看研究材料。";
            if (kind == ItemKind.Clay) return "采集黏土矿床获得。陶窑消耗黏土×4、木炭×1，产出陶砖×4。";
            if (kind == ItemKind.Charcoal) return "炭窑消耗木材×4，产出木炭×2。用于陶窑烧制陶砖与原始冶金。";
            if (kind == ItemKind.Brick) return "由陶窑消耗黏土和木炭烧制，用于建筑与工艺研究。研究要求的生产数量按累计产出计算。";
            if (kind == ItemKind.NuclearWaste) return "反应堆产物。仅由反应堆自动送入正前方直连的核废料专用仓。";
            var uses = new List<string>();
            foreach (RecipeDefinition recipe in TechCatalog.Recipes) if (Count(recipe.inputs, item) > 0 && !uses.Contains(recipe.name)) uses.Add(recipe.name);
            bool construction = false, equipment = false;
            for (int i = 0; i < DemoCatalog.BuildNames.Length; i++) if (Count(DemoCatalog.BuildCost((BuildKind)i), item) > 0) { construction = true; break; }
            for (int i = 0; i < EquipmentCatalog.GearNames.Length; i++) if (Count(EquipmentCatalog.Gear((GearKind)i).cost, item) > 0) { equipment = true; break; }
            if (!equipment) for (int i = 0; i < EquipmentCatalog.PartNames.Length; i++) if (Count(EquipmentCatalog.Part((PartKind)i).cost, item) > 0) { equipment = true; break; }
            return (construction ? "建造材料。" : "工业材料。") + (equipment ? "可制作装备 / 配件。" : "") +
                (uses.Count > 0 ? "\n配方：" + string.Join("、", uses) : "") + "\n在左侧快捷制作查看所需科技与材料；按 T 查看科技和建造。";
        }
    }
}
