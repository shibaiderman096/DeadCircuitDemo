using System;

namespace DeadCircuit
{
    public sealed class GearDefinition
    {
        public GearKind kind;
        public string name;
        public string description = "";
        public TechEra era;
        public int[] cost;
        public int maxDurability, magazine, pellets;
        public bool tool;
        public ItemKind ammo;
        public float damage, range, interval, reload, spread, recoil;
        public int armorSlot = -1;
        public float armor, movePenalty;
    }

    public sealed class PartDefinition
    {
        public PartKind kind;
        public string name, description;
        public AttachmentSlot slot;
        public TechEra era;
        public int[] cost;
    }

    public struct WeaponStats
    {
        public float damage, range, interval, reload, spread, recoil;
        public int magazine, pellets;
    }

    /// <summary>Shared display/rules catalog. All costs returned here are independent arrays.
    /// Spread/recoil are radians; recoil is accumulated and bounded at one radian.
    /// CanCraft also checks the real workbench footprint, reach, and intervening obstacles.
    /// </summary>
    public static class EquipmentCatalog
    {
        public const int MaxGear = 24;
        public static readonly string[] GearNames = { "石斧", "石镐", "钢斧", "钢镐", "扳手", "KA-BAR 1217军刀", "GLOCK 17手枪", "Mossberg 590霰弹枪", "M4A1卡宾枪", "M24狙击步枪", "布头巾", "皮夹克", "工装裤", "工装靴", "钢盔", "防弹背心", "战术头盔", "战术背心", "战术靴" };
        public static readonly string[] PartNames = { "红点瞄具", "四倍瞄具", "消音器", "制退器", "扩容弹匣", "人体工学握把", "精密枪管" };
        public static readonly string[] SlotNames = { "瞄具", "枪口", "弹匣", "握把", "枪管" };
        public static readonly string[] ArmorSlotNames = { "头部", "躯干", "腿部", "脚部" };

        public static bool UsesPickaxe(NodeKind kind) =>
            kind == NodeKind.Stone || kind == NodeKind.Clay || kind == NodeKind.Ore ||
            kind == NodeKind.CopperOre || kind == NodeKind.TinOre || kind == NodeKind.UraniumOre ||
            kind == NodeKind.Coal || kind == NodeKind.Limestone || kind == NodeKind.FireClay || kind == NodeKind.Quartz;

