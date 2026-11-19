using System;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        public RoomLayout Interiors { get; private set; }
        private string interiorGround;
        private ulong interiorSignature;

        // Room membership depends on physical barriers, never site labels or machine graphics.
        // Open doors retain the room/roof boundary while allowing actors and shots through.
        private void RefreshInteriors()
        {
            ulong signature=14695981039346656037UL;
            unchecked
            {
                foreach(var n in State.nodes) if(n.kind==NodeKind.Wall && n.hp>0 && n.stock>0)
                {
                    signature=(signature^(uint)n.id)*1099511628211UL;
                    signature=(signature^(uint)(n.x+n.y*State.width))*1099511628211UL;
                }
                foreach(var b in State.buildings) if(b.hp>0 && (b.kind==BuildKind.Wall || b.kind==BuildKind.Door))
                {
                    signature=(signature^(uint)b.id)*1099511628211UL;
                    signature=(signature^(uint)(b.x+b.y*State.width))*1099511628211UL;
                    signature=(signature^(uint)((int)b.kind+DemoCatalog.Width(b)*256+DemoCatalog.Height(b)*4096))*1099511628211UL;
                }
            }
            if(Interiors!=null && interiorGround==State.ground && signature==interiorSignature) return;
            Interiors=RoomLayout.Build(State); interiorGround=State.ground; interiorSignature=signature;
        }

        private void SetDoor(PlayerState player,BuildingState door,int setting)
        {
            if(door.setting==setting) return;
            if(setting==0)
            {
                bool occupied=OccupiedByActor(door.x,door.y);
                if(State.survival!=null && State.survival.enabled)
                    foreach(var c in State.survival.creatures)
                        if(c.hp>0 && Math.Abs(c.x-door.x)<.8f && Math.Abs(c.y-door.y)<.8f) { occupied=true; break; }
                if(occupied) { Notice(player,"门口有人或生物，无法关门。"); return; }
            }
            door.setting=setting; flowRevision=-1;
            Notice(player,setting==1 ? "已开门，可以进入室内。" : "已关门。");
        }
    }
}
