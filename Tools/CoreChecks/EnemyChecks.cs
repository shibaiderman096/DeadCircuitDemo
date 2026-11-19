using DeadCircuit;

static partial class Checks
{
    // Exercises only public Apply/Step and save restoration, never private AI methods.
    static void EnemyChecks()
    {
        Run("enemy spitter attacks a visible player at range through Step", () =>
        {
            var sim = EnemyFixture(out var player);
            var spitter = EnemyAt(sim, 4, 27, 35);
            Advance(sim, .05f);
            Require(player.hp < 100 && spitter.x == 27, "spitter walked instead of using its ranged attack");
            float hp = player.hp; Advance(sim, .5f);
            Near(player.hp, hp, "spitter ignored its special cooldown");
        });
        Run("enemy spitter obeys walls and approaches from outside range", () =>
        {
            var blocked = EnemyFixture(out var p, arrange: (s, _) => Add(s, BuildKind.Wall, 30, 35));
            var spitter = EnemyAt(blocked, 4, 27, 35); Advance(blocked, .05f);
            Near(p.hp, 100, "spit passed through a wall"); Require(spitter.x > 27, "blocked spitter did not approach");
            var far = EnemyFixture(out var q); var distant = EnemyAt(far, 4, 24, 35); Advance(far, .05f);
            Near(q.hp, 100, "spit exceeded range"); Require(distant.x > 24, "distant spitter did not approach");
        });
        foreach (int variant in new[] { 0, 1, 2 })
        {
            Run($"enemy legacy {variant} preserves melee damage and cadence", () =>
            {
                var sim = EnemyFixture(out var p); var z = EnemyAt(sim, variant, 32.9f, 35);
                Advance(sim, .05f); Near(p.hp, variant == 2 ? 82 : 90, "legacy melee damage");
                Near(z.attackCooldown, variant == 1 ? .8f : 1.05f, "legacy attack interval");
                float hp = p.hp; Advance(sim, .7f); Near(p.hp, hp, "legacy melee repeated early");
                Advance(sim, variant == 1 ? .15f : .4f); Require(p.hp < hp, "legacy melee did not repeat");
            });
            Run($"enemy legacy {variant} preserves movement and obstacle damage", () =>
            {
                var sim = EnemyFixture(out _); var z = EnemyAt(sim, variant, 27, 35); Advance(sim, .05f);
                float speed = variant == 0 ? 1.15f : variant == 1 ? 1.8f : .85f;
                Near(z.x - 27, speed * 1.015f * .05f, "legacy speed");
                var blocked = EnemyFixture(out _, arrange: (s, _) => Add(s, BuildKind.Wall, 31, 35));
                var wall = blocked.State.buildings.Single(); float hp = wall.hp;
                var breaker = EnemyAt(blocked, variant, 30.1f, 35); Advance(blocked, .05f);
                Near(wall.hp, hp - (variant == 2 ? 26 : 14), "legacy structure damage");
                Near(breaker.attackCooldown, .8f, "legacy structure interval");
            });
        }
        Run("enemy crawler drains stamina with its faster bite cadence", () =>
        {
            var sim = EnemyFixture(out var p); var z = EnemyAt(sim, 3, 32.9f, 35); Advance(sim, .05f);
            Near(p.hp, 93, "crawler bite damage"); Near(p.stamina, 84, "crawler stamina drain");
            Near(z.attackCooldown, .7f, "crawler cadence");
            Advance(sim, .75f); Require(p.hp <= 86, "crawler did not bite again");
        });
        Run("enemy armored target halves real Apply melee damage", () =>
        {
            foreach (int variant in new[] { 0, 5 })
            {
                var sim = EnemyFixture(out var p); var z = EnemyAt(sim, variant, 33, 35);
                sim.Apply(new PlayerCommand { playerId = p.id, action = "attack", aimX = 1 });
                Near(z.hp, variant == 5 ? 490.5f : 481, "enemy armor did not affect actual attack");
            }
        });
        Run("enemy armor piercing bypasses armor and retains three target penetration", () =>
        {
            var sim = EnemyFixture(out var p, arrange: (s, q) => EnemyGun(s, q, ItemKind.ArmorPiercingAmmo));
            var targets = Enumerable.Range(0, 4).Select(i => EnemyAt(sim, 5, 34 + i * 2, 35)).ToArray();
            sim.Apply(new PlayerCommand { playerId = p.id, action = "attack", aimX = 1, aiming = true });
            Near(500 - targets[0].hp, 98.9f, "AP armor bypass or original 15 percent bonus changed");
            Near(500 - targets[1].hp, 79.12f, "second AP target"); Near(500 - targets[2].hp, 63.296f, "third AP target");
            Near(targets[3].hp, 500, "AP exceeded three targets");
        });
        Run("enemy ordinary precision rounds keep armor and two target penetration", () =>
        {
            var sim = EnemyFixture(out var p, arrange: (s, q) => EnemyGun(s, q, ItemKind.PrecisionAmmo));
            var targets = Enumerable.Range(0, 3).Select(i => EnemyAt(sim, 5, 34 + i * 2, 35)).ToArray();
            sim.Apply(new PlayerCommand { playerId = p.id, action = "attack", aimX = 1, aiming = true });
            Near(500 - targets[0].hp, 43, "ordinary bullet ignored armor"); Near(500 - targets[1].hp, 34.4f, "second ordinary target");
            Near(targets[2].hp, 500, "ordinary precision round exceeded two targets");
        });
        Run("enemy burning pulse hits players and a multicell machine once per cadence", () =>
        {
            var sim = EnemyFixture(out var p, arrange: (s, _) =>
            {
                Add(s, BuildKind.Assembler, 28, 33);
                s.players.Add(new PlayerState { id = "burn-second", seat = 1, x = 31, y = 36 });
            });
            var machine = sim.State.buildings.Single(); float hp = machine.hp;
            var other = sim.State.players.Single(q => q.id == "burn-second");
            EnemyAt(sim, 6, 30.8f, 35); Advance(sim, .05f);
            Near(p.hp, 95, "burning primary player damage"); Near(other.hp, 95, "burning secondary player damage");
            Near(machine.hp, hp - 8, "multicell machine took repeated pulse damage");
            Advance(sim, .5f); Near(p.hp, 95, "burning pulse repeated early"); Near(machine.hp, hp - 8, "machine pulse ignored cooldown");
        });
        foreach (int variant in new[] { 8, 9 })
            Run($"enemy special {variant} drains stamina and respects cooldown and sight", () =>
            {
                var sim = EnemyFixture(out var p); EnemyAt(sim, variant, 30, 35); Advance(sim, .05f);
                Near(p.hp, 96, "pulse damage"); Near(p.stamina, variant == 8 ? 92 : 76, "special stamina drain");
                Advance(sim, .5f); Near(p.hp, 96, "special repeated before two seconds");
                var blocked = EnemyFixture(out var q, arrange: (s, _) => Add(s, BuildKind.Wall, 31, 35));
                EnemyAt(blocked, variant, 30, 35); Advance(blocked, .05f);
                Near(q.hp, 100, "special damaged through wall"); Near(q.stamina, 100, "special drained through wall");
            });
        Run("enemy infection fire and frost apply persistent survival effects through Step", () =>
        {
            foreach (int variant in new[] { 6, 8, 9 })
            {
                var control = EnemyFixture(out var reference); control.State.survival.enabled = control.State.survival.initialized = true;
                var sim = EnemyFixture(out var p); sim.State.survival.enabled = sim.State.survival.initialized = true;
                reference.needs.initialized = p.needs.initialized = true;
                EnemyAt(sim, variant, 30.8f, 35); Advance(control, .05f); Advance(sim, .05f);
                Near(p.needs.infection - reference.needs.infection, variant == 8 ? 6 : 0, "infection delta");
                Near(p.needs.bodyTemperature - reference.needs.bodyTemperature, variant == 6 ? .6f : variant == 9 ? -1.5f : 0, "temperature delta");
                var restored = new WorldSimulator(Saved(sim.State)); var copied = restored.State.players.Single();
                Near(copied.needs.infection, p.needs.infection, "infection did not persist");
                Near(copied.needs.bodyTemperature, p.needs.bodyTemperature, "enemy temperature did not persist");
            }
        });
        Run("enemy special attacks honor new player protection and equipped armor", () =>
        {
            foreach (int variant in new[] { 3, 4, 6, 8, 9, 10 })
            {
                var sim = new WorldSimulator(Flat(10)); var p = sim.AddPlayer("protected", "Protected");
                EnemyAt(sim, variant, p.x + .9f, p.y); Advance(sim, .05f);
                Near(p.hp, 100, "special bypassed protection"); Near(p.stamina, 100, "protected stamina changed");
            }
            var armored = EnemyFixture(out var q, arrange: (s, p) =>
            {
                var armor = new GearState { id = s.nextId++, kind = GearKind.ArmorVest };
                p.gear.Add(armor); p.armorIds[1] = armor.id;
            });
            EnemyAt(armored, 4, 27, 35); Advance(armored, .05f);
            Require(q.hp > 88 && q.hp < 100 && q.gear.Single().durability == 99, "special bypassed armor mitigation or wear");
        });
        Run("enemy scream wakes distant companions and persists through restoration", () =>
        {
            var sim = EnemyFixture(out _, wave: 0); var dormant = EnemyAt(sim, 0, 10, 35);
            Advance(sim, .05f); Near(dormant.x, 10, "opening dormant enemy activated without a scream");
            sim.State.zombies.Clear(); var screamer = EnemyAt(sim, 7, 24, 35); var companion = EnemyAt(sim, 0, 10, 35);
            Advance(sim, .05f); Require(companion.x > 10, "scream did not awaken distant companion");
            Near(companion.alertedUntil, sim.State.elapsed + 8, "scream alert duration");
            sim.State.zombies.Remove(screamer);
            var restored = new WorldSimulator(Saved(sim.State)); var copied = restored.State.zombies.Single();
            Near(copied.alertedUntil, companion.alertedUntil, "alert lost in save/restore");
            float x = copied.x; Advance(restored, .05f); Require(copied.x > x, "restored alert no longer affects AI");
            Advance(restored, 8.1f); x = copied.x; Advance(restored, .05f); Near(copied.x, x, "expired alert kept distant enemy awake");
        });
        Run("enemy scream cannot wake allies through a wall", () =>
        {
            var sim = EnemyFixture(out _, wave: 0, arrange: (s, _) => Add(s, BuildKind.Wall, 28, 35));
            EnemyAt(sim, 7, 24, 35); var companion = EnemyAt(sim, 0, 10, 35); Advance(sim, .05f);
            Near(companion.alertedUntil, 0, "scream triggered without seeing a player"); Near(companion.x, 10, "companion woke through wall");
        });
        Run("enemy bomber damages nearby player and machine then removes itself safely", () =>
        {
            var sim = EnemyFixture(out var p, arrange: (s, _) =>
            {
                Add(s, BuildKind.Assembler, 28, 33);
                s.players.Add(new PlayerState { id = "blast-far", seat = 1, x = 35, y = 35 });
            });
            var machine = sim.State.buildings.Single(); float hp = machine.hp;
            EnemyAt(sim, 10, 30.8f, 35); var follower = EnemyAt(sim, 0, 27, 35); Advance(sim, .05f);
            Require(sim.State.zombies.Count == 1 && follower.x > 27, "self-removal skipped or invalidated the next enemy");
            Require(p.hp < 100 && p.hp > 58, "explosion player damage/falloff");
            Near(sim.State.players.Single(q => q.id == "blast-far").hp, 100, "explosion exceeded radius");
            Require(machine.hp < hp && machine.hp >= hp - 75, "multicell machine missed or hit multiple times");
            Require(sim.State.kills == 0 && p.xp == 0, "suicide granted kill rewards");
        });
        Run("enemy bomber snapshots wall cover before destroying obstacles", () =>
        {
            var sim = EnemyFixture(out var p, arrange: (s, _) =>
                s.nodes.Add(new NodeState { id = s.nextId++, kind = NodeKind.Wall, x = 31, y = 35, hp = 1 }));
            EnemyAt(sim, 10, 30.1f, 35); Advance(sim, .05f);
            Require(sim.State.nodes.Count == 0 && sim.State.zombies.Count == 0, "bomber did not destroy obstacle and itself");
            Near(p.hp, 100, "destroyed cover leaked damage in the same explosion");
        });
        Run("enemy killing bomber through Apply prevents detonation", () =>
        {
            var sim = EnemyFixture(out var p); EnemyAt(sim, 10, 33, 35, 1);
            sim.Apply(new PlayerCommand { playerId = p.id, action = "attack", aimX = 1 }); Advance(sim, .05f);
            Near(p.hp, 100, "killed bomber still exploded"); Require(sim.State.zombies.Count == 0 && sim.State.kills == 1, "player kill did not resolve");
        });
        Run("enemy simultaneous bombers safely remove themselves during Step", () =>
        {
            var sim = EnemyFixture(out var p); EnemyAt(sim, 10, 31.1f, 35); EnemyAt(sim, 10, 32.9f, 35);
            Advance(sim, .05f); Require(sim.State.zombies.Count == 0 && p.hp > 0 && p.hp < 70, "simultaneous explosions failed");
        });
        Run("enemy nocturnal elite sleeps at distance by day and pursues at night", () =>
        {
            var day = EnemyFixture(out _, 10); var a = EnemyAt(day, 11, 23, 35); Advance(day, .05f); Near(a.x, 23, "day elite pursued beyond seven cells");
            var night = EnemyFixture(out _, 100); var b = EnemyAt(night, 11, 23, 35); Advance(night, .05f); Require(b.x > 23, "night elite stayed asleep");
            a.x = 27; b.x = 27; Advance(day, .05f); Advance(night, .05f);
            Require(b.x - 27 > (a.x - 27) * 2, "night pursuit lacks speed difference");
        });
        Run("enemy nocturnal elite changes real melee damage and cadence at night", () =>
        {
            foreach (bool night in new[] { false, true })
            {
                var sim = EnemyFixture(out var p, night ? 100 : 10); var z = EnemyAt(sim, 11, 32.9f, 35); Advance(sim, .05f);
                Near(p.hp, night ? 77 : 89, "elite day/night damage"); Near(z.attackCooldown, night ? .65f : 1.3f, "elite day/night cadence");
                float hp = p.hp; Advance(sim, .7f); Require((p.hp < hp) == night, "elite cadence did not affect subsequent attacks");
            }
        });
        EnemySpawnChecks();
    }

