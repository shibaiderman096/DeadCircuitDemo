namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void ResearchEquipment(PlayerState player, ResearchDomain domain, int id)
        {
            string requirement = domain == ResearchDomain.Weapon
                ? WeaponTechCatalog.ResearchRequirement(State, player, id)
                : ToolTechCatalog.ResearchRequirement(State, player, id);
            if (requirement.Length != 0)
            {
                Notice(player, requirement);
                return;
            }

            var definition = domain == ResearchDomain.Weapon
                ? WeaponTechCatalog.Definition(id)
                : ToolTechCatalog.Definition(id);
            Pay(player.items, definition.cost);
            if (domain == ResearchDomain.Weapon) State.weaponResearch |= 1 << id;
            else State.toolResearch |= 1 << id;
            player.interactCooldown = .5f;
            State.announcement = "全队" + (domain == ResearchDomain.Weapon ? "武器" : "工具") + "研究已解锁：" + definition.name;
            State.announcementUntil = State.elapsed + 8f;
            Notice(player, State.announcement);
        }

        private static void CopyEquipmentResearchProgression(WorldState source, WorldState target)
        {
            SaveCheck(source.weaponResearch >= 0 && source.weaponResearch <= WeaponTechCatalog.AllResearch, "weapon research bits");
            SaveCheck(source.toolResearch >= 0 && source.toolResearch <= ToolTechCatalog.AllResearch, "tool research bits");
            SaveCheck(EquipmentResearchCatalog.ResearchStateValid(source), "equipment research state");
            foreach (var definition in WeaponTechCatalog.All)
            {
                if (!WeaponTechCatalog.HasResearch(source, definition.id)) continue;
                SaveCheck(WeaponTechCatalog.ResearchEvidence(source, definition.id).Length == 0, "weapon research production evidence");
            }
            foreach (var definition in ToolTechCatalog.All)
            {
                if (!ToolTechCatalog.HasResearch(source, definition.id)) continue;
                SaveCheck(ToolTechCatalog.ResearchEvidence(source, definition.id).Length == 0, "tool research production evidence");
            }
            target.weaponResearch = source.weaponResearch;
            target.toolResearch = source.toolResearch;
        }
    }
}
