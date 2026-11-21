using System;

namespace DeadCircuit
{
    public enum ResearchDomain { Industry = 0, Weapon = 1, Tool = 2 }

    [Serializable] public sealed class ResearchRef
    {
        public ResearchDomain domain;
        public int era,id;
        public int Key => domain==ResearchDomain.Weapon ? 10000+id : domain==ResearchDomain.Tool ? 20000+id : era*100+id+1;
    }

    [Serializable] public sealed class EquipmentTechnologyDefinition
    {
        public int id,era;
        public ResearchDomain domain;
        public string name,description;
        public ResearchRef[] prerequisites;
        public int[] cost,production;
    }

    public static class EquipmentResearchCatalog
    {
        static EquipmentTechnologyDefinition Definition(ResearchRef research) => research==null ? null :
            research.domain==ResearchDomain.Weapon ? WeaponTechCatalog.Definition(research.id) :
            research.domain==ResearchDomain.Tool ? ToolTechCatalog.Definition(research.id) : null;

        static bool Valid(ResearchRef research)
        {
            if(research==null || research.era<0 || research.era>6 || research.id<0 || research.id>30) return false;
            if(research.domain!=ResearchDomain.Industry)
            {
                var definition=Definition(research);
                return definition!=null && definition.era==research.era;
            }
            switch((TechEra)research.era)
            {
                case TechEra.Primitive: return PrimitiveCatalog.Definition(research.id)!=null;
                case TechEra.Water: return WaterCatalog.Definition(research.id)!=null;
                case TechEra.Steam: return SteamCatalog.Definition(research.id)!=null;
                case TechEra.LowVoltage: return LowVoltageCatalog.Definition(research.id)!=null;
                case TechEra.MediumVoltage: return PetrochemicalCatalog.Definition(research.id)!=null;
                case TechEra.HighVoltage: return PrecisionCatalog.Definition(research.id)!=null;
                case TechEra.Nuclear: return NuclearCatalog.Definition(research.id)!=null;
                default: return false;
            }
        }

        public static bool Known(WorldState world,ResearchRef research)
        {
            if(world==null || !Valid(research) || (int)world.era<research.era) return false;
            if(research.domain==ResearchDomain.Weapon) return WeaponTechCatalog.HasResearch(world,research.id);
            if(research.domain==ResearchDomain.Tool) return ToolTechCatalog.HasResearch(world,research.id);
            // New equipment branches require the actual industrial bit, never a legacy opt-out.
            int bits=research.era==0 ? world.primitiveResearch : research.era==1 ? world.waterResearch :
                research.era==2 ? world.steamResearch : research.era==3 ? world.lvResearch :
                research.era==4 ? world.mvResearch : research.era==5 ? world.hvResearch : world.nuclearResearch;
            return (bits & (1<<research.id))!=0;
        }

        public static string Requirement(WorldState world,PlayerState player,ResearchRef research)
        {
            if(world==null || !Valid(research)) return "无法识别这项研究。";
            if(research.domain==ResearchDomain.Industry)
            {
                switch((TechEra)research.era)
                {
                    case TechEra.Primitive: return PrimitiveCatalog.ResearchRequirement(world,player,research.id);
                    case TechEra.Water: return WaterCatalog.ResearchRequirement(world,player,research.id);
                    case TechEra.Steam: return SteamCatalog.ResearchRequirement(world,player,research.id);
                    case TechEra.LowVoltage: return LowVoltageCatalog.ResearchRequirement(world,player,research.id);
                    case TechEra.MediumVoltage: return PetrochemicalCatalog.ResearchRequirement(world,player,research.id);
                    case TechEra.HighVoltage: return PrecisionCatalog.ResearchRequirement(world,player,research.id);
                    case TechEra.Nuclear: return NuclearCatalog.ResearchRequirement(world,player,research.id);
                }
            }
            var definition=Definition(research);
            if((int)world.era<definition.era) return "需要先进入："+TechCatalog.EraName((TechEra)definition.era)+"。";
            if(Known(world,research)) return "全队已完成此项研究。";
            // Stable missing-condition order: industry, equipment, production, operator, samples.
            foreach(var parent in definition.prerequisites)
                if(parent.domain==ResearchDomain.Industry && !Known(world,parent)) return Missing(parent);
            foreach(var parent in definition.prerequisites)
                if(parent.domain!=ResearchDomain.Industry && !Known(world,parent)) return Missing(parent);
            string evidence=Evidence(world,research);
            if(evidence.Length>0) return evidence;
            if(player==null || world.players==null || !world.players.Contains(player) || !player.connected || !(player.hp>0f) ||
                player.items==null || player.items.Length!=DemoCatalog.ItemCount) return "当前角色无法研究，请确认角色已连接且仍存活。";
            if(!(player.interactCooldown<=0f)) return "操作尚未结束，请稍候。";
            if(!EquipmentCatalog.HasWorkbench(world,player)) return "请靠近工作台，并确保中间没有障碍。";
            for(int i=0;i<definition.cost.Length;i++) if(player.items[i]<definition.cost[i]) return "研究材料不足："+DemoCatalog.ItemNames[i];
            return "";
        }

