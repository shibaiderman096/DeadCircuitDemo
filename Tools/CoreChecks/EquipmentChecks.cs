using DeadCircuit;
using System.Text.Json;

static partial class Checks
{
    // Only arrange input graphs before restoration; every operation under test is
    // Apply/Step/SetConnected. No private simulator methods or reflected members.
    static WorldSimulator EquipmentFixture(out PlayerState player, GearKind? kind = null,
        TechEra era = TechEra.Nuclear, Action<WorldState, PlayerState> arrange = null)
    {
        var state = Flat(era: era); Add(state, BuildKind.Workbench, 30, 35);
        if(era>=TechEra.Nuclear)
        {
            state.nuclearUraniumMined=state.gathered=4;
            state.nuclearResearch=NuclearBit(NuclearTech.UraniumBeneficiation)|NuclearBit(NuclearTech.RadiationMonitoring)|NuclearBit(NuclearTech.RadiationProtection);
        }
        // Legacy equipment behavior fixtures represent an already established
        // industrial branch. Dedicated research tests clear these masks again.
        state.primitiveVersion=1; state.primitiveResearch=255;
        if(era>=TechEra.Water)
        {
            state.waterVersion=1; state.waterResearch=255;
            state.waterCrushed=4; state.waterSmelted=6; state.waterPressed=4;
            state.waterGears=state.waterBearings=2; state.waterTransported=4;
        }
        state.weaponResearch=WeaponTechCatalog.All.Where(definition=>definition.era<=(int)era)
            .Aggregate(0,(mask,definition)=>mask|(1<<definition.id));
        state.toolResearch=ToolTechCatalog.All.Where(definition=>definition.era<=(int)era)
            .Aggregate(0,(mask,definition)=>mask|(1<<definition.id));
        foreach(var (item,amount) in new[]
        {
            (ItemKind.Steel,8),(ItemKind.SteelPlate,2),(ItemKind.Copper,4),(ItemKind.Polymer,4),
            (ItemKind.Circuit,2),(ItemKind.Lubricant,2),(ItemKind.PrecisionPart,2),
            (ItemKind.AdvancedCircuit,2),(ItemKind.HeatAlloy,2),(ItemKind.Gear,2),(ItemKind.Bearing,2)
        }) state.produced[(int)item]=Math.Max(state.produced[(int)item],amount);
        var p = new PlayerState { id = "equipment", name = "Equipment", x = 32, y = 35,
            items = Enumerable.Repeat(300, DemoCatalog.ItemCount).ToArray() };
        p.items[(int)ItemKind.NuclearWaste] = 0;
        if (kind.HasValue)
        {
            var gear = new GearState { id = state.nextId++, kind = kind.Value, loadedAmmo = EquipmentCatalog.Gear(kind.Value).ammo };
            p.gear.Add(gear);
            if (EquipmentCatalog.IsArmor(kind.Value)) { p.armorIds[EquipmentCatalog.ArmorSlot(kind.Value)] = gear.id; p.activeSlot = 2; }
            else if (EquipmentCatalog.Gear(kind.Value).tool) { p.toolId = gear.id; p.activeSlot = 1; }
            else { p.weaponId = gear.id; p.activeSlot = 0; }
        }
        else p.activeSlot = 2;
        state.players.Add(p); arrange?.Invoke(state, p);
        state.generation.seed = state.seed;
        var sim = new WorldSimulator(state); player = sim.State.players.Single(candidate => candidate.id == "equipment"); return sim;
    }
    static string EquipmentEconomy(PlayerState p) => JsonSerializer.Serialize(new
    { p.items, p.gear, p.parts, p.weaponId, p.toolId, p.activeSlot, p.armorIds, p.selectedAmmo, p.reloadingGearId, p.reloadRemaining }, Json);
    static GearState CraftGear(WorldSimulator sim, PlayerState p, GearKind kind)
    {
        Advance(sim, .6f); var before = p.gear.Select(g => g.id).ToHashSet();
        sim.Apply(Command(p, "gearcraft", arg: (int)kind));
        var made = p.gear.Where(g => !before.Contains(g.id)).ToArray();
        Require(made.Length == 1 && made[0].kind == kind, "gearcraft did not create exactly one " + kind);
        return made[0];
    }
    static void CraftPart(WorldSimulator sim, PlayerState p, PartKind kind)
    {
        Advance(sim, .6f); int before = p.parts[(int)kind];
        sim.Apply(Command(p, "partcraft", arg: (int)kind));
        Require(p.parts[(int)kind] == before + 1, "partcraft did not create " + kind);
    }
    static void InstallPart(WorldSimulator sim, PlayerState p, GearState gear, PartKind kind)
    {
        CraftPart(sim, p, kind); Advance(sim, .6f);
        sim.Apply(Command(p, "attach", x: (int)kind, arg: gear.id));
        Require(gear.attachments[(int)EquipmentCatalog.Part(kind).slot] == (int)kind, "attach did not install " + kind);
    }
    static void LoadEquipment(WorldSimulator sim, PlayerState p, GearState gear, ItemKind? ammo = null)
    {
        Advance(sim, .6f); sim.Apply(Command(p, "equip", arg: gear.id));
        sim.Apply(Command(p, "switch", arg: 0));
        sim.Apply(Command(p, "ammoselect", arg: (int)(ammo ?? EquipmentCatalog.Gear(gear.kind).ammo)));
        sim.Apply(Command(p, "reload"));
        Require(p.reloadRemaining > 0, "reload did not begin"); Advance(sim, p.reloadRemaining + .1f);
        Require(gear.magazine > 0 && p.reloadRemaining == 0, "reload did not complete");
    }
    static ZombieState EquipmentTarget(WorldState state, float x, float y = 35, float hp = 1000)
    {
        var z = new ZombieState { id = state.nextId++, x = x, y = y, hp = hp }; state.zombies.Add(z); return z;
    }
    static void FireEquipment(WorldSimulator sim, PlayerState p, float x = 1, float y = 0, bool aiming = false) =>
        sim.Apply(new PlayerCommand { playerId = p.id, action = "attack", aimX = x, aimY = y, aiming = aiming });

    static void EquipmentChecks()
    {
        Run("equipment current worlds save new primitive material slots", () =>
        {
            var sim = New(out var p);
            Require(sim.State.version == WorldSimulator.SaveVersion, "new world must use the current save version");
            Require(DemoCatalog.ItemCount == 66 && p.items.Length == DemoCatalog.ItemCount && sim.State.produced.Length == DemoCatalog.ItemCount, "equipment inventory does not include ground pickups");
        });
        Run("equipment preserves old ammo and appends four caliber ordinals", () =>
        {
            Require((int)ItemKind.Ammo == 5, "legacy turret/pistol ammunition ordinal changed");
            string[] names = { "RifleAmmo", "Shell", "PrecisionAmmo", "ArmorPiercingAmmo" };
            for (int i = 0; i < names.Length; i++)
                Require(Enum.TryParse<ItemKind>(names[i], out var item) && (int)item == 21 + i, "missing or reordered ammunition: " + names[i]);
        });
        Run("equipment Apply crafts a primitive tool at a nearby workbench", () =>
        {
            var sim = EquipmentFixture(out var p, era: TechEra.Primitive); int before = p.gear.Count;
            sim.Apply(Command(p, "gearcraft", arg: 0));
            Require(p.gear.Count == before + 1 && p.gear.Last().kind == GearKind.StoneAxe, "public gearcraft command did not create a StoneAxe");
        });
        EquipmentCatalogChecks();
        EquipmentCraftChecks();
        EquipmentSaveChecks();
        EquipmentUseChecks();
        EquipmentReloadChecks();
        EquipmentAttachmentChecks();
        EquipmentCombatChecks();
        EquipmentSalvageChecks();
        EquipmentWrenchChecks();
        EquipmentArmorChecks();
    }

