using DeadCircuit;
using System.Text.Json;

static partial class Checks
{
    static void EnableAmmoResearchForRecipe(WorldState state, RecipeDefinition recipe)
    {
        if(state==null || recipe==null) return;
        bool basic=false, precision=false, armorPiercing=false;
        for(int item=0;item<recipe.outputs.Length;item++)
        {
            if(recipe.outputs[item]<=0) continue;
            switch((ItemKind)item)
            {
                case ItemKind.Ammo: case ItemKind.RifleAmmo: case ItemKind.Shell: basic=true; break;
                case ItemKind.PrecisionAmmo: precision=true; break;
                case ItemKind.ArmorPiercingAmmo: armorPiercing=true; break;
            }
        }
        if(!basic && !precision && !armorPiercing) return;
        state.weaponResearch |= 1 << (int)WeaponTech.BasicAmmunition;
        state.produced[(int)ItemKind.Steel]=Math.Max(state.produced[(int)ItemKind.Steel],4);
        state.produced[(int)ItemKind.Copper]=Math.Max(state.produced[(int)ItemKind.Copper],4);
        if(precision || armorPiercing)
        {
            state.weaponResearch |= 1 << (int)WeaponTech.PrecisionAmmunition;
            state.produced[(int)ItemKind.Steel]=Math.Max(state.produced[(int)ItemKind.Steel],8);
            state.produced[(int)ItemKind.Lubricant]=Math.Max(state.produced[(int)ItemKind.Lubricant],2);
        }
        if(armorPiercing)
        {
            state.weaponResearch |= 1 << (int)WeaponTech.ArmorPiercing;
            state.produced[(int)ItemKind.HeatAlloy]=Math.Max(state.produced[(int)ItemKind.HeatAlloy],2);
        }
    }

    // Input fixtures supply the earlier industrial era explicitly. Equipment
    // research itself always starts at zero and is earned by public commands.
    static void EquipmentIndustrialInputs(WorldState state)
    {
        state.primitiveVersion = 1; state.primitiveResearch = 255;
        if (state.era >= TechEra.Water)
        {
            state.waterVersion = 1; state.waterResearch = 255;
            state.waterCrushed = 4; state.waterSmelted = 6; state.waterPressed = 4;
            state.waterGears = state.waterBearings = 2; state.waterTransported = 4;
        }
        foreach (var (item, amount) in new[]
        {
            (ItemKind.Bronze,4), (ItemKind.Iron,6), (ItemKind.Brick,4), (ItemKind.Charcoal,2),
            (ItemKind.Steel,8), (ItemKind.SteelPlate,2), (ItemKind.Copper,4),
            (ItemKind.Gear,2), (ItemKind.Bearing,2), (ItemKind.Polymer,4),
            (ItemKind.Circuit,2), (ItemKind.Lubricant,2), (ItemKind.PrecisionPart,2),
            (ItemKind.AdvancedCircuit,2), (ItemKind.HeatAlloy,2)
        }) state.produced[(int)item] = Math.Max(state.produced[(int)item], amount);
    }

    static WorldSimulator EquipmentTechFixture(out PlayerState player, TechEra era = TechEra.HighVoltage,
        Action<WorldState, PlayerState> arrange = null) => EquipmentFixture(out player, era: era, arrange: (state, p) =>
        {
            state.weaponResearch=0; state.toolResearch=0;
            EquipmentIndustrialInputs(state);
            arrange?.Invoke(state, p);
        });

    static readonly (ItemKind item, int amount)[][] WeaponResearchSamples =
    {
        new[] {(ItemKind.Steel,2),(ItemKind.Copper,2)},
        new[] {(ItemKind.Steel,2),(ItemKind.Copper,1)},
        new[] {(ItemKind.Steel,3),(ItemKind.Wood,2)},
        new[] {(ItemKind.Steel,2),(ItemKind.Copper,1)},
        new[] {(ItemKind.Circuit,1),(ItemKind.Copper,1)},
        new[] {(ItemKind.Steel,3),(ItemKind.Polymer,2),(ItemKind.Circuit,1)},
        new[] {(ItemKind.Polymer,2),(ItemKind.Lubricant,1),(ItemKind.Steel,1)},
        new[] {(ItemKind.Circuit,2),(ItemKind.Polymer,2)},
        new[] {(ItemKind.Steel,2),(ItemKind.Lubricant,1)},
        new[] {(ItemKind.PrecisionPart,1),(ItemKind.AdvancedCircuit,1),(ItemKind.Steel,2)},
        new[] {(ItemKind.PrecisionPart,1),(ItemKind.Lubricant,1)},
        new[] {(ItemKind.HeatAlloy,1),(ItemKind.Steel,2)}
    };