        public static string Evidence(WorldState world,ResearchRef research)
        {
            if(world==null || !Valid(research)) return "无法识别这项研究。";
            if(research.domain==ResearchDomain.Industry)
            {
                switch((TechEra)research.era)
                {
                    case TechEra.Primitive: return PrimitiveCatalog.ResearchEvidence(world,research.id);
                    case TechEra.Water: return WaterCatalog.ResearchEvidence(world,research.id);
                    case TechEra.Steam: return SteamCatalog.ResearchEvidence(world,research.id);
                    case TechEra.LowVoltage: return LowVoltageCatalog.ResearchEvidence(world,(LowVoltageTech)research.id);
                    case TechEra.MediumVoltage: return PetrochemicalCatalog.ResearchEvidence(world,(PetrochemicalTech)research.id);
                    case TechEra.HighVoltage: return PrecisionCatalog.ResearchEvidence(world,(PrecisionTech)research.id);
                    case TechEra.Nuclear: return NuclearCatalog.ResearchEvidence(world,(NuclearTech)research.id);
                }
            }
            var definition=Definition(research);
            string missing="";
            for(int i=0;i<definition.production.Length;i++)
            {
                int required=definition.production[i];
                if(required<=0) continue;
                int produced=world.produced!=null && i<world.produced.Length ? world.produced[i] : 0;
                if(produced<required) missing+=(missing.Length==0 ? "" : "、")+DemoCatalog.ItemNames[i]+"×"+produced+"/"+required;
            }
            return missing.Length==0 ? "" : "需全队累计实产："+missing+"。";
        }

        internal static string Missing(ResearchRef research)
        {
            if(!Valid(research)) return "无法识别这项研究。";
            if(research.domain!=ResearchDomain.Industry) return "需要"+(research.domain==ResearchDomain.Weapon ? "武器" : "工具")+"研究："+Definition(research).name;
            string name;
            switch((TechEra)research.era)
            {
                case TechEra.Primitive: name=PrimitiveCatalog.Definition(research.id).name; break;
                case TechEra.Water: name=WaterCatalog.Definition(research.id).name; break;
                case TechEra.Steam: name=SteamCatalog.Definition(research.id).name; break;
                case TechEra.LowVoltage: name=LowVoltageCatalog.Definition(research.id).name; break;
                case TechEra.MediumVoltage: name=PetrochemicalCatalog.Definition(research.id).name; break;
                case TechEra.HighVoltage: name=PrecisionCatalog.Definition(research.id).name; break;
                default: name=NuclearCatalog.Definition(research.id).name; break;
            }
            return "需要"+TechCatalog.EraName((TechEra)research.era)+"研究："+name;
        }

