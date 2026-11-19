namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private static void CopyPrimitiveProgression(WorldState source, WorldState target)
        {
            // Older worlds keep their existing progression and resources. Upgrading a save
            // never opts it into the new research gates or grants any primitive materials.
            target.primitiveVersion = 0;
            target.primitiveResearch = 0;
            if (source.version < 11) return;

            SaveCheck(source.primitiveVersion == 0 || source.primitiveVersion == 1, "primitive version");
            SaveCheck(source.primitiveResearch >= 0 && source.primitiveResearch <= 255, "primitive research bits");
            SaveCheck(source.primitiveVersion != 0 || source.primitiveResearch == 0, "primitive bypass research");
            foreach (var definition in PrimitiveCatalog.All)
            {
                if ((source.primitiveResearch & (1 << (int)definition.id)) == 0) continue;
                SaveCheck((source.primitiveResearch & definition.prerequisites) == definition.prerequisites, "primitive research prerequisites");
            }
            // Validate progression independently of machine unlocks. A fresh world contains
            // an idle StoneFurnace with its default recipe before the research is completed.
            target.primitiveVersion = source.primitiveVersion;
            target.primitiveResearch = source.primitiveResearch;
        }
    }
}
