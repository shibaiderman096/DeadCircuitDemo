using DeadCircuit;

static partial class Checks
{
    static void RadiationChecks()
    {
        Run("radiation from a fueled reactor accumulates only through fixed simulation steps", () =>
        {
            var dose=typeof(PlayerState).GetField("radiationDose");
            Require(dose!=null,"player exposure is missing from persistent state");
            var state=Flat(era:TechEra.Nuclear);
            Add(state,BuildKind.Reactor,13,10).fuel=2f;
            var sim=new WorldSimulator(state);
            var p=Builder(sim); p.x=11.5f; p.y=10;
            Near((float)dose.GetValue(p),0,"initial exposure");
            Advance(sim,1f);
            Require((float)dose.GetValue(p)>1f,"a fueled reactor did not expose a nearby survivor");
            float before=(float)dose.GetValue(p);
            var restored=new WorldSimulator(Saved(sim.State));
            Near((float)dose.GetValue(restored.State.players[0]),before,"restore advanced or discarded exposure");
            p.radiationDose=99.99f;
            sim.Step(WorldSimulator.FixedStep);
            Near(p.radiationDose,100f,"exposure cap");
            _=new WorldSimulator(Saved(sim.State));
        });

        Run("radiation worn durable protection reduces exposure and wears only during exposure", () =>
        {
            var state=Flat(era:TechEra.Nuclear);
            Add(state,BuildKind.Reactor,13,10).fuel=2f;
            var sim=new WorldSimulator(state);
            var plain=Builder(sim,"plain"); plain.x=11.5f; plain.y=10;
            var protectedPlayer=Builder(sim,"protected"); protectedPlayer.x=11.5f; protectedPlayer.y=10;
            var suit=new GearState {id=sim.State.nextId++,kind=GearKind.RadiationSuit,durability=150};
            protectedPlayer.gear.Add(suit);
            sim.Apply(Command(protectedPlayer,"equip",arg:suit.id));
            Advance(sim,1f);
            Near(protectedPlayer.radiationDose,plain.radiationDose*.2f,"80 percent reduction from a worn suit");
            Near(RadiationCatalog.Protection(protectedPlayer),.8f,"valid suit protection");
            Require(suit.durability==150,"suit wore out before a full wear interval");
            var restored=new WorldSimulator(Saved(sim.State));
            var p=restored.State.players.Single(p=>p.id==protectedPlayer.id);
            Advance(restored,1f);
            Require(EquipmentCatalog.Find(p,suit.id).durability==149,"restore discarded partial suit wear");
            restored.Apply(Command(p,"unequip",arg:suit.id));
            Near(RadiationCatalog.Protection(p),0,"stored suit still protected the wearer");
            float before=p.radiationDose; Advance(restored,.5f);
            Near(p.radiationDose-before,1.5f,"unprotected exposure after unequipping");
            restored.Apply(Command(p,"equip",arg:suit.id));
            EquipmentCatalog.Find(p,suit.id).durability=0;
            Near(RadiationCatalog.Protection(p),0,"broken suit still protected the wearer");
            p.x=0;p.y=0;
            int wear=EquipmentCatalog.Find(p,suit.id).durability;
            Advance(restored,10f);
            Near(p.radiationDose,0,"exposure did not recover after leaving the field");
            Require(EquipmentCatalog.Find(p,suit.id).durability==wear,"safe-zone time consumed suit durability");
        });

        Run("radiation damage bypasses combat armor freezes offline and clears only on successful respawn", () =>
        {
            var state=Flat(era:TechEra.Nuclear);
            Add(state,BuildKind.Reactor,13,10).fuel=2f;
            var sim=new WorldSimulator(state);
            var p=Builder(sim);p.x=11.5f;p.y=10;p.radiationDose=50;
            var vest=new GearState {id=sim.State.nextId++,kind=GearKind.TacticalVest};
            p.gear.Add(vest);p.armorIds[1]=vest.id;
            sim.Step(WorldSimulator.FixedStep);
            Near(p.hp,99.93974f,"combat armor or spawn immunity changed environmental radiation damage");
            p.connected=false;
            float dose=p.radiationDose,hp=p.hp;
            Advance(sim,1f);
            Near(p.radiationDose,dose,"offline exposure advanced");Near(p.hp,hp,"offline health changed");
            sim.AddPlayer(p.id,p.name);
            Near(p.radiationDose,dose,"reconnecting erased exposure");
            p.radiationDose=99;p.hp=.01f;
            sim.Step(WorldSimulator.FixedStep);
            Require(p.hp==0 && p.deaths==1,"radiation did not follow the normal one-time death path");
            Advance(sim,1f);
            Require(p.deaths==1 && p.radiationDose>90,"a dead survivor was damaged twice or recovered exposure");
            sim.Apply(Command(p,"respawn"));
            Require(p.hp==0 && p.radiationDose>90,"premature respawn reset exposure");
            Advance(sim,4.1f);
            sim.Apply(Command(p,"respawn"));
            Require(p.hp>0 && p.deaths==1 && p.radiationDose==0 && p.radiationWear==0,
                "successful respawn did not reset exposure and suit wear state");
        });

        Run("radiation query covers shielded waste and its in-flight transport without mutating state", () =>
        {
            var state=Flat(era:TechEra.Nuclear);
            var source=Add(state,BuildKind.NuclearWasteVault,10,10);
            source.items[(int)ItemKind.NuclearWaste]=200;
            float ordinary=RadiationCatalog.AmbientRate(state,10,8.8f);
            EarnNuclearEvidence(state);state.nuclearResearch=NuclearCatalog.AllResearch;
            float shielded=RadiationCatalog.AmbientRate(state,10,8.8f);
            Require(ordinary>0 && shielded>0 && shielded<ordinary*.25f,"shielded storage did not reduce waste emissions");
            source.items[(int)ItemKind.NuclearWaste]=1;
            var destination=Add(state,BuildKind.NuclearWasteVault,13,10);
            var sim=new WorldSimulator(state);
            var cargo=AwaitCargo(sim,source.id,destination.id);
            Require(Building(sim,source.id).items[(int)ItemKind.NuclearWaste]==0 &&
                Building(sim,destination.id).items[(int)ItemKind.NuclearWaste]==0,"transport did not isolate the in-flight waste");
            string before=Dump(sim.State);
            Require(RadiationCatalog.AmbientRate(sim.State,cargo.fromX,cargo.fromY)>0,"in-flight nuclear waste became radiation-free");
            Require(Dump(sim.State)==before,"a radiation monitor query mutated world state");
            var restored=new WorldSimulator(Saved(sim.State));
            Advance(restored,1f);
            Require(Building(restored,destination.id).items[(int)ItemKind.NuclearWaste]==1,"radiation monitoring broke waste delivery");
            Near(RadiationCatalog.AmbientRate(restored.State,50,50),0,"radiation escaped its local field");
        });

        Run("radiation query remains translation invariant across negative coordinates and rotated footprints", () =>
        {
            foreach(int rotation in new[] {0,1,2,3})
            {
                var state=Flat(era:TechEra.Nuclear);
                var reactor=Add(state,BuildKind.Reactor,16,16,rotation);reactor.fuel=1;
                float before=RadiationCatalog.AmbientRate(state,14.5f,16);
                reactor.x-=33;reactor.y-=17;
                Near(RadiationCatalog.AmbientRate(state,-18.5f,-1),before,"global-coordinate radiation field");
            }
        });

        Run("radiation current saves validate finite bounded dose and resume exposure without preview advancement", () =>
        {
            var sim=new WorldSimulator(Flat(era:TechEra.Nuclear));
            var p=Builder(sim);p.radiationDose=37.5f;p.radiationWear=1.75f;p.x=8;p.y=10;
            Add(sim.State,BuildKind.Workbench,10,10);
            foreach(float value in new[] {float.NaN,float.PositiveInfinity,-1f,100.01f})
            {
                var bad=Saved(sim.State);bad.players[0].radiationDose=value;
                Reject(bad,"invalid radiation dose survived restore");
            }
            foreach(float value in new[] {float.NaN,float.PositiveInfinity,-.01f,2.01f})
            {
                var bad=Saved(sim.State);bad.players[0].radiationWear=value;
                Reject(bad,"invalid suit wear survived restore");
            }
            var restored=new WorldSimulator(Saved(sim.State));
            Near(restored.State.players[0].radiationDose,37.5f,"restore changed exposure");
            Near(restored.State.players[0].radiationWear,1.75f,"restore changed partial wear");
            var raw=Add(sim.State,BuildKind.Chest,30,30);
            raw.items[(int)ItemKind.UraniumOre]=20;raw.items[(int)ItemKind.UraniumConcentrate]=20;raw.items[(int)ItemKind.NuclearFuel]=20;
            Near(RadiationCatalog.AmbientRate(sim.State,30,28.8f),0,"unprocessed ore or unused fuel caused an early-game radiation trap");
            Advance(sim,1f);Advance(restored,1f);
            Near(restored.State.players[0].radiationDose,p.radiationDose,"save/resume exposure diverged");
            Near(restored.State.players[0].hp,p.hp,"save/resume radiation damage diverged");
        });
    }
}