    static WorldSimulator EnemyFixture(out PlayerState player, float elapsed = 10f, int wave = 1, Action<WorldState, PlayerState> arrange = null)
    {
        var state = Flat(elapsed); state.wave = wave; state.nextWaveAt = elapsed + 180;
        var p = new PlayerState { id = "enemy-check", name = "敌人检查", x = 32, y = 35, activeSlot = 2 };
        state.players.Add(p); arrange?.Invoke(state, p);
        var sim = new WorldSimulator(state);
        player = sim.State.players.Single(p => p.id == "enemy-check"); return sim;
    }

    static ZombieState EnemyAt(WorldSimulator sim, int variant, float x, float y, float hp = 500f)
    {
        var zombie = new ZombieState { id = sim.State.nextId++, variant = variant, x = x, y = y, hp = hp };
        sim.State.zombies.Add(zombie); return zombie;
    }

    static void EnemyGun(WorldState state, PlayerState player, ItemKind ammo)
    {
        var gun = new GearState { id = state.nextId++, kind = GearKind.PrecisionRifle, magazine = 5, loadedAmmo = ammo };
        player.gear.Add(gun); player.weaponId = gun.id; player.activeSlot = 0;
    }

    static int[] EnemySpawnSample(int wave, int generatorVersion = 1, BiomeKind biome = BiomeKind.Meadow, DistrictKind district = DistrictKind.Wilderness)
    {
        var sim = EnemyFixture(out _, wave: wave - 1); sim.State.generatorVersion = generatorVersion;
        sim.State.biomes = new string((char)('0' + (int)biome), 4096);
        if (district != DistrictKind.Wilderness)
        {
            // Metadata alone determines districts: destroyed POI walls/containers need not exist.
            foreach (int edge in new[] { 2, 57 }) foreach (int along in new[] { 2, 13, 24, 35, 46, 57 })
            {
                sim.State.sites.Add(new SiteState { id = sim.State.nextId++, x = along, y = edge, width = 5, height = 5, district = district });
                if (along != 2 && along != 57)
                    sim.State.sites.Add(new SiteState { id = sim.State.nextId++, x = edge, y = along, width = 5, height = 5, district = district });
            }
        }
        var variants = new List<int>();
        for (int n = 0; n < 32; n++)
        {
            sim.State.zombies.Clear(); sim.State.wave = wave - 1; sim.State.nextWaveAt = sim.State.elapsed;
            Advance(sim, .05f);
            Require(sim.State.wave == wave && sim.State.zombies.Count > 0, "public Step did not start requested wave");
            foreach (var z in sim.State.zombies)
            {
                Near(z.hp, EnemyCatalog.Stats(z.variant).hp + Math.Min(100, wave * 4), "spawn HP did not use enemy catalog");
                Require(!sim.IsBlocked((int)z.x, (int)z.y), "enemy spawned in blocked cell");
                variants.Add(z.variant);
            }
        }
        return variants.ToArray();
    }

