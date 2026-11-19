using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void ApplyEnemyStatus(PlayerState player, float infectionDelta, float temperatureDelta)
        {
            // Legacy/industrial worlds stay inert. Survival owns the subsequent
            // temperature/infection penalties and uses the same persistent needs.
            if (State.survival == null || !State.survival.enabled || player.needs == null) return;
            player.needs.infection = Clamp(player.needs.infection + infectionDelta, 0f, 100f);
            player.needs.bodyTemperature = Clamp(player.needs.bodyTemperature + temperatureDelta, 25f, 45f);
        }

        private bool EnemySpecial(ZombieState zombie, PlayerState player, float distanceSquared)
        {
            int variant = zombie.variant;
            if (variant < 3) return false;
            bool night = EnemyCatalog.IsNight(State.elapsed, State.dayLength);
            if (variant == 11) return !night && distanceSquared > 49f && zombie.alertedUntil <= State.elapsed;
            EnemyStats stats = EnemyCatalog.Stats(variant);
            if (stats.specialRange <= 0 || distanceSquared > stats.specialRange * stats.specialRange ||
                !LineClear(zombie.x, zombie.y, player.x, player.y, RoundCell(player.x), RoundCell(player.y))) return false;
            if (variant == 7)
            {
                if (zombie.attackCooldown <= 0f)
                {
                    zombie.attackCooldown = stats.specialInterval;
                    foreach (ZombieState other in State.zombies)
                        if (other.hp > 0f && DistSq(zombie.x, zombie.y, other.x, other.y) <= 196f)
                            other.alertedUntil = Math.Max(other.alertedUntil, State.elapsed + 8f);
                    AddTrace(zombie.x, zombie.y, player.x, player.y, true, 8);
                }
                return false; // Roaring shares the attack cooldown and never grants a second hit.
            }
            if (variant == 10)
            {
                if (zombie.attackCooldown <= 0f) DetonateEnemy(zombie);
                return true;
            }
            if (variant == 4 && distanceSquared <= 1.05f * 1.05f) return false;
            if (variant != 4 && variant != 6 && variant != 8 && variant != 9) return false;
            if (zombie.attackCooldown <= 0f)
            {
                zombie.attackCooldown = stats.specialInterval;
                if (variant == 4 || variant == 9)
                {
                    EnemyDamagePlayer(player, stats.specialDamage, variant);
                    AddTrace(zombie.x, zombie.y, player.x, player.y, true, variant == 4 ? 3 : 6);
                }
                else EnemyAreaPulse(zombie, stats.specialRange, stats.specialDamage, variant == 6 ? 8f : 0f, false);
            }
            return true;
        }

        private void EnemyDamagePlayer(PlayerState player, float amount, int variant)
        {
            float before = player.hp;
            DamagePlayer(player, amount, true);
            if (player.hp >= before) return; // Spawn protection also blocks stamina/status effects.
            if (variant == 3) player.stamina = Math.Max(0f, player.stamina - 16f);
            else if (variant == 8) player.stamina = Math.Max(0f, player.stamina - 8f);
            else if (variant == 9) player.stamina = Math.Max(0f, player.stamina - 24f);
            float infection = variant == 8 ? 6f : 0f;
            float temperature = variant == 6 ? .6f : variant == 9 ? -1.5f : 0f;
            if (infection != 0 || temperature != 0) ApplyEnemyStatus(player, infection, temperature);
        }

        private struct EnemyPlayerHit { public PlayerState player; public float amount; }
        private struct EnemyBuildingHit { public BuildingState building; public float amount; }
        private struct EnemyNodeHit { public NodeState node; public float amount; }

        private static float EnemyBuildingDistanceSquared(BuildingState building, float x, float y, out float hitX, out float hitY)
        {
            hitX = Math.Max(building.x - .49f, Math.Min(building.x + DemoCatalog.Width(building) - .51f, x));
            hitY = Math.Max(building.y - .49f, Math.Min(building.y + DemoCatalog.Height(building) - .51f, y));
            float dx = hitX - x, dy = hitY - y; return dx * dx + dy * dy;
        }

        private void EnemyAreaPulse(ZombieState zombie, float radius, float playerDamage, float structureDamage, bool explosion)
        {
            AddTrace(zombie.x, zombie.y, zombie.x, zombie.y, true, explosion ? 7 : zombie.variant == 6 ? 4 : 5);
            var players = new List<EnemyPlayerHit>(); var buildings = new List<EnemyBuildingHit>(); var nodes = new List<EnemyNodeHit>();
            // Snapshot visibility before dealing ANY damage: a wall destroyed by this pulse
            // cannot expose victims behind it during the same pulse. Multi-cell machines
            // are selected once by their nearest footprint point, never by each grid cell.
            foreach (PlayerState player in State.players)
            {
                float distance = DistSq(zombie.x, zombie.y, player.x, player.y);
                if (!player.connected || player.hp <= 0 || distance > radius * radius ||
                    !LineClear(zombie.x, zombie.y, player.x, player.y, RoundCell(player.x), RoundCell(player.y))) continue;
                float scale = explosion ? Math.Max(.35f, 1f - (float)Math.Sqrt(distance) / radius) : 1f;
                players.Add(new EnemyPlayerHit { player = player, amount = playerDamage * scale });
            }
            if (structureDamage > 0) foreach (BuildingState building in State.buildings)
            {
                float hitX, hitY, distance = EnemyBuildingDistanceSquared(building, zombie.x, zombie.y, out hitX, out hitY);
                if (distance > radius * radius || !LineClear(zombie.x, zombie.y, hitX, hitY, RoundCell(hitX), RoundCell(hitY))) continue;
                float scale = explosion ? Math.Max(.35f, 1f - (float)Math.Sqrt(distance) / radius) : 1f;
                buildings.Add(new EnemyBuildingHit { building = building, amount = structureDamage * scale });
            }
            if (explosion) foreach (NodeState node in State.nodes)
            {
                float distance = DistSq(zombie.x, zombie.y, node.x, node.y);
                if (distance <= radius * radius && LineClear(zombie.x, zombie.y, node.x, node.y, node.x, node.y))
                    nodes.Add(new EnemyNodeHit { node = node, amount = structureDamage * Math.Max(.35f, 1f - (float)Math.Sqrt(distance) / radius) });
            }
            foreach (EnemyPlayerHit hit in players)
            {
                EnemyDamagePlayer(hit.player, hit.amount, zombie.variant);
                AddTrace(zombie.x, zombie.y, hit.player.x, hit.player.y, true);
            }
            foreach (EnemyBuildingHit hit in buildings)
            {
                hit.building.hp = Math.Max(0f, hit.building.hp - hit.amount);
                if (hit.building.hp <= 0f) RemoveBuilding(hit.building);
                AddTrace(zombie.x, zombie.y, hit.building.x, hit.building.y, true);
            }
            foreach (EnemyNodeHit hit in nodes)
            {
                hit.node.hp = Math.Max(0f, hit.node.hp - hit.amount);
                if (hit.node.hp <= 0f) RemoveNode(hit.node);
                AddTrace(zombie.x, zombie.y, hit.node.x, hit.node.y, true);
            }
        }

        private void DetonateEnemy(ZombieState zombie)
        {
            if (zombie.hp <= 0f) return;
            zombie.hp = 0f; State.zombies.Remove(zombie);
            // Self-destruction grants no kill/XP/loot. It only occurs during the AI phase,
            // never in DamageZombie/Shoot/turret enumeration or another damage callback.
            EnemyAreaPulse(zombie, 3f, EnemyCatalog.Stats(10).specialDamage, 75f, true);
        }

        private int SelectSpawnEnemy(int legacyVariant, int x, int y, uint roll)
        {
            if (State.generatorVersion <= 0 || State.wave < 2) return legacyVariant;
            BiomeKind biome = BiomeKind.Meadow;
            int encoded = WorldTiles.Biome(State, x, y) - '0';
            if (encoded >= 0 && encoded <= 5) biome = (BiomeKind)encoded;
            DistrictKind district = DistrictKind.Wilderness; float nearest = 100f; int nearestId = int.MaxValue;
            if (State.sites != null) foreach (SiteState site in State.sites)
            {
                float dx = Math.Max(site.x - x, Math.Max(0, x - (site.x + site.width - 1)));
                float dy = Math.Max(site.y - y, Math.Max(0, y - (site.y + site.height - 1)));
                float distance = dx * dx + dy * dy;
                if (distance < nearest || (distance == nearest && site.id < nearestId))
                { district = site.district; nearest = distance; nearestId = site.id; }
            }
            // Decouple selection from the low bits already consumed for spawn side/position.
            unchecked { roll ^= roll >> 16; roll *= 2246822519u; roll ^= roll >> 13; }
            return EnemyCatalog.SelectVariant(State.wave, biome, district, EnemyCatalog.IsNight(State.elapsed, State.dayLength), roll);
        }
    }
}
