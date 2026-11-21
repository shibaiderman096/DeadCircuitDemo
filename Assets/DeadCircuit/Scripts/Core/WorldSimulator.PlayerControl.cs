using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private enum PlayerControlIntent { None, Walk, Primary, Secondary, Work, Fight }
        private enum PlayerControlTarget { None, Node, Building, Stair, Repair, Water, Plot, Corpse, Zombie, Creature }
        private const int ControlPathRadius = 64, ControlPathNodes = 4096;
        private const float ControlRadius = .28f;

        private void ClearPlayerControl(PlayerState p, HeldInput input)
        {
            if (input.intent != PlayerControlIntent.None)
            { input.x = input.y = 0; input.sprint = false; p.moving = p.aiming = false; }
            input.intent = PlayerControlIntent.None; input.route = null; input.routeIndex = 0;
            input.travelDistance = 0; input.targetKind = PlayerControlTarget.None; input.targetId = 0;
            input.holdStopped = false;
        }

        private bool ApplyPlayerControl(PlayerState p, HeldInput input, PlayerCommand cmd)
        {
            if (cmd.action == "cancel")
            { ClearPlayerControl(p, input); input.x = input.y = 0; input.sprint = false; p.moving = p.aiming = false; return true; }
            if (cmd.action == "work" || cmd.action == "fight")
            {
                if (cmd.action == "fight" && cmd.arg != 0 && cmd.arg != 1) return true;
                if (p.hp <= 0 || (input.intent == PlayerControlIntent.None && (input.x != 0 || input.y != 0))) return true;
                PlayerControlIntent intent = cmd.action == "work" ? PlayerControlIntent.Work : PlayerControlIntent.Fight;
                if (input.intent != intent)
                { ClearPlayerControl(p, input); input.x = input.y = 0; input.sprint = false; input.intent = intent; }
                input.allowNeutral = cmd.arg == 1;
                input.refreshedAt = State.tick; return true;
            }
            if (cmd.action != "walk" && cmd.action != "primary" && cmd.action != "secondary") return false;
            if (input.intent == PlayerControlIntent.Primary && cmd.action == "primary" &&
                cmd.x == input.targetX && cmd.y == input.targetY) return true;
            ClearPlayerControl(p, input); input.x = input.y = 0; input.sprint = false;
            if (p.hp <= 0 || !Inside(cmd.x, cmd.y)) return true;
            input.intent = PlayerControlIntent.Walk; input.targetX = cmd.x; input.targetY = cmd.y;
            if (cmd.action == "primary" || cmd.action == "secondary")
            {
                RefreshSpatial();
                if (cmd.action == "primary" && BindPlayerControlEnemy(input, cmd))
                { input.intent = PlayerControlIntent.Primary; return true; }
                BuildingState building = BuildingAt(cmd.x, cmd.y); NodeState node = NodeAt(cmd.x, cmd.y);
                bool secondary = cmd.action == "secondary";
                if (!secondary && FloorWorld.IsStairCell(State, cmd.x, cmd.y)) input.targetKind = PlayerControlTarget.Stair;
                else if (building != null)
                {
                    if (!secondary || building.kind == BuildKind.Door) input.targetKind = PlayerControlTarget.Building;
                    else if (building.hp < BuildingMaxHealth(building.kind)) input.targetKind = PlayerControlTarget.Repair;
                    input.targetId = building.id;
                }
                else if (!secondary && node != null) { input.targetKind = PlayerControlTarget.Node; input.targetId = node.id; }
                else if (State.survival != null && State.survival.enabled)
                {
                    if (secondary && WorldTiles.Ground(State, cmd.x, cmd.y) == '2') input.targetKind = PlayerControlTarget.Water;
                    FarmPlotState plot = SurvivalPlotAt(cmd.x, cmd.y);
                    if (plot != null && plot.crop >= 0 && plot.growth >= 1 && plot.health > 0)
                    { input.targetKind = PlayerControlTarget.Plot; input.targetId = plot.id; }
                    foreach (CreatureState corpse in State.survival.creatures)
                        if (corpse.hp <= 0 && corpse.meatLeft > 0 &&
                            (cmd.arg > 0 ? corpse.id == cmd.arg : RoundCell(corpse.x) == cmd.x && RoundCell(corpse.y) == cmd.y))
                        { input.targetKind = PlayerControlTarget.Corpse; input.targetId = corpse.id; input.targetX = RoundCell(corpse.x); input.targetY = RoundCell(corpse.y); break; }
                }
                if (input.targetKind != PlayerControlTarget.None)
                    input.intent = secondary ? PlayerControlIntent.Secondary : PlayerControlIntent.Primary;
                else if (secondary) ClearPlayerControl(p, input);
            }
            return true;
        }

        private void UpdatePlayerControl(PlayerState p, HeldInput input)
        {
            if (input.intent == PlayerControlIntent.None) return;
            input.x = input.y = 0; input.travelDistance = 0;
            if (input.intent == PlayerControlIntent.Work || input.intent == PlayerControlIntent.Fight)
            {
                if (State.tick - input.refreshedAt >= 7) { ClearPlayerControl(p, input); return; }
                if (input.holdStopped) return;
                if (input.intent == PlayerControlIntent.Fight && input.targetKind != PlayerControlTarget.None && !PlayerControlTargetValid(p, input))
                    StopPlayerControlTarget(p, input, false);
                if (input.targetKind == PlayerControlTarget.None &&
                    !(input.intent == PlayerControlIntent.Work ? FindPlayerControlWork(p, input) : FindPlayerControlEnemy(p, input))) return;
            }
            if (input.intent != PlayerControlIntent.Walk)
            {
                if (!PlayerControlTargetValid(p, input)) { StopPlayerControlTarget(p, input, true); return; }
                if (PlayerControlAtGoal(p, p.x, p.y, input))
                {
                    if (PlayerControlCombatTarget(input))
                    {
                        if (!PlayerControlWeaponReady(p, input)) return;
                        Aim(p, input.focusX - p.x, input.focusY - p.y);
                        Attack(p, input.targetId); return;
                    }
                    if (p.interactCooldown > 0) return;
                    Aim(p, (input.targetKind == PlayerControlTarget.Corpse ? input.focusX : input.targetX) - p.x,
                        (input.targetKind == PlayerControlTarget.Corpse ? input.focusY : input.targetY) - p.y);
                    NodeState target = input.targetKind == PlayerControlTarget.Node ? NodeAt(input.targetX, input.targetY) : null;
                    float hpBefore = target?.hp ?? 0f;
                    PerformPlayerControlInteraction(p, input);
                    if (target == null || NodeAt(input.targetX, input.targetY) != target) StopPlayerControlTarget(p, input, false);
                    else if (target.hp == hpBefore) StopPlayerControlTarget(p, input, true);
                    else if (!PlayerControlTargetValid(p, input)) StopPlayerControlTarget(p, input, true);
                    return;
                }
            }
            else if (PlayerControlAtGoal(p, p.x, p.y, input))
            { ClearPlayerControl(p, input); return; }
            if (input.route == null && !FindPlayerControlRoute(p, input))
            { Notice(p, "无法到达目标：道路受阻或超出寻路范围。"); StopPlayerControlTarget(p, input, true); return; }
            while (input.routeIndex < input.route.Count)
            {
                long cell = input.route[input.routeIndex];
                float tx = WorldTiles.X(cell), ty = WorldTiles.Y(cell);
                float dx = tx - p.x, dy = ty - p.y;
                if (dx * dx + dy * dy < .0001f) { input.routeIndex++; continue; }
                if (!PlayerControlSegmentClear(p.x, p.y, tx, ty))
                { input.route = null; return; }
                input.travelDistance = (float)Math.Sqrt(dx * dx + dy * dy);
                Normalize(ref dx, ref dy, true); input.x = dx; input.y = dy; Aim(p, dx, dy); return;
            }
            ClearPlayerControl(p, input);
        }

        private void StopPlayerControlTarget(PlayerState p, HeldInput input, bool failed)
        {
            if (input.intent != PlayerControlIntent.Work && input.intent != PlayerControlIntent.Fight) { ClearPlayerControl(p, input); return; }
            input.targetKind = PlayerControlTarget.None; input.targetId = 0; input.route = null; input.routeIndex = 0;
            input.x = input.y = 0; input.travelDistance = 0; input.holdStopped = failed;
        }

        private static bool PlayerControlCombatTarget(HeldInput input) =>
            input.targetKind == PlayerControlTarget.Zombie || input.targetKind == PlayerControlTarget.Creature;

        private void SetPlayerControlEnemy(HeldInput input, int id, float x, float y, bool creature)
        {
            int tx = RoundCell(x), ty = RoundCell(y);
            if (input.targetX != tx || input.targetY != ty || input.targetId != id)
            { input.route = null; input.routeIndex = 0; }
            input.targetId = id; input.targetX = tx; input.targetY = ty; input.focusX = x; input.focusY = y;
            input.targetKind = creature ? PlayerControlTarget.Creature : PlayerControlTarget.Zombie;
        }

        private bool BindPlayerControlEnemy(HeldInput input, PlayerCommand cmd)
        {
            foreach (ZombieState zombie in State.zombies)
                if (zombie.hp > 0 && (cmd.arg > 0 ? zombie.id == cmd.arg : RoundCell(zombie.x) == cmd.x && RoundCell(zombie.y) == cmd.y))
                { SetPlayerControlEnemy(input, zombie.id, zombie.x, zombie.y, false); return true; }
            if (State.survival != null && State.survival.enabled) foreach (CreatureState creature in State.survival.creatures)
                if (creature.hp > 0 && (cmd.arg > 0 ? creature.id == cmd.arg : RoundCell(creature.x) == cmd.x && RoundCell(creature.y) == cmd.y))
                { SetPlayerControlEnemy(input, creature.id, creature.x, creature.y, true); return true; }
            return false;
        }

        private bool PlayerControlHostileCreature(PlayerState p, CreatureState creature)
        {
            var definition = SurvivalCatalog.Creatures[(int)creature.kind];
            return definition.damage > 0 && !definition.huntsSmallPrey &&
                (DistSq(p.x, p.y, creature.x, creature.y) < definition.aggroRange * definition.aggroRange ||
                    (definition.temperament == CreatureTemperament.Defensive && creature.fleeTicks > 0));
        }

        private bool FindPlayerControlEnemy(PlayerState p, HeldInput input)
        {
            float best = 64; int id = 0; float x = 0, y = 0; bool animal = false;
            foreach (ZombieState zombie in State.zombies)
            {
                float distance = DistSq(p.x, p.y, zombie.x, zombie.y);
                if (zombie.hp <= 0 || distance > best || (distance == best && id > 0 && id < zombie.id)) continue;
                best = distance; id = zombie.id; x = zombie.x; y = zombie.y; animal = false;
            }
            if (State.survival != null && State.survival.enabled) foreach (CreatureState creature in State.survival.creatures)
            {
                float distance = DistSq(p.x, p.y, creature.x, creature.y);
                if (creature.hp <= 0 || (!input.allowNeutral && !PlayerControlHostileCreature(p, creature)) ||
                    distance > best || (distance == best && id > 0 && id < creature.id)) continue;
                best = distance; id = creature.id; x = creature.x; y = creature.y; animal = true;
            }
            if (id == 0) return false;
            SetPlayerControlEnemy(input, id, x, y, animal); return true;
        }

        private bool PlayerControlWeaponReady(PlayerState p, HeldInput input)
        {
            GearState gear = EquipmentCatalog.Equipped(p);
            if (gear != null && (EquipmentCatalog.IsArmor(gear.kind) || gear.durability <= 0))
            { Notice(p, "装备无法攻击，请修理或切换武器。"); StopPlayerControlTarget(p, input, true); return false; }
            if (p.reloadingGearId != 0) return false;
            if (gear != null && EquipmentCatalog.IsGun(gear.kind) && gear.magazine <= 0)
            { Notice(p, "空弹匣，请先装填。"); StopPlayerControlTarget(p, input, true); return false; }
            return true;
        }

        private void PerformPlayerControlInteraction(PlayerState p, HeldInput input)
        {
            string action = input.targetKind == PlayerControlTarget.Water ? "collectwater" :
                input.targetKind == PlayerControlTarget.Plot ? "harvest" : input.targetKind == PlayerControlTarget.Corpse ? "butcher" : null;
            if (action != null)
            {
                ApplySurvival(p, new PlayerCommand { playerId = p.id, action = action, x = input.targetX, y = input.targetY,
                    arg = input.targetKind == PlayerControlTarget.Corpse ? input.targetId : 0 });
                return;
            }
            p.interactCooldown = .25f;
            if (input.targetKind == PlayerControlTarget.Repair) Repair(p, input.targetX, input.targetY);
            else Interact(p, input.targetX, input.targetY, 0);
        }

        private bool FindPlayerControlWork(PlayerState p, HeldInput input)
        {
            float best = 36f; int selected = 0, tx = 0, ty = 0;
            PlayerControlTarget kind = PlayerControlTarget.None;
            int sx = RoundCell(p.x), sy = RoundCell(p.y);
            for (int y = sy - 6; y <= sy + 6; y++) for (int x = sx - 6; x <= sx + 6; x++)
            {
                NodeState node = NodeAt(x, y);
                if (node == null || node.stock <= 0 || !EquipmentCatalog.CanHarvest(p, node.kind) || BuildingAt(x, y) != null) continue;
                float distance = DistSq(p.x, p.y, x, y);
                if (distance > best || (distance == best && selected != 0 && selected <= node.id)) continue;
                best = distance; selected = node.id; tx = x; ty = y; kind = PlayerControlTarget.Node;
            }
            if (State.survival != null && State.survival.enabled)
            {
                foreach (FarmPlotState plot in State.survival.plots)
                {
                    if (plot.crop < 0 || plot.growth < 1 || plot.health <= 0 || NodeAt(plot.x, plot.y) != null || BuildingAt(plot.x, plot.y) != null) continue;
                    float distance = DistSq(p.x, p.y, plot.x, plot.y);
                    if (distance > best || (distance == best && selected != 0 && selected <= plot.id)) continue;
                    best = distance; selected = plot.id; tx = plot.x; ty = plot.y; kind = PlayerControlTarget.Plot;
                }
                foreach (CreatureState corpse in State.survival.creatures)
                {
                    if (corpse.hp > 0 || corpse.meatLeft <= 0) continue;
                    float distance = DistSq(p.x, p.y, corpse.x, corpse.y);
                    if (distance > best || (distance == best && selected != 0 && selected <= corpse.id)) continue;
                    best = distance; selected = corpse.id; tx = RoundCell(corpse.x); ty = RoundCell(corpse.y); kind = PlayerControlTarget.Corpse;
                }
            }
            if (selected == 0) return false;
            input.targetKind = kind; input.targetId = selected; input.targetX = tx; input.targetY = ty;
            input.route = null; input.routeIndex = 0; return true;
        }

        private bool PlayerControlTargetValid(PlayerState p, HeldInput input)
        {
            if (input.targetKind == PlayerControlTarget.Zombie)
            {
                ZombieState zombie = State.zombies.Find(z => z.id == input.targetId && z.hp > 0);
                if (zombie == null) return false;
                SetPlayerControlEnemy(input, zombie.id, zombie.x, zombie.y, false); return true;
            }
            if (input.targetKind == PlayerControlTarget.Creature)
            {
                CreatureState creature = State.survival == null || !State.survival.enabled ? null : State.survival.creatures.Find(c => c.id == input.targetId && c.hp > 0);
                if (creature == null || (input.intent == PlayerControlIntent.Fight && !input.allowNeutral && !PlayerControlHostileCreature(p, creature))) return false;
                SetPlayerControlEnemy(input, creature.id, creature.x, creature.y, true); return true;
            }
            if (input.targetKind == PlayerControlTarget.Node)
            {
                NodeState node = NodeAt(input.targetX, input.targetY);
                if (node == null || node.id != input.targetId || node.stock <= 0) return false;
                string requirement = HarvestRequirement(p, node.kind);
                if (requirement.Length == 0) return true;
                Notice(p, requirement);
                return false;
            }
            if (input.targetKind == PlayerControlTarget.Building)
            {
                BuildingState building = BuildingAt(input.targetX, input.targetY);
                return building != null && building.id == input.targetId && building.hp > 0;
            }
            if (input.targetKind == PlayerControlTarget.Repair)
            {
                BuildingState b = BuildingAt(input.targetX, input.targetY);
                return b != null && b.id == input.targetId && b.hp > 0 && b.hp < BuildingMaxHealth(b.kind);
            }
            if (State.survival != null && State.survival.enabled && p.needs != null)
            {
                if (input.targetKind == PlayerControlTarget.Water)
                    return CanCollectSurvivalWater(p, input.targetX, input.targetY);
                if (input.targetKind == PlayerControlTarget.Plot)
                {
                    FarmPlotState plot = SurvivalPlotAt(input.targetX, input.targetY);
                    if (plot == null || plot.id != input.targetId || plot.crop < 0 || plot.health <= 0 || plot.growth < 1) return false;
                    if (CanHarvestSurvivalPlot(p, plot, out _)) return true;
                    Notice(p, "背包已满，已停止收获。"); return false;
                }
                if (input.targetKind == PlayerControlTarget.Corpse)
                {
                    CreatureState corpse = State.survival.creatures.Find(c => c.id == input.targetId);
                    if (corpse == null || corpse.hp > 0 || corpse.meatLeft <= 0) return false;
                    input.focusX = corpse.x; input.focusY = corpse.y;
                    if (CanButcherSurvivalCreature(p, corpse)) return true;
                    Notice(p, "背包已满，已停止分解。"); return false;
                }
            }
            return input.targetKind == PlayerControlTarget.Stair && FloorWorld.IsStairCell(State, input.targetX, input.targetY);
        }

        private bool PlayerControlAtGoal(PlayerState p, float x, float y, HeldInput input)
        {
            if (PlayerControlCombatTarget(input))
            {
                GearState gear = EquipmentCatalog.Equipped(p);
                bool gun = gear != null && EquipmentCatalog.IsGun(gear.kind);
                float range = EquipmentCatalog.Stats(gear).range + (gun ? p.attributes[3] * .35f : p.attributes[0] * .015f);
                float dx = input.focusX - x, dy = input.focusY - y, distance = (float)Math.Sqrt(dx * dx + dy * dy);
                return distance <= range && (distance < .0001f || BulletStop(x, y, dx / distance, dy / distance, distance) >= distance - .0001f);
            }
            float interactionRange = input.intent == PlayerControlIntent.Walk ? .01f : Reach;
            float tx = input.targetKind == PlayerControlTarget.Corpse ? input.focusX : input.targetX;
            float ty = input.targetKind == PlayerControlTarget.Corpse ? input.focusY : input.targetY;
            return DistSq(x, y, tx, ty) <= interactionRange * interactionRange &&
                (input.intent == PlayerControlIntent.Walk || LineClear(x, y, tx, ty, input.targetX, input.targetY));
        }

        // Cardinal routes and swept circles forbid diagonal corner cutting, including
        // the initial fractional position. Search work and loaded-world reach are bounded.
        private bool PlayerControlSegmentClear(float x, float y, float tx, float ty)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(DistSq(x, y, tx, ty)) / .12));
            for (int i = 1; i <= steps; i++)
                if (CircleBlocked(x + (tx - x) * i / steps, y + (ty - y) * i / steps, ControlRadius)) return false;
            return true;
        }

        private sealed class PlayerPathNode
        {
            public long cell, parent;
            public int distance, score;
        }

        private bool FindPlayerControlRoute(PlayerState p, HeldInput input)
        {
            int sx = RoundCell(p.x), sy = RoundCell(p.y), tx = input.targetX, ty = input.targetY;
            if (Math.Abs((long)tx - sx) > ControlPathRadius || Math.Abs((long)ty - sy) > ControlPathRadius ||
                (input.intent == PlayerControlIntent.Walk && CircleBlocked(tx, ty, ControlRadius)) ||
                !PlayerControlSegmentClear(p.x, p.y, sx, sy)) return false;
            long start = Cell(sx, sy);
            var discovered = new Dictionary<long, PlayerPathNode>();
            var open = new List<PlayerPathNode>();
            var first = new PlayerPathNode { cell = start, parent = start, score = Math.Abs(tx - sx) + Math.Abs(ty - sy) };
            discovered.Add(start, first); PushPlayerPath(open, first);
            while (open.Count > 0)
            {
                PlayerPathNode node = PopPlayerPath(open);
                if (discovered[node.cell] != node) continue;
                int x = WorldTiles.X(node.cell), y = WorldTiles.Y(node.cell);
                if (PlayerControlAtGoal(p, x, y, input))
                {
                    var route = new List<long>();
                    while (node.cell != start) { route.Add(node.cell); node = discovered[node.parent]; }
                    route.Add(start); route.Reverse(); input.route = route; input.routeIndex = 0; return true;
                }
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DirectionX[d], ny = y + DirectionY[d];
                    if (Math.Abs((long)nx - sx) > ControlPathRadius || Math.Abs((long)ny - sy) > ControlPathRadius ||
                        !PlayerControlSegmentClear(x, y, nx, ny)) continue;
                    long key = Cell(nx, ny); int distance = node.distance + 1;
                    if (discovered.TryGetValue(key, out var old) && old.distance <= distance) continue;
                    if (discovered.Count >= ControlPathNodes) return false;
                    var next = new PlayerPathNode { cell = key, parent = node.cell, distance = distance,
                        score = distance + Math.Abs(tx - nx) + Math.Abs(ty - ny) };
                    discovered[key] = next; PushPlayerPath(open, next);
                }
            }
            return false;
        }

        private static void PushPlayerPath(List<PlayerPathNode> heap, PlayerPathNode node)
        {
            int index = heap.Count; heap.Add(node);
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (heap[parent].score <= node.score) break;
                heap[index] = heap[parent]; index = parent;
            }
            heap[index] = node;
        }

        private static PlayerPathNode PopPlayerPath(List<PlayerPathNode> heap)
        {
            PlayerPathNode result = heap[0], last = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
            if (heap.Count == 0) return result;
            int index = 0;
            while (index * 2 + 1 < heap.Count)
            {
                int child = index * 2 + 1;
                if (child + 1 < heap.Count && heap[child + 1].score < heap[child].score) child++;
                if (heap[child].score >= last.score) break;
                heap[index] = heap[child]; index = child;
            }
            heap[index] = last; return result;
        }
    }
}
