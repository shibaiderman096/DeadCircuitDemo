namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private static void CopySteamProgression(WorldState source,WorldState target)
        {
            SaveCheck(source.steamResearch>=0 && source.steamResearch<=255,"steam research bits");
            SaveCheck(source.steamResearch==0 || source.era>=TechEra.Steam,"steam research era");
            foreach(var d in SteamCatalog.All)
                if(SteamCatalog.HasResearch(source,d.id))
                    SaveCheck((source.steamResearch & d.prerequisites)==d.prerequisites,"steam research prerequisites");
            if(SteamCatalog.HasResearch(source,SteamTech.Coking))
                SaveCheck(PrimitiveCatalog.HasResearch(source,PrimitiveTech.Charcoal),"steam coking primitive prerequisite");
            if(SteamCatalog.HasResearch(source,SteamTech.Refractories))
                SaveCheck(PrimitiveCatalog.HasResearch(source,PrimitiveTech.Ceramics),"steam refractory primitive prerequisite");
            if(source.steamResearch!=0) SaveCheck(WaterCatalog.SteamRequirement(source).Length==0,"steam water acceptance");
            if(SteamCatalog.HasResearch(source,SteamTech.PigIron))
                SaveCheck(WaterCatalog.HasResearch(source,WaterTech.Crushing) && WaterCatalog.HasResearch(source,WaterTech.Blast),"steam blast prerequisite");
            if(SteamCatalog.HasResearch(source,SteamTech.Machining))
                SaveCheck(WaterCatalog.HasResearch(source,WaterTech.Pressing) && WaterCatalog.HasResearch(source,WaterTech.Components),"steam machining water prerequisite");
            int[] counters={source.steamCoked,source.steamRefractory,source.steamPigIron,source.steamSteel,
                source.steamPlates,source.steamFittings,source.steamDriven};
            SteamTech[] required={SteamTech.Coking,SteamTech.Refractories,SteamTech.PigIron,SteamTech.Refining,
                SteamTech.Machining,SteamTech.Piping,SteamTech.Drive};
            for(int i=0;i<counters.Length;i++)
                SaveCheck(counters[i]>=0 && (counters[i]==0 || SteamCatalog.HasResearch(source,required[i])),"steam production evidence");
            if(SteamCatalog.HasResearch(source,SteamTech.PigIron)) SaveCheck(source.steamCoked>=3 && source.steamRefractory>=4,"steam iron prerequisites");
            if(SteamCatalog.HasResearch(source,SteamTech.Refining)) SaveCheck(source.steamPigIron>=3,"steam refining prerequisite");
            if(SteamCatalog.HasResearch(source,SteamTech.Machining)) SaveCheck(source.steamSteel>=2,"steam machining prerequisite");
            if(SteamCatalog.HasResearch(source,SteamTech.Piping)) SaveCheck(source.steamPlates>=2,"steam piping prerequisite");
            if(SteamCatalog.HasResearch(source,SteamTech.Boiler)) SaveCheck(source.steamFittings>=2,"steam boiler prerequisite");
            if(source.era>TechEra.Steam) SaveCheck(SteamCatalog.LowVoltageRequirement(source).Length==0,"steam low voltage acceptance");
            target.steamResearch=source.steamResearch;
            target.steamCoked=source.steamCoked;target.steamRefractory=source.steamRefractory;
            target.steamPigIron=source.steamPigIron;target.steamSteel=source.steamSteel;
            target.steamPlates=source.steamPlates;target.steamFittings=source.steamFittings;target.steamDriven=source.steamDriven;
        }
    }
}
