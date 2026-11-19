namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private static void CopyWaterProgression(WorldState source,WorldState target)
        {
            // Historical worlds retain their existing machinery and era. No invented
            // research, output or ecological regeneration is needed to load them.
            if(source.version<12) return;
            SaveCheck(source.waterVersion==0 || source.waterVersion==1,"water version");
            SaveCheck(source.waterResearch>=0 && source.waterResearch<=255,"water research bits");
            int[] counters={source.waterCrushed,source.waterSmelted,source.waterPressed,source.waterGears,
                source.waterBearings,source.waterTransported,source.waterMilled};
            foreach(int count in counters) SaveCheck(count>=0,"water production evidence");
            SaveRange(source.waterIrrigated,0f,10000000f,"water irrigation evidence");
            if(source.waterVersion==0)
            {
                SaveCheck(source.waterResearch==0 && source.waterIrrigated==0f,"water bypass evidence");
                foreach(int count in counters) SaveCheck(count==0,"water bypass evidence");
                return;
            }
            SaveCheck(source.waterResearch==0 || source.era>=TechEra.Water,"water research era");
            foreach(var definition in WaterCatalog.All)
            {
                if((source.waterResearch & (1<<(int)definition.id))==0) continue;
                SaveCheck((source.waterResearch & definition.prerequisites)==definition.prerequisites,"water research prerequisites");
            }
            if((source.waterResearch & (1<<(int)WaterTech.Agriculture))!=0)
                SaveCheck(PrimitiveCatalog.IsCookingUnlocked(source),"water agriculture prerequisite");
            WaterTech[] research={WaterTech.Crushing,WaterTech.Blast,WaterTech.Pressing,WaterTech.Components,
                WaterTech.Components,WaterTech.Transport,WaterTech.Agriculture};
            for(int i=0;i<counters.Length;i++)
                SaveCheck(counters[i]==0 || WaterCatalog.HasResearch(source,research[i]),"water evidence research");
            SaveCheck(source.waterIrrigated==0f || WaterCatalog.HasResearch(source,WaterTech.Agriculture),"water irrigation research");
            if(source.era>TechEra.Water) SaveCheck(WaterCatalog.SteamRequirement(source).Length==0,"water steam acceptance");
            target.waterVersion=1; target.waterResearch=source.waterResearch;
            target.waterCrushed=source.waterCrushed; target.waterSmelted=source.waterSmelted;
            target.waterPressed=source.waterPressed; target.waterGears=source.waterGears;
            target.waterBearings=source.waterBearings; target.waterTransported=source.waterTransported;
            target.waterMilled=source.waterMilled; target.waterIrrigated=source.waterIrrigated;
        }
    }
}
