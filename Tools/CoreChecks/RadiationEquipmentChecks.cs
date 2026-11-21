using DeadCircuit;
using System.Text.Json;

static partial class Checks
{
    // Arrange only the world supplied to the public simulator constructor. Commands,
    // time and persistence are exercised through Apply, Step and WorldArchive.
    static WorldSimulator RadiationEquipmentFixture(out PlayerState player, bool unlocked = false,
        Action<WorldState, PlayerState> arrange = null) => EquipmentFixture(out player, arrange: (s, p) =>
        {
            s.nuclearUraniumMined = s.gathered = 4;
            s.nuclearResearch = 0;
            if(unlocked) s.nuclearResearch = (1 << (int)NuclearTech.UraniumBeneficiation) |
                (1 << (int)NuclearTech.RadiationMonitoring) | (1 << (int)NuclearTech.RadiationProtection);
            arrange?.Invoke(s, p);
        });

    static GearState RadiationEquipmentCraft(WorldSimulator sim, PlayerState p)
    {
        int before = p.gear.Count;
        sim.Apply(Command(p, "gearcraft", arg: (int)GearKind.RadiationSuit));
        Require(p.gear.Count == before + 1 && p.gear.Last().kind == GearKind.RadiationSuit,
            "suit craft did not create exactly one item: " + p.notice);
        return p.gear.Last();
    }

