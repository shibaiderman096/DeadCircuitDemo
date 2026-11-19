using System;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        // Interact calls once after depletion and immediately removes the finite source.
        // Destruction, mining machines and partial harvesting do not award forage.
        private void AwardForageLoot(PlayerState p, NodeState n)
        {
            if (State.generatorVersion < 1 || State.generatorVersion > WorldGeneration.CurrentGeneratorVersion ||
                State.survival == null || !State.survival.enabled ||
                p == null || !p.connected || p.hp <= 0 || FindPlayer(p.id) != p || p.needs == null || !p.needs.initialized ||
                n == null || n.stock != 0 || (n.kind != NodeKind.Tree && n.kind != NodeKind.Crate) ||
                !WorldTiles.Inside(State, n.x, n.y) || !State.nodes.Contains(n) || p.needs.foods == null ||
                p.needs.foods.Length != SurvivalCatalog.FoodCount || p.needs.seeds == null ||
                p.needs.seeds.Length != SurvivalCatalog.CropCount) return;
            int biome = WorldTiles.Biome(State, n.x, n.y) - '0';
            if (biome < 0 || biome > 5) return;
            // Streamed entity IDs follow discovery order; world coordinates do not.
            // Keep non-chunk gen1's node-ID hash unchanged for legacy exact yields.
            uint loot = ChunkStreaming.Enabled(State)
                ? ForageHash(unchecked((int)ForageHash(State.seed, n.x, biome)), n.y, biome)
                : ForageHash(State.seed, n.id, biome);
            if (n.kind == NodeKind.Tree)
            {
                // Depleting a tree includes gathering its undergrowth. No new/free forage action.
                // Meadow/broadleaf: fruit; conifer/wetland/tundra: mushrooms; desert: sparse berries.
                AddForageStack(p.needs.foods, (int)FoodKind.Berries, biome == 3 ? 1 : 1 + (int)(loot % 3));
                if (biome == 0 || biome == 1) AddForageStack(p.needs.foods, (int)FoodKind.Apple, 1 + (int)((loot >> 4) % 2));
                if (biome == 2 || biome == 4 || biome == 5) AddForageStack(p.needs.foods, (int)FoodKind.Mushroom, 1 + (int)((loot >> 6) % 3));
                if ((loot & 3u) == 0) AddForageStack(p.needs.seeds, (int)((loot >> 10) % SurvivalCatalog.CropCount), 1);
            }
            else
            {
                // Finite wilderness supply crates and building/site provisions share NodeKind.Crate.
                // Eggs/fish are guaranteed, so neither cooking ingredient depends on rare RNG luck.
                AddForageStack(p.needs.foods, (int)FoodKind.Egg, 2 + (int)(loot % 2));
                AddForageStack(p.needs.foods, (int)FoodKind.RawFish, 1 + (int)((loot >> 3) % 2));
                AddForageStack(p.needs.foods, (int)FoodKind.Apple, 1 + (int)((loot >> 6) % 2));
                AddForageStack(p.needs.foods, (int)FoodKind.Berries, 1 + (int)((loot >> 8) % 3));
                AddForageStack(p.needs.seeds, (int)((loot >> 12) % SurvivalCatalog.CropCount), 1 + (int)((loot >> 17) & 1u));
            }
            // Capacity overflow is discarded when the source is exhausted, never banked for replay.
            // No industrial item, node stock, weather/wildlife PRNG or new save field is changed.
        }

        private static uint ForageHash(int seed, int nodeId, int biome)
        {
            unchecked
            {
                uint value = (uint)seed ^ (uint)nodeId * 0x9E3779B9u ^ (uint)(biome + 1) * 0x85EBCA6Bu;
                value ^= value >> 16; value *= 0x7FEB352Du;
                value ^= value >> 15; value *= 0x846CA68Bu;
                return value ^ (value >> 16);
            }
        }

        private static void AddForageStack(int[] inventory, int index, int amount)
        {
            // Clamp before addition: even malformed host-side extreme ints cannot wrap or go negative.
            int current = Math.Max(0, Math.Min(SurvivalCatalog.StackLimit, inventory[index]));
            inventory[index] = current + Math.Min(SurvivalCatalog.StackLimit - current, Math.Max(0, amount));
        }
    }
}
