namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void ResearchLowVoltage(PlayerState p,int arg)
        {
            string requirement=LowVoltageCatalog.ResearchRequirement(State,p,arg);
            if(requirement.Length!=0) { Notice(p,requirement); return; }
            var d=LowVoltageCatalog.Definition(arg); Pay(p.items,d.cost);
            State.lvResearch |= 1<<arg; p.interactCooldown=.5f;
            State.announcement="全队低压科技已解锁："+d.name; State.announcementUntil=State.elapsed+8f;
            Notice(p,State.announcement); RefreshPower(false);
        }
        // Inputs have been paid and the complete output batch has reached its real destination.
        private void RecordLowVoltageProduction(BuildingState machine,RecipeDefinition recipe)
        {
            switch(recipe.id)
            {
                case 140: State.lvInsulators=SaturatingAdd(State.lvInsulators,recipe.outputs[(int)ItemKind.Insulator]); break;
                case 141: case 147: State.lvWire=SaturatingAdd(State.lvWire,recipe.outputs[(int)ItemKind.CopperWire]); break;
                case 142: case 148: State.lvCable=SaturatingAdd(State.lvCable,recipe.outputs[(int)ItemKind.Cable]); break;
                case 143: State.lvCoils=SaturatingAdd(State.lvCoils,recipe.outputs[(int)ItemKind.Coil]); break;
                case 144: case 149: State.lvMotors=SaturatingAdd(State.lvMotors,recipe.outputs[(int)ItemKind.Motor]); break;
                case 145: case 150: State.lvRelays=SaturatingAdd(State.lvRelays,recipe.outputs[(int)ItemKind.Relay]); break;
                case 60: State.lvCircuits=SaturatingAdd(State.lvCircuits,recipe.outputs[(int)ItemKind.Circuit]); break;
            }
            if(machine==null || !machine.powered || machine.powerRatio<=0f || machine.networkId<=0 || TechCatalog.EnergyTier(machine)!=2) return;
            bool protectedOutput=IsProtectedLowVoltage(machine);
            foreach(int amount in recipe.outputs)
            {
                State.lvElectric=SaturatingAdd(State.lvElectric,amount);
                if(machine.kind==BuildKind.Assembler) State.lvAutomated=SaturatingAdd(State.lvAutomated,amount);
                if(protectedOutput) State.lvProtected=SaturatingAdd(State.lvProtected,amount);
            }
        }
        private static void CopyLowVoltageProgression(WorldState source,WorldState target)
        {
            SaveCheck(source.lvResearch>=0 && source.lvResearch<=255,"LV research bits");
            SaveCheck(source.lvResearch==0 || source.era>=TechEra.LowVoltage,"LV research era");
            if(source.lvResearch!=0) SaveCheck(SteamCatalog.LowVoltageRequirement(source).Length==0,"LV steam acceptance");
            foreach(var d in LowVoltageCatalog.All) if(LowVoltageCatalog.HasResearch(source,d.id))
            {
                SaveCheck((source.lvResearch & d.prerequisites)==d.prerequisites,"LV research prerequisites");
                SaveCheck(LowVoltageCatalog.ResearchEvidence(source,d.id).Length==0,"LV research production evidence");
            }
            int[] counts={source.lvWire,source.lvInsulators,source.lvCable,source.lvCoils,source.lvMotors,source.lvRelays,source.lvCircuits,source.lvElectric,source.lvAutomated,source.lvSorted,source.lvProtected};
            foreach(int n in counts) SaveCheck(n>=0 && (n==0 || source.era>=TechEra.LowVoltage),"LV production counter");
            LowVoltageTech[] required={LowVoltageTech.Cabling,LowVoltageTech.Generation,LowVoltageTech.Motors,LowVoltageTech.Relays,LowVoltageTech.Relays};
            for(int i=0;i<required.Length;i++) SaveCheck(counts[i+2]==0 || LowVoltageCatalog.HasResearch(source,required[i]),"LV material research evidence");
            SaveCheck(source.lvAutomated==0 || LowVoltageCatalog.HasResearch(source,LowVoltageTech.Assembly),"LV assembly evidence");
            SaveCheck(source.lvSorted==0 || LowVoltageCatalog.HasResearch(source,LowVoltageTech.Sorting),"LV sorting evidence");
            SaveCheck(source.lvProtected==0 || LowVoltageCatalog.HasResearch(source,LowVoltageTech.Protection),"LV protection evidence");
            SaveCheck(source.lvCircuits<=source.lvElectric && source.lvAutomated<=source.lvElectric && source.lvProtected<=source.lvElectric,"LV electrical evidence totals");
            if(source.era>TechEra.LowVoltage) SaveCheck(LowVoltageCatalog.MediumVoltageRequirement(source).Length==0,"LV middle voltage acceptance");
            target.lvResearch=source.lvResearch; target.lvWire=source.lvWire; target.lvInsulators=source.lvInsulators;
            target.lvCable=source.lvCable; target.lvCoils=source.lvCoils; target.lvMotors=source.lvMotors; target.lvRelays=source.lvRelays;
            target.lvCircuits=source.lvCircuits; target.lvElectric=source.lvElectric; target.lvAutomated=source.lvAutomated;
            target.lvSorted=source.lvSorted; target.lvProtected=source.lvProtected;
        }
    }
}
