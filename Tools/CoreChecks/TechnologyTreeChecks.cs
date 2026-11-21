using DeadCircuit;

static partial class Checks
{
    static void TechnologyTreeChecks()
    {
        Run("technology tree covers all catalog nodes and forms an acyclic graph", () =>
        {
            var nodes = TechnologyTreeModel.All;
            Require(nodes.Select(node => node.key).Distinct().Count() == nodes.Length, "duplicate node key");
            var craftCount = 0;
            CheckCrafts(0, "primitive", PrimitiveCatalog.All.Select(d => ((int)d.id, d.name, d.description, d.prerequisites, d.cost)));
            CheckCrafts(1, "water", WaterCatalog.All.Select(d => ((int)d.id, d.name, d.description, d.prerequisites, d.cost)));
            CheckCrafts(2, "steam", SteamCatalog.All.Select(d => ((int)d.id, d.name, d.description, d.prerequisites, d.cost)));
            CheckCrafts(3, "lv", LowVoltageCatalog.All.Select(d => ((int)d.id, d.name, d.description, d.prerequisites, d.cost)));
            CheckCrafts(4, "mv", PetrochemicalCatalog.All.Select(d => ((int)d.id, d.name, d.description, d.prerequisites, d.cost)));
            CheckCrafts(5, "hv", PrecisionCatalog.All.Select(d => ((int)d.id, d.name, d.description, d.prerequisites, d.cost)));
            CheckCrafts(6, "nuclear", NuclearCatalog.All.Select(d => ((int)d.id, d.name, d.description, d.prerequisites, d.cost)));
            Require(nodes.Length == craftCount + 7, "extra or missing technology nodes");

            for (int era = 0; era <= 6; era++)
            {
                var gate = TechnologyTreeModel.Find(era * 100);
                Require(gate != null && gate.IsEra && gate.era == era && gate.researchId == -1 && gate.command == "research",
                    $"missing or incorrect era node {era}");
                Require(gate.name == TechCatalog.EraName((TechEra)era) && gate.description.Length > 0, $"missing era text {era}");
                Require(gate.cost.SequenceEqual(TechCatalog.ResearchCost((TechEra)era)), $"wrong cost for era {era}");
                Require(era == 0 ? gate.parents.Length == 0 : gate.parents.Contains((era - 1) * 100), $"broken era chain at {era}");
            }
            var checkedKeys = new HashSet<int>();
            var active = new HashSet<int>();
            foreach (var node in nodes) Visit(node);
            Require(TechnologyTreeModel.Find(-1) == null && TechnologyTreeModel.Find(610) == null, "unknown key resolved");

            void CheckCrafts(int era, string command, IEnumerable<(int id, string name, string description, int mask, int[] cost)> catalog)
            {
                foreach (var d in catalog)
                {
                    craftCount++;
                    var node = TechnologyTreeModel.Find(era * 100 + d.id + 1);
                    Require(node != null && !node.IsEra && node.era == era && node.researchId == d.id && node.command == command,
                        $"missing or incorrect {command} node {d.id}");
                    Require(node.name == d.name && node.description == d.description && node.cost.SequenceEqual(d.cost),
                        $"catalog data differs for {node.name}");
                    Require(!ReferenceEquals(node.cost, d.cost), $"{node.name} shares a mutable cost with the simulation");
                    var sameEra = Enumerable.Range(0, 32).Where(id => (d.mask & (1 << id)) != 0).Select(id => era * 100 + id + 1);
                    if (d.mask == 0) sameEra = new[] { era * 100 };
                    Require(node.parents.Where(key => key / 100 == era).OrderBy(key => key).SequenceEqual(sameEra),
                        $"catalog prerequisites differ for {node.name}");
                }
            }

            void Visit(TechnologyTreeNode node)
            {
                if (checkedKeys.Contains(node.key)) return;
                Require(active.Add(node.key), $"cycle at {node.name}");
                Require(node.parents.Distinct().Count() == node.parents.Length, $"duplicate edge at {node.name}");
                foreach (int parentKey in node.parents)
                {
                    var parent = TechnologyTreeModel.Find(parentKey);
                    Require(parent != null && parent.era <= node.era, $"invalid parent {parentKey} at {node.name}");
                    Visit(parent);
                }
                active.Remove(node.key);
                checkedKeys.Add(node.key);
            }
        });

        Run("technology tree keeps cross-era requirements and optional crafts separate", () =>
        {
            int Craft(int era, Enum research) => era * 100 + Convert.ToInt32(research) + 1;
            int cooking = Craft(0, PrimitiveTech.Cooking), agriculture = Craft(1, WaterTech.Agriculture);
            RequireParents(agriculture, cooking);
            RequireParents(Craft(2, SteamTech.Coking), Craft(0, PrimitiveTech.Charcoal));
            RequireParents(Craft(2, SteamTech.Refractories), Craft(0, PrimitiveTech.Ceramics));
            RequireParents(Craft(2, SteamTech.PigIron), Craft(1, WaterTech.Blast), Craft(1, WaterTech.Crushing));
            RequireParents(Craft(2, SteamTech.Machining), Craft(1, WaterTech.Pressing), Craft(1, WaterTech.Components));
            for (int era = 1; era <= 6; era++)
            {
                var gate = TechnologyTreeModel.Find(era * 100);
                var required = TechnologyTreeModel.All.Where(node => !node.IsEra && node.era == era - 1 &&
                    node.key != cooking && node.key != agriculture);
                var ancestors = Ancestors(gate);
                Require(required.All(node => ancestors.Contains(node.key)), $"era {era} omits a required craft");
                Require(!ancestors.Contains(cooking) && !ancestors.Contains(agriculture), $"era {era} requires an optional craft");
                var craftParents = gate.parents.Select(TechnologyTreeModel.Find).Where(parent => !parent.IsEra).ToArray();
                Require(craftParents.All(parent => !craftParents.Any(other => other != parent && Ancestors(other).Contains(parent.key))),
                    $"era {era} draws a duplicate line to a craft ancestor");
            }

            void RequireParents(int key, params int[] expected)
            {
                var node = TechnologyTreeModel.Find(key);
                Require(expected.All(node.parents.Contains), $"missing cross-era edge at {node.name}");
            }

            HashSet<int> Ancestors(TechnologyTreeNode node)
            {
                var result = new HashSet<int>();
                var pending = new Stack<int>(node.parents);
                while (pending.TryPop(out int key))
                    if (result.Add(key)) foreach (int parent in TechnologyTreeModel.Find(key).parents) pending.Push(parent);
                return result;
            }
        });

        Run("technology tree reports player, workbench, materials and future era gates without changing the world", () =>
        {
            var sim = PrimitiveFixture(out var player, arrange: state => state.waterVersion = 1);
            var world = sim.State;
            var origin = TechnologyTreeModel.Find(0);
            var tools = TechnologyTreeModel.Find((int)PrimitiveTech.Tools + 1);
            var charcoal = TechnologyTreeModel.Find((int)PrimitiveTech.Charcoal + 1);
            Require(TechnologyTreeModel.Known(world, origin) && origin.cost.All(cost => cost == 0) &&
                TechnologyTreeModel.Requirement(world, null, origin) == "" && !TechnologyTreeModel.CanResearch(world, player, origin),
                "Primitive entry is not free and already known");
            Require(!TechnologyTreeModel.Known(world, new TechnologyTreeNode { key = 0, era = 0, researchId = -2 }) &&
                !TechnologyTreeModel.CanResearch(world, player, new TechnologyTreeNode { key = 1, era = 0, researchId = 50 }),
                "invalid research id accepted");
            foreach (var node in TechnologyTreeModel.All.Where(node => node.era > 0 && node.key != 100))
            {
                var era = (TechEra)(node.IsEra ? node.era - 1 : node.era);
                Require(!TechnologyTreeModel.Known(world, node) && !TechnologyTreeModel.CanResearch(world, player, node) &&
                    TechnologyTreeModel.Requirement(world, player, node).Contains(TechCatalog.EraName(era)),
                    $"{node.name} did not indicate its era lock");
            }
            Require(!TechnologyTreeModel.PrerequisitesMet(world, charcoal) &&
                TechnologyTreeModel.Requirement(world, player, charcoal) == PrimitiveCatalog.ResearchRequirement(world, player, charcoal.researchId),
                "missing prerequisite was not reported by the catalog");

            player.connected = false;
            Require(!TechnologyTreeModel.CanResearch(world, player, tools), "disconnected player could research");
            player.connected = true;
            player.hp = 0;
            Require(!TechnologyTreeModel.CanResearch(world, player, tools), "dead player could research");
            player.hp = 100;
            player.interactCooldown = .25f;
            Require(!TechnologyTreeModel.CanResearch(world, player, tools), "player could research during cooldown");
            Ready(player);
            Require(!TechnologyTreeModel.CanResearch(world, new PlayerState(), tools), "player outside the world could research");
            player.x = 1;
            Require(TechnologyTreeModel.PrerequisitesMet(world, tools) && !TechnologyTreeModel.CanResearch(world, player, tools) &&
                TechnologyTreeModel.Requirement(world, player, tools).Contains("工作台"), "workbench requirement was skipped");
            player.x = 8;
            player.items[(int)ItemKind.Wood] = 0;
            Require(!TechnologyTreeModel.CanResearch(world, player, tools) &&
                TechnologyTreeModel.Requirement(world, player, tools).Contains(DemoCatalog.ItemNames[(int)ItemKind.Wood]),
                "missing craft material was not reported");
            player.items[(int)ItemKind.Wood] = 100;

            string before = Dump(world);
            foreach (var node in TechnologyTreeModel.All)
            {
                TechnologyTreeModel.Known(world, node);
                TechnologyTreeModel.Requirement(world, player, node);
                TechnologyTreeModel.CanResearch(world, player, node);
                TechnologyTreeModel.PrerequisitesMet(world, node);
            }
            Require(Dump(world) == before, "querying the technology tree changed world or player state");
            Require(TechnologyTreeModel.CanResearch(world, player, tools), "eligible tool research was rejected");
            var itemsBefore = Copy(player.items);
            sim.Apply(Command(player, tools.command, arg: tools.researchId));
            Require(TechnologyTreeModel.Known(world, tools) && TechnologyTreeModel.Requirement(world, null, tools) == "" &&
                !TechnologyTreeModel.CanResearch(world, player, tools) && TechnologyTreeModel.PrerequisitesMet(world, charcoal),
                "knowledge did not update after the simulator accepted the command");
            Paid(player.items, itemsBefore, tools.cost, "tool research command");
        });

        Run("technology tree delegates Water promotion to the catalog and leaves Cooking optional", () =>
        {
            var sim = PrimitiveFixture(out var player, (1 << (int)PrimitiveTech.Cooking) - 1, state =>
            {
                state.waterVersion = 1;
                state.produced[(int)ItemKind.Bronze] = 4;
                state.produced[(int)ItemKind.Iron] = 5;
                state.produced[(int)ItemKind.Brick] = 4;
                state.produced[(int)ItemKind.Charcoal] = 2;
            });
            var world = sim.State;
            var gate = TechnologyTreeModel.Find(100);
            var cooking = TechnologyTreeModel.Find((int)PrimitiveTech.Cooking + 1);
            Require(TechnologyTreeModel.PrerequisitesMet(world, gate) && !TechnologyTreeModel.Known(world, cooking) &&
                !TechnologyTreeModel.CanResearch(world, player, gate) &&
                TechnologyTreeModel.Requirement(world, player, gate) == TechCatalog.ResearchRequirement(world, TechEra.Water),
                "graph completion bypassed the production milestone");
            world.produced[(int)ItemKind.Iron] = 6;
            var bench = world.buildings.Single(building => building.kind == BuildKind.Workbench);
            float benchHealth = bench.hp;
            bench.hp = 0;
            Require(!TechnologyTreeModel.CanResearch(world, player, gate) &&
                TechnologyTreeModel.Requirement(world, player, gate) == TechCatalog.ResearchRequirement(world, TechEra.Water),
                "promotion bypassed the required working building");
            bench.hp = benchHealth;
            player.items[(int)ItemKind.Iron] = 5;
            Require(!TechnologyTreeModel.CanResearch(world, player, gate) &&
                TechnologyTreeModel.Requirement(world, player, gate).Contains(DemoCatalog.ItemNames[(int)ItemKind.Iron]),
                "promotion bypassed the material cost");
            player.items[(int)ItemKind.Iron] = 100;
            player.interactCooldown = .25f;
            Require(TechCatalog.CanResearch(world, player, TechEra.Water) && !TechnologyTreeModel.CanResearch(world, player, gate),
                "promotion bypassed the simulator cooldown");
            Ready(player);
            Require(TechnologyTreeModel.CanResearch(world, player, gate), "optional Cooking blocked Water promotion");

            var before = Copy(player.items);
            sim.Apply(Command(player, gate.command, arg: gate.era));
            Require(world.era == TechEra.Water && TechnologyTreeModel.Known(world, gate) &&
                TechnologyTreeModel.Requirement(world, player, gate) == "" && !TechnologyTreeModel.CanResearch(world, player, gate),
                "era entry did not reflect the simulator result");
            Paid(player.items, before, gate.cost, "Water promotion command");

            var agriculture = TechnologyTreeModel.Find(100 + (int)WaterTech.Agriculture + 1);
            world.waterResearch = (1 << (int)WaterTech.Wheel) | (1 << (int)WaterTech.Shafts);
            Ready(player);
            Require(!TechnologyTreeModel.PrerequisitesMet(world, agriculture) && !TechnologyTreeModel.CanResearch(world, player, agriculture) &&
                TechnologyTreeModel.Requirement(world, player, agriculture) == WaterCatalog.ResearchRequirement(world, player, agriculture.researchId),
                "optional Agriculture bypassed its Cooking prerequisite");
        });
    }
}
