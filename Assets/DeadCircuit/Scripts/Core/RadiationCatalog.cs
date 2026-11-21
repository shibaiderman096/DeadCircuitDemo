using System;

namespace DeadCircuit
{
    // Game exposure units, independent of survival settings. Queries never advance the world.
    public static class RadiationCatalog
    {
        public const float MaxDose = 100f, DamageThreshold = 30f, RecoveryPerSecond = 2f, WearInterval = 2f;

        public static float Protection(PlayerState player)
        {
            GearState suit = ProtectiveGear(player);
            return suit == null ? 0f : EquipmentCatalog.Gear(suit.kind).radiationProtection;
        }

        internal static GearState ProtectiveGear(PlayerState player)
        {
            if(player==null || player.armorIds==null || player.armorIds.Length!=4) return null;
            GearState gear=EquipmentCatalog.Find(player,player.armorIds[1]);
            return gear!=null && gear.durability>0 && EquipmentCatalog.ArmorSlot(gear.kind)==1 &&
                EquipmentCatalog.Gear(gear.kind).radiationProtection>0f ? gear : null;
        }

        public static float AmbientRate(WorldState world,float x,float y)
        {
            if(world==null || world.era<TechEra.Nuclear || float.IsNaN(x) || float.IsNaN(y) ||
                float.IsInfinity(x) || float.IsInfinity(y)) return 0f;
            float rate=0f;
            bool shielded=NuclearCatalog.HasResearch(world,NuclearTech.ShieldedStorage);
            foreach(var building in world.buildings)
            {
                if(building==null || building.hp<=0f) continue;
                if(building.kind==BuildKind.Reactor && (building.fuel>0f || building.powerGeneration>0f))
                    rate+=Falloff(x,y,building,4f)*4f;
                if(building.kind!=BuildKind.Reactor && building.kind!=BuildKind.NuclearWasteVault) continue;
                int waste=building.items[(int)ItemKind.NuclearWaste];
                if(waste<=0) continue;
                float shielding=building.kind==BuildKind.NuclearWasteVault ? (shielded ? .02f : .1f) : 1f;
                rate+=Falloff(x,y,building,3f)*Math.Min(2.5f,waste*.25f)*shielding;
            }
            foreach(var cargo in world.cargo)
            {
                if(cargo.item!=(int)ItemKind.NuclearWaste) continue;
                float cx=cargo.fromX+(cargo.toX-cargo.fromX)*cargo.progress;
                float cy=cargo.fromY+(cargo.toY-cargo.fromY)*cargo.progress;
                float dx=x-cx,dy=y-cy;
                rate+=Math.Max(0f,1f-(float)Math.Sqrt(dx*dx+dy*dy)/2f)*(shielded ? .1f : .5f);
            }
            return Math.Min(12f,rate);
        }

        static float Falloff(float x,float y,BuildingState building,float radius)
        {
            float px=Math.Max(building.x-.5f,Math.Min(x,building.x+DemoCatalog.Width(building)-.5f));
            float py=Math.Max(building.y-.5f,Math.Min(y,building.y+DemoCatalog.Height(building)-.5f));
            float dx=x-px,dy=y-py;
            return Math.Max(0f,1f-(float)Math.Sqrt(dx*dx+dy*dy)/radius);
        }
    }
}
