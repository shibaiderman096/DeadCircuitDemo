namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void ResearchNuclear(PlayerState player,int id)
        {
            string requirement=NuclearCatalog.ResearchRequirement(State,player,id);
            if(requirement.Length!=0) { Notice(player,requirement); return; }
            var definition=NuclearCatalog.Definition(id);
            Pay(player.items,definition.cost);
            State.nuclearResearch|=1<<id;
            player.interactCooldown=.5f;
            State.announcement="全队核工业科技已解锁："+definition.name;
            State.announcementUntil=State.elapsed+8f;
            Notice(player,State.announcement);
            RefreshPower(false);
        }

        private static void CopyNuclearProgression(WorldState source,WorldState target)
        {
            SaveCheck(source.nuclearResearch>=0 && source.nuclearResearch<=NuclearCatalog.AllResearch,"nuclear research bits");
            SaveCheck(source.era>=TechEra.Nuclear || source.nuclearResearch==0,"nuclear research era");
            foreach(var definition in NuclearCatalog.All) if(NuclearCatalog.HasResearch(source,definition.id))
            {
                SaveCheck((source.nuclearResearch & definition.prerequisites)==definition.prerequisites,"nuclear research prerequisites");
                SaveCheck(NuclearCatalog.ResearchEvidence(source,definition.id).Length==0,"nuclear research production evidence");
            }
            target.nuclearResearch=source.nuclearResearch;
        }
    }
}