    static void EquipmentCatalogChecks()
    {
        Run("equipment catalog keeps persistent gear part and slot ordinals", () =>
        {
            var gears = new[] { GearKind.StoneAxe, GearKind.StonePickaxe, GearKind.SteelAxe, GearKind.SteelPickaxe, GearKind.Wrench, GearKind.Knife, GearKind.Pistol, GearKind.Shotgun, GearKind.AssaultRifle, GearKind.PrecisionRifle };
            var parts = new[] { PartKind.RedDot, PartKind.Scope4x, PartKind.Suppressor, PartKind.MuzzleBrake, PartKind.ExtendedMag, PartKind.ErgonomicGrip, PartKind.MatchBarrel };
            var slots = new[] { AttachmentSlot.Optic, AttachmentSlot.Muzzle, AttachmentSlot.Magazine, AttachmentSlot.Grip, AttachmentSlot.Barrel };
            Require(EquipmentCatalog.GearNames.Length == 20 && (int)GearKind.RadiationSuit==19 && EquipmentCatalog.PartNames.Length == 7 && EquipmentCatalog.SlotNames.Length == 5, "equipment catalog lengths changed");
            for (int i = 0; i < gears.Length; i++) Require((int)gears[i] == i, "gear ordinal changed: " + gears[i]);
            for (int i = 0; i < parts.Length; i++) Require((int)parts[i] == i, "part ordinal changed: " + parts[i]);
            for (int i = 0; i < slots.Length; i++) Require((int)slots[i] == i, "slot ordinal changed: " + slots[i]);
            foreach (var kind in gears)
            {
                var d = EquipmentCatalog.Gear(kind);
                Require(d.kind == kind && !string.IsNullOrWhiteSpace(d.name) && d.cost.Length == DemoCatalog.ItemCount && d.cost.Any(n => n > 0) && d.cost.All(n => n >= 0), "incomplete gear catalog: " + kind);
                d.cost[0] = -1; Require(EquipmentCatalog.Gear(kind).cost[0] >= 0, "gear catalog leaked its cost array");
            }
        });
        Run("equipment fresh players research tools before receiving equipment", () =>
        {
            var sim = New(out var p);
            Require(PrimitiveCatalog.Enabled(sim.State) && p.gear.Count == 0, "fresh world bypassed primitive tool research");
            Require(p.gear.All(g => g.kind < GearKind.Pistol && g.magazine == 0), "fresh player received a free modern gun");
            Require(p.gear.Select(g => g.id).Distinct().Count() == p.gear.Count && p.gear.All(g => g.id > 0), "starter gear identity");
        });
    }

    static void EquipmentCraftChecks()
    {
        foreach (var kind in Enum.GetValues<GearKind>())
            Run($"equipment craft {kind} charges declared materials once and respects technology", () =>
            {
                var sim = EquipmentFixture(out var p); var d = EquipmentCatalog.Gear(kind); int[] before = Copy(p.items);
                var made = CraftGear(sim, p, kind); Paid(p.items, before, d.cost, "gear crafting cost");
                Require(made.durability == d.maxDurability && made.magazine == 0 && made.attachments.Length == 5 && made.attachments.All(n => n == -1), "new gear is preloaded or malformed");
                Require(EquipmentCatalog.Find(p, made.id) == made, "crafted ID not retrievable");
                int missing = Array.FindIndex(d.cost, n => n > 0);
                var poor = EquipmentFixture(out var q, arrange: (_, player) => player.items[missing] = d.cost[missing] - 1);
                string economy = EquipmentEconomy(q); Require(!EquipmentCatalog.CanCraft(poor.State, q, kind), "underfunded gear is craftable");
                poor.Apply(Command(q, "gearcraft", arg: (int)kind)); Require(EquipmentEconomy(q) == economy, "failed gear craft changed inventory");
                if (d.era > TechEra.Primitive)
                {
                    var locked = EquipmentFixture(out var r, era: (TechEra)((int)d.era - 1)); economy = EquipmentEconomy(r);
                    Require(!EquipmentCatalog.CanCraft(locked.State, r, kind), "locked gear reported craftable");
                    locked.Apply(Command(r, "gearcraft", arg: (int)kind)); Require(EquipmentEconomy(r) == economy, "locked gear bypassed technology");
                }
            });
        foreach (var kind in Enum.GetValues<PartKind>())
            Run($"equipment partcraft {kind} charges materials and rejects locked or missing inputs", () =>
            {
                var sim = EquipmentFixture(out var p); var d = EquipmentCatalog.Part(kind); var before = Copy(p.items);
                Require(d.cost.Length == DemoCatalog.ItemCount && d.cost.Any(n => n > 0) && d.cost.All(n => n >= 0), "part has no material cost");
                CraftPart(sim, p, kind); Paid(p.items, before, d.cost, "part crafting cost");
                int missing = Array.FindIndex(d.cost, n => n > 0);
                foreach (bool shortMaterials in new[] { true, false })
                {
                    var reject = EquipmentFixture(out var q, era: shortMaterials ? TechEra.Nuclear : (TechEra)((int)d.era - 1),
                        arrange: (_, player) => { if (shortMaterials) player.items[missing] = d.cost[missing] - 1; });
                    string economy = EquipmentEconomy(q); Require(!EquipmentCatalog.CanCraftPart(reject.State, q, kind), "unavailable part reported craftable");
                    reject.Apply(Command(q, "partcraft", arg: (int)kind)); Require(EquipmentEconomy(q) == economy, "rejected part changed inventory");
                }
            });
        foreach (string reason in new[] { "absent", "distant", "wall", "node", "water", "dead", "disconnected" })
            Run($"equipment workbench commands reject {reason} without spending", () =>
            {
                var sim = EquipmentFixture(out var p, arrange: (state, player) =>
                {
                    if (reason == "absent") state.buildings.Clear();
                    if (reason == "distant") state.buildings[0].x = 29;
                    if (reason == "wall") Add(state, BuildKind.Wall, 31, 35);
                    if (reason == "node") Ore(state, 31, 35);
                    if (reason == "water") Water(state, 31, 35);
                    if (reason == "dead") player.hp = 0;
                    if (reason == "disconnected") player.connected = false;
                });
                string before = EquipmentEconomy(p); int nextId = sim.State.nextId;
                Require(!EquipmentCatalog.CanCraft(sim.State, p, GearKind.Knife) && !EquipmentCatalog.CanCraftPart(sim.State, p, PartKind.RedDot), "invalid workbench state is craftable");
                sim.Apply(Command(p, "gearcraft", arg: (int)GearKind.Knife)); sim.Apply(Command(p, "partcraft", arg: 0));
                Require(EquipmentEconomy(p) == before && sim.State.nextId == nextId, "denied workbench command spent or allocated gear");
            });
        Run("equipment malformed craft indices and full gear inventory cannot mint gear", () =>
        {
            foreach (int value in new[] { -1, 20, int.MaxValue })
            {
                var sim = EquipmentFixture(out var p); string before = EquipmentEconomy(p);
                sim.Apply(Command(p, "gearcraft", arg: value)); sim.Apply(Command(p, "partcraft", arg: value == 20 ? 7 : value));
                Require(EquipmentEconomy(p) == before, "invalid craft enum changed inventory");
            }
            var full = EquipmentFixture(out var q, arrange: (state, player) =>
            {
                for (int n = 0; n < 24; n++) player.gear.Add(new GearState { id = state.nextId++, kind = GearKind.Knife });
            });
            string snapshot = EquipmentEconomy(q); full.Apply(Command(q, "gearcraft", arg: 0));
            Require(q.gear.Count == 24 && EquipmentEconomy(q) == snapshot, "gear count cap was bypassed");
        });
    }

