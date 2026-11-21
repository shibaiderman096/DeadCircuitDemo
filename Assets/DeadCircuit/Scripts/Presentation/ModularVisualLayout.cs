namespace DeadCircuit
{
    /// <summary>Read-only, Unity-independent geometry for a single machine's sparse modules.
    /// The enclosing frame is a visual annotation, never an occupied footprint.</summary>
    public static class ModularVisualLayout
    {
        public const int East = 1, North = 2, West = 4, South = 8;
        public const float FramePadding = .12f;

        public readonly struct Bounds
        {
            public readonly float Left, Right, Bottom, Top;
            public readonly bool HasCells;

            internal Bounds(int minX, int maxX, int minY, int maxY)
            {
                Left = minX - .5f - FramePadding;
                Right = maxX + .5f + FramePadding;
                Bottom = minY - .5f - FramePadding;
                Top = maxY + .5f + FramePadding;
                HasCells = true;
            }
        }

        // Membership is deliberately restricted to this BuildingState: nearby machines
        // and diagonal cells never contribute, even when they have the same BuildKind.
        public static int ConnectionMask(BuildingState building, int moduleIndex)
        {
            int count = ModularMachineCatalog.ModuleCount(building);
            if (moduleIndex < 0 || moduleIndex >= count) return 0;
            var module = building.modularModules[moduleIndex];
            if (module == null) return 0;
            int mask = 0;
            for (int i = 0; i < count; i++)
            {
                var next = building.modularModules[i];
                if (i == moduleIndex || next == null) continue;
                // Widen before subtraction so opposite integer limits cannot wrap
                // around into a false one-cell connection.
                long dx = (long)next.x - module.x, dy = (long)next.y - module.y;
                if (dy == 0)
                {
                    if (dx == 1) mask |= East;
                    else if (dx == -1) mask |= West;
                }
                else if (dx == 0)
                {
                    if (dy == 1) mask |= North;
                    else if (dy == -1) mask |= South;
                }
            }
            return mask;
        }

        public static Bounds FrameBounds(BuildingState building)
        {
            int count = ModularMachineCatalog.ModuleCount(building);
            bool found = false;
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            for (int i = 0; i < count; i++)
            {
                var module = building.modularModules[i];
                if (module == null) continue;
                found = true;
                if (module.x < minX) minX = module.x;
                if (module.x > maxX) maxX = module.x;
                if (module.y < minY) minY = module.y;
                if (module.y > maxY) maxY = module.y;
            }
            return found ? new Bounds(minX, maxX, minY, maxY) : default;
        }
    }
}