        public static GearDefinition Gear(GearKind kind)
        {
            if ((int)kind < 0 || (int)kind >= GearNames.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            var d = new GearDefinition { kind = kind, name = GearNames[(int)kind], era = TechEra.Primitive, maxDurability = 100,
                tool = kind <= GearKind.Wrench, ammo = ItemKind.Ammo, damage = 19f, range = 1.65f, interval = .62f, pellets = 1 };
            switch (kind)
            {
                case GearKind.StoneAxe: d.cost = DemoCatalog.Materials(ItemKind.Wood,3,ItemKind.Ore,2); d.damage=23f; break;
                case GearKind.StonePickaxe: d.cost = DemoCatalog.Materials(ItemKind.Wood,3,ItemKind.Ore,3); d.damage=21f; break;
                case GearKind.SteelAxe: d.era=TechEra.Steam; d.cost=DemoCatalog.Materials(ItemKind.Steel,3,ItemKind.Wood,2,ItemKind.Scrap,1); d.damage=36f; d.interval=.55f; break;
                case GearKind.SteelPickaxe: d.era=TechEra.Steam; d.cost=DemoCatalog.Materials(ItemKind.Steel,3,ItemKind.Wood,2,ItemKind.Scrap,2); d.damage=32f; d.interval=.55f; break;
                case GearKind.Wrench: d.era=TechEra.Steam; d.cost=DemoCatalog.Materials(ItemKind.Iron,3,ItemKind.Bronze,2); d.damage=26f; break;
                case GearKind.Knife: d.description="KA-BAR 1217固定刃战斗/通用刀。"; d.cost=DemoCatalog.Materials(ItemKind.Iron,2,ItemKind.Scrap,1); d.damage=24f; d.interval=.42f; d.range=1.5f; break;
                case GearKind.Pistol:
                    d.description="9×19mm半自动手枪原型；基础弹匣17发。";
                    d.era=TechEra.LowVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,4,ItemKind.Copper,2,ItemKind.Scrap,5);
                    SetGun(d,ItemKind.Ammo,17,24f,9f,.32f,1.3f,.035f,.028f,1); break;
                case GearKind.Shotgun:
                    d.description="12号口径泵动霰弹枪；游戏采用6发管式弹仓。";
                    d.era=TechEra.LowVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,6,ItemKind.Wood,3,ItemKind.Scrap,5);
                    SetGun(d,ItemKind.Shell,6,12f,7f,.95f,2.4f,.17f,.09f,7); break;
                case GearKind.AssaultRifle:
                    d.description="5.56×45mm选择射击卡宾枪原型；基础弹匣30发。";
                    d.era=TechEra.MediumVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,8,ItemKind.Polymer,4,ItemKind.Circuit,2);
                    SetGun(d,ItemKind.RifleAmmo,30,22f,15f,.13f,2.2f,.045f,.04f,1); break;
                case GearKind.PrecisionRifle:
                    d.description="7.62×51mm栓动狙击步枪原型；基础弹仓5发。";
                    d.era=TechEra.HighVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,10,ItemKind.Polymer,4,ItemKind.AdvancedCircuit,2);
                    SetGun(d,ItemKind.PrecisionAmmo,5,86f,24f,1.35f,2.8f,.012f,.12f,1); break;
                case GearKind.ClothHood:
                    d.cost=DemoCatalog.Materials(ItemKind.Wood,3,ItemKind.Scrap,1); SetArmor(d,4f,0f); break;
                case GearKind.LeatherJacket:
                    d.era=TechEra.Water; d.cost=DemoCatalog.Materials(ItemKind.Wood,4,ItemKind.Scrap,3); SetArmor(d,10f,.02f); break;
                case GearKind.WorkPants:
                    d.cost=DemoCatalog.Materials(ItemKind.Wood,3,ItemKind.Scrap,2); SetArmor(d,6f,.01f); break;
                case GearKind.WorkBoots:
                    d.era=TechEra.Water; d.cost=DemoCatalog.Materials(ItemKind.Wood,2,ItemKind.Scrap,3); SetArmor(d,4f,.01f); break;
                case GearKind.SteelHelmet:
                    d.era=TechEra.LowVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,3,ItemKind.Scrap,2); SetArmor(d,18f,.025f); break;
                case GearKind.ArmorVest:
                    d.era=TechEra.LowVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,5,ItemKind.Bronze,2,ItemKind.Scrap,3); SetArmor(d,28f,.06f); break;
                case GearKind.TacticalHelmet:
                    d.era=TechEra.MediumVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,3,ItemKind.Polymer,3,ItemKind.Circuit,1); SetArmor(d,26f,.015f); break;
                case GearKind.TacticalVest:
                    d.era=TechEra.HighVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,6,ItemKind.Polymer,6,ItemKind.AdvancedCircuit,2); SetArmor(d,42f,.04f); break;
                case GearKind.TacticalBoots:
                    d.era=TechEra.MediumVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Polymer,3); SetArmor(d,12f,.005f); break;
            }
            return d;
        }

        private static void SetGun(GearDefinition d, ItemKind ammo, int magazine, float damage, float range, float interval, float reload, float spread, float recoil, int pellets)
        { d.ammo=ammo; d.magazine=magazine; d.damage=damage; d.range=range; d.interval=interval; d.reload=reload; d.spread=spread; d.recoil=recoil; d.pellets=pellets; }
        private static void SetArmor(GearDefinition d,float armor,float movePenalty)
        { d.armorSlot=ArmorSlot(d.kind); d.armor=armor; d.movePenalty=movePenalty; d.damage=d.range=d.interval=0f; d.pellets=0; }

        public static PartDefinition Part(PartKind kind)
        {
            if ((int)kind < 0 || (int)kind >= PartNames.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            var d = new PartDefinition { kind=kind, name=PartNames[(int)kind], era=TechEra.LowVoltage };
            switch (kind)
            {
                case PartKind.RedDot: d.slot=AttachmentSlot.Optic; d.cost=DemoCatalog.Materials(ItemKind.Copper,2,ItemKind.Circuit,1); d.description="散布×0.75；兼容所有枪械"; break;
                case PartKind.Scope4x: d.slot=AttachmentSlot.Optic; d.era=TechEra.MediumVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Polymer,2,ItemKind.Circuit,2); d.description="散布×0.5，射程×1.2；步枪专用"; break;
                case PartKind.Suppressor: d.slot=AttachmentSlot.Muzzle; d.era=TechEra.MediumVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,3,ItemKind.Polymer,1); d.description="后坐×0.75，伤害×0.95；非霰弹枪"; break;
                case PartKind.MuzzleBrake: d.slot=AttachmentSlot.Muzzle; d.cost=DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Scrap,2); d.description="后坐×0.6；兼容所有枪械"; break;
                case PartKind.ExtendedMag: d.slot=AttachmentSlot.Magazine; d.cost=DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Copper,2); d.description="容量增加50%，装填时间×1.15；超额弹须先卸弹"; break;
                case PartKind.ErgonomicGrip: d.slot=AttachmentSlot.Grip; d.era=TechEra.MediumVoltage; d.cost=DemoCatalog.Materials(ItemKind.Polymer,2,ItemKind.Steel,1); d.description="散布×0.8，后坐×0.8，装填×0.9；长枪专用"; break;
                default: d.slot=AttachmentSlot.Barrel; d.era=TechEra.HighVoltage; d.cost=DemoCatalog.Materials(ItemKind.Steel,4,ItemKind.AdvancedCircuit,1); d.description="伤害×1.12，射程×1.2，散布×0.7；步枪专用"; break;
            }
            return d;
        }

        public static WeaponStats Stats(GearState gear)
        {
            if (gear == null) return new WeaponStats { damage=19f,range=1.65f,interval=.62f,pellets=1 };
            GearDefinition d=Gear(gear.kind);
            var s=new WeaponStats { damage=d.damage, range=d.range, interval=d.interval, reload=d.reload, spread=d.spread, recoil=d.recoil, magazine=d.magazine, pellets=d.pellets };
            if(gear.attachments!=null) foreach(int value in gear.attachments)
            {
                if(value<0 || value>=PartNames.Length || !PartCompatible(gear.kind,(PartKind)value)) continue;
                switch((PartKind)value)
                {
                    case PartKind.RedDot: s.spread*=.75f; break;
                    case PartKind.Scope4x: s.spread*=.5f; s.range*=1.2f; break;
                    case PartKind.Suppressor: s.recoil*=.75f; s.damage*=.95f; break;
                    case PartKind.MuzzleBrake: s.recoil*=.6f; break;
                    case PartKind.ExtendedMag: s.magazine+=(s.magazine+1)/2; s.reload*=1.15f; break;
                    case PartKind.ErgonomicGrip: s.spread*=.8f; s.recoil*=.8f; s.reload*=.9f; break;
                    case PartKind.MatchBarrel: s.damage*=1.12f; s.range*=1.2f; s.spread*=.7f; break;
                }
            }
            return s;
        }

        public static float EffectiveSpread(PlayerState p, GearState g)
        {
            if(g==null || !IsGun(g.kind)) return 0f;
            WeaponStats s=Stats(g);
            if(p==null) return s.spread;
            float spread=s.spread+(p.moving ? s.spread*.75f+.025f : 0f)+p.recoil*.6f;
            return spread*(p.aiming ? .35f : 1f)/(1f+p.attributes[3]*.04f);
        }

        public static GearState Find(PlayerState player, int id) => player == null || player.gear == null || id <= 0 ? null : player.gear.Find(g => g != null && g.id == id);
        public static GearState Equipped(PlayerState player) => player == null ? null : player.activeSlot == 0 ? Find(player,player.weaponId) : player.activeSlot == 1 ? Find(player,player.toolId) : null;
        public static bool IsGun(GearKind kind) => kind >= GearKind.Pistol && kind <= GearKind.PrecisionRifle;
        public static bool IsArmor(GearKind kind) => ArmorSlot(kind)>=0;
        public static int ArmorSlot(GearKind kind)
        {
            switch(kind)
            {
                case GearKind.ClothHood: case GearKind.SteelHelmet: case GearKind.TacticalHelmet: return 0;
                case GearKind.LeatherJacket: case GearKind.ArmorVest: case GearKind.TacticalVest: return 1;
                case GearKind.WorkPants: return 2;
                case GearKind.WorkBoots: case GearKind.TacticalBoots: return 3;
                default: return -1;
            }
        }
        public static float ArmorRating(PlayerState p) => ArmorTotal(p,false);
        public static float MovementPenalty(PlayerState p) => Math.Min(.25f,ArmorTotal(p,true));
        private static float ArmorTotal(PlayerState p,bool movement)
        {
            if(p==null || p.armorIds==null || p.armorIds.Length!=4) return 0f;
            float total=0f;
            for(int i=0;i<p.armorIds.Length;i++)
            {
                GearState g=Find(p,p.armorIds[i]);
                if(g==null || g.durability<=0 || ArmorSlot(g.kind)!=i) continue;
                GearDefinition d=Gear(g.kind); total+=movement ? d.movePenalty : d.armor;
            }
            return total;
        }
        public static bool AmmoCompatible(GearKind kind, ItemKind ammo) => kind == GearKind.Pistol ? ammo == ItemKind.Ammo : kind == GearKind.Shotgun ? ammo == ItemKind.Shell : kind == GearKind.AssaultRifle ? ammo == ItemKind.RifleAmmo || ammo == ItemKind.ArmorPiercingAmmo : kind == GearKind.PrecisionRifle && (ammo == ItemKind.PrecisionAmmo || ammo == ItemKind.ArmorPiercingAmmo);
        public static bool IsAmmo(ItemKind ammo) => ammo == ItemKind.Ammo || ammo >= ItemKind.RifleAmmo && ammo <= ItemKind.ArmorPiercingAmmo;
        public static string AmmoName(ItemKind ammo) => IsAmmo(ammo) ? DemoCatalog.ItemName(ammo) : "无弹药";
        public static bool PartCompatible(GearKind kind, PartKind part)
        {
            if(!IsGun(kind) || (int)part<0 || (int)part>=PartNames.Length) return false;
            if(part==PartKind.Scope4x || part==PartKind.MatchBarrel) return kind==GearKind.AssaultRifle || kind==GearKind.PrecisionRifle;
            if(part==PartKind.Suppressor) return kind!=GearKind.Shotgun;
            if(part==PartKind.ErgonomicGrip) return kind!=GearKind.Pistol;
            return true;
        }
        public static bool CanCraft(WorldState world, PlayerState player, GearKind kind) =>
            (int)kind>=0 && (int)kind<GearNames.Length && ValidCrafter(world,player) && player.gear.Count<MaxGear && CanPay(player.items,Gear(kind).cost) && world.era>=Gear(kind).era && PrimitiveCatalog.IsGearUnlocked(world,kind);
        public static bool CanCraftPart(WorldState world, PlayerState player, PartKind kind) =>
            (int)kind>=0 && (int)kind<PartNames.Length && ValidCrafter(world,player) && player.parts!=null && player.parts.Length==PartNames.Length && player.parts[(int)kind]<WorldSimulator.InventoryLimit && CanPay(player.items,Part(kind).cost) && world.era>=Part(kind).era;
        private static bool CanPay(int[] items,int[] cost)
        {
            if(items==null || items.Length!=DemoCatalog.ItemCount) return false;
            for(int i=0;i<cost.Length;i++) if(items[i]<cost[i]) return false;
            return true;
        }
        private static bool ValidCrafter(WorldState world, PlayerState player) => world!=null && player!=null && world.players!=null && world.players.Contains(player) && player.connected && player.hp>0f && player.gear!=null && HasWorkbench(world,player);

        public static bool HasWorkbench(WorldState world, PlayerState player)
        {
            if(world==null || player==null || world.buildings==null || world.nodes==null) return false;
            foreach(var bench in world.buildings)
            {
                if(bench==null || bench.kind!=BuildKind.Workbench || bench.hp<=0f) continue;
                float tx=DemoCatalog.CenterX(bench), ty=DemoCatalog.CenterY(bench), dx=tx-player.x, dy=ty-player.y;
                if(dx*dx+dy*dy>2.4f*2.4f) continue;
                int steps=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(dx*dx+dy*dy)/.08)); bool blocked=false;
                for(int n=1;n<=steps && !blocked;n++)
                {
                    int x=(int)Math.Floor(player.x+dx*n/steps+.5f), y=(int)Math.Floor(player.y+dy*n/steps+.5f);
                    if(DemoCatalog.Contains(bench,x,y)) continue;
                    char ground=WorldTiles.Ground(world,x,y);
                    if(!WorldTiles.Inside(world,x,y) || ground=='2' || ground=='?') { blocked=true; break; }
                    foreach(var node in world.nodes) if(node!=null && node.hp>0f && node.stock>0 && node.x==x && node.y==y) { blocked=true; break; }
                    if(!blocked) foreach(var b in world.buildings) if(b!=null && b!=bench && b.hp>0f && b.kind!=BuildKind.Belt && DemoCatalog.Contains(b,x,y)) { blocked=true; break; }
                }
                if(!blocked) return true;
            }
            return false;
        }
    }
}
