namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void ResearchPetrochemical(PlayerState p,int arg)
        {
            string requirement=PetrochemicalCatalog.ResearchRequirement(State,p,arg);
            if(requirement.Length!=0) { Notice(p,requirement); return; }
            var d=PetrochemicalCatalog.Definition(arg); Pay(p.items,d.cost);
            State.mvResearch |= 1<<arg; p.interactCooldown=.5f;
            State.announcement="全队石化科技已解锁："+d.name;
            State.announcementUntil=State.elapsed+8f; Notice(p,State.announcement); RefreshPower(false);
        }
        private static void CopyPetrochemicalProgression(WorldState source,WorldState target)
        {
            SaveCheck(source.mvResearch>=0 && source.mvResearch<=255,"MV research bits");
            SaveCheck(source.mvResearch==0 || source.era>=TechEra.MediumVoltage,"MV research era");
            SaveCheck(source.mvSurvey>=0 && source.mvSurvey<=1,"MV survey evidence");
            int[] counts={source.mvSurvey,source.mvExtracted,source.mvPiped,source.mvDistilled,source.mvDiesel,source.mvPolymer,source.mvLubricant,source.mvCooled,source.mvPowered};
            foreach(int n in counts) SaveCheck(n>=0 && (n==0 || source.era>=TechEra.MediumVoltage),"MV production counter");
            foreach(var d in PetrochemicalCatalog.All) if(PetrochemicalCatalog.HasResearch(source,d.id))
            {
                SaveCheck((source.mvResearch & d.prerequisites)==d.prerequisites,"MV research prerequisites");
                SaveCheck(PetrochemicalCatalog.ResearchEvidence(source,d.id).Length==0,"MV research production evidence");
            }
            PetrochemicalTech[] required={PetrochemicalTech.Prospecting,PetrochemicalTech.Extraction,PetrochemicalTech.Extraction,PetrochemicalTech.Distillation,
                PetrochemicalTech.Refining,PetrochemicalTech.Polymerization,PetrochemicalTech.Lubrication,PetrochemicalTech.Cooling,PetrochemicalTech.Transformation};
            for(int i=0;i<required.Length;i++) SaveCheck(counts[i]==0 || PetrochemicalCatalog.HasResearch(source,required[i]),"MV material research evidence");
            SaveCheck(source.mvPowered<=source.mvPolymer,"MV electrical production totals");
            if(source.era>TechEra.MediumVoltage) SaveCheck(PetrochemicalCatalog.HighVoltageRequirement(source).Length==0,"MV high voltage acceptance");
            target.mvResearch=source.mvResearch; target.mvSurvey=source.mvSurvey; target.mvExtracted=source.mvExtracted;
            target.mvPiped=source.mvPiped; target.mvDistilled=source.mvDistilled; target.mvDiesel=source.mvDiesel;
            target.mvPolymer=source.mvPolymer; target.mvLubricant=source.mvLubricant; target.mvCooled=source.mvCooled; target.mvPowered=source.mvPowered;
        }
    }
}