    static void EquipmentSaveChecks()
    {
        foreach (int version in new[] { 1, 2, 3 })
            RunHistorical($"equipment v{version} migration preserves real legacy slots and grants an empty basic pistol once", () =>
            {
                var source = version == 3 ? VersionThreeFixture() : LegacyFixture(version);
                Require(source.version == version && source.players.Single().items.Length == (version == 3 ? 21 : 7), "not a genuine legacy inventory fixture");
                if (version == 3) Require(source.produced.Length == 21 && source.buildings.All(b => b.items.Length == 21), "v3 input was secretly v4");
                string before = Dump(source); var restored = new WorldSimulator(source).State; var p = restored.players.Single();
                Require(restored.version == WorldSimulator.SaveVersion && p.items.Length == DemoCatalog.ItemCount && restored.produced.Length == DemoCatalog.ItemCount, "legacy equipment migration did not reach v4");
                Require(p.items.Take(source.players[0].items.Length).SequenceEqual(source.players[0].items) && p.items.Skip(source.players[0].items.Length).All(n => n == 0), "migration invented or lost reserve ammunition/material");
                if (version == 3) Require(restored.era == source.era && restored.produced.Take(21).SequenceEqual(source.produced) && restored.produced.Skip(21).All(n => n == 0), "v3 research/production was reset");
                Require(p.gear.Count(g => g.kind == GearKind.Pistol) == 1 && p.gear.All(g => g.magazine == 0) && p.gear.All(g => g.kind <= GearKind.Pistol), "legacy basic gun missing, preloaded, or upgraded for free");
                Require(EquipmentCatalog.Find(p, p.weaponId)?.kind == GearKind.Pistol, "legacy pistol is not equipped");
                var again = new WorldSimulator(Saved(restored)).State.players.Single();
                Require(again.gear.Count == p.gear.Count && again.items.SequenceEqual(p.items), "restoring v4 repeatedly duplicated legacy equipment");
                Require(Dump(source) == before, "migration mutated the original save graph");
            });
        Run("equipment v4 restore deep copies gear parts attachments and equipped state", () =>
        {
            var sim = EquipmentFixture(out var p, GearKind.AssaultRifle, arrange: (_, player) =>
            {
                var gear = player.gear.Single(); gear.durability = 73; gear.magazine = 4; gear.attachments[0] = (int)PartKind.Scope4x;
                player.parts[0] = 3; player.selectedAmmo = ItemKind.RifleAmmo;
            });
            var source = Saved(sim.State); var restored = new WorldSimulator(source).State; var a = source.players.Single(); var b = restored.players.Single();
            Require(b.weaponId == p.weaponId && b.activeSlot == 0 && b.gear.Single().magazine == 4 && b.gear.Single().durability == 73 && b.gear.Single().loadedAmmo == ItemKind.RifleAmmo, "v4 restore changed equipped gear");
            Require(!ReferenceEquals(a.gear,b.gear) && !ReferenceEquals(a.gear[0],b.gear[0]) && !ReferenceEquals(a.gear[0].attachments,b.gear[0].attachments) && !ReferenceEquals(a.parts,b.parts), "equipment restore aliases caller-owned storage");
            string before = Dump(restored); a.parts[0]++; a.gear[0].attachments[0] = -1; a.gear[0].durability--; a.gear.Clear();
            Require(Dump(restored) == before, "caller mutation crossed v4 equipment boundary");
        });
        var corruptions = new (string label, Action<PlayerState> change)[]
        {
            ("null gear list", p => p.gear = null), ("null gear", p => p.gear[0] = null),
            ("zero id", p => p.gear[0].id = 0), ("negative id", p => p.gear[0].id = -1),
            ("invalid kind", p => p.gear[0].kind = (GearKind)20),
            ("negative durability", p => p.gear[0].durability = -1), ("excess durability", p => p.gear[0].durability = 101),
            ("negative magazine", p => p.gear[0].magazine = -1), ("excess magazine", p => p.gear[0].magazine = 31),
            ("wrong caliber", p => { p.gear[0].magazine = 1; p.gear[0].loadedAmmo = ItemKind.Shell; }),
            ("invalid caliber", p => p.gear[0].loadedAmmo = (ItemKind)25),
            ("null attachments", p => p.gear[0].attachments = null), ("short attachments", p => p.gear[0].attachments = new[] { -1,-1,-1,-1 }),
            ("bad attachment enum", p => p.gear[0].attachments[0] = 7), ("negative attachment", p => p.gear[0].attachments[0] = -2),
            ("wrong attachment slot", p => p.gear[0].attachments[1] = (int)PartKind.RedDot),
            ("tool attachments", p => { p.gear[0].kind = GearKind.StoneAxe; p.gear[0].loadedAmmo = ItemKind.Ammo; p.gear[0].attachments[0] = (int)PartKind.RedDot; p.weaponId = 0; p.toolId = p.gear[0].id; p.activeSlot = 1; }),
            ("melee magazine", p => { p.gear[0].kind = GearKind.Knife; p.gear[0].magazine = 1; p.gear[0].loadedAmmo = ItemKind.Ammo; }),
            ("null parts", p => p.parts = null), ("short parts", p => p.parts = new int[6]), ("long parts", p => p.parts = new int[8]),
            ("negative parts", p => p.parts[0] = -1), ("excess parts", p => p.parts[0] = 1000),
            ("forged equipped id", p => p.weaponId = 987654), ("gun in tool slot", p => p.toolId = p.gear[0].id),
            ("negative active slot", p => p.activeSlot = -1), ("invalid active slot", p => p.activeSlot = 3),
            ("invalid selected ammo", p => p.selectedAmmo = ItemKind.Wood),
            ("NaN reload", p => p.reloadRemaining = float.NaN), ("negative reload", p => p.reloadRemaining = -.1f),
            ("forged reload id", p => { p.reloadRemaining = 1; p.reloadingGearId = 987654; }),
            ("NaN recoil", p => p.recoil = float.NaN), ("negative recoil", p => p.recoil = -.1f),
            ("duplicate gear id", p => p.gear.Add(new GearState { id = p.gear[0].id, kind = GearKind.Knife }))
        };
        foreach (var (label, change) in corruptions)
            Run("equipment v4 save rejects " + label, () =>
            {
                var sim = EquipmentFixture(out _, GearKind.AssaultRifle); var source = Saved(sim.State); change(source.players.Single());
                Reject(source, "v4 accepted " + label);
            });
        Run("equipment v4 save rejects globally reused ids and more than 24 gear", () =>
        {
            var sim = EquipmentFixture(out _, GearKind.AssaultRifle);
            var collision = Saved(sim.State); collision.players[0].gear[0].id = collision.buildings[0].id; collision.players[0].weaponId = collision.buildings[0].id;
            Reject(collision, "gear reused a building entity ID");
            var over = Saved(sim.State);
            for (int n = 0; n < 24; n++) over.players[0].gear.Add(new GearState { id = over.nextId++, kind = GearKind.Knife });
            Reject(over, "25 saved gear bypassed the cap");
            var foreign = Saved(sim.State); foreign.players.Add(new PlayerState { id = "other", seat = 1, x = 34, y = 35 });
            foreign.players[1].weaponId = foreign.players[0].weaponId; Reject(foreign, "equipped another player's saved gear");
        });
        Run("equipment v4 rejects 21-slot material arrays instead of migrating corrupt current saves", () =>
        {
            foreach (string field in new[] { "player", "machine", "production" })
            {
                var sim = EquipmentFixture(out _); var source = Saved(sim.State);
                if (field == "player") source.players[0].items = new int[21];
                if (field == "machine") source.buildings[0].items = new int[21];
                if (field == "production") source.produced = new int[21];
                Reject(source, "v4 silently migrated malformed " + field);
            }
        });
        Run("equipment v4 rejects stale next entity id instead of reusing saved gear identities",()=>
        {
            var sim=EquipmentFixture(out var p,GearKind.Pistol);var source=Saved(sim.State);source.nextId=p.gear[0].id;
            Reject(source,"v4 nextId collided with saved gear");
        });
    }

