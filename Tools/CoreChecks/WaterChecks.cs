using DeadCircuit;

static partial class Checks
{
    static WorldSimulator WaterFixture(out PlayerState p,int research=0,Action<WorldState> arrange=null)
    {
        var s=Flat(era:TechEra.Water); s.primitiveVersion=1; s.primitiveResearch=255;
        s.waterVersion=1; s.waterResearch=research;
        s.produced[(int)ItemKind.Bronze]=4; s.produced[(int)ItemKind.Iron]=6;
        s.produced[(int)ItemKind.Brick]=4; s.produced[(int)ItemKind.Charcoal]=2;
        Add(s,BuildKind.Workbench,10,10);
        Add(s,BuildKind.ResearchBench,9,12);
        arrange?.Invoke(s);
        var sim=new WorldSimulator(s); p=Builder(sim); p.x=8;p.y=10;
        return sim;
    }
    static void WaterDrive(WorldState s,BuildKind kind,bool shaft=true)
    {
        var ground=s.ground.ToCharArray(); ground[20*64+19]='2'; s.ground=new string(ground);
        Add(s,BuildKind.Waterwheel,20,20);
        if(shaft) Add(s,BuildKind.Shaft,22,20);
        Add(s,kind,shaft ? 23 : 22,20);
    }
    static void WaterUnlock(WorldSimulator sim,PlayerState p,WaterTech id)
    {
        Advance(sim,.55f); p.x=8;p.y=10;var before=Copy(p.items);
        sim.Apply(Command(p,"water",arg:(int)id));
        Require(WaterCatalog.HasResearch(sim.State,id),"could not research "+id+": "+p.notice);
        Paid(p.items,before,WaterCatalog.Definition((int)id).cost,"water research cost");
    }
    // Progression scenarios may restock inputs from already completed eras.
    // Missing materials outside the allowlist must come from the production route.
    static void RestockPriorEra(PlayerState p, int[] amounts, ItemKind[] available, int batches = 1)
    {
        foreach (ItemKind kind in available)
        {
            int item = (int)kind;
            p.items[item] = Math.Max(p.items[item], amounts[item] * batches);
        }
        long grams = InventoryWeight.CarriedGrams(p);
        Require(grams <= InventoryWeight.CapacityGrams(p), $"progression fixture supplied {grams} g for a single backpack");
    }

