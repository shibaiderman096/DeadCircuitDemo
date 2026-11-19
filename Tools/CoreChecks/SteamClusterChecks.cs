using DeadCircuit;
using System.Text.Json;

static partial class Checks
{
    static WorldState SteamClusterWorld()
    {
        var state = Flat(era: TechEra.Steam);
        WorldGeneration.LegacyMetadata(state);
        state.version = WorldSimulator.SaveVersion;
        return state;
    }

    static (BuildingState boiler, BuildingState chamber, BuildingState pump, BuildingState chimney)
        AddSteamCluster(WorldState state, int x = 10, int y = 10, int fuel = 2)
    {
        var boiler = Add(state, BuildKind.Boiler, x, y);
        boiler.modularBoiler = true;
        var chamber = Add(state, BuildKind.CombustionChamber, x - 2, y);
        chamber.items[(int)ItemKind.Fuel] = fuel;
        var pump = Add(state, BuildKind.FeedwaterPump, x, y + 2);
        var chimney = Add(state, BuildKind.Chimney, x + 2, y);
        Water(state, x - 1, y + 3);
        return (boiler, chamber, pump, chimney);
    }

    static (BuildingState boiler, BuildingState chamber, BuildingState pump, BuildingState chimney)
        ExpandSteamCluster(WorldState state, bool wet = true)
    {
        var boiler = Add(state, BuildKind.Boiler, 12, 11);
        boiler.modularBoiler = true;
        var chamber = Add(state, BuildKind.CombustionChamber, 8, 8);
        chamber.items[(int)ItemKind.Fuel] = 2;
        var pump = Add(state, BuildKind.FeedwaterPump, 11, 12);
        var chimney = Add(state, BuildKind.Chimney, 12, 13);
        if (wet) Water(state, 11, 14);
        return (boiler, chamber, pump, chimney);
    }

    static string SteamClusterSignature(WorldSimulator sim) => JsonSerializer.Serialize(new
    {
        clusters = sim.State.steamClusters,
        buildings = sim.State.buildings.OrderBy(b => b.id).Select(b => new
            { b.id, b.clusterId, b.powerGeneration, b.fuel, b.items, b.modularBoiler })
    }, Json);

