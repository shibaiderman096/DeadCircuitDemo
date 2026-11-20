namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void ResearchPrecision(PlayerState p,int arg)
        {
            string requirement=PrecisionCatalog.ResearchRequirement(State,p,arg);
            if(requirement.Length!=0) { Notice(p,requirement); return; }
            var d=PrecisionCatalog.Definition(arg);
            Pay(p.items,d.cost); State.hvResearch |= 1 << arg; p.interactCooldown=.5f;
            State.announcement="全队高压科技已解锁："+d.name;
            State.announcementUntil=State.elapsed+8f; Notice(p,State.announcement); RefreshPower(false);
        }

        private bool ConfigurePrecision(PlayerState p,BuildingState b,int arg)
        {
            if(b.kind!=BuildKind.PrecisionAssembler || arg<0 || arg>1) return false;
            if(!PrecisionCatalog.HasResearch(State,PrecisionTech.Control)) { Notice(p,"需要高压研究：自动控制。"); return true; }
            b.setting=arg;
            Notice(p,arg==0 ? "精密装配机：连续加工。" : "精密装配机：成品和副产物各限8，满仓暂停。");
            return true;
        }

        private static void CopyPrecisionProgression(WorldState source,WorldState target)
        {
            SaveCheck(source.hvResearch>=0 && source.hvResearch<=PrecisionCatalog.AllResearch,"HV research bits");
            SaveCheck(source.hvPowered>=0 && source.hvPowered<=target.produced[(int)ItemKind.ControlModule],"HV powered production");
            SaveCheck(source.era>=TechEra.HighVoltage || (source.hvResearch==0 && source.hvPowered==0),"HV research era");
            SaveCheck(source.hvPowered==0 || PrecisionCatalog.HasResearch(source,PrecisionTech.Grid),"HV grid evidence");
            foreach(var d in PrecisionCatalog.All) if(PrecisionCatalog.HasResearch(source,d.id))
            {
                SaveCheck((source.hvResearch & d.prerequisites)==d.prerequisites,"HV research prerequisites");
                SaveCheck(PrecisionCatalog.ResearchEvidence(source,d.id).Length==0,"HV research production evidence");
            }
            if(source.era==TechEra.Nuclear) SaveCheck(PrecisionCatalog.NuclearRequirement(source).Length==0,"HV nuclear acceptance");
            target.hvResearch=source.hvResearch; target.hvPowered=source.hvPowered;
        }
    }
}
