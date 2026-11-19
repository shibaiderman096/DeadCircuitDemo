using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCircuit
{
    /// <summary>Presentation only. Snapshots are hints, not an event log.
    /// Add once and call Init. This component never changes gameplay or AudioListener.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class DemoAudio : MonoBehaviour
    {
        const int VoiceCount = 12;
        const float MaxGap = .4f, StaleAfter = .75f;
        const string ManifestPath = "Audio/Expansion/audio_manifest";
        static DemoAudio active;
        static readonly string[] ClipNames = {
            "ui_click_01", "ui_click_02", "ui_open", "ui_close", "gear_equip", "gear_unequip",
            "reload_start", "reload_complete", "weapon_pistol", "weapon_shotgun",
            "weapon_rifle", "weapon_sniper", "weapon_knife_swing", "hit_flesh", "hit_metal",
            "hit_wood", "gather_wood", "gather_ore", "farm_water", "farm_sow", "farm_harvest",
            "weather_thunder", "amb_wind_loop", "amb_rain_loop", "amb_machine_loop",
            "fx_electric_spark", "fx_explosion", "footstep_grit", "ui_denied", "item_pickup"
        };

        [SerializeField, Range(0f, 1f)] float effectsWeight = .8f;
        [SerializeField, Range(0f, 1f)] float ambienceWeight = .55f;
        string masterPreference = "DeadCircuit.MasterVolume", effectsPreference = "DeadCircuit.EffectsVolume";
        float targetMix = .56f, mix = .56f, preferenceAt, nextClick, nextGain, nextHit, nextFootstep;
        float lastFrameAt, lastAdvanceAt, nextMachineScan, machineTarget, nextThunder;
        long observedTick = -1;
        bool initialized, baseline, wasRunning, suspended;
        int clickVariant;
        GameBridge bridge;
        GameObject ownedRoot;
        Voice[] voices;
        AudioSource windSource, rainSource, machineSource;
        Snapshot previous = new Snapshot(), current = new Snapshot();
        readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>(StringComparer.Ordinal);

        [Serializable] sealed class Manifest { public Entry[] clips; }
        [Serializable] sealed class Entry
        { public string name, resourcePath; public bool loop; public float recommendedVolume = .7f; }
        sealed class Clip { public AudioClip audio; public float gain; public bool loop; }
        sealed class Voice
        { public AudioSource source; public float gain, started; public int priority; }
        struct GearSample
        { public GearKind kind; public int magazine; public ItemKind ammo; }
        struct PlotSample
        { public int crop; public float growth, moisture, health; }
        sealed class Snapshot
        {
            public string playerId;
            public int seed, width, height, deaths, weapon, tool, slot, reload, water;
            public long tick;
            public float elapsed, hp, cooldown, x, y;
            public bool survival, needs;
            public readonly int[] armor = new int[4];
            public int[] items = new int[DemoCatalog.ItemCount];
            public int[] foods = new int[SurvivalCatalog.FoodCount];
            public int[] seeds = new int[SurvivalCatalog.CropCount];
            public readonly Dictionary<int, GearSample> gear = new Dictionary<int, GearSample>();
            public readonly Dictionary<int, PlotSample> plots = new Dictionary<int, PlotSample>();
        }

        public void Init(GameBridge gameBridge)
        {
            if (gameBridge == null) throw new ArgumentNullException(nameof(gameBridge));
            if (active != null && active != this)
            { Debug.LogWarning("DeadCircuit: another initialized DemoAudio owns playback.", this); return; }
            bridge = gameBridge;
            if (!initialized)
            {
                LoadClips();
                ownedRoot = new GameObject("DeadCircuit Audio (owned)");
                ownedRoot.transform.SetParent(transform, false);
                voices = new Voice[VoiceCount];
                for (int i = 0; i < voices.Length; i++)
                    voices[i] = new Voice { source = CreateSource("Effect " + i, false) };
                windSource = CreateSource("Wind", true);
                rainSource = CreateSource("Rain", true);
                machineSource = CreateSource("Machinery", true);
                BindLoop(windSource, "amb_wind_loop");
                BindLoop(rainSource, "amb_rain_loop");
                BindLoop(machineSource, "amb_machine_loop");
                initialized = true;
            }
            ReadMix();
            mix = targetMix;
            ResetTracking(true);
            lastFrameAt = Time.unscaledTime;
            if (isActiveAndEnabled) active = this;
        }

        /// <summary>Settings handshake; empty master means full volume. Empty effects key
        /// uses effectsWeight. Polls preferences, writes none. Does not change a listener.
        /// </summary>
        public void SetVolumePreferences(string masterKey, string effectsKey = null)
        { masterPreference = masterKey; effectsPreference = effectsKey; ReadMix(); }

        public static void UiClick()
        {
            if (!Available || Time.unscaledTime < active.nextClick) return;
            active.nextClick = Time.unscaledTime + .035f;
            active.Play((active.clickVariant++ & 1) == 0 ? "ui_click_01" : "ui_click_02", 30);
        }
        public static void UiOpen() { if (Available) active.Play("ui_open", 30); }
        public static void UiClose() { if (Available) active.Play("ui_close", 30); }

        /// <summary>For exact event hooks unavailable from snapshots (explosion, spark,
        /// denied UI, material-specific impacts). A manifest cue name; never loads or queues.
        /// </summary>
        public static void PlayEffect(string cue)
        { if (Available) active.Play(cue, 100); }
        public static void PlayEffect(string cue, Vector3 worldPosition)
        { if (Available && active.bridge != null && active.bridge.IsRunning) active.Play(cue, 100, true, worldPosition); }

        static bool Available => active != null && active.initialized && active.isActiveAndEnabled && !active.suspended;

        void LoadClips()
        {
            TextAsset asset = Resources.Load<TextAsset>(ManifestPath);
            Manifest manifest = null;
            try { if (asset != null) manifest = JsonUtility.FromJson<Manifest>(asset.text); }
            catch (ArgumentException) { }
            int missing = 0;
            foreach (string name in ClipNames)
            {
                Entry found = null;
                if (manifest != null && manifest.clips != null)
                    foreach (Entry entry in manifest.clips)
                        if (entry != null && entry.name == name && entry.resourcePath == "Audio/Expansion/" + name)
                        { found = entry; break; }
                AudioClip audio = found == null ? null : Resources.Load<AudioClip>(found.resourcePath);
                if (audio == null) { missing++; continue; }
                clips.Add(name, new Clip { audio = audio, gain = Unit(found.recommendedVolume), loop = found.loop });
            }
            if (missing > 0) Debug.LogWarning("DeadCircuit: expansion audio unavailable for " + missing + "/30 cues; missing cues stay silent.", this);
        }

        AudioSource CreateSource(string label, bool loop)
        {
            var child = new GameObject(label);
            child.transform.SetParent(ownedRoot.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.volume = 0;
            source.spatialBlend = 0; source.dopplerLevel = 0; source.priority = loop ? 180 : 100;
            source.minDistance = 3; source.maxDistance = 24; source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        void BindLoop(AudioSource source, string name)
        { if (clips.TryGetValue(name, out Clip clip) && clip.loop) source.clip = clip.audio; }

        void Play(string name, int priority, bool spatial = false, Vector3 position = default(Vector3))
        {
            if (!initialized || active != this || !isActiveAndEnabled || suspended || string.IsNullOrEmpty(name) ||
                !clips.TryGetValue(name, out Clip clip) || clip.loop || targetMix < .0001f) return;
            Voice chosen = null;
            foreach (Voice voice in voices)
            {
                if (!voice.source.isPlaying) { chosen = voice; break; }
                if (chosen == null || voice.priority > chosen.priority ||
                    (voice.priority == chosen.priority && voice.started < chosen.started)) chosen = voice;
            }
            if (chosen == null || (chosen.source.isPlaying && chosen.priority < priority)) return;
            AudioSource source = chosen.source;
            source.Stop(); source.clip = clip.audio; source.loop = false; source.pitch = 1;
            source.spatialBlend = spatial ? 1 : 0; source.priority = priority;
            source.transform.position = spatial ? position : transform.position;
            chosen.gain = clip.gain; chosen.priority = priority; chosen.started = Time.unscaledTime;
            source.volume = chosen.gain * mix;
            // PlayOneShot could overlap extra voices on a source: use one clip per slot.
            source.Play();
        }

        void ReadMix()
        {
            float master = string.IsNullOrEmpty(masterPreference) ? 1 : PlayerPrefs.GetFloat(masterPreference, .7f);
            float effects = string.IsNullOrEmpty(effectsPreference) ? effectsWeight : PlayerPrefs.GetFloat(effectsPreference, effectsWeight);
            targetMix = Unit(master) * Unit(effects);
            preferenceAt = Time.unscaledTime + .2f;
        }

        void LateUpdate()
        {
            if (!initialized || active != this || suspended) return;
            if (bridge == null) { if (wasRunning) ResetTracking(true); return; }
            float now = Time.unscaledTime;
            float wallGap = now - lastFrameAt;
            lastFrameAt = now;
            float dt = Mathf.Min(Mathf.Max(Time.unscaledDeltaTime, 0), .1f);
            if (now >= preferenceAt) ReadMix();
            mix = Mathf.Lerp(mix, targetMix, 1 - Mathf.Exp(-dt / .12f));
            foreach (Voice voice in voices) voice.source.volume = voice.gain * mix;

            WorldState world = bridge.World;
            PlayerState player = bridge.LocalPlayer;
            if (!bridge.IsRunning || world == null || player == null || !player.connected)
            { if (wasRunning) ResetTracking(true); return; }
            wasRunning = true;
            if (world.tick != observedTick) { observedTick = world.tick; lastAdvanceAt = now; }
            if (now - lastAdvanceAt > StaleAfter) { ResetTracking(false); return; }

            var survival = world.survival;
            var needs = player.needs;
            Capture(current, world, player, survival, needs);
            bool discontinuity = !baseline || wallGap > MaxGap || world.tick < previous.tick ||
                world.tick - previous.tick > 8 || world.elapsed < previous.elapsed ||
                world.elapsed - previous.elapsed > MaxGap + .001f || current.seed != previous.seed ||
                current.width != previous.width || current.height != previous.height ||
                current.playerId != previous.playerId || current.deaths != previous.deaths ||
                current.survival != previous.survival || current.needs != previous.needs ||
                (previous.hp <= 0 && current.hp > 0) ||
                DistanceSq(current.x, current.y, previous.x, previous.y) > 64;
            if (discontinuity)
            {
                StopAll(); nextThunder = now + 10; nextFootstep = now + .3f;
                nextMachineScan = now; machineTarget = 0; baseline = true;
            }
            else DetectEvents(now, player);
            UpdateAmbience(world, player, survival, now, dt, !discontinuity);
            Snapshot swap = previous; previous = current; current = swap;
        }

        static void Capture(Snapshot sample, WorldState world, PlayerState p, SurvivalWorldState s, SurvivalPlayerState n)
        {
            sample.seed = world.seed; sample.width = world.width; sample.height = world.height;
            sample.tick = world.tick; sample.elapsed = world.elapsed; sample.playerId = p.id;
            sample.deaths = p.deaths; sample.hp = p.hp; sample.cooldown = p.attackCooldown;
            sample.weapon = p.weaponId; sample.tool = p.toolId; sample.slot = p.activeSlot; sample.reload = p.reloadingGearId;
            sample.x = p.x; sample.y = p.y; sample.survival = s != null && s.enabled;
            sample.needs = sample.survival && n != null && n.initialized;
            Copy(p.items, ref sample.items);
            for (int i = 0; i < sample.armor.Length; i++) sample.armor[i] = Count(p.armorIds, i);
            Copy(sample.needs ? n.foods : null, ref sample.foods);
            Copy(sample.needs ? n.seeds : null, ref sample.seeds);
            sample.water = sample.needs ? n.waterRaw + n.waterClean : 0;
            sample.gear.Clear();
            if (p.gear != null) foreach (GearState gear in p.gear) if (gear != null)
                sample.gear[gear.id] = new GearSample { kind = gear.kind, magazine = gear.magazine, ammo = gear.loadedAmmo };
            sample.plots.Clear();
            if (sample.survival && s.plots != null) foreach (FarmPlotState plot in s.plots)
                if (plot != null && DistanceSq(p.x, p.y, plot.x, plot.y) <= 9)
                    sample.plots[plot.id] = new PlotSample { crop = plot.crop, growth = plot.growth, moisture = plot.moisture, health = plot.health };
        }

        void DetectEvents(float now, PlayerState player)
        {
            bool gearChanged = current.weapon != previous.weapon || current.tool != previous.tool || current.slot != previous.slot;
            bool equipped = current.weapon != previous.weapon && current.weapon > 0 ||
                current.tool != previous.tool && current.tool > 0 || current.slot != previous.slot;
            for (int i = 0; i < current.armor.Length; i++)
                if (current.armor[i] != previous.armor[i]) { gearChanged = true; equipped |= current.armor[i] > 0; }
            if (gearChanged) Play(equipped ? "gear_equip" : "gear_unequip", 75);
            if (current.reload > 0 && current.reload != previous.reload) Play("reload_start", 60);
            int selected = current.slot == 0 ? current.weapon : current.tool;
            int oldSelected = previous.slot == 0 ? previous.weapon : previous.tool;
            if (selected > 0 && selected == oldSelected && current.gear.TryGetValue(selected, out GearSample gear) &&
                previous.gear.TryGetValue(selected, out GearSample old))
            {
                if (EquipmentCatalog.IsGun(gear.kind))
                {
                    // Collapse a multi-shot delta to ONE current report, never replay a magazine.
                    bool ammoReturned = Count(current.items, (int)gear.ammo) > Count(previous.items, (int)gear.ammo);
                    if (gear.kind == old.kind && gear.ammo == old.ammo && gear.magazine < old.magazine &&
                        current.cooldown > 0 && current.reload == 0 && previous.reload == 0 && !ammoReturned)
                        Play(GunCue(gear.kind), 45);
                    else if (gear.kind == old.kind && gear.magazine > old.magazine) Play("reload_complete", 60);
                }
                else if (current.cooldown > previous.cooldown + .01f && current.reload == 0) Play("weapon_knife_swing", 70);
            }
            if (current.hp < previous.hp - .01f && now >= nextHit)
            { Play("hit_flesh", 50); nextHit = now + .18f; }
            DetectGainsAndFarming(now);
            if (player.moving && current.hp > 0 && now >= nextFootstep &&
                DistanceSq(current.x, current.y, previous.x, previous.y) > .0004f)
            { Play("footstep_grit", 160); nextFootstep = now + .38f; }
        }

        void DetectGainsAndFarming(float now)
        {
            bool food = Increased(current.foods, previous.foods) ||
                Count(current.items, (int)ItemKind.Food) > Count(previous.items, (int)ItemKind.Food);
            bool seedSpent = Decreased(current.seeds, previous.seeds);
            bool planted = false, watered = false, harvested = false;
            foreach (KeyValuePair<int, PlotSample> pair in current.plots)
            {
                if (!previous.plots.TryGetValue(pair.Key, out PlotSample old)) continue;
                PlotSample plot = pair.Value;
                planted |= seedSpent && plot.crop >= 0 && (old.crop < 0 || old.crop != plot.crop || old.health <= 0);
                watered |= current.water < previous.water && plot.moisture > old.moisture + 2;
                // Natural incremental growth is silent; only an actual harvest or maturity edge.
                harvested |= old.crop >= 0 && plot.crop < 0 && food ||
                    old.crop == plot.crop && plot.crop >= 0 && old.growth < 1 && plot.growth >= 1;
            }
            bool material = false;
            for (int i = 0; i < current.items.Length; i++)
                if (i != (int)ItemKind.Food && !EquipmentCatalog.IsAmmo((ItemKind)i) &&
                    Count(current.items, i) > Count(previous.items, i)) { material = true; break; }
            // One inventory/farm cue per observation, independent of amount or number of plots.
            if (now < nextGain) return;
            string cue = planted ? "farm_sow" : watered ? "farm_water" : food || harvested ? "farm_harvest" : null;
            if (cue == null && material)
                cue = Count(current.items, (int)ItemKind.Wood) > Count(previous.items, (int)ItemKind.Wood) ? "gather_wood" :
                    OreIncreased() ? "gather_ore" : "farm_harvest";
            if (cue != null) { Play(cue, 110); nextGain = now + .25f; }
        }

        bool OreIncreased() =>
            Count(current.items, (int)ItemKind.Ore) > Count(previous.items, (int)ItemKind.Ore) ||
            Count(current.items, (int)ItemKind.CopperOre) > Count(previous.items, (int)ItemKind.CopperOre) ||
            Count(current.items, (int)ItemKind.TinOre) > Count(previous.items, (int)ItemKind.TinOre) ||
            Count(current.items, (int)ItemKind.UraniumOre) > Count(previous.items, (int)ItemKind.UraniumOre);

        void UpdateAmbience(WorldState world, PlayerState player, SurvivalWorldState survival, float now, float dt, bool allowThunder)
        {
            float rain = 0, wind = 0;
            SurvivalWeatherState weather = survival != null && survival.enabled ? survival.weather : null;
            if (weather != null)
            {
                // Interpolants, not kind alone, let weather phase transitions fade smoothly.
                rain = Unit(weather.rainfall) * (.6f + .4f * Unit(weather.intensity));
                wind = Unit(Mathf.Sqrt(weather.windX * weather.windX + weather.windY * weather.windY) / 2f) *
                    (.65f + .35f * Unit(weather.intensity));
                if (allowThunder && weather.kind == WeatherKind.Thunderstorm && now >= nextThunder)
                { Play("weather_thunder", 120); nextThunder = now + 14 + (int)((ulong)world.tick % 11); }
                else if (weather.kind != WeatherKind.Thunderstorm) nextThunder = now + 10;
            }
            else nextThunder = now + 10;
            if (now >= nextMachineScan)
            {
                nextMachineScan = now + .25f; machineTarget = 0;
                if (world.buildings != null) foreach (BuildingState building in world.buildings)
                {
                    if (building == null || building.hp <= 0 || building.kind == BuildKind.Wall ||
                        building.kind == BuildKind.PowerPole || building.kind == BuildKind.MediumPole ||
                        building.kind == BuildKind.HighPole || building.kind == BuildKind.SolarPanel ||
                        building.kind == BuildKind.Belt || building.kind == BuildKind.Chest ||
                        !(building.powered || building.powerGeneration > .01f)) continue;
                    float distance = Mathf.Sqrt(DistanceSq(player.x, player.y, DemoCatalog.CenterX(building), DemoCatalog.CenterY(building)));
                    machineTarget = Mathf.Max(machineTarget, Mathf.Clamp01(1 - distance / 14));
                }
            }
            FadeLoop(rainSource, "amb_rain_loop", rain, dt);
            FadeLoop(windSource, "amb_wind_loop", wind, dt);
            FadeLoop(machineSource, "amb_machine_loop", machineTarget, dt);
        }

        void FadeLoop(AudioSource source, string cue, float strength, float dt)
        {
            if (source.clip == null || !clips.TryGetValue(cue, out Clip clip)) return;
            float volume = Unit(strength) * clip.gain * Unit(ambienceWeight) * mix;
            source.volume = Mathf.MoveTowards(source.volume, volume, dt * .35f);
            if (source.volume > .0001f) { if (!source.isPlaying) source.Play(); }
            else if (volume <= .0001f && source.isPlaying) source.Stop();
        }

        void ResetTracking(bool resetTick)
        {
            baseline = false; wasRunning = false; machineTarget = 0;
            nextGain = nextHit = nextClick = nextFootstep = 0;
            if (resetTick) { observedTick = -1; lastAdvanceAt = Time.unscaledTime; }
            StopAll();
        }
        void StopAll()
        {
            if (voices != null) foreach (Voice voice in voices)
                if (voice.source != null) { voice.source.Stop(); voice.source.clip = null; voice.source.volume = 0; voice.gain = 0; }
            StopLoop(windSource); StopLoop(rainSource); StopLoop(machineSource);
        }
        static void StopLoop(AudioSource source)
        { if (source != null) { source.Stop(); source.volume = 0; } }

        void OnEnable()
        {
            if (initialized && (active == null || active == this))
            { active = this; ResetTracking(true); lastFrameAt = Time.unscaledTime; }
        }
        void OnDisable() { if (active == this) active = null; ResetTracking(true); }
        void OnApplicationPause(bool paused)
        { suspended = paused; if (initialized) { ResetTracking(true); lastFrameAt = Time.unscaledTime; } }
        void OnDestroy()
        {
            if (active == this) active = null;
            StopAll();
            if (ownedRoot != null) Destroy(ownedRoot);
            // Resource clips are shared: never unload assets or touch external audio objects.
            clips.Clear(); bridge = null; initialized = false;
        }

        static string GunCue(GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Pistol: return "weapon_pistol";
                case GearKind.Shotgun: return "weapon_shotgun";
                case GearKind.AssaultRifle: return "weapon_rifle";
                case GearKind.PrecisionRifle: return "weapon_sniper";
                default: return null;
            }
        }
        static float Unit(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value);
        static int Count(int[] values, int i) => values != null && i >= 0 && i < values.Length ? values[i] : 0;
        static float DistanceSq(float ax, float ay, float bx, float by) => (ax - bx) * (ax - bx) + (ay - by) * (ay - by);
        static void Copy(int[] source, ref int[] destination)
        {
            int length = source == null ? 0 : source.Length;
            if (destination.Length != length) destination = new int[length];
            if (length > 0) Array.Copy(source, destination, length);
        }
        static bool Increased(int[] now, int[] before)
        { if (now.Length != before.Length) return false; for (int i = 0; i < now.Length; i++) if (now[i] > before[i]) return true; return false; }
        static bool Decreased(int[] now, int[] before)
        { if (now.Length != before.Length) return false; for (int i = 0; i < now.Length; i++) if (now[i] < before[i]) return true; return false; }
    }
}