    static void EquipmentUseChecks()
    {
        Run("equipment equip and switch select owned weapon tool and unarmed slots", () =>
        {
            var sim = EquipmentFixture(out var p, GearKind.StoneAxe); int axe = p.toolId;
            var knife = CraftGear(sim, p, GearKind.Knife); sim.Apply(Command(p, "equip", arg: knife.id));
            Require(p.weaponId == knife.id && p.toolId == axe && EquipmentCatalog.Equipped(p) == knife, "equip lost the other slot or selected wrong gear");
            sim.Apply(Command(p, "switch", arg: 1)); Require(p.activeSlot == 1 && EquipmentCatalog.Equipped(p)?.id == axe, "tool switch");
            sim.Apply(Command(p, "switch", arg: 2)); Require(p.activeSlot == 2 && EquipmentCatalog.Equipped(p) == null, "unarmed switch");
            sim.Apply(Command(p, "switch", arg: 0)); Require(EquipmentCatalog.Equipped(p) == knife, "weapon switch");
        });
        Run("equipment invalid active slot commands preserve both independent equipment slots",()=>
        {
            foreach(int slot in new[] { -1,3,int.MaxValue })
            {
                var sim=EquipmentFixture(out var p,GearKind.Pistol);string before=EquipmentEconomy(p);
                sim.Apply(Command(p,"switch",arg:slot));Require(EquipmentEconomy(p)==before,"invalid active slot was accepted");
            }
        });
        Run("equipment commands reject forged and foreign gear identities", () =>
        {
            foreach (string action in new[] { "equip", "unequip", "unload", "gearrepair", "gearsalvage", "attach", "detach" })
            {
                var sim = EquipmentFixture(out var p, GearKind.Pistol, arrange: (state, _) =>
                {
                    var other = new PlayerState { id = "other", seat = 1, x = 34, y = 36 };
                    other.gear.Add(new GearState { id = state.nextId++, kind = GearKind.Pistol }); state.players.Add(other);
                });
                int id = sim.State.players[1].gear[0].id;
                string economy = EquipmentEconomy(p); var foreign = EquipmentEconomy(sim.State.players[1]);
                sim.Apply(Command(p, action, x: 0, arg: id));
                Require(EquipmentEconomy(p) == economy && EquipmentEconomy(sim.State.players[1]) == foreign, action + " accepted foreign gear");
                Advance(sim, .6f); sim.Apply(Command(p, action, x: 0, arg: int.MaxValue));
                Require(EquipmentEconomy(p) == economy, action + " accepted invented gear");
            }
        });
        foreach (var (kind, node, item, expected) in new[]
        {
            (GearKind.StoneAxe,NodeKind.Tree,ItemKind.Wood,12), (GearKind.SteelAxe,NodeKind.Tree,ItemKind.Wood,20),
            (GearKind.StonePickaxe,NodeKind.Ore,ItemKind.Ore,12), (GearKind.SteelPickaxe,NodeKind.Ore,ItemKind.Ore,20),
            (GearKind.StonePickaxe,NodeKind.CopperOre,ItemKind.CopperOre,12), (GearKind.SteelPickaxe,NodeKind.TinOre,ItemKind.TinOre,20),
            (GearKind.SteelPickaxe,NodeKind.UraniumOre,ItemKind.UraniumOre,20), (GearKind.Wrench,NodeKind.Scrap,ItemKind.Scrap,16),
            (GearKind.StoneAxe,NodeKind.Ore,ItemKind.Ore,0), (GearKind.StonePickaxe,NodeKind.Tree,ItemKind.Wood,0)
        })
            Run($"equipment {kind} harvests {node} only with the right tool and remaining durability", () =>
            {
                var sim = EquipmentFixture(out var p, kind, arrange: (state, player) =>
                {
                    player.gear[0].durability = 1; state.nodes.Add(new NodeState { id = state.nextId++, kind = node, x = 34, y = 35, stock = 40 });
                });
                int before = p.items[(int)item]; sim.Apply(Command(p, "interact", 34, 35));
                Require(p.items[(int)item] == before && sim.State.nodes.Single().stock == 40, "harvest paid before destruction");
                Near(sim.State.nodes.Single().hp, 30 - expected, "wrong node damage");
                int remaining = expected == 0 ? 1 : 0;
                Require(p.gear[0].durability == remaining, "wrong tool durability charge");
                Advance(sim, .6f); sim.Apply(Command(p, "interact", 34, 35));
                Near(sim.State.nodes.Single().hp, 30 - expected, "broken or mismatched tool damaged a node");
                Require(p.items[(int)item] == before && p.gear[0].durability == remaining, "rejected harvest charged durability or paid resources");
            });
        Run("equipment gearrepair spends material and restores only the owned damaged gear", () =>
        {
            var sim = EquipmentFixture(out var p, GearKind.StoneAxe, arrange: (_, player) => player.gear[0].durability = 0);
            var before = Copy(p.items); int id = p.gear[0].id; sim.Apply(Command(p, "gearrepair", arg: id));
            Require(p.gear[0].durability == 25, "repair did not restore 25 durability");
            before[(int)ItemKind.Wood]--; before[(int)ItemKind.Stone]--; Inventory(p.items, before, "stone gear repair");
            foreach (string condition in new[] { "materials", "workbench", "dead", "disconnected" })
            {
                var reject = EquipmentFixture(out var q, GearKind.Pistol, arrange: (state, player) =>
                {
                    player.gear[0].durability = 50;
                    if (condition == "materials") Array.Clear(player.items);
                    if (condition == "workbench") state.buildings.Clear();
                    if (condition == "dead") player.hp = 0;
                    if (condition == "disconnected") player.connected = false;
                });
                string economy = EquipmentEconomy(q); reject.Apply(Command(q, "gearrepair", arg: q.gear[0].id));
                Require(EquipmentEconomy(q) == economy, "repair bypassed " + condition);
            }
        });
    }

