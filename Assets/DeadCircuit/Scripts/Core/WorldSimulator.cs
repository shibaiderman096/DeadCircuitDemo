using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    /// <summary>Single-threaded host authority. State is host-owned, never accept it from a client.
    /// Step uses fixed 50ms ticks (at most one second per call); movement persists until replaced.
    /// Empty/move updates manual movement; nonzero movement cancels automatic player intents.
    /// craft: 0 = 3 wood -> 2 fuel; 1 = 1 iron -> 4 ammo. Targets use integer cell centres.
    /// Save State between calls. Restore validates and deep-copies it, releasing held input.
    /// </summary>
    public sealed partial class WorldSimulator
    {
        public const float FixedStep = 0.05f;
        public const int SaveVersion = 23;
        public const int MaxPlayers = 8, MaxNodes = 1000000, MaxBuildings = 16384, MaxSites = 32768, MaxBlueprints = 4096;
        public const int MaxZombies = 160, MaxTraces = 192, InventoryLimit = InventoryWeight.MaxCapacityGrams / 10, BufferLimit = 200;
        public const int MaxCargo = 1024;
        private const float Reach = 2.4f;
        private const int MaxLevel = 30;
        private readonly Dictionary<string, HeldInput> inputs = new Dictionary<string, HeldInput>(StringComparer.Ordinal);
        private readonly SparseGrid<NodeState> nodeGrid = new SparseGrid<NodeState>();
        private readonly SparseGrid<BuildingState> buildingGrid = new SparseGrid<BuildingState>();
        private readonly FlowGrid flow = new FlowGrid();
        private readonly Queue<long> flowQueue = new Queue<long>();
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
            public PlayerControlIntent intent;
            public PlayerControlTarget targetKind;
            public int targetId;
            public long refreshedAt;
            public bool holdStopped;
            public bool allowNeutral;
            public float focusX, focusY;
            public int targetX, targetY;
            public List<long> route;
            public int routeIndex;
            public float travelDistance;
        }

        public WorldSimulator(int seed) : this(CreateWorld(seed)) { }
        public WorldSimulator(WorldGenerationSettings settings) : this(WorldGeneration.Generate(settings)) { }

        public WorldSimulator(WorldState restored)
        {
            State = CopyAndValidate(restored);
            clock = State.elapsed;
            foreach (PlayerState p in State.players) inputs.Add(p.id, new HeldInput());
            RefreshSpatial();
            RefreshBonuses();
            RefreshPower(false);
        }

        public PlayerState AddPlayer(string id, string name, int? appearance = null)
        {
            if (appearance.HasValue && !AppearanceCatalog.IsValid(appearance.Value))
                throw new ArgumentException("Player appearance preset is invalid.", nameof(appearance));
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || HasControl(id))
                throw new ArgumentException("Player id must contain 1-64 printable characters.", nameof(id));
            PlayerState existing = FindPlayer(id);
            if (existing != null)
            {
                if (!existing.connected && appearance.HasValue) existing.appearance = appearance.Value;
                SetConnected(id, true);
                return existing;
            }
            if (State.players.Count >= MaxPlayers) throw new InvalidOperationException("World has eight persistent player slots.");
            RefreshSpatial();
            int sx, sy;
            if (!FindSpawn(out sx, out sy)) throw new InvalidOperationException("No safe spawn cell available.");
            var p = new PlayerState { id = id, name = CleanName(name), seat = FirstFreeSeat(), x = sx, y = sy,
                appearance = appearance ?? AppearanceCatalog.DefaultCode };
            p.items = DemoCatalog.Materials(ItemKind.Wood,12, ItemKind.Scrap,4, ItemKind.Food,6);
            GiveStartingGear(State,p,false);
            State.players.Add(p);
            inputs.Add(id, new HeldInput { protectionUntil = State.elapsed + 4f });
            InitializeSurvivalPlayer(p, true);
            MapExploration.Reveal(State,p);
            Notice(p, PrimitiveCatalog.Enabled(State) ? "原始开局：探索并采集木材与石材，建造工作台和石炉。按T查看研究，科技全队共享。" : "原始开局：采矿、3木材制2燃料，石炉炼铁铜锡。科技全队共享。");
            flowRevision = -1;
            return p;
        }

        public void SetConnected(string id, bool connected)
        {
            PlayerState p = FindPlayer(id);
            if (p == null) return;
            p.connected = connected;
            HeldInput input = inputs[p.id];
            ClearPlayerControl(p, input);
            input.x = input.y = 0f; input.sprint = false;
            CancelReload(p); p.aiming=false; p.moving=false;
            flowRevision = -1;
        }

        public void Apply(PlayerCommand cmd)
        {
            if (cmd == null || cmd.playerId == null || cmd.playerId.Length > 64) return;
            PlayerState p = FindPlayer(cmd.playerId);
            if (p == null || !p.connected) return;
            string action = cmd.action ?? "";
            if (action.Length > 16) return;
            if (action == "appearance") return;
            HeldInput input = inputs[p.id];
            if (action.Length == 0 || action == "move")
            {
                if (!Finite(cmd.moveX) || !Finite(cmd.moveY) || !Finite(cmd.aimX) || !Finite(cmd.aimY)) return;
                float mx = cmd.moveX, my = cmd.moveY;
                Normalize(ref mx, ref my, false);
                if (mx != 0f || my != 0f || p.hp <= 0f) ClearPlayerControl(p, input);
                if (input.intent == PlayerControlIntent.None)
                { input.x = p.hp > 0f ? mx : 0f; input.y = p.hp > 0f ? my : 0f; }
                input.sprint = cmd.sprint && p.hp > 0f;
                p.aiming=cmd.aiming && p.hp>0f;
                p.moving=input.x!=0f || input.y!=0f;
                Aim(p, cmd.aimX, cmd.aimY);
                return;
            }
            // Bound discrete work even if a client floods the host between ticks.
            if (input.commandTick != State.tick) { input.commandTick = State.tick; input.commands = 0; }
            if (input.commands >= 10) return;
            input.commands++;
            if (ApplyPlayerControl(p, input, cmd)) return;
            ClearPlayerControl(p, input);
            if (action == "respawn") { Respawn(p); return; }
            if (p.hp <= 0f) return;
            RefreshSpatial();
            if (ApplySurvival(p, cmd)) { AllocateBackpackContents(p); return; }
            if(action=="moduleplan" || action=="moduleerase" || action=="moduleunload")
            {
                var machine=State.buildings.Find(b=>b.id==cmd.buildingId);
                if(p.interactCooldown>0f || !Inside(cmd.x,cmd.y) || DistSq(p.x,p.y,cmd.x,cmd.y)>Reach*Reach ||
                    !LineClear(p.x,p.y,cmd.x,cmd.y,cmd.x,cmd.y,machine)) return;
                p.interactCooldown=.25f; EditModularPlan(p,cmd,machine); AllocateBackpackContents(p); return;
            }
            try { ApplyInventoryCommand(p, cmd, action); }
            finally { AllocateBackpackContents(p); }
        }

        private void ApplyInventoryCommand(PlayerState p, PlayerCommand cmd, string action)
        {
            switch (action)
            {
                case "attack":
                    if (!Finite(cmd.aimX) || !Finite(cmd.aimY)) return;
                    Aim(p, cmd.aimX, cmd.aimY);
                    p.aiming=cmd.aiming;
                    Attack(p); return;
                case "packput": case "packtake": MoveBackpackItem(p, cmd, action == "packput"); return;
                case "storeitem": StoreBackpackItem(p, cmd.arg); return;
                case "gearcraft": CraftGear(p,cmd.arg); return;
                case "partcraft": CraftPart(p,cmd.arg); return;
                case "equip": EquipGear(p,cmd.arg); return;
                case "unequip": UnequipGear(p,cmd.arg); return;
                case "switch": SwitchGear(p,cmd.arg); return;
                case "attach": AttachPart(p,cmd.arg,cmd.x); return;
                case "detach": DetachPart(p,cmd.arg,cmd.x); return;
                case "reload": StartReload(p); return;
                case "ammoselect": SelectAmmo(p,cmd.arg); return;
                case "unload": UnloadGear(p,cmd.arg); return;
                case "gearrepair": RepairGear(p,cmd.arg); return;
                case "gearsalvage": SalvageGear(p,cmd.arg); return;
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
                case "primitive": ResearchPrimitive(p, cmd.arg); return;
                case "water": ResearchWater(p, cmd.arg); return;
                case "steam": ResearchSteam(p, cmd.arg); return;
                case "lv": ResearchLowVoltage(p, cmd.arg); return;
                case "mv": ResearchPetrochemical(p, cmd.arg); return;
                case "hv": ResearchPrecision(p, cmd.arg); return;
                case "nuclear": ResearchNuclear(p, cmd.arg); return;
                case "weapontech": ResearchEquipment(p, ResearchDomain.Weapon, cmd.arg); return;
                case "tooltech": ResearchEquipment(p, ResearchDomain.Tool, cmd.arg); return;
                case "prospect": ProspectPetrochemical(p); return;
                case "mill": Mill(p, cmd.x, cmd.y); return;
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
            if (ChunkStreaming.Step(State)) flowRevision = -1;
            RefreshSpatial();
            RefreshBonuses();
            foreach (PlayerState p in State.players) UpdatePlayer(p);
            if (State.tick % 5 == 0) foreach (PlayerState p in State.players) MapExploration.Reveal(State,p);
            UpdateSurvival();
            RefreshPower(true);
            UpdateWaterAgriculture();
            UpdateProduction();
            UpdateRadiation();
            UpdateTurrets();
            if (flowRevision < 0 || State.tick % 5 == 0) RebuildFlow();
            UpdateZombies();
            foreach (PlayerState player in State.players)
                if (player.backpackId != 0) AllocateBackpackContents(player);
            if (State.elapsed >= State.nextWaveAt && LivingPlayers() > 0) StartWave();
            else if (State.tick % 600 == 0 && LivingPlayers() > 0 && State.zombies.Count < 5 + Math.Min(12, State.wave))
                SpawnZombie((int)(State.tick % 3));
            State.traces.RemoveAll(t => t.until <= State.elapsed);
        }

        private void UpdatePlayer(PlayerState p)
        {
            p.attackCooldown = Math.Max(0f, p.attackCooldown - FixedStep);
            p.interactCooldown = Math.Max(0f, p.interactCooldown - FixedStep);
            UpdateEquipment(p);
            HeldInput input = inputs[p.id];
            if (!p.connected || p.hp <= 0f) { ClearPlayerControl(p, input); return; }
            UpdatePlayerControl(p, input);
            bool moving = input.x != 0f || input.y != 0f;
            p.moving=moving;
            bool sprinting = moving && input.sprint && !p.aiming && p.stamina >= 1.5f && p.hunger > 0f;
            float speed = 3.2f + p.attributes[2] * 0.09f + (p.skills[2] >= 2 ? 0.25f : 0f);
            if (sprinting) speed *= 1.6f;
            if (p.aiming) speed *= .6f;
            speed*=1f-EquipmentCatalog.MovementPenalty(p);
            speed *= SurvivalMovementMultiplier(p);
            if (p.hunger <= 0f) speed *= 0.7f;
            float distance = speed * FixedStep;
            if (input.intent != PlayerControlIntent.None) distance = Math.Min(distance, input.travelDistance);
            MoveCircle(ref p.x, ref p.y, input.x * distance, input.y * distance, 0.28f);
            p.stamina = Clamp(p.stamina + (sprinting ? -19f : 13f + p.skills[2] * 2f) * FixedStep, 0f, PlayerMaxStamina(p));
            float hungerRate = 0.085f * (1f - p.skills[2] * 0.15f) * (sprinting ? 2.8f : 1f);
            hungerRate *= SurvivalHungerMultiplier(p);
            p.hunger = Math.Max(0f, p.hunger - hungerRate * FixedStep);
            if (p.hunger <= 0f) DamagePlayer(p, 2.5f * FixedStep, false);
            else if (p.hunger >= 60f && State.elapsed - p.lastDamageAt > 7f)
                p.hp = Math.Min(PlayerMaxHealth(p), p.hp + (0.55f + (p.skills[2] >= 3 ? 1.2f : 0f)) * FixedStep);
        }

        private void Build(PlayerState p, PlayerCommand cmd)
        {
            if (cmd.arg < 0 || cmd.arg >= DemoCatalog.BuildNames.Length || cmd.rotation < 0 || cmd.rotation > 3) return;
            BuildKind kind = (BuildKind)cmd.arg;
            if (!TechCatalog.IsUnlocked(State, kind)) { Notice(p, PrimitiveCatalog.BuildRequirement(State,kind)); return; }
            if (State.buildings.Count >= MaxBuildings || State.blueprints.Count >= MaxBlueprints) { Notice(p, "设备或蓝图数量已达上限。"); return; }
            int width = DemoCatalog.Width(kind, cmd.rotation), height = DemoCatalog.Height(kind, cmd.rotation);
            for (int y = cmd.y; y < cmd.y + height; y++) for (int x = cmd.x; x < cmd.x + width; x++)
            {
                if (DroppedBackpackAt(x, y) != null) { Notice(p, "请先拾取地上的背包。"); return; }
                if (FloorWorld.IsProtectedCell(State, x, y)) { Notice(p, "无法建造：请保持楼梯及其出入口畅通。"); return; }
                if (SurvivalOccupiesCell(x, y)) { Notice(p, "无法建造：占地范围内有农田或生物。"); return; }
                if (BlockedAt(x, y) || NodeAt(x, y) != null || BuildingAt(x, y) != null || BlueprintAt(x, y) != null) { Notice(p, "无法建造：占地范围内有资源、障碍、水面、设备或蓝图。"); return; }
                if (NearSpawn(x, y)) { Notice(p, "无法建造：救援区须保持畅通。"); return; }
                if (kind != BuildKind.Belt && OccupiedByActor(x, y)) { Notice(p, "无法建造：占地范围内有人物。"); return; }
            }
            var probe = new BuildingState { x = cmd.x, y = cmd.y, rotation = cmd.rotation, kind = kind, footprintVersion = 1 };
            if ((kind == BuildKind.Miner || kind == BuildKind.SteamMiner) && NearbyOre(probe) == null)
            { Notice(p, "采矿机需紧邻未耗尽矿脉（含斜邻格）。"); return; }
            if (kind == BuildKind.OilPump && NearbyPetroleum(probe) == null)
            { Notice(p, "抽油机需紧邻未耗尽油藏（含斜邻格）。"); return; }
            if (TechCatalog.RequiresWater(kind) && !NearWater(probe))
            { Notice(p, "设备须紧邻水面，且占地全部位于陆地。"); return; }
            if (ModularMachineCatalog.IsModular(kind))
            {
                if (!ModularMachineCatalog.HasWrench(p)) { Notice(p, "放置大型蓝图需要手持耐久正常的扳手。"); return; }
                var machine = new BuildingState { id = AllocateId(), x = cmd.x, y = cmd.y, rotation = cmd.rotation, kind = kind,
                    footprintVersion = 1, modularBoiler = kind == BuildKind.Boiler, hp = BuildingMaxHealth(kind) };
                machine.modularModules.Add(new MachineModuleState { role = MachineModuleRole.Controller, x = cmd.x, y = cmd.y, rotation = cmd.rotation });
                State.buildings.Add(machine); RefreshSpatial(); RefreshPower(false); flowRevision = -1;
                Notice(p, "已放置控制器蓝图：" + DemoCatalog.BuildName(kind) + "，从模块菜单继续绘制并投料。");
                return;
            }
            // Ordinary buildings start as free plans; the full build cost is delivered by hand.
            State.blueprints.Add(new BlueprintState { id = AllocateId(), x = cmd.x, y = cmd.y, rotation = cmd.rotation, kind = kind });
            flowRevision = -1;
            Notice(p, "已放置蓝图：" + DemoCatalog.BuildName(kind) + "，走近按 E 投料。");
        }

        private void Repair(PlayerState p, int x, int y)
        {
            BuildingState b = BuildingAt(x, y);
            if (b == null) return;
            if(ModularMachineCatalog.IsModular(b.kind)) { RepairModular(p,b); return; }
            if(b.hp >= BuildingMaxHealth(b.kind)) return;
            int material = b.kind == BuildKind.Wall || b.kind == BuildKind.Door ? (int)ItemKind.Wood : (int)ItemKind.Scrap;
            if (p.items[material] < 1) { Notice(p, "修理需要 1 " + DemoCatalog.ItemNames[material]); return; }
            p.items[material]--;
            GearState wrench=EquippedWrench(p);
            b.hp = Math.Min(BuildingMaxHealth(b.kind), b.hp + 32f + p.attributes[4] * 2f + p.skills[0] * 8f + (wrench!=null ? 20f : 0f));
            if(wrench!=null) wrench.durability--;
            Notice(p, "已修理。");
        }

        private void Dismantle(PlayerState p, int x, int y)
        {
            BlueprintState plan = BlueprintAt(x, y);
            if (plan != null) { CancelBlueprint(p, plan); return; }
            BuildingState b = BuildingAt(x, y);
            if (b == null) return;
            if(ModularMachineCatalog.IsModular(b.kind)) { DismantleModular(p,b); return; }
            int[] cost = DemoCatalog.BuildCost(b.kind);
            var returned = new int[DemoCatalog.ItemCount];
            float refund = p.skills[0] >= 3 ? 1f : p.skills[0] >= 2 ? 0.75f : 0.5f;
            GearState wrench=EquippedWrench(p);
            if(wrench!=null) refund=Math.Min(1f,refund+.25f);
            float health = Clamp(b.hp / BuildingMaxHealth(b.kind), 0f, 1f);
            if (b.items[(int)ItemKind.NuclearWaste] > 0 || State.cargo.Exists(c => c.item == (int)ItemKind.NuclearWaste && (c.fromId == b.id || c.toId == b.id)))
            {
                Notice(p, "核废料必须先转移到专用废料仓，不能拆除并带回背包。");
                return;
            }
            for (int i = 0; i < DemoCatalog.ItemCount; i++)
            {
                returned[i] = b.items[i] + (int)Math.Floor(cost[i] * refund * health + 0.00001f);
            }
            foreach (CargoState cargo in State.cargo) if (cargo.fromId == b.id || cargo.toId == b.id) returned[cargo.item]++;
            if (!InventoryWeight.CanFit(p, InventoryWeight.MaterialsGrams(returned)))
            { Notice(p, "背包负重不足以收回全部库存和建材。"); return; }
            for (int i = 0; i < DemoCatalog.ItemCount; i++) p.items[i] += returned[i];
            State.cargo.RemoveAll(c => c.fromId == b.id || c.toId == b.id);
            State.buildings.Remove(b); RefreshSpatial(); RefreshPower(false); flowRevision = -1;
            if(wrench!=null) wrench.durability--;
            Notice(p, "已拆除。库存已全部收回，建材按设备耐久与工程专精返还；燃烧中的燃料不返还。");
        }

        private void Craft(PlayerState p, int arg)
        {
            if (p.interactCooldown > 0f || arg < 0) return;
            if (arg >= 2)
            {
                RecipeDefinition r = TechCatalog.Recipe(arg);
                if (r == null || r.building != BuildKind.Workbench || State.era < r.era) return;
                if (!PrimitiveCatalog.IsRecipeUnlocked(State,r)) { Notice(p,PrimitiveCatalog.RecipeRequirement(State,r)); return; }
                bool bench = EquipmentCatalog.HasWorkbench(State,p);
                if (!bench) { Notice(p, "请靠近工作台。"); return; }
                if (!CanPay(p.items, r.inputs) || !InventoryWeight.CanExchange(p, r.inputs, r.outputs)) { Notice(p, "配方材料不足或背包已满。"); return; }
                Pay(p.items,r.inputs);
                for(int i=0;i<DemoCatalog.ItemCount;i++) { p.items[i]+=r.outputs[i]; RecordProduced(i,r.outputs[i]); }
                RecordLowVoltageProduction(null,r);
                p.interactCooldown=.5f; Notice(p,"已制作："+r.name); return;
            }
            if(arg==1)
            {
                if(State.era<TechEra.LowVoltage) { Notice(p,"需要先进入："+TechCatalog.EraName(TechEra.LowVoltage)); return; }
                if(!EquipmentResearchCatalog.IsAmmoUnlocked(State,ItemKind.Ammo))
                { Notice(p,EquipmentResearchCatalog.Missing(EquipmentResearchCatalog.AmmoResearch(ItemKind.Ammo))); return; }
                if(!EquipmentCatalog.HasWorkbench(State,p)) { Notice(p,"请靠近工作台，并确保中间没有障碍。"); return; }
            }
            int source = arg == 0 ? (int)ItemKind.Wood : (int)ItemKind.Iron;
            int target = arg == 0 ? (int)ItemKind.Fuel : (int)ItemKind.Ammo;
            int take = arg == 0 ? 3 : 1, give = arg == 0 ? 2 : 4;
            if (p.items[source] < take || !InventoryWeight.CanFit(p, give * InventoryWeight.Grams((ItemKind)target) - take * InventoryWeight.Grams((ItemKind)source)))
            { Notice(p, "制作失败：材料不足或背包已满。木材×3 → 燃料×2；铁锭×1 → 9mm弹×4。"); return; }
            p.items[source] -= take; p.items[target] += give; p.interactCooldown = 0.5f;
            RecordProduced(target,give);
            // Manual crafting deliberately awards no XP: dismantle/rebuild and crafting cannot farm levels.
            Notice(p, arg == 0 ? "已制作 2 燃料。" : "已制作 4 弹药。");
        }

        private void Research(PlayerState p,int arg)
        {
            if (p.interactCooldown > 0f || arg < 1 || arg > 6) return;
            TechEra target=(TechEra)arg;
            string requirement=TechCatalog.ResearchRequirement(State,p,target);
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
            if(ConfigureElectrical(p,b,arg)) return;
            if(ConfigurePetrochemical(p,b,arg)) return;
            if(ConfigurePrecision(p,b,arg)) return;
            if(b.kind==BuildKind.Door)
            {
                if(arg==0 || arg==1) SetDoor(p,b,arg);
                return;
            }
            if(b.kind==BuildKind.Transformer)
            {
                if(arg<0 || arg>1) return;
                if(arg==1 && !PrecisionCatalog.HasResearch(State,PrecisionTech.Grid)) { Notice(p,"需要高压研究：高压电网。"); return; }
                b.setting=arg; b.progress=0f; RefreshPower(false); Notice(p,arg==0 ? "变压器已设为中压 → 低压" : "变压器已设为高压 → 中压"); return;
            }
            if(b.kind==BuildKind.ReactorControlConsole)
            {
                if(arg!=0 && arg!=1) { Notice(p,"请选择启用或停用反应堆控制台。"); return; }
                b.setting=arg; RefreshPower(false); Notice(p,arg==1 ? "反应堆控制台已启用。" : "反应堆控制台已停用。"); return;
            }
            RecipeDefinition r=TechCatalog.Recipe(arg);
            if(r==null || r.building!=b.kind || b.kind==BuildKind.Workbench || r.era>State.era) return;
            if(!PrimitiveCatalog.IsRecipeUnlocked(State,r)) { Notice(p,PrimitiveCatalog.RecipeRequirement(State,r)); return; }
            if(ModularMachineCatalog.IsModular(b.kind) && b.recipe!=arg && State.cargo.Exists(c=>c.fromId==b.id || c.toId==b.id))
            { Notice(p,"模块机械有在途物品，送达后才能更换配方。"); return; }
            if(b.recipe!=arg) foreach(var cargo in State.cargo)
                if(PetrochemicalCatalog.IsFluid(cargo.item) && (cargo.fromId==b.id || cargo.toId==b.id))
                { Notice(p,"仍有液体正在运输，请等待送达后再切换配方。"); return; }
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
            if (!FindSpawn(out x, out y, p)) { Notice(p, "暂时找不到安全落脚处，无法复活。"); return; }
            p.x = x; p.y = y; p.hp = PlayerMaxHealth(p); p.stamina = PlayerMaxStamina(p);
            p.hunger = Math.Max(45f, p.hunger); p.attackCooldown = 0.5f;
            ResetSurvivalAfterRespawn(p);
            p.radiationDose = p.radiationWear = 0f;
            p.lastDamageAt = State.elapsed;
            HeldInput input = inputs[p.id]; input.x = input.y = 0f; input.sprint = false; input.protectionUntil = State.elapsed + 5f;
            Notice(p, "已复活，保留背包与成长；短暂无敌。"); flowRevision = -1;
        }

        private void GainXp(PlayerState p, int amount)
        {
            if (p.level >= MaxLevel || amount <= 0) return;
            p.xp += Math.Max(1, (int)(amount * (1f + p.attributes[4] * 0.05f)));
            while (p.level < MaxLevel && p.xp >= XpToNextLevel(p.level))
            {
                p.xp -= XpToNextLevel(p.level); p.level++; p.attributePoints += 2; p.skillPoints++;
                p.hp = Math.Min(PlayerMaxHealth(p), p.hp + 15f);
                Notice(p, "等级提升！属性点 +2，技能点 +1。第 2、3 阶技能分别需要角色等级 3、5。");
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
                case BuildKind.Door: return 220f;
                case BuildKind.Wall: return 240f; case BuildKind.Generator: return 160f;
                case BuildKind.Miner: return 140f; case BuildKind.Belt: return 60f;
                case BuildKind.Smelter: return 180f; case BuildKind.Assembler: return 160f;
                case BuildKind.Turret: return 180f; default: return 120f;
            }
        }

        private PlayerState FindPlayer(string id) { if (id == null) return null; return State.players.Find(p => p.id == id); }
        private void Notice(PlayerState p, string text) { p.notice = text; p.noticeUntil = State.elapsed + 6f; }
        private bool Inside(int x, int y) { return WorldTiles.Inside(State,x,y); }
        private long Cell(int x, int y) { return WorldTiles.Key(x,y); }
        private BuildingState BuildingAt(int x, int y) { return Inside(x, y) ? buildingGrid[Cell(x, y)] : null; }

        private BlueprintState BlueprintAt(int x, int y)
        {
            if (!Inside(x, y)) return null;
            foreach (BlueprintState plan in State.blueprints)
                if (x >= plan.x && x < plan.x + DemoCatalog.Width(plan.kind, plan.rotation) &&
                    y >= plan.y && y < plan.y + DemoCatalog.Height(plan.kind, plan.rotation)) return plan;
            return null;
        }
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