    static BuildingState WaterBuild(WorldSimulator sim,PlayerState p,BuildKind kind,int x,int y,ItemKind[] supplies=null)
    {
        if (supplies != null) RestockPriorEra(p, DemoCatalog.BuildCost(kind), supplies);
        Advance(sim,.55f);p.x=x;p.y=y-1;int count=sim.State.buildings.Count;
        BuildNow(sim,p,x,y,(int)kind);
        Require(sim.State.buildings.Count==count+1,"water chain cannot build "+kind+": "+p.notice);
        return sim.State.buildings.Single(b=>b.kind==kind && b.x==x && b.y==y);
    }
    static void WaterUse(WorldSimulator sim,PlayerState p,BuildingState b,string action="interact",int arg=0)
    {
        Advance(sim,.55f);p.x=b.x;p.y=b.y-1;
        sim.Apply(Command(p,action,b.x,b.y,arg));
    }
    static void WaterChecks()
    {
        Run("water research command shares team knowledge and pays only the researcher once",()=>
        {
            var sim=WaterFixture(out var p);var peer=Builder(sim,"peer");
            var other=Copy(peer.items);int xp=p.xp,skills=p.skillPoints,attributes=p.attributePoints;
            var before=Copy(p.items);sim.Apply(Command(p,"water",arg:0));
            Require(sim.State.waterResearch==1 && TechCatalog.IsUnlocked(sim.State,BuildKind.Waterwheel),"wheel research not shared");
            Paid(p.items,before,WaterCatalog.All[0].cost,"wheel samples");Inventory(peer.items,other,"teammate charged");
            Require(p.xp==xp && p.skillPoints==skills && p.attributePoints==attributes,"research changed personal progression");
            before=Copy(p.items);Ready(p);sim.Apply(Command(p,"water",arg:0));Inventory(p.items,before,"duplicate research charged twice");
            Require(sim.State.waterResearch==1,"duplicate research changed mask");
        });
        Run("water full startup researches constructs feeds processes and delivers without steam electricity or pre-made parts",()=>
        {
            var sim=WaterFixture(out var p,arrange:s=>{var ground=s.ground.ToCharArray();ground[20*64+19]='2';s.ground=new string(ground);});
            GrantReinforcedBackpack(sim.State, p);
            Array.Clear(p.items);
            foreach (var (item, amount) in new[] { (ItemKind.Wood, 61), (ItemKind.Stone, 18),
                (ItemKind.Brick, 14), (ItemKind.Charcoal, 12), (ItemKind.Iron, 31),
                (ItemKind.Bronze, 27), (ItemKind.Ore, 14), (ItemKind.Scrap, 8) }) p.items[(int)item] = amount;
            Require(InventoryWeight.CarriedGrams(p) < InventoryWeight.CapacityGrams(p), "water startup supplies exceed backpack capacity");
            WaterUnlock(sim,p,WaterTech.Wheel);WaterBuild(sim,p,BuildKind.Waterwheel,20,20);
            WaterUnlock(sim,p,WaterTech.Shafts);
            for(int y=20;y<=32;y++)WaterBuild(sim,p,BuildKind.Shaft,22,y);
            WaterUnlock(sim,p,WaterTech.Crushing);var crusher=WaterBuild(sim,p,BuildKind.MechanicalCrusher,23,20);
            WaterUse(sim,p,crusher);Advance(sim,10);WaterUse(sim,p,crusher);
            Require(p.items[(int)ItemKind.CrushedOre]>=4,"crusher chain has no recovered output");
            WaterUnlock(sim,p,WaterTech.Blast);var blast=WaterBuild(sim,p,BuildKind.BlastFurnace,23,26);
            WaterUse(sim,p,blast);Advance(sim,12);WaterUse(sim,p,blast);
            WaterUnlock(sim,p,WaterTech.Pressing);var press=WaterBuild(sim,p,BuildKind.MechanicalPress,23,23, new[] { ItemKind.Wood });
            WaterUse(sim,p,press);Advance(sim,30);WaterUse(sim,p,press);
            Require(p.items[(int)ItemKind.IronPlate]>=4 && sim.State.waterPressed>=4,"press failed to bootstrap parts");
            WaterUnlock(sim,p,WaterTech.Components);var workshop=WaterBuild(sim,p,BuildKind.MechanicalWorkshop,23,29, new[] { ItemKind.Wood, ItemKind.Iron });
            WaterUse(sim,p,workshop);Advance(sim,14);WaterUse(sim,p,workshop);
            WaterUse(sim,p,workshop,"recipe",115);
            RestockPriorEra(p, TechCatalog.Recipe(115).inputs, new[] { ItemKind.Bronze, ItemKind.Iron }, batches: 4);
            WaterUse(sim,p,workshop);Advance(sim,14);WaterUse(sim,p,workshop);
            Require(p.items[(int)ItemKind.Gear]>=3 && p.items[(int)ItemKind.Bearing]>=2,"workshop cannot bootstrap loader samples and materials");
            RestockPriorEra(p, DemoCatalog.Materials(ItemKind.Wood, 16), new[] { ItemKind.Wood });
            WaterUnlock(sim,p,WaterTech.Transport);WaterUse(sim,p,press);var hopper=WaterBuild(sim,p,BuildKind.MechanicalHopper,23,32);
            WaterBuild(sim,p,BuildKind.Chest,24,32);WaterUse(sim,p,hopper);Advance(sim,5);
            Require(WaterCatalog.SteamRequirement(sim.State).Length==0,"startup acceptance incomplete: "+WaterCatalog.SteamRequirement(sim.State));
            RestockPriorEra(p, TechCatalog.ResearchCost(TechEra.Steam), new[] { ItemKind.Iron, ItemKind.Bronze });
            p.x=8;p.y=10;Advance(sim,.55f);sim.Apply(Command(p,"research",arg:(int)TechEra.Steam));
            Require(sim.State.era==TechEra.Steam && sim.State.buildings.All(b=>TechCatalog.RequiredEra(b.kind)<=TechEra.Water),"startup depended on future-era machinery");
            var loaded=new WorldSimulator(Saved(sim.State));
            Require(loaded.State.era==TechEra.Steam && loaded.State.waterTransported>=4,"complete chain did not survive restore");
        });
        Run("water research rejects skips materials range walls cooldown dead and offline without payment",()=>
        {
            foreach(string reason in new[]{"skip","negative","huge","short","range","wall","broken","cooldown","dead","offline","era"})
            {
                var sim=WaterFixture(out var p,arrange:s=>{if(reason=="wall") Add(s,BuildKind.Wall,9,11);});
                if(reason=="short")p.items[(int)ItemKind.Bronze]=1;
                if(reason=="range")p.x=3;
                if(reason=="broken")sim.State.buildings.Single(building=>building.kind==BuildKind.ResearchBench).hp=0;
                if(reason=="cooldown")p.interactCooldown=1;
                if(reason=="dead")p.hp=0;
                if(reason=="offline")sim.SetConnected(p.id,false);
                if(reason=="era")sim.State.era=TechEra.Primitive;
                int arg=reason=="skip"?1:reason=="negative"?-1:reason=="huge"?int.MaxValue:0;
                var before=Copy(p.items);sim.Apply(Command(p,"water",arg:arg));
                Inventory(p.items,before,"rejected research charged "+reason);Require(sim.State.waterResearch==0,"rejected research unlocked "+reason);
            }
        });
        Run("water funded samples and an idle crusher cannot bypass production acceptance",()=>
        {
            var sim=WaterFixture(out var p,31,s=>Add(s,BuildKind.MechanicalCrusher,20,20));
            sim.Apply(Command(p,"water",arg:(int)WaterTech.Components));
            Require(!WaterCatalog.HasResearch(sim.State,WaterTech.Components),"inventory plates substituted for actual pressing");
            Require(TechCatalog.ResearchRequirement(sim.State,TechEra.Steam).Length>0,"idle crusher unlocked Steam");
            Require(!TechCatalog.CanResearch(sim.State,p,TechEra.Steam),"funded Steam bypassed water acceptance");
        });
        Run("water locked construction recipes and existing machinery remain idle",()=>
        {
            foreach(BuildKind kind in new[]{BuildKind.Waterwheel,BuildKind.Shaft,BuildKind.MechanicalCrusher,BuildKind.BlastFurnace,BuildKind.MechanicalPress,BuildKind.MechanicalWorkshop,BuildKind.WaterMill,BuildKind.IrrigationPump,BuildKind.MechanicalHopper})
            {
                var sim=WaterFixture(out var p);p.x=18;p.y=20;var before=Copy(p.items);
                sim.Apply(Command(p,"build",20,20,(int)kind));
                Require(sim.State.buildings.Count==2 && sim.State.buildings.All(building=>building.kind!=kind),"constructed locked "+kind);Inventory(p.items,before,"locked building paid");
            }
            var world=WaterFixture(out _,arrange:s=>{WaterDrive(s,BuildKind.MechanicalPress);s.buildings.Last().items[(int)ItemKind.Iron]=4;});
            Advance(world,10);Require(world.State.produced[(int)ItemKind.IronPlate]==0 && world.State.waterPressed==0,"locked machinery ran");
            Require(!PrimitiveCatalog.IsRecipeUnlocked(world.State,TechCatalog.Recipe(13)),"stone furnace bypassed Crushing research");
        });
        Run("water processing conserves materials and requires shaft-connected water for evidence",()=>
        {
            foreach(var pair in new[]{(BuildKind.MechanicalCrusher,20,ItemKind.Ore,ItemKind.CrushedOre,1),(BuildKind.BlastFurnace,110,ItemKind.CrushedOre,ItemKind.Iron,3),(BuildKind.MechanicalPress,113,ItemKind.Iron,ItemKind.IronPlate,1),(BuildKind.MechanicalWorkshop,114,ItemKind.IronPlate,ItemKind.Gear,2),(BuildKind.MechanicalWorkshop,115,ItemKind.Bronze,ItemKind.Bearing,2)})
            {
                var sim=WaterFixture(out _,127,s=>
                {
                    WaterDrive(s,pair.Item1);var b=s.buildings.Last();b.recipe=pair.Item2;
                    Array.Copy(TechCatalog.Recipe(pair.Item2).inputs,b.items,DemoCatalog.ItemCount);
                });
                int previous=sim.State.produced[(int)pair.Item4];Advance(sim,10);var machine=sim.State.buildings.Last();
                var recipe=TechCatalog.Recipe(pair.Item2);
                Require(machine.items.SequenceEqual(recipe.outputs),"recipe lost/created materials: "+pair.Item2);
                Require(sim.State.produced[(int)pair.Item4]==previous+pair.Item5,"wrong produced output");
                int proof=pair.Item2==20?sim.State.waterCrushed:pair.Item2==110?sim.State.waterSmelted:pair.Item2==113?sim.State.waterPressed:pair.Item2==114?sim.State.waterGears:sim.State.waterBearings;
                Require(proof==pair.Item5,"missing water processing proof "+pair.Item2);
            }
            var direct=WaterFixture(out _,127,s=>{WaterDrive(s,BuildKind.MechanicalPress,false);s.buildings.Last().items[(int)ItemKind.Iron]=1;});
            Advance(direct,5);Require(direct.State.produced[(int)ItemKind.IronPlate]==1 && direct.State.waterPressed==0,"direct wheel falsely proved shaft operation");
        });
        Run("water machines stop without power inputs or output room and resume without phantom evidence",()=>
        {
            foreach(string condition in new[]{"power","inputs","blocked"})
            {
                var sim=WaterFixture(out _,127,s=>
                {
                    WaterDrive(s,BuildKind.MechanicalPress);var b=s.buildings.Last();b.items[(int)ItemKind.Iron]=condition=="inputs"?0:1;
                    if(condition=="blocked")b.items[(int)ItemKind.IronPlate]=WorldSimulator.BufferLimit;
                });
                if(condition=="power")sim.State.buildings.Single(x=>x.kind==BuildKind.Shaft).hp=0;
                Advance(sim,5);Require(sim.State.waterPressed==0,"phantom output when "+condition);
                var press=sim.State.buildings.Last();
                if(condition=="inputs")press.items[(int)ItemKind.Iron]=1;
                if(condition=="blocked")press.items[(int)ItemKind.IronPlate]=0;
                if(condition=="power")sim.State.buildings.Single(x=>x.kind==BuildKind.Shaft).hp=120;
                Advance(sim,5);Require(sim.State.waterPressed==1,"machine did not resume "+condition);
            }
        });
        Run("water negative cross-chunk mechanical processing survives unloaded-world save restore",()=>
        {
            var s=WorldGeneration.Generate(new WorldGenerationSettings{seed=17,chunked=true});
            s.era=TechEra.Water;s.primitiveResearch=255;s.waterResearch=127;
            var shore=InfiniteClearChunk(s,-100,50);InfiniteClearChunk(s,-99,50);
            var ground=shore.ground.ToCharArray();ground[5*16+11]='2';shore.ground=new string(ground);
            Add(s,BuildKind.Waterwheel,-1588,805);Add(s,BuildKind.Shaft,-1586,805);
            var press=Add(s,BuildKind.MechanicalPress,-1585,805,rotation:1);press.items[(int)ItemKind.Iron]=1;
            var live=new WorldSimulator(s);Advance(live,.55f);
            Require(Building(live,press.id).progress>0 && !ChunkStreaming.IsActive(live.State,press.x,press.y),"remote signed machine lost power/progress");
            var loaded=new WorldSimulator(Saved(live.State));Advance(live,3);Advance(loaded,3);
            Require(Building(loaded,press.id).items[(int)ItemKind.IronPlate]==1 && loaded.State.waterPressed==1 &&
                Building(live,press.id).items.SequenceEqual(Building(loaded,press.id).items),"negative cross-chunk processing changed on restore");
        });
        Run("water loader counts actual arrivals and conserves in-flight inventory across saves",()=>
        {
            var sim=WaterFixture(out _,127,s=>
            {
                WaterDrive(s,BuildKind.MechanicalHopper);s.buildings.Last().items[(int)ItemKind.IronPlate]=4;
                Add(s,BuildKind.Chest,24,20);
            });
            int[] totals=ItemTotals(sim.State);sim.Step(.05f);
            Require(sim.State.cargo.Count==1 && sim.State.waterTransported==0,"dispatch counted before arrival");
            sim=new WorldSimulator(Saved(sim.State));
            for(int n=0;n<60;n++){sim.Step(.05f);Conserved(sim.State,totals);}
            Require(sim.State.waterTransported==4 && sim.State.buildings.Single(b=>b.kind==BuildKind.Chest).items[(int)ItemKind.IronPlate]==4,"loader missed or duplicated deliveries");
        });
        Run("water loader pauses in-flight on shaft loss resumes once and refunds a lost destination",()=>
        {
            foreach(bool lostTarget in new[]{false,true})
            {
                var sim=WaterFixture(out _,127,s=>{WaterDrive(s,BuildKind.MechanicalHopper);s.buildings.Last().items[(int)ItemKind.Wood]=1;Add(s,BuildKind.Chest,24,20);});
                var totals=ItemTotals(sim.State);sim.Step(.05f);
                var shaft=sim.State.buildings.Single(b=>b.kind==BuildKind.Shaft);shaft.hp=0;Advance(sim,1);
                Require(sim.State.cargo.Count==1 && sim.State.cargo[0].progress==0 && sim.State.waterTransported==0,"unpowered in-flight loader moved");
                Conserved(sim.State,totals);
                if(lostTarget)sim.State.buildings.Single(b=>b.kind==BuildKind.Chest).hp=0;
                else shaft.hp=120;
                Advance(sim,1);Conserved(sim.State,totals);
                Require(sim.State.cargo.Count==0 && sim.State.waterTransported==(lostTarget?0:1),"resume/refund created evidence or duplicate cargo");
                if(lostTarget)Require(sim.State.buildings.Single(b=>b.kind==BuildKind.MechanicalHopper).items[(int)ItemKind.Wood]==1,"orphan refund lost inventory");
            }
        });
        Run("water steam gate requires all industrial evidence while agriculture remains optional",()=>
        {
            var sim=WaterFixture(out var p,127,s=>Add(s,BuildKind.MechanicalCrusher,20,20));
            p.items = TechCatalog.ResearchCost(TechEra.Steam);
            var s=sim.State;s.waterCrushed=4;s.waterSmelted=6;s.waterPressed=4;s.waterGears=2;s.waterBearings=2;s.waterTransported=4;
            foreach(var reduce in new Action[]{()=>s.waterCrushed=3,()=>s.waterSmelted=5,()=>s.waterPressed=3,()=>s.waterGears=1,()=>s.waterBearings=1,()=>s.waterTransported=3})
            {
                reduce();Require(!TechCatalog.CanResearch(s,p,TechEra.Steam),"missing industrial evidence accepted");
                s.waterCrushed=4;s.waterSmelted=6;s.waterPressed=4;s.waterGears=2;s.waterBearings=2;s.waterTransported=4;
            }
            var before=Copy(p.items);sim.Apply(Command(p,"research",arg:(int)TechEra.Steam));
            Require(s.era==TechEra.Steam && !WaterCatalog.HasResearch(s,WaterTech.Agriculture),"optional agriculture blocked Steam");
            Paid(p.items,before,TechCatalog.ResearchCost(TechEra.Steam),"Steam entry samples");
        });
        Run("water powered mill improves flour yield and rejects missing wheat blocked range and full output",()=>
        {
            foreach(string condition in new[]{"ok","exact","power","wheat","full","range","cooldown"})
            {
                var sim=WaterFixture(out var p,255,s=>
                {
                    s.survival=new SurvivalWorldState{enabled=true,initialized=true};
                    WaterDrive(s,BuildKind.WaterMill);
                });
                Array.Clear(p.items);
                p.x=25;p.y=20;p.needs.foods[(int)FoodKind.Wheat]=2;p.needs.foods[(int)FoodKind.Flour]=0;
                if(condition=="power") {sim.State.buildings.Single(b=>b.kind==BuildKind.Shaft).hp=0;sim.Step(.05f);}
                if(condition=="wheat")p.needs.foods[(int)FoodKind.Wheat]=1;
                // The complete batch gains 510 g: three flour (750 g) minus two wheat (240 g).
                if(condition=="full" || condition=="exact") FillSurvivalLoad(p, condition=="exact" ? 510 : 500);
                if(condition=="range")p.x=30;
                if(condition=="cooldown")p.interactCooldown=1;
                var before=Copy(p.needs.foods);sim.Apply(Command(p,"mill",24,20));
                if(condition=="ok" || condition=="exact")
                    Require(p.needs.foods[(int)FoodKind.Wheat]==0 && p.needs.foods[(int)FoodKind.Flour]==3 && sim.State.waterMilled==3,"powered flour output/yield/proof incorrect: "+p.notice);
                else Require(p.needs.foods.SequenceEqual(before) && sim.State.waterMilled==0,"rejected mill changed foods: "+condition);
            }
        });
        Run("water irrigation supplies nearby dry plots only while powered without consuming carried water",()=>
        {
            var sim=WaterFixture(out var p,255,s=>
            {
                s.ground=new string('0',4096);s.survival=new SurvivalWorldState{enabled=true,initialized=true};
                WaterDrive(s,BuildKind.IrrigationPump);var ground=s.ground.ToCharArray();ground[19*64+24]='2';s.ground=new string(ground);
                s.survival.plots.Add(new FarmPlotState{id=s.nextId++,x=26,y=20,moisture=10});
                s.survival.plots.Add(new FarmPlotState{id=s.nextId++,x=35,y=20,moisture=10});
                HoldSurvivalWeather(s);
            });
            int raw=p.needs.waterRaw,clean=p.needs.waterClean;Advance(sim,2);
            Require(sim.State.survival.plots[0].moisture>20 && sim.State.survival.plots[1].moisture<=10 && sim.State.waterIrrigated>10,"pump did not supply only nearby plot");
            Require(p.needs.waterRaw==raw && p.needs.waterClean==clean,"pump consumed carried water");
            sim.State.buildings.Single(b=>b.kind==BuildKind.Shaft).hp=0;float proof=sim.State.waterIrrigated;Advance(sim,2);
            Require(sim.State.waterIrrigated==proof,"disconnected pump created irrigation proof");
        });
        Run("water fresh world opts into independent water research",()=>
        {
            var fresh=new WorldSimulator(73149);Require(fresh.State.waterVersion==1 && fresh.State.waterResearch==0,"new world omitted water research");
        });
        RunHistorical("water genuine v11 saves migrate without materials evidence or locks",()=>
        {
            var old=PrimitiveCurrentSaveFixture();string before=Dump(old);var restored=new WorldSimulator(old);
            Require(restored.State.version==12 && restored.State.waterVersion==0 && restored.State.waterResearch==0 && restored.State.waterPressed==0,"legacy save gained water locks/evidence");
            Require(restored.State.produced.Skip(29).All(n=>n==0) && restored.State.players[0].items.Skip(29).All(n=>n==0) && Dump(old)==before,"legacy migration gifted or mutated inventory");
            Require(TechCatalog.IsUnlocked(restored.State,BuildKind.MechanicalCrusher),"old water machinery relocked");
        });
        Run("water save v12 roundtrip preserves all research counters inventories and scalar progress",()=>
        {
            var sim=WaterFixture(out var p,255);sim.State.waterCrushed=9;sim.State.waterSmelted=12;sim.State.waterPressed=7;
            sim.State.waterGears=4;sim.State.waterBearings=4;sim.State.waterTransported=6;sim.State.waterMilled=3;sim.State.waterIrrigated=2.5f;
            var input=Saved(sim.State);var loaded=new WorldSimulator(input);
            Require(Dump(loaded.State)==Dump(new WorldSimulator(Saved(loaded.State)).State),"v12 restore changed second time");
            Require(loaded.State.waterResearch==255 && loaded.State.waterPressed==7 && loaded.State.waterMilled==3 && loaded.State.waterIrrigated==2.5f,"lost progression fields");
            input.players[0].items[29]++;input.waterPressed++;
            Require(loaded.State.players[0].items[29]==p.items[29] && loaded.State.waterPressed==7,"restore aliases caller");
        });
        Run("water save rejects malformed research evidence historical slots and new building ordinals",()=>
        {
            foreach(var mutate in new Action<WorldState>[]{s=>s.waterVersion=2,s=>s.waterResearch=256,s=>s.waterResearch=2,s=>s.waterCrushed=-1,
                s=>s.waterIrrigated=float.NaN,s=>s.waterIrrigated=float.PositiveInfinity,s=>{s.waterVersion=0;s.waterResearch=1;},
                s=>{s.waterResearch=0;s.waterPressed=1;},s=>s.produced=new int[29],s=>s.players[0].items=new int[29]})
            {
                var s=Saved(WaterFixture(out _).State);mutate(s);Reject(s,"corrupt water progression accepted");
            }
            var old=PrimitiveCurrentSaveFixture();old.era=TechEra.Water;var b=Add(old,BuildKind.MechanicalPress,20,20);b.items=new int[29];
            Reject(old,"v11 accepted new water building");
        });
    }
}
