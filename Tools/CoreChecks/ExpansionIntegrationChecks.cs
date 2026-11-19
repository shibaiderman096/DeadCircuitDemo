using DeadCircuit;

static partial class Checks
{
    static WorldSimulator HuntingFixture(out PlayerState player, GearKind weapon, Action<WorldState,PlayerState> arrange) =>
        EquipmentFixture(out player, weapon, arrange:(s,p)=>
        {
            s.version=6; WorldGeneration.LegacyMetadata(s);
            s.survival=new SurvivalWorldState { enabled=true,initialized=true };
            p.needs.initialized=true;
            if (EquipmentCatalog.IsGun(weapon)) p.gear[0].magazine=1;
            arrange(s,p);
        });
    static CreatureState HuntingAnimal(WorldState s,float x=34,float y=35)
    {
        var c=new CreatureState { id=s.nextId++,kind=CreatureKind.Bear,x=x,y=y,hp=240 };
        s.survival.creatures.Add(c); return c;
    }
    static void ExpansionIntegrationChecks()
    {
        Run("integration pistol first hits nearer wildlife before a zombie",()=>
        {
            var sim=HuntingFixture(out var p,GearKind.Pistol,(s,p)=> { HuntingAnimal(s); EquipmentTarget(s,36); });
            FireEquipment(sim,p);
            Require(sim.State.survival.creatures[0].hp<240,"near animal did not take the bullet");
            Near(sim.State.zombies[0].hp,1000,"ordinary pistol passed through animal");
            Require(p.gear[0].magazine==0,"shot did not consume magazine");
        });
        Run("integration pistol first hits nearer zombie before wildlife",()=>
        {
            var sim=HuntingFixture(out var p,GearKind.Pistol,(s,p)=> { EquipmentTarget(s,34); HuntingAnimal(s,36); });
            FireEquipment(sim,p);
            Require(sim.State.zombies[0].hp<1000,"near zombie did not intercept the bullet");
            Near(sim.State.survival.creatures[0].hp,240,"bullet passed through zombie into animal");
        });
        Run("integration terrain stops mixed wildlife zombie firearm targets",()=>
        {
            var sim=HuntingFixture(out var p,GearKind.PrecisionRifle,(s,p)=>
            {
                s.nodes.Add(new NodeState { id=s.nextId++,kind=NodeKind.Wall,x=33,y=35,stock=1,hp=160 });
                HuntingAnimal(s); EquipmentTarget(s,36);
            });
            FireEquipment(sim,p);
            Near(sim.State.survival.creatures[0].hp,240,"rifle bypassed wall into animal");
            Near(sim.State.zombies[0].hp,1000,"rifle bypassed wall into zombie");
        });
        Run("integration precision penetration crosses a mixed actor sequence",()=>
        {
            var sim=HuntingFixture(out var p,GearKind.PrecisionRifle,(s,p)=> { HuntingAnimal(s); EquipmentTarget(s,36); });
            FireEquipment(sim,p);
            float first=240-sim.State.survival.creatures[0].hp,second=1000-sim.State.zombies[0].hp;
            Require(first>0 && second>0,"precision bullet did not penetrate both actor kinds");
            Near(second,first*.8f,"mixed penetration lost attenuation",.001f);
        });
        Run("integration disabled survival cannot intercept industrial world bullets",()=>
        {
            var sim=HuntingFixture(out var p,GearKind.Pistol,(s,p)=>
            { s.survival.enabled=false; HuntingAnimal(s); EquipmentTarget(s,36); });
            FireEquipment(sim,p);
            Near(sim.State.survival.creatures[0].hp,240,"disabled wildlife took damage");
            Require(sim.State.zombies[0].hp<1000,"legacy ranged target stopped working");
        });
        Run("integration melee shares nearest order across animals and zombies",()=>
        {
            var sim=HuntingFixture(out var p,GearKind.Knife,(s,p)=> { HuntingAnimal(s,33); EquipmentTarget(s,32.5f,hp:1); });
            FireEquipment(sim,p);
            Require(sim.State.zombies.Count==0,"closer zombie was skipped");
            Near(sim.State.survival.creatures[0].hp,240,"ordinary melee cleaved into second actor");
        });
        Run("integration tier three melee cleaves both actor kinds once",()=>
        {
            var sim=HuntingFixture(out var p,GearKind.Knife,(s,p)=>
            { p.level=5;p.skills[1]=3;p.skillPoints=2;HuntingAnimal(s,33);EquipmentTarget(s,32.5f,hp:1); });
            FireEquipment(sim,p);
            Require(sim.State.zombies.Count==0 && sim.State.survival.creatures[0].hp<240,"cleave did not reach both actor kinds");
            Require(p.gear[0].durability==99,"cleave spent tool durability once per victim");
        });
        Run("integration twenty seeded living worlds stay valid after thirty seconds",()=>
        {
            for(int seed=0;seed<20;seed++)
            {
                var sim=new WorldSimulator(seed); sim.AddPlayer("long-live","Living");
                Advance(sim,30); var restored=new WorldSimulator(Saved(sim.State));
                Require(restored.State.tick==sim.State.tick,"restored tick drift");
                Require(restored.State.survival.creatures.Count<=32 && restored.State.zombies.Count<=160,"entity cap drift");
                Require(SurvivalDump(restored.State)==SurvivalDump(sim.State),"future simulation state was changed during restore");
            }
        });
    }
}
