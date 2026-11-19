using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    /// <summary>Single-threaded host authority. State is host-owned, never accept it from a client.
    /// Step uses fixed 50ms ticks (at most one second per call); movement persists until replaced.
    /// Empty/move changes movement only; every other action leaves the held input intact.
    /// craft: 0 = 3 wood -> 2 fuel; 1 = 1 iron -> 4 ammo. Targets use integer cell centres.
    /// Save State between calls. Restore validates and deep-copies it, releasing held input.
    /// </summary>
    public sealed partial class WorldSimulator
    {
        public const float FixedStep = 0.05f;
        public const int MaxPlayers = 8, MaxNodes = 1400, MaxBuildings = 1024;
        public const int MaxZombies = 160, MaxTraces = 192, InventoryLimit = 999, BufferLimit = 200;
        public const int MaxCargo = 1024;
        private const float Reach = 2.4f;
        private const int MaxLevel = 30;
        private readonly Dictionary<string, HeldInput> inputs = new Dictionary<string, HeldInput>(StringComparer.Ordinal);
        private readonly NodeState[] nodeGrid;
        private readonly BuildingState[] buildingGrid;
        private readonly int[] flow, flowQueue;
        private double accumulator, clock;
        private float engineeringRate = 1f, turretBonus = 1f;
        private int flowRevision = -1;
        public WorldState State { get; private set; }

        private sealed class HeldInput
        {
            public float x, y, protectionUntil;
            public bool sprint;
            public long commandTick = -1;
            public int commands;
        }

        public WorldSimulator(int seed) : this(CreateWorld(seed)) { }

        public WorldSimulator(WorldState restored)
        {
            State = CopyAndValidate(restored);
            clock = State.elapsed;
            int size = State.width * State.height;
            nodeGrid = new NodeState[size];
            buildingGrid = new BuildingState[size];
            flow = new int[size];
            flowQueue = new int[size];
            foreach (PlayerState p in State.players) inputs.Add(p.id, new HeldInput());
            RefreshSpatial();
            RefreshBonuses();
            RefreshPower(false);
        }

        public PlayerState AddPlayer(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || HasControl(id))
                throw new ArgumentException("Player id must contain 1-64 printable characters.", nameof(id));
            PlayerState existing = FindPlayer(id);
            if (existing != null)
            {
                SetConnected(id, true);
                return existing;
            }
            if (State.players.Count >= MaxPlayers) throw new InvalidOperationException("World has eight persistent player slots.");
            RefreshSpatial();
            int sx, sy;
            if (!FindSpawn(out sx, out sy)) throw new InvalidOperationException("No safe spawn cell available.");
            var p = new PlayerState { id = id, name = CleanName(name), seat = FirstFreeSeat(), x = sx, y = sy };
            p.items = DemoCatalog.Materials(ItemKind.Wood,12, ItemKind.Scrap,4, ItemKind.Food,6);
            State.players.Add(p);
            inputs.Add(id, new HeldInput { protectionUntil = State.elapsed + 4f });
            Notice(p, "原始开局：采矿、3木材制2燃料，石炉炼铁铜锡。水车保底岸位(25,25)，科技全队共享。");
            flowRevision = -1;
            return p;
        }

        public void SetConnected(string id, bool connected)
        {
            PlayerState p = FindPlayer(id);
            if (p == null) return;
            p.connected = connected;
            HeldInput input = inputs[p.id];
            input.x = input.y = 0f; input.sprint = false;
            flowRevision = -1;
        }

        public void Apply(PlayerCommand cmd)
        {
            if (cmd == null || cmd.playerId == null || cmd.playerId.Length > 64) return;
            PlayerState p = FindPlayer(cmd.playerId);
            if (p == null || !p.connected) return;
            string action = cmd.action ?? "";
            if (action.Length > 16) return;
            HeldInput input = inputs[p.id];
            if (action.Length == 0 || action == "move")
            {
                if (!Finite(cmd.moveX) || !Finite(cmd.moveY) || !Finite(cmd.aimX) || !Finite(cmd.aimY)) return;
                float mx = cmd.moveX, my = cmd.moveY;
                Normalize(ref mx, ref my, false);
                input.x = p.hp > 0f ? mx : 0f; input.y = p.hp > 0f ? my : 0f;
                input.sprint = cmd.sprint && p.hp > 0f;
                Aim(p, cmd.aimX, cmd.aimY);
                return;
            }
            // Bound discrete work even if a client floods the host between ticks.
            if (input.commandTick != State.tick) { input.commandTick = State.tick; input.commands = 0; }
            if (input.commands >= 6) return;
            input.commands++;
            if (action == "respawn") { Respawn(p); return; }
            if (p.hp <= 0f) return;
            RefreshSpatial();
            switch (action)
            {
                case "attack":
                    if (!Finite(cmd.aimX) || !Finite(cmd.aimY)) return;
                    Aim(p, cmd.aimX, cmd.aimY);
                    Attack(p); return;
                case "attribute": UpgradeAttribute(p, cmd.arg); return;
                case "skill": UpgradeSkill(p, cmd.arg); return;
                case "eat":
                    if (p.interactCooldown <= 0f && p.hunger < 99f && p.items[(int)ItemKind.Food] > 0)
                    {
                        p.items[(int)ItemKind.Food]--; p.hunger = Math.Min(100f, p.hunger + 38f + p.skills[2] * 4f);
                        p.hp = Math.Min(PlayerMaxHealth(p), p.hp + 6f + p.skills[2] * 2f);
                        p.interactCooldown = 0.3f; Notice(p, "已食用口粮。");
                    }
                    return;
                case "craft": Craft(p, cmd.arg); return;
                case "research": Research(p, cmd.arg); return;
                case "interact": case "build": case "repair": case "dismantle": case "recipe": case "configure":
                    if (p.interactCooldown > 0f || !Inside(cmd.x, cmd.y) ||
                        DistSq(p.x, p.y, cmd.x, cmd.y) > Reach * Reach ||
                        !LineClear(p.x, p.y, cmd.x, cmd.y, cmd.x, cmd.y)) return;
                    p.interactCooldown = 0.25f;
                    if (action == "build") Build(p, cmd);
                    else if (action == "interact") Interact(p, cmd.x, cmd.y, cmd.arg);
                    else if (action == "repair") Repair(p, cmd.x, cmd.y);
                    else if (action == "recipe" || action == "configure") Configure(p, cmd.x, cmd.y, cmd.arg);
                    else Dismantle(p, cmd.x, cmd.y);
                    return;
            }
        }

        public void Step(float dt)
        {
            if (!Finite(dt) || dt <= 0f) return;
            accumulator += Math.Min(1.0, dt);
            int steps = 0;
            while (accumulator + 0.000000001 >= 0.05 && steps++ < 20)
            {
                accumulator -= 0.05;
                if (accumulator < 0) accumulator = 0;
                Tick();
            }
        }

        public bool IsBlocked(int x, int y) { RefreshSpatial(); return BlockedAt(x, y); }

        private void Tick()
        {
            if (State.tick >= long.MaxValue - 1) return;
            State.tick++; clock += 0.05; State.elapsed = (float)clock;
            RefreshSpatial();
            RefreshBonuses();
            foreach (PlayerState p in State.players) UpdatePlayer(p);
            RefreshPower(true);
            UpdateProduction();
            UpdateTurrets();
            if (flowRevision < 0 || State.tick % 5 == 0) RebuildFlow();
            UpdateZombies();
            if (State.elapsed >= State.nextWaveAt && LivingPlayers() > 0) StartWave();
            else if (State.tick % 600 == 0 && LivingPlayers() > 0 && State.zombies.Count < 5 + Math.Min(12, State.wave))
                SpawnZombie((int)(State.tick % 3));
            State.traces.RemoveAll(t => t.until <= State.elapsed);
        }

        private void UpdatePlayer(PlayerState p)
        {
            p.attackCooldown = Math.Max(0f, p.attackCooldown - FixedStep);
            p.interactCooldown = Math.Max(0f, p.interactCooldown - FixedStep);
            if (!p.connected || p.hp <= 0f) return;
            HeldInput input = inputs[p.id];
            bool moving = input.x != 0f || input.y != 0f;
            bool sprinting = moving && input.sprint && p.stamina >= 1.5f && p.hunger > 0f;
            float speed = 3.2f + p.attributes[2] * 0.09f + (p.skills[2] >= 2 ? 0.25f : 0f);
            if (sprinting) speed *= 1.6f;
            if (p.hunger <= 0f) speed *= 0.7f;
            MoveCircle(ref p.x, ref p.y, input.x * speed * FixedStep, input.y * speed * FixedStep, 0.28f);
            p.stamina = Clamp(p.stamina + (sprinting ? -19f : 13f + p.skills[2] * 2f) * FixedStep, 0f, PlayerMaxStamina(p));
            float hungerRate = 0.085f * (1f - p.skills[2] * 0.15f) * (sprinting ? 2.8f : 1f);
            p.hunger = Math.Max(0f, p.hunger - hungerRate * FixedStep);
            if (p.hunger <= 0f) DamagePlayer(p, 2.5f * FixedStep, false);
            else if (p.hunger >= 60f && State.elapsed - p.lastDamageAt > 7f)
                p.hp = Math.Min(PlayerMaxHealth(p), p.hp + (0.55f + (p.skills[2] >= 3 ? 1.2f : 0f)) * FixedStep);
        }

        private void Build(PlayerState p, PlayerCommand cmd)
        {
            if (cmd.arg < 0 || cmd.arg >= DemoCatalog.BuildNames.Length || cmd.rotation < 0 || cmd.rotation > 3) return;
            BuildKind kind = (BuildKind)cmd.arg;
            if (!TechCatalog.IsUnlocked(State, kind)) { Notice(p, "需要科技：" + TechCatalog.EraName(TechCatalog.RequiredEra(kind))); return; }
            var b = new BuildingState { x = cmd.x, y = cmd.y, rotation = cmd.rotation, kind = kind, footprintVersion = 1 };
            if (State.buildings.Count >= MaxBuildings) { Notice(p, "设备数量已达上限。"); return; }
            for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++) for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++)
            {
                if (BlockedAt(x, y) || BuildingAt(x, y) != null) { Notice(p, "完整占地内有障碍、水域或其他设备。"); return; }
                if (NearSpawn(x, y)) { Notice(p, "出生救援区不可建造。"); return; }
                if (kind != BuildKind.Belt && OccupiedByActor(x, y)) { Notice(p, "人物占据设备范围。"); return; }
            }
            if ((kind == BuildKind.Miner || kind == BuildKind.SteamMiner) && NearbyOre(b) == null)
            { Notice(p, "采矿机需紧邻未耗尽矿脉（含斜邻格）。"); return; }
            if ((kind == BuildKind.Waterwheel || kind == BuildKind.Boiler || kind == BuildKind.CoolingTower) && !NearWater(b))
            { Notice(p, "必须建在紧邻水面的岸上干地。"); return; }
            int[] cost = DemoCatalog.BuildCost(kind);
            if (!CanPay(p.items, cost)) { Notice(p, "建材不足：" + DemoCatalog.CostText(kind)); return; }
            int id = AllocateId();
            Pay(p.items, cost);
            b.id = id; b.hp = BuildingMaxHealth(kind);
            State.buildings.Add(b); RefreshSpatial(); RefreshPower(false);
            flowRevision = -1;
            Notice(p, "已建造" + DemoCatalog.BuildName(kind));
        }

        private void Repair(PlayerState p, int x, int y)
        {
            BuildingState b = BuildingAt(x, y);
            if (b == null || b.hp >= BuildingMaxHealth(b.kind)) return;
            int material = b.kind == BuildKind.Wall ? (int)ItemKind.Wood : (int)ItemKind.Scrap;
            if (p.items[material] < 1) { Notice(p, "修理需要 1 " + DemoCatalog.ItemNames[material]); return; }
            p.items[material]--;
            b.hp = Math.Min(BuildingMaxHealth(b.kind), b.hp + 32f + p.attributes[4] * 2f + p.skills[0] * 8f);
            Notice(p, "已修理。");
        }

        private void Dismantle(PlayerState p, int x, int y)
        {
            BuildingState b = BuildingAt(x, y);
            if (b == null) return;
            int[] cost = DemoCatalog.BuildCost(b.kind);
            var returned = new int[DemoCatalog.ItemCount];
            float refund = p.skills[0] >= 3 ? 1f : p.skills[0] >= 2 ? 0.75f : 0.5f;
            float health = Clamp(b.hp / BuildingMaxHealth(b.kind), 0f, 1f);
            for (int i = 0; i < DemoCatalog.ItemCount; i++)
            {
                returned[i] = b.items[i] + (int)Math.Floor(cost[i] * refund * health + 0.00001f);
            }
            foreach (CargoState cargo in State.cargo) if (cargo.fromId == b.id || cargo.toId == b.id) returned[cargo.item]++;
            for (int i = 0; i < DemoCatalog.ItemCount; i++)
                if (p.items[i] + returned[i] > InventoryLimit) { Notice(p, "背包不足：须能完整收回库存、在途物料与返还建材。"); return; }
            for (int i = 0; i < DemoCatalog.ItemCount; i++) p.items[i] += returned[i];
            State.cargo.RemoveAll(c => c.fromId == b.id || c.toId == b.id);
            State.buildings.Remove(b); RefreshSpatial(); RefreshPower(false); flowRevision = -1;
            Notice(p, "已拆除；库存全额收回，建材按耐久和工程专精返还，已点燃燃料不返还。");
        }

        private void Craft(PlayerState p, int arg)
        {
            if (p.interactCooldown > 0f || arg < 0) return;
            if (arg >= 2)
            {
                RecipeDefinition r = TechCatalog.Recipe(arg);
                if (r == null || r.building != BuildKind.Workbench || State.era < r.era) return;
                bool bench = State.buildings.Exists(b => b.hp > 0f && b.kind == BuildKind.Workbench &&
                    DistSq(p.x,p.y,DemoCatalog.CenterX(b),DemoCatalog.CenterY(b)) <= Reach * Reach && LineClear(p.x,p.y,b.x,b.y,b.x,b.y));
                if (!bench) { Notice(p, "请靠近工作台。"); return; }
                if (!CanRunRecipe(p.items,r,InventoryLimit,null)) { Notice(p, "配方材料不足或背包已满。"); return; }
                Pay(p.items,r.inputs);
                for(int i=0;i<DemoCatalog.ItemCount;i++) { p.items[i]+=r.outputs[i]; RecordProduced(i,r.outputs[i]); }
                p.interactCooldown=.5f; Notice(p,"已制作："+r.name); return;
            }
            int source = arg == 0 ? (int)ItemKind.Wood : (int)ItemKind.Iron;
            int target = arg == 0 ? (int)ItemKind.Fuel : (int)ItemKind.Ammo;
            int take = arg == 0 ? 3 : 1, give = arg == 0 ? 2 : 4;
            if (p.items[source] < take || p.items[target] > InventoryLimit - give)
            { Notice(p, "材料不足或背包已满：3 木材→2 燃料；1 铁锭→4 弹药。"); return; }
            p.items[source] -= take; p.items[target] += give; p.interactCooldown = 0.5f;
            RecordProduced(target,give);
            // Manual crafting deliberately awards no XP: dismantle/rebuild and crafting cannot farm levels.
            Notice(p, arg == 0 ? "已制作 2 燃料。" : "已制作 4 弹药。");
        }

        private void Research(PlayerState p,int arg)
        {
            if (p.interactCooldown > 0f || arg < 1 || arg > 6) return;
            TechEra target=(TechEra)arg;
            string requirement=TechCatalog.ResearchRequirement(State,target);
            if(requirement.Length>0) { Notice(p,requirement); return; }
            if(!TechCatalog.CanResearch(State,p,target)) { Notice(p,"研究材料不足。"); return; }
            // Host serializes commands. Advancing the era before another command rejects replay.
            Pay(p.items,TechCatalog.ResearchCost(target)); State.era=target; p.interactCooldown=.5f;
            State.announcement="全队科技已解锁："+TechCatalog.EraName(target); State.announcementUntil=State.elapsed+12f;
            Notice(p,State.announcement);
        }
        private void Configure(PlayerState p,int x,int y,int arg)
        {
            BuildingState b=BuildingAt(x,y);
            if(b==null || !TechCatalog.IsUnlocked(State,b.kind)) return;
            if(b.kind==BuildKind.Transformer)
            {
                if(arg<0 || arg>1 || (arg==1 && State.era<TechEra.HighVoltage)) return;
                b.setting=arg; b.progress=0f; RefreshPower(false); Notice(p,arg==0 ? "变压器 MV→LV" : "变压器 HV→MV"); return;
            }
            RecipeDefinition r=TechCatalog.Recipe(arg);
            if(r==null || r.building!=b.kind || b.kind==BuildKind.Workbench || r.era>State.era) return;
            if(b.recipe!=arg) { b.recipe=arg; b.progress=0f; }
            Notice(p,"配方："+r.name);
        }

        private void UpgradeAttribute(PlayerState p, int arg)
        {
            if (arg < 0 || arg >= 5 || p.attributePoints <= 0 || p.attributes[arg] >= 10) return;
            p.attributePoints--; p.attributes[arg]++;
            if (arg == 1) p.hp = Math.Min(PlayerMaxHealth(p), p.hp + 15f);
            if (arg == 2) p.stamina = Math.Min(PlayerMaxStamina(p), p.stamina + 8f);
            Notice(p, DemoCatalog.AttributeNames[arg] + "提升。");
        }

        private void UpgradeSkill(PlayerState p, int arg)
        {
            if (arg < 0 || arg >= 3 || p.skillPoints <= 0 || p.skills[arg] >= 3) return;
            int requiredLevel = 1 + p.skills[arg] * 2;
            if (p.level < requiredLevel) { Notice(p, "此分支下一层需人物等级 " + requiredLevel); return; }
            p.skillPoints--; p.skills[arg]++; RefreshBonuses();
            Notice(p, DemoCatalog.SkillNames[arg] + "第 " + p.skills[arg] + " 层已解锁。");
        }

        private void Respawn(PlayerState p)
        {
            if (p.hp > 0f || State.elapsed - p.lastDamageAt < 5f) return;
            RefreshSpatial(); int x, y;
            if (!FindSpawn(out x, out y)) { Notice(p, "救援区被敌人封锁，暂时无法复活。"); return; }
            p.x = x; p.y = y; p.hp = PlayerMaxHealth(p); p.stamina = PlayerMaxStamina(p);
            p.hunger = Math.Max(45f, p.hunger); p.attackCooldown = 0.5f;
            p.lastDamageAt = State.elapsed;
            HeldInput input = inputs[p.id]; input.x = input.y = 0f; input.sprint = false; input.protectionUntil = State.elapsed + 5f;
            Notice(p, "已在救援区复活，保留背包与成长；短暂无敌。"); flowRevision = -1;
        }

        private void GainXp(PlayerState p, int amount)
        {
            if (p.level >= MaxLevel || amount <= 0) return;
            p.xp += Math.Max(1, (int)(amount * (1f + p.attributes[4] * 0.05f)));
            while (p.level < MaxLevel && p.xp >= XpToNextLevel(p.level))
            {
                p.xp -= XpToNextLevel(p.level); p.level++; p.attributePoints += 2; p.skillPoints++;
                p.hp = Math.Min(PlayerMaxHealth(p), p.hp + 15f);
                Notice(p, "升级！属性点 +2，独立技能点 +1；专精第2/3层需3/5级。");
            }
            if (p.level >= MaxLevel) p.xp = 0;
        }

        public static int XpToNextLevel(int level) { return 30 + Math.Max(1, Math.Min(30, level)) * 15; }
        public static float PlayerMaxHealth(PlayerState p) { return 100f + p.attributes[1] * 15f; }
        public static float PlayerMaxStamina(PlayerState p) { return 100f + p.attributes[2] * 8f + p.skills[2] * 10f; }
        public static float BuildingMaxHealth(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.Wall: return 240f; case BuildKind.Generator: return 160f;
                case BuildKind.Miner: return 140f; case BuildKind.Belt: return 60f;
                case BuildKind.Smelter: return 180f; case BuildKind.Assembler: return 160f;
                case BuildKind.Turret: return 180f; default: return 120f;
            }
        }

        private PlayerState FindPlayer(string id) { if (id == null) return null; return State.players.Find(p => p.id == id); }
        private void Notice(PlayerState p, string text) { p.notice = text; p.noticeUntil = State.elapsed + 6f; }
        private bool Inside(int x, int y) { return x >= 0 && y >= 0 && x < State.width && y < State.height; }
        private int Cell(int x, int y) { return x + y * State.width; }
        private BuildingState BuildingAt(int x, int y) { return Inside(x, y) ? buildingGrid[Cell(x, y)] : null; }
        private NodeState NodeAt(int x, int y) { return Inside(x, y) ? nodeGrid[Cell(x, y)] : null; }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        private static float Clamp(float v, float lo, float hi) { return Math.Max(lo, Math.Min(hi, v)); }
        private static float DistSq(float x, float y, float tx, float ty) { float a = x - tx, b = y - ty; return a * a + b * b; }
        private static int RoundCell(float v) { return (int)Math.Floor(v + 0.5f); }
        private int AllocateId() { if (State.nextId >= int.MaxValue - 1) throw new InvalidOperationException("Entity ID space exhausted."); return State.nextId++; }
        private static bool CanPay(int[] inventory, int[] cost) { for (int i = 0; i < DemoCatalog.ItemCount; i++) if (inventory[i] < cost[i]) return false; return true; }
        private static void Pay(int[] inventory, int[] cost) { for (int i = 0; i < DemoCatalog.ItemCount; i++) inventory[i] -= cost[i]; }
        private static void Normalize(ref float x, ref float y, bool unit)
        {
            double length = Math.Sqrt((double)x * x + (double)y * y);
            if (length > 0.000001 && (unit || length > 1.0)) { x = (float)(x / length); y = (float)(y / length); }
            else if (length <= 0.000001) x = y = 0f;
        }
        private static void Aim(PlayerState p, float x, float y)
        {
            Normalize(ref x, ref y, true); if (x != 0f || y != 0f) { p.aimX = x; p.aimY = y; }
        }
        private int LivingPlayers() { return State.players.FindAll(p => p.connected && p.hp > 0f).Count; }
        private int FirstFreeSeat()
        {
            for (int i = 0; i < MaxPlayers; i++) if (!State.players.Exists(p => p.seat == i)) return i;
            throw new InvalidOperationException("No free player seat.");
        }
    }
}