    static void SteamClusterChecks()
    {
        Run("steam cluster complete modules generate once and burn one chamber once per tick", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state);
            var sim = new WorldSimulator(state);
            var cluster = sim.State.steamClusters.Single();
            Require(cluster.id == parts.boiler.id && cluster.buildingIds.SequenceEqual(new[]
                { parts.boiler.id, parts.chamber.id, parts.pump.id, parts.chimney.id }), "cluster identity/members");
            Require(cluster.boilers == 1 && cluster.chambers == 1 && cluster.pumps == 1 && cluster.chimneys == 1, "module counts");
            Near(cluster.heatCapacity, 120, "installed heat capacity");
            Near(cluster.waterCapacity, 120, "shoreline pump capacity");
            Near(cluster.exhaustCapacity, 120, "chimney capacity");
            Near(cluster.output, 120, "complete cluster preview");
            Require(cluster.status.Contains("运行"), "running status missing");
            var chamber = Building(sim, parts.chamber.id);
            Require(chamber.items[(int)ItemKind.Fuel] == 2, "preview spent an item");
            Near(chamber.fuel, 0, "preview lit persistent fuel");
            sim.Step(WorldSimulator.FixedStep);
            Require(chamber.items[(int)ItemKind.Fuel] == 1, "tick did not ignite exactly one item");
            Near(chamber.fuel, 19.95f, "tick spent the chamber reserve more than once");
            Near(Building(sim, parts.boiler.id).powerGeneration, 120, "modular boiler steam");
            Near(sim.State.powerNetworks.Single(n => n.tier == 1).generation, 120, "steam network output");
            Require(sim.State.buildings.All(b => b.clusterId == cluster.id), "module ownership");
        });

        Run("steam cluster multiple boilers share one chamber without multiplying steam or fuel", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state);
            var extra = Add(state, BuildKind.Boiler, 12, 11);
            extra.modularBoiler = true;
            var sim = new WorldSimulator(state);
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "shared chamber limit");
            Near(Building(sim, parts.boiler.id).powerGeneration, 60, "first boiler share");
            Near(Building(sim, extra.id).powerGeneration, 60, "second boiler share");
            var chamber = Building(sim, parts.chamber.id);
            Require(chamber.items[(int)ItemKind.Fuel] == 1, "multiple boilers consumed multiple fuel items");
            Near(chamber.fuel, 19.95f, "multiple boilers charged the same chamber twice");
        });

        Run("steam cluster expansion is capped independently by boilers heat water and exhaust", () =>
        {
            foreach (string bottleneck in new[] { "none", "boiler", "heat", "water", "exhaust" })
            {
                var state = SteamClusterWorld();
                var first = AddSteamCluster(state);
                var extra = ExpandSteamCluster(state, wet: bottleneck != "water");
                if (bottleneck == "boiler") state.buildings.Remove(extra.boiler);
                if (bottleneck == "heat") state.buildings.Remove(extra.chamber);
                if (bottleneck == "exhaust") state.buildings.Remove(extra.chimney);
                var sim = new WorldSimulator(state);
                sim.Step(WorldSimulator.FixedStep);
                var cluster = sim.State.steamClusters.Single();
                float expected = bottleneck == "none" ? 240 : 120;
                Near(cluster.output, expected, bottleneck + " capacity");
                Near(sim.State.buildings.Where(b => b.modularBoiler).Sum(b => b.powerGeneration), expected, "boiler sum");
                Near(Building(sim, first.boiler.id).powerGeneration, bottleneck == "boiler" ? 120 : expected / 2, "equal boiler output");
                Near(cluster.heatCapacity, bottleneck == "heat" ? 120 : 240, "installed chamber capacity");
                Near(cluster.waterCapacity, bottleneck == "water" ? 120 : 240, "only wet pumps count capacity");
                Require(cluster.pumps == 2, "dry pump disappeared from physical counts");
                Near(cluster.exhaustCapacity, bottleneck == "exhaust" ? 120 : 240, "installed exhaust capacity");
                Require(Building(sim, first.chamber.id).items[(int)ItemKind.Fuel] == 1, "first chamber did not ignite");
                if (bottleneck != "heat")
                {
                    var spare = Building(sim, extra.chamber.id);
                    Require(spare.items[(int)ItemKind.Fuel] == (bottleneck == "none" ? 1 : 2), "unused chamber burned fuel");
                    Near(spare.fuel, bottleneck == "none" ? 19.95f : 0, "unused chamber reserve changed");
                }
            }
        });

        Run("steam cluster missing any module or shoreline preserves both fuel items and reserve", () =>
        {
            foreach (string missing in new[] { "锅炉", "燃烧室", "给水泵", "烟囱", "离水" })
            {
                var state = SteamClusterWorld();
                var parts = AddSteamCluster(state);
                parts.chamber.fuel = 1.25f;
                if (missing == "锅炉") state.buildings.Remove(parts.boiler);
                if (missing == "燃烧室") state.buildings.Remove(parts.chamber);
                if (missing == "给水泵") state.buildings.Remove(parts.pump);
                if (missing == "烟囱") state.buildings.Remove(parts.chimney);
                if (missing == "离水") state.ground = new string('4', 4096);
                var sim = new WorldSimulator(state);
                Advance(sim, 1);
                Require(sim.State.steamClusters.All(c => c.output == 0), missing + " generated steam");
                Require(sim.State.steamClusters.Any(c => c.status.Contains(missing)), missing + " has no clear status");
                foreach (var chamber in sim.State.buildings.Where(b => b.kind == BuildKind.CombustionChamber))
                {
                    Require(chamber.items[(int)ItemKind.Fuel] == 2, missing + " consumed fuel item");
                    Near(chamber.fuel, 1.25f, missing + " consumed burning reserve");
                }
            }
        });

        Run("steam cluster dry pump stops immediately without losing its burning reserve", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state);
            var sim = new WorldSimulator(state);
            sim.Step(WorldSimulator.FixedStep);
            var chamber = Building(sim, parts.chamber.id);
            float reserve = chamber.fuel;
            sim.State.ground = new string('4', 4096);
            Advance(sim, .5f);
            Near(sim.State.steamClusters.Single().output, 0, "lost shoreline did not stop steam");
            Require(sim.State.steamClusters.Single().status.Contains("离水"), "dry shoreline status");
            Near(chamber.fuel, reserve, "dry cluster kept burning");
            Require(chamber.items[(int)ItemKind.Fuel] == 1, "dry cluster spent another item");
            Water(sim.State, 9, 13);
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "shoreline restoration did not resume");
            Near(chamber.fuel, reserve - WorldSimulator.FixedStep, "resumed cluster reserve");
        });

        Run("steam cluster air gaps diagonals walls doors and pipes cannot bridge modules", () =>
        {
            foreach (string bridge in new[] { "gap", "diagonal", "wall", "door", "pipe" })
            {
                var state = SteamClusterWorld();
                var parts = AddSteamCluster(state);
                parts.chamber.x = bridge == "diagonal" ? 8 : 7;
                parts.chamber.y = bridge == "diagonal" ? 8 : 10;
                if (bridge == "wall") Add(state, BuildKind.Wall, 9, 10);
                if (bridge == "door") Add(state, BuildKind.Door, 9, 10);
                if (bridge == "pipe") Add(state, BuildKind.SteamPipe, 9, 10);
                var sim = new WorldSimulator(state);
                sim.Step(WorldSimulator.FixedStep);
                Require(sim.State.steamClusters.Count == 2 && sim.State.steamClusters.All(c => c.output == 0), bridge + " bridged a cluster");
                var chamber = Building(sim, parts.chamber.id);
                Require(chamber.items[(int)ItemKind.Fuel] == 2 && chamber.fuel == 0, bridge + " burned fuel");
                Require(Building(sim, parts.boiler.id).clusterId != chamber.clusterId, bridge + " shared ownership");
                Require(sim.State.buildings.Where(b => b.kind == BuildKind.Wall || b.kind == BuildKind.Door || b.kind == BuildKind.SteamPipe)
                    .All(b => b.clusterId == 0), bridge + " became a module");
            }
        });

        Run("steam cluster rotated pump joins at its actual extended footprint", () =>
        {
            for (int rotation = 0; rotation < 4; rotation++)
            {
                var state = SteamClusterWorld();
                var boiler = Add(state, BuildKind.Boiler, 10, 10);
                boiler.modularBoiler = true;
                var pump = Add(state, BuildKind.FeedwaterPump, 10, 12, rotation);
                var chamber = Add(state, BuildKind.CombustionChamber, 12, 12);
                chamber.items[(int)ItemKind.Fuel] = 1;
                Add(state, BuildKind.Chimney, 14, 12);
                Water(state, 11, 13);
                var sim = new WorldSimulator(state);
                sim.Step(WorldSimulator.FixedStep);
                Near(Building(sim, boiler.id).powerGeneration, rotation % 2 == 1 ? 120 : 0, "rotated footprint contact");
                Require(sim.State.steamClusters.Count == (rotation % 2 == 1 ? 1 : 2), "rotation did not change physical grouping");
                Require(DemoCatalog.Width(pump) == (rotation % 2 == 1 ? 2 : 1) &&
                    DemoCatalog.Height(pump) == (rotation % 2 == 1 ? 1 : 2), "pump 1x2 rotation contract");
            }
        });

        Run("steam cluster rotated 3x2 engine never becomes a heat or water bridge", () =>
        {
            for (int rotation = 0; rotation < 4; rotation++)
            {
                int width = rotation % 2 == 0 ? 3 : 2;
                var state = SteamClusterWorld();
                var parts = AddSteamCluster(state, 10 + width, 10);
                parts.chamber.x = 8;
                var engine = Add(state, BuildKind.SteamEngine, 10, 10, rotation);
                Require(DemoCatalog.Width(engine) == width && DemoCatalog.Height(engine) == (rotation % 2 == 0 ? 2 : 3), "engine 3x2 rotation contract");
                var sim = new WorldSimulator(state);
                sim.Step(WorldSimulator.FixedStep);
                Require(sim.State.steamClusters.Count == 2 && sim.State.steamClusters.All(c => c.output == 0), "engine bridged separated modules");
                Require(Building(sim, engine.id).clusterId == 0, "engine acquired cluster membership");
                Require(Building(sim, parts.chamber.id).items[(int)ItemKind.Fuel] == 2, "unconnected chamber burned");
            }
        });

        Run("steam cluster connected steam grids cannot share modules or chamber fuel between groups", () =>
        {
            var state = SteamClusterWorld();
            var fueled = AddSteamCluster(state);
            var empty = AddSteamCluster(state, 20, 10, fuel: 0);
            for (int x = 10; x <= 20; x++) Add(state, BuildKind.SteamPipe, x, 9);
            var sim = new WorldSimulator(state);
            sim.Step(WorldSimulator.FixedStep);
            var first = sim.State.steamClusters.Single(c => c.id == fueled.boiler.id);
            var second = sim.State.steamClusters.Single(c => c.id == empty.boiler.id);
            Near(first.output, 120, "fueled group");
            Near(second.output, 0, "empty group borrowed chamber fuel");
            Require(second.status.Contains("无燃料"), "empty group's status");
            Near(Building(sim, empty.boiler.id).powerGeneration, 0, "remote boiler acquired generation");
            Require(Building(sim, fueled.boiler.id).networkId == Building(sim, empty.boiler.id).networkId, "fixture steam grid did not connect");
            int[] owned = sim.State.steamClusters.SelectMany(c => c.buildingIds).ToArray();
            Require(owned.Length == 8 && owned.Distinct().Count() == 8, "module belonged to multiple groups");
            Near(Building(sim, fueled.chamber.id).fuel, 19.95f, "remote group charged fueled chamber again");
        });

        Run("steam cluster destruction and dismantling remove a physical bridge and stale ownership", () =>
        {
            foreach (bool destroy in new[] { true, false })
            {
                var state = SteamClusterWorld();
                var parts = AddSteamCluster(state);
                parts.chamber.x = 7;
                parts.chimney.x = 9;
                var sim = new WorldSimulator(state);
                sim.Step(WorldSimulator.FixedStep);
                var chamber = Building(sim, parts.chamber.id);
                float fuel = chamber.fuel;
                Near(sim.State.steamClusters.Single().output, 120, "bridge fixture not operating");
                if (destroy) Building(sim, parts.chimney.id).hp = 0;
                else
                {
                    var player = Builder(sim);
                    player.x = 9; player.y = 8;
                    sim.Apply(Command(player, "dismantle", 9, 10));
                    Require(!sim.State.buildings.Any(b => b.id == parts.chimney.id), "bridge dismantle rejected");
                }
                sim.Step(WorldSimulator.FixedStep);
                Require(sim.State.steamClusters.Count == 2 && sim.State.steamClusters.All(c => c.output == 0), "removed bridge still joined modules");
                Require(sim.State.steamClusters.All(c => !c.buildingIds.Contains(parts.chimney.id)), "removed bridge remained a member");
                if (destroy) Require(Building(sim, parts.chimney.id).clusterId == 0, "destroyed module retained cluster ID");
                Near(chamber.fuel, fuel, "severed cluster kept burning reserve");
                Require(chamber.items[(int)ItemKind.Fuel] == 1, "severed cluster burned another item");
            }
        });

        Run("steam cluster skips empty chambers and starts the next usable ID only", () =>
        {
            var state = SteamClusterWorld();
            var first = AddSteamCluster(state, fuel: 0);
            var extra = Add(state, BuildKind.CombustionChamber, 8, 8);
            extra.items[(int)ItemKind.Fuel] = 2;
            var spare = Add(state, BuildKind.CombustionChamber, 6, 8);
            spare.items[(int)ItemKind.Fuel] = 2;
            var sim = new WorldSimulator(state);
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "empty low-ID chamber starved group");
            Near(Building(sim, first.chamber.id).fuel, 0, "empty chamber invented fuel");
            Near(Building(sim, extra.id).fuel, 19.95f, "first available chamber did not run");
            Require(Building(sim, spare.id).items[(int)ItemKind.Fuel] == 2 && Building(sim, spare.id).fuel == 0, "spare chamber wasted fuel");
        });

        Run("steam cluster last half tick conserves heat and leaves standby chamber unburned", () =>
        {
            var state = SteamClusterWorld();
            var first = AddSteamCluster(state, fuel: 0);
            first.chamber.fuel = .025f;
            var spare = Add(state, BuildKind.CombustionChamber, 8, 8);
            spare.items[(int)ItemKind.Fuel] = 1;
            var sim = new WorldSimulator(state);
            Near(sim.State.steamClusters.Single().output, 60, "partial-tick preview");
            Near(Building(sim, first.chamber.id).fuel, .025f, "preview burned final reserve");
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 60, "last half tick lost or multiplied heat");
            Near(Building(sim, first.boiler.id).powerGeneration, 60, "partial heat reaching boiler");
            Near(Building(sim, first.chamber.id).fuel, 0, "last reserve not exhausted");
            Require(Building(sim, spare.id).items[(int)ItemKind.Fuel] == 1 && Building(sim, spare.id).fuel == 0, "partial tick ignited a wasted standby chamber");
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "standby did not take over next tick");
            Near(Building(sim, spare.id).fuel, 19.95f, "standby charged more than one tick");
        });

        Run("steam cluster twenty seconds plus a final half tick conserve the entire fuel budget", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state, fuel: 1);
            parts.chamber.fuel = .025f;
            var extra = Add(state, BuildKind.Boiler, 12, 11);
            extra.modularBoiler = true;
            var sim = new WorldSimulator(state);
            double energy = 0;
            for (int tick = 0; tick < 402; tick++)
            {
                sim.Step(WorldSimulator.FixedStep);
                float output = sim.State.steamClusters.Single().output;
                Near(sim.State.buildings.Where(b => b.modularBoiler).Sum(b => b.powerGeneration), output, "per-tick steam conservation");
                energy += output * WorldSimulator.FixedStep;
            }
            Require(Math.Abs(energy - 2403) < .05, $"fuel energy budget expected 2403, got {energy}");
            var chamber = Building(sim, parts.chamber.id);
            Require(chamber.items[(int)ItemKind.Fuel] == 0 && chamber.fuel == 0, "fuel not exhausted exactly once");
            Near(sim.State.steamClusters.Single().output, 0, "exhausted cluster generated");
            Require(sim.State.steamClusters.Single().status.Contains("无燃料"), "exhausted fuel status");
        });

        Run("steam cluster destroyed boiler chamber pump or chimney is ignored without burning fuel", () =>
        {
            foreach (BuildKind kind in new[] { BuildKind.Boiler, BuildKind.CombustionChamber, BuildKind.FeedwaterPump, BuildKind.Chimney })
            {
                var state = SteamClusterWorld();
                var parts = AddSteamCluster(state);
                var sim = new WorldSimulator(state);
                var dead = sim.State.buildings.Single(b => b.kind == kind);
                dead.hp = 0; dead.clusterId = 987;
                sim.Step(WorldSimulator.FixedStep);
                Require(dead.clusterId == 0 && dead.powerGeneration == 0, kind + " kept derived generation/ownership");
                Require(sim.State.steamClusters.All(c => !c.buildingIds.Contains(dead.id) && c.output == 0), kind + " survived in a cluster");
                var chamber = Building(sim, parts.chamber.id);
                Require(chamber.items[(int)ItemKind.Fuel] == 2 && chamber.fuel == 0, kind + " destruction still charged fuel");
            }
        });

        Run("steam cluster legacy v6 boilers keep independent combustion and never lend their modules", () =>
        {
            var state = SteamClusterWorld();
            state.version = 6;
            var legacy = Add(state, BuildKind.Boiler, 10, 10);
            legacy.items[(int)ItemKind.Fuel] = 2;
            Water(state, 11, 9);
            PrepareLegacySaveFixture(state, 6);
            var sim = new WorldSimulator(state);
            var boiler = Building(sim, legacy.id);
            Require(!boiler.modularBoiler && boiler.clusterId == 0, "legacy boiler became modular");
            Require(sim.State.steamClusters.Count == 0, "standalone legacy boiler became a cluster");
            Require(boiler.items[(int)ItemKind.Fuel] == 2 && boiler.fuel == 0, "legacy restore consumed fuel");
            sim.Step(WorldSimulator.FixedStep);
            Near(boiler.powerGeneration, 120, "legacy boiler lost self-contained generation");
            Require(boiler.items[(int)ItemKind.Fuel] == 1, "legacy fuel item count");
            Near(boiler.fuel, 19.95f, "legacy fuel timing");

            var current = SteamClusterWorld();
            var parts = AddSteamCluster(current);
            parts.boiler.modularBoiler = false;
            parts.boiler.items[(int)ItemKind.Fuel] = 1;
            Water(current, 11, 9);
            var mixed = new WorldSimulator(current);
            mixed.Step(WorldSimulator.FixedStep);
            Near(Building(mixed, parts.boiler.id).powerGeneration, 120, "v7 false mode lost legacy behavior");
            Require(Building(mixed, parts.boiler.id).clusterId == 0, "legacy boiler bridged modules");
            Require(mixed.State.steamClusters.All(c => c.output == 0 && !c.buildingIds.Contains(parts.boiler.id)), "accessories borrowed legacy boiler");
            Require(Building(mixed, parts.chamber.id).items[(int)ItemKind.Fuel] == 2, "orphan chamber burned for a legacy boiler");
        });

        Run("steam cluster modular boilers ignore their own fuel even on a shoreline", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state);
            Water(state, 11, 9);
            var sim = new WorldSimulator(state);
            var boiler = Building(sim, parts.boiler.id);
            boiler.items[(int)ItemKind.Fuel] = 3;
            Advance(sim, .2f);
            Near(boiler.powerGeneration, 120, "modular boiler bypassed cluster allocation");
            Require(boiler.items[(int)ItemKind.Fuel] == 3 && boiler.fuel == 0, "modular boiler burned a second fuel source");
            Near(Building(sim, parts.chamber.id).fuel, 19.8f, "cluster did not exclusively burn its chamber");
        });

        Run("steam cluster preview restores preserve fuel inventory reserves both boiler modes and caller data", () =>
        {
            foreach (bool modular in new[] { false, true })
            {
                var state = SteamClusterWorld();
                var parts = AddSteamCluster(state, fuel: 1);
                parts.boiler.modularBoiler = modular;
                parts.chamber.fuel = .025f;
                if (!modular)
                {
                    parts.boiler.items[(int)ItemKind.Fuel] = 1;
                    parts.boiler.fuel = .025f;
                }
                Water(state, 11, 9);
                state.steamClusters.Add(new SteamClusterState { id = 987, buildingIds = new[] { 987 }, output = 999, status = "过期缓存" });
                foreach (var building in state.buildings) building.clusterId = 987;
                for (int restore = 0; restore < 5; restore++)
                {
                    string before = Dump(state);
                    var sim = new WorldSimulator(state);
                    Require(Dump(state) == before, "preview restore mutated caller-owned state");
                    var chamber = Building(sim, parts.chamber.id);
                    var boiler = Building(sim, parts.boiler.id);
                    Require(boiler.modularBoiler == modular, "save lost bool mode");
                    Require(chamber.items[(int)ItemKind.Fuel] == 1, "preview restore spent chamber item");
                    Near(chamber.fuel, .025f, "preview restore advanced chamber reserve");
                    Require(sim.State.steamClusters.All(c => c.id != 987 && !c.buildingIds.Contains(987)), "restore trusted saved cluster cache");
                    Require(sim.State.buildings.All(b => b.clusterId != 987), "restore trusted saved membership");
                    Near(boiler.fuel, modular ? 0 : .025f, "preview restore changed boiler reserve");
                    Require(boiler.items[(int)ItemKind.Fuel] == (modular ? 0 : 1), "preview restore spent legacy item");
                    Near(boiler.powerGeneration, 120, "preview restore generation");
                    state = Saved(sim.State);
                }
            }
        });

        Run("steam cluster nonconsuming build refreshes never advance fuel or ignite a preview item", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state, fuel: 1);
            parts.chamber.fuel = .025f;
            var sim = new WorldSimulator(state);
            var player = Builder(sim);
            player.x = 20; player.y = 20;
            for (int refresh = 0; refresh < 3; refresh++)
            {
                Ready(player);
                sim.Apply(Command(player, "build", 21, 20, (int)BuildKind.Wall));
                Require(sim.State.buildings.Any(b => b.kind == BuildKind.Wall && b.x == 21 && b.y == 20), "preview fixture did not build");
                var chamber = Building(sim, parts.chamber.id);
                Require(chamber.items[(int)ItemKind.Fuel] == 1, "build preview consumed item");
                Near(chamber.fuel, .025f, "build preview advanced reserve");
                Near(sim.State.steamClusters.Single().output, 120, "build preview cannot anticipate next ignition");
                Ready(player);
                sim.Apply(Command(player, "dismantle", 21, 20));
                Require(!sim.State.buildings.Any(b => b.kind == BuildKind.Wall), "preview fixture did not dismantle");
                Require(chamber.items[(int)ItemKind.Fuel] == 1 && chamber.fuel == .025f, "dismantle preview consumed reserve/item");
            }
        });

        Run("steam cluster ID ordering ownership chamber choice and output ignore input list permutations", () =>
        {
            var state = SteamClusterWorld();
            AddSteamCluster(state);
            ExpandSteamCluster(state);
            var second = AddSteamCluster(state, 30, 10);
            second.chamber.fuel = .025f;
            second.chamber.items[(int)ItemKind.Fuel] = 0;
            var standby = Add(state, BuildKind.CombustionChamber, 28, 8);
            standby.items[(int)ItemKind.Fuel] = 1;
            var reference = new WorldSimulator(state);
            Advance(reference, .1f);
            string expected = SteamClusterSignature(reference);
            for (int seed = 0; seed < 12; seed++)
            {
                var shuffled = Saved(state);
                var random = new Random(seed);
                shuffled.buildings = shuffled.buildings.OrderBy(_ => random.Next()).ToList();
                var sim = new WorldSimulator(shuffled);
                Advance(sim, .1f);
                Require(SteamClusterSignature(sim) == expected, "list order changed cluster results for seed " + seed);
                Require(sim.State.steamClusters.Select(c => c.id).SequenceEqual(new[] { 1, second.boiler.id }), "clusters not sorted by minimum ID");
                foreach (var cluster in sim.State.steamClusters)
                    Require(cluster.id == cluster.buildingIds.Min() && cluster.buildingIds.SequenceEqual(cluster.buildingIds.OrderBy(id => id)), "unstable cluster/member IDs");
            }
        });

        Run("steam cluster save reload continues midburn and partial final ticks identically", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state, fuel: 0);
            parts.chamber.fuel = .125f;
            var first = new WorldSimulator(state);
            first.Step(WorldSimulator.FixedStep);
            var restored = new WorldSimulator(Saved(first.State));
            for (int tick = 0; tick < 5; tick++)
            {
                first.Step(WorldSimulator.FixedStep);
                restored.Step(WorldSimulator.FixedStep);
                Require(SteamClusterSignature(first) == SteamClusterSignature(restored), "save replay diverged at tick " + tick);
            }
            Near(first.State.steamClusters.Single().output, 0, "save replay invented residual steam");
            Require(Building(first, parts.chamber.id).fuel == 0 && Building(restored, parts.chamber.id).fuel == 0, "save replay fuel not exhausted");
        });

        Run("steam cluster warm topology follows moved modules and changed boiler mode", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state);
            var sim = new WorldSimulator(state);
            sim.Step(WorldSimulator.FixedStep);
            var chamber = Building(sim, parts.chamber.id);
            var boiler = Building(sim, parts.boiler.id);
            chamber.x = 7;
            sim.Step(WorldSimulator.FixedStep);
            Require(sim.State.steamClusters.Count == 2 && sim.State.steamClusters.All(c => c.output == 0), "moved module used stale topology");
            Near(chamber.fuel, 19.95f, "moved chamber kept burning");
            chamber.x = 8;
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "returning module did not rejoin");
            Near(chamber.fuel, 19.9f, "rejoined chamber timing");
            boiler.modularBoiler = false;
            sim.Step(WorldSimulator.FixedStep);
            Require(boiler.clusterId == 0 && sim.State.steamClusters.All(c => c.output == 0), "false mode retained cached boiler");
            Near(chamber.fuel, 19.9f, "orphan chamber kept burning after mode switch");
            boiler.modularBoiler = true;
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "true mode did not rejoin cached topology");
            chamber.kind = BuildKind.Chimney;
            sim.Step(WorldSimulator.FixedStep);
            Require(sim.State.steamClusters.All(c => c.chambers == 0 && c.output == 0), "changed module kind retained cached chamber");
            Near(chamber.fuel, 19.85f, "changed module kind kept burning reserve");
            chamber.kind = BuildKind.CombustionChamber;
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "restored module kind did not rejoin");
        });

        Run("steam cluster warm topology tracks pump rotation and footprint version changes", () =>
        {
            var state = SteamClusterWorld();
            var boiler = Add(state, BuildKind.Boiler, 10, 10);
            boiler.modularBoiler = true;
            var pump = Add(state, BuildKind.FeedwaterPump, 10, 12, rotation: 1);
            var chamber = Add(state, BuildKind.CombustionChamber, 12, 12);
            chamber.items[(int)ItemKind.Fuel] = 1;
            Add(state, BuildKind.Chimney, 14, 12);
            Water(state, 11, 13);
            var sim = new WorldSimulator(state);
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "rotated warm fixture not running");
            var livePump = Building(sim, pump.id);
            livePump.rotation = 0;
            sim.Step(WorldSimulator.FixedStep);
            Require(sim.State.steamClusters.Count == 2 && sim.State.steamClusters.All(c => c.output == 0), "rotation retained stale 2x1 footprint");
            livePump.rotation = 1;
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "restored rotation did not rejoin");
            livePump.footprintVersion = 0;
            sim.Step(WorldSimulator.FixedStep);
            Require(sim.State.steamClusters.Count == 2 && sim.State.steamClusters.All(c => c.output == 0), "legacy 1x1 footprint retained cached contact");
            Near(Building(sim, chamber.id).fuel, 19.9f, "disconnected footprint wasted reserve");
        });

        Run("steam cluster warm topology replaces references and recomputes minimum IDs", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state);
            var sim = new WorldSimulator(state);
            sim.Step(WorldSimulator.FixedStep);
            var old = Building(sim, parts.chamber.id);
            var replacement = new BuildingState
            {
                id = old.id, kind = old.kind, x = old.x, y = old.y, rotation = old.rotation,
                footprintVersion = old.footprintVersion, hp = old.hp, fuel = 3, items = Copy(old.items)
            };
            sim.State.buildings[sim.State.buildings.IndexOf(old)] = replacement;
            sim.Step(WorldSimulator.FixedStep);
            Near(replacement.fuel, 2.95f, "replacement reference did not supply heat");
            Near(old.fuel, 19.95f, "cached removed object was burned");
            var boiler = Building(sim, parts.boiler.id);
            boiler.id = sim.State.nextId++;
            sim.Step(WorldSimulator.FixedStep);
            var cluster = sim.State.steamClusters.Single();
            Require(cluster.id == replacement.id && cluster.buildingIds.SequenceEqual(new[]
                { replacement.id, parts.pump.id, parts.chimney.id, boiler.id }), "changed ID left stale root or member ordering");
            Require(sim.State.buildings.All(b => b.clusterId == replacement.id), "changed minimum ID left stale ownership");
        });

        Run("steam cluster destroyed member replacement works when live module count stays constant", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state);
            var sim = new WorldSimulator(state);
            sim.Step(WorldSimulator.FixedStep);
            var old = Building(sim, parts.chamber.id);
            old.hp = 0;
            var replacement = Add(sim.State, BuildKind.CombustionChamber, old.x, old.y);
            replacement.items[(int)ItemKind.Fuel] = 1;
            sim.Step(WorldSimulator.FixedStep);
            var cluster = sim.State.steamClusters.Single();
            Require(cluster.buildingIds.Length == 4 && !cluster.buildingIds.Contains(old.id) && cluster.buildingIds.Contains(replacement.id), "same-count live replacement retained destroyed member");
            Require(old.clusterId == 0, "destroyed member ownership not cleared");
            Near(old.fuel, 19.95f, "destroyed member kept burning");
            Near(replacement.fuel, 19.95f, "replacement not burned once");
            Near(cluster.output, 120, "replacement did not restore output");
        });

        Run("steam cluster cached groups recompute groundwater capacity before selecting chambers", () =>
        {
            var state = SteamClusterWorld();
            var first = AddSteamCluster(state);
            var extra = ExpandSteamCluster(state);
            var sim = new WorldSimulator(state);
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 240, "expanded warm fixture");
            var ground = sim.State.ground.ToCharArray();
            ground[11 + 14 * sim.State.width] = '4';
            sim.State.ground = new string(ground);
            sim.Step(WorldSimulator.FixedStep);
            var cluster = sim.State.steamClusters.Single();
            Require(cluster.pumps == 2, "shoreline changed physical pump count");
            Near(cluster.waterCapacity, 120, "cached pump retained lost shoreline");
            Near(cluster.output, 120, "cached group ignored water bottleneck");
            Near(Building(sim, extra.chamber.id).fuel, 19.95f, "unused extra chamber burned after water loss");
            Water(sim.State, 11, 14);
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 240, "cached group did not regain water capacity");
            Near(Building(sim, first.chamber.id).fuel, 19.85f, "primary chamber timing");
            Near(Building(sim, extra.chamber.id).fuel, 19.9f, "secondary chamber timing");
        });

        Run("steam cluster cached groups read current inventory and partial fuel every tick", () =>
        {
            var state = SteamClusterWorld();
            var parts = AddSteamCluster(state, fuel: 0);
            var sim = new WorldSimulator(state);
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 0, "unfueled warm fixture");
            var chamber = Building(sim, parts.chamber.id);
            chamber.items[(int)ItemKind.Fuel] = 1;
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "cached group ignored fresh fuel item");
            Require(chamber.items[(int)ItemKind.Fuel] == 0, "fresh fuel not consumed once");
            chamber.fuel = .025f;
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 60, "cached group ignored final partial fuel");
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 0, "cached group reused depleted fuel");
            Require(sim.State.steamClusters.Single().status.Contains("无燃料"), "cached group reused running status");
            chamber.items[(int)ItemKind.Fuel] = 1;
            sim.Step(WorldSimulator.FixedStep);
            Near(sim.State.steamClusters.Single().output, 120, "refueled cached group did not restart");
            Near(chamber.fuel, 19.95f, "refueled cached chamber timing");
        });
    }
}
