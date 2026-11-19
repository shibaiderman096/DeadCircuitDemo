using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        // Reject structural/economic corruption rather than silently truncating entities or granting
        // replacement inventories. Never mutate the caller's graph, including its nested arrays.
        private static WorldState CopyAndValidate(WorldState source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            SaveCheck(source.version >= 1 && source.version <= SaveVersion, "unsupported version");
            bool legacy=source.version<3;
            int itemCount=legacy ? 7 : source.version==3 ? 21 : source.version<11 ? 25 : DemoCatalog.ItemCount;
            if(!legacy)
            {
                SaveCheck((int)source.era>=0 && (int)source.era<=6, "technology era");
                SaveCheck(source.produced!=null && source.produced.Length==itemCount, "production counters length");
                foreach(int n in source.produced) SaveCheck(n>=0, "production counter");
                SaveCheck(source.powerNetworks!=null && source.powerNetworks.Count<=MaxBuildings, "power networks");
                foreach(var n in source.powerNetworks)
                {
                    SaveCheck(n!=null && n.id>0 && n.tier>=0 && n.tier<=4 && n.nodes>0 && n.nodes<=MaxBuildings && n.consumers>=0 && n.consumers<=MaxBuildings, "power network fields");
                    SaveRange(n.generation,0f,2000000f,"network generation"); SaveRange(n.demand,0f,2000000f,"network demand"); SaveRange(n.ratio,0f,1f,"network ratio");
                }
            }
            bool chunked=source.version>=8 && ChunkStreaming.Enabled(source);
            SaveCheck(source.width == 64 && source.height == 64, "map dimensions");
            SaveCheck(source.ground != null && source.ground.Length == source.width*source.height, "ground length");
            foreach (char tile in source.ground) SaveCheck(tile >= '0' && tile <= '4', "ground kind");
            SaveCheck(source.tick >= 0 && source.tick < long.MaxValue - 1, "tick");
            SaveRange(source.elapsed, 0f, 10000000f, "elapsed");
            SaveRange(source.nextWaveAt, 0f, source.elapsed + 10000f, "nextWaveAt");
            SaveRange(source.dayLength, 30f, 86400f, "dayLength");
            SaveRange(source.announcementUntil, 0f, source.elapsed + 120f, "announcementUntil");
            SaveCheck(source.wave >= 0 && source.wave <= 10000 && source.kills >= 0 && source.gathered >= 0 && source.manufactured >= 0, "world counters");
            SaveCheck(source.players != null && source.players.Count <= MaxPlayers, "player list");
            SaveCheck(source.nodes != null && source.nodes.Count <= MaxNodes, "node list");
            SaveCheck(source.buildings != null && source.buildings.Count <= MaxBuildings, "building list");
            SaveCheck(source.zombies != null && source.zombies.Count <= MaxZombies, "zombie list");
            SaveCheck(source.traces != null && source.traces.Count <= MaxTraces, "trace list");
            SaveCheck((source.cargo != null && source.cargo.Count <= MaxCargo) || (source.version == 1 && source.cargo == null), "cargo list");
            SaveCheck(source.announcement != null && source.announcement.Length <= 512, "announcement");
            var s = new WorldState
            {
                version = SaveVersion, era = legacy ? TechEra.Primitive : source.era, produced = legacy ? new int[DemoCatalog.ItemCount] : CopyItems(source.produced,int.MaxValue,"production counters",itemCount), seed = source.seed, width = source.width, height = source.height,
                nextId = source.nextId, wave = source.wave, tick = source.tick, elapsed = source.elapsed,
                nextWaveAt = source.nextWaveAt, dayLength = source.dayLength, ground = source.ground,
                announcement = source.announcement, announcementUntil = source.announcementUntil,
                kills = source.kills, manufactured = source.manufactured, gathered = source.gathered
            };
            CopyPrimitiveProgression(source, s);
            var entityIds = new HashSet<int>();
            CopyChunks(source,s);
            var occupied = new HashSet<long>();
            var solid = new HashSet<long>();
            int highestId = 0;
            foreach (NodeState n in source.nodes)
            {
                SaveCheck(n != null, "null node"); CheckEntityId(n.id, entityIds, ref highestId);
                CheckCell(n.x, n.y,source); long cell = WorldTiles.Key(n.x,n.y);
                SaveCheck((int)n.kind >= 0 && (int)n.kind <= (legacy ? 4 : source.version<11 ? 7 : 9), "node kind");
                char terrain=WorldTiles.Ground(source,n.x,n.y);
                SaveCheck(occupied.Add(cell) && terrain != '2' && terrain!='?', "overlapping/water node"); solid.Add(cell);
                SaveCheck(n.stock > 0 && n.stock <= 100000, "node stock"); SaveRange(n.hp, 0.001f, 10000f, "node hp");
                s.nodes.Add(new NodeState { id = n.id, x = n.x, y = n.y, kind = n.kind, stock = n.stock, hp = n.hp });
            }
            foreach (BuildingState b in source.buildings)
            {
                SaveCheck(b != null, "null building"); CheckEntityId(b.id, entityIds, ref highestId);
                CheckCell(b.x, b.y,source);
                SaveCheck((int)b.kind >= 0 && (int)b.kind < (legacy ? 10 : source.version < 7 ? 31 : source.version<11 ? 36 : DemoCatalog.BuildNames.Length) && b.rotation >= 0 && b.rotation <= 3, "building kind/rotation");
                SaveCheck(b.footprintVersion == 0 || b.footprintVersion == 1, "footprint version");
                for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++) for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++)
                {
                    CheckCell(x, y,source); long cell = WorldTiles.Key(x,y);char terrain=WorldTiles.Ground(source,x,y);
                    SaveCheck(occupied.Add(cell) && terrain != '2' && terrain!='?', "overlapping/water building");
                    if (!DemoCatalog.Walkable(b)) solid.Add(cell);
                }
                SaveRange(b.hp, 0.001f, BuildingMaxHealth(b.kind), "building hp");
                SaveRange(b.progress,0f,4f,"machine progress");
                SaveRange(b.fuel,0f,b.kind==BuildKind.HighGenerator ? 10.01f : 20.01f,"fuel seconds");
                SaveCheck(TechCatalog.IsFuelBurner(b.kind) || b.kind==BuildKind.Reactor || b.fuel==0f,"fuel in non-burner");
                if(!legacy)
                {
                    SaveCheck(b.setting>=0 && b.setting<=(b.kind==BuildKind.Transformer || b.kind==BuildKind.Door ? 1 : 0),"machine setting");
                    SaveCheck(source.version<7 || !b.modularBoiler || (b.kind==BuildKind.Boiler && b.fuel==0f),"modular boiler mode");
                    SaveCheck(b.kind!=BuildKind.Transformer || b.setting==0 || source.era>=TechEra.HighVoltage,"unresearched transformer setting");
                    SaveRange(b.temperature,20f,1200f,"machine temperature");
                    SaveCheck(b.kind==BuildKind.Reactor || (!b.tripped && b.temperature==20f),"reactor-only state");
                    RecipeDefinition r=TechCatalog.SelectedRecipe(b);
                    SaveCheck(b.recipe==0 || (r!=null && r.building==b.kind && b.kind!=BuildKind.Workbench),"machine recipe");
                    SaveCheck(r==null || r.era<=source.era,"unresearched machine recipe");
                    if(r!=null) SaveRange(b.progress,0f,r.duration,"recipe progress");
                    if(b.kind==BuildKind.Reactor) SaveRange(b.progress,0f,1.001f,"reactor coolant reserve");
                }
                int[] items=CopyItems(b.items,BufferLimit,"machine inventory",itemCount);
                SaveCheck(b.kind!=BuildKind.Belt || TotalItems(items)<=2,"belt capacity");
                for(int i=0;i<DemoCatalog.ItemCount;i++) if(items[i]!=0) SaveCheck(TechCatalog.Stores(b.kind,i),"incompatible machine inventory");
                s.buildings.Add(new BuildingState { id=b.id,x=b.x,y=b.y,kind=b.kind,rotation=b.rotation,footprintVersion=b.footprintVersion,hp=b.hp,progress=b.progress,fuel=b.fuel,items=items,recipe=legacy ? 0 : b.recipe,setting=legacy ? 0 : b.setting,temperature=legacy ? 20f : b.temperature,tripped=!legacy && b.tripped,modularBoiler=source.version>=7 && b.modularBoiler });
                if(legacy)
                {
                    if(b.kind==BuildKind.SolarPanel) s.era=TechEra.MediumVoltage;
                    else if(TechCatalog.RequiredEra(b.kind)>=TechEra.LowVoltage && s.era<TechEra.LowVoltage) s.era=TechEra.LowVoltage;
                }
            }
            var byId = new Dictionary<int, BuildingState>();
            foreach (BuildingState b in s.buildings) byId.Add(b.id, b);
            var reservations = new Dictionary<int, int[]>();
            var sources = new HashSet<int>();
            if (source.cargo != null) foreach (CargoState c in source.cargo)
            {
                SaveCheck(c != null, "null cargo"); CheckEntityId(c.id, entityIds, ref highestId);
                SaveCheck(c.item >= 0 && c.item < itemCount && c.fromId != c.toId && sources.Add(c.fromId), "cargo item/source");
                BuildingState from, to;
                SaveCheck(byId.TryGetValue(c.fromId, out from) && byId.TryGetValue(c.toId, out to), "cargo endpoints");
                to = byId[c.toId]; from = byId[c.fromId];
                SaveCheck(DemoCatalog.Contains(to, DemoCatalog.OutputX(from), DemoCatalog.OutputY(from)) && Accepts(to.kind, c.item), "cargo route");
                SaveRange(c.fromX, from.x - .5f, from.x + DemoCatalog.Width(from) - .5f, "cargo from x");
                SaveRange(c.fromY, from.y - .5f, from.y + DemoCatalog.Height(from) - .5f, "cargo from y");
                SaveRange(c.toX, to.x - .5f, to.x + DemoCatalog.Width(to) - .5f, "cargo to x");
                SaveRange(c.toY, to.y - .5f, to.y + DemoCatalog.Height(to) - .5f, "cargo to y");
                SaveRange(c.progress, 0f, 1f, "cargo progress"); SaveRange(c.duration, .02f, 8f, "cargo duration");
                int[] reserved;
                if (!reservations.TryGetValue(to.id, out reserved)) { reserved = new int[DemoCatalog.ItemCount]; reservations.Add(to.id, reserved); }
                reserved[c.item]++;
                SaveCheck(to.items[c.item] + reserved[c.item] <= BufferLimit &&
                    (to.kind != BuildKind.Belt || TotalItems(to.items) + TotalItems(reserved) <= 2), "cargo reservation capacity");
                s.cargo.Add(new CargoState { id = c.id, fromId = c.fromId, toId = c.toId, item = c.item,
                    fromX = c.fromX, fromY = c.fromY, toX = c.toX, toY = c.toY, progress = c.progress, duration = c.duration });
            }
            var playerIds = new HashSet<string>(StringComparer.Ordinal);
            var seats = new HashSet<int>();
            foreach (PlayerState p in source.players)
            {
                SaveCheck(p != null, "null player");
                SaveCheck(!string.IsNullOrWhiteSpace(p.id) && p.id.Length <= 64 && !HasControl(p.id) && playerIds.Add(p.id), "player id");
                SaveCheck(p.seat >= 0 && p.seat < MaxPlayers && seats.Add(p.seat), "player seat");
                SaveCheck(p.name != null && p.name.Length <= 128 && p.notice != null && p.notice.Length <= 256, "player text");
                SaveCheck(p.level >= 1 && p.level <= MaxLevel && p.deaths >= 0, "player level/deaths");
                int[] attributes = CopyStats(p.attributes, 5, 10, "attributes");
                int[] skills = CopyStats(p.skills, 3, 3, "skills");
                SaveCheck(p.attributePoints >= 0 && p.skillPoints >= 0 &&
                    (long)TotalItems(attributes) + p.attributePoints <= p.level * 2L &&
                    (long)TotalItems(skills) + p.skillPoints <= p.level, "independent point budgets");
                foreach (int rank in skills) SaveCheck(rank == 0 || p.level >= 1 + (rank - 1) * 2, "skill prerequisite");
                SaveCheck(p.xp >= 0 && p.xp < XpToNextLevel(p.level), "xp");
                CheckPosition(p.x, p.y,source);
                SaveRange(p.hp, 0f, 100f + attributes[1] * 15f, "player hp");
                SaveRange(p.hunger, 0f, 100f, "hunger");
                SaveRange(p.stamina, 0f, 100f + attributes[2] * 8f + skills[2] * 10f, "stamina");
                SaveCheck(Finite(p.aimX) && Finite(p.aimY), "aim");
                SaveRange(p.attackCooldown, 0f, 5f, "attack cooldown"); SaveRange(p.interactCooldown, 0f, 5f, "interact cooldown");
                SaveRange(p.noticeUntil, 0f, source.elapsed + 120f, "noticeUntil");
                SaveRange(p.lastDamageAt, 0f, source.elapsed, "lastDamageAt");
                int px=RoundCell(p.x),py=RoundCell(p.y); long cell = WorldTiles.Key(px,py);char playerTerrain=WorldTiles.Ground(source,px,py);
                SaveCheck(p.hp <= 0f || (playerTerrain != '2' && playerTerrain!='?' && !solid.Contains(cell)), "living player in solid cell");
                var clone = new PlayerState
                {
                    id = p.id, name = CleanName(p.name), notice = p.notice, seat = p.seat, level = p.level, xp = p.xp,
                    attributePoints = p.attributePoints, skillPoints = p.skillPoints, deaths = p.deaths, connected = p.connected,
                    x = p.x, y = p.y, aimX = 1f, aimY = 0f, hp = p.hp, hunger = p.hunger, stamina = p.stamina,
                    attackCooldown = p.attackCooldown, interactCooldown = p.interactCooldown, noticeUntil = p.noticeUntil, lastDamageAt = p.lastDamageAt,
                    items = CopyItems(p.items, InventoryLimit, "player inventory",itemCount), attributes = attributes, skills = skills
                };
                if(source.version>=4) CopyEquipment(p,clone,entityIds,ref highestId);
                CopyNeeds(p, clone, source.version);
                clone.explored=CopyExplored(source,p);
                clone.exploredChunks=CopyExploredChunks(source,p);
                Aim(clone, p.aimX, p.aimY); s.players.Add(clone);
            }
            foreach (ZombieState z in source.zombies)
            {
                SaveCheck(z != null, "null zombie"); CheckEntityId(z.id, entityIds, ref highestId);
                CheckPosition(z.x, z.y,source); SaveRange(z.hp, 0.001f, 1000f, "zombie hp"); SaveRange(z.attackCooldown, 0f, 5f, "zombie cooldown");
                SaveCheck(z.variant >= 0 && z.variant <= (source.version >= 6 ? 11 : 2), "zombie variant");
                if (source.version >= 6) SaveRange(z.alertedUntil, 0f, source.elapsed + 8.1f, "enemy alert duration");
                int zx=RoundCell(z.x),zy=RoundCell(z.y);long cell = WorldTiles.Key(zx,zy);char zombieTerrain=WorldTiles.Ground(source,zx,zy);
                SaveCheck(zombieTerrain != '2' && zombieTerrain!='?' && !solid.Contains(cell), "zombie in solid cell");
                s.zombies.Add(new ZombieState { id = z.id, x = z.x, y = z.y, hp = z.hp, variant = z.variant, attackCooldown = z.attackCooldown, alertedUntil = source.version >= 6 ? z.alertedUntil : 0f });
            }
            foreach (TraceState t in source.traces)
            {
                SaveCheck(t != null, "null trace");
                float traceMin=chunked ? -1048592f : -16f,traceMax=chunked ? 1048592f : 80f;
                SaveRange(t.x, traceMin, traceMax, "trace x"); SaveRange(t.y, traceMin, traceMax, "trace y");
                SaveRange(t.endX, traceMin, traceMax, "trace endX"); SaveRange(t.endY, traceMin, traceMax, "trace endY");
                SaveRange(t.until, 0f, source.elapsed + 1f, "trace lifetime");
                SaveCheck(source.version < 6 || (t.effect >= 0 && t.effect <= 8), "trace effect");
                if (t.until > source.elapsed) s.traces.Add(new TraceState { x = t.x, y = t.y, endX = t.endX, endY = t.endY, until = t.until, hostile = t.hostile, effect = source.version >= 6 ? t.effect : 0 });
            }
            CopyExploration(source, s, entityIds, ref highestId);
            ValidateFloors(s, occupied);
            CopySurvival(source, s, entityIds, ref highestId);
            SaveCheck(source.nextId > 0 && source.nextId < int.MaxValue - 1 && highestId < int.MaxValue - 2, "next entity id");
            if(source.version>=4) SaveCheck(source.nextId>highestId,"next entity id must exceed all equipment and world ids");
            s.nextId = Math.Max(source.nextId, highestId + 1);
            if(legacy) EnsureMigratedResources(s);
            if(source.version<4) foreach(PlayerState p in s.players)
            {
                GiveStartingGear(s,p,true);
                p.notice="旧存档已迁移：物资与成长保留；补发空匣基础手枪，请装备后装填原9mm弹。";
                p.noticeUntil=s.elapsed+12f;
            }
            return s;
        }

        private static void SaveCheck(bool ok, string message) { if (!ok) throw new ArgumentException("Invalid save: " + message, "restored"); }
        private static void SaveRange(float value, float minimum, float maximum, string message) { SaveCheck(Finite(value) && value >= minimum && value <= maximum, message); }
        private static void CheckCell(int x, int y,WorldState world=null) { SaveCheck(world==null ? x>=0 && x<64 && y>=0 && y<64 : WorldTiles.Inside(world,x,y),"entity cell"); }
        private static void CheckPosition(float x, float y,WorldState world=null)
        {
            if(ChunkStreaming.Enabled(world))
            {
                SaveRange(x,-WorldTiles.CoordinateLimit-.5f,WorldTiles.CoordinateLimit-.5f,"entity x");
                SaveRange(y,-WorldTiles.CoordinateLimit-.5f,WorldTiles.CoordinateLimit-.5f,"entity y");
                SaveCheck(WorldTiles.Inside(world,RoundCell(x),RoundCell(y)),"entity rounded cell");
                return;
            }
            SaveRange(x,-.49f,63.49f,"entity x");
            SaveRange(y,-.49f,63.49f,"entity y");
        }
        private static void CheckEntityId(int id, HashSet<int> ids, ref int highest) { SaveCheck(id > 0 && id < int.MaxValue - 2 && ids.Add(id), "duplicate/invalid entity id"); highest = Math.Max(highest, id); }
        private static int[] CopyItems(int[] items,int maximum,string label,int itemCount)
        {
            SaveCheck(items!=null && items.Length==itemCount,label+" length");
            var result=new int[DemoCatalog.ItemCount];
            for(int i=0;i<items.Length;i++) { SaveCheck(items[i]>=0 && items[i]<=maximum,label+" value"); result[i]=items[i]; }
            return result;
        }
        private static void CopyEquipment(PlayerState source,PlayerState target,HashSet<int> ids,ref int highestId)
        {
            SaveCheck(source.gear!=null && source.gear.Count<=EquipmentCatalog.MaxGear,"gear list");
            target.parts=CopyStats(source.parts,EquipmentCatalog.PartNames.Length,InventoryLimit,"part inventory");
            foreach(GearState g in source.gear)
            {
                SaveCheck(g!=null,"null gear"); CheckEntityId(g.id,ids,ref highestId);
                SaveCheck((int)g.kind>=0 && (int)g.kind<EquipmentCatalog.GearNames.Length,"gear kind");
                SaveCheck(g.durability>=0 && g.durability<=100,"gear durability");
                SaveCheck(g.attachments!=null && g.attachments.Length==EquipmentCatalog.SlotNames.Length,"attachment slots");
                for(int i=0;i<g.attachments.Length;i++)
                {
                    int part=g.attachments[i];
                    SaveCheck(part>=-1 && part<EquipmentCatalog.PartNames.Length,"attachment kind");
                    if(part>=0) SaveCheck((int)EquipmentCatalog.Part((PartKind)part).slot==i && EquipmentCatalog.PartCompatible(g.kind,(PartKind)part),"attachment compatibility/slot");
                }
                bool gun=EquipmentCatalog.IsGun(g.kind);
                SaveCheck(g.magazine>=0 && g.magazine<=EquipmentCatalog.Stats(g).magazine,"magazine capacity");
                SaveCheck(gun ? EquipmentCatalog.AmmoCompatible(g.kind,g.loadedAmmo) : g.loadedAmmo==ItemKind.Ammo && g.magazine==0,"loaded ammunition");
                target.gear.Add(new GearState { id=g.id,kind=g.kind,durability=g.durability,magazine=g.magazine,loadedAmmo=g.loadedAmmo,attachments=(int[])g.attachments.Clone() });
            }
            SaveCheck(source.activeSlot>=0 && source.activeSlot<=2 && source.weaponId>=0 && source.toolId>=0,"equipped slots");
            GearState weapon=EquipmentCatalog.Find(target,source.weaponId), tool=EquipmentCatalog.Find(target,source.toolId);
            SaveCheck(source.weaponId==0 || weapon!=null && !EquipmentCatalog.Gear(weapon.kind).tool && !EquipmentCatalog.IsArmor(weapon.kind),"weapon ownership/type");
            SaveCheck(source.toolId==0 || tool!=null && EquipmentCatalog.Gear(tool.kind).tool,"tool ownership/type");
            SaveCheck(source.armorIds!=null && source.armorIds.Length==4,"armor slots");
            var worn=new HashSet<int>();
            for(int i=0;i<source.armorIds.Length;i++)
            {
                int id=source.armorIds[i];
                SaveCheck(id>=0,"negative armor id");
                if(id==0) continue;
                GearState armor=EquipmentCatalog.Find(target,id);
                SaveCheck(worn.Add(id) && armor!=null && EquipmentCatalog.ArmorSlot(armor.kind)==i,"armor ownership/duplicate/slot");
            }
            target.armorIds=(int[])source.armorIds.Clone();
            SaveCheck(EquipmentCatalog.IsAmmo(source.selectedAmmo),"selected ammunition");
            SaveRange(source.recoil,0f,1f,"recoil"); SaveRange(source.reloadRemaining,0f,5f,"reload timer");
            target.weaponId=source.weaponId; target.toolId=source.toolId; target.activeSlot=source.activeSlot; target.selectedAmmo=source.selectedAmmo;
            target.recoil=source.recoil; target.aiming=source.aiming; target.moving=source.moving;
            SaveCheck(source.reloadingGearId>=0,"reload gear id");
            if(source.reloadingGearId==0) SaveCheck(source.reloadRemaining==0f,"orphaned reload timer");
            else
            {
                GearState loading=EquipmentCatalog.Equipped(target);
                SaveCheck(source.connected && source.hp>0f && loading!=null && loading.id==source.reloadingGearId && EquipmentCatalog.IsGun(loading.kind) && loading.durability>0,"reload ownership/active gear");
                SaveCheck(EquipmentCatalog.AmmoCompatible(loading.kind,source.selectedAmmo) && (loading.magazine==0 || loading.loadedAmmo==source.selectedAmmo),"reload ammunition");
                SaveCheck(loading.magazine<EquipmentCatalog.Stats(loading).magazine && source.reloadRemaining>0f && source.reloadRemaining<=EquipmentCatalog.Stats(loading).reload+.001f,"reload state");
            }
            target.reloadRemaining=source.reloadRemaining; target.reloadingGearId=source.reloadingGearId;
        }
        private static int[] CopyStats(int[] values, int length, int maximum, string label)
        {
            SaveCheck(values != null && values.Length == length, label + " length");
            var result = new int[length];
            for (int i = 0; i < length; i++) { SaveCheck(values[i] >= 0 && values[i] <= maximum, label + " value"); result[i] = values[i]; }
            return result;
        }
    }
}
