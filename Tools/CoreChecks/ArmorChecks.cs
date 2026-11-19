using DeadCircuit;

static partial class Checks
{
    static GearState AddArmorFixture(WorldState state,PlayerState p,GearKind kind,int durability=100)
    {
        var armor=new GearState{id=state.nextId++,kind=kind,durability=durability};
        p.gear.Add(armor);p.armorIds[EquipmentCatalog.ArmorSlot(kind)]=armor.id;return armor;
    }
    static readonly GearKind[] ArmorSet={ GearKind.TacticalHelmet,GearKind.TacticalVest,GearKind.WorkPants,GearKind.TacticalBoots };

    static void EquipmentArmorChecks()
    {
        Run("equipment armor appends nine persistent armor kinds without renumbering weapons", () =>
        {
            Require(EquipmentCatalog.GearNames.Length == 19, "armor catalog requires 19 gear kinds");
            string[] names = { "ClothHood", "LeatherJacket", "WorkPants", "WorkBoots", "SteelHelmet", "ArmorVest", "TacticalHelmet", "TacticalVest", "TacticalBoots" };
            for (int i = 0; i < names.Length; i++)
                Require(Enum.TryParse<GearKind>(names[i], out var kind) && (int)kind == 10 + i, "missing or reordered armor kind: " + names[i]);
        });
        Run("equipment armor catalog has four compatible slots and armor is never a gun or tool",()=>
        {
            Require(EquipmentCatalog.ArmorSlotNames.Length==4,"armor slot labels missing");
            var expected=new[] { (GearKind.ClothHood,0),(GearKind.LeatherJacket,1),(GearKind.WorkPants,2),(GearKind.WorkBoots,3),
                (GearKind.SteelHelmet,0),(GearKind.ArmorVest,1),(GearKind.TacticalHelmet,0),(GearKind.TacticalVest,1),(GearKind.TacticalBoots,3) };
            foreach(var (kind,slot) in expected)
            {
                var d=EquipmentCatalog.Gear(kind);
                Require(EquipmentCatalog.IsArmor(kind) && EquipmentCatalog.ArmorSlot(kind)==slot && d.armorSlot==slot,"armor slot compatibility: "+kind);
                Require(d.armor>0 && float.IsFinite(d.armor) && d.movePenalty>=0 && d.movePenalty<=.25f && !d.tool && d.magazine==0,"invalid armor definition: "+kind);
                Require(!EquipmentCatalog.IsGun(kind) && !EquipmentCatalog.AmmoCompatible(kind,ItemKind.Ammo),"armor classified as firearm");
            }
            for(int n=0;n<10;n++)Require(!EquipmentCatalog.IsArmor((GearKind)n) && EquipmentCatalog.ArmorSlot((GearKind)n)==-1,"old weapon/tool became armor");
            var sim=New(out var p);Require(p.armorIds.Length==4 && p.armorIds.All(id=>id==0) && EquipmentCatalog.ArmorRating(p)==0,"new player has free equipped armor");
        });
        foreach(var kind in Enum.GetValues<GearKind>().Where(EquipmentCatalog.IsArmor))
            Run($"equipment armor {kind} equips and unequips without changing weapon tool or active slot",()=>
            {
                var sim=EquipmentFixture(out var p,GearKind.Pistol);int weapon=p.weaponId,tool=p.toolId,active=p.activeSlot;
                var armor=CraftGear(sim,p,kind);sim.Apply(Command(p,"equip",arg:armor.id));int slot=EquipmentCatalog.ArmorSlot(kind);
                Require(p.armorIds[slot]==armor.id && p.weaponId==weapon && p.toolId==tool && p.activeSlot==active,"armor equip overwrote independent combat slots");
                Require(EquipmentCatalog.ArmorRating(p)>0,"equipped armor gave no protection");
                sim.Apply(Command(p,"unequip",arg:armor.id));
                Require(p.armorIds[slot]==0 && p.weaponId==weapon && p.toolId==tool && p.activeSlot==active && p.gear.Contains(armor),"armor unequip lost gear or changed combat slot");
            });
        Run("equipment armor replacement preserves old item and changing armor does not cancel weapon reload",()=>
        {
            var sim=EquipmentFixture(out var p,GearKind.Pistol);var cloth=CraftGear(sim,p,GearKind.ClothHood);var steel=CraftGear(sim,p,GearKind.SteelHelmet);
            Advance(sim,.6f);sim.Apply(Command(p,"reload"));int loading=p.reloadingGearId;float remaining=p.reloadRemaining;
            Require(loading==p.weaponId && remaining>0,"armor/reload fixture never reloaded");
            sim.Apply(Command(p,"equip",arg:cloth.id));sim.Apply(Command(p,"equip",arg:steel.id));
            Require(p.armorIds[0]==steel.id && p.gear.Contains(cloth) && p.gear.Contains(steel),"armor replacement destroyed the old item");
            sim.Apply(Command(p,"unequip",arg:cloth.id));
            Require(p.armorIds[0]==steel.id && p.reloadingGearId==loading && p.reloadRemaining==remaining && p.activeSlot==0,"unequipping old armor removed current item or interrupted gun");
        });
        foreach(bool broken in new[] { false,true })
            Run("equipment armor actual zombie hit "+(broken?"ignores broken armor":"reduces damage and wears each equipped piece"),()=>
            {
                var sim=EquipmentFixture(out var p,GearKind.Pistol,arrange:(state,player)=>
                {
                    foreach(var kind in ArmorSet)AddArmorFixture(state,player,kind,broken?0:100);EquipmentTarget(state,33);
                });
                Near(EquipmentCatalog.ArmorRating(p),broken?0:86,"effective armor rating");
                Advance(sim,.05f);Near(p.hp,broken?90:100-1000f/186f,"actual hostile armor mitigation");
                foreach(int id in p.armorIds)Require(EquipmentCatalog.Find(p,id).durability==(broken?0:99),"armor wear on actual hit");
                Require(p.gear[0].durability==100,"taking damage consumed firearm durability");
            });
        Run("equipment armor last durability protects once then damage and movement revert to unarmored values",()=>
        {
            var sim=EquipmentFixture(out var p,GearKind.Pistol,arrange:(state,player)=>
            {
                foreach(var kind in ArmorSet)AddArmorFixture(state,player,kind,1);EquipmentTarget(state,33);
            });
            Advance(sim,.05f);float afterFirst=p.hp;Require(p.armorIds.All(id=>EquipmentCatalog.Find(p,id).durability==0),"last armor durability not consumed");
            Near(EquipmentCatalog.ArmorRating(p),0,"broken armor rating");Near(EquipmentCatalog.MovementPenalty(p),0,"broken armor movement penalty");
            Advance(sim,1.2f);Near(p.hp,afterFirst-10,"broken armor still reduced second hostile hit");
        });
        Run("equipment armor mitigation preserves original constitution reduction",()=>
        {
            var sim=EquipmentFixture(out var p,GearKind.Pistol,arrange:(state,player)=>
            {
                player.level=3;player.attributePoints=0;player.attributes[1]=2;AddArmorFixture(state,player,GearKind.TacticalVest);EquipmentTarget(state,33);
            });
            Advance(sim,.05f);Near(p.hp,100-960f/142f,"constitution then armor damage formula");
        });
        Run("equipment armor movement penalty changes actual speed and broken armor restores speed",()=>
        {
            foreach(int durability in new[] { 0,100 })
            {
                var sim=EquipmentFixture(out var p,GearKind.Pistol,arrange:(state,player)=>{foreach(var kind in ArmorSet)AddArmorFixture(state,player,kind,durability);});
                Near(EquipmentCatalog.MovementPenalty(p),durability==0?0:.07f,"summed movement penalty");
                float x=p.x;sim.Apply(new PlayerCommand{playerId=p.id,action="move",moveX=1});Advance(sim,1);
                Near(p.x-x,durability==0?3.2f:2.976f,"real armored movement speed",.002f);
                Require(EquipmentCatalog.MovementPenalty(p)<=.25f,"armor movement penalty exceeded cap");
            }
        });
        Run("equipment armor current save deep copies slot identities",()=>
        {
            var sim=EquipmentFixture(out var p,GearKind.Pistol,arrange:(state,player)=>{foreach(var kind in ArmorSet)AddArmorFixture(state,player,kind);});
            var source=Saved(sim.State);var restored=new WorldSimulator(source).State.players[0];
            Require(!ReferenceEquals(source.players[0].armorIds,restored.armorIds) && restored.armorIds.SequenceEqual(p.armorIds),"armor slots alias or changed on restore");
            source.players[0].armorIds[0]=0;Require(restored.armorIds[0]!=0,"caller mutation crossed armor slot boundary");
        });
        RunHistorical("equipment armor historical save keeps migrated slots empty",()=>
        {
            foreach(int version in new[] { 1,2,3 })
            {
                var legacy=version==3?VersionThreeFixture():LegacyFixture(version);var migrated=new WorldSimulator(legacy).State.players[0];
                Require(migrated.armorIds.Length==4 && migrated.armorIds.All(id=>id==0),"old save migrated with fabricated worn armor");
            }
        });
        foreach(var (label,change) in new (string,Action<PlayerState>)[]
        {
            ("null",p=>p.armorIds=null),("short",p=>p.armorIds=new int[3]),("long",p=>p.armorIds=new int[5]),
            ("negative id",p=>p.armorIds[0]=-1),("forged id",p=>p.armorIds[0]=987654),
            ("wrong armor slot",p=>{p.armorIds[1]=p.armorIds[0];p.armorIds[0]=0;}),
            ("duplicate armor id",p=>p.armorIds[1]=p.armorIds[0]),
            ("firearm as armor",p=>p.armorIds[0]=p.weaponId),
            ("armor as weapon",p=>p.weaponId=p.armorIds[0]),
            ("armor as tool",p=>p.toolId=p.armorIds[0])
        })
            Run("equipment armor v4 save rejects "+label,()=>
            {
                var sim=EquipmentFixture(out _,GearKind.Pistol,arrange:(state,player)=>AddArmorFixture(state,player,GearKind.ClothHood));
                var source=Saved(sim.State);change(source.players[0]);Reject(source,"corrupt armor slot accepted: "+label);
            });
        Run("equipment armor gearsalvage returns materials clears worn slot and awards no XP",()=>
        {
            var sim=EquipmentFixture(out var p,GearKind.Pistol,arrange:(state,player)=>AddArmorFixture(state,player,GearKind.TacticalVest));
            int id=p.armorIds[1],weapon=p.weaponId,xp=p.xp;var before=Copy(p.items);var cost=EquipmentCatalog.Gear(GearKind.TacticalVest).cost;
            sim.Apply(Command(p,"gearsalvage",arg:id));
            Require(p.armorIds[1]==0 && EquipmentCatalog.Find(p,id)==null && p.weaponId==weapon && p.activeSlot==0 && p.xp==xp,"salvage left dangling armor references or altered weapon");
            for(int i=0;i<25;i++)Require(p.items[i]==before[i]+cost[i]/2,"armor salvage refund mismatch");
        });
        Run("equipment armor save rejects another player's otherwise compatible head armor",()=>
        {
            var sim=EquipmentFixture(out _,GearKind.Pistol,arrange:(state,player)=>
            {
                AddArmorFixture(state,player,GearKind.ClothHood);
                var other=new PlayerState{id="other",seat=1,x=34,y=36};AddArmorFixture(state,other,GearKind.TacticalHelmet);state.players.Add(other);
            });
            var source=Saved(sim.State);source.players[0].armorIds[0]=source.players[1].armorIds[0];
            Reject(source,"saved armor ownership crossed players");
        });
        Run("equipment armor cannot attack as a weapon and repair restores protection",()=>
        {
            var sim=EquipmentFixture(out var p,GearKind.TacticalVest,arrange:(state,player)=>{player.gear[0].durability=0;EquipmentTarget(state,33);});
            int id=p.armorIds[1];FireEquipment(sim,p);Near(sim.State.zombies[0].hp,981,"worn armor should leave unarmed attack intact");
            sim.Apply(Command(p,"gearrepair",arg:id));Require(p.gear[0].durability==25 && EquipmentCatalog.ArmorRating(p)==42,"armor repair did not restore protection");
        });
    }
}
