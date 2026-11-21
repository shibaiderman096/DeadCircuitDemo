using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        sealed class ActorEquipment
        {
            public SpriteRenderer weapon, hat, vest, trousers, boots, optic, muzzle, flash;
            public Vector2 muzzleWorld, samplePosition;
            public string playerId;
            public int gearId, magazine, durability;
            public GearKind gearKind;
            public bool primed;
            public long sampleTick=-1;
            public float sampleElapsed, sampledAt, lastTraceUntil=-1, previousCooldown, attackAt=-100, armorImpactAt=-100;
            public void SetVisible(bool value)
            {
                weapon.enabled=hat.enabled=vest.enabled=trousers.enabled=boots.enabled=optic.enabled=muzzle.enabled=flash.enabled=value;
                if (!value)
                {
                    primed=false; playerId=null; sampleTick=-1; lastTraceUntil=-1; attackAt=armorImpactAt=-100;
                    muzzleWorld=Vector2.zero;
                }
            }
        }
        readonly Dictionary<int,Visual> wildlife = new Dictionary<int,Visual>();
        readonly Dictionary<int,Visual> farmPlots = new Dictionary<int,Visual>();
        readonly List<SpriteRenderer> weatherParticles = new List<SpriteRenderer>(96);
        Material expansionGroundMaterial;
        Image weatherVeil;
        struct EffectKey : System.IEquatable<EffectKey>
        {
            public float x,y,until; public int effect;
            public bool Equals(EffectKey other) => x==other.x && y==other.y && until==other.until && effect==other.effect;
            public override bool Equals(object other) => other is EffectKey key && Equals(key);
            public override int GetHashCode() => unchecked(((x.GetHashCode()*397^y.GetHashCode())*397^until.GetHashCode())*397^effect);
        }
        readonly Dictionary<EffectKey,float> observedEffects=new Dictionary<EffectKey,float>();
        readonly List<EffectKey> expiredEffects=new List<EffectKey>();
        long effectTick=-1;
        float effectElapsed;

        static readonly Color[] BiomeTints =
        {
            Color.white, new Color(.76f,.94f,.76f), new Color(.68f,.83f,.81f),
            new Color(1.24f,1.13f,.78f), new Color(.76f,.91f,.84f), new Color(1.08f,1.18f,1.28f),
            new Color(.61f,.77f,.58f), new Color(.83f,.94f,1.12f),
            new Color(1.10f,.72f,.91f), new Color(.72f,.91f,.94f)
        };
        static int BiomeAt(WorldState world,int x,int y)
        {
            char biome = WorldTiles.Biome(world, x, y);
            return biome >= '0' && biome < '0' + WorldGeneration.BiomeCount ? biome - '0' : 0;
        }
        static int BiomeArtIndex(int biome) => biome == (int)BiomeKind.Swamp ? (int)BiomeKind.Wetland :
            biome == (int)BiomeKind.SnowMountain ? (int)BiomeKind.Tundra :
            biome == (int)BiomeKind.SakuraGrove ? (int)BiomeKind.DeciduousForest :
            biome == (int)BiomeKind.RiverValley ? (int)BiomeKind.Meadow : biome;
        static Color BiomeTint(WorldState world,int x,int y) => world.generatorVersion>0 ? BiomeTints[BiomeAt(world,x,y)] : Color.white;
        Material ExpansionGroundMaterial()
        {
            if (expansionGroundMaterial==null)
            {
                expansionGroundMaterial=new Material(terrainMaterial) { name="DeadCircuit SVG terrain" };
                Sprite tile=DemoArt.Get("terrain_0");
                if (tile!=null) expansionGroundMaterial.mainTexture=tile.texture;
            }
            return expansionGroundMaterial;
        }

        void DrawExpansion(WorldState world,PlayerState local)
        {
            if (world.survival!=null && world.survival.enabled)
            {
                foreach (var plot in world.survival.plots)
                {
                    if (!Visible(plot.x,plot.y) || NaturalInteriorHidden(plot.x, plot.y)) continue;
                    var v=Get(farmPlots,plot.id,new Vector2(plot.x,plot.y),plot.health,100);
                    v.shadow.enabled=false; v.body.sprite=DemoArt.Get("fx_soil") ?? Art("pixel");
                    v.body.sortingOrder=-16000; v.body.transform.localScale=Vector3.one*.95f;
                    v.body.color=Color.Lerp(new Color(.65f,.49f,.30f),new Color(.32f,.29f,.23f),plot.moisture/100f);
                    v.detail.enabled=plot.crop>=0;
                    if (plot.crop>=0)
                    {
                        v.detail.sprite=DemoArt.Get("crop_"+plot.crop+"_"+(plot.growth>=1 ? 1 : 0));
                        v.detail.color=plot.health<25 ? new Color(.65f,.49f,.27f) : Color.white;
                        v.detail.transform.localScale=Vector3.one*(.45f+plot.growth*.55f);
                        v.detail.transform.localPosition=new Vector3(0,.12f,0);
                        v.detail.sortingOrder=-15000;
                    }
                    Health(v,plot.growth*100,plot.crop>=0 && plot.growth<1,new Vector2(0,-.35f));
                    if (plot.crop>=0) v.bar.color=plot.health<25 ? Danger : new Color(.60f,.78f,.31f);
                }
                foreach (var creature in world.survival.creatures)
                {
                    if (!Visible(creature.x,creature.y) || NaturalInteriorHidden(creature.x, creature.y)) continue;
                    var definition=SurvivalCatalog.Creatures[(int)creature.kind];
                    var v=Get(wildlife,creature.id,new Vector2(creature.x,creature.y),creature.hp,definition.health);
                    bool walking=(new Vector2(creature.x,creature.y)-v.previous).sqrMagnitude>.00001f;
                    if (walking) v.walk+=Time.unscaledDeltaTime*8;
                    var delta=new Vector2(creature.x,creature.y)-v.previous;
                    if (Mathf.Abs(delta.x)>.005f) v.body.flipX=delta.x<0;
                    v.body.sprite=DemoArt.Get("creature_"+(int)creature.kind);
                    v.body.color=DamageColor(v,creature.hp>0 ? Color.white : new Color(.59f,.52f,.43f,.85f));
                    float scale=creature.kind==CreatureKind.Bear || creature.kind==CreatureKind.Cattle ? 1.45f :
                        creature.kind==CreatureKind.Hare || creature.kind==CreatureKind.Chicken || creature.kind==CreatureKind.Duck ? .7f : 1.08f;
                    v.body.transform.localScale=Vector3.one*scale;
                    v.body.transform.localPosition=new Vector3(0,creature.hp>0 ? .25f+(walking ? Mathf.Sin(v.walk*3)*.025f : 0) : 0,0);
                    v.body.transform.localRotation=Quaternion.Euler(0,0,creature.hp<=0 ? -90 : walking ? Mathf.Sin(v.walk*2)*2 : 0);
                    Health(v,creature.hp,creature.hp>0 && creature.hp<definition.health,new Vector2(0,.8f));
                    v.previous=Vector2.Lerp(v.previous,new Vector2(creature.x,creature.y),1-Mathf.Exp(-8*Time.unscaledDeltaTime));
                }
            }
            Sweep(farmPlots); Sweep(wildlife);
            DrawEffectCues(world);
            DrawWeather(world);
        }

        void DrawPlayerEquipment(Visual visual,PlayerState player)
        {
            if (visual.equipment==null)
            {
                var e=new ActorEquipment(); visual.equipment=e; Transform parent=visual.root.transform;
                e.weapon=NewSprite("Held weapon or tool",parent,6);
                e.hat=NewSprite("Head armor",parent,7); e.vest=NewSprite("Torso armor",parent,4);
                e.trousers=NewSprite("Leg armor",parent,3); e.boots=NewSprite("Foot armor",parent,3);
                e.optic=NewSprite("Installed optic",parent,8); e.muzzle=NewSprite("Installed muzzle",parent,8);
                e.flash=NewSprite("Shot muzzle flash",parent,10);
            }
            var look=visual.equipment; int order=visual.body.sortingOrder;
            if (visual.avatar != null && visual.avatar.root.gameObject.activeSelf)
                look.hat.enabled=look.vest.enabled=look.trousers.enabled=look.boots.enabled=false;
            else
            {
                DrawArmorLayer(look.hat,player,0,new Vector2(0,.86f),.42f,order+7);
                DrawArmorLayer(look.vest,player,1,new Vector2(0,.52f),.46f,order+4);
                DrawArmorLayer(look.trousers,player,2,new Vector2(0,.26f),.38f,order+3);
                DrawArmorLayer(look.boots,player,3,new Vector2(0,.06f),.36f,order+3);
            }
            if (visual.damageAt>look.armorImpactAt && visual.damageAt>0)
            {
                look.armorImpactAt=visual.damageAt;
                if (EquipmentCatalog.ArmorRating(player)>0)
                {
                    Spawn("fx_spark",visual.position+Vector2.up*.5f,new Color(.8f,.9f,1),.3f,.42f,Vector2.up);
                    DemoAudio.PlayEffect("hit_metal",visual.position);
                }
            }
            GearState gear=EquipmentCatalog.Equipped(player);
            bool holding=gear!=null && player.hp>0; look.weapon.enabled=holding;
            look.optic.enabled=look.muzzle.enabled=look.flash.enabled=false;
            if (!holding) { look.primed=false; look.attackAt=-100; look.muzzleWorld=visual.position; return; }
            DrawEquippedWeapon(visual,player,gear);
        }

        void DrawArmorLayer(SpriteRenderer renderer,PlayerState player,int slot,Vector2 position,float scale,int order)
        {
            var gear=EquipmentCatalog.Find(player,player.armorIds[slot]);
            renderer.enabled=player.hp>0 && gear!=null && gear.durability>0;
            if (!renderer.enabled) return;
            renderer.sprite=DemoArt.Get("gear_"+(int)gear.kind); renderer.color=Color.white;
            renderer.transform.localPosition=position; renderer.transform.localScale=Vector3.one*scale;
            renderer.transform.localRotation=Quaternion.identity; renderer.sortingOrder=order;
        }
        void DrawWeaponPart(SpriteRenderer renderer,int part,Vector2 position,float angle,bool flipY,float scale,int order)
        {
            renderer.enabled=part>=0 && part<EquipmentCatalog.PartNames.Length; if (!renderer.enabled) return;
            renderer.sprite=DemoArt.Get("part_"+part) ?? Art("pixel"); renderer.color=Color.white;
            renderer.flipX=false; renderer.flipY=flipY; renderer.transform.localScale=Vector3.one*scale;
            renderer.transform.localRotation=Quaternion.Euler(0,0,angle); renderer.sortingOrder=order;
            // Legacy part art is centred: mount the muzzle's rear or the optic's foot, not its centre.
            Vector2 anchor=part==(int)PartKind.Suppressor || part==(int)PartKind.MuzzleBrake ? new Vector2(3,16) : new Vector2(16,26);
            renderer.transform.position=(Vector3)position-renderer.transform.localRotation*(Vector3)(WeaponSpritePoint(renderer.sprite,anchor,32,32,flipY)*scale);
        }
        void DrawEnemyAura(Visual v,ZombieState enemy)
        {
            if (enemy.variant!=6 && enemy.variant!=8 && enemy.variant!=9 && enemy.variant!=10) return;
            if (Time.unscaledTime<v.nextSpecialEffect) return;
            v.nextSpecialEffect=Time.unscaledTime+.35f;
            string key=enemy.variant==6 ? "fx_flame" : enemy.variant==8 ? "fx_poison" : enemy.variant==9 ? "fx_ice" : "fx_spark";
            Color tint=enemy.variant==9 ? new Color(.62f,.86f,1) : enemy.variant==8 ? new Color(.70f,.91f,.32f) : Amber;
            Spawn(key,v.position+new Vector2(.1f,.55f),tint,.5f,.30f,Vector2.up*.4f);
        }
        void DrawEffectCues(WorldState world)
        {
            if (effectTick==world.tick) return;
            bool discontinuity=effectTick<0 || world.tick<effectTick || world.tick-effectTick>8 || world.elapsed-effectElapsed>.4f;
            expiredEffects.Clear();
            foreach (var pair in observedEffects) if (pair.Value<world.elapsed) expiredEffects.Add(pair.Key);
            foreach (var key in expiredEffects) observedEffects.Remove(key);
            if (observedEffects.Count>WorldSimulator.MaxTraces*4) { observedEffects.Clear(); discontinuity=true; }
            foreach (var trace in world.traces)
            {
                if (trace.effect<3 || trace.until<world.elapsed) continue;
                var key=new EffectKey { x=trace.x,y=trace.y,until=trace.until,effect=trace.effect };
                if (observedEffects.ContainsKey(key)) continue;
                observedEffects.Add(key,trace.until+.3f);
                if (discontinuity || !Visible(trace.x,trace.y)) continue;
                if (NaturalInteriorHidden(trace.x, trace.y) || NaturalInteriorHidden(trace.endX, trace.endY)) continue;
                Vector2 at=new Vector2(trace.x,trace.y), end=new Vector2(trace.endX,trace.endY);
                string sprite=trace.effect==3 || trace.effect==5 ? "fx_poison" : trace.effect==6 ? "fx_ice" : trace.effect==7 ? "fx_explosion" : trace.effect==8 ? "fx_wave" : "fx_flame";
                Color tint=trace.effect==3 || trace.effect==5 ? new Color(.64f,.91f,.31f) : trace.effect==6 ? new Color(.53f,.84f,1) : trace.effect==8 ? new Color(.79f,.56f,.93f) : Amber;
                Spawn(sprite,trace.effect==3 || trace.effect==6 ? end : at,tint,.55f,trace.effect==7 ? 2.3f : trace.effect==8 ? 1.4f : .8f,Vector2.up*.2f);
                if (trace.effect==7)
                {
                    Spawn("fx_wave",at,Amber,.45f,3,Vector2.zero);
                    DemoAudio.PlayEffect("fx_explosion",at);
                }
            }
            effectTick=world.tick; effectElapsed=world.elapsed;
        }

        void DrawWeather(WorldState world)
        {
            var survival=world.survival; bool active=survival!=null && survival.enabled;
            var w=active ? survival.weather : null;
            WeatherKind kind=active ? w.kind : WeatherKind.Clear;
            bool rain=active && w.rainfall>.015f;
            bool snow=active && kind==WeatherKind.Snow;
            bool dust=active && kind==WeatherKind.DustStorm;
            bool fog=active && kind==WeatherKind.Fog;
            int count=active ? Mathf.Clamp(Mathf.RoundToInt((snow ? w.intensity*65 : rain ? w.rainfall*88 : dust ? w.intensity*72 : fog ? 16 : 0)),0,96) : 0;
            string key=snow ? "fx_snow" : dust ? "fx_dust" : fog ? "fx_fog" : "fx_rain";
            while (weatherParticles.Count<count) weatherParticles.Add(NewSprite("Weather particle "+weatherParticles.Count,effectsRoot,9700));
            Vector2 span=viewMax-viewMin; float time=Time.unscaledTime;
            for (int i=0;i<weatherParticles.Count;i++)
            {
                var r=weatherParticles[i]; r.enabled=i<count; if (!r.enabled) continue;
                uint hash=Hash(i,79,seed); float ax=(hash%997)/997f, ay=((hash>>12)%991)/991f;
                float fall=snow ? 1.1f : dust || fog ? .12f : 11;
                float px=Mathf.Repeat(ax*span.x+time*w.windX*(snow ? 1.3f : 2.2f),span.x);
                float py=Mathf.Repeat(ay*span.y-time*fall,span.y);
                r.sprite=DemoArt.Get(key); r.transform.position=new Vector3(viewMin.x+px,viewMin.y+py,0);
                r.transform.localRotation=Quaternion.Euler(0,0,rain ? -w.windX*12 : snow ? time*10+i*19 : 0);
                r.transform.localScale=new Vector3(fog ? 4 : dust ? 1.3f : snow ? .22f : .36f,fog ? 2 : dust ? .7f : snow ? .22f : .7f,1);
                r.color=fog ? new Color(.78f,.84f,.82f,.16f*w.intensity) : dust ? new Color(.83f,.67f,.39f,.5f) : new Color(.78f,.89f,.95f,snow ? .8f : .5f);
            }
            if (weatherVeil==null)
            {
                weatherVeil=UI("Weather veil",hurtOverlay.transform.parent,Color.clear);
                weatherVeil.raycastTarget=false; weatherVeil.rectTransform.anchorMin=Vector2.zero; weatherVeil.rectTransform.anchorMax=Vector2.one;
                weatherVeil.rectTransform.offsetMin=weatherVeil.rectTransform.offsetMax=Vector2.zero; weatherVeil.transform.SetAsFirstSibling();
            }
            weatherVeil.gameObject.SetActive(active);
            float flash=active && kind==WeatherKind.Thunderstorm && w.phaseTick>20 && Mathf.Repeat(world.tick+seed%91,360)<2 ? .10f*w.intensity : 0;
            weatherVeil.color=flash>0 ? new Color(.82f,.91f,1,flash) : fog ? new Color(.68f,.78f,.77f,.12f*w.intensity) :
                dust ? new Color(.64f,.46f,.23f,.13f*w.intensity) : kind==WeatherKind.Heatwave ? new Color(.84f,.49f,.21f,.035f) : Color.clear;
        }
        void ResetExpansion()
        {
            foreach (var v in wildlife.Values) Release(v); wildlife.Clear();
            foreach (var v in farmPlots.Values) Release(v); farmPlots.Clear();
            foreach (var p in weatherParticles) if (p!=null) p.enabled=false;
            if (weatherVeil!=null) weatherVeil.gameObject.SetActive(false);
            observedEffects.Clear(); expiredEffects.Clear(); effectTick=-1; effectElapsed=0;
        }
    }
}
