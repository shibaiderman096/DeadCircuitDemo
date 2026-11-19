using System.Collections.Generic;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        readonly LineRenderer[] weaponReticle = new LineRenderer[4];
        const int MaxWeaponEffects = 96;
        enum WeaponEffectKind { Smoke, Casing, Shell, Slash, Glow }
        sealed class WeaponEffect
        {
            public SpriteRenderer sprite;
            public WeaponEffectKind kind;
            public Vector2 origin, velocity;
            public Color tint;
            public float born, lifetime, scale, angle, spin;
            public bool flipY;
        }
        readonly List<WeaponEffect> weaponEffects = new List<WeaponEffect>(MaxWeaponEffects);
        readonly Sprite[] heldWeaponArt = new Sprite[5];
        readonly bool[] registeredWeaponArt = new bool[5];
        readonly Sprite[,] weaponFlashArt = new Sprite[5,3];
        readonly bool[,] registeredFlashArt = new bool[5,3];
        readonly Sprite[] weaponSmokeArt = new Sprite[3], weaponSlashArt = new Sprite[3];
        Sprite weaponCasingArt, weaponShellArt, weaponGlowArt;
        bool weaponArtReady;

        void LoadWeaponEffectArt()
        {
            if (weaponArtReady) return;
            weaponArtReady=true;
            for (int i=0;i<5;i++)
            {
                Sprite held=DemoArt.Get("weapon_"+(i+5));
                registeredWeaponArt[i]=held!=null;
                heldWeaponArt[i]=held ?? Art("gear_"+(i+5)) ?? Art("turret_barrel") ?? Art("pixel");
            }
            for (int gun=0;gun<5;gun++) for (int frame=0;frame<3;frame++)
            {
                Sprite flash=DemoArt.Get(gun==4 ? "fx_weapon_suppressed_"+frame : "fx_weapon_muzzle_"+(gun+6)+"_"+frame);
                registeredFlashArt[gun,frame]=flash!=null;
                string old=gun==1 ? "fx_muzzle_shotgun" : gun==2 ? "fx_muzzle_rifle" : gun==3 ? "fx_muzzle_sniper" : "fx_muzzle";
                weaponFlashArt[gun,frame]=flash ?? Art(old) ?? Art("hit") ?? Art("pixel");
            }
            for (int frame=0;frame<3;frame++)
            {
                weaponSmokeArt[frame]=DemoArt.Get("fx_weapon_smoke_"+frame) ?? Art("fx_smoke") ?? Art("glow");
                weaponSlashArt[frame]=DemoArt.Get("fx_weapon_slash_"+frame) ?? Art("fx_wave") ?? Art("ring");
            }
            weaponCasingArt=DemoArt.Get("fx_weapon_casing") ?? Art("fx_spark") ?? Art("hit");
            weaponShellArt=DemoArt.Get("fx_weapon_shell") ?? weaponCasingArt;
            weaponGlowArt=DemoArt.Get("fx_weapon_glow") ?? Art("glow") ?? Art("hit");
        }

        // Atlas coordinates are measured from the top left. flipY affects rendering
        // only, so socket coordinates must receive exactly the same reflection.
        static Vector2 WeaponSpritePoint(Sprite sprite,Vector2 source,float width,float height,bool flipY)
        {
            if (sprite==null) return Vector2.zero;
            Vector2 point=new Vector2(source.x*sprite.rect.width/width-sprite.pivot.x,
                sprite.rect.height-source.y*sprite.rect.height/height-sprite.pivot.y)/sprite.pixelsPerUnit;
            if (flipY) point.y=-point.y;
            return point;
        }

        static Vector2 WeaponMuzzleSource(GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Knife: return new Vector2(58,16);
                case GearKind.Pistol: return new Vector2(45,14);
                case GearKind.Shotgun: return new Vector2(61,14);
                case GearKind.AssaultRifle: return new Vector2(60,14);
                default: return new Vector2(62,14);
            }
        }

        static Vector2 WeaponOpticSource(GearKind kind)
        {
            return kind==GearKind.Pistol ? new Vector2(29,9) :
                kind==GearKind.Shotgun ? new Vector2(28,9) : new Vector2(29,8);
        }

        static Vector2 WeaponSocket(ActorEquipment look,Vector2 source)
        {
            return look.weapon.transform.TransformPoint(WeaponSpritePoint(look.weapon.sprite,source,64,32,look.weapon.flipY));
        }

        static int WeaponPart(GearState gear,int slot)
        {
            return gear.attachments!=null && slot<gear.attachments.Length ? gear.attachments[slot] : -1;
        }

        bool ObserveWeaponAttack(ActorEquipment look,PlayerState player,GearState gear)
        {
            var world=bridge.World;
            if (world==null) { look.primed=false; return false; }
            Vector2 position=new Vector2(player.x,player.y);
            float delta=world.elapsed-look.sampleElapsed;
            long ticks=world.tick-look.sampleTick;
            // ObserveWorld has already updated its own clock before DrawActors. Keep
            // a separate sample, including identity, for host mutations AND snapshots.
            bool continuous=look.primed && look.playerId==player.id && look.gearId==gear.id && look.gearKind==gear.kind &&
                ticks>=0 && ticks<=6 && delta>=0 && delta<=.31f && Mathf.Abs(delta-ticks*.05f)<.04f &&
                Time.unscaledTime-look.sampledAt<=.4f && (position-look.samplePosition).sqrMagnitude<4f;
            if (!continuous) { look.attackAt=-100; look.lastTraceUntil=-1; }
            bool gun=EquipmentCatalog.IsGun(gear.kind);
            TraceState newest=null;
            float latest=look.lastTraceUntil;
            if (world.traces!=null) foreach (var trace in world.traces)
            {
                if (trace.hostile || trace.effect!=(gun ? 1 : 2) || trace.until<=world.elapsed) continue;
                // A player can move between the attack command and a LAN snapshot.
                // Match the swept actor position rather than its interpolated Visual.
                Vector2 from=new Vector2(trace.x,trace.y), travel=position-look.samplePosition;
                Vector2 closest=continuous && travel.sqrMagnitude>.00001f ?
                    look.samplePosition+travel*Mathf.Clamp01(Vector2.Dot(from-look.samplePosition,travel)/travel.sqrMagnitude) : position;
                if ((from-closest).sqrMagnitude>.09f) continue;
                latest=Mathf.Max(latest,trace.until);
                if (trace.until<=look.lastTraceUntil+.001f || trace.until-.15f<look.sampleElapsed-.005f) continue;
                if (newest==null || trace.until>newest.until) newest=trace;
            }
            bool attacked=false;
            if (continuous && newest!=null && gear.durability<look.durability)
            {
                float interval=EquipmentCatalog.Stats(gear).interval*(1f-player.attributes[2]*.025f)*(player.skills[1]>=2 ? .85f : 1f);
                float shotAt=newest.until-.15f;
                float expected=Mathf.Max(0,interval-Mathf.Max(0,world.elapsed-shotAt));
                bool resetCooldown=player.attackCooldown>Mathf.Max(0,look.previousCooldown-delta)+.005f;
                bool crossedReady=look.previousCooldown<=shotAt-look.sampleElapsed+.005f;
                // Unload changes magazine, not durability/cooldown, and creates no
                // shot trace. Surviving traces cannot be reused on a later frame.
                attacked=(resetCooldown || crossedReady) && Mathf.Abs(player.attackCooldown-expected)<.055f;
            }
            look.playerId=player.id; look.gearId=gear.id; look.gearKind=gear.kind;
            look.magazine=gear.magazine; look.durability=gear.durability; look.previousCooldown=player.attackCooldown;
            look.sampleTick=world.tick; look.sampleElapsed=world.elapsed; look.samplePosition=position;
            look.sampledAt=Time.unscaledTime; look.lastTraceUntil=latest; look.primed=true;
            return attacked;
        }

        void DrawEquippedWeapon(Visual visual,PlayerState player,GearState gear)
        {
            LoadWeaponEffectArt();
            ActorEquipment look=visual.equipment;
            bool attacked=ObserveWeaponAttack(look,player,gear);
            if (attacked) look.attackAt=Time.unscaledTime;
            Vector2 aim=new Vector2(player.aimX,player.aimY).normalized;
            if (aim.sqrMagnitude<.001f) aim=Vector2.right;
            bool gun=EquipmentCatalog.IsGun(gear.kind), flipY=aim.x<0;
            int artIndex=(int)gear.kind-5, order=visual.body.sortingOrder;
            bool registered=artIndex>=0 && artIndex<5 && registeredWeaponArt[artIndex];
            look.weapon.sprite=artIndex>=0 && artIndex<5 ? heldWeaponArt[artIndex] : Art("gear_"+(int)gear.kind) ?? Art("pixel");
            float scale=registered ? (gear.kind==GearKind.Knife ? .42f : gear.kind==GearKind.Pistol ? .52f : .54f) :
                gun && gear.kind!=GearKind.Pistol ? .78f : .54f;
            float age=Time.unscaledTime-look.attackAt;
            float recover=gear.kind==GearKind.AssaultRifle ? .11f : gear.kind==GearKind.Pistol ? .14f : .22f;
            float kick=gun ? 1-Mathf.SmoothStep(0,1,Mathf.Clamp01(age/recover)) : 0;
            bool suppressed=gun && WeaponPart(gear,1)==(int)PartKind.Suppressor;
            float strength=suppressed ? .75f : WeaponPart(gear,1)==(int)PartKind.MuzzleBrake ? .6f : 1;
            float travel=gear.kind==GearKind.Pistol ? .075f : gear.kind==GearKind.Shotgun ? .18f : gear.kind==GearKind.PrecisionRifle ? .20f : .10f;
            float rise=gear.kind==GearKind.Pistol ? 7 : gear.kind==GearKind.AssaultRifle ? 4 : 10;
            float swing=!gun && age>=0 && age<.24f ? -65*Mathf.Sin(age/.24f*Mathf.PI) : 0;
            float angle=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg+(gun ? rise*kick*strength : swing)*(flipY ? -1 : 1);
            var rotation=Quaternion.Euler(0,0,angle);
            look.weapon.flipX=false; look.weapon.flipY=flipY;
            look.weapon.sortingOrder=order+6; look.weapon.color=gear.durability>0 ? Color.white : new Color(.45f,.43f,.4f);
            look.weapon.transform.localScale=Vector3.one*scale; look.weapon.transform.localRotation=rotation;
            Vector2 grip=WeaponSpritePoint(look.weapon.sprite,new Vector2(20,20),64,32,flipY);
            // Rotate 180 degrees plus flipY when looking left: the stock stays behind
            // the hand, and the sights remain above the barrel in both hemispheres.
            look.weapon.transform.localPosition=(Vector3)(aim*(.25f-travel*kick*strength)+Vector2.up*.43f)-rotation*(Vector3)(grip*scale);
            Vector2 barrel=WeaponSocket(look,WeaponMuzzleSource(gear.kind));
            look.muzzleWorld=barrel;
            if (gun)
            {
                // M24 art already includes its scope. Installed optics retain all
                // authoritative stat effects without drawing a second scope.
                if (gear.kind!=GearKind.PrecisionRifle)
                    DrawWeaponPart(look.optic,WeaponPart(gear,0),WeaponSocket(look,WeaponOpticSource(gear.kind)),angle,flipY,.36f*scale,order+8);
                DrawWeaponPart(look.muzzle,WeaponPart(gear,1),barrel,angle,flipY,.42f*scale,order+8);
                if (look.muzzle.enabled) look.muzzleWorld=look.muzzle.transform.TransformPoint(
                    WeaponSpritePoint(look.muzzle.sprite,new Vector2(28,16),32,32,flipY));
                DrawWeaponFlash(look,gear,age,angle,suppressed,order+10);
            }
            if (!attacked) return;
            Vector2 forward=rotation*Vector2.right;
            if (gun)
            {
                float power=gear.kind==GearKind.Shotgun ? 1.25f : gear.kind==GearKind.PrecisionRifle ? 1.4f : 1;
                EmitWeaponEffect(WeaponEffectKind.Glow,look.muzzleWorld,Vector2.zero,new Color(1f,.56f,.17f,suppressed ? .13f : .42f),
                    .10f,(suppressed ? .38f : .95f)*power,angle,false);
                EmitWeaponEffect(WeaponEffectKind.Smoke,look.muzzleWorld,forward*.20f+Vector2.up*(suppressed ? .19f : .32f),
                    new Color(.86f,.82f,.72f,suppressed ? .17f : .48f),suppressed ? .32f : .64f,
                    (suppressed ? .24f : .43f)*power,angle,flipY);
                Vector2 top=rotation*(flipY ? Vector2.down : Vector2.up);
                bool shell=gear.kind==GearKind.Shotgun;
                Vector2 eject=WeaponSocket(look,gear.kind==GearKind.Pistol ? new Vector2(29,13) : new Vector2(28,14));
                EmitWeaponEffect(shell ? WeaponEffectKind.Shell : WeaponEffectKind.Casing,eject,
                    top*.8f-forward*.4f+Vector2.up*.65f,Color.white,shell ? 1.3f : 1.05f,shell ? .9f : .72f,angle,flipY);
            }
            else if (gear.kind==GearKind.Knife)
                EmitWeaponEffect(WeaponEffectKind.Slash,(Vector2)visual.root.transform.position+Vector2.up*.43f+aim*.33f,
                    Vector2.zero,new Color(.84f,.95f,1f,.95f),.20f,.68f,Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg,flipY);
            else Spawn("fx_wave",visual.position+aim*.55f,Amber,.16f,.45f,aim*.7f);
        }

        void DrawWeaponFlash(ActorEquipment look,GearState gear,float age,float angle,bool suppressed,int order)
        {
            float lifetime=gear.kind==GearKind.AssaultRifle ? .075f : gear.kind==GearKind.Pistol ? .09f : .12f;
            look.flash.enabled=age>=0 && age<lifetime;
            if (!look.flash.enabled) return;
            int gun=suppressed ? 4 : (int)gear.kind-6, frame=Mathf.Clamp(Mathf.FloorToInt(age/lifetime*3),0,2);
            look.flash.sprite=weaponFlashArt[gun,frame]; look.flash.sharedMaterial=effectMaterial;
            float scale=suppressed ? .34f : gear.kind==GearKind.Pistol ? .42f : gear.kind==GearKind.AssaultRifle ? .55f : .65f;
            if (!registeredFlashArt[gun,frame]) scale*=.75f;
            look.flash.transform.localScale=Vector3.one*scale; look.flash.transform.localRotation=Quaternion.Euler(0,0,angle);
            look.flash.flipX=false; look.flash.flipY=look.weapon.flipY; look.flash.sortingOrder=order;
            // New flash origin is (4,16), not its centre. Legacy radial flashes are centred.
            Vector2 origin=registeredFlashArt[gun,frame] ? WeaponSpritePoint(look.flash.sprite,new Vector2(4,16),64,32,look.flash.flipY) : Vector2.zero;
            look.flash.transform.position=(Vector3)look.muzzleWorld-look.flash.transform.localRotation*(Vector3)(origin*scale);
            look.flash.color=new Color(1f,.94f,.79f,1-age/lifetime*.35f);
        }

        void EmitWeaponEffect(WeaponEffectKind kind,Vector2 at,Vector2 velocity,Color tint,float lifetime,float scale,float angle,bool flipY)
        {
            WeaponEffect effect=null;
            foreach (var candidate in weaponEffects) if (!candidate.sprite.enabled) { effect=candidate; break; }
            if (effect==null && weaponEffects.Count<MaxWeaponEffects)
            {
                effect=new WeaponEffect { sprite=NewSprite("Pooled weapon effect",effectsRoot,9800) };
                effect.sprite.sharedMaterial=effectMaterial; weaponEffects.Add(effect);
            }
            if (effect==null)
            {
                // Recycle the most nearly expired entry at the hard cap; never grow
                // the pool under sustained automatic fire or multiplayer bursts.
                float oldest=-1;
                foreach (var candidate in weaponEffects)
                {
                    float age=(Time.unscaledTime-candidate.born)/candidate.lifetime;
                    if (age>oldest) { oldest=age; effect=candidate; }
                }
            }
            effect.kind=kind; effect.origin=at; effect.velocity=velocity; effect.tint=tint;
            effect.born=Time.unscaledTime; effect.lifetime=lifetime; effect.scale=scale; effect.angle=angle;
            effect.flipY=flipY; effect.spin=flipY ? -510 : 510;
            effect.sprite.enabled=true; effect.sprite.flipX=false; effect.sprite.flipY=flipY;
        }

        // Called once at the end of DrawParticles, after every actor has emitted.
        void DrawWeaponEffects()
        {
            foreach (var effect in weaponEffects)
            {
                var renderer=effect.sprite; if (!renderer.enabled) continue;
                float seconds=Time.unscaledTime-effect.born, age=seconds/effect.lifetime;
                if (seconds<0 || age>=1) { renderer.enabled=false; continue; }
                int frame=Mathf.Clamp(Mathf.FloorToInt(age*3),0,2);
                bool casing=effect.kind==WeaponEffectKind.Casing || effect.kind==WeaponEffectKind.Shell;
                Vector2 position=effect.origin+effect.velocity*seconds;
                float scale=effect.scale, angle=effect.angle; Color tint=effect.tint;
                switch (effect.kind)
                {
                    case WeaponEffectKind.Smoke:
                        renderer.sprite=weaponSmokeArt[frame]; scale*=.65f+age*1.7f;
                        angle+=seconds*(effect.flipY ? -22 : 22); tint.a*=(1-age)*(1-age); break;
                    case WeaponEffectKind.Slash:
                        renderer.sprite=weaponSlashArt[frame];
                        angle+=Mathf.Lerp(-35,45,age)*(effect.flipY ? -1 : 1); tint.a*=1-age*.8f; break;
                    case WeaponEffectKind.Glow:
                        renderer.sprite=weaponGlowArt; scale*=.8f+age*.6f; tint.a*=(1-age)*(1-age); break;
                    default:
                        renderer.sprite=effect.kind==WeaponEffectKind.Shell ? weaponShellArt : weaponCasingArt;
                        float fall=Mathf.Min(seconds,.48f);
                        position=effect.origin+effect.velocity*fall+Vector2.down*(2.5f*fall*fall);
                        angle+=effect.spin*fall; tint.a*=1-Mathf.InverseLerp(.72f,1,age); break;
                }
                renderer.sortingOrder=casing ? Order(position.y)+9 : 9800;
                renderer.transform.position=position; renderer.transform.localScale=Vector3.one*scale;
                renderer.transform.localRotation=Quaternion.Euler(0,0,angle); renderer.color=tint;
            }
        }

        // Called by ResetEntities. Keep allocated renderers for the next world.
        void ResetWeaponEffects()
        {
            foreach (var effect in weaponEffects) effect.sprite.enabled=false;
            foreach (var actor in players.Values) if (actor.equipment!=null) actor.equipment.SetVisible(false);
            foreach (var actor in pool) if (actor.equipment!=null) actor.equipment.SetVisible(false);
        }

        // The reticle shows the same radian spread and recoil as the authority.
        // It is presentation only; hit detection always remains in WorldSimulator.
        void DrawWeaponReticle(PlayerState player)
        {
            var gear = player == null ? null : EquipmentCatalog.Equipped(player);
            bool visible = player != null && player.hp > 0 && gear != null &&
                !EquipmentCatalog.Gear(gear.kind).tool && gear.kind != GearKind.Knife &&
                !bridge.BuildSelection.HasValue && !bridge.WorldInputBlocked;
            for (int i = 0; i < weaponReticle.Length; i++)
            {
                if (weaponReticle[i] == null && visible)
                    weaponReticle[i] = NewLine("Weapon spread reticle " + i, 10010, .026f, false);
                if (weaponReticle[i] != null) weaponReticle[i].enabled = visible;
            }
            if (!visible) return;
            Vector2 cursor = bridge.MouseWorld;
            float distance = Vector2.Distance(new Vector2(player.x, player.y), cursor);
            var stats = EquipmentCatalog.Stats(gear);
            float radians = EquipmentCatalog.EffectiveSpread(player, gear);
            float radius = Mathf.Max(.09f, Mathf.Tan(radians) * Mathf.Min(distance, stats.range + player.attributes[3] * .35f));
            Color color = player.reloadRemaining > 0 || gear.magazine == 0 || distance > stats.range + player.attributes[3] * .35f ?
                new Color(1f, .65f, .35f, .85f) : player.aiming ?
                new Color(.48f, 1f, .78f, .9f) : new Color(.9f, .95f, .9f, .75f);
            for (int i = 0; i < weaponReticle.Length; i++)
            {
                Vector2 direction = i == 0 ? Vector2.right : i == 1 ? Vector2.left : i == 2 ? Vector2.up : Vector2.down;
                var line = weaponReticle[i];
                line.startColor = line.endColor = color;
                line.positionCount = 2;
                line.SetPosition(0, cursor + direction * radius);
                line.SetPosition(1, cursor + direction * (radius + .15f));
            }
        }
    }
}
