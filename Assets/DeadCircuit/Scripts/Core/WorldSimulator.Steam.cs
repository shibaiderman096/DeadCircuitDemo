namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void ResearchSteam(PlayerState p,int arg)
        {
            string requirement=SteamCatalog.ResearchRequirement(State,p,arg);
            if(requirement.Length!=0) { Notice(p,requirement); return; }
            var definition=SteamCatalog.Definition(arg);
            Pay(p.items,definition.cost);
            State.steamResearch |= 1<<arg;
            p.interactCooldown=.5f;
            State.announcement="全队蒸汽科技已解锁："+definition.name;
            State.announcementUntil=State.elapsed+8f;
            RefreshPower(false);
        }

        // Called only after input payment and actual output delivery into the machine buffer.
        private void RecordSteamProduction(BuildingState b,RecipeDefinition recipe)
        {
            switch(recipe.id)
            {
                case 120: State.steamCoked=SaturatingAdd(State.steamCoked,recipe.outputs[(int)ItemKind.Coke]); break;
                case 121: State.steamRefractory=SaturatingAdd(State.steamRefractory,recipe.outputs[(int)ItemKind.RefractoryBrick]); break;
                case 122: State.steamPigIron=SaturatingAdd(State.steamPigIron,recipe.outputs[(int)ItemKind.PigIron]); break;
                case 123: case 30: State.steamSteel=SaturatingAdd(State.steamSteel,recipe.outputs[(int)ItemKind.Steel]); break;
                case 124: State.steamPlates=SaturatingAdd(State.steamPlates,recipe.outputs[(int)ItemKind.SteelPlate]); break;
                case 125: State.steamFittings=SaturatingAdd(State.steamFittings,recipe.outputs[(int)ItemKind.PipeFitting]); break;
            }
            if(!IsSteamDriven(b)) return;
            foreach(int amount in recipe.outputs) State.steamDriven=SaturatingAdd(State.steamDriven,amount);
        }

        private bool IsSteamDriven(BuildingState machine)
        {
            if(machine==null || TechCatalog.EnergyTier(machine)!=0 || !machine.powered ||
                machine.powerRatio<=0f || machine.networkId<=0 || !SteamCatalog.HasResearch(State,SteamTech.Drive)) return false;
            bool shaft=false;
            foreach(var layout in powerLayout)
            {
                var b=layout.building;
                if(b.kind==BuildKind.Shaft && b.hp>0f && b.powered && b.networkId==machine.networkId) { shaft=true; break; }
            }
            if(!shaft) return false;
            foreach(var layout in powerLayout)
            {
                var engine=layout.building;
                if(engine.kind!=BuildKind.SteamEngine || engine.hp<=0f || !layout.unlocked || !engine.powered ||
                    engine.powerGeneration<=0f || engine.networkId!=machine.networkId || layout.inputNetwork==null) continue;
                // An old self-firing boiler or a distant cluster is not evidence for the module chain.
                foreach(var source in powerLayout)
                {
                    var boiler=source.building;
                    if(boiler.kind==BuildKind.Boiler && boiler.hp>0f && boiler.modularBoiler && source.unlocked &&
                        boiler.powerGeneration>0f && boiler.networkId==layout.inputNetwork.id) return true;
                }
            }
            return false;
        }
    }
}