    static void EquipmentReloadChecks()
    {
        foreach (var kind in new[] { GearKind.Pistol, GearKind.Shotgun, GearKind.AssaultRifle, GearKind.PrecisionRifle })
            Run($"equipment {kind} reload charges reserve only at completion and firing consumes magazine", () =>
            {
                var sim = EquipmentFixture(out var p, kind); var g = p.gear.Single(); var d = EquipmentCatalog.Gear(kind);
                sim.Apply(Command(p, "ammoselect", arg: (int)d.ammo)); int reserve = p.items[(int)d.ammo];
                sim.Apply(Command(p, "reload")); Require(p.reloadRemaining > 0 && p.reloadingGearId == g.id && g.magazine == 0 && p.items[(int)d.ammo] == reserve, "reload start charged ammunition");
                Advance(sim, d.reload - .1f); Require(g.magazine == 0 && p.items[(int)d.ammo] == reserve, "partial reload charged reserve or filled magazine");
                Advance(sim, .2f); Require(g.magazine == d.magazine && p.items[(int)d.ammo] == reserve - d.magazine && p.reloadingGearId == 0, "reload completion did not transfer exactly one magazine");
                FireEquipment(sim, p); Require(g.magazine == d.magazine - 1 && p.items[(int)d.ammo] == reserve - d.magazine, "shot did not spend one loaded round");
            });
        foreach (string cancellation in new[] { "switch", "disconnect", "death", "ammo change" })
            Run("equipment unfinished reload cancellation by " + cancellation + " cannot duplicate ammunition", () =>
            {
                var sim = EquipmentFixture(out var p, GearKind.AssaultRifle, arrange: (state, player) =>
                {
                    player.selectedAmmo = ItemKind.RifleAmmo;
                    if (cancellation == "death") { player.hp = 1; EquipmentTarget(state, 33); }
                });
                var g = p.gear.Single(); int reserve = p.items[(int)ItemKind.RifleAmmo];
                sim.Apply(Command(p, "reload")); Require(p.reloadRemaining > 0, "cancel fixture never started reload");
                if (cancellation == "switch") sim.Apply(Command(p, "switch", arg: 2));
                if (cancellation == "disconnect") sim.SetConnected(p.id, false);
                if (cancellation == "ammo change") sim.Apply(Command(p, "ammoselect", arg: (int)ItemKind.ArmorPiercingAmmo));
                Advance(sim, 3); Require(p.reloadRemaining == 0 && p.reloadingGearId == 0 && g.magazine == 0 && p.items[(int)ItemKind.RifleAmmo] == reserve, "cancellation changed total ammunition");
                if (cancellation == "death") Require(p.hp == 0, "death cancellation fixture did not die");
            });
        Run("equipment in-progress reload survives save without charging twice", () =>
        {
            var sim = EquipmentFixture(out var p, GearKind.Pistol); sim.Apply(Command(p, "reload")); Advance(sim, .3f);
            int total = p.items[(int)ItemKind.Ammo]; var restored = new WorldSimulator(Saved(sim.State));
            Advance(sim, 3); Advance(restored, 3); var q = restored.State.players.Single();
            Require(p.gear[0].magazine == 17 && q.gear[0].magazine == 17 && p.items[(int)ItemKind.Ammo] == total - 17 && q.items[(int)ItemKind.Ammo] == total - 17, "mid-reload save invented or lost rounds");
        });
        Run("equipment legacy pistol 12 and extended 18 round saves preserve ammo and reload only the deficit", () =>
        {
            foreach (var (savedRounds, capacity, deficit) in new[] { (12, 17, 5), (18, 26, 8) })
                foreach (int reserve in new[] { 300, 3, 0 })
                {
                    var sim = EquipmentFixture(out _, GearKind.Pistol, arrange: (_, player) =>
                    {
                        player.gear[0].magazine = savedRounds; player.items[(int)ItemKind.Ammo] = reserve;
                        if (savedRounds == 18) player.gear[0].attachments[(int)AttachmentSlot.Magazine] = (int)PartKind.ExtendedMag;
                    });
                    var restored = new WorldSimulator(Saved(sim.State)); var p = restored.State.players.Single(); var g = p.gear.Single();
                    Require(g.magazine == savedRounds && p.items[(int)ItemKind.Ammo] == reserve, "restoring a legacy pistol changed ammunition");
                    Advance(restored, .3f);
                    Require(g.magazine == savedRounds && p.items[(int)ItemKind.Ammo] == reserve && p.reloadRemaining == 0, "legacy pistol refilled without an explicit reload");
                    restored.Apply(Command(p, "reload"));
                    Require(g.magazine == savedRounds && p.items[(int)ItemKind.Ammo] == reserve && (p.reloadRemaining > 0) == (reserve > 0), "legacy pistol reload did not start correctly or charged early");
                    Advance(restored, 3); int transferred = Math.Min(reserve, deficit);
                    Require(EquipmentCatalog.Stats(g).magazine == capacity && g.magazine == savedRounds + transferred &&
                        p.items[(int)ItemKind.Ammo] == reserve - transferred && p.items[(int)ItemKind.Ammo] + g.magazine == reserve + savedRounds &&
                        p.reloadRemaining == 0 && p.reloadingGearId == 0, "legacy pistol reload did not conserve ammunition or fill only the deficit");
                }
        });
        Run("equipment incompatible caliber and empty reserves reject without gifting melee", () =>
        {
            foreach(int reserve in new[] { 0,300 })
            {
                var sim = EquipmentFixture(out var p, GearKind.Pistol, arrange: (state, player) => { player.items[(int)ItemKind.Ammo] = reserve; EquipmentTarget(state, 33); });
                var g = p.gear[0]; string before = EquipmentEconomy(p);
                sim.Apply(Command(p, "ammoselect", arg: (int)ItemKind.RifleAmmo)); Require(p.selectedAmmo == ItemKind.Ammo, "pistol accepted rifle caliber");
                if(reserve==0){sim.Apply(Command(p, "reload")); Require(p.reloadRemaining == 0 && g.magazine == 0, "empty reserve filled gun");}
                FireEquipment(sim, p); Require(sim.State.zombies.Single().hp == 1000 && EquipmentEconomy(p) == before, "empty gun gave free melee or reserve-only shooting");
            }
        });
        Run("equipment ammo selection rejects every incompatible caliber through Apply",()=>
        {
            foreach(var kind in new[] { GearKind.Pistol,GearKind.Shotgun,GearKind.AssaultRifle,GearKind.PrecisionRifle })
                foreach(var ammo in new[] { ItemKind.Ammo,ItemKind.RifleAmmo,ItemKind.Shell,ItemKind.PrecisionAmmo,ItemKind.ArmorPiercingAmmo })
                {
                    if(EquipmentCatalog.AmmoCompatible(kind,ammo))continue;
                    var sim=EquipmentFixture(out var p,kind,arrange:(_,player)=>player.selectedAmmo=EquipmentCatalog.Gear(kind).ammo);
                    string before=EquipmentEconomy(p);sim.Apply(Command(p,"ammoselect",arg:(int)ammo));
                    Require(EquipmentEconomy(p)==before,$"Apply accepted incompatible {kind}/{ammo}");
                }
        });
        Run("equipment unload is lossless and full reserve rejects the whole transfer", () =>
        {
            foreach (int reserve in new[] { 300, 997, 999 })
            {
                var sim = EquipmentFixture(out var p, GearKind.Pistol, arrange: (_, player) => { player.items[(int)ItemKind.Ammo] = reserve; player.gear[0].magazine = 5; });
                sim.Apply(Command(p, "unload", arg: p.gear[0].id));
                bool full=reserve+5>999;
                Require(p.gear[0].magazine == (full ? 5 : 0) && p.items[(int)ItemKind.Ammo] == (full ? reserve : 305), "unload lost ammunition at capacity boundary");
            }
        });
    }

