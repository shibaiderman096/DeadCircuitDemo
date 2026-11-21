using System;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private void UpdateRadiation()
        {
            foreach(var player in State.players)
            {
                if(!player.connected || player.hp<=0f) continue;
                float ambient=RadiationCatalog.AmbientRate(State,player.x,player.y);
                GearState protection=RadiationCatalog.ProtectiveGear(player);
                float factor=1f-RadiationCatalog.Protection(player);
                if(ambient>0f)
                {
                    player.radiationDose=Math.Min(RadiationCatalog.MaxDose,player.radiationDose+ambient*factor*FixedStep);
                    if(protection!=null)
                    {
                        player.radiationWear+=FixedStep;
                        if(player.radiationWear+.00001f>=RadiationCatalog.WearInterval)
                        {
                            player.radiationWear=Math.Max(0f,player.radiationWear-RadiationCatalog.WearInterval);
                            protection.durability--;
                        }
                    }
                }
                else player.radiationDose=Math.Max(0f,player.radiationDose-RadiationCatalog.RecoveryPerSecond*FixedStep);
                if(player.radiationDose>=RadiationCatalog.DamageThreshold)
                    DamagePlayer(player,(.5f+(player.radiationDose-RadiationCatalog.DamageThreshold)*.035f)*FixedStep,false);
            }
        }
    }
}
