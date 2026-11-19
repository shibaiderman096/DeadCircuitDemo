namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void ResearchWater(PlayerState p,int arg)
        {
            if(p.interactCooldown>0f) return;
            string requirement=WaterCatalog.ResearchRequirement(State,p,arg);
            if(requirement.Length!=0) { Notice(p,requirement); return; }
            var definition=WaterCatalog.Definition(arg);
            Pay(p.items,definition.cost);
            State.waterResearch |= 1<<arg;
            p.interactCooldown=.5f;
            State.announcement="全队水力科技已解锁："+definition.name;
            State.announcementUntil=State.elapsed+8f;
            // Rebuild any cached machine layout after shared research changes.
            RefreshPower(false);
        }
    }
}