    static void EquipmentAttachmentChecks()
    {
        foreach (var kind in new[] { GearKind.Pistol, GearKind.Shotgun, GearKind.AssaultRifle, GearKind.PrecisionRifle })
            foreach (var part in Enum.GetValues<PartKind>())
                Run($"equipment {kind} attachment {part} enforces compatibility and conserves parts", () =>
                {
                    bool compatible = part == PartKind.Scope4x || part == PartKind.MatchBarrel ? kind == GearKind.AssaultRifle || kind == GearKind.PrecisionRifle :
                        part == PartKind.Suppressor ? kind != GearKind.Shotgun : part == PartKind.ErgonomicGrip ? kind != GearKind.Pistol : true;
                    var sim = EquipmentFixture(out var p, kind, arrange: (_, player) => player.parts[(int)part] = 1);
                    var g = p.gear[0]; int slot = (int)EquipmentCatalog.Part(part).slot; var before = Copy(p.items);
                    sim.Apply(Command(p, "attach", x: (int)part, arg: g.id));
                    Require(g.attachments[slot] == (compatible ? (int)part : -1) && p.parts[(int)part] == (compatible ? 0 : 1), "attachment compatibility/consumption");
                    if (compatible)
                    {
                        Advance(sim, .3f); sim.Apply(Command(p, "detach", x: slot, arg: g.id));
                        Require(g.attachments[slot] == -1 && p.parts[(int)part] == 1, "detach did not return original part");
                    }
                    Inventory(p.items, before, "attachment operations silently charged crafting materials");
                });
        Run("equipment occupied slots reject replacement and detached full inventories retain the part", () =>
        {
            var sim = EquipmentFixture(out var p, GearKind.AssaultRifle, arrange: (_, player) => { player.parts[(int)PartKind.Scope4x] = 1; player.parts[(int)PartKind.RedDot] = 999; player.gear[0].attachments[0] = (int)PartKind.RedDot; });
            string before = EquipmentEconomy(p); sim.Apply(Command(p, "attach", x: (int)PartKind.Scope4x, arg: p.gear[0].id));
            Require(EquipmentEconomy(p) == before, "occupied optic slot discarded its old part");
            Advance(sim, .3f); sim.Apply(Command(p, "detach", x: 0, arg: p.gear[0].id));
            Require(EquipmentEconomy(p) == before, "full part reserve lost the installed optic");
        });
        Run("equipment extended magazine capacity changes cannot delete excess rounds", () =>
        {
            var sim = EquipmentFixture(out var p, GearKind.Pistol); var g = p.gear[0];
            InstallPart(sim, p, g, PartKind.ExtendedMag); Require(EquipmentCatalog.Stats(g).magazine == 26, "pistol extended magazine must hold 26");
            LoadEquipment(sim, p, g); Require(g.magazine == 26, "reload ignored extended capacity");
            sim.Apply(Command(p, "detach", x: (int)AttachmentSlot.Magazine, arg: g.id));
            Require(g.magazine == 26 && g.attachments[2] == (int)PartKind.ExtendedMag && p.parts[(int)PartKind.ExtendedMag] == 0, "removing a loaded extended magazine lost excess rounds");
            Advance(sim, .3f); int total = p.items[(int)ItemKind.Ammo] + g.magazine; sim.Apply(Command(p, "unload", arg: g.id));
            Advance(sim, .3f); sim.Apply(Command(p, "detach", x: 2, arg: g.id));
            Require(EquipmentCatalog.Stats(g).magazine == 17 && p.parts[(int)PartKind.ExtendedMag] == 1 && p.items[(int)ItemKind.Ammo] + g.magazine == total, "unload/detach did not preserve ammo and part");
        });
    }

