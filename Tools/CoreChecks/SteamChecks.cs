using DeadCircuit;

static partial class Checks
{
    static readonly ItemKind[] SteamPriorMaterials =
    {
        ItemKind.Food, ItemKind.Wood, ItemKind.Stone, ItemKind.Brick, ItemKind.Charcoal, ItemKind.Iron, ItemKind.Scrap,
        ItemKind.Bronze, ItemKind.IronPlate, ItemKind.Gear, ItemKind.Bearing, ItemKind.Coal,
        ItemKind.Limestone, ItemKind.FireClay, ItemKind.CrushedOre
    };

    static WorldSimulator SteamFixture(out PlayerState p,int research=0,Action<WorldState> arrange=null)
    {
        var s=Flat(era:TechEra.Steam);
        s.steamResearch=research;
        s.steamCoked=s.steamRefractory=s.steamPigIron=s.steamSteel=s.steamPlates=s.steamFittings=s.steamDriven=0;
        if((research & (1<<(int)SteamTech.PigIron))!=0) { s.steamCoked=3;s.steamRefractory=4; }
        if((research & (1<<(int)SteamTech.Refining))!=0) s.steamPigIron=3;
        if((research & (1<<(int)SteamTech.Machining))!=0) s.steamSteel=2;
        if((research & (1<<(int)SteamTech.Piping))!=0) s.steamPlates=2;
        if((research & (1<<(int)SteamTech.Boiler))!=0) s.steamFittings=2;
        Add(s,BuildKind.Workbench,10,10);
        Add(s,BuildKind.ResearchBench,9,12);
        arrange?.Invoke(s);
        var sim=new WorldSimulator(s); p=Builder(sim);p.x=8;p.y=10;
        return sim;
    }
    static void SteamUnlock(WorldSimulator sim,PlayerState p,SteamTech id)
    {
        Advance(sim,.55f);p.x=8;p.y=10;
        RestockPriorEra(p, SteamCatalog.Definition((int)id).cost, SteamPriorMaterials);
        var before=Copy(p.items);
        sim.Apply(Command(p,"steam",arg:(int)id));
        Require(SteamCatalog.HasResearch(sim.State,id),"cannot research "+id+": "+p.notice);
        Paid(p.items,before,SteamCatalog.Definition((int)id).cost,"steam research cost");
    }
    static void SteamBatch(WorldSimulator sim,PlayerState p,BuildingState b,int batches=1,int feedBatches=10)
    {
        for(int i=0;i<batches;i++)
        {
            if (p.hunger < 90)
            {
                RestockPriorEra(p, DemoCatalog.Materials(ItemKind.Food, 1), SteamPriorMaterials);
                Ready(p); sim.Apply(Command(p,"eat"));
            }
            RestockPriorEra(p, TechCatalog.SelectedRecipe(b).inputs, SteamPriorMaterials, batches: feedBatches);
            WaterUse(sim,p,b);Advance(sim,90);WaterUse(sim,p,b);
        }
    }
    static void SteamChecks()
    {
        Run("steam research charges one player shares knowledge and ignores character skill points",()=>
        {
            var sim=SteamFixture(out var p);var peer=Builder(sim,"peer");
            p.items = Copy(SteamCatalog.Definition((int)SteamTech.Coking).cost);
            var before=Copy(p.items);var other=Copy(peer.items);int xp=p.xp,skills=p.skillPoints,attributes=p.attributePoints;
            sim.Apply(Command(p,"steam",arg:(int)SteamTech.Coking));
            Require(SteamCatalog.HasResearch(sim.State,SteamTech.Coking),"coking research command did not unlock team knowledge");
            Paid(p.items,before,SteamCatalog.All[0].cost,"coking samples");Inventory(peer.items,other,"teammate charged");
            Require(p.xp==xp && p.skillPoints==skills && p.attributePoints==attributes,"industrial research changed character progression");
            before=Copy(p.items);Ready(p);sim.Apply(Command(p,"steam",arg:0));Inventory(p.items,before,"repeated research consumed samples");
        });
        Run("steam coking and refractory kilns consume finite raw materials and count completed batches only",()=>
        {
            var sim=SteamFixture(out _,3,s=>
            {
                var coke=Add(s,BuildKind.CokeOven,20,20);coke.items[(int)ItemKind.Coal]=4;coke.items[(int)ItemKind.Charcoal]=1;
                var kiln=Add(s,BuildKind.RefractoryKiln,24,20);kiln.items[(int)ItemKind.FireClay]=3;kiln.items[(int)ItemKind.Stone]=1;kiln.items[(int)ItemKind.Charcoal]=1;
            });
            Advance(sim,3.9f);Require(sim.State.steamCoked==0 && sim.State.steamRefractory==0,"unfinished batches counted");
            Advance(sim,.1f);Require(sim.State.steamCoked==3 && sim.State.steamRefractory==4,"completed kiln output missing");
            var coke=sim.State.buildings.Single(b=>b.kind==BuildKind.CokeOven);
            Require(coke.items[(int)ItemKind.Coal]==0 && coke.items[(int)ItemKind.Charcoal]==0 && coke.items[(int)ItemKind.Coke]==3,"coking material balance changed");
            Advance(sim,8);Require(sim.State.steamCoked==3 && sim.State.steamRefractory==4,"unfed kilns generated evidence");
            Require(sim.State.steamDriven==0,"passive kilns counted as steam-driven machines");
        });
        Run("steam future machines stay inert until team research refreshes their cached power layout",()=>
        {
            var sim=SteamFixture(out var p,arrange:s=>
            {
                var b=Add(s,BuildKind.CokeOven,20,20);b.items[(int)ItemKind.Coal]=4;b.items[(int)ItemKind.Charcoal]=1;
            });
            var oven=sim.State.buildings.Single(b=>b.kind==BuildKind.CokeOven);
            Advance(sim,6);Require(!oven.powered && oven.progress==0 && sim.State.steamCoked==0 && oven.items[(int)ItemKind.Coal]==4,"unresearched oven ran");
            p.items = Copy(SteamCatalog.Definition((int)SteamTech.Coking).cost);
            SteamUnlock(sim,p,SteamTech.Coking);Advance(sim,4);
            Require(oven.powered && sim.State.steamCoked==3,"research did not refresh existing machine permissions");
        });
        Run("steam research rejects missing prerequisites forged samples invalid actors and distant workbenches",()=>
        {
            foreach(string reason in new[]{"samples","distance","prerequisite","milestone","dead","disconnected","bad id","wall"})
            {
                var sim=SteamFixture(out var p,reason=="milestone"?3:0);
                int id=reason=="prerequisite" || reason=="milestone" ? (int)SteamTech.PigIron : reason=="bad id" ? 1000 : 0;
                if(reason=="samples") p.items[(int)ItemKind.Coal]=0;
                if(reason=="distance") p.x=30;
                if(reason=="dead") p.hp=0;
                if(reason=="disconnected") p.connected=false;
                if(reason=="wall") Add(sim.State,BuildKind.Wall,9,11);
                var before=Copy(p.items);int research=sim.State.steamResearch;
                sim.Apply(Command(p,"steam",arg:id));
                Require(sim.State.steamResearch==research,"blocked research unlocked: "+reason);Inventory(p.items,before,"blocked research charged "+reason);
            }
        });
        Run("steam mechanical processing pauses without shaft missing inputs or output space",()=>
        {
            foreach(string stop in new[]{"shaft","input","full"})
            {
                var sim=SteamFixture(out _,7,s=>
                {
                    WaterDrive(s,BuildKind.IronBlastFurnace);var b=s.buildings.Last();
                    b.items[(int)ItemKind.CrushedOre]=2;b.items[(int)ItemKind.Coke]=2;b.items[(int)ItemKind.Limestone]=1;
                });
                var b=sim.State.buildings.Single(n=>n.kind==BuildKind.IronBlastFurnace);
                var shaft=sim.State.buildings.Single(n=>n.kind==BuildKind.Shaft);
                Advance(sim,1);float progress=b.progress;int before=sim.State.steamPigIron;
                if(stop=="shaft") shaft.hp=0;
                if(stop=="input") b.items[(int)ItemKind.Coke]=0;
                if(stop=="full") b.items[(int)ItemKind.PigIron]=WorldSimulator.BufferLimit;
                Advance(sim,5);Near(b.progress,progress,"stopped recipe progress");Require(sim.State.steamPigIron==before,"stopped plant gained evidence: "+stop);
                if(stop=="shaft") shaft.hp=120;
                if(stop=="input") b.items[(int)ItemKind.Coke]=2;
                if(stop=="full") b.items[(int)ItemKind.PigIron]=0;
                Advance(sim,3);Require(b.items[(int)ItemKind.PigIron]==3 && sim.State.steamPigIron==before+3,"resumed plant lost or duplicated output");
            }
        });
        Run("steam engine evidence needs a complete burning module cluster a live shaft and completed output",()=>
        {
            var sim=SteamFixture(out _,255,s=>
            {
                AddSteamCluster(s,20,20,0).chamber.items[(int)ItemKind.Coal]=1;
                Add(s,BuildKind.SteamPipe,22,21);Add(s,BuildKind.SteamEngine,23,21);Add(s,BuildKind.Shaft,26,21);
                var b=Add(s,BuildKind.MechanicalWorkshop,27,21);b.items[(int)ItemKind.IronPlate]=2;b.items[(int)ItemKind.Bronze]=2;
            });
            var workshop=sim.State.buildings.Single(b=>b.kind==BuildKind.MechanicalWorkshop);
            var chimney=sim.State.buildings.Single(b=>b.kind==BuildKind.Chimney);
            Advance(sim,1);Require(sim.State.steamDriven==0 && workshop.progress>0,"unfinished output was counted");
            chimney.hp=0;float progress=workshop.progress;Advance(sim,5);
            Near(workshop.progress,progress,"missing exhaust did not stop driven machinery");Require(sim.State.steamDriven==0,"incomplete boiler cluster counted engine output");
            chimney.hp=120;Advance(sim,2);Require(sim.State.steamDriven==2 && workshop.items[(int)ItemKind.Gear]==2,"restored steam chain did not complete one batch");
            Advance(sim,.75f);var source=Saved(sim.State);string frozen=Dump(source);var restored=new WorldSimulator(source);
            Advance(sim,2.25f);Advance(restored,2.25f);
            Require(sim.State.steamDriven==4 && restored.State.steamDriven==4,"save duplicated or forgot completed steam evidence");
            Require(Dump(source)==frozen,"restore mutated caller-owned save graph");
        });
        Run("steam chamber accepts gathered coal coke and fuel through manual and belt-free automatic delivery",()=>
        {
            foreach(ItemKind fuel in new[]{ItemKind.Coal,ItemKind.Coke,ItemKind.Fuel})
            {
                var sim=SteamFixture(out var p,255,s=>AddSteamCluster(s,20,20,0));
                var chamber=sim.State.buildings.Single(b=>b.kind==BuildKind.CombustionChamber);
                Array.Clear(p.items);p.items[(int)fuel]=2;WaterUse(sim,p,chamber);sim.Step(.05f);
                Require(chamber.items[(int)fuel]==1,"manual chamber did not select available "+fuel);
                Near(chamber.fuel,19.95f,"manual ignition reserve");
                var restored=new WorldSimulator(Saved(sim.State));
                Require(Building(restored,chamber.id).items[(int)fuel]==1,"nonconsuming restore burnt replacement fuel");
                var automatic=SteamFixture(out _,255,s=>
                {
                    AddSteamCluster(s,20,20,0);var chest=Add(s,BuildKind.Chest,17,20);chest.items[(int)fuel]=1;
                });
                Advance(automatic,2);var burner=automatic.State.buildings.Single(b=>b.kind==BuildKind.CombustionChamber);
                Require(burner.fuel>0 && automatic.State.buildings.Single(b=>b.kind==BuildKind.Chest).items[(int)fuel]==0 && automatic.State.cargo.Count==0,"automatic fuel route failed: "+fuel);
            }
        });
        Run("steam miners collect and export each new finite deposit using steam power",()=>
        {
            foreach(var resource in new[]{(NodeKind.Coal,ItemKind.Coal),(NodeKind.Limestone,ItemKind.Limestone),(NodeKind.FireClay,ItemKind.FireClay)})
            {
                var sim=SteamFixture(out var p,255,s=>
                {
                    AddSteamCluster(s,20,20,1);Add(s,BuildKind.SteamPipe,22,21);Add(s,BuildKind.SteamMiner,23,21);
                    s.nodes.Add(new NodeState {id=s.nextId++,kind=resource.Item1,x=25,y=21,stock=2,hp=35});
                });
                Array.Clear(p.items);
                Advance(sim,3);var miner=sim.State.buildings.Single(b=>b.kind==BuildKind.SteamMiner);
                Require(miner.items[(int)resource.Item2]==1 && sim.State.nodes.Single().stock==1,"steam extraction failed: "+resource.Item1);
                int before=p.items[(int)resource.Item2];WaterUse(sim,p,miner);
                Require(p.items[(int)resource.Item2]==before+1 && miner.items[(int)resource.Item2]==0,"mined output cannot be retrieved");
            }
        });
        Run("steam LV gate ignores stockpiled samples and requires every actual production milestone",()=>
        {
            foreach(int missing in Enumerable.Range(0,7))
            {
                var sim=SteamFixture(out var p,255,s=>
                {
                    s.steamCoked=6;s.steamRefractory=8;s.steamPigIron=6;s.steamSteel=4;s.steamPlates=4;s.steamFittings=4;s.steamDriven=4;
                    if(missing==0)s.steamCoked=5;if(missing==1)s.steamRefractory=7;if(missing==2)s.steamPigIron=5;
                    if(missing==3)s.steamSteel=3;if(missing==4)s.steamPlates=3;if(missing==5)s.steamFittings=3;if(missing==6)s.steamDriven=3;
                    Add(s,BuildKind.SteamFurnace,20,20);
                });
                var before=Copy(p.items);sim.Apply(Command(p,"research",arg:(int)TechEra.LowVoltage));
                Require(sim.State.era==TechEra.Steam,"stockpiles bypassed milestone "+missing);Inventory(p.items,before,"failed LV acceptance charged samples");
            }
        });
        Run("steam current save rejects forged research and counters while preserving valid progress",()=>
        {
            var sim=SteamFixture(out _,3);var source=Saved(sim.State);
            foreach(int mutation in Enumerable.Range(0,4))
            {
                var bad=Saved(source);if(mutation==0)bad.steamResearch=256;if(mutation==1)bad.steamCoked=-1;
                if(mutation==2)bad.steamResearch|=1<<(int)SteamTech.Refining;if(mutation==3)bad.steamPigIron=100;
                bool rejected=false;try { new WorldSimulator(bad); }catch(ArgumentException) {rejected=true;}
                Require(rejected,"forged steam progression accepted: "+mutation);
            }
        });
        Run("steam full startup uses water-powered steel and machine tools before building its first engine",()=>
        {
            var sim=SteamFixture(out var p,arrange:s=>
            {
                s.nextWaveAt=9999;
                s.primitiveVersion=s.waterVersion=1;s.primitiveResearch=255;s.waterResearch=127;
                s.produced[(int)ItemKind.Bronze]=4;s.produced[(int)ItemKind.Iron]=6;s.produced[(int)ItemKind.Brick]=4;s.produced[(int)ItemKind.Charcoal]=2;
                s.waterCrushed=4;s.waterSmelted=6;s.waterPressed=4;s.waterGears=2;s.waterBearings=2;s.waterTransported=4;
                var ground=s.ground.ToCharArray();ground[20*64+19]='2';ground[44*64+20]='2';s.ground=new string(ground);
            });
            Array.Clear(p.items);
            p.items[(int)ItemKind.Food] = 4;
            WaterBuild(sim,p,BuildKind.Waterwheel,20,20,SteamPriorMaterials);
            for(int y=20;y<=36;y++) WaterBuild(sim,p,BuildKind.Shaft,22,y,SteamPriorMaterials);
            SteamUnlock(sim,p,SteamTech.Coking);var coke=WaterBuild(sim,p,BuildKind.CokeOven,27,14,SteamPriorMaterials);SteamBatch(sim,p,coke,3);
            SteamUnlock(sim,p,SteamTech.Refractories);var kiln=WaterBuild(sim,p,BuildKind.RefractoryKiln,31,14,SteamPriorMaterials);SteamBatch(sim,p,kiln,3,feedBatches:3);
            SteamUnlock(sim,p,SteamTech.PigIron);var blast=WaterBuild(sim,p,BuildKind.IronBlastFurnace,23,20,SteamPriorMaterials);SteamBatch(sim,p,blast,4,feedBatches:5);
            SteamUnlock(sim,p,SteamTech.Refining);var forge=WaterBuild(sim,p,BuildKind.RefiningForge,23,24,SteamPriorMaterials);SteamBatch(sim,p,forge,6);
            Require(p.items[(int)ItemKind.Steel]>=20,"water-driven steel chain cannot bootstrap machine tools: steel="+p.items[(int)ItemKind.Steel]+", pig="+sim.State.steamPigIron+", refined="+sim.State.steamSteel+", forge inputs="+string.Join(",",forge.items)+", power="+forge.powerRatio);
            SteamUnlock(sim,p,SteamTech.Machining);var press=WaterBuild(sim,p,BuildKind.MechanicalPress,23,28,SteamPriorMaterials);
            // Capture produced plates separately so collecting them does not also reload the press.
            var plates = WaterBuild(sim,p,BuildKind.Chest,25,28,SteamPriorMaterials);
            WaterUse(sim,p,press,"recipe",124);
            WaterUse(sim,p,press); Advance(sim, 90); WaterUse(sim,p,plates);
            var tool=WaterBuild(sim,p,BuildKind.MachineTool,23,32,SteamPriorMaterials);
            SteamUnlock(sim,p,SteamTech.Piping);SteamBatch(sim,p,tool);
            WaterUse(sim,p,press); Advance(sim, 90); WaterUse(sim,p,plates);
            SteamBatch(sim,p,coke,feedBatches:5); SteamBatch(sim,p,blast,feedBatches:5); SteamBatch(sim,p,forge,feedBatches:5);
            Require(sim.State.steamFittings>=4 && sim.State.steamDriven==0 && !sim.State.buildings.Any(b=>b.kind==BuildKind.SteamEngine),"bootstrap counted steam or skipped pressure parts");
            SteamUnlock(sim,p,SteamTech.Boiler);
            var boiler=WaterBuild(sim,p,BuildKind.Boiler,20,40,SteamPriorMaterials);
            var chamber=WaterBuild(sim,p,BuildKind.CombustionChamber,18,40,SteamPriorMaterials);
            WaterBuild(sim,p,BuildKind.Chimney,20,39,SteamPriorMaterials);WaterBuild(sim,p,BuildKind.FeedwaterPump,20,42,SteamPriorMaterials);
            RestockPriorEra(p, DemoCatalog.Materials(ItemKind.Coal, 8), SteamPriorMaterials);
            WaterUse(sim,p,chamber);
            SteamUnlock(sim,p,SteamTech.Drive);var engine=WaterBuild(sim,p,BuildKind.SteamEngine,22,40,SteamPriorMaterials);
            // An independent engine shaft and workshop prove steam contributes without a waterwheel.
            WaterBuild(sim,p,BuildKind.Shaft,25,40,SteamPriorMaterials);var driven=WaterBuild(sim,p,BuildKind.MechanicalWorkshop,26,40,SteamPriorMaterials);
            RestockPriorEra(p, TechCatalog.SelectedRecipe(driven).inputs, SteamPriorMaterials, batches: 4);
            WaterUse(sim,p,driven);Advance(sim,12);WaterUse(sim,p,driven);
            Require(sim.State.steamDriven>=4 && engine.powerGeneration>0 && boiler.powerGeneration>0,"module steam did not drive actual output");
            WaterBuild(sim,p,BuildKind.SteamFurnace,22,42,SteamPriorMaterials);
            SteamBatch(sim,p,coke);SteamBatch(sim,p,blast);SteamBatch(sim,p,forge);
            p.x=8;p.y=10;Ready(p);
            Require(TechCatalog.ResearchRequirement(sim.State,TechEra.LowVoltage).Length==0,"LV gate still blocked: "+TechCatalog.ResearchRequirement(sim.State,TechEra.LowVoltage));
            RestockPriorEra(p, TechCatalog.ResearchCost(TechEra.LowVoltage), SteamPriorMaterials);
            Ready(p);sim.Apply(Command(p,"research",arg:(int)TechEra.LowVoltage));
            Require(sim.State.era==TechEra.LowVoltage,"cannot enter low voltage after full steam acceptance: "+p.notice+", steel="+p.items[(int)ItemKind.Steel]+", bronze="+p.items[(int)ItemKind.Bronze]+", total steel="+sim.State.steamSteel+", pig="+sim.State.steamPigIron+", forge="+string.Join(",",forge.items)+", hp="+p.hp);
            var restored=new WorldSimulator(Saved(sim.State));
            Require(restored.State.steamResearch==255 && restored.State.steamDriven==sim.State.steamDriven,"current-format save lost steam research/evidence");
        });
    }
}
