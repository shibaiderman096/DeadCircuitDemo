using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        sealed class SurvivalRow
        {
            public int id;
            public RectTransform root;
            public TextMeshProUGUI text;
            public UnityEngine.UI.Image icon;
            public UiButton action;
        }

        RectTransform survivalPanel, survivalTarget, survivalWaterFill;
        TextMeshProUGUI survivalHudWater, survivalHudStatus, survivalStatus, survivalFeedback;
        TextMeshProUGUI survivalTargetText, survivalTargetHelp, survivalWaterText, survivalCropText, survivalPlotText;
        UnityEngine.UI.Image survivalCropIcon;
        readonly RectTransform[] survivalPages = new RectTransform[4];
        readonly UnityEngine.UI.ScrollRect[] survivalScrolls = new UnityEngine.UI.ScrollRect[4];
        readonly UiButton[] survivalTabs = new UiButton[4];
        readonly UiButton[] survivalCells = new UiButton[25];
        readonly List<SurvivalRow> survivalFoods = new List<SurvivalRow>();
        readonly List<SurvivalRow> survivalRecipes = new List<SurvivalRow>();
        readonly List<SurvivalRow> survivalCorpses = new List<SurvivalRow>();
        readonly List<SurvivalRow> survivalPlots = new List<SurvivalRow>();
        readonly List<UiButton> survivalCommands = new List<UiButton>();
        readonly List<string> survivalActions = new List<string>();
        readonly List<int> survivalArguments = new List<int>();
        int survivalTab, survivalCrop, survivalX, survivalY;
        bool survivalHasTarget;
        float survivalSendAt;
        string survivalPreviousNotice;
        float survivalPreviousNoticeUntil;

        partial void CreateAdditionalPanels(RectTransform root)
        {
            RectTransform hud = Box("Survival HUD", game, 18, 134, 242, 104, HudSurface);
            survivalHudWater = Label("Survival HUD Water", hud, "", 12, 3, 136, 24, 14, Ink);
            Button("Open Survival", hud, "生存 J", 157, 4, 77, 27, ToggleSurvival);
            RectTransform track = Box("Survival Water Track", hud, 12, 33, 218, 5, Edge);
            survivalWaterFill = Box("Survival Water Fill", track, 0, 0, 218, 5, Accent);
            Stretch(survivalWaterFill);
            survivalHudStatus = Label("Survival HUD Status", hud, "", 12, 42, 218, 58, 12, Muted);
            survivalHudStatus.enableAutoSizing = false;
            survivalHudStatus.alignment = TextAlignmentOptions.TopLeft;

            survivalPanel = Backdrop("Survival Panel", root, new Color32(4, 12, 8, 210));
            RectTransform card = Box("Survival Frame", survivalPanel, 0, 0, 1120, 656, Panel, true);
            Center(card, 1120, 656);
            Label("Survival Title", card, "生存手册", 22, 14, 760, 40, 27, Ink, FontStyles.Bold);
            Button("Close Survival", card, "返回 J / Esc", 952, 18, 146, 34, () => SetPanel(survivalPanel, false));
            PanelRule(card, 1076, 58);
            survivalStatus = Label("Survival Status", card, "", 22, 66, 1076, 64, 15, Ink);
            survivalStatus.alignment = TextAlignmentOptions.TopLeft;
            string[] titles = { "食物饮水", "烹饪", "种植", "野生物图鉴" };
            string[] names = { "Food", "Cooking", "Farming", "Wildlife" };
            for (int i = 0; i < 4; i++)
            {
                int tab = i;
                survivalTabs[i] = Button("Survival Tab " + names[i], card, titles[i], 22 + i * 271, 138, 263, 38,
                    () => { survivalTab = tab; ResetScroll(survivalScrolls[tab]); RefreshAll(); });
                bool targetPage = i == 0 || i == 2;
                survivalPages[i] = ScrollContent("Survival " + names[i] + " Scroll", card,
                    targetPage ? 390 : 22, 190, targetPage ? 708 : 1076, 402, out survivalScrolls[i]);
            }
            MakeSurvivalTargets(card);
            MakeSurvivalFood();
            MakeSurvivalCooking();
            MakeSurvivalFarming();
            MakeSurvivalWildlife();
            survivalFeedback = Label("Survival Feedback", card, "", 22, 602, 1076, 42, 14, Amber);
            survivalFeedback.alignment = TextAlignmentOptions.TopLeft;
            survivalPanel.gameObject.SetActive(false);
        }

        void ToggleSurvival()
        {
            if (survivalPanel == null) return;
            bool open = !survivalPanel.gameObject.activeSelf;
            if (open)
            {
                Vector2 target = bridge.MouseWorld;
                survivalX = Mathf.FloorToInt(target.x + .5f);
                survivalY = Mathf.FloorToInt(target.y + .5f);
                var p = bridge.LocalPlayer;
                var w = bridge.World;
                if (p != null && w != null && !SurvivalInReach(w, p, survivalX, survivalY))
                { survivalX = Mathf.FloorToInt(p.x + .5f); survivalY = Mathf.FloorToInt(p.y + .5f); }
                survivalHasTarget = true;
            }
            SetPanel(survivalPanel, open);
        }

        partial void CloseAdditionalPanels()
        {
            if (survivalPanel != null) survivalPanel.gameObject.SetActive(false);
        }

        partial void HasAdditionalPanelOpen(ref bool open)
        {
            if (survivalPanel != null && survivalPanel.gameObject.activeSelf) open = true;
        }

        void MakeSurvivalTargets(RectTransform card)
        {
            survivalTarget = Box("Survival Target Selector", card, 22, 190, 350, 402, Recessed, true);
            Label("Survival Target Heading", survivalTarget, "附近格子 · 点击选择", 12, 4, 326, 28, 16, Accent);
            for (int i = 0; i < survivalCells.Length; i++)
            {
                int index = i;
                survivalCells[i] = Button("Survival Target Cell " + i, survivalTarget, "", 12 + i % 5 * 66, 40 + i / 5 * 39, 62, 35,
                    () => SelectSurvivalCell(index));
                survivalCells[i].label.fontSizeMax = 12;
                survivalCells[i].label.fontSizeMin = 12;
            }
            survivalTargetText = Label("Survival Target Selection", survivalTarget, "", 12, 240, 326, 52, 14, Ink);
            survivalTargetHelp = Label("Survival Target Requirement", survivalTarget, "", 12, 296, 326, 94, 14, Amber);
            survivalTargetHelp.alignment = TextAlignmentOptions.TopLeft;
        }

        void SelectSurvivalCell(int index)
        {
            var p = bridge.LocalPlayer;
            if (p == null) return;
            survivalX = Mathf.FloorToInt(p.x + .5f) + index % 5 - 2;
            survivalY = Mathf.FloorToInt(p.y + .5f) + 2 - index / 5;
            survivalHasTarget = true;
            RefreshAll();
        }

        SurvivalRow MakeSurvivalRow(string name, RectTransform parent, int id, float width, float height, bool action)
        {
            RectTransform row = FlowRow(name, parent, width, height);
            RectTransform surface = Box("Surface", row, 0, 0, width, height, Raised);
            Stretch(surface);
            var icon = MakeHudArt("Icon", row, 10, 12, 42);
            TextMeshProUGUI caption = Label("Details", row, "", 64, 6, width - (action ? 186 : 76), height - 12, 14, Ink);
            caption.enableAutoSizing = false;
            caption.alignment = TextAlignmentOptions.TopLeft;
            UiButton button = action ? Button(name + " Action", row, "", width - 112, 16, 102, 38, () => { }) : null;
            return new SurvivalRow { id = id, root = row, icon = icon, text = caption, action = button };
        }

        void BindSurvivalRow(SurvivalRow row, string action, Func<int> argument, bool useTarget = false)
        {
            row.action.button.onClick.RemoveAllListeners();
            row.action.button.onClick.AddListener(() =>
            {
                DemoAudio.UiClick();
                SurvivalRequest(action, argument(), useTarget ? survivalX : 0, useTarget ? survivalY : 0);
            });
        }

        UiButton SurvivalCommand(string name, RectTransform row, string title, float x, float y, float width, string action, int arg)
        {
            UiButton result = Button(name, row, title, x, y, width, 36,
                () => SurvivalRequest(action, arg, survivalX, survivalY));
            survivalCommands.Add(result); survivalActions.Add(action); survivalArguments.Add(arg);
            return result;
        }

        void MakeSurvivalFood()
        {
            RectTransform content = survivalPages[0];
            var guide = FlowLabel("Survival Guide", content,
                "食物来源：用 E 交互耗尽树木、补给箱，寻找浆果 / 苹果 / 蘑菇、蛋 / 原鱼与种子；狩猎后在图鉴页分解取肉。种植需翻土→播种→浇水→成熟→收获；工作台 / 石炉处理原料。旧工业口粮仍在背包使用。",
                680, 106, 14, Muted);
            guide.overflowMode = TextOverflowModes.Overflow;
            survivalWaterText = FlowLabel("Survival Water Inventory", content, "", 680, 52, 15, Accent);
            RectTransform water = FlowRow("Survival Water Actions", content, 680, 86);
            SurvivalCommand("Survival Drink Clean", water, "饮用净水 +40", 0, 0, 324, "drink", 1);
            SurvivalCommand("Survival Drink Raw", water, "原水 +30 / 感染 +25", 334, 0, 324, "drink", 0);
            SurvivalCommand("Survival Collect Water", water, "从所选水格取原水", 0, 44, 658, "collectwater", 0);
            foreach (var food in SurvivalCatalog.Foods)
            {
                var entry = food;
                SurvivalRow row = MakeSurvivalRow("Survival Food " + (int)food.kind, content, (int)food.kind, 680, 114, true);
                SetHudArt(row.icon, "food_" + row.id);
                Set(row.action.label, food.edible ? "食用" : "需加工");
                BindSurvivalRow(row, "eatfood", () => (int)entry.kind);
                survivalFoods.Add(row);
            }
        }

        void MakeSurvivalCooking()
        {
            RectTransform content = survivalPages[1];
            FlowLabel("Survival Cooking Guide", content,
                "自动选择 2.4 格内、视线畅通且已解锁的工作站占地格。成本从随身库存扣除；操作后保留面板。未满足距离、原料或库存空间时按钮禁用。", 1048, 50, 14, Muted);
            SurvivalRow boil = MakeSurvivalRow("Survival Boil Water", content, -1, 1048, 100, true);
            Set(boil.action.label, "煮沸一份");
            boil.action.button.onClick.RemoveAllListeners();
            boil.action.button.onClick.AddListener(() => { DemoAudio.UiClick(); RequestSurvivalStation("boilwater", 0, BuildKind.StoneFurnace); });
            survivalRecipes.Add(boil);
            foreach (var recipe in SurvivalCatalog.Recipes)
            {
                var entry = recipe;
                SurvivalRow row = MakeSurvivalRow("Survival Recipe " + recipe.id, content, recipe.id, 1048, 118, true);
                SetHudArt(row.icon, "food_" + (int)recipe.output);
                Set(row.action.label, "处理 ×" + recipe.quantity);
                row.action.button.onClick.RemoveAllListeners();
                row.action.button.onClick.AddListener(() => { DemoAudio.UiClick(); RequestSurvivalStation("cook", entry.id, entry.station); });
                survivalRecipes.Add(row);
            }
        }

        void MakeSurvivalFarming()
        {
            RectTransform content = survivalPages[2];
            RectTransform selection = FlowRow("Survival Crop Selection", content, 680, 110);
            survivalCropIcon = MakeHudArt("Survival Selected Crop Icon", selection, 8, 12, 40);
            survivalCropText = Label("Survival Selected Crop", selection, "", 60, 0, 606, 66, 14, Ink);
            survivalCropText.alignment = TextAlignmentOptions.TopLeft;
            Button("Survival Previous Crop", selection, "上一作物", 0, 72, 324, 34,
                () => { survivalCrop = (survivalCrop + SurvivalCatalog.CropCount - 1) % SurvivalCatalog.CropCount; RefreshAll(); });
            Button("Survival Next Crop", selection, "下一作物", 334, 72, 324, 34,
                () => { survivalCrop = (survivalCrop + 1) % SurvivalCatalog.CropCount; RefreshAll(); });
            RectTransform actions = FlowRow("Survival Farming Actions", content, 680, 126);
            SurvivalCommand("Survival Till", actions, "1 翻土 · 木材 1", 0, 0, 324, "till", 0);
            // Crop is resolved at click time; unlike the other commands its argument changes.
            UiButton sow = Button("Survival Sow", actions, "2 播种 · 种子 1", 334, 0, 324, 36,
                () => SurvivalRequest("sow", survivalCrop, survivalX, survivalY));
            SurvivalCommand("Survival Water Raw", actions, "3 浇原水 · 1份", 0, 44, 324, "watercrop", 0);
            SurvivalCommand("Survival Water Clean", actions, "3 浇净水 · 1份", 334, 44, 324, "watercrop", 1);
            SurvivalCommand("Survival Harvest", actions, "4 收获成熟作物", 0, 88, 658, "harvest", 0);
            survivalSow = sow.button;
            survivalPlotText = FlowLabel("Survival Selected Plot", content, "", 680, 100, 14, Accent);
            FlowLabel("Survival Farming Guide", content,
                "左侧选地后可连续操作，不会关闭面板。翻土需草地 / 泥土、木材 1，避开救援区与占地。浇水每份 +40 湿度；需湿度至少 12 且温度适宜才生长。枯死作物可重新播种。下方点击已有农田可定位，超距请先关闭面板走近。",
                680, 108, 14, Muted);
            for (int i = 0; i < SurvivalCatalog.MaxPlots; i++)
            {
                SurvivalRow row = MakeSurvivalRow("Survival Plot Slot " + i, content, -1, 680, 88, true);
                Set(row.action.label, "选择地块");
                var selected = row;
                row.action.button.onClick.RemoveAllListeners();
                row.action.button.onClick.AddListener(() =>
                {
                    DemoAudio.UiClick();
                    var plot = FindSurvivalPlotById(bridge.World, selected.id);
                    if (plot == null) return;
                    survivalX = plot.x; survivalY = plot.y; survivalHasTarget = true;
                    ResetScroll(survivalScrolls[2]); RefreshAll();
                });
                row.root.gameObject.SetActive(false);
                survivalPlots.Add(row);
            }
        }

        UnityEngine.UI.Button survivalSow;

        void MakeSurvivalWildlife()
        {
            RectTransform content = survivalPages[3];
            FlowLabel("Survival Wildlife Guide", content,
                "尸体 · 近身 2.4 格且视线畅通可分解；一次取走剩余肉，库存满则拒绝。狩猎使用原有攻击操作。下方为完整物种图鉴，记录性情与产物，不代表附近已生成。", 1048, 54, 14, Muted);
            survivalCorpseHeading = FlowLabel("Survival Corpse Heading", content, "", 1048, 32, 16, Accent);
            for (int i = 0; i < SurvivalCatalog.MaxCreatures; i++)
            {
                SurvivalRow row = MakeSurvivalRow("Survival Corpse Slot " + i, content, -1, 1048, 88, true);
                Set(row.action.label, "分解取肉");
                var selected = row;
                BindSurvivalRow(row, "butcher", () => selected.id);
                row.root.gameObject.SetActive(false);
                survivalCorpses.Add(row);
            }
            FlowLabel("Survival Creature Catalog Heading", content, "物种图鉴 · " + SurvivalCatalog.Creatures.Count + " 种", 1048, 32, 17, Accent);
            foreach (var creature in SurvivalCatalog.Creatures)
            {
                SurvivalRow row = MakeSurvivalRow("Survival Creature " + (int)creature.kind, content, (int)creature.kind, 1048, 112, false);
                SetHudArt(row.icon, "creature_" + row.id);
                string behaviour = creature.temperament == CreatureTemperament.Predator ? (creature.huntsSmallPrey ? "捕食 · 猎小动物、避人" : "捕食 · 会主动追猎") :
                    creature.temperament == CreatureTemperament.Defensive ? "防御 · 靠近 " + creature.aggroRange.ToString("0.#") + " 格或受击会反击" :
                    creature.temperament == CreatureTemperament.Docile ? "温和 · 遇险逃离" : "胆小 · 接近即逃";
                Set(row.text, creature.name + "  /  " + behaviour + (creature.huntsSmallPrey ? " · 捕食小动物" : "") +
                    "\n生命 " + creature.health.ToString("0") + " · 伤害 " + creature.damage.ToString("0.#") + " · 速度 " + creature.speed.ToString("0.#") +
                    " · 分解 " + SurvivalCatalog.Food((int)creature.food).name + " ×" + creature.meat +
                    "\n栖息地：" + SurvivalHabitat(creature.biomeMask));
            }
        }

        TextMeshProUGUI survivalCorpseHeading;

        partial void RefreshAdditionalPanels(WorldState world, PlayerState player)
        {
            bool enabled = world.survival != null && world.survival.enabled;
            var needs = player.needs;
            bool ready = enabled && needs != null && needs.initialized;
            if (ready)
            {
                Set(survivalHudWater, "水分 " + needs.thirst.ToString("0") + " / 100");
                survivalWaterFill.anchorMax = new Vector2(Mathf.Clamp01(needs.thirst / 100), 1);
                survivalWaterFill.GetComponent<UnityEngine.UI.Image>().color = needs.thirst < 15 ? Amber : Accent;
                Set(survivalHudStatus, SurvivalWeatherText(world) + "\n体温 " + needs.bodyTemperature.ToString("0.0") + "℃ · 感染 " + needs.infection.ToString("0") +
                    " · 营养 " + needs.nutrition.ToString("0") + "\n" + SurvivalRisk(needs));
                survivalHudStatus.color = SurvivalRisk(needs) == "状态平稳" ? Muted : Amber;
            }
            else
            {
                Set(survivalHudWater, "生存 " + (enabled ? "同步中" : "未启用"));
                survivalWaterFill.anchorMax = new Vector2(0, 1);
                Set(survivalHudStatus, enabled ? "等待主机同步生存状态" : "旧世界：不消耗生存数值\n不补发水、食物或种子");
                survivalHudStatus.color = Muted;
            }
            if (!survivalPanel.gameObject.activeSelf) return;
            Set(survivalStatus, !enabled ? "此存档未启用生存：不消耗新增生存数值，也不补发物资。原有生命 / 饱食 / 体力规则保留；本页仅供查阅。" :
                !ready ? "正在等待主机同步生存状态，操作暂不可用。" :
                SurvivalWeatherText(world) + " · 水分 " + needs.thirst.ToString("0") + " · 体温 " + needs.bodyTemperature.ToString("0.0") + "℃" +
                " · 感染 " + needs.infection.ToString("0") + " · 营养 " + needs.nutrition.ToString("0") +
                "\n湿润 " + needs.wetness.ToString("0") + " · 干燥 " + needs.dryness.ToString("0") + " · 移速 ×" + needs.movementMultiplier.ToString("0.00") +
                " · 饱食消耗 ×" + needs.hungerMultiplier.ToString("0.00") + " · " + SurvivalRisk(needs));
            for (int i = 0; i < survivalPages.Length; i++)
            {
                survivalScrolls[i].gameObject.SetActive(i == survivalTab);
                Tint(survivalTabs[i], i == survivalTab ? Selected : Raised);
            }
            survivalTarget.gameObject.SetActive(survivalTab == 0 || survivalTab == 2);
            if (survivalTarget.gameObject.activeSelf) RefreshSurvivalTargets(world, player);
            for (int i = 0; i < survivalCommands.Count; i++)
                survivalCommands[i].button.interactable = SurvivalPermit(survivalActions[i], survivalArguments[i], survivalX, survivalY, out _);
            if (survivalTab == 0)
            {
                Set(survivalWaterText, ready ? "随身原水 " + needs.waterRaw + " / 净水 " + needs.waterClean + " · 原水饮用会增加感染\n在烹饪页煮水：原水 1 + 燃料 1 → 净水 1" : "水库存：未启用或等待同步，不发放物资。");
                foreach (var row in survivalFoods)
                {
                    var f = SurvivalCatalog.Food(row.id);
                    Set(row.text, f.name + " ×" + (ready ? Count(needs.foods, row.id).ToString() : "—") + (f.edible ? "" : " · 加工原料") +
                        "\n饱食 " + Signed(f.hunger) + " · 水分 " + Signed(f.hydration) + " · 营养 " + Signed(f.nutrition) +
                        "\n体温 " + Signed(f.warmth) + "℃ · 感染 " + Signed(f.infection) + " · 治疗 " + Signed(f.healing));
                    row.text.color = f.infection > 0 ? Amber : Ink;
                    row.action.button.interactable = SurvivalPermit("eatfood", row.id, 0, 0, out _);
                }
            }
            if (survivalTab == 1) RefreshSurvivalCooking(world, player);
            if (survivalTab == 2) RefreshSurvivalFarming(world, player);
            if (survivalTab == 3) RefreshSurvivalWildlife(world, player);
            bool newHostNotice = player.noticeUntil > world.elapsed &&
                (player.notice != survivalPreviousNotice || player.noticeUntil != survivalPreviousNoticeUntil);
            Set(survivalFeedback, CurrentLocalFeedback() && localFeedbackError ? Clean(localFeedback) :
                newHostNotice ? Clean(player.notice) : CurrentLocalFeedback() ? Clean(localFeedback) : player.noticeUntil > world.elapsed ? Clean(player.notice) :
                !enabled ? "未启用：所有生存操作关闭。新生成且启用生存的世界才使用上述流程。" :
                player.interactCooldown > 0 || Time.unscaledTime < survivalSendAt ? "交互冷却中，请稍候…" : "面板不暂停世界；动作由主机验证。操作不会关闭此面板，J / Esc 返回。");
        }

        void RefreshSurvivalTargets(WorldState world, PlayerState player)
        {
            int cx = Mathf.FloorToInt(player.x + .5f), cy = Mathf.FloorToInt(player.y + .5f);
            if (!survivalHasTarget) { survivalX = cx; survivalY = cy; survivalHasTarget = true; }
            for (int i = 0; i < survivalCells.Length; i++)
            {
                int x = cx + i % 5 - 2, y = cy + 2 - i / 5;
                bool inside = SurvivalInside(world, x, y);
                string title = inside ? x + "," + y : "边界";
                if (inside && SurvivalGround(world, x, y) == '2') title += " 水";
                else if (inside && FindSurvivalPlot(world, x, y) != null) title += " 田";
                Set(survivalCells[i].label, title);
                survivalCells[i].button.interactable = inside;
                Tint(survivalCells[i], x == survivalX && y == survivalY ? Selected : Raised);
                survivalCells[i].label.color = SurvivalInReach(world, player, x, y) ? Ink : Muted;
            }
            Set(survivalTargetText, "所选：(" + survivalX + ", " + survivalY + ") · " +
                (SurvivalInside(world, survivalX, survivalY) ? SurvivalGroundName(SurvivalGround(world, survivalX, survivalY)) : "地图外") +
                "\n距离 " + Vector2.Distance(new Vector2(player.x, player.y), new Vector2(survivalX, survivalY)).ToString("0.0") + " / 2.4 格");
            string reason;
            if (!SurvivalInReach(world, player, survivalX, survivalY)) reason = "目标超出 2.4 格或视线受阻；请关闭面板走近。";
            else if (survivalTab == 0) reason = SurvivalGround(world, survivalX, survivalY) == '2' ? "已选水域，可收集原水。净水需在石炉煮沸。" : "请在附近格子中选择标有「水」的格子。";
            else if (FindSurvivalPlot(world, survivalX, survivalY) != null) reason = "已选农田。播种 / 浇水 / 收获使用本格，不会取消作物选择。";
            else { SurvivalPermit("till", 0, survivalX, survivalY, out reason); if (reason.Length == 0) reason = "可翻土，消耗木材 1。随后选择作物播种。"; }
            Set(survivalTargetHelp, reason);
        }

        void RefreshSurvivalCooking(WorldState world, PlayerState player)
        {
            foreach (var row in survivalRecipes)
            {
                var recipe = row.id < 0 ? null : SurvivalCatalog.Recipe(row.id);
                BuildKind kind = recipe == null ? BuildKind.StoneFurnace : recipe.station;
                bool station = FindSurvivalStation(world, player, kind, out int x, out int y);
                string action = recipe == null ? "boilwater" : "cook";
                bool can = SurvivalPermit(action, recipe == null ? 0 : row.id, x, y, out string reason);
                var n = player.needs;
                var cost = new StringBuilder();
                if (recipe == null) cost.Append("原水 ").Append(n == null ? 0 : n.waterRaw).Append("/1");
                else for (int i = 0; i < recipe.foods.Length; i++) if (recipe.foods[i] > 0)
                { if (cost.Length > 0) cost.Append(" · "); cost.Append(SurvivalCatalog.Food(i).name).Append(' ').Append(Count(n?.foods, i)).Append('/').Append(recipe.foods[i]); }
                cost.Append(" · 净水 ").Append(n == null ? 0 : n.waterClean).Append('/').Append(recipe == null ? 0 : recipe.cleanWater);
                cost.Append(" · 燃料 ").Append(Count(player.items, (int)ItemKind.Fuel)).Append('/').Append(recipe == null ? 1 : recipe.fuel);
                Set(row.text, (recipe == null ? "原水煮沸 → 净水 ×1" : recipe.name + " → " + SurvivalCatalog.Food((int)recipe.output).name + " ×" + recipe.quantity) +
                    "\n" + cost + "\n" + (kind == BuildKind.Workbench ? "工作台" : "石炉") +
                    (station ? " (" + x + "," + y + ")" : " · 无附近合法工作站") + (can ? " · 就绪" : " · " + reason));
                row.action.button.interactable = station && can;
            }
        }

        void RefreshSurvivalFarming(WorldState world, PlayerState player)
        {
            var crop = SurvivalCatalog.Crop(survivalCrop);
            SetHudArt(survivalCropIcon, "crop_" + survivalCrop + "_0");
            Set(survivalCropText, "作物 " + (survivalCrop + 1) + "/" + SurvivalCatalog.CropCount + "：" + crop.name + " · 种子 " + Count(player.needs?.seeds, survivalCrop) +
                "\n适温 " + crop.minTemperature + "～" + crop.maxTemperature + "℃ · 生长 " + crop.growSeconds + "秒 · 耗水 " + crop.waterPerSecond.ToString("0.###") + "/秒" +
                "\n收获 " + SurvivalCatalog.Food((int)crop.food).name + " ×" + crop.harvestCount + " / 种子 ×" + crop.seedYield);
            survivalSow.interactable = SurvivalPermit("sow", survivalCrop, survivalX, survivalY, out _);
            var selected = FindSurvivalPlot(world, survivalX, survivalY);
            bool canSow = SurvivalPermit("sow", survivalCrop, survivalX, survivalY, out string sowReason);
            Set(survivalPlotText, (selected == null ? "当前格未开垦。先翻土，再播种。" : SurvivalPlotDescription(selected)) +
                "\n随身原水 " + (player.needs?.waterRaw ?? 0) + " / 净水 " + (player.needs?.waterClean ?? 0) + " / 木材 " + Count(player.items, (int)ItemKind.Wood) +
                "\n" + (canSow ? "播种就绪：消耗所选种子 1。" : "播种：" + sowReason));
            var plots = world.survival?.plots;
            for (int i = 0; i < survivalPlots.Count; i++)
            {
                var row = survivalPlots[i];
                var plot = plots != null && i < plots.Count ? plots[i] : null;
                row.root.gameObject.SetActive(plot != null);
                if (plot == null) continue;
                row.id = plot.id;
                Set(row.text, "(" + plot.x + "," + plot.y + ") " + SurvivalPlotDescription(plot));
                SetHudArt(row.icon, plot.crop < 0 ? null : "crop_" + plot.crop + (plot.growth >= 1 ? "_1" : "_0"));
            }
        }

        void RefreshSurvivalWildlife(WorldState world, PlayerState player)
        {
            int index = 0;
            if (world.survival?.creatures != null) foreach (var c in world.survival.creatures)
            {
                if (c.hp > 0 || c.meatLeft <= 0 || index >= survivalCorpses.Count) continue;
                var row = survivalCorpses[index++];
                row.root.gameObject.SetActive(true); row.id = c.id;
                var d = SurvivalCatalog.Creatures[(int)c.kind];
                bool can = SurvivalPermit("butcher", c.id, 0, 0, out string reason);
                Set(row.text, d.name + "尸体 (" + c.x.ToString("0.0") + "," + c.y.ToString("0.0") + ") · 距离 " +
                    Vector2.Distance(new Vector2(player.x, player.y), new Vector2(c.x, c.y)).ToString("0.0") + " 格" +
                    "\n" + SurvivalCatalog.Food((int)d.food).name + " ×" + c.meatLeft + " · " + (can ? "可分解" : reason));
                SetHudArt(row.icon, "creature_" + (int)c.kind);
                row.action.button.interactable = can;
            }
            Set(survivalCorpseHeading, index == 0 ? "当前无可分解尸体" : "当前尸体 " + index + " · 走近后分解");
            for (int i = index; i < survivalCorpses.Count; i++) survivalCorpses[i].root.gameObject.SetActive(false);
        }

        void RequestSurvivalStation(string action, int arg, BuildKind station)
        {
            if (!FindSurvivalStation(bridge.World, bridge.LocalPlayer, station, out int x, out int y))
            { Notify("请靠近已解锁的" + (station == BuildKind.Workbench ? "工作台" : "石炉") + "，保持 2.4 格内且视线畅通。", true); return; }
            SurvivalRequest(action, arg, x, y);
        }

        void SurvivalRequest(string action, int arg, int x, int y)
        {
            if (!SurvivalPermit(action, arg, x, y, out string reason)) { Notify(reason, true); RefreshAll(); return; }
            localFeedback = "";
            survivalPreviousNotice = bridge.LocalPlayer.notice;
            survivalPreviousNoticeUntil = bridge.LocalPlayer.noticeUntil;
            Send(action, arg, x, y);
            survivalSendAt = Time.unscaledTime + .4f;
            if (!CurrentLocalFeedback()) Notify("已发送生存操作请求，结果以主机反馈为准。");
            RefreshAll();
        }

        // Snapshot-only affordance checks mirror the Core gates. The host remains authoritative.
        bool SurvivalPermit(string action, int arg, int x, int y, out string reason)
        {
            var w = bridge.World; var p = bridge.LocalPlayer; var n = p?.needs;
            reason = "";
            if (!bridge.IsRunning || w == null || p == null || !p.connected || p.hp <= 0) reason = "角色尚未就绪";
            else if (w.survival == null || !w.survival.enabled) reason = "此存档未启用生存；不消耗、不补发物资";
            else if (n == null || !n.initialized) reason = "等待主机同步生存状态";
            else if (p.interactCooldown > 0 || Time.unscaledTime < survivalSendAt) reason = "交互冷却中";
            if (reason.Length > 0) return false;
            bool cellAction = action == "collectwater" || action == "boilwater" || action == "cook" ||
                action == "till" || action == "sow" || action == "watercrop" || action == "harvest";
            if (cellAction && !SurvivalInReach(w, p, x, y)) { reason = "超出 2.4 格或视线受阻"; return false; }
            switch (action)
            {
                case "eatfood":
                    var food = SurvivalCatalog.Food(arg);
                    if (food == null || !food.edible) reason = "此原料需加工";
                    else if (Count(n.foods, arg) < 1) reason = "没有此食物";
                    else if (p.hunger >= 99 && n.thirst >= 99 && n.nutrition >= 99 &&
                        (food.infection >= 0 || n.infection <= 0) && (food.healing <= 0 || p.hp >= WorldSimulator.PlayerMaxHealth(p))) reason = "状态已满，无需食用";
                    break;
                case "drink":
                    if (arg < 0 || arg > 1 || (arg == 0 ? n.waterRaw : n.waterClean) < 1) reason = "没有所选水";
                    else if (n.thirst >= 99) reason = "水分已充足";
                    break;
                case "collectwater":
                    if (arg != 0 || SurvivalGround(w, x, y) != '2') reason = "请选择附近水域";
                    else if (n.waterRaw >= SurvivalCatalog.StackLimit) reason = "原水库存已满";
                    break;
                case "boilwater":
                    if (!SurvivalStationAt(w, p, x, y, BuildKind.StoneFurnace)) reason = "需要附近合法石炉";
                    else if (arg != 0 || n.waterRaw < 1 || Count(p.items, (int)ItemKind.Fuel) < 1) reason = "需要原水 1、燃料 1";
                    else if (n.waterClean >= SurvivalCatalog.StackLimit) reason = "净水库存已满";
                    break;
                case "cook":
                    var recipe = SurvivalCatalog.Recipe(arg);
                    if (recipe == null) reason = "配方不存在";
                    else if (!SurvivalStationAt(w, p, x, y, recipe.station)) reason = "需要附近合法工作站";
                    else if (n.waterClean < recipe.cleanWater || Count(p.items, (int)ItemKind.Fuel) < recipe.fuel) reason = "净水或燃料不足";
                    else for (int i = 0; i < recipe.foods.Length; i++)
                    {
                        if (Count(n.foods, i) < recipe.foods[i]) { reason = "缺少 " + SurvivalCatalog.Food(i).name; break; }
                        if (Count(n.foods, i) - recipe.foods[i] + ((int)recipe.output == i ? recipe.quantity : 0) > SurvivalCatalog.StackLimit) { reason = "食物库存空间不足"; break; }
                    }
                    break;
                case "till":
                    if (FindSurvivalPlot(w, x, y) != null) reason = "此处已有农田";
                    else if (w.survival.plots.Count >= SurvivalCatalog.MaxPlots) reason = "农田数量达到上限";
                    else if (SurvivalGround(w, x, y) != '0' && SurvivalGround(w, x, y) != '1') reason = "只能在草地、泥土翻土";
                    else if (SurvivalBuilding(w, x, y) != null || SurvivalNode(w, x, y)) reason = "设备或资源占据此格";
                    else if (Math.Abs(x - w.width / 2) <= 1 && Math.Abs(y - (w.height / 2 + 3)) <= 1) reason = "救援区不可开垦";
                    else if (SurvivalActorAt(w, x, y)) reason = "人物或生物占据此格";
                    else if (Count(p.items, (int)ItemKind.Wood) < 1) reason = "需要木材 1";
                    else if (w.nextId >= int.MaxValue - 2) reason = "世界实体数量达到上限";
                    break;
                case "sow": case "watercrop": case "harvest":
                    var plot = FindSurvivalPlot(w, x, y);
                    if (plot == null) reason = "请先翻土";
                    else if (SurvivalBuilding(w, x, y) != null || SurvivalNode(w, x, y)) reason = "农田被占据";
                    else if (action == "sow")
                    {
                        if (SurvivalCatalog.Crop(arg) == null || Count(n.seeds, arg) < 1) reason = "所选作物种子不足";
                        else if (plot.crop >= 0 && plot.health > 0) reason = "已有生长作物，请先收获";
                    }
                    else if (action == "watercrop")
                    {
                        if (arg < 0 || arg > 1 || (arg == 0 ? n.waterRaw : n.waterClean) < 1) reason = "所选水不足";
                        else if (plot.moisture >= 99) reason = "农田湿度已充足";
                    }
                    else
                    {
                        var crop = SurvivalCatalog.Crop(plot.crop);
                        if (crop == null || plot.growth < 1 || plot.health <= 0) reason = "作物未成熟或已枯死";
                        else if (Count(n.foods, (int)crop.food) > SurvivalCatalog.StackLimit - crop.harvestCount ||
                            Count(n.seeds, plot.crop) > SurvivalCatalog.StackLimit - crop.seedYield) reason = "食物或种子库存空间不足";
                    }
                    break;
                case "butcher":
                    CreatureState corpse = w.survival.creatures.Find(c => c.id == arg);
                    if (corpse == null || corpse.hp > 0 || corpse.meatLeft <= 0) reason = "尸体已消失或无剩余产物";
                    else if (!SurvivalInReach(w, p, corpse.x, corpse.y)) reason = "走近尸体 2.4 格内并保持视线畅通";
                    else if (Count(n.foods, (int)SurvivalCatalog.Creatures[(int)corpse.kind].food) > SurvivalCatalog.StackLimit - corpse.meatLeft) reason = "肉类库存已满";
                    break;
                default: reason = "未知生存操作"; break;
            }
            return reason.Length == 0;
        }

        static bool SurvivalInside(WorldState w, int x, int y) => w != null && x >= 0 && y >= 0 && x < w.width && y < w.height;
        static char SurvivalGround(WorldState w, int x, int y) => SurvivalInside(w, x, y) && w.ground != null && x + y * w.width < w.ground.Length ? w.ground[x + y * w.width] : '?';
        static string SurvivalGroundName(char g) => g == '0' ? "草地" : g == '1' ? "泥土" : g == '2' ? "水域" : g == '3' ? "道路" : g == '4' ? "地板" : "未知地形";
        static FarmPlotState FindSurvivalPlot(WorldState w, int x, int y) => w?.survival?.plots?.Find(p => p.x == x && p.y == y);
        static FarmPlotState FindSurvivalPlotById(WorldState w, int id) => w?.survival?.plots?.Find(p => p.id == id);
        static BuildingState SurvivalBuilding(WorldState w, int x, int y) => w.buildings.Find(b => DemoCatalog.Contains(b, x, y));
        static bool SurvivalNode(WorldState w, int x, int y) => w.nodes.Exists(n => n.x == x && n.y == y);

        static bool SurvivalActorAt(WorldState w, int x, int y)
        {
            foreach (var p in w.players) if (p.hp > 0 && Math.Abs(p.x - x) < .8f && Math.Abs(p.y - y) < .8f) return true;
            foreach (var z in w.zombies) if (Math.Abs(z.x - x) < .8f && Math.Abs(z.y - y) < .8f) return true;
            foreach (var c in w.survival.creatures) if (Math.Abs(c.x - x) < .8f && Math.Abs(c.y - y) < .8f) return true;
            return false;
        }

        static bool SurvivalInReach(WorldState w, PlayerState p, float tx, float ty)
        {
            if (w == null || p == null) return false;
            int ix = Mathf.FloorToInt(tx + .5f), iy = Mathf.FloorToInt(ty + .5f);
            float dx = tx - p.x, dy = ty - p.y;
            if (!SurvivalInside(w, ix, iy) || dx * dx + dy * dy > 2.4f * 2.4f) return false;
            var ignored = SurvivalBuilding(w, ix, iy);
            int sx = Mathf.FloorToInt(p.x + .5f), sy = Mathf.FloorToInt(p.y + .5f);
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / .16));
            for (int i = 1; i <= steps; i++)
            {
                int x = Mathf.FloorToInt(p.x + dx * i / steps + .5f), y = Mathf.FloorToInt(p.y + dy * i / steps + .5f);
                if ((x == ix && y == iy) || (x == sx && y == sy) || (ignored != null && DemoCatalog.Contains(ignored, x, y))) continue;
                if (!SurvivalInside(w, x, y) || SurvivalGround(w, x, y) == '2') return false;
                foreach (var n in w.nodes) if (n.x == x && n.y == y && n.hp > 0 && n.stock > 0) return false;
                var b = SurvivalBuilding(w, x, y);
                if (b != null && b.hp > 0 && b.kind != BuildKind.Belt) return false;
            }
            return true;
        }

        static bool SurvivalStationAt(WorldState w, PlayerState p, int x, int y, BuildKind kind)
        {
            if (!SurvivalInReach(w, p, x, y)) return false;
            var b = SurvivalBuilding(w, x, y);
            return b != null && b.hp > 0 && b.kind == kind && TechCatalog.IsUnlocked(w, kind);
        }

        static bool FindSurvivalStation(WorldState w, PlayerState p, BuildKind kind, out int x, out int y)
        {
            x = y = -1;
            if (w == null || p == null || !TechCatalog.IsUnlocked(w, kind)) return false;
            foreach (var b in w.buildings)
            {
                if (b.kind != kind || b.hp <= 0) continue;
                for (int cy = b.y; cy < b.y + DemoCatalog.Height(b); cy++)
                    for (int cx = b.x; cx < b.x + DemoCatalog.Width(b); cx++)
                        if (SurvivalInReach(w, p, cx, cy)) { x = cx; y = cy; return true; }
            }
            return false;
        }

        static string Signed(float v) => v.ToString("+0.##;-0.##;0");
        static string SurvivalWeatherText(WorldState w)
        {
            var weather = w.survival?.weather;
            if (weather == null) return "天气未同步";
            int kind = (int)weather.kind;
            return (kind >= 0 && kind < SurvivalCatalog.WeatherNames.Count ? SurvivalCatalog.WeatherNames[kind] : "未知天气") +
                " · 天气基温 " + weather.temperature.ToString("0.0") + "℃";
        }

        static string SurvivalRisk(SurvivalPlayerState n)
        {
            var r = new List<string>();
            if (n.thirst < 15) r.Add(n.thirst <= 0 ? "脱水" : "缺水");
            if (n.bodyTemperature < 35) r.Add("失温"); else if (n.bodyTemperature > 39) r.Add("过热");
            if (n.infection > 35) r.Add("感染风险");
            if (n.dryness > 65) r.Add("干燥");
            if (n.nutrition <= 0) r.Add("营养耗尽");
            return r.Count == 0 ? "状态平稳" : string.Join(" / ", r);
        }

        static string SurvivalPlotDescription(FarmPlotState plot)
        {
            var crop = SurvivalCatalog.Crop(plot.crop);
            return (crop == null ? "空田" : crop.name + (plot.health <= 0 ? " · 枯死，可重播" : plot.growth >= 1 ? " · 已成熟" : " · 生长 " + (plot.growth * 100).ToString("0") + "%")) +
                "\n湿度 " + plot.moisture.ToString("0") + " / 100 · 健康 " + plot.health.ToString("0") + " / 100";
        }

        static string SurvivalHabitat(int mask)
        {
            var names = new List<string>();
            for (int i = 0; i < WorldGenerationCatalog.Biomes.Count; i++) if ((mask & (1 << i)) != 0) names.Add(WorldGenerationCatalog.Biomes[i].name);
            return string.Join("、", names);
        }
    }
}