    static void EquipmentCombatChecks()
    {
        Run("equipment caliber compatibility matrix matches all five ammunition types", () =>
        {
            var valid = new HashSet<(GearKind,ItemKind)>
            {
                (GearKind.Pistol,ItemKind.Ammo), (GearKind.Shotgun,ItemKind.Shell),
                (GearKind.AssaultRifle,ItemKind.RifleAmmo), (GearKind.AssaultRifle,ItemKind.ArmorPiercingAmmo),
                (GearKind.PrecisionRifle,ItemKind.PrecisionAmmo), (GearKind.PrecisionRifle,ItemKind.ArmorPiercingAmmo)
            };
            foreach (var gear in Enum.GetValues<GearKind>()) foreach (var ammo in Enum.GetValues<ItemKind>())
                Require(EquipmentCatalog.AmmoCompatible(gear,ammo) == valid.Contains((gear,ammo)), $"wrong caliber compatibility: {gear}/{ammo}");
        });
        foreach (var (kind, damage) in new[] { (GearKind.Pistol,24f), (GearKind.AssaultRifle,22f), (GearKind.PrecisionRifle,86f) })
            Run($"equipment {kind} actual hit uses its declared damage and wears the gun", () =>
            {
                var sim = EquipmentFixture(out var p, kind, arrange: (state, player) => { player.gear[0].magazine = 3; EquipmentTarget(state,34); });
                FireEquipment(sim,p); Near(sim.State.zombies.Single().hp,1000-damage,"actual gear damage");
                Require(p.gear[0].magazine == 2 && p.gear[0].durability == 99,"shot did not consume one round and one durability");
            });
        Run("equipment shotgun emits seven damaging pellets but consumes one shell", () =>
        {
            var sim = EquipmentFixture(out var p,GearKind.Shotgun,arrange:(state,player)=>{player.gear[0].magazine=3;EquipmentTarget(state,33.2f);});
            int reserve=p.items[(int)ItemKind.Shell];FireEquipment(sim,p);
            Require(sim.State.traces.Count==7 && p.gear[0].magazine==2 && p.items[(int)ItemKind.Shell]==reserve,"pellets spent multiple shells or missed traces");
            Near(sim.State.zombies.Single().hp,916,"seven pellets must each deal 12 damage");
        });
        Run("equipment match barrel and suppressor modify actual inflicted damage", () =>
        {
            foreach(var (part,expected) in new[] { (PartKind.MatchBarrel,24.64f),(PartKind.Suppressor,20.9f) })
            {
                var sim=EquipmentFixture(out var p,GearKind.AssaultRifle,arrange:(state,player)=>
                {
                    player.gear[0].magazine=3;player.gear[0].attachments[(int)EquipmentCatalog.Part(part).slot]=(int)part;EquipmentTarget(state,34);
                });
                FireEquipment(sim,p);Near(sim.State.zombies.Single().hp,1000-expected,"attachment changed catalog but not damage: "+part);
            }
        });
        Run("equipment scope and barrel extend real projectile reach beyond base precision range", () =>
        {
            foreach(PartKind? part in new PartKind?[] { null,PartKind.Scope4x,PartKind.MatchBarrel })
            {
                var sim=EquipmentFixture(out var p,GearKind.PrecisionRifle,arrange:(state,player)=>
                {
                    player.gear[0].magazine=3;if(part.HasValue)player.gear[0].attachments[(int)EquipmentCatalog.Part(part.Value).slot]=(int)part.Value;EquipmentTarget(state,58);
                });
                FireEquipment(sim,p);Require((sim.State.zombies.Single().hp<1000)==part.HasValue,"actual precision range ignored attachment: "+part);
            }
        });
        Run("equipment assault rifle interval permits a second shot before pistol cooldown", () =>
        {
            foreach(var kind in new[] { GearKind.Pistol,GearKind.AssaultRifle })
            {
                var sim=EquipmentFixture(out var p,kind,arrange:(_,player)=>player.gear[0].magazine=3);
                FireEquipment(sim,p);Advance(sim,.15f);FireEquipment(sim,p);
                Require(p.gear[0].magazine==(kind==GearKind.AssaultRifle?1:2),"actual firing ignored weapon interval");
            }
        });
        Run("equipment attack specialization and perception strength agility preserve real combat bonuses", () =>
        {
            var gun=EquipmentFixture(out var p,GearKind.Pistol,arrange:(state,player)=>
            {
                player.level=5;player.attributePoints=0;player.skillPoints=0;player.attributes[3]=2;player.skills[1]=2;player.gear[0].magazine=3;EquipmentTarget(state,34);
            });
            FireEquipment(gun,p);Near(gun.State.zombies.Single().hp,961.376f,"perception plus combat specialization damage");
            Advance(gun,.3f);FireEquipment(gun,p);Require(p.gear[0].magazine==1,"combat specialization lost its attack speed");
            var melee=EquipmentFixture(out var q,GearKind.Knife,arrange:(state,player)=>
            {
                player.level=5;player.attributePoints=0;player.skillPoints=0;player.attributes[0]=2;player.skills[1]=1;EquipmentTarget(state,33);
            });
            FireEquipment(melee,q);Near(melee.State.zombies.Single().hp,962.24f,"strength plus combat specialization melee damage");
            var quick=EquipmentFixture(out var r,GearKind.Pistol,arrange:(_,player)=>{player.level=3;player.attributePoints=0;player.attributes[2]=4;player.gear[0].magazine=3;});
            FireEquipment(quick,r);Advance(quick,.3f);FireEquipment(quick,r);Require(r.gear[0].magazine==1,"agility lost its attack speed");
        });
        Run("equipment perception extends actual precision rifle hit range",()=>
        {
            foreach(int perception in new[] { 0,8 })
            {
                var sim=EquipmentFixture(out var p,GearKind.PrecisionRifle,arrange:(state,player)=>
                {
                    player.level=5;player.attributePoints=0;player.attributes[3]=perception;player.gear[0].magazine=3;EquipmentTarget(state,58);
                });
                FireEquipment(sim,p);Require((sim.State.zombies[0].hp<1000)==(perception>0),"actual range lost perception bonus");
            }
        });
        Run("equipment combat specialization tier three preserves three-target melee cleave",()=>
        {
            foreach(int rank in new[] { 0,3 })
            {
                var sim=EquipmentFixture(out var p,GearKind.Knife,arrange:(state,player)=>
                {
                    player.level=5;player.skillPoints=0;player.skills[1]=rank;EquipmentTarget(state,33,35);EquipmentTarget(state,33,35.2f);EquipmentTarget(state,33,34.8f);
                });
                FireEquipment(sim,p);Require(sim.State.zombies.Count(z=>z.hp<1000)==(rank==3?3:1),"melee cleave specialization was lost");
            }
        });
        foreach(string modifier in new[] { "optic","aiming","movement","perception" })
            Run("equipment "+modifier+" changes real deterministic shot spread",()=>
            {
                double baseAngles=0,modifiedAngles=0;
                for(int seed=1;seed<=12;seed++)
                {
                    var baseline=EquipmentFixture(out var p,GearKind.AssaultRifle,arrange:(state,player)=>{state.seed=seed;player.gear[0].magazine=3;});
                    var changed=EquipmentFixture(out var q,GearKind.AssaultRifle,arrange:(state,player)=>
                    {
                        state.seed=seed;player.gear[0].magazine=3;
                        if(modifier=="optic")player.gear[0].attachments[0]=(int)PartKind.Scope4x;
                        if(modifier=="perception"){player.level=5;player.attributePoints=0;player.attributes[3]=8;}
                    });
                    baseline.Apply(new PlayerCommand{playerId=p.id,action="move"});
                    changed.Apply(new PlayerCommand{playerId=q.id,action="move",moveX=modifier=="movement"?1:0});
                    Advance(baseline,.05f);Advance(changed,.05f);
                    FireEquipment(baseline,p);FireEquipment(changed,q,aiming:modifier=="aiming");
                    var a=baseline.State.traces.Single();var b=changed.State.traces.Single();
                    baseAngles+=Math.Abs(Math.Atan2(a.endY-a.y,a.endX-a.x));modifiedAngles+=Math.Abs(Math.Atan2(b.endY-b.y,b.endX-b.x));
                }
                Require(baseAngles>.001,"spread control produced no measurable angular offset");
                Require(modifier=="movement"?modifiedAngles>baseAngles*1.5:modifiedAngles<baseAngles*.8,"actual shot spread ignored "+modifier);
            });
        Run("equipment recoil builds on firing and brake reduces actual recoil before recovery",()=>
        {
            float[] values=new float[2];
            for(int n=0;n<2;n++)
            {
                int which=n;var sim=EquipmentFixture(out var p,GearKind.AssaultRifle,arrange:(_,player)=>{player.gear[0].magazine=3;if(which==1)player.gear[0].attachments[1]=(int)PartKind.MuzzleBrake;});
                float before=EquipmentCatalog.EffectiveSpread(p,p.gear[0]);FireEquipment(sim,p);values[n]=p.recoil;
                Require(p.recoil>0 && EquipmentCatalog.EffectiveSpread(p,p.gear[0])>before,"shots generated no recoil/spread");
                Advance(sim,1);Require(p.recoil<values[n],"recoil never recovered");
            }
            Require(values[1]<values[0]*.7f,"installed brake did not affect actual recoil");
        });
        foreach(string obstruction in new[] { "none","wall","node","water" })
            Run("equipment armor piercing hits aligned targets but respects "+obstruction+" obstruction",()=>
            {
                var sim=EquipmentFixture(out var p,GearKind.AssaultRifle,arrange:(state,player)=>
                {
                    player.gear[0].magazine=3;player.gear[0].loadedAmmo=ItemKind.ArmorPiercingAmmo;player.selectedAmmo=ItemKind.ArmorPiercingAmmo;
                    EquipmentTarget(state,34);EquipmentTarget(state,37);
                    if(obstruction=="wall")Add(state,BuildKind.Wall,35,35);
                    if(obstruction=="node")Ore(state,35,35);if(obstruction=="water")Water(state,35,35);
                });
                FireEquipment(sim,p);var targets=sim.State.zombies.OrderBy(z=>z.x).ToArray();
                Require(targets[0].hp<1000,"AP never hit front target");
                Require((targets[1].hp<1000)==(obstruction=="none"),"AP penetration ignored targets or obstacle");
                Require(p.gear[0].magazine==2,"AP spent more than one round");
            });
        Run("equipment ordinary rifle round stops at the first target while precision reaches two",()=>
        {
            foreach(var kind in new[] { GearKind.AssaultRifle,GearKind.PrecisionRifle })
            {
                var sim=EquipmentFixture(out var p,kind,arrange:(state,player)=>{player.gear[0].magazine=3;EquipmentTarget(state,34);EquipmentTarget(state,37);});
                FireEquipment(sim,p);Require(sim.State.zombies[0].hp<1000 && (sim.State.zombies[1].hp<1000)==(kind==GearKind.PrecisionRifle),"ordinary projectile penetration identity");
            }
        });
        Run("equipment broken firearms cannot fire or reload and unarmed needs no ammunition",()=>
        {
            var sim=EquipmentFixture(out var p,GearKind.Pistol,arrange:(state,player)=>{player.gear[0].durability=0;player.gear[0].magazine=3;EquipmentTarget(state,33);});
            FireEquipment(sim,p);sim.Apply(Command(p,"reload"));Require(p.gear[0].magazine==3 && p.reloadRemaining==0 && sim.State.zombies[0].hp==1000,"broken gun remains functional");
            sim.Apply(Command(p,"switch",arg:2));FireEquipment(sim,p);Near(sim.State.zombies[0].hp,981,"unarmed attack");
        });
        Run("equipment legacy turret still consumes old ammunition and combat specialization applies",()=>
        {
            var state=Flat();var turret=Add(state,BuildKind.Turret,10,10);turret.items[(int)ItemKind.Ammo]=5;
            Add(state,BuildKind.Generator,10,7).fuel=10;EquipmentTarget(state,12,10);
            state.players.Add(new PlayerState{id="turret-owner",x=12,y=12,level=3,attributePoints=0,skillPoints=0,skills=new[]{0,1,0}});
            var sim=new WorldSimulator(state);Advance(sim,.05f);
            Require(Building(sim,turret.id).items[(int)ItemKind.Ammo]==4 && sim.State.zombies.Single().hp<974,"turret no longer consumes old ammo or lost specialization bonus");
        });
    }