    static void EnemySpawnChecks()
    {
        Run("enemy gen0 waves retain original three types and gen1 first wave stays ordinary", () =>
        {
            var legacy = EnemySpawnSample(8, 0); Require(legacy.All(v => v <= 2) && legacy.Distinct().Count() == 3, "gen0 gained special enemies");
            Require(EnemySpawnSample(1).All(v => v == 0), "new first wave gained special enemies");
            var opening = new WorldSimulator(73149);
            int[] x = { 14, 50, 32, 32, 12, 54 }, y = { 32, 32, 14, 54, 33, 33 };
            Require(opening.State.zombies.Count == 6, "initial six count changed");
            for (int i = 0; i < 6; i++)
            {
                var z = opening.State.zombies[i]; Near(z.x, x[i], "opening x"); Near(z.y, y[i], "opening y");
                Require(z.variant == (i == 5 ? 1 : 0), "opening type changed"); Near(z.hp, i == 5 ? 32 : 45, "opening HP");
            }
        });
        Run("enemy waves progressively unlock specials through public Step", () =>
        {
            int[][] allowed = { new[] { 0, 1, 3 }, new[] { 0, 1, 2, 3, 4, 7 }, new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, Enumerable.Range(0, 11).ToArray(), Enumerable.Range(0, 12).ToArray() };
            for (int wave = 2; wave <= 7; wave++)
            {
                var sample = EnemySpawnSample(wave);
                Require(sample.All(v => allowed[wave - 2].Contains(v)), "future type unlocked early at wave " + wave);
                Require(allowed[wave - 2].All(sample.Contains), "unlocked type absent from sample at wave " + wave);
            }
        });
        Run("enemy biome and district change actual wave distribution deterministically", () =>
        {
            var meadow = EnemySpawnSample(8); var tundra = EnemySpawnSample(8, biome: BiomeKind.Tundra);
            var industrial = EnemySpawnSample(8, district: DistrictKind.Industrial);
            Require(meadow.SequenceEqual(EnemySpawnSample(8)), "same seed and history changed spawn selection");
            Require(tundra.Count(v => v == 9) > meadow.Count(v => v == 9) * 2, "tundra did not favor frost enemies");
            Require(industrial.Count(v => v == 5 || v == 10) > meadow.Count(v => v == 5 || v == 10), "industrial metadata did not affect waves");
        });
    }
}
