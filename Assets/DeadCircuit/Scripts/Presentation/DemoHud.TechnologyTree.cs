using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        const float TreeWidth = 816, TreeHeight = 424, NodeWidth = 144, NodeHeight = 70;
        const float TreeColumn = 183, TreeRow = 94, TreeTop = 118;
        const float DefaultTreeZoom = .9f;
        static readonly Color TreeLocked = new Color32(30, 43, 42, 255);
        static readonly Color TreeComplete = new Color32(36, 68, 56, 255);
        static readonly Color TreeReady = new Color32(76, 58, 32, 255);
        static readonly Color TreeWire = new Color32(78, 105, 97, 255);
        static readonly Color[] EraColors =
        {
            new Color32(190, 177, 134, 255), new Color32(99, 183, 212, 255),
            new Color32(220, 159, 105, 255), new Color32(225, 198, 111, 255),
            new Color32(144, 200, 157, 255), new Color32(159, 170, 230, 255),
            new Color32(218, 155, 139, 255)
        };
        static readonly string[] TreeResearchNames =
        {
            "Primitive Research ", "Water Research ", "Steam Research ",
            "Low Voltage Research ", "Petrochemical Research ", "Precision Node ",
            "Nuclear Research "
        };
        static readonly BuildKind[] TreeEraIcons =
        {
            BuildKind.Workbench, BuildKind.Waterwheel, BuildKind.SteamEngine, BuildKind.Generator,
            BuildKind.DieselGenerator, BuildKind.PrecisionAssembler, BuildKind.Reactor
        };
        static readonly string[][] TreeProcessIcons =
        {
            new[] { "item_25", "item_27", "item_28", "item_8", "item_9", "item_2", "machine_0", "ui_survival" },
            new[] { "machine_12", "machine_13", "machine_14", "machine_38", "machine_39", "item_30", "machine_43", "machine_41" },
            new[] { "machine_44", "machine_45", "machine_46", "machine_47", "machine_48", "item_39", "machine_15", "machine_35" },
            new[] { "machine_6", "item_42", "item_44", "machine_52", "item_45", "machine_5", "machine_50", "machine_51" },
            new[] { "ui_research", "machine_53", "machine_56", "item_50", "item_12", "item_51", "machine_24", "machine_30" },
            new[] { "item_56", "item_57", "item_58", "item_13", "machine_27", "item_61", "item_60", "machine_26" },
            new[] { "machine_27", "machine_28", "machine_61", "machine_62", "item_61", "machine_30", "machine_28", "machine_62", "item_62", "ui_survival", "gear_19" }
        };

        sealed class TreeCard
        {
            public TechnologyTreeNode node;
            public Vector2 position;
            public UiButton button;
            public TextMeshProUGUI status;
            public UnityEngine.UI.Image stripe, selection;
        }

        readonly Dictionary<int, TreeCard> treeCards = new Dictionary<int, TreeCard>();
        readonly List<TreeCard> orderedTreeCards = new List<TreeCard>();
        readonly List<(int parent, int child, TechnologyTreeLines.Path path)> treeLinks =
            new List<(int, int, TechnologyTreeLines.Path)>();
        readonly int[] treeEraCounts = new int[7];
        readonly int[] treeEraCompleted = new int[7];
        readonly UiButton[] treeDomainButtons = new UiButton[4];
        readonly RectTransform[] treeEraRegions = new RectTransform[7];
        readonly RectTransform[] treeEraRules = new RectTransform[7];
        readonly TextMeshProUGUI[] treeEraBands = new TextMeshProUGUI[7];
        readonly List<UiButton> treeParentButtons = new List<UiButton>();
        RectTransform treeParentLinks;
        int treeDomainFilter = -1; // All; remaining values are explicit ResearchDomain values.
        TechnologyTreeViewport treeViewport;
        TechnologyTreeLines treeLines;
        RectTransform treeContent, technologyGuide;
        UnityEngine.UI.ScrollRect treeDetailsScroll;
        TextMeshProUGUI treeHeading, treeTabTitle, treeTitle, treeState, treeDescription, treeParents, treeCost, treeRequirement, treeEvidence, treeZoomLabel;
        UiButton treeGuideToggle;
        int selectedTreeKey;
        float treeZoom = DefaultTreeZoom;
        float treeResearchSentAt;
        int treeResearchSentKey = -1;

        void MakeTechnology(RectTransform root)
        {
            technology = Backdrop("Technology Panel", root, new Color32(4, 12, 8, 226));
            RectTransform card = Box("Technology Card", technology, 0, 0, 1208, 656, Panel, true);
            Center(card, 1208, 656);
            PanelRule(card, 1208, 66);
            Label("Technology Heading", card, "科技树", 22, 14, 118, 38, 27, Ink, FontStyles.Bold);
            treeHeading = Label("Technology Progress", card, "", 147, 19, 695, 30, 15, Muted);
            treeGuideToggle = Button("Technology Guide Toggle", card, "工艺与建造", 926, 17, 150, 34, ToggleTechnologyGuide);
            SetButtonArt(treeGuideToggle, "ui_craft", 19);
            SetButtonArt(Button("Close Technology", card, "返回", 1084, 17, 102, 34, () => SetPanel(technology, false)), "ui_back", 18);

            for (int i = 0; i < eraButtons.Length; i++)
            {
                int era = i;
                eraButtons[i] = Button("Technology Era " + i, card, "", 22 + i * 116, 77, 110, 37, () => BrowseEra(era));
                eraButtons[i].label.fontSizeMax = 15;
            }

            string[] domainNames = { "All", "Industry", "Weapon", "Tool" };
            string[] domainLabels = { "全部", "工业", "武器", "工具" };
            for (int i = 0; i < treeDomainButtons.Length; i++)
            {
                int filter = i - 1;
                treeDomainButtons[i] = Button("Technology Domain " + domainNames[i], card, domainLabels[i],
                    22 + i * 100, 122, 94, 30, () => BrowseTechnologyDomain(filter));
            }
            RectTransform map = Box("Technology Tree Canvas", card, 22, 162, TreeWidth, TreeHeight, Recessed, true);
            treeViewport = map.gameObject.AddComponent<TechnologyTreeViewport>();
            treeViewport.horizontal = treeViewport.vertical = true;
            treeViewport.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            treeViewport.inertia = false;
            treeViewport.zoom = ZoomTechnologyTree;
            RectTransform viewport = NewRect("Technology Tree Viewport", map);
            Stretch(viewport);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            treeContent = NewRect("Technology Tree Content", viewport);
            treeViewport.content = treeContent;
            treeViewport.viewport = viewport;
            MakeTechnologyTree();

            RectTransform controls = Box("Technology Tree Controls", card, 22, 595, TreeWidth, 37, HudSurface);
            Label("Technology Navigation Hint", controls, "拖动画布 · 滚轮缩放 · 选择节点查看条件", 9, 0, 470, 37, 14, Muted);
            Button("Technology Zoom Out", controls, "−", 553, 2, 34, 31, () => ZoomTechnologyTreeAtCenter(-1));
            treeZoomLabel = Label("Technology Zoom Level", controls, "90%", 590, 1, 51, 34, 14, Ink);
            treeZoomLabel.alignment = TextAlignmentOptions.Center;
            Button("Technology Zoom In", controls, "+", 644, 2, 34, 31, () => ZoomTechnologyTreeAtCenter(1));
            Button("Technology Focus Current", controls, "定位当前", 694, 2, 114, 31, FocusCurrentTechnology);

            technologyGuide = Box("Technology Guide", card, 22, 162, TreeWidth, 470, Recessed, true);
            MakeTechnologyGuide(technologyGuide);
            technologyGuide.gameObject.SetActive(false);

            RectTransform details = Box("Technology Details", card, 852, 77, 334, 555, HudSurface, true);
            treeTabTitle = Label("Technology Details Era", details, "", 14, 12, 302, 22, 14, Muted);
            treeTitle = Label("Technology Selected Node", details, "", 14, 37, 306, 34, 24, Ink, FontStyles.Bold);
            treeState = Label("Technology Selected State", details, "", 14, 73, 306, 24, 15, Accent, FontStyles.Bold);
            Box("Technology Details Rule", details, 14, 105, 306, 1, Edge);
            RectTransform body = ScrollContent("Technology Details Scroll View", details, 12, 116, 310, 338, out treeDetailsScroll);
            treeDescription = FlowLabel("Technology Node Description", body, "", 282, 62, 16, Ink);
            treeParents = FlowLabel("Technology Prerequisites", body, "", 282, 32, 15, Ink);
            treeParentLinks = FlowRow("Technology Prerequisite Links", body, 282, 1);
            treeCost = FlowLabel("Technology Research Materials", body, "", 282, 36, 15, Ink);
            treeRequirement = FlowLabel("Technology Research Requirement", body, "", 282, 48, 15, Amber);
            treeEvidence = FlowLabel("Technology Production Progress", body, "", 282, 32, 14, Muted);
            researchButton = Button("Research Button", details, "", 12, 464, 310, 44, Research, true);
            SetButtonArt(researchButton, "ui_research", 24);
            technologyNotice = Label("Technology Feedback", details, "", 14, 510, 306, 43, 13, Muted);
            technology.gameObject.SetActive(false);
        }

        void MakeTechnologyGuide(RectTransform panel)
        {
            RectTransform content = ScrollContent("Technology Scroll View", panel, 0, 0, TreeWidth, 470, out technologyScroll);
            technologyTitle = FlowLabel("Technology Era Title", content, "", 768, 34, 21, Accent);
            technologyDescription = FlowLabel("Technology Description", content, "", 768, 56, 16, Ink);
            MakeWaterTechnology(content);
            MakeSteamTechnology(content);
            MakeLowVoltageTechnology(content);
            MakePetrochemistryTechnology(content);
            FlowLabel("Era Buildings Heading", content, "建造目录 · 选择已解锁设备开始建造", 768, 30, 17, Accent);
            for (int i = 0; i < technologyBuildButtons.Length; i++)
            {
                int kind = i;
                RectTransform row = FlowRow("Technology Building Row " + i, content, 768, 88);
                technologyBuildRows[i] = row;
                technologyBuildButtons[i] = Button("Technology Build " + i, row, "", 4, 8, 170, 64, () => SelectTechnologyBuild(kind));
                SetButtonArt(technologyBuildButtons[i], "machine_" + i, 22);
                TextMeshProUGUI label = Label("Building Materials", row, "", 188, 3, 564, 80, 14, Ink);
                label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.TopLeft;
                technologyBuildCosts[i] = label;
            }
        }

        void MakeTechnologyTree()
        {
            for (int era = 0; era < eraButtons.Length; era++)
            {
                treeEraRegions[era] = Box("Technology Era Region " + era, treeContent, 0, 0, 1, 1,
                    era % 2 == 0 ? new Color32(18, 36, 31, 255) : new Color32(22, 41, 36, 255));
                treeEraRules[era] = Box("Technology Era Rule " + era, treeContent, 0, 0, 1, 1, Edge);
                treeEraBands[era] = Label("Technology Era Band " + era, treeContent, TechCatalog.EraName((TechEra)era),
                    0, 0, 1, 24, 16, EraColors[era], FontStyles.Bold);
            }
            RectTransform wires = NewRect("Technology Tree Connections", treeContent);
            if (wires.GetComponent<CanvasRenderer>() == null) wires.gameObject.AddComponent<CanvasRenderer>();
            treeLines = wires.gameObject.AddComponent<TechnologyTreeLines>();
            treeLines.raycastTarget = false;
            foreach (var node in TechnologyTreeModel.All)
            {
                int key = node.key;
                UiButton button = Button(TechnologyNodeName(node), treeContent, "", 0, 0, NodeWidth, NodeHeight, () => SelectTreeNode(key));
                button.label.gameObject.SetActive(false);
                Transform panel = button.button.transform;
                var stripe = Box("Status Stripe", panel, 0, 0, 3, NodeHeight, EraColors[node.era]).GetComponent<UnityEngine.UI.Image>();
                UnityEngine.UI.Image icon = MakeHudArt("Research Icon", panel, 10, 13, 29);
                SetHudArt(icon, TechnologyIcon(node));
                if (!icon.enabled) SetHudArt(icon, "ui_research");
                Label("Node Name", panel, node.IsEra ? node.name + "时代" : node.name, 45, 8, 94, 29, 16, Ink, FontStyles.Bold);
                TextMeshProUGUI status = Label("Node Status", panel, "", 45, 36, 94, 24, 13, Muted);
                var selection = Box("Selected Edge", panel, 0, NodeHeight - 3, NodeWidth, 3, Accent).GetComponent<UnityEngine.UI.Image>();
                var entry = new TreeCard { node = node, button = button, status = status, stripe = stripe, selection = selection };
                treeCards.Add(key, entry);
                orderedTreeCards.Add(entry);
                if (!node.IsEra) treeEraCounts[node.era]++;
            }
            LayoutTechnologyTree();
        }

        bool TreeNodeVisible(TechnologyTreeNode node) => node.IsEra || treeDomainFilter < 0 || (int)node.domain == treeDomainFilter;

        void LayoutTechnologyTree()
        {
            var depths = new Dictionary<int, int>();
            var slots = new Dictionary<int, int>();
            int columnBase = 0;
            float bottom = TreeTop + NodeHeight;
            for (int era = 0; era < eraButtons.Length; era++)
            {
                int farthest = 0;
                float eraBottom = TreeTop + NodeHeight;
                foreach (var entry in orderedTreeCards)
                {
                    var node = entry.node;
                    if (node.era != era) continue;
                    bool visible = TreeNodeVisible(node);
                    entry.button.button.gameObject.SetActive(visible);
                    if (!visible) continue;
                    int depth = VisibleTreeDepth(node, depths);
                    farthest = Mathf.Max(farthest, depth);
                    int column = columnBase + depth;
                    slots.TryGetValue(column, out int slot);
                    slots[column] = slot + 1;
                    entry.position = new Vector2(26 + column * TreeColumn, TreeTop + slot * TreeRow);
                    Place((RectTransform)entry.button.button.transform, entry.position.x, entry.position.y, NodeWidth, NodeHeight);
                    eraBottom = Mathf.Max(eraBottom, entry.position.y + NodeHeight);
                }
                float left = 12 + columnBase * TreeColumn;
                float width = (farthest + 1) * TreeColumn - 8;
                Place(treeEraRegions[era], left, 10, width, eraBottom + 28 - 10);
                Place(treeEraRules[era], left + 12, 42, width - 24, 1);
                Place(treeEraBands[era].rectTransform, left + 14, 16, width - 28, 24);
                bottom = Mathf.Max(bottom, eraBottom + 28);
                columnBase += farthest + 1;
            }
            treeLinks.Clear();
            treeLines.paths.Clear();
            float lowerLane = bottom + 24;
            int crossLane = 0;
            foreach (var entry in orderedTreeCards)
            {
                var node = entry.node;
                if (!TreeNodeVisible(node)) continue;
                foreach (int parentKey in node.parents)
                {
                    if (!treeCards.TryGetValue(parentKey, out var parent) || !TreeNodeVisible(parent.node)) continue;
                    Vector2 from = parent.position, to = entry.position;
                    bool cross = parent.node.era != node.era;
                    float lane = cross && node.IsEra ? 62 + (crossLane % 5) * 9 : lowerLane + (crossLane % 8) * 9;
                    var line = new TechnologyTreeLines.Path
                    {
                        from = new Vector2(from.x + NodeWidth, -from.y - NodeHeight * .5f),
                        to = new Vector2(to.x - 1, -to.y - NodeHeight * .5f),
                        acrossEra = cross, laneY = -lane, color = TreeWire
                    };
                    if (cross) { crossLane++; bottom = Mathf.Max(bottom, lane + 24); }
                    treeLinks.Add((parentKey, node.key, line));
                    treeLines.paths.Add(line);
                }
            }
            float widthTotal = Mathf.Max(TreeWidth, columnBase * TreeColumn + 82);
            float heightTotal = Mathf.Max(TreeHeight, bottom + 32);
            Place(treeContent, 0, 0, widthTotal, heightTotal);
            Place(treeLines.rectTransform, 0, 0, widthTotal, heightTotal);
            treeContent.localScale = new Vector3(treeZoom, treeZoom, 1f);
            treeLines.SetVerticesDirty();
        }

        int VisibleTreeDepth(TechnologyTreeNode node, Dictionary<int, int> depths)
        {
            if (depths.TryGetValue(node.key, out int depth)) return depth;
            // Hidden prerequisites stay accessible in details; they do not reserve empty columns.
            foreach (int key in node.parents)
            {
                var parent = TechnologyTreeModel.Find(key);
                if (parent != null && parent.era == node.era && TreeNodeVisible(parent))
                    depth = Mathf.Max(depth, VisibleTreeDepth(parent, depths) + 1);
            }
            if (!node.IsEra) depth = Mathf.Max(1, depth);
            depths.Add(node.key, depth);
            return depth;
        }

        public static string TechnologyNodeName(TechnologyTreeNode node) => node.IsEra ? "Era Node " + node.era :
            node.domain == ResearchDomain.Weapon ? "Weapon Research " + node.researchId :
            node.domain == ResearchDomain.Tool ? "Tool Research " + node.researchId : TreeResearchNames[node.era] + node.researchId;

        static string ResearchDomainName(ResearchDomain domain) => domain == ResearchDomain.Weapon ? "武器" :
            domain == ResearchDomain.Tool ? "工具" : "工业";

        void BrowseTechnologyDomain(int filter)
        {
            if (treeDomainFilter == filter) return;
            treeDomainFilter = filter;
            LayoutTechnologyTree();
            var selected = TechnologyTreeModel.Find(selectedTreeKey);
            if (selected == null || !TreeNodeVisible(selected)) selectedTreeKey = selectedEra * 100;
            RefreshAll();
            FocusTreeNode(selectedTreeKey);
        }

        void OpenResearch(ResearchRef reference)
        {
            if (reference == null) return;
            TechnologyTreeNode node = null;
            foreach (var candidate in TechnologyTreeModel.All)
                if (!candidate.IsEra && candidate.domain == reference.domain && candidate.researchId == reference.id)
                {
                    node = candidate;
                    break;
                }
            if (node == null) return;
            SetPanel(technology, true);
            treeDomainFilter = (int)reference.domain;
            LayoutTechnologyTree();
            selectedEra = node.era;
            selectedTreeKey = node.key;
            RefreshAll();
            FocusTreeNode(node.key);
        }

        void SelectTreePrerequisite(int key)
        {
            var parent = TechnologyTreeModel.Find(key);
            if (parent == null) return;
            if (!parent.IsEra && !TreeNodeVisible(parent))
            {
                treeDomainFilter = (int)parent.domain;
                LayoutTechnologyTree();
            }
            if (selectedEra != parent.era) ResetScroll(technologyScroll);
            selectedEra = parent.era;
            selectedTreeKey = parent.key;
            ResetScroll(treeDetailsScroll);
            RefreshAll();
            FocusTreeNode(parent.key);
        }

        static string TechnologyIcon(TechnologyTreeNode node) => node.IsEra ? "machine_" + (int)TreeEraIcons[node.era] :
            EquipmentTechnologyIcon(node) ?? (node.era >= 0 && node.era < TreeProcessIcons.Length &&
                node.researchId >= 0 && node.researchId < TreeProcessIcons[node.era].Length
                    ? TreeProcessIcons[node.era][node.researchId] : "ui_research");

        static string EquipmentTechnologyIcon(TechnologyTreeNode node)
        {
            if (node.domain == ResearchDomain.Tool)
                return node.researchId == (int)ToolTech.SteelTools ? "gear_2" : "gear_4";
            if (node.domain != ResearchDomain.Weapon) return null;
            switch ((WeaponTech)node.researchId)
            {
                case WeaponTech.BasicAmmunition: return "item_" + (int)ItemKind.Ammo;
                case WeaponTech.Sidearms: return "gear_" + (int)GearKind.Pistol;
                case WeaponTech.Shotguns: return "gear_" + (int)GearKind.Shotgun;
                case WeaponTech.BasicFittings: return "part_" + (int)PartKind.MuzzleBrake;
                case WeaponTech.ReflexOptics: return "part_" + (int)PartKind.RedDot;
                case WeaponTech.AutomaticWeapons: return "gear_" + (int)GearKind.AssaultRifle;
                case WeaponTech.TacticalFittings: return "part_" + (int)PartKind.Suppressor;
                case WeaponTech.MagnifiedOptics: return "part_" + (int)PartKind.Scope4x;
                case WeaponTech.PrecisionAmmunition: return "item_" + (int)ItemKind.PrecisionAmmo;
                case WeaponTech.PrecisionWeapons: return "gear_" + (int)GearKind.PrecisionRifle;
                case WeaponTech.MatchBarrels: return "part_" + (int)PartKind.MatchBarrel;
                case WeaponTech.ArmorPiercing: return "item_" + (int)ItemKind.ArmorPiercingAmmo;
                default: return "ui_research";
            }
        }

        void SelectTreeNode(int key)
        {
            if (!treeCards.TryGetValue(key, out var selection)) return;
            if (selectedEra != selection.node.era) ResetScroll(technologyScroll);
            selectedEra = selection.node.era;
            selectedTreeKey = key;
            ResetScroll(treeDetailsScroll);
            RefreshAll();
        }

        void BrowseEra(int era)
        {
            era = Mathf.Clamp(era, 0, eraButtons.Length - 1);
            SelectTreeNode(era * 100);
            FocusTreeNode(selectedTreeKey);
        }

        void FocusCurrentTechnology()
        {
            var world = bridge.World;
            var player = bridge.LocalPlayer;
            if (world == null || player == null) return;
            int era = (int)world.era;
            int key = era * 100;
            TechnologyTreeNode candidate = null;
            foreach (var node in TechnologyTreeModel.All)
            {
                if (node.era != era || node.IsEra || !TreeNodeVisible(node) || TechnologyTreeModel.Known(world, node) ||
                    !TechnologyTreeModel.PrerequisitesMet(world, node)) continue;
                if (candidate == null) candidate = node;
                if (TechnologyTreeModel.CanResearch(world, player, node)) { candidate = node; break; }
            }
            if (candidate != null) key = candidate.key;
            else if (era < eraButtons.Length - 1 && TechnologyTreeModel.CanResearch(world, player, TechnologyTreeModel.Find((era + 1) * 100))) key = (era + 1) * 100;
            SelectTreeNode(key);
            FocusTreeNode(key);
        }

        void OpenTechnologyTree()
        {
            technologyGuide.gameObject.SetActive(false);
            treeViewport.gameObject.SetActive(true);
            Set(treeGuideToggle.label, "工艺与建造");
            ResetScroll(technologyScroll);
            FocusCurrentTechnology();
        }

        void ToggleTechnologyGuide()
        {
            bool show = !technologyGuide.gameObject.activeSelf;
            technologyGuide.gameObject.SetActive(show);
            treeViewport.gameObject.SetActive(!show);
            Set(treeGuideToggle.label, show ? "查看科技树" : "工艺与建造");
            if (show) ResetScroll(technologyScroll);
            RefreshAll();
        }

        void FocusTreeNode(int key)
        {
            if (!treeCards.TryGetValue(key, out var entry)) return;
            float width = treeViewport.viewport.rect.width;
            float height = treeViewport.viewport.rect.height;
            SetTreeOffset(new Vector2(width * .5f - (entry.position.x + NodeWidth * .5f) * treeZoom,
                (entry.position.y + NodeHeight * .5f) * treeZoom - height * .5f));
        }

        void ZoomTechnologyTreeAtCenter(float steps) => SetTreeZoom(steps,
            new Vector2(treeViewport.viewport.rect.width * .5f, -treeViewport.viewport.rect.height * .5f));

        void ZoomTechnologyTree(float steps, Vector2 screen, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(treeViewport.viewport, screen, eventCamera, out Vector2 position)) return;
            Vector2 topLeft = new Vector2(treeViewport.viewport.rect.xMin, treeViewport.viewport.rect.yMax);
            SetTreeZoom(Mathf.Sign(steps) * (steps == 0 ? 0 : 1), position - topLeft);
        }

        void SetTreeZoom(float steps, Vector2 point)
        {
            if (!treeViewport.gameObject.activeInHierarchy || steps == 0) return;
            float next = Mathf.Clamp(treeZoom + steps * .1f, .60f, 1.20f);
            if (Mathf.Approximately(next, treeZoom)) return;
            Vector2 offset = point - (point - treeContent.anchoredPosition) * (next / treeZoom);
            treeZoom = next;
            treeContent.localScale = new Vector3(next, next, 1f);
            SetTreeOffset(offset);
            Set(treeZoomLabel, Mathf.RoundToInt(next * 100f) + "%");
        }

        void SetTreeOffset(Vector2 offset)
        {
            treeViewport.StopMovement();
            treeContent.anchoredPosition = new Vector2(
                Mathf.Clamp(offset.x, -Mathf.Max(0, treeContent.rect.width * treeZoom - treeViewport.viewport.rect.width), 0),
                Mathf.Clamp(offset.y, 0, Mathf.Max(0, treeContent.rect.height * treeZoom - treeViewport.viewport.rect.height)));
        }

        void RefreshTechnology(WorldState world, PlayerState player)
        {
            var selected = TechnologyTreeModel.Find(selectedTreeKey);
            if (selected == null) return;
            System.Array.Clear(treeEraCompleted, 0, treeEraCompleted.Length);
            System.Array.Clear(treeEraCounts, 0, treeEraCounts.Length);
            int complete = 0, total = 0;
            foreach (var entry in orderedTreeCards)
            {
                var node = entry.node;
                bool known = TechnologyTreeModel.Known(world, node);
                bool ready = !known && TechnologyTreeModel.CanResearch(world, player, node);
                bool active = node.key == selectedTreeKey;
                if (!node.IsEra && (treeDomainFilter < 0 || (int)node.domain == treeDomainFilter))
                {
                    total++;
                    treeEraCounts[node.era]++;
                    if (known) { complete++; treeEraCompleted[node.era]++; }
                }
                Color state = known ? Accent : ready ? Amber : Muted;
                Color surface = known ? TreeComplete : ready ? TreeReady : TreeLocked;
                Tint(entry.button, active ? Color.Lerp(surface, Accent, .17f) : surface);
                entry.stripe.color = known || ready ? state : EraColors[node.era] * new Color(.68f, .68f, .68f, 1);
                entry.selection.enabled = active;
                entry.status.color = state;
                Set(entry.status, TechnologyStatus(world, node, known, ready));
            }
            bool recolor = false;
            foreach (var link in treeLinks)
            {
                bool met = TechnologyTreeModel.Known(world, treeCards[link.parent].node);
                bool selectedLink = link.child == selectedTreeKey || link.parent == selectedTreeKey;
                Color line = selectedLink ? (met ? Accent : Amber) : met ? Color.Lerp(Edge, Accent, .38f) : TreeWire;
                if (line == link.path.color) continue;
                link.path.color = line;
                recolor = true;
            }
            if (recolor) treeLines.SetVerticesDirty();
            Set(treeHeading, "当前时代  " + TechCatalog.EraName(world.era) + "     ·     工艺 " + complete + " / " + total);
            for (int i = 0; i < treeDomainButtons.Length; i++)
            {
                bool active = treeDomainFilter == i - 1;
                Tint(treeDomainButtons[i], active ? Selected : Raised);
                treeDomainButtons[i].label.color = active ? Accent : Ink;
            }
            for (int i = 0; i < eraButtons.Length; i++)
            {
                string count = treeEraCounts[i] > 0 ? "  " + treeEraCompleted[i] + "/" + treeEraCounts[i] : "";
                Set(eraButtons[i].label, TechCatalog.EraName((TechEra)i) + count);
                eraButtons[i].label.color = i == selectedEra ? Accent : i <= (int)world.era ? Ink : Muted;
                Tint(eraButtons[i], i == selectedEra ? Selected : Raised);
            }
            RefreshTreeDetails(world, player, selected);
            if (technologyGuide.gameObject.activeSelf) RefreshTechnologyGuide(world, player);
        }

        static string TechnologyStatus(WorldState world, TechnologyTreeNode node, bool known, bool ready) =>
            known ? node.IsEra ? ((int)world.era == node.era ? "当前时代" : "已解锁") : "已研究" :
            ready ? "可研究" : TechnologyTreeModel.PrerequisitesMet(world, node) ? "待满足条件" : "前置未完成";

        void RefreshTreeDetails(WorldState world, PlayerState player, TechnologyTreeNode node)
        {
            bool known = TechnologyTreeModel.Known(world, node);
            string requirement = TechnologyTreeModel.Requirement(world, player, node);
            bool ready = bridge.IsRunning && TechnologyTreeModel.CanResearch(world, player, node);
            bool sending = !known && node.key == treeResearchSentKey && Time.unscaledTime < treeResearchSentAt;
            Color state = known ? Accent : ready ? Amber : Muted;
            Set(treeTabTitle, TechCatalog.EraName((TechEra)node.era) +
                (node.IsEra ? "  /  时代入口" : "  /  " + ResearchDomainName(node.domain) + "研究"));
            treeTabTitle.color = EraColors[node.era];
            Set(treeTitle, node.IsEra ? node.name + "时代" : node.name);
            Set(treeState, TechnologyStatus(world, node, known, ready));
            treeState.color = state;
            SetFlowText(treeDescription, node.description, 48);
            SetFlowText(treeParents, node.parents.Length == 0 ? "直接前置研究\n无" : "直接前置研究 · 点击跳转", 30);
            int parentCount = node.parents.Length;
            while (treeParentButtons.Count < parentCount)
            {
                int index = treeParentButtons.Count;
                UiButton link = Button("Technology Prerequisite Link " + index, treeParentLinks, "", 0, 0, 282, 32, () => { });
                link.label.fontSizeMax = 13;
                var linkLayout = link.button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                linkLayout.preferredWidth = 282;
                linkLayout.preferredHeight = 32;
                treeParentButtons.Add(link);
            }
            for (int i = 0; i < treeParentButtons.Count; i++)
            {
                bool visible = i < parentCount;
                treeParentButtons[i].button.gameObject.SetActive(visible);
                if (!visible) continue;
                var prerequisite = TechnologyTreeModel.Find(node.parents[i]);
                if (prerequisite == null) continue;
                int key = prerequisite.key;
                treeParentButtons[i].button.onClick.RemoveAllListeners();
                treeParentButtons[i].button.onClick.AddListener(() => { DemoAudio.UiClick(); SelectTreePrerequisite(key); });
                Place((RectTransform)treeParentButtons[i].button.transform, 0, i * 36, 282, 32);
                treeParentButtons[i].button.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 32;
                treeParentButtons[i].label.textWrappingMode = TextWrappingModes.NoWrap;
                Set(treeParentButtons[i].label, (prerequisite.IsEra ? "时代 · " : ResearchDomainName(prerequisite.domain) + " · ") + prerequisite.name);
                Tint(treeParentButtons[i], TechnologyTreeModel.Known(world, prerequisite) ? TreeComplete : TreeReady);
            }
            treeParentLinks.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = parentCount * 36f;
            SetFlowText(treeCost, "研究材料 · 持有 / 所需\n" + MaterialText(node.cost, player.items, true), 37);
            SetFlowText(treeRequirement, "研究条件\n" + (known ? "全队已完成" : sending ? "正在提交研究…" : ready ? "条件已满足" :
                string.IsNullOrEmpty(requirement) ? "请等待角色恢复操作。" : requirement), 37);
            treeRequirement.color = known || ready ? Accent : Amber;
            string progress = TreeEvidence(world, node);
            treeEvidence.gameObject.SetActive(!string.IsNullOrEmpty(progress));
            if (!string.IsNullOrEmpty(progress)) SetFlowText(treeEvidence, "生产进度\n" + progress, 37);
            researchButton.button.interactable = ready && !sending;
            Set(researchButton.label, known ? "已完成" : sending ? "正在研究…" : ready ? (node.IsEra ? "进入" : "研究") + node.name : "研究条件未满足");
        }

        static string TreeEvidence(WorldState world, TechnologyTreeNode node)
        {
            if (node.IsEra) return node.era == (int)TechEra.Nuclear && world.era >= TechEra.HighVoltage
                ? SteamProgress("高压装配控制模块", world.hvPowered, 4) + " · " +
                  SteamProgress("控制模块", Count(world.produced, (int)ItemKind.ControlModule), 4) + " · " +
                  SteamProgress("耐热合金", Count(world.produced, (int)ItemKind.HeatAlloy), 6) : "";
            if (node.domain != ResearchDomain.Industry)
                return EquipmentResearchCatalog.Evidence(world, TechnologyTreeModel.Reference(node));
            if ((int)world.era < node.era) return "";
            switch ((TechEra)node.era)
            {
                case TechEra.Steam: return SteamResearchEvidence(world, (SteamTech)node.researchId);
                case TechEra.LowVoltage: return LowVoltageResearchEvidence(world, (LowVoltageTech)node.researchId);
                case TechEra.MediumVoltage: return PetrochemicalCatalog.ResearchEvidence(world, (PetrochemicalTech)node.researchId);
                case TechEra.HighVoltage: return PrecisionCatalog.ResearchEvidence(world, (PrecisionTech)node.researchId);
                case TechEra.Nuclear:
                {
                    string evidence = NuclearCatalog.ResearchEvidence(world, (NuclearTech)node.researchId);
                    if (node.researchId == (int)NuclearTech.UraniumBeneficiation ||
                        node.researchId == (int)NuclearTech.RadiationMonitoring ||
                        node.researchId == (int)NuclearTech.RadiationProtection)
                        return SteamProgress("实际开采铀矿", world.nuclearUraniumMined, 4) +
                            "\n首堆装料前即可研究，无需核燃料或核废料产量。";
                    return SteamProgress("实际开采铀矿", world.nuclearUraniumMined, 4) + " · " +
                        "铀精矿累计生产 " + Count(world.produced, (int)ItemKind.UraniumConcentrate) + " · " +
                        SteamProgress("核燃料", Count(world.produced, (int)ItemKind.NuclearFuel), 2) +
                        (evidence.Length > 0 ? "\n" + evidence : "");
                }
                default: return "";
            }
        }

        void Research()
        {
            if (!technology.gameObject.activeSelf || !bridge.IsRunning) return;
            var node = TechnologyTreeModel.Find(selectedTreeKey);
            if (node == null || (node.key == treeResearchSentKey && Time.unscaledTime < treeResearchSentAt) ||
                !TechnologyTreeModel.CanResearch(bridge.World, bridge.LocalPlayer, node)) return;
            treeResearchSentKey = node.key;
            treeResearchSentAt = Time.unscaledTime + .4f;
            Send(node.command, node.IsEra ? node.era : node.researchId);
            RefreshAll();
        }
    }
}