    static void EquipmentStudy(WorldSimulator sim, PlayerState player, bool tool, int id)
    {
        Advance(sim, .6f);
        int[] before = Copy(player.items);
        int xp = player.xp, attributes = player.attributePoints, skills = player.skillPoints;
        sim.Apply(Command(player, tool ? "tooltech" : "weapontech", arg: id));
        int mask = tool ? sim.State.toolResearch : sim.State.weaponResearch;
        Require((mask & (1 << id)) != 0, "research rejected: " + (tool ? "T" : "W") + id + " " + player.notice);
        var cost = tool ? id == 0 ? new[] {(ItemKind.Steel,2),(ItemKind.Gear,1)} :
            new[] {(ItemKind.Iron,2),(ItemKind.Bronze,1),(ItemKind.Gear,1)} : WeaponResearchSamples[id];
        var expected = Copy(before);
        foreach (var (item, amount) in cost) expected[(int)item] -= amount;
        Inventory(player.items, expected, "research samples");
        Require(player.xp == xp && player.attributePoints == attributes && player.skillPoints == skills,
            "world knowledge spent personal progression");
    }

    static void EquipmentResearchChecks()
    {
        Run("equipment research unlocked era alone cannot manufacture a pistol or shortcut ammunition", () =>
        {
            var sim = EquipmentTechFixture(out var player, era: TechEra.LowVoltage);
            string before = EquipmentEconomy(player);
            sim.Apply(Command(player, "gearcraft", arg: (int)GearKind.Pistol));
            Require(EquipmentEconomy(player) == before, "LV without sidearm research manufactured a pistol");
            Advance(sim, .6f);
            before = EquipmentEconomy(player);
            sim.Apply(Command(player, "craft", arg: 1));
            Require(EquipmentEconomy(player) == before, "shortcut ammunition bypassed basic ammunition research");
        });

        Run("equipment research stone tools handcraft from pickups and repair with wood and stone", () =>
        {
            foreach (var kind in new[] {GearKind.StoneAxe, GearKind.StonePickaxe})
            {
                var sim = PrimitiveFixture(out var player);
                Array.Clear(player.items); player.items[(int)ItemKind.Wood] = 1;
                int stoneCost = kind == GearKind.StoneAxe ? 2 : 3;
                player.items[(int)ItemKind.Stone] = 1;
                player.items[(int)ItemKind.Branch] = 2;
                player.items[(int)ItemKind.LooseStone] = stoneCost;
                player.items[(int)ItemKind.DryGrass] = 1;
                sim.Apply(Command(player, "gearcraft", arg: (int)kind));
                var tool = player.gear.Single();
                Require(tool.kind == kind && player.items[(int)ItemKind.Stone] == 1 &&
                    player.items[(int)ItemKind.Wood] == 1 && player.items[(int)ItemKind.Ore] == 0 &&
                    player.items[(int)ItemKind.Branch] == 0 && player.items[(int)ItemKind.LooseStone] == 0 && player.items[(int)ItemKind.DryGrass] == 0,
                    "stone-only starting materials could not make the tool");
                tool.durability = 70; Advance(sim, .6f);
                sim.Apply(Command(player, "gearrepair", arg: tool.id));
                Require(tool.durability == 95 && player.items[(int)ItemKind.Wood] == 0 &&
                    player.items[(int)ItemKind.Stone] == 0 && player.items[(int)ItemKind.Ore] == 0,
                    "stone repair did not charge one wood and one stone");
            }
        });

        Run("equipment research fourteen optional studies pay exact samples and unlock existing manufacturing", () =>
        {
            var sim = EquipmentTechFixture(out var player);
            Require(sim.State.weaponResearch == 0 && sim.State.toolResearch == 0, "fixture pre-unlocked equipment studies");
            foreach (int id in Enumerable.Range(0, 12)) EquipmentStudy(sim, player, false, id);
            EquipmentStudy(sim, player, true, 0); EquipmentStudy(sim, player, true, 1);
            Require(sim.State.weaponResearch == 4095 && sim.State.toolResearch == 3, "wrong P0 research masks");
            foreach (var kind in new[] {GearKind.SteelAxe, GearKind.SteelPickaxe, GearKind.Wrench,
                GearKind.Pistol, GearKind.Shotgun, GearKind.AssaultRifle, GearKind.PrecisionRifle})
                CraftGear(sim, player, kind);
            foreach (var kind in Enum.GetValues<PartKind>()) CraftPart(sim, player, kind);
        });

        Run("equipment research rejects missing evidence even when the same samples are in inventory", () =>
        {
            foreach (var missing in new[] {ItemKind.Steel, ItemKind.Copper})
            {
                var sim = EquipmentTechFixture(out var player, arrange: (s, _) => s.produced[(int)missing] = 0);
                string before = EquipmentEconomy(player);
                sim.Apply(Command(player, "weapontech", arg: 0));
                Require(sim.State.weaponResearch == 0 && EquipmentEconomy(player) == before,
                    "inventory substituted for industrial production evidence: " + missing);
            }
        });

        Run("equipment research validates player workbench cooldown samples and command ids before payment", () =>
        {
            foreach (string scenario in new[] {"era","industrial","weapon parent","bench","wall",
                "dead","disconnected","cooldown","samples","negative","unknown","node key"})
            {
                var sim = EquipmentTechFixture(out var player);
                int id = 0;
                switch (scenario)
                {
                    case "era": sim.State.era = TechEra.Steam; break;
                    case "industrial": sim.State.steamResearch = 0; break;
                    case "weapon parent": id = 1; break;
                    case "bench": player.x = 45; break;
                    case "wall": Ore(sim.State, 31, 35); break;
                    case "dead": player.hp = 0; break;
                    case "disconnected": sim.SetConnected(player.id, false); break;
                    case "cooldown": player.interactCooldown = .4f; break;
                    case "samples": player.items[(int)ItemKind.Steel] = 1; break;
                    case "negative": id = -1; break;
                    case "unknown": id = 12; break;
                    case "node key": id = 10000; break;
                }
                string before = EquipmentEconomy(player);
                sim.Apply(Command(player, "weapontech", arg: id));
                Require(sim.State.weaponResearch == 0 && EquipmentEconomy(player) == before,
                    "invalid research changed inventory: " + scenario);
            }
        });

        Run("equipment research competing players spend samples once and share manufacture rights", () =>
        {
            var sim = EquipmentTechFixture(out var player);
            var peer = Builder(sim, "research-peer"); peer.x = player.x; peer.y = player.y;
            var other = Copy(peer.items);
            EquipmentStudy(sim, player, false, 0);
            sim.Apply(Command(peer, "weapontech", arg: 0));
            Inventory(peer.items, other, "duplicate peer request charged samples");
            sim.SetConnected(player.id, false); sim.AddPlayer(player.id, player.name);
            Require(sim.State.weaponResearch == 1, "reconnection reset world knowledge");
            EquipmentStudy(sim, peer, false, 1);
            var pistol = CraftGear(sim, player, GearKind.Pistol);
            Require(pistol.kind == GearKind.Pistol, "the other player could not use shared sidearm research");
        });

        Run("equipment research held equipment remains usable and compatible attachments install in early eras", () =>
        {
            var sim = EquipmentFixture(out var player, GearKind.PrecisionRifle, TechEra.Primitive, (s, p) =>
            {
                p.parts[(int)PartKind.Scope4x] = 1;
            });
            GearState rifle = player.gear.Single();
            sim.Apply(Command(player, "attach", x: (int)PartKind.Scope4x, arg: rifle.id));
            Require(rifle.attachments[(int)AttachmentSlot.Optic] == (int)PartKind.Scope4x &&
                player.parts[(int)PartKind.Scope4x] == 0, "manufacture era blocked installing a held compatible attachment");
            LoadEquipment(sim, player, rifle);
            var target = EquipmentTarget(sim.State, 38, 35);
            int ammunition = rifle.magazine;
            FireEquipment(sim, player);
            Require(rifle.magazine == ammunition - 1 && target.hp < 1000, "unresearched held rifle could not fire");
        });

        Run("equipment research current archive rejects corrupt branches and preserves independent team knowledge", () =>
        {
            var sim = EquipmentTechFixture(out var player);
            foreach (int id in Enumerable.Range(0, 12)) EquipmentStudy(sim, player, false, id);
            EquipmentStudy(sim, player, true, 0); EquipmentStudy(sim, player, true, 1);
            var before = Dump(sim.State);
            string folder = Path.Combine(Path.GetTempPath(), "DeadCircuit-EquipmentResearch-" + Guid.NewGuid().ToString("N"));
            try
            {
                var archive = new WorldArchive(folder, Dump, text => JsonSerializer.Deserialize<WorldState>(text, Json));
                string id = Guid.NewGuid().ToString("N"); archive.Write(id, sim.State);
                var restored = archive.Read(id);
                Require(restored.version == WorldSimulator.SaveVersion && restored.weaponResearch == 4095 && restored.toolResearch == 3,
                    "current archive dropped weapon or tool knowledge");
                restored.weaponResearch = 0; restored.toolResearch = 0;
                Require(Dump(sim.State) == before, "restored knowledge mutation reached the source world");
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            foreach (string corruption in new[] {"negative","unknown weapon","unknown tool","isolated","early era","industry","evidence"})
            {
                var bad = Saved(sim.State);
                switch (corruption)
                {
                    case "negative": bad.weaponResearch = -1; break;
                    case "unknown weapon": bad.weaponResearch |= 1 << 12; break;
                    case "unknown tool": bad.toolResearch |= 1 << 2; break;
                    case "isolated": bad.weaponResearch = 2; break;
                    case "early era": bad.era = TechEra.Steam; break;
                    case "industry": bad.steamResearch = 0; break;
                    case "evidence": bad.produced[(int)ItemKind.Copper] = 3; break;
                }
                Reject(bad, "corrupt equipment knowledge was accepted: " + corruption);
            }
        });
    }
}