    static void EquipmentSalvageChecks()
    {
        foreach(string capacity in new[] { "materials","parts","ammunition" })
            Run("equipment gearsalvage rejects full "+capacity+" atomically without losing equipment",()=>
            {
                var sim=EquipmentFixture(out var p,GearKind.Pistol,arrange:(_,player)=>
                {
                    player.gear[0].magazine=5;player.gear[0].attachments[0]=(int)PartKind.RedDot;
                    if(capacity=="materials")player.items[(int)ItemKind.Steel]=999;
                    if(capacity=="parts")player.parts[(int)PartKind.RedDot]=999;
                    if(capacity=="ammunition")player.items[(int)ItemKind.Ammo]=999;
                });
                string before=EquipmentEconomy(p);int xp=p.xp;sim.Apply(Command(p,"gearsalvage",arg:p.gear[0].id));
                Require(EquipmentEconomy(p)==before && p.xp==xp,"failed salvage lost material/part/magazine or changed slots");
            });
        Run("equipment gearsalvage returns half materials all parts and magazine and releases gear capacity",()=>
        {
            var sim=EquipmentFixture(out var p,GearKind.Pistol,arrange:(state,player)=>
            {
                player.gear[0].magazine=5;player.gear[0].attachments[0]=(int)PartKind.RedDot;
                for(int n=1;n<24;n++)player.gear.Add(new GearState{id=state.nextId++,kind=GearKind.Knife});
                player.reloadRemaining=1;player.reloadingGearId=player.gear[0].id;
            });
            int id=p.gear[0].id,xp=p.xp;var expected=Copy(p.items);expected[(int)ItemKind.Steel]+=2;expected[(int)ItemKind.Copper]++;expected[(int)ItemKind.Scrap]+=2;expected[(int)ItemKind.Ammo]+=5;
            sim.Apply(Command(p,"gearsalvage",arg:id));
            Require(p.gear.Count==23 && EquipmentCatalog.Find(p,id)==null && p.weaponId==0 && EquipmentCatalog.Equipped(p)==null,"salvage retained gear ID/slot");
            Inventory(p.items,expected,"salvage material/ammo refund");Require(p.parts[0]==1 && p.reloadRemaining==0 && p.reloadingGearId==0 && p.xp==xp,"salvage lost part, reload state, or granted XP");
            CraftGear(sim,p,GearKind.StoneAxe);Require(p.gear.Count==24,"salvage did not release a usable gear capacity slot");
        });
        Run("equipment salvage attach detach and repair reject inaccessible workbenches and invalid actors",()=>
        {
            foreach(string action in new[] { "gearsalvage","attach","detach","gearrepair" })
                foreach(string condition in new[] { "absent","far","wall","dead","disconnected" })
                {
                    var sim=EquipmentFixture(out var p,GearKind.Pistol,arrange:(state,player)=>
                    {
                        player.gear[0].magazine=5;player.gear[0].durability=50;player.gear[0].attachments[1]=(int)PartKind.MuzzleBrake;player.parts[(int)PartKind.RedDot]=1;
                        if(condition=="absent")state.buildings.Clear();if(condition=="far")state.buildings[0].x=29;
                        if(condition=="wall")Add(state,BuildKind.Wall,31,35);if(condition=="dead")player.hp=0;if(condition=="disconnected")player.connected=false;
                    });
                    string before=EquipmentEconomy(p);sim.Apply(Command(p,action,x:action=="detach"?1:0,arg:p.gear[0].id));
                    Require(EquipmentEconomy(p)==before,action+" bypassed "+condition);
                }
        });
    }

    static void EquipmentWrenchChecks()
    {
        Run("equipment wrench repair gains twenty HP and loses the bonus after last durability",()=>
        {
            var sim=EquipmentFixture(out var p,GearKind.Wrench,arrange:(state,player)=>
            {
                player.gear[0].durability=1;Add(state,BuildKind.Generator,34,34).hp=50;
            });
            var building=sim.State.buildings.Single(b=>b.kind==BuildKind.Generator);int scrap=p.items[(int)ItemKind.Scrap];
            sim.Apply(Command(p,"repair",34,35));Near(building.hp,102,"active wrench repair");
            Require(p.gear[0].durability==0 && p.items[(int)ItemKind.Scrap]==scrap-1,"successful wrench repair cost");
            Advance(sim,.3f);sim.Apply(Command(p,"repair",34,35));Near(building.hp,134,"broken wrench must use old repair formula");
            Require(p.gear[0].durability==0 && p.items[(int)ItemKind.Scrap]==scrap-2,"broken wrench durability or repair material");
        });
        foreach(var (rank,plainRefund,wrenchRefund) in new[] { (0,3,4),(2,4,6),(3,6,6) })
            Run($"equipment wrench dismantle improves engineering rank {rank} refund and caps at full cost",()=>
            {
                foreach(bool equipped in new[] { false,true })
                {
                    var sim=EquipmentFixture(out var p,GearKind.Wrench,arrange:(state,player)=>
                    {
                        Add(state,BuildKind.Workbench,34,35);player.activeSlot=equipped?1:2;player.level=5;player.attributePoints=0;player.skillPoints=0;player.skills[0]=rank;
                    });
                    int wood=p.items[(int)ItemKind.Wood];sim.Apply(Command(p,"dismantle",34,35));
                    Require(!sim.State.buildings.Any(b=>b.x==34 && b.y==35),"dismantle fixture was not removed");
                    Require(p.items[(int)ItemKind.Wood]==wood+(equipped?wrenchRefund:plainRefund),"wrench ignored skill refund/cap");
                    Require(p.gear[0].durability==(equipped?99:100),"wrench durability changed while inactive or failed to wear");
                }
            });
        Run("equipment wrench dismantle scales refund by building health and broken wrench keeps old rate",()=>
        {
            foreach(int durability in new[] { 0,100 })
            {
                var sim=EquipmentFixture(out var p,GearKind.Wrench,arrange:(state,player)=>
                {
                    var b=Add(state,BuildKind.Workbench,34,35);b.hp=WorldSimulator.BuildingMaxHealth(b.kind)*.5f;player.gear[0].durability=durability;
                });
                int wood=p.items[(int)ItemKind.Wood];sim.Apply(Command(p,"dismantle",34,35));
                Require(p.items[(int)ItemKind.Wood]==wood+(durability==0?1:2),"damaged building refund ignored health or broken wrench");
            }
        });
        Run("equipment failed wrench repair and full-backpack dismantle spend no durability",()=>
        {
            foreach(string action in new[] { "repair","dismantle" })
            {
                var sim=EquipmentFixture(out var p,GearKind.Wrench,arrange:(state,player)=>
                {
                    var b=Add(state,BuildKind.Workbench,34,35);
                    if(action=="repair"){b.hp=20;player.items[(int)ItemKind.Scrap]=0;}
                    else player.items[(int)ItemKind.Wood]=999;
                });
                string before=EquipmentEconomy(p);var target=sim.State.buildings.Single(b=>b.x==34);float hp=target.hp;
                sim.Apply(Command(p,action,34,35));
                Require(EquipmentEconomy(p)==before && sim.State.buildings.Contains(target) && target.hp==hp,"failed wrench action consumed durability/material or building");
            }
        });
    }
}