        public static ResearchRef GearResearch(GearKind kind)
        {
            switch(kind)
            {
                case GearKind.StoneAxe: case GearKind.StonePickaxe: return new ResearchRef { domain=ResearchDomain.Industry,era=0,id=(int)PrimitiveTech.Tools };
                case GearKind.Knife: return new ResearchRef { domain=ResearchDomain.Industry,era=0,id=(int)PrimitiveTech.Bloomery };
                case GearKind.SteelAxe: case GearKind.SteelPickaxe: return ToolTechCatalog.Reference((int)ToolTech.SteelTools);
                case GearKind.Wrench: return ToolTechCatalog.Reference((int)ToolTech.MechanicalService);
                case GearKind.Pistol: return WeaponTechCatalog.Reference((int)WeaponTech.Sidearms);
                case GearKind.Shotgun: return WeaponTechCatalog.Reference((int)WeaponTech.Shotguns);
                case GearKind.AssaultRifle: return WeaponTechCatalog.Reference((int)WeaponTech.AutomaticWeapons);
                case GearKind.PrecisionRifle: return WeaponTechCatalog.Reference((int)WeaponTech.PrecisionWeapons);
                default: return null;
            }
        }
        public static ResearchRef PartResearch(PartKind kind)
        {
            switch(kind)
            {
                case PartKind.MuzzleBrake: case PartKind.ExtendedMag: return WeaponTechCatalog.Reference((int)WeaponTech.BasicFittings);
                case PartKind.RedDot: return WeaponTechCatalog.Reference((int)WeaponTech.ReflexOptics);
                case PartKind.Scope4x: return WeaponTechCatalog.Reference((int)WeaponTech.MagnifiedOptics);
                case PartKind.Suppressor: case PartKind.ErgonomicGrip: return WeaponTechCatalog.Reference((int)WeaponTech.TacticalFittings);
                case PartKind.MatchBarrel: return WeaponTechCatalog.Reference((int)WeaponTech.MatchBarrels);
                default: return null;
            }
        }
        public static ResearchRef AmmoResearch(ItemKind kind)
        {
            switch(kind)
            {
                case ItemKind.Ammo: case ItemKind.RifleAmmo: case ItemKind.Shell: return WeaponTechCatalog.Reference((int)WeaponTech.BasicAmmunition);
                case ItemKind.PrecisionAmmo: return WeaponTechCatalog.Reference((int)WeaponTech.PrecisionAmmunition);
                case ItemKind.ArmorPiercingAmmo: return WeaponTechCatalog.Reference((int)WeaponTech.ArmorPiercing);
                default: return null;
            }
        }
        public static bool IsGearUnlocked(WorldState world,GearKind kind)
        {
            if(world==null || (int)kind<0 || (int)kind>=EquipmentCatalog.GearNames.Length || world.era<EquipmentCatalog.Gear(kind).era) return false;
            if(kind==GearKind.RadiationSuit) return NuclearCatalog.HasResearch(world,NuclearTech.RadiationProtection);
            var research=GearResearch(kind); return research==null || Known(world,research);
        }
        public static bool IsPartUnlocked(WorldState world,PartKind kind) => world!=null && (int)kind>=0 && (int)kind<EquipmentCatalog.PartNames.Length && world.era>=EquipmentCatalog.Part(kind).era && Known(world,PartResearch(kind));
        public static bool IsAmmoUnlocked(WorldState world,ItemKind kind) => world!=null && AmmoResearch(kind)!=null && Known(world,AmmoResearch(kind));

        internal static string RecipeRequirement(WorldState world,RecipeDefinition recipe)
        {
            if(world==null || recipe==null) return "无法识别这项配方。";
            for(int i=0;i<recipe.outputs.Length;i++) if(recipe.outputs[i]>0)
            {
                var research=AmmoResearch((ItemKind)i);
                if(research!=null && !Known(world,research)) return Missing(research);
            }
            return "";
        }

        // Snapshot-safe: only bits, eras and direct prerequisite closure, never world entities or production.
        public static bool ResearchStateValid(WorldState world)
        {
            if(world==null || (int)world.era<0 || (int)world.era>6 ||
                (world.weaponResearch & ~WeaponTechCatalog.AllResearch)!=0 || (world.toolResearch & ~ToolTechCatalog.AllResearch)!=0) return false;
            return BranchValid(world,WeaponTechCatalog.All) && BranchValid(world,ToolTechCatalog.All);
        }
        static bool BranchValid(WorldState world,EquipmentTechnologyDefinition[] definitions)
        {
            foreach(var definition in definitions)
            {
                int bits=definition.domain==ResearchDomain.Weapon ? world.weaponResearch : world.toolResearch;
                if((bits & (1<<definition.id))==0) continue;
                if((int)world.era<definition.era) return false;
                foreach(var parent in definition.prerequisites) if(!Known(world,parent)) return false;
            }
            return true;
        }
    }
}
