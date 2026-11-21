using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void Attack(PlayerState p, int targetId = 0)
        {
            if (p.attackCooldown > 0f) return;
            GearState gear=EquipmentCatalog.Equipped(p);
            if(gear!=null && EquipmentCatalog.IsArmor(gear.kind)) return;
            if(gear!=null && gear.durability<=0) { Notice(p,"装备已损坏，请修理或切换徒手。"); return; }
            if(p.reloadingGearId!=0) { Notice(p,"正在装填。"); return; }
            bool ranged=gear!=null && EquipmentCatalog.IsGun(gear.kind);
            WeaponStats stats=EquipmentCatalog.Stats(gear);
            if(ranged && gear.magazine<=0) { Notice(p,"空弹匣，请先装填。"); return; }
            p.attackCooldown=stats.interval*(1f-p.attributes[2]*.025f)*(p.skills[1]>=2 ? .85f : 1f);
            float damage=(stats.damage+(ranged ? p.attributes[3]*2.2f : p.attributes[0]*4f))*(1f+p.skills[1]*.18f);
            if (ranged)
            {
                float spread=EquipmentCatalog.EffectiveSpread(p,gear);
                gear.magazine--; gear.durability--;
                bool ap=gear.loadedAmmo==ItemKind.ArmorPiercingAmmo;
                int penetration=ap ? 3 : gear.kind==GearKind.PrecisionRifle ? 2 : 1;
                for(int pellet=0;pellet<stats.pellets;pellet++)
                {
                    uint hash=unchecked((uint)State.seed ^ (uint)State.tick*747796405u ^ (uint)gear.id*2891336453u);
                    hash=unchecked((hash^(hash>>16))*2246822519u);
                    float jitter=(hash%65536)/32767.5f-1f;
                    float offset=stats.pellets>1 ? ((2f*pellet/(stats.pellets-1)-1f)*.8f+jitter*.2f)*spread : jitter*spread;
                    float cos=(float)Math.Cos(offset), sin=(float)Math.Sin(offset);
                    Shoot(p.x,p.y,p.aimX*cos-p.aimY*sin,p.aimX*sin+p.aimY*cos,stats.range+p.attributes[3]*.35f,damage*(ap ? 1.15f : 1f),p,penetration,targetId);
                }
                p.recoil=Math.Min(1f,p.recoil+stats.recoil);
            }
            else
            {
                if(gear!=null) gear.durability--;
                float range = stats.range + p.attributes[0] * 0.015f;
                AddTrace(p.x, p.y, p.x + p.aimX * range, p.y + p.aimY * range, false, 2);
                int remaining = p.skills[1] >= 3 ? 3 : 1;
                // Stable nearest-first melee rather than list order; tier three cleaves three targets.
                int firstId = -1, secondId = -1;
                while (remaining-- > 0)
                {
                    ZombieState best = null; CreatureState animal = null; float nearest = range * range;
                    foreach (ZombieState z in State.zombies)
                    {
                        if (z.id == firstId || z.id == secondId || (targetId != 0 && z.id != targetId)) continue;
                        float dx = z.x - p.x, dy = z.y - p.y, dist = dx * dx + dy * dy;
                        if (dist > nearest || (dist > 0.01f && dx * p.aimX + dy * p.aimY < Math.Sqrt(dist) * 0.2f) ||
                            !LineClear(p.x, p.y, z.x, z.y, RoundCell(z.x), RoundCell(z.y))) continue;
                        best = z; nearest = dist;
                    }
                    if (State.survival != null && State.survival.enabled) foreach (CreatureState c in State.survival.creatures)
                    {
                        if (c.hp <= 0 || c.id == firstId || c.id == secondId || (targetId != 0 && c.id != targetId)) continue;
                        float dx = c.x - p.x, dy = c.y - p.y, dist = dx * dx + dy * dy;
                        if (dist > nearest || (dist == nearest && (best != null || animal != null)) ||
                            (dist > .01f && dx * p.aimX + dy * p.aimY < Math.Sqrt(dist) * .2f) ||
                            !LineClear(p.x, p.y, c.x, c.y, RoundCell(c.x), RoundCell(c.y))) continue;
                        animal = c; best = null; nearest = dist;
                    }
                    if (best == null && animal == null) break;
                    int hitId = best != null ? best.id : animal.id;
                    if (firstId < 0) firstId = hitId; else secondId = hitId;
                    if (best != null) DamageZombie(best, damage, p); else DamageCreature(animal.id, damage, p);
                }
            }
        }

        private struct BulletHit
        {
            public ZombieState zombie; public CreatureState creature; public float distance;
            public int Id => zombie != null ? zombie.id : creature.id;
        }

        private void Shoot(float x, float y, float dx, float dy, float range, float damage, PlayerState owner, int penetration=1, int targetId=0)
        {
            Normalize(ref dx, ref dy, true);
            float stop=BulletStop(x,y,dx,dy,range);
            var hits=new List<BulletHit>();
            foreach (ZombieState z in State.zombies)
            {
                if (targetId != 0 && z.id != targetId) continue;
                float zx = z.x - x, zy = z.y - y, projection = zx * dx + zy * dy;
                if (z.hp<=0f || projection < 0f || projection > stop) continue;
                float perpendicular = zx * zx + zy * zy - projection * projection;
                if (perpendicular > 0.34f * 0.34f) continue;
                hits.Add(new BulletHit { zombie=z,distance=projection });
            }
            if (owner != null && State.survival != null && State.survival.enabled) foreach (CreatureState c in State.survival.creatures)
            {
                if (targetId != 0 && c.id != targetId) continue;
                float cx=c.x-x, cy=c.y-y, projection=cx*dx+cy*dy;
                if (c.hp<=0 || projection<0 || projection>stop || cx*cx+cy*cy-projection*projection>.34f*.34f) continue;
                hits.Add(new BulletHit { creature=c, distance=projection });
            }
            hits.Sort((a,b)=> { int result=a.distance.CompareTo(b.distance); return result!=0 ? result : a.Id.CompareTo(b.Id); });
            for(int i=0;i<hits.Count && i<penetration;i++)
            {
                if (hits[i].zombie!=null) DamageZombie(hits[i].zombie,damage,owner);
                else DamageCreature(hits[i].creature.id,damage,owner);
                damage*=.8f;
                if(i==penetration-1) stop=hits[i].distance;
            }
            AddTrace(x, y, x + dx * stop, y + dy * stop, false, 1);
        }

        private float BulletStop(float x,float y,float dx,float dy,float range)
        {
            // Traverse every crossed cell, including both neighbours at a corner. Sampling
            // can miss thin diagonal obstructions; penetration never bypasses terrain.
            int cx=RoundCell(x), cy=RoundCell(y), sx=Math.Sign(dx), sy=Math.Sign(dy);
            float tx=sx==0 ? float.PositiveInfinity : (cx+(sx>0 ? .5f : -.5f)-x)/dx;
            float ty=sy==0 ? float.PositiveInfinity : (cy+(sy>0 ? .5f : -.5f)-y)/dy;
            float stepx=sx==0 ? float.PositiveInfinity : Math.Abs(1f/dx), stepy=sy==0 ? float.PositiveInfinity : Math.Abs(1f/dy);
            for(int n=0;n<256;n++)
            {
                float at=Math.Min(tx,ty);
                if(at>range || float.IsPositiveInfinity(at)) return range;
                if(Math.Abs(tx-ty)<.000001f)
                {
                    if(BlockedAt(cx+sx,cy) || BlockedAt(cx,cy+sy)) return Math.Max(0f,at-.001f);
                    cx+=sx; cy+=sy; tx+=stepx; ty+=stepy;
                }
                else if(tx<ty) { cx+=sx; tx+=stepx; }
                else { cy+=sy; ty+=stepy; }
                if(BlockedAt(cx,cy)) return Math.Max(0f,at-.001f);
            }
            return 0f;
        }

        private void UpdateTurrets()
        {
            foreach (BuildingState b in State.buildings)
            {
                if (b.kind != BuildKind.Turret) continue;
                b.progress = Math.Max(0f, b.progress - FixedStep * b.powerRatio);
                if (!b.powered || b.progress > 0f || b.items[(int)ItemKind.Ammo] <= 0) continue;
                ZombieState target = null; float distance = 8.5f * 8.5f;
                foreach (ZombieState z in State.zombies)
                {
                    float d = DistSq(b.x, b.y, z.x, z.y);
                    if (d <= distance && LineClear(b.x, b.y, z.x, z.y, RoundCell(z.x), RoundCell(z.y))) { target = z; distance = d; }
                }
                if (target == null) continue;
                b.items[(int)ItemKind.Ammo]--; b.progress = 0.4f;
                float dx = target.x - b.x, dy = target.y - b.y;
                if (Math.Abs(dx) + Math.Abs(dy) < 0.0001f) dx = 1f;
                Shoot(b.x, b.y, dx, dy, 8.5f, 26f * turretBonus, null);
            }
        }

        private void DamageZombie(ZombieState z, float amount, PlayerState owner)
        {
            if (z.hp <= 0f) return;
            GearState weapon = owner == null ? null : EquipmentCatalog.Equipped(owner);
            bool armorPiercing = weapon != null && EquipmentCatalog.IsGun(weapon.kind) && weapon.loadedAmmo == ItemKind.ArmorPiercingAmmo;
            amount *= EnemyCatalog.IncomingDamageMultiplier(z.variant, armorPiercing);
            z.hp = Math.Max(0f, z.hp - amount);
            if (z.hp > 0f) return;
            State.zombies.Remove(z); State.kills = SaturatingAdd(State.kills, 1);
            if (owner != null)
            {
                GainXp(owner, 12 + z.variant * 4);
                // A corpse is an explicit loot source; a full backpack never wraps or becomes negative.
                owner.items[(int)ItemKind.Scrap] = Math.Min(InventoryLimit, owner.items[(int)ItemKind.Scrap] + 1);
            }
            else foreach (PlayerState p in State.players) if (p.connected && p.hp > 0f && DistSq(p.x, p.y, z.x, z.y) <= 196f) GainXp(p, 5);
        }

        private void DamagePlayer(PlayerState p, float amount, bool hostile)
        {
            if (p.hp <= 0f || !p.connected) return;
            if (hostile && State.elapsed < inputs[p.id].protectionUntil) return;
            if (hostile)
            {
                amount *= 1f-p.attributes[1]*.02f;
                float rating=EquipmentCatalog.ArmorRating(p);
                if(rating>0f) amount*=100f/(100f+rating);
                if(amount>0f) for(int i=0;i<p.armorIds.Length;i++)
                {
                    GearState armor=EquipmentCatalog.Find(p,p.armorIds[i]);
                    if(armor!=null && armor.durability>0 && EquipmentCatalog.ArmorSlot(armor.kind)==i) armor.durability--;
                }
            }
            p.hp = Math.Max(0f, p.hp - amount); p.lastDamageAt = State.elapsed;
            if (p.hp > 0f) return;
            p.deaths = SaturatingAdd(p.deaths, 1);
            HeldInput input = inputs[p.id]; input.x = input.y = 0f; input.sprint = false;
            ClearPlayerControl(p, input);
            CancelReload(p); p.aiming=false; p.moving=false;
            Notice(p, "你已倒下；5秒后可复活，背包和成长保留。"); flowRevision = -1;
        }

        private void AddTrace(float x, float y, float tx, float ty, bool hostile, int effect = 0)
        {
            if (State.traces.Count >= MaxTraces) State.traces.RemoveAt(0);
            State.traces.Add(new TraceState { x = x, y = y, endX = tx, endY = ty, hostile = hostile, effect = effect, until = State.elapsed + 0.15f });
        }

        private void RebuildFlow()
        {
            flow.Clear(); flowQueue.Clear();
            foreach (PlayerState p in State.players) if (p.connected && p.hp > 0f)
            {
                int x = RoundCell(p.x), y = RoundCell(p.y);
                if (!Inside(x, y) || flow[Cell(x, y)] == 0) continue;
                flow[Cell(x, y)] = 0; flowQueue.Enqueue(Cell(x, y));
            }
            while (flowQueue.Count>0)
            {
                long cell=flowQueue.Dequeue(); int x=WorldTiles.X(cell), y=WorldTiles.Y(cell);
                // Distant explored land does not expand every enemy search. The
                // local horde uses a 64-cell field around each living survivor.
                if(ChunkStreaming.Enabled(State) && flow[cell]>=64) continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DirectionX[d], ny = y + DirectionY[d];
                    char ground=WorldTiles.Ground(State,nx,ny);
                    if (!Inside(nx, ny) || ground == '2' || ground == '?' || flow[Cell(nx, ny)] != int.MaxValue) continue;
                    // Destructible obstructions remain in the flow graph: zombies break a closed wall
                    // instead of losing their target when a player completely encloses the base.
                    flow[Cell(nx, ny)] = flow[cell] + 1; flowQueue.Enqueue(Cell(nx, ny));
                }
            }
            flowRevision = 0;
        }

        private void UpdateZombies()
        {
            // A bomber can remove itself in this phase. Preserve original AI order while
            // avoiding mutation of the live collection during enumeration.
            foreach (ZombieState z in new List<ZombieState>(State.zombies))
            {
                if (z.hp <= 0f) continue;
                z.attackCooldown = Math.Max(0f, z.attackCooldown - FixedStep);
                PlayerState nearest = null; float bestDistance = float.MaxValue;
                foreach (PlayerState p in State.players) if (p.connected && p.hp > 0f)
                {
                    float d = DistSq(p.x, p.y, z.x, z.y); if (d < bestDistance) { nearest = p; bestDistance = d; }
                }
                if (nearest == null) continue;
                if (State.wave == 0 && bestDistance > 12f * 12f && z.alertedUntil <= State.elapsed) continue;
                if (EnemySpecial(z, nearest, bestDistance)) continue;
                if (bestDistance <= 1.05f * 1.05f && LineClear(z.x, z.y, nearest.x, nearest.y, RoundCell(nearest.x), RoundCell(nearest.y)))
                {
                    if (z.attackCooldown <= 0f)
                    {
                        bool night = EnemyCatalog.IsNight(State.elapsed, State.dayLength);
                        z.attackCooldown = EnemyCatalog.AttackInterval(z.variant, night);
                        EnemyDamagePlayer(nearest, EnemyCatalog.Damage(z.variant, night), z.variant);
                        AddTrace(z.x, z.y, nearest.x, nearest.y, true);
                    }
                    continue;
                }
                int cx = RoundCell(z.x), cy = RoundCell(z.y), tx = cx, ty = cy, value = int.MaxValue;
                if (!Inside(cx, cy)) continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DirectionX[d], ny = cy + DirectionY[d];
                    if (Inside(nx, ny) && flow[Cell(nx, ny)] < value) { value = flow[Cell(nx, ny)]; tx = nx; ty = ny; }
                }
                if (value == int.MaxValue) continue;
                float targetX = tx, targetY = ty;
                if (RoundCell(nearest.x) == cx && RoundCell(nearest.y) == cy) { targetX = nearest.x; targetY = nearest.y; }
                if (BlockedAt(tx, ty) && DistSq(z.x, z.y, tx, ty) <= 1.3f * 1.3f)
                { ZombieBreak(z, tx, ty); continue; }
                float dx = targetX - z.x, dy = targetY - z.y; Normalize(ref dx, ref dy, true);
                float speed = EnemyCatalog.Speed(z.variant, EnemyCatalog.IsNight(State.elapsed, State.dayLength));
                speed *= 1f + Math.Min(0.35f, State.wave * 0.015f);
                float oldX = z.x, oldY = z.y;
                MoveCircle(ref z.x, ref z.y, dx * speed * FixedStep, dy * speed * FixedStep, 0.29f);
                if (DistSq(oldX, oldY, z.x, z.y) < 0.000001f)
                {
                    // At an obstructed corner, attack the actual adjacent collision cell.
                    bool attacked = false;
                    for (int oy = -1; oy <= 1 && !attacked; oy++) for (int ox = -1; ox <= 1; ox++)
                    {
                        int bx = cx + ox, by = cy + oy;
                        if (Inside(bx, by) && (NodeAt(bx, by) != null || BuildingAt(bx, by) != null) && DistSq(z.x, z.y, bx, by) < 1.7f)
                        { ZombieBreak(z, bx, by); attacked = true; break; }
                    }
                }
            }
        }

        private void ZombieBreak(ZombieState z, int x, int y)
        {
            if (z.attackCooldown > 0f) return;
            BuildingState b = BuildingAt(x, y); NodeState n = NodeAt(x, y);
            if (b == null && n == null) return;
            if (z.variant == 10) { DetonateEnemy(z); return; }
            EnemyStats stats = EnemyCatalog.Stats(z.variant);
            z.attackCooldown = stats.structureInterval; float damage = stats.structureDamage;
            if (b != null) { b.hp -= damage; if (b.hp <= 0f) RemoveBuilding(b); }
            else { n.hp -= damage; if (n.hp <= 0f) RemoveNode(n); }
            // Enemy destruction is an intentional resource sink (contents burn); dismantling is the
            // lossless way to recover stored items. No attack creates harvest output.
            AddTrace(z.x, z.y, x, y, true);
        }

        private void StartWave()
        {
            State.wave = Math.Min(10000, State.wave + 1);
            int amount = Math.Min(64, 8 + State.wave * 4 + LivingPlayers() * 2);
            int spawned = 0;
            for (int i = 0; i < amount && State.zombies.Count < MaxZombies; i++)
                if (SpawnZombie(i % (State.wave >= 3 ? 3 : State.wave >= 2 ? 2 : 1))) spawned++;
            State.nextWaveAt = State.elapsed + Math.Max(80f, 120f - State.wave * 2f);
            State.announcement = "第 " + State.wave + " 波尸潮：" + spawned + " 只。守住产线并及时为发电机补充燃料！";
            State.announcementUntil = State.elapsed + 12f;
        }
        private bool SpawnZombie(int variant)
        {
            if (State.zombies.Count >= MaxZombies) return false;
            uint hash = unchecked((uint)State.seed ^ (uint)State.tick * 747796405u ^ (uint)State.nextId * 2891336453u);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                hash = unchecked(hash * 1664525u + 1013904223u);
                int side = (int)(hash % 4);
                hash = unchecked(hash * 1664525u + 1013904223u);
                int position = 2 + (int)(hash % (uint)(State.width - 4));
                int x = side == 0 ? 2 : side == 2 ? State.width - 3 : position;
                int y = side == 1 ? 2 : side == 3 ? State.height - 3 : position;
                if(ChunkStreaming.Enabled(State))
                {
                    var living=State.players.FindAll(p=>p.connected && p.hp>0);
                    if(living.Count==0) return false;
                    var target=living[(int)(hash%(uint)living.Count)];
                    int edge=(int)(hash%49)-24;
                    x=RoundCell(target.x)+(side==0 ? -24 : side==2 ? 24 : edge);
                    y=RoundCell(target.y)+(side==1 ? -24 : side==3 ? 24 : edge);
                }
                if (CircleBlocked(x, y, 0.3f) || OccupiedByActor(x, y)) continue;
                bool safe = true;
                foreach (PlayerState p in State.players) if (p.connected && p.hp > 0f && DistSq(x, y, p.x, p.y) < 225f) { safe = false; break; }
                if (!safe) continue;
                int selected = SelectSpawnEnemy(variant, x, y, hash);
                State.zombies.Add(new ZombieState { id = AllocateId(), x = x, y = y, variant = selected, hp = EnemyCatalog.Stats(selected).hp + Math.Min(100f, State.wave * 4f) });
                return true;
            }
            return false;
        }
    }
}