    static void RadiationEquipmentChecks()
    {
        Run("radiation equipment repairs toward its own 150 durability limit without overcharging", () =>
        {
            var sim=RadiationEquipmentFixture(out var p,true);
            var suit=RadiationEquipmentCraft(sim,p);
            suit.durability=140;
            Advance(sim,.6f);
            var before=Copy(p.items);
            sim.Apply(Command(p,"gearrepair",arg:suit.id));
            Require(suit.durability==150 && !before.SequenceEqual(p.items),"suit repair was capped at 100 or failed to charge materials");
            Advance(sim,.6f);before=Copy(p.items);
            sim.Apply(Command(p,"gearrepair",arg:suit.id));
            Require(suit.durability==150 && before.SequenceEqual(p.items),"full-durability suit repair charged materials");
        });

        Run("radiation equipment bare Nuclear era rejects suit without charging and explains research lock", () =>
        {
            var sim = RadiationEquipmentFixture(out var p);
            string before = EquipmentEconomy(p);
            sim.Apply(Command(p, "gearcraft", arg: (int)GearKind.RadiationSuit));
            Require(EquipmentEconomy(p) == before, "unresearched suit changed inventory or equipment");
            Require(p.notice.Contains("核工业研究") && p.notice.Contains("防护"), "research lock was not explained: " + p.notice);
        });

        Run("radiation equipment public research unlocks one exact-cost torso suit before the first reactor", () =>
        {
            var sim = RadiationEquipmentFixture(out var p);
            foreach(var tech in new[] { NuclearTech.UraniumBeneficiation, NuclearTech.RadiationMonitoring })
            {
                sim.Apply(Command(p, "nuclear", arg: (int)tech));
                Require((sim.State.nuclearResearch & (1 << (int)tech)) != 0, "pre-reactor research rejected: " + p.notice);
                Advance(sim, .6f);
            }
            string locked = EquipmentEconomy(p);
            sim.Apply(Command(p, "gearcraft", arg: (int)GearKind.RadiationSuit));
            Require(EquipmentEconomy(p) == locked && p.notice.Contains("防护"),
                "monitoring alone bypassed protection research");
            sim.Apply(Command(p, "nuclear", arg: (int)NuclearTech.RadiationProtection));
            Require((sim.State.nuclearResearch & (1 << (int)NuclearTech.RadiationProtection)) != 0,
                "protection research rejected before reactor operation: " + p.notice);
            Advance(sim, .6f);
            int[] before = Copy(p.items);
            GearState suit = RadiationEquipmentCraft(sim, p);
            for(int i = 0; i < before.Length; i++)
            {
                int cost = i == (int)ItemKind.Polymer ? 6 :
                    i == (int)ItemKind.SteelPlate || i == (int)ItemKind.RefractoryBrick ? 2 : 0;
                Require(p.items[i] == before[i] - cost, "wrong suit material charge for slot " + i);
            }
            Require(suit.durability == 150 && suit.magazine == 0 && suit.attachments.All(a => a == -1),
                "suit was not created at full 150 durability without ammo or attachments");
            int weapon = p.weaponId, tool = p.toolId, active = p.activeSlot;
            sim.Apply(Command(p, "equip", arg: suit.id));
            Require(p.armorIds.SequenceEqual(new[] { 0, suit.id, 0, 0 }) &&
                p.weaponId == weapon && p.toolId == tool && p.activeSlot == active,
                "equip did not exclusively use the torso slot");
            int count = p.gear.Count;
            sim.Apply(Command(p, "gearcraft", arg: (int)GearKind.RadiationSuit));
            Require(p.gear.Count == count, "immediate duplicate command bypassed crafting cooldown");
            Require(sim.State.produced[(int)ItemKind.NuclearWaste] == 0 &&
                sim.State.buildings.All(b => b.kind != BuildKind.Reactor),
                "fixture required a reactor before researching protection");
        });

        Run("radiation equipment unlocked suit still requires each material and a reachable workbench", () =>
        {
            foreach(ItemKind missing in new[] { ItemKind.Polymer, ItemKind.SteelPlate, ItemKind.RefractoryBrick })
            {
                var sim = RadiationEquipmentFixture(out var p, true, (_, q) =>
                    q.items[(int)missing] = missing == ItemKind.Polymer ? 5 : 1);
                string before = EquipmentEconomy(p);
                sim.Apply(Command(p, "gearcraft", arg: (int)GearKind.RadiationSuit));
                Require(EquipmentEconomy(p) == before, "missing " + missing + " still charged or crafted a suit");
            }
            foreach(bool obstructed in new[] { false, true })
            {
                var sim = RadiationEquipmentFixture(out var p, true, (s, q) =>
                {
                    if(obstructed) Ore(s, 31, 35); else q.x = 40;
                });
                string before = EquipmentEconomy(p);
                sim.Apply(Command(p, "gearcraft", arg: (int)GearKind.RadiationSuit));
                Require(EquipmentEconomy(p) == before, "unreachable workbench still charged or crafted a suit");
            }
        });

        Run("radiation equipment suit respects era and unlocked suit respects full gear inventory", () =>
        {
            var early = EquipmentFixture(out var p, era: TechEra.HighVoltage);
            string before = EquipmentEconomy(p);
            early.Apply(Command(p, "gearcraft", arg: (int)GearKind.RadiationSuit));
            Require(EquipmentEconomy(p) == before, "pre-Nuclear era crafted a suit");
            var full = RadiationEquipmentFixture(out var q, true, (s, player) =>
            {
                for(int i = 0; i < EquipmentCatalog.MaxGear; i++)
                    player.gear.Add(new GearState { id = s.nextId++, kind = GearKind.Knife });
            });
            before = EquipmentEconomy(q);
            full.Apply(Command(q, "gearcraft", arg: (int)GearKind.RadiationSuit));
            Require(EquipmentEconomy(q) == before, "full gear inventory crafted or charged a suit");
        });

        Run("radiation equipment torso replacement and unequip retain the existing vest", () =>
        {
            var sim = RadiationEquipmentFixture(out var p, true, (s, q) =>
            {
                var vest = new GearState { id = s.nextId++, kind = GearKind.TacticalVest, durability = 83 };
                q.gear.Add(vest); q.armorIds[1] = vest.id;
            });
            GearState vest = p.gear.Single();
            GearState suit = RadiationEquipmentCraft(sim, p);
            sim.Apply(Command(p, "equip", arg: suit.id));
            Require(p.armorIds[1] == suit.id && p.gear.Contains(vest) && vest.durability == 83,
                "suit replacement removed or damaged the old torso armor");
            sim.Apply(Command(p, "unequip", arg: suit.id));
            Require(p.armorIds[1] == 0 && p.gear.Contains(suit), "unequip destroyed the suit or left it equipped");
            sim.Apply(Command(p, "equip", arg: vest.id));
            Require(p.armorIds[1] == vest.id && vest.durability == 83, "existing vest could not be re-equipped unchanged");
        });

        Run("radiation equipment current WorldArchive preserves a living player's crafted 150 durability suit", () =>
        {
            var sim = RadiationEquipmentFixture(out var p, true);
            GearState suit = RadiationEquipmentCraft(sim, p);
            sim.Apply(Command(p, "equip", arg: suit.id));
            string folder = Path.Combine(Path.GetTempPath(), "DeadCircuit-RadiationSuit-" + Guid.NewGuid().ToString("N"));
            try
            {
                var archive = new WorldArchive(folder, Dump, text => JsonSerializer.Deserialize<WorldState>(text, Json));
                string id = Guid.NewGuid().ToString("N");
                archive.Write(id, sim.State);
                WorldState loaded = archive.Read(id);
                PlayerState restored = loaded.players.Single(q => q.id == p.id);
                Require(loaded.version == 19 && restored.items.Length == 66 && restored.hp > 0,
                    "suit persistence altered the save contract or living player");
                Require(restored.armorIds[1] == suit.id && restored.gear.Single(g => g.id == suit.id).durability == 150 &&
                    restored.gear.Single(g => g.id == suit.id).kind == GearKind.RadiationSuit && restored.items.SequenceEqual(p.items),
                    "suit identity, durability, equipped slot or material balances did not survive archive");
            }
            finally { if(Directory.Exists(folder)) Directory.Delete(folder, true); }
        });
    }
}
