namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void ResearchPrimitive(PlayerState p, int arg)
        {
            if (p.interactCooldown > 0f) return;
            string requirement = PrimitiveCatalog.ResearchRequirement(State, p, arg);
            if (requirement.Length != 0) { Notice(p, requirement); return; }
            var definition = PrimitiveCatalog.Definition(arg);
            // Apply serializes commands; every gate precedes payment and the shared bit.
            Pay(p.items, definition.cost);
            State.primitiveResearch |= 1 << arg;
            p.interactCooldown = .5f;
            State.announcement = "全队原始科技已解锁：" + definition.name;
            State.announcementUntil = State.elapsed + 12f;
            Notice(p, State.announcement);
        }
    }
}
