using System;
using System.Linq;
using System.Text.Json;
using DeadCircuit;

static partial class Checks
{
    // Main invokes ForagingChecks() from Program.Main. Every reward is obtained through Apply.
    static WorldSimulator ForagingFixture(out PlayerState player, NodeKind kind = NodeKind.Tree,
        int stock = 1, int biome = 1, int seed = 73149, int generator = 1, bool enabled = true, float hp = 12f)
    {
        var sim = SurvivalFixture(out player, s =>
        {
            s.seed = seed; s.generation.seed = seed; s.generatorVersion = generator;
            s.biomes = new string((char)('0' + biome), 4096); s.survival.enabled = enabled;
            s.nodes.Add(new NodeState { id = s.nextId++, x = 10, y = 10, kind = kind, stock = stock, hp = hp });
        });
        sim.Apply(Command(player, "equip", arg: player.gear.Single(g => g.kind == (kind == NodeKind.Ore ? GearKind.StonePickaxe : GearKind.StoneAxe)).id));
        return sim;
    }

    static void ForagingChecks()
    {
        Run("foraging gathering notice reports actual node biome food and capped receipt", () =>
        {
            var sim = ForagingFixture(out var p, biome: (int)BiomeKind.Wetland);
            var map = sim.State.biomes.ToCharArray(); map[8 + 10 * 64] = '0'; sim.State.biomes = new string(map);
            p.needs.foods[(int)FoodKind.Berries] = SurvivalCatalog.StackLimit;
            p.needs.foods[(int)FoodKind.Mushroom] = SurvivalCatalog.StackLimit - 1;
            sim.Apply(Command(p, "interact", 10, 10));
            Require(p.notice.Contains("野蘑菇 +1"), "actual capped biome forage absent from notice: " + p.notice);
            Require(!p.notice.Contains("浆果") && !p.notice.Contains("苹果"), "notice promised overflow or player-cell biome loot");
            Require(p.needs.foods[(int)FoodKind.Mushroom] == SurvivalCatalog.StackLimit && p.needs.foods[(int)FoodKind.Apple] == 0, "node biome or cap was ignored");
        });
        Run("foraging depleted broadleaf tree gives actual apples and berries", () =>
        {
            var sim = ForagingFixture(out var p); int wood = p.items[(int)ItemKind.Wood];
            sim.Apply(Command(p, "interact", 10, 10));
            Require(p.needs.foods[(int)FoodKind.Apple] > 0 && p.needs.foods[(int)FoodKind.Berries] > 0, "depleted tree awarded no usable forage");
            Require(sim.State.nodes.Count == 0 && p.items[(int)ItemKind.Wood] == wood + 1, "forage changed industrial wood/depletion");
        });
        Run("foraging wilderness and provision crates cover every noncrop nonbutchery ingredient", () =>
        {
            var available = new bool[SurvivalCatalog.FoodCount];
            foreach (int biome in new[] { 0, 1, 2, 3, 4, 5 })
            {
                var forest = ForagingFixture(out var p, biome: biome);
                forest.Apply(Command(p, "interact", 10, 10));
                if (biome == 2 || biome == 4 || biome == 5)
                    Require(p.needs.foods[(int)FoodKind.Mushroom] > 0, "fungal biome has no mushrooms");
                for (int i = 0; i < available.Length; i++) available[i] |= p.needs.foods[i] > 0;
            }
            var supply = ForagingFixture(out var looter, NodeKind.Crate);
            int ration = looter.items[(int)ItemKind.Food]; supply.Apply(Command(looter, "interact", 10, 10));
            Require(looter.needs.foods[(int)FoodKind.Egg] >= 2 && looter.needs.foods[(int)FoodKind.RawFish] >= 1, "crate cannot supply egg/fish recipes");
            Require(looter.needs.seeds.Sum() >= 1 && looter.needs.seeds.Sum() <= 2, "crate seed supplement missing/unbounded");
            Require(looter.items[(int)ItemKind.Food] == ration + 1, "crate industrial ration reward changed");
            for (int i = 0; i < available.Length; i++) available[i] |= looter.needs.foods[i] > 0;
            foreach (var ingredient in new[] { FoodKind.Apple, FoodKind.Berries, FoodKind.Mushroom, FoodKind.Egg, FoodKind.RawFish })
                Require(available[(int)ingredient], "normal gathering cannot obtain " + ingredient);
        });
        Run("foraging partial harvesting gives nothing and exhausted sources never pay twice", () =>
        {
            var sim = ForagingFixture(out var p, stock: 7, hp: 35); int wood = p.items[(int)ItemKind.Wood];
            sim.Apply(Command(p, "switch", arg: 2));
            sim.Apply(Command(p, "interact", 10, 10));
            Require(p.items[(int)ItemKind.Wood] == wood && sim.State.nodes.Single().hp == 35f, "bare hands damaged or looted the tree");
            Advance(sim, .5f);
            sim.Apply(Command(p, "equip", arg: p.gear.Single(g => g.kind == GearKind.StoneAxe).id));
            sim.Apply(Command(p, "interact", 10, 10));
            Require(sim.State.nodes.Single().stock == 7 && p.items[(int)ItemKind.Wood] == wood && p.needs.foods.All(n => n == 0) && p.needs.seeds.All(n => n == 0), "partial tree paid early");
            Advance(sim, .5f); sim.Apply(Command(p, "interact", 10, 10));
            Require(sim.State.nodes.Single().stock == 7 && p.items[(int)ItemKind.Wood] == wood && p.needs.foods.All(n => n == 0), "second partial harvest paid early");
            Advance(sim, .5f); sim.Apply(Command(p, "interact", 10, 10));
            Require(sim.State.nodes.Count == 0 && p.items[(int)ItemKind.Wood] == wood + 7, "industrial stock was not conserved");
            var food = (int[])p.needs.foods.Clone(); var seeds = (int[])p.needs.seeds.Clone();
            Require(food.Sum() > 0, "final depletion gave no forage");
            for (int i = 0; i < 4; i++) { Advance(sim, .5f); sim.Apply(Command(p, "interact", 10, 10)); }
            Require(p.needs.foods.SequenceEqual(food) && p.needs.seeds.SequenceEqual(seeds), "empty cell duplicated forage");
        });
        Run("foraging legacy disabled and uninitialized players receive only original industrial loot", () =>
        {
            foreach (string mode in new[] { "legacy", "generator-zero", "disabled", "uninitialized" })
            {
                var sim = ForagingFixture(out var p, NodeKind.Crate, generator: mode == "generator-zero" ? 0 : 1, enabled: mode != "disabled");
                if (mode == "legacy")
                {
                    var old = Saved(sim.State); PrepareLegacySaveFixture(old, 5); sim = new WorldSimulator(old); p = sim.State.players.Single();
                }
                if (mode == "uninitialized") p.needs.initialized = false;
                int ration = p.items[(int)ItemKind.Food]; sim.Apply(Command(p, "interact", 10, 10));
                Require(sim.State.nodes.Count == 0 && p.items[(int)ItemKind.Food] == ration + 1, "opt-out changed industrial gathering: " + mode);
                Require(p.needs.foods.All(n => n == 0) && p.needs.seeds.All(n => n == 0), "inactive world/player gained forage: " + mode);
            }
        });
        Run("foraging inventory caps saturate and discarded overflow cannot be recovered", () =>
        {
            foreach (int fill in new[] { 998, 999 })
            {
                var sim = ForagingFixture(out var p, NodeKind.Crate); Array.Fill(p.needs.foods, fill); Array.Fill(p.needs.seeds, fill);
                sim.Apply(Command(p, "interact", 10, 10));
                Require(p.needs.foods.All(n => n >= 0 && n <= 999) && p.needs.seeds.All(n => n >= 0 && n <= 999), "forage overflowed stack limits");
                foreach (var food in new[] { FoodKind.Egg, FoodKind.RawFish, FoodKind.Apple, FoodKind.Berries })
                    Require(p.needs.foods[(int)food] == 999, "forage did not fill remaining capacity");
                Array.Fill(p.needs.foods, 0); Array.Fill(p.needs.seeds, 0);
                Advance(sim, .5f); sim.Apply(Command(p, "interact", 10, 10));
                Require(p.needs.foods.All(n => n == 0) && p.needs.seeds.All(n => n == 0), "overflow was banked on a removed source");
            }
        });
        Run("foraging save restores partial source identically and exhausted source stays exhausted", () =>
        {
            var live = ForagingFixture(out var p, NodeKind.Crate, stock: 4, hp: 24);
            live.Apply(Command(p, "interact", 10, 10)); Require(live.State.nodes.Single().stock == 4 && live.State.nodes.Single().hp == 12, "partial crate fixture");
            var source = Saved(live.State); string before = Dump(source); var restored = new WorldSimulator(source); var restoredPlayer = restored.State.players.Single();
            Advance(live, .5f); Advance(restored, .5f);
            live.Apply(Command(p, "interact", 10, 10)); restored.Apply(Command(restoredPlayer, "interact", 10, 10));
            Require(p.needs.foods.SequenceEqual(restoredPlayer.needs.foods) && p.needs.seeds.SequenceEqual(restoredPlayer.needs.seeds), "save/load rerolled loot");
            Require(p.needs.foods[(int)FoodKind.Egg] >= 2 && live.State.nodes.Count == 0, "saved source did not award at final depletion");
            Require(Dump(source) == before, "foraging mutated caller save");
            var exhausted = new WorldSimulator(Saved(restored.State)); var actor = exhausted.State.players.Single();
            var food = (int[])actor.needs.foods.Clone(); var seeds = (int[])actor.needs.seeds.Clone();
            Advance(exhausted, .5f); exhausted.Apply(Command(actor, "interact", 10, 10));
            Require(exhausted.State.nodes.Count == 0 && actor.needs.foods.SequenceEqual(food) && actor.needs.seeds.SequenceEqual(seeds), "exhausted saved source repaid forage");
        });
        Run("foraging seed node and biome loot is deterministic and does not consume simulation PRNGs", () =>
        {
            var results = new System.Collections.Generic.HashSet<string>();
            foreach (int seed in new[] { int.MinValue, -1, 0, 73149, int.MaxValue })
            {
                var first = ForagingFixture(out var a, NodeKind.Crate, seed: seed);
                var second = ForagingFixture(out var b, NodeKind.Crate, seed: seed);
                second.State.survival.weather.randomState = 123u; second.State.survival.creatureRandomState = 456u;
                uint weather = first.State.survival.weather.randomState, wildlife = first.State.survival.creatureRandomState;
                first.Apply(Command(a, "interact", 10, 10)); second.Apply(Command(b, "interact", 10, 10));
                Require(a.needs.foods.SequenceEqual(b.needs.foods) && a.needs.seeds.SequenceEqual(b.needs.seeds), "same source depended on mutable PRNG");
                Require(first.State.survival.weather.randomState == weather && first.State.survival.creatureRandomState == wildlife &&
                    second.State.survival.weather.randomState == 123u && second.State.survival.creatureRandomState == 456u, "forage advanced weather/wildlife PRNG");
                results.Add(JsonSerializer.Serialize(a.needs.foods, Json) + JsonSerializer.Serialize(a.needs.seeds, Json));
            }
            Require(results.Count > 1, "world seed never affects forage yield");
        });
        Run("foraging invalid interactions cannot bypass industrial capacity range or authority", () =>
        {
            foreach (string mode in new[] { "capacity", "far", "wall", "offline", "dead", "unknown" })
            {
                var sim = ForagingFixture(out var p);
                if (mode == "capacity") p.items[(int)ItemKind.Wood] = 999;
                if (mode == "far") p.x = 3;
                if (mode == "wall") Add(sim.State, BuildKind.Wall, 9, 10);
                if (mode == "offline") sim.SetConnected(p.id, false);
                if (mode == "dead") p.hp = 0;
                var command = Command(p, "interact", 10, 10); if (mode == "unknown") command.playerId = "unknown";
                sim.Apply(command);
                Require(sim.State.nodes.Single().stock == 1 && p.needs.foods.All(n => n == 0) && p.needs.seeds.All(n => n == 0), "invalid interaction obtained forage: " + mode);
            }
            var ore = ForagingFixture(out var miner, NodeKind.Ore); ore.Apply(Command(miner, "interact", 10, 10));
            Require(ore.State.nodes.Count == 0 && miner.needs.foods.All(n => n == 0) && miner.needs.seeds.All(n => n == 0), "industrial ore acquired tree/crate forage");
        });
    }
}
