using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DeadCircuit
{
    /// <summary>Read-only presentation. Integer XY is the centre of a world cell.
    /// Existing art is authored by Tools/generate_art.py; power pixels are runtime-only.
    /// No camera zoom or simulation commands live here.</summary>
    public sealed partial class DemoWorldView : MonoBehaviour
    {
        public Camera WorldCamera { get; private set; }
        public RectTransform MinimapRect { get; private set; }

        // These fields are populated by JsonUtility, rather than C# assignments.
#pragma warning disable 0649
        [Serializable] sealed class Atlas { public Entry[] sprites; }
        [Serializable] sealed class Entry
        {
            public string name;
            public int x, y, width, height;
            public float pixelsPerUnit, pivotX, pivotY;
        }
#pragma warning restore 0649

        sealed class Visual
        {
            public GameObject root;
            public SpriteRenderer body, shadow, detail, arrow, lamp, cargo, barBack, bar;
            public Vector2 position, previous;
            public float hp, maxHp, damageAt = -100f, walk, nextSpecialEffect;
            public int stamp;
            public ActorEquipment equipment;
            public SurvivorRig avatar;
            public ModularMachineVisual modules;
        }

        sealed class TraceVisual
        {
            public LineRenderer line, core;
            public SpriteRenderer impact;
            public float until = -1, x, y, endX, endY;
            public Vector2 shotFrom, shotTo;
            public bool shotImpact;
        }

        sealed class CargoVisual
        {
            public SpriteRenderer sprite;
            public Vector2 from, to;
            public int fromId, toId, item, stamp;
            public float progress;
        }

        struct PowerLink
        {
            public int fromId, toId;
            public PowerLink(int from, int to) { fromId = from; toId = to; }
        }

        sealed class Particle
        {
            public SpriteRenderer sprite;
            public float born, lifetime;
            public Vector3 velocity;
            public Color tint;
            public float scale;
        }

        // Fixed chunk geometry, pooled visible entities, and a 5 Hz minimap avoid
        // creating thousands of terrain GameObjects or rebuilding on every snapshot.
        const int ChunkSize = ChunkStreaming.ChunkSize;
        const int MaxParticles = 64;
        static readonly Color Amber = new Color(1f, .70f, .33f);
        static readonly Color Mint = new Color(.46f, .94f, .77f);
        static readonly Color Danger = new Color(1f, .35f, .26f);
        static readonly Color[] BackpackFallbackTints =
        {
            new Color(1f, .83f, .54f), new Color(.69f, 1f, .72f), new Color(.59f, .86f, 1f)
        };
        static readonly string[] MachineNames = { "wall", "generator", "miner", "belt", "smelter", "assembler", "turret", "chest" };
        static readonly Color[] NetworkColors =
        {
            Mint, new Color(.47f, .75f, .98f), new Color(.90f, .72f, .42f),
            new Color(.77f, .64f, .96f), new Color(.93f, .60f, .62f)
        };
        static readonly Color[] ItemColors =
        {
            new Color(.70f,.46f,.24f), new Color(.63f,.75f,.73f), new Color(.85f,.89f,.85f),
            new Color(.75f,.44f,.31f), Amber, new Color(.91f,.80f,.46f), Mint,
            new Color(.77f,.45f,.25f), new Color(.94f,.58f,.31f), new Color(.77f,.60f,.27f),
            new Color(.65f,.73f,.78f), new Color(.29f,.72f,.53f), new Color(.86f,.86f,.74f),
            new Color(.45f,.66f,.90f), new Color(.59f,.82f,.32f), new Color(.84f,.92f,.28f),
            new Color(.32f,.80f,.92f), new Color(.70f,.66f,.25f), new Color(.64f,.62f,.55f),
            new Color(.57f,.67f,.73f), new Color(.79f,.85f,.84f),
            new Color(.88f,.72f,.36f), new Color(.80f,.30f,.24f),
            new Color(.65f,.83f,.92f), new Color(.64f,.80f,.40f),
            new Color(.70f,.72f,.68f), new Color(.85f,.53f,.34f),
            new Color(.28f,.30f,.29f), new Color(.78f,.36f,.23f)
        };
        static readonly Color32[] MapColors =
        {
            new Color32(67, 87, 61, 255), new Color32(119, 103, 70, 255),
            new Color32(36, 83, 88, 255), new Color32(83, 88, 76, 255),
            new Color32(124, 123, 100, 255)
        };

        GameBridge bridge;
        DemoHud backpackHud;
        Sprite backpackFallback;
        readonly Sprite[] backpackSprites = new Sprite[3];
        Transform worldRoot, terrainRoot, entityRoot, effectsRoot;
        GameObject minimapCanvas;
        GameObject ownedAudioListener;
        RawImage minimapImage;
        Image hurtOverlay;
        Texture2D spriteTexture, terrainTexture, mapTexture, powerTexture, industryTexture;
        Color32[] mapPixels;
        Material spriteMaterial, terrainMaterial, effectMaterial;
        Volume volume;
        VolumeProfile volumeProfile;
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly Sprite[] itemSprites = new Sprite[DemoCatalog.ItemCount];
        readonly Dictionary<int, Visual> nodes = new Dictionary<int, Visual>();
        readonly Dictionary<int, Visual> droppedBackpacks = new Dictionary<int, Visual>();
        readonly Dictionary<int, Visual> buildings = new Dictionary<int, Visual>();
        readonly Dictionary<int, Visual> zombies = new Dictionary<int, Visual>();
        readonly Dictionary<string, Visual> players = new Dictionary<string, Visual>();
        readonly Stack<Visual> pool = new Stack<Visual>();
        readonly List<int> removeIds = new List<int>();
        readonly List<string> removePlayers = new List<string>();
        readonly List<TraceVisual> traces = new List<TraceVisual>();
        readonly List<LineRenderer> ranges = new List<LineRenderer>();
        readonly List<LineRenderer> wires = new List<LineRenderer>();
        readonly List<LineRenderer> previewWires = new List<LineRenderer>();
        readonly List<BuildingState> powerNodes = new List<BuildingState>();
        readonly Dictionary<int, BuildingState> buildingStates = new Dictionary<int, BuildingState>();
        readonly Dictionary<int, PowerNetworkState> networks = new Dictionary<int, PowerNetworkState>();
        readonly List<PowerLink> powerLinks = new List<PowerLink>();
        readonly HashSet<int> linkedNodes = new HashSet<int>();
        readonly List<int> linkQueue = new List<int>();
        readonly Dictionary<int, CargoVisual> cargoVisuals = new Dictionary<int, CargoVisual>();
        readonly Stack<CargoVisual> cargoPool = new Stack<CargoVisual>();
        readonly HashSet<int> liveCargoIds = new HashSet<int>();
        readonly List<Particle> particles = new List<Particle>();
        readonly HashSet<long> occupied = new HashSet<long>();
        readonly HashSet<long> blockedNodes = new HashSet<long>();
        readonly HashSet<long> groundPickupCells = new HashSet<long>();
        readonly HashSet<long> solidBuildings = new HashSet<long>();
        readonly Vector3[] circleVertices = new Vector3[96];
        readonly Vector3[] footprintVertices = new Vector3[5];
        readonly Vector3[] wireVertices = new Vector3[9];
        readonly BuildingState previewBuilding = new BuildingState { footprintVersion = 1 };
        SpriteRenderer ghost, selectionRing, hoverRing, ghostArrow, lantern;
        LineRenderer footprint, ghostRange, ghostLinkRange, hoverFootprint;
        WorldState observedWorld;
        long observedTick = -1;
        ulong powerTopology;
        int focusedNetwork;
        string groundCache;
        int width, height, seed, stamp;
        float nextMapAt, damagePulse, visualTime, observedElapsed = -1f, observedAt;
        Vector3 cameraVelocity;
        Vector2 viewMin, viewMax;
        bool initialized, wasRunning, cameraPlaced, ownsCamera, minimapAttached;

        public void Init(GameBridge game)
        {
            bridge = game;
            if (initialized) return;
            spriteTexture = Resources.Load<Texture2D>("Art/sprites");
            terrainTexture = Resources.Load<Texture2D>("Art/terrain");
            var manifest = Resources.Load<TextAsset>("Art/atlas");
            if (spriteTexture == null || terrainTexture == null || manifest == null)
            {
                Debug.LogError("DeadCircuit art is missing. Run python Tools/generate_art.py and import Resources/Art.", this);
                return;
            }
            spriteTexture.filterMode = terrainTexture.filterMode = FilterMode.Point;
            spriteTexture.wrapMode = terrainTexture.wrapMode = TextureWrapMode.Clamp;
            var entries = JsonUtility.FromJson<Atlas>(manifest.text);
            if (entries == null || entries.sprites == null) return;
            foreach (var e in entries.sprites)
            {
                // Sprite.Create supplies each asset's real PPU regardless of importer PPU=32.
                var rect = new Rect(e.x, spriteTexture.height - e.y - e.height, e.width, e.height);
                var sprite = Sprite.Create(spriteTexture, rect, new Vector2(e.pivotX, e.pivotY),
                    e.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = e.name;
                sprites.Add(e.name, sprite);
            }
            for (int i = 0; i < itemSprites.Length; i++) itemSprites[i] = Art("item_" + i);
            backpackFallback = Art("ui_inventory");
            for (int i = 0; i < backpackSprites.Length; i++)
                backpackSprites[i] = DemoArt.GetIcon("gear_" + ((int)GearKind.GrassBackpack + i)) ?? backpackFallback;
            CreatePowerArt();
            CreateIndustryArt();
            var supplied = Resources.Load<Material>("Art/WorldMaterial");
            var pixelShader = Resources.Load<Shader>("Art/DeadCircuitPixel");
            if (supplied == null && pixelShader == null)
            {
                Debug.LogError("DeadCircuit needs Art/WorldMaterial or Art/DeadCircuitPixel shader.", this);
                return;
            }
            spriteMaterial = supplied != null ? new Material(supplied) : new Material(pixelShader);
            spriteMaterial.name = "DeadCircuit runtime sprites";
            spriteMaterial.mainTexture = spriteTexture;
            // The included Resources shader also handles vertex-coloured chunk meshes.
            terrainMaterial = pixelShader != null ? new Material(pixelShader) : new Material(spriteMaterial);
            terrainMaterial.mainTexture = terrainTexture;
            effectMaterial = pixelShader != null ? new Material(pixelShader) : new Material(spriteMaterial);
            effectMaterial.mainTexture = Texture2D.whiteTexture;
            worldRoot = NewRoot("DeadCircuit / World view", transform);
            terrainRoot = NewRoot("Ground chunks", worldRoot);
            entityRoot = NewRoot("Visible entities", worldRoot);
            effectsRoot = NewRoot("Read-only effects", worldRoot);
            SetupCamera();
            SetupPostProcessing();
            SetupMinimap();
            ghost = NewSprite("Build preview", effectsRoot, 10000);
            ghostArrow = NewSprite("Preview output", effectsRoot, 10002);
            ghostArrow.sprite = Art("arrow");
            ghostArrow.transform.localScale = Vector3.one * .55f;
            selectionRing = NewSprite("Local survivor ring", effectsRoot, -11000);
            selectionRing.sprite = Art("ring");
            selectionRing.color = new Color(.47f, .89f, .73f, .58f);
            selectionRing.transform.localScale = new Vector3(.61f, .38f, 1);
            hoverRing = NewSprite("Inspection marker", effectsRoot, 9800);
            hoverRing.sprite = Art("ring");
            lantern = NewSprite("Survivor lantern", effectsRoot, 9000);
            lantern.sprite = Art("glow");
            lantern.transform.localScale = Vector3.one * 5.5f;
            footprint = NewLine("Preview footprint", 10001, .035f, false);
            ghostRange = NewLine("Preview power reach", -10500, .035f, true);
            ghostLinkRange = NewLine("Preview node link reach", -10500, .02f, true);
            hoverFootprint = NewLine("Inspected footprint", 9800, .03f, false);
            worldRoot.gameObject.SetActive(false);
            minimapCanvas.SetActive(false);
            initialized = true;
        }

        void CreatePowerArt()
        {
            // Two tiny runtime sprites share one point-filtered atlas; no imported art changes.
            var pixels = new Color32[64 * 32];
            var ink = new Color32(24, 35, 37, 255);
            var steel = new Color32(126, 144, 138, 255);
            PaintPixels(pixels, 5, 2, 6, 4, ink);
            PaintPixels(pixels, 6, 4, 4, 24, ink);
            PaintPixels(pixels, 7, 5, 2, 21, new Color32(149, 113, 66, 255));
            PaintPixels(pixels, 1, 24, 14, 3, ink);
            PaintPixels(pixels, 2, 25, 12, 1, steel);
            PaintPixels(pixels, 3, 27, 3, 3, ink);
            PaintPixels(pixels, 10, 27, 3, 3, ink);
            PaintPixels(pixels, 4, 27, 1, 2, new Color32(118, 201, 185, 255));
            PaintPixels(pixels, 11, 27, 1, 2, new Color32(118, 201, 185, 255));
            PaintPixels(pixels, 19, 2, 42, 27, ink);
            PaintPixels(pixels, 20, 4, 40, 24, steel);
            PaintPixels(pixels, 21, 5, 38, 22, new Color32(15, 28, 48, 255));
            for (int row = 0; row < 3; row++) for (int col = 0; col < 6; col++)
            {
                int x = 22 + col * 6, y = 6 + row * 7;
                PaintPixels(pixels, x, y, 5, 6, new Color32(29, 59, 92, 255));
                PaintPixels(pixels, x, y + 5, 5, 1, new Color32(52, 102, 143, 255));
                PaintPixels(pixels, x + 1, y + 1, 1, 4, new Color32(42, 77, 113, 255));
            }
            PaintPixels(pixels, 23, 2, 3, 2, steel);
            PaintPixels(pixels, 54, 2, 3, 2, steel);
            powerTexture = new Texture2D(64, 32, TextureFormat.RGBA32, false)
            { name = "DeadCircuit runtime power pixels", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            powerTexture.SetPixels32(pixels); powerTexture.Apply(false, true);
            var pole = Sprite.Create(powerTexture, new Rect(0, 0, 16, 32), new Vector2(.5f, .3f), 24, 0, SpriteMeshType.FullRect);
            var solar = Sprite.Create(powerTexture, new Rect(16, 0, 48, 32), new Vector2(.5f, .5f), 16, 0, SpriteMeshType.FullRect);
            pole.name = "runtime_powerpole"; solar.name = "runtime_solarpanel";
            sprites.Add(pole.name, pole); sprites.Add(solar.name, solar);
        }

        static void PaintPixels(Color32[] pixels, int x, int y, int w, int h, Color32 color)
        {
            for (int iy = y; iy < y + h; iy++) for (int ix = x; ix < x + w; ix++) pixels[ix + iy * 64] = color;
        }

        void SetupCamera()
        {
            WorldCamera = Camera.main;
            if (WorldCamera == null)
            {
                var obj = new GameObject("DeadCircuit / World Camera");
                obj.transform.SetParent(transform, false);
                WorldCamera = obj.AddComponent<Camera>();
                obj.tag = "MainCamera";
                ownsCamera = true;
            }
            WorldCamera.orthographic = true;
            WorldCamera.orthographicSize = 9.75f;
            WorldCamera.nearClipPlane = .1f;
            WorldCamera.farClipPlane = 150f;
            WorldCamera.clearFlags = CameraClearFlags.SolidColor;
            WorldCamera.backgroundColor = new Color(.035f, .065f, .069f);
            WorldCamera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
            WorldCamera.allowHDR = true;
            WorldCamera.allowMSAA = false;
            bool hasListener = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled) { hasListener = true; break; }
            if (!hasListener)
            {
                ownedAudioListener = new GameObject("DeadCircuit / Audio Listener");
                ownedAudioListener.transform.SetParent(WorldCamera.transform, false);
                ownedAudioListener.transform.localPosition = new Vector3(0, 0, 10);
                ownedAudioListener.AddComponent<AudioListener>();
            }
            var additional = WorldCamera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = true;
            additional.antialiasing = AntialiasingMode.None;
            additional.volumeLayerMask |= 1;
            additional.volumeTrigger = WorldCamera.transform;
            // No PixelPerfectCamera: the bridge owns continuous mouse-wheel zoom.
        }

        void SetupPostProcessing()
        {
            // The saved scene owns its persistent profile so URP build stripping sees it.
            var supplied = Resources.Load<VolumeProfile>("Art/Atmosphere");
            if (supplied != null)
            {
                foreach (var existing in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                    if (existing.isGlobal && existing.sharedProfile == supplied) return;
            }
            var obj = new GameObject("DeadCircuit / restrained atmosphere");
            obj.transform.SetParent(transform, false);
            obj.layer = 0;
            volume = obj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = -10f;
            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volumeProfile.name = "DeadCircuit runtime atmosphere";
            var color = volumeProfile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.06f);
            color.contrast.Override(3f);
            color.saturation.Override(-3f);
            var vignette = volumeProfile.Add<Vignette>(true);
            vignette.intensity.Override(.12f);
            vignette.smoothness.Override(.65f);
            vignette.color.Override(new Color(.035f, .065f, .065f));
            var bloom = volumeProfile.Add<Bloom>(true);
            bloom.intensity.Override(.10f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(.4f);
            volume.sharedProfile = volumeProfile;
        }

        void SetupMinimap()
        {
            minimapCanvas = new GameObject("DeadCircuit / Minimap", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            minimapCanvas.transform.SetParent(transform, false);
            var canvas = minimapCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4;
            var scaler = minimapCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var frame = UI("Map frame", minimapCanvas.transform, new Color(.035f, .075f, .075f, .97f));
            MinimapRect = frame.rectTransform;
            MinimapRect.anchorMin = MinimapRect.anchorMax = new Vector2(1, 1);
            MinimapRect.pivot = new Vector2(1, 1);
            // Keep orientation in the corner; the contextual inspector sits below.
            MinimapRect.anchoredPosition = new Vector2(-18, -18);
            MinimapRect.sizeDelta = new Vector2(144, 144);
            var mapObj = new GameObject("Terrain and signals", typeof(RectTransform), typeof(RawImage));
            mapObj.transform.SetParent(frame.transform, false);
            minimapImage = mapObj.GetComponent<RawImage>();
            var rect = minimapImage.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4, 4); rect.offsetMax = new Vector2(-4, -4);
            // AttachMinimapToHud makes this part of the HUD's own raycast surface.
            minimapImage.raycastTarget = true;
            var accent = UI("Map accent", frame.transform, new Color(.74f, .64f, .38f));
            accent.raycastTarget = false;
            accent.rectTransform.anchorMin = new Vector2(0, 1); accent.rectTransform.anchorMax = Vector2.one;
            accent.rectTransform.pivot = new Vector2(.5f, 1);
            accent.rectTransform.anchoredPosition = Vector2.zero;
            accent.rectTransform.sizeDelta = new Vector2(0, 2);
            hurtOverlay = UI("Damage pulse", minimapCanvas.transform, Color.clear);
            hurtOverlay.raycastTarget = false;
            hurtOverlay.rectTransform.anchorMin = Vector2.zero;
            hurtOverlay.rectTransform.anchorMax = Vector2.one;
            hurtOverlay.rectTransform.offsetMin = hurtOverlay.rectTransform.offsetMax = Vector2.zero;
            // Screen-space overlay UI is not processed by the world camera's Volume.
        }

        static Image UI(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>(); image.color = color; return image;
        }

        void LateUpdate()
        {
            if (!initialized || bridge == null || WorldCamera == null) return;
            var world = bridge.World;
            bool running = bridge.IsRunning && world != null && world.width > 0 && world.height > 0;
            if (running != wasRunning)
            {
                worldRoot.gameObject.SetActive(running);
                minimapCanvas.SetActive(running);
                MinimapRect.gameObject.SetActive(running);
                hurtOverlay.gameObject.SetActive(running);
                cameraPlaced = false;
                if (!running) { ResetEntities(); ResetGroundStreaming(); }
                wasRunning = running;
            }
            if (!running) return;
            AttachMinimapToHud();
            ObserveWorld(world);
            RebuildGround(world);
            stamp++;
            var local = bridge.LocalPlayer;
            FollowCamera(local);
            float halfHeight = WorldCamera.orthographicSize + 2.8f;
            float halfWidth = WorldCamera.orthographicSize * WorldCamera.aspect + 2.8f;
            Vector2 center = WorldCamera.transform.position;
            viewMin = center - new Vector2(halfWidth, halfHeight);
            viewMax = center + new Vector2(halfWidth, halfHeight);
            UpdateAmbient(world, local);
            DrawNaturalExteriors(world, local);
            DrawRooms(world);
            DrawSites(world);
            DrawNodes(world, local);
            DrawDroppedBackpacks(world);
            DrawBuildings(world);
            DrawNaturalEntranceGuides();
            DrawActors(world, local);
            DrawExpansion(world, local);
            Sweep(nodes); Sweep(droppedBackpacks); Sweep(buildings); Sweep(zombies);
            removePlayers.Clear();
            foreach (var pair in players) if (pair.Value.stamp != stamp) removePlayers.Add(pair.Key);
            foreach (var key in removePlayers) { Release(players[key]); players.Remove(key); }
            DrawTraces(world);
            DrawWeaponReticle(local);
            DrawPreview(world, local);
            DrawPowerLinks();
            DrawCargo(world);
            DrawParticles();
            if (Time.unscaledTime >= nextMapAt)
            {
                nextMapAt = Time.unscaledTime + .2f;
                UpdateMap(world, local);
            }
        }

        void ObserveWorld(WorldState world)
        {
            bool snapshot = !ReferenceEquals(observedWorld, world);
            bool reset = observedElapsed < 0 || world.elapsed < observedElapsed || world.tick < observedTick ||
                (observedWorld != null && (seed != world.seed || width != world.width || height != world.height));
            if (reset) { ResetEntities(); ResetGroundStreaming(); cameraPlaced = false; nextMapAt = 0; }
            // A LAN snapshot replaces WorldState too. Rebase its clock without treating
            // every ordinary snapshot as a new game or discarding in-flight visuals.
            if (reset || snapshot || observedElapsed != world.elapsed)
            { observedElapsed = world.elapsed; observedAt = Time.unscaledTime; }
            observedWorld = world; observedTick = world.tick;
            visualTime = world.elapsed + Mathf.Clamp(Time.unscaledTime - observedAt, 0, .2f);
        }

        void AttachMinimapToHud()
        {
            if (minimapAttached) return;
            // View.Init precedes Hud.Init. Adopt the HUD Canvas only after it exists.
            // Only our two graphics move; no HUD object or file is modified.
            foreach (var canvas in bridge.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.gameObject==minimapCanvas || canvas.renderMode!=RenderMode.ScreenSpaceOverlay || canvas.sortingOrder<=4) continue;
                MinimapRect.SetParent(canvas.transform,false);
                hurtOverlay.rectTransform.SetParent(canvas.transform,false);
                hurtOverlay.rectTransform.SetAsFirstSibling();
                MinimapRect.SetAsFirstSibling();
                minimapAttached=true;
                break;
            }
        }

        void FollowCamera(PlayerState local)
        {
            Vector2 target = local != null ? new Vector2(local.x, local.y) : new Vector2((width-1)*.5f, (height-1)*.5f);
            if (local != null) target += new Vector2(local.aimX, local.aimY) * .55f;
            if (!groundChunked)
            {
                float halfY = WorldCamera.orthographicSize;
                float halfX = halfY * WorldCamera.aspect;
                target.x = ClampCamera(target.x, width, halfX);
                target.y = ClampCamera(target.y, height, halfY);
            }
            var position = new Vector3(target.x, target.y, -10f);
            if (!cameraPlaced || Vector2.Distance(WorldCamera.transform.position, target) > 12f)
            { WorldCamera.transform.position = position; cameraVelocity = Vector3.zero; cameraPlaced = true; }
            else WorldCamera.transform.position = Vector3.SmoothDamp(WorldCamera.transform.position, position,
                ref cameraVelocity, .13f, 80f, Mathf.Min(Time.unscaledDeltaTime, .05f));
        }

        static float ClampCamera(float value, int extent, float half)
        {
            return extent <= half * 2f ? (extent - 1f) * .5f : Mathf.Clamp(value, half - .5f, extent - .5f - half);
        }

        void UpdateAmbient(WorldState world, PlayerState local)
        {
            float cycle = world.dayLength > 0 ? world.elapsed / world.dayLength : 0f;
            // Starts at readable morning; night never blacks out the survival space.
            float night = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Cos((cycle + .5f) * Mathf.PI * 2f) + .12f) * 1.5f));
            Color ambient = Color.Lerp(Color.white, new Color(.50f, .65f, .76f), night);
            ambient *= UpdateSeasonTint(world);
            if (world.survival != null && world.survival.enabled)
                ambient = Color.Lerp(ambient, ambient * new Color(.78f, .84f, .88f), world.survival.weather.intensity * .35f);
            spriteMaterial.SetColor("_Color", ambient);
            terrainMaterial.SetColor("_Color", ambient);
            if (spriteMeshMaterial!=null) spriteMeshMaterial.SetColor("_Color",ambient);
            if (expansionGroundMaterial != null) expansionGroundMaterial.SetColor("_Color", ambient);
            if (roomFloorMaterial != null) roomFloorMaterial.SetColor("_Color", ambient);
            lantern.enabled = local != null && local.hp > 0 && night > .03f;
            if (local != null) lantern.transform.position = new Vector3(local.x, local.y + .25f, 0);
            lantern.color = new Color(1f, .87f, .56f, night * .20f);
            damagePulse = Mathf.MoveTowards(damagePulse, 0, Time.unscaledDeltaTime * 1.8f);
            hurtOverlay.color = new Color(.72f, .16f, .08f, damagePulse * .16f);
        }

        int Ground(int x, int y)
        {
            char tile = WorldTiles.Ground(groundWorld, x, y);
            return tile >= '0' && tile <= '4' ? tile - '0' : -1;
        }

        static uint Hash(int x, int y, int s)
        {
            unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + s * 1274126177); h = (h ^ (h >> 13)) * 1274126177u; return h ^ (h >> 16); }
        }

        Rect TerrainUV(int tile)
        {
            const float inset = .25f;
            return new Rect(((tile % 8)*32 + inset)/256f, (256-(tile/8+1)*32+inset)/256f, (32-2*inset)/256f, (32-2*inset)/256f);
        }

        static void AddQuad(List<Vector3> v, List<Vector2> u, List<int> t, List<Color32> c,
            float x, float y, float w, float h, Rect tex, Color color)
        {
            int start = v.Count;
            v.Add(new Vector3(x,y,0)); v.Add(new Vector3(x+w,y,0));
            v.Add(new Vector3(x+w,y+h,0)); v.Add(new Vector3(x,y+h,0));
            u.Add(new Vector2(tex.xMin,tex.yMin)); u.Add(new Vector2(tex.xMax,tex.yMin));
            u.Add(new Vector2(tex.xMax,tex.yMax)); u.Add(new Vector2(tex.xMin,tex.yMax));
            t.Add(start); t.Add(start+2); t.Add(start+1); t.Add(start); t.Add(start+3); t.Add(start+2);
            for (int i=0;i<4;i++) c.Add(color);
        }

        static void AddSpriteQuad(List<Vector3> v, List<Vector2> u, List<int> t, List<Color32> c, Sprite s, Vector2 p, float scale, Color color)
        {
            if (s == null) return;
            var r = s.rect; float ppu = s.pixelsPerUnit;
            var tex = new Rect(r.x/s.texture.width,r.y/s.texture.height,r.width/s.texture.width,r.height/s.texture.height);
            AddQuad(v,u,t,c,p.x-s.pivot.x/ppu*scale,p.y-s.pivot.y/ppu*scale,r.width/ppu*scale,r.height/ppu*scale,tex,color);
        }

        bool Visible(float x, float y) { return x >= viewMin.x && x <= viewMax.x && y >= viewMin.y && y <= viewMax.y; }
        // Keep sorting local to the camera so far-away signed coordinates cannot
        // push actors behind terrain or exceed Unity's sorting-order range.
        int Order(float y) { return 6000 - Mathf.RoundToInt((y - WorldCamera.transform.position.y) * 80); }

        void DrawNodes(WorldState world, PlayerState local)
        {
            blockedNodes.Clear();
            groundPickupCells.Clear();
            if (world.nodes == null) return;
            foreach (var state in world.nodes)
            {
                if (state.hp <= 0 || state.stock <= 0) continue;
                bool pickup = DemoCatalog.IsGroundPickup(state.kind);
                long cell = WorldTiles.Key(state.x, state.y);
                if (pickup) groundPickupCells.Add(cell);
                else blockedNodes.Add(cell);
                if (!Visible(state.x,state.y) || NaturalInteriorHidden(state.x, state.y)) continue;
                var v = Get(nodes, state.id, new Vector2(state.x, state.y), state.hp, pickup ? 1f : state.kind == NodeKind.Wall ? 160f : 35f);
                if (pickup)
                {
                    DrawGroundPickup(v, state.kind, state.id);
                    continue;
                }
                string art;
                switch (state.kind)
                {
                    case NodeKind.Tree: art = "tree_" + state.id % 3; break;
                    case NodeKind.Ore: case NodeKind.CopperOre: case NodeKind.TinOre: case NodeKind.UraniumOre:
                    case NodeKind.Stone: case NodeKind.Clay: case NodeKind.Coal: case NodeKind.Limestone: case NodeKind.FireClay:
                    case NodeKind.Quartz: art = "node_ore"; break;
                    case NodeKind.Scrap: art = state.id % 5 == 0 ? "car" : "node_scrap"; break;
                    case NodeKind.Crate: art = "node_crate"; break;
                    case NodeKind.Oil: art = "node_ore"; break;
                    default: art = "node_wall"; break;
                }
                int biome = BiomeAt(world, state.x, state.y);
                bool dedicatedBiomeTree = state.kind == NodeKind.Tree && world.generatorVersion >= 8 && biome >= (int)BiomeKind.Swamp;
                Sprite nodeArt = state.kind == NodeKind.Wall ? ConnectedWall(state.x, state.y) :
                    dedicatedBiomeTree ? DemoArt.Get("biome_tree_" + biome) : DemoArt.Get("node_" + (int)state.kind);
                v.body.sprite = nodeArt ?? Art(art);
                if (nodeArt!=null)
                {
                    v.body.transform.localScale=Vector3.one*(state.kind==NodeKind.Tree ? 1.8f : 1.05f);
                    v.body.transform.localPosition=new Vector3(0,state.kind==NodeKind.Tree ? .6f : .18f,0);
                }
                float canopy = state.kind == NodeKind.Tree && local != null && Mathf.Abs(local.x-state.x)<.8f && local.y > state.y && local.y < state.y+1.8f ? .52f : 1f;
                Color mineral = state.kind == NodeKind.CopperOre ? new Color(1f,.62f,.35f) :
                    state.kind == NodeKind.TinOre ? new Color(.68f,.84f,1f) :
                    state.kind == NodeKind.UraniumOre ? new Color(.72f,1f,.35f) :
                    state.kind == NodeKind.Stone ? new Color(.75f,.79f,.75f) :
                    state.kind == NodeKind.Clay ? new Color(1f,.55f,.32f) :
                    state.kind == NodeKind.Coal ? new Color(.3f,.34f,.4f) :
                    state.kind == NodeKind.Limestone ? new Color(.92f,.9f,.72f) :
                    state.kind == NodeKind.FireClay ? new Color(.83f,.48f,.37f) :
                    state.kind == NodeKind.Quartz ? new Color(.96f,.86f,1f) :
                    state.kind == NodeKind.Oil ? new Color(.36f,.28f,.48f) : Color.white;
                if (state.kind == NodeKind.Oil)
                {
                    v.root.name = "油源 / Oil · 采油泵开采";
                    v.body.transform.localScale = new Vector3(1.3f, .65f, 1f);
                    v.body.transform.localPosition = new Vector3(0, .05f, 0);
                }
                if (state.kind == NodeKind.Stone || state.kind == NodeKind.Clay)
                    v.root.name = state.kind == NodeKind.Stone ? "石料堆 / Stone" : "黏土矿床 / Clay";
                if (state.kind == NodeKind.Coal || state.kind == NodeKind.Limestone || state.kind == NodeKind.FireClay)
                    v.root.name = (state.kind == NodeKind.Coal ? "煤" : state.kind == NodeKind.Limestone ? "石灰石" : "耐火黏土") + "矿床";
                if (state.kind == NodeKind.Quartz) v.root.name = "石英矿床";
                Color ecology = state.kind == NodeKind.Tree && world.generatorVersion >= 8 && !dedicatedBiomeTree ? BiomeTint(world,state.x,state.y) : Color.white;
                v.body.color = DamageColor(v,mineral * ecology); v.body.color = new Color(v.body.color.r,v.body.color.g,v.body.color.b,canopy);
                v.shadow.transform.localScale = state.kind == NodeKind.Tree ? new Vector3(1.4f,.9f,1) : Vector3.one;
                float sway = state.kind == NodeKind.Tree ? Mathf.Sin(Time.unscaledTime*.9f+state.id)*.8f : 0;
                v.body.transform.localRotation = Quaternion.Euler(0,0,sway);
                if (state.kind == NodeKind.Wall) ConfigureWall(v, world, state.x, state.y);
                Health(v,state.hp,Time.unscaledTime-v.damageAt < 2f,new Vector2(0,.95f));
            }
        }

        void DrawGroundPickup(Visual visual, NodeKind kind, int id)
        {
            (string sprite, Vector3 scale, Color tint, float angle) = kind switch
            {
                NodeKind.Branch => ("deco_6", new Vector3(.63f, .52f, 1), new Color(1f, .92f, .82f), 66f),
                NodeKind.LooseStone => ("node_1", new Vector3(.42f, .33f, 1), new Color(.84f, .92f, 1f), 0f),
                _ => ("crop_3_1", new Vector3(.59f, .48f, 1), new Color(1f, .96f, .76f), -28f)
            };
            visual.body.sprite = Art(sprite);
            visual.body.transform.localScale = scale;
            visual.body.transform.localPosition = new Vector3(0, .07f, 0);
            visual.body.transform.localRotation = Quaternion.Euler(0, 0, angle + (id % 5 - 2) * 4f);
            visual.body.color = tint;
            visual.shadow.transform.localScale = new Vector3(.46f, .55f, 1);
            visual.bar.enabled = visual.barBack.enabled = false;
        }

        void DrawDroppedBackpacks(WorldState world)
        {
            if (world.droppedBackpacks == null) return;
            foreach (var pack in world.droppedBackpacks)
            {
                if (pack?.gear == null || !EquipmentCatalog.IsBackpack(pack.gear.kind)) continue;
                // Placement previews share pickup cells; backpack visuals are keyed by gear ID.
                groundPickupCells.Add(WorldTiles.Key(pack.x, pack.y));
                if (!Visible(pack.x, pack.y) || NaturalInteriorHidden(pack.x, pack.y)) continue;

                var position = new Vector2(pack.x, pack.y);
                if (!droppedBackpacks.TryGetValue(pack.gear.id, out var visual))
                {
                    int tier = (int)pack.gear.kind - (int)GearKind.GrassBackpack;
                    if (tier < 0 || tier >= backpackSprites.Length) continue;
                    Sprite sprite = backpackSprites[tier];
                    if (sprite == null) continue;

                    visual = Acquire(position, 1f, 1f);
                    droppedBackpacks.Add(pack.gear.id, visual);
                    visual.body.sprite = sprite;
                    visual.body.color = sprite == backpackFallback ? BackpackFallbackTints[tier] : Color.white;
                    var bounds = sprite.bounds;
                    float scale = (.48f + tier * .07f) / Mathf.Max(bounds.size.x, bounds.size.y);
                    var bagScale = new Vector3(scale, scale * .84f, 1f);
                    visual.body.transform.localScale = bagScale;
                    visual.body.transform.localPosition = new Vector3(0f, .07f, 0f) - Vector3.Scale(bounds.center, bagScale);
                    visual.shadow.transform.localScale = new Vector3(.58f + tier * .06f, .56f, 1f);
                }
                // A drop always occupies its exact cell, including when a snapshot moves an existing gear ID.
                visual.stamp = stamp;
                visual.root.transform.position = position;
                visual.body.sortingOrder = Order(pack.y) - 1;
            }
        }

        void DrawBuildings(WorldState world)
        {
            occupied.Clear(); solidBuildings.Clear(); buildingStates.Clear(); powerNodes.Clear(); networks.Clear();
            if (world.powerNetworks != null) foreach (var network in world.powerNetworks) networks[network.id] = network;
            if (world.buildings == null) return;
            foreach (var state in world.buildings)
            {
                if (state.hp <= 0) continue;
                int w = DemoCatalog.Width(state), h = DemoCatalog.Height(state);
                for (int y = state.y; y < state.y + h; y++) for (int x = state.x; x < state.x + w; x++)
                {
                    if (!DemoCatalog.Contains(state, x, y)) continue;
                    long cell = WorldTiles.Key(x, y);
                    occupied.Add(cell);
                    if (!DemoCatalog.Walkable(state)) solidBuildings.Add(cell);
                }
                buildingStates[state.id] = state;
                if (DemoCatalog.IsPowerNode(state.kind)) powerNodes.Add(state);
                Vector2 center = BuildingCenter(state);
                if (center.x + w * .5f < viewMin.x || center.x - w * .5f > viewMax.x ||
                    center.y + h * .5f < viewMin.y || center.y - h * .5f > viewMax.y) continue;
                if (NaturalInteriorHidden(center.x, center.y) && !(state.kind == BuildKind.Door && NaturalEntrance(state.x, state.y))) continue;
                var v = Get(buildings, state.id, center, state.hp, WorldSimulator.BuildingMaxHealth(state.kind));
                // A machine is fixed geometry, including when its dimensions change on rotation.
                v.position = v.previous = center; v.root.transform.position = center;
                SetOrder(v, state.y);
                if (ModularMachineCatalog.IsModular(state.kind))
                {
                    DrawModularMachine(v, state);
                    continue;
                }
                int rotation = ((state.rotation % 4) + 4) % 4;
                ConfigureMachine(v.body, state);
                if (state.kind == BuildKind.Door)
                    v.body.sortingOrder = NaturalDoorOrder(state.x, state.y, v.body.sortingOrder);
                Color primitiveTint = state.kind == BuildKind.ResearchBench ? new Color(.72f,.84f,.98f) :
                    state.kind == BuildKind.CharcoalKiln ? new Color(.48f,.55f,.52f) :
                    state.kind == BuildKind.ClayKiln ? new Color(1f,.57f,.36f) : Color.white;
                v.body.color = DamageColor(v, primitiveTint);
                if (state.kind == BuildKind.CharcoalKiln || state.kind == BuildKind.ClayKiln)
                    v.root.name = DemoCatalog.BuildName(state.kind);
                if (state.kind == BuildKind.Wall || state.kind == BuildKind.Door)
                    ConfigureWall(v, world, state.x, state.y);
                v.shadow.transform.localPosition = new Vector3(0, -.20f * h, 0);
                v.shadow.transform.localScale = new Vector3(w * .94f, h * .72f, 1);
                bool needsPower = DemoCatalog.Demand(state.kind) > 0;
                bool node = DemoCatalog.IsPowerNode(state.kind);
                bool active = needsPower ? state.powerRatio > 0 : node ? state.powerGeneration > 0 : true;
                bool supplied = node ? NetworkEnergized(state.networkId) : needsPower && state.networkId > 0 && state.powerRatio > 0;
                v.lamp.enabled = node || needsPower;
                v.lamp.sprite = Art("pixel");
                v.lamp.transform.localPosition = new Vector3(-w * .5f + .22f, -h * .5f + .22f, 0);
                v.lamp.transform.localScale = new Vector3(.055f, .055f, 1);
                v.lamp.color = supplied ? Color.Lerp(new Color(.18f,.25f,.24f), NetworkColor(state.networkId),
                    node ? 1 : .4f + Mathf.Clamp01(state.powerRatio) * .6f) : new Color(.72f,.32f,.20f);
                var dir = new Vector2(DemoCatalog.DirectionX(rotation), DemoCatalog.DirectionY(rotation));
                v.arrow.enabled = HasOutput(state.kind);
                v.arrow.sprite = Art("arrow");
                v.arrow.transform.localPosition = (Vector3)(new Vector2(DemoCatalog.OutputX(state), DemoCatalog.OutputY(state)) - center - dir * .52f);
                v.arrow.transform.localScale = Vector3.one * .37f;
                v.arrow.transform.localRotation = Quaternion.Euler(0, 0, rotation * 90);
                v.arrow.color = active ? Amber : new Color(.61f,.58f,.43f);
                v.detail.enabled = state.kind == BuildKind.Generator || state.kind == BuildKind.Miner || state.kind == BuildKind.Turret ||
                    state.kind == BuildKind.Waterwheel || state.kind == BuildKind.MechanicalCrusher || state.kind == BuildKind.SteamTurbine || state.kind == BuildKind.SteamEngine;
                v.detail.transform.localPosition = Vector3.zero;
                v.detail.transform.localScale = Vector3.one * (.65f * Mathf.Min(w,h));
                v.detail.transform.localRotation = Quaternion.identity;
                if (state.kind == BuildKind.Turret)
                {
                    v.detail.sprite = Art("turret_barrel");
                    float angle = rotation * 90; float nearest = 100f;
                    if (world.zombies != null) foreach (var z in world.zombies)
                    {
                        float dx = z.x - center.x, dy = z.y - center.y, distance = dx * dx + dy * dy;
                        if (z.hp > 0 && distance < nearest) { nearest = distance; angle = Mathf.Atan2(dy,dx) * Mathf.Rad2Deg; }
                    }
                    v.detail.transform.localRotation = Quaternion.Euler(0,0,angle);
                    v.detail.transform.localScale = Vector3.one * .82f;
                }
                else
                {
                    v.detail.sprite = Art("rotor");
                    v.detail.transform.localPosition = new Vector3(state.kind == BuildKind.Generator ? -.08f * w : 0, -.05f * h, 0);
                    v.detail.transform.localRotation = Quaternion.Euler(0,0,active ? visualTime * 160f : 0);
                }
                // Stored stock is a stationary coloured dot. Only WorldState.cargo
                // may display a travelling item; removed stock is never drawn twice.
                int stock = -1;
                if (state.items != null) for (int i = 0; i < Mathf.Min(ItemColors.Length,state.items.Length); i++)
                    if (state.items[i] > 0) { stock = i; break; }
                v.cargo.enabled = stock >= 0 && state.kind != BuildKind.Wall && state.kind != BuildKind.Door;
                if (stock >= 0)
                {
                    v.cargo.sprite = Art("pixel"); v.cargo.color = ItemColors[stock];
                    v.cargo.transform.localPosition = new Vector3(w * .5f - .22f, -h * .5f + .22f, 0);
                    v.cargo.transform.localScale = new Vector3(.065f,.065f,1);
                }
                if ((active && (state.kind == BuildKind.Smelter || state.kind == BuildKind.StoneFurnace ||
                    state.kind == BuildKind.CharcoalKiln || state.kind == BuildKind.ClayKiln ||
                    state.kind == BuildKind.SteamFurnace || state.kind == BuildKind.Boiler)) || state.kind == BuildKind.Reactor)
                {
                    v.detail.enabled = true; v.detail.sprite = Art("glow"); v.detail.transform.localScale = Vector3.one * (.8f * Mathf.Min(w,h));
                    v.detail.transform.localRotation = Quaternion.identity;
                    v.detail.color = state.kind == BuildKind.Reactor ?
                        (state.tripped || state.temperature >= 750 ? new Color(1f,.28f,.1f,.6f) : new Color(.45f,1f,.42f,active ? .32f : .08f)) :
                        new Color(1f,.55f,.18f,(.40f + Mathf.Sin(visualTime * 7) * .07f) * Mathf.Clamp01(state.powerRatio));
                }
                else v.detail.color = Color.white;
                Health(v,state.hp,state.hp < v.maxHp - .01f,new Vector2(0,h * .5f + .16f),.58f + (w - 1) * .48f);
            }
        }

        static Vector2 BuildingCenter(BuildingState b) { return new Vector2(DemoCatalog.CenterX(b), DemoCatalog.CenterY(b)); }
        static bool HasOutput(BuildKind kind)
        { return kind != BuildKind.Wall && kind != BuildKind.Door && kind != BuildKind.Workbench && kind != BuildKind.ResearchBench &&
            kind != BuildKind.CombustionChamber && kind != BuildKind.Chimney && kind != BuildKind.FeedwaterPump &&
            (!DemoCatalog.IsPowerNode(kind) || kind == BuildKind.Reactor) && kind != BuildKind.Turret; }

        void ConfigureMachine(SpriteRenderer renderer, BuildingState b)
        {
            int rotation = ((b.rotation % 4) + 4) % 4;
            if (b.kind == BuildKind.Door) rotation = DoorVisualRotation(b, rotation);
            float w = DemoCatalog.Width(b), h = DemoCatalog.Height(b);
            Sprite svg = b.kind == BuildKind.Wall ? ConnectedWall(b.x, b.y) :
                b.kind == BuildKind.Door && b.setting == 1 ? DemoArt.Get("door_open") : DemoArt.Get("machine_" + (int)b.kind);
            // A missing open-door SVG must never fall back to a solid machine.
            renderer.sprite = svg != null || b.kind == BuildKind.Door ? svg : Machine(b.kind, rotation);
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = new Vector3(w,h,1);
            if (svg != null)
            {
                // SVG frames are all one world unit; rotate the unrotated footprint.
                renderer.transform.localRotation = Quaternion.Euler(0,0,b.kind == BuildKind.Wall ? 0 : rotation * 90);
                renderer.transform.localScale = b.kind == BuildKind.Wall || (rotation & 1) == 0 ? new Vector3(w,h,1) : new Vector3(h,w,1);
            }
            else if (b.kind == BuildKind.SolarPanel)
            {
                renderer.transform.localRotation = Quaternion.Euler(0,0,rotation * 90);
                renderer.transform.localScale = (rotation & 1) == 0 ? new Vector3(w / 3f,h / 2f,1) : new Vector3(h / 3f,w / 2f,1);
            }
            else if ((int)b.kind >= (int)BuildKind.Workbench)
            {
                renderer.transform.localRotation = Quaternion.Euler(0,0,rotation * 90);
                renderer.transform.localScale = (rotation & 1) == 0 ? new Vector3(w,h,1) : new Vector3(h,w,1);
            }
        }

        void DrawActors(WorldState world, PlayerState local)
        {
            if (world.players != null) foreach (var state in world.players)
            {
                if (!state.connected || !Visible(state.x,state.y) || NaturalInteriorHidden(state.x, state.y)) continue;
                Vector2 target=new Vector2(state.x,state.y);
                bool isLocal=local != null && state.id==local.id;
                Visual v;
                if (!players.TryGetValue(state.id,out v))
                {
                    v=Acquire(target,state.hp,WorldSimulator.PlayerMaxHealth(state)); players.Add(state.id,v);
                }
                Touch(v,target,state.hp,isLocal);
                v.maxHp=WorldSimulator.PlayerMaxHealth(state);
                Vector2 movement=target-v.previous;
                bool walking=movement.sqrMagnitude>.000002f;
                if (walking) v.walk += Time.unscaledDeltaTime*9f;
                int frame=walking ? Mathf.FloorToInt(v.walk)%4 : 0;
                int facing=Direction(new Vector2(state.aimX,state.aimY));
                bool modular = DrawPlayerAvatar(v, state, facing, walking);
                if (!modular)
                {
                    v.body.enabled = true;
                    Sprite survivorArt=DemoArt.Get("survivor_"+facing+"_"+frame);
                    v.body.sprite=survivorArt ?? Art("actor_0_"+facing+"_"+frame);
                    v.body.transform.localScale=survivorArt!=null ? Vector3.one*1.5f : Vector3.one;
                    v.body.transform.localPosition=new Vector3(0,(survivorArt!=null ? .42f : 0)+(walking ? Mathf.Sin(v.walk*Mathf.PI)*.018f : 0),0);
                    v.body.color=DamageColor(v, state.hp > 0 ? Color.white : new Color(.48f,.53f,.50f,.65f));
                }
                DrawPlayerEquipment(v, state);
                v.body.transform.localRotation=Quaternion.Euler(0,0,state.hp<=0 ? -80 : 0);
                Health(v,state.hp,state.hp>0,new Vector2(0,modular ? 1.27f : 1.04f));
                v.previous=Vector2.Lerp(v.previous,target,1-Mathf.Exp(-8f*Time.unscaledDeltaTime));
            }
            if (world.zombies != null) foreach (var state in world.zombies)
            {
                if (state.hp<=0 || !Visible(state.x,state.y) || NaturalInteriorHidden(state.x, state.y)) continue;
                var target=new Vector2(state.x,state.y);
                float baseHealth=EnemyCatalog.Stats(state.variant).hp;
                var v=Get(zombies,state.id,target,state.hp,Mathf.Max(baseHealth,state.hp));
                Vector2 movement=target-v.previous;
                if (movement.sqrMagnitude<.00005f && local!=null) movement=new Vector2(local.x,local.y)-target;
                bool walking=(target-v.previous).sqrMagnitude>.000002f;
                if (walking) v.walk+=Time.unscaledDeltaTime*(state.variant==1 ? 10f : 7f);
                int facing=Direction(movement);
                int frame=walking ? Mathf.FloorToInt(v.walk)%4 : 0;
                Sprite enemyArt = DemoArt.Get("enemy_"+state.variant);
                v.body.sprite=enemyArt != null ? enemyArt : Art("actor_"+(1+Mathf.Abs(state.variant)%2)+"_"+facing+"_"+frame);
                v.body.transform.localScale=Vector3.one*(state.variant==2 || state.variant==5 ? 1.25f : state.variant==3 ? .78f : 1.05f);
                v.body.transform.localPosition=new Vector3(0, enemyArt != null ? .35f : 0, 0);
                if (enemyArt != null) v.body.flipX=movement.x<0;
                v.body.transform.localRotation=Quaternion.Euler(0,0,walking ? Mathf.Sin(v.walk*Mathf.PI*.5f)*3f : 0);
                v.body.color=DamageColor(v,Color.white);
                DrawEnemyAura(v,state);
                Health(v,state.hp,state.hp<v.maxHp || Time.unscaledTime-v.damageAt<3f,new Vector2(0,1.04f));
                v.previous=Vector2.Lerp(v.previous,target,1-Mathf.Exp(-8f*Time.unscaledDeltaTime));
            }
            selectionRing.enabled=local!=null && local.hp>0;
            // Keep the local survivor locatable behind a tall exterior without
            // removing its roof or revealing any rooms from outside.
            selectionRing.sortingOrder = local != null && NaturalExteriorOccludes(local.x, local.y) ? 9600 : -11000;
            if (local!=null)
            {
                Visual v;
                Vector2 p=players.TryGetValue(local.id,out v) ? v.position : new Vector2(local.x,local.y);
                selectionRing.transform.position=p;
            }
        }

        static int Direction(Vector2 direction)
        {
            if (Mathf.Abs(direction.x)>Mathf.Abs(direction.y)) return direction.x<0 ? 2 : 0;
            return direction.y>0 ? 1 : 3;
        }

        Visual Get(Dictionary<int,Visual> dict,int id,Vector2 p,float hp,float maxHp)
        {
            Visual v;
            if (!dict.TryGetValue(id,out v)) { v=Acquire(p,hp,maxHp); dict.Add(id,v); }
            Touch(v,p,hp,false); return v;
        }

        Visual Acquire(Vector2 p,float hp,float maxHp)
        {
            Visual v;
            if (pool.Count>0) { v=pool.Pop(); v.root.SetActive(true); }
            else
            {
                v=new Visual(); v.root=NewRoot("Entity",entityRoot).gameObject;
                v.shadow=NewSprite("Contact shadow",v.root.transform,-12000); v.shadow.sprite=Art("shadow");
                v.body=NewSprite("Body",v.root.transform,0);
                v.detail=NewSprite("Mechanism",v.root.transform,1);
                v.arrow=NewSprite("Output direction",v.root.transform,2);
                v.lamp=NewSprite("Power status",v.root.transform,3);
                v.cargo=NewSprite("Stored inventory dot",v.root.transform,4);
                v.barBack=NewSprite("Health background",v.root.transform,10); v.barBack.sprite=Art("pixel");
                v.bar=NewSprite("Health",v.root.transform,11); v.bar.sprite=Art("pixel");
            }
            v.position=v.previous=p; v.root.transform.position=p; v.hp=hp; v.maxHp=maxHp;
            v.damageAt=-100; v.walk=0; v.nextSpecialEffect=0;
            v.body.color=v.detail.color=Color.white;
            v.body.enabled=true;
            v.body.transform.localScale=Vector3.one;
            v.body.transform.localPosition=Vector3.zero;
            v.body.transform.localRotation=Quaternion.identity;
            v.body.flipX=false;
            if (v.equipment != null) v.equipment.SetVisible(false);
            if (v.avatar != null) v.avatar.root.gameObject.SetActive(false);
            if (v.modules != null) v.modules.root.gameObject.SetActive(false);
            v.detail.enabled=v.arrow.enabled=v.lamp.enabled=v.cargo.enabled=v.bar.enabled=v.barBack.enabled=false;
            v.detail.transform.localPosition=Vector3.zero; v.detail.transform.localScale=Vector3.one; v.detail.transform.localRotation=Quaternion.identity;
            v.shadow.enabled=true;
            v.shadow.transform.localScale=Vector3.one;
            v.shadow.transform.localPosition=Vector3.zero;
            return v;
        }

        void Touch(Visual v,Vector2 p,float hp,bool isLocal)
        {
            if (hp<v.hp-.01f)
            {
                v.damageAt=Time.unscaledTime;
                Spawn("hit",p+Vector2.up*.45f,new Color(1,.8f,.48f),.22f,.22f,Vector2.up*.35f);
                Spawn("blood",p,new Color(.9f,.7f,.6f,.8f),1.2f,.45f,Vector2.zero);
                if (isLocal) damagePulse=1;
            }
            v.hp=hp; v.maxHp=Mathf.Max(v.maxHp,hp); v.stamp=stamp;
            float smoothing=1-Mathf.Exp(-(isLocal ? 24f : 16f)*Time.unscaledDeltaTime);
            v.position=Vector2.Distance(v.position,p)>4 ? p : Vector2.Lerp(v.position,p,smoothing);
            v.root.transform.position=v.position;
            SetOrder(v,v.position.y);
        }

        void SetOrder(Visual v,float y)
        {
            int order=Order(y);
            v.body.sortingOrder=order; v.detail.sortingOrder=order+1; v.arrow.sortingOrder=order+2;
            v.lamp.sortingOrder=order+3; v.cargo.sortingOrder=order+4;
            v.barBack.sortingOrder=order+10; v.bar.sortingOrder=order+11;
        }

        static Color DamageColor(Visual v,Color normal)
        {
            return Time.unscaledTime-v.damageAt<.14f ? new Color(1f,.40f,.25f,normal.a) : normal;
        }

        void Health(Visual v,float hp,bool show,Vector2 offset,float span=.58f)
        {
            v.bar.enabled=v.barBack.enabled=show && hp>0;
            if (!show || hp<=0) return;
            float ratio=Mathf.Clamp01(hp/Mathf.Max(1,v.maxHp));
            v.barBack.transform.localPosition=offset;
            v.barBack.transform.localScale=new Vector3(span,.055f,1);
            v.barBack.color=new Color(.05f,.10f,.10f,.94f);
            v.bar.transform.localPosition=(Vector3)offset+new Vector3((ratio-1)*(span-.03f)*2f/3f,0,0);
            v.bar.transform.localScale=new Vector3((span-.03f)*ratio,.027f,1);
            v.bar.color=ratio<.35f ? Danger : Mint;
        }

        void Sweep(Dictionary<int,Visual> dict)
        {
            removeIds.Clear();
            foreach (var pair in dict) if (pair.Value.stamp!=stamp) removeIds.Add(pair.Key);
            foreach (int key in removeIds) { Release(dict[key]); dict.Remove(key); }
        }

        void Release(Visual v) { v.root.SetActive(false); pool.Push(v); }

        void ResetEntities()
        {
            ResetRooms();
            ResetExpansion();
            foreach (var v in nodes.Values) Release(v); nodes.Clear();
            foreach (var v in droppedBackpacks.Values) Release(v); droppedBackpacks.Clear();
            if (backpackHud != null) backpackHud.ShowDroppedBackpackHint(null, Vector2.zero);
            foreach (var v in buildings.Values) Release(v); buildings.Clear();
            foreach (var v in zombies.Values) Release(v); zombies.Clear();
            foreach (var v in players.Values) Release(v); players.Clear();
            foreach (var t in traces) { t.line.enabled=t.core.enabled=t.impact.enabled=false; t.until=-1; }
            foreach (var r in ranges) r.enabled=false;
            foreach (var line in wires) line.enabled=false;
            foreach (var line in previewWires) line.enabled=false;
            foreach (var v in cargoVisuals.Values) { v.sprite.enabled=false; cargoPool.Push(v); }
            cargoVisuals.Clear(); liveCargoIds.Clear();
            buildingStates.Clear(); networks.Clear(); powerNodes.Clear();
            occupied.Clear(); solidBuildings.Clear(); blockedNodes.Clear(); groundPickupCells.Clear();
            powerLinks.Clear(); powerTopology=0; focusedNetwork=0;
            observedWorld=null; observedTick=-1; observedElapsed=-1; observedAt=0; visualTime=0;
            ghost.enabled=ghostArrow.enabled=footprint.enabled=ghostRange.enabled=ghostLinkRange.enabled=hoverFootprint.enabled=hoverRing.enabled=false;
            HideModularPreview();
            foreach (var p in particles) p.sprite.enabled=false;
            ResetWeaponEffects();
            foreach (var line in weaponReticle) if (line != null) line.enabled=false;
            damagePulse=0;
        }

        bool NetworkEnergized(int id)
        {
            PowerNetworkState network;
            return id > 0 && networks.TryGetValue(id,out network) && network.generation > 0f;
        }

        Color NetworkColor(int id)
        {
            PowerNetworkState network;
            if(id<=0 || !networks.TryGetValue(id,out network)) return Amber;
            Color tier=network.tier==0 ? new Color(.72f,.52f,.29f) : network.tier==1 ? Amber :
                network.tier==2 ? Mint : network.tier==3 ? new Color(.45f,.73f,.96f) : new Color(.78f,.61f,.96f);
            return Color.Lerp(NetworkColors[(id-1)%NetworkColors.Length],tier,.8f);
        }

        static bool ConnectsVisually(BuildingState a,BuildingState b)
        {
            if(TechCatalog.EnergyTier(a)!=TechCatalog.EnergyTier(b)) return false;
            float range=TechCatalog.LinkRange(a.kind);
            return range<=0 ? TouchingFootprints(a,b) :
                (BuildingCenter(a)-BuildingCenter(b)).sqrMagnitude<=range*range+.0001f;
        }
        static bool TouchingFootprints(BuildingState a,BuildingState b)
        {
            int aw=DemoCatalog.Width(a),ah=DemoCatalog.Height(a),bw=DemoCatalog.Width(b),bh=DemoCatalog.Height(b);
            return ((a.x+aw==b.x || b.x+bw==a.x) && a.y<b.y+bh && b.y<a.y+ah) ||
                ((a.y+ah==b.y || b.y+bh==a.y) && a.x<b.x+bw && b.x<a.x+aw);
        }

        void DrawPowerLinks()
        {
            ulong topology = 1469598103934665603UL;
            unchecked
            {
                foreach (var b in powerNodes)
                {
                    topology = (topology ^ (uint)b.id) * 1099511628211UL;
                    topology = (topology ^ (uint)b.networkId) * 1099511628211UL;
                    topology = (topology ^ (uint)b.x) * 1099511628211UL;
                    topology = (topology ^ (uint)b.y) * 1099511628211UL;
                    topology = (topology ^ (uint)((int)b.kind + b.rotation * 32 + b.footprintVersion * 128 + b.setting * 512)) * 1099511628211UL;
                }
            }
            if (topology != powerTopology)
            {
                powerTopology = topology; powerLinks.Clear(); linkedNodes.Clear();
                // Breadth-first spanning forest: one visible wire per discovered node,
                // never a decorative edge between separate authoritative networks.
                for (int root = 0; root < powerNodes.Count; root++)
                {
                    var first = powerNodes[root];
                    if (first.networkId <= 0 || !linkedNodes.Add(first.id)) continue;
                    linkQueue.Clear(); linkQueue.Add(root);
                    for (int q = 0; q < linkQueue.Count; q++)
                    {
                        var from = powerNodes[linkQueue[q]];
                        for (int i = 0; i < powerNodes.Count; i++)
                        {
                            var to = powerNodes[i];
                            if (to.networkId != from.networkId || linkedNodes.Contains(to.id) || !ConnectsVisually(from,to)) continue;
                            linkedNodes.Add(to.id); linkQueue.Add(i); powerLinks.Add(new PowerLink(from.id,to.id));
                        }
                    }
                }
            }
            int count = 0;
            foreach (var link in powerLinks)
            {
                BuildingState from, to;
                if (!buildingStates.TryGetValue(link.fromId,out from) || !buildingStates.TryGetValue(link.toId,out to)) continue;
                Vector2 a = WireAnchor(from), b = WireAnchor(to);
                if (NaturalInteriorHidden(from.x, from.y) || NaturalInteriorHidden(to.x, to.y)) continue;
                if (!SegmentVisible(a,b)) continue;
                if (count == wires.Count) wires.Add(NewLine("Power network wire",9100,.018f,false));
                bool focused = focusedNetwork > 0 && from.networkId == focusedNetwork;
                Color color = NetworkEnergized(from.networkId) ? Color.Lerp(new Color(.10f,.15f,.16f),NetworkColor(from.networkId),focused ? .90f : .32f) :
                    new Color(.35f,.25f,.19f);
                color.a = focused ? .90f : .57f;
                var line = wires[count++];
                bool physical=TechCatalog.EnergyTier(from)<2;
                line.startWidth=line.endWidth=physical ? .075f : focused ? .027f : .018f;
                if(physical)
                {
                    line.enabled=true; line.positionCount=2; line.SetPosition(0,BuildingCenter(from)); line.SetPosition(1,BuildingCenter(to));
                    line.startColor=line.endColor=color;
                }
                else SagWire(line,a,b,color);
            }
            for (int i = count; i < wires.Count; i++) wires[i].enabled = false;
        }

        static Vector2 WireAnchor(BuildingState b)
        {
            return BuildingCenter(b) + Vector2.up * (b.kind == BuildKind.PowerPole || b.kind == BuildKind.MediumPole || b.kind == BuildKind.HighPole ? .75f : DemoCatalog.Height(b) * .22f);
        }

        bool SegmentVisible(Vector2 a, Vector2 b)
        {
            return Mathf.Max(a.x,b.x) >= viewMin.x && Mathf.Min(a.x,b.x) <= viewMax.x &&
                Mathf.Max(a.y,b.y) >= viewMin.y && Mathf.Min(a.y,b.y) - .25f <= viewMax.y;
        }

        void SagWire(LineRenderer line, Vector2 a, Vector2 b, Color color)
        {
            float sag = Mathf.Min(.24f,Vector2.Distance(a,b) * .045f);
            for (int i = 0; i < wireVertices.Length; i++)
            {
                float t = (float)i / (wireVertices.Length - 1);
                Vector2 p = Vector2.Lerp(a,b,t) - Vector2.up * (4f * sag * t * (1f - t));
                wireVertices[i] = new Vector3(p.x,p.y,0);
            }
            line.enabled = true; line.positionCount = wireVertices.Length; line.SetPositions(wireVertices);
            line.startColor = line.endColor = color;
        }

        void DrawCargo(WorldState world)
        {
            int count = 0;
            liveCargoIds.Clear();
            if (world.cargo != null) foreach (var state in world.cargo)
            {
                if (count++ >= WorldSimulator.MaxCargo) break;
                if (state != null) liveCargoIds.Add(state.id);
            }
            // Recycle arrivals before acquiring new trips, keeping the pool bounded
            // even when every cargo ID changes in the same snapshot.
            removeIds.Clear();
            foreach (var pair in cargoVisuals) if (!liveCargoIds.Contains(pair.Key)) removeIds.Add(pair.Key);
            foreach (int id in removeIds) ReleaseCargo(id);
            count = 0;
            float extrapolation = Mathf.Clamp(Time.unscaledTime - observedAt,0,.2f);
            if (world.cargo != null) foreach (var state in world.cargo)
            {
                if (count++ >= WorldSimulator.MaxCargo) break;
                if (state == null || state.item < 0 || state.item >= ItemColors.Length || !Finite(state.duration) || state.duration <= 0 ||
                    !Finite(state.progress) || !Finite(state.fromX) || !Finite(state.fromY) || !Finite(state.toX) || !Finite(state.toY)) continue;
                Vector2 from = new Vector2(state.fromX,state.fromY), to = new Vector2(state.toX,state.toY);
                float progress = Mathf.Clamp01(state.progress + extrapolation / state.duration);
                CargoVisual v;
                bool exists = cargoVisuals.TryGetValue(state.id,out v);
                bool sameTrip = exists && v.fromId == state.fromId && v.toId == state.toId && v.item == state.item && v.from == from && v.to == to;
                // Small snapshot timing corrections may hold the icon briefly, never pull it backwards.
                if (sameTrip) progress = Mathf.Max(v.progress,progress);
                Vector2 position = Vector2.Lerp(from,to,progress);
                if (!Visible(position.x,position.y) || NaturalInteriorHidden(position.x, position.y)) continue;
                if (!exists)
                {
                    v = cargoPool.Count > 0 ? cargoPool.Pop() : new CargoVisual { sprite = NewSprite("In-flight item",effectsRoot,9400) };
                    cargoVisuals.Add(state.id,v);
                }
                v.fromId = state.fromId; v.toId = state.toId; v.item = state.item; v.from = from; v.to = to;
                v.progress = progress; v.stamp = stamp;
                v.sprite.enabled = true; v.sprite.sprite = itemSprites[state.item]; v.sprite.color = Color.white;
                v.sprite.transform.position = new Vector3(position.x,position.y,0);
                v.sprite.transform.localScale = Vector3.one * .78f;
                v.sprite.transform.localRotation = Quaternion.identity;
            }
            removeIds.Clear();
            foreach (var pair in cargoVisuals) if (pair.Value.stamp != stamp) removeIds.Add(pair.Key);
            foreach (int id in removeIds) ReleaseCargo(id);
        }

        void ReleaseCargo(int id)
        {
            var v = cargoVisuals[id]; v.sprite.enabled = false; cargoPool.Push(v); cargoVisuals.Remove(id);
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        void DrawTraces(WorldState world)
        {
            int count=0;
            if (world.traces!=null) foreach (var t in world.traces)
            {
                if (t.until<visualTime || count>=64 || t.effect==2) continue;
                if (NaturalInteriorHidden(t.x, t.y) || NaturalInteriorHidden(t.endX, t.endY)) continue;
                if (!Visible(t.x,t.y) && !Visible(t.endX,t.endY)) continue;
                TraceVisual v;
                if (count==traces.Count)
                {
                    v=new TraceVisual { line=NewLine("Weapon trace",9500,.035f,false),
                        core=NewLine("Tracer bright core",9501,.01f,false), impact=NewSprite("Impact spark",effectsRoot,9502) };
                    v.line.positionCount=v.core.positionCount=2;
                    v.impact.sharedMaterial=effectMaterial; traces.Add(v);
                }
                else v=traces[count];
                float alpha=Mathf.Clamp01((t.until-visualTime)/.12f);
                Color color=t.effect==3 || t.effect==5 ? new Color(.65f,.90f,.3f) : t.effect==6 ? new Color(.55f,.84f,1) : t.effect==8 ? new Color(.79f,.56f,.93f) : t.hostile ? Danger : new Color(1f,.88f,.53f); color.a=alpha;
                bool shot=t.effect==1 && !t.hostile;
                if (shot && (v.until!=t.until || v.x!=t.x || v.y!=t.y || v.endX!=t.endX || v.endY!=t.endY))
                {
                    v.until=t.until; v.x=t.x; v.y=t.y; v.endX=t.endX; v.endY=t.endY;
                    RegisterShotTrace(world,t,v);
                }
                Vector2 from=shot ? v.shotFrom : new Vector2(t.x,t.y);
                Vector2 to=shot ? v.shotTo : new Vector2(t.endX,t.endY);
                bool drawLine=(t.effect<4 || t.effect==6) && (!shot || Vector2.Dot(to-from,new Vector2(t.endX-t.x,t.endY-t.y))>0);
                v.line.enabled=drawLine; v.line.startWidth=v.line.endWidth=shot ? .027f : .035f;
                v.line.startColor=color; v.line.endColor=new Color(color.r,color.g,color.b,alpha*.35f);
                v.line.SetPosition(0,from); v.line.SetPosition(1,to);
                v.core.enabled=shot && drawLine;
                if (v.core.enabled)
                {
                    v.core.startColor=new Color(1f,1f,.88f,alpha); v.core.endColor=new Color(1f,.94f,.68f,alpha*.45f);
                    v.core.SetPosition(0,from); v.core.SetPosition(1,to);
                }
                v.impact.enabled=shot ? v.shotImpact : t.effect<4 || t.effect==6;
                int frame=Mathf.Clamp(Mathf.FloorToInt((.15f-(t.until-visualTime))/.05f),0,2);
                v.impact.sprite=shot ? DemoArt.Get("fx_weapon_impact_"+frame) ?? Art("hit") : Art("hit");
                v.impact.transform.localScale=Vector3.one*(shot ? .48f : .12f);
                v.impact.color=shot ? new Color(1f,1f,1f,alpha) : color; v.impact.transform.position=to;
                count++;
            }
            for (int i=count;i<traces.Count;i++) { traces[i].line.enabled=traces[i].core.enabled=traces[i].impact.enabled=false; traces[i].until=-1; }
        }

        void RegisterShotTrace(WorldState world,TraceState trace,TraceVisual visual)
        {
            Vector2 from=new Vector2(trace.x,trace.y), end=new Vector2(trace.endX,trace.endY);
            visual.shotFrom=from+Vector2.up*.35f; visual.shotTo=end+Vector2.up*.35f;
            float range=8.5f, nearest=.64f; // Unmatched friendly shots belong to turrets.
            foreach (var player in world.players)
            {
                float distance=(new Vector2(player.x,player.y)-from).sqrMagnitude;
                if (distance>nearest) continue;
                var gear=EquipmentCatalog.Equipped(player);
                Visual actor;
                if (gear==null || !EquipmentCatalog.IsGun(gear.kind) || !players.TryGetValue(player.id,out actor) ||
                    actor.equipment==null || !actor.equipment.primed || actor.equipment.gearId!=gear.id) continue;
                nearest=distance; range=EquipmentCatalog.Stats(gear).range+player.attributes[3]*.35f;
                visual.shotFrom=actor.equipment.muzzleWorld;
            }
            // The authority clips a ray at a wall or its final target. A full-range
            // miss has no surface to spark against; do not invent an impact there.
            visual.shotImpact=Vector2.Distance(from,end)<range-.06f;
        }

        void DrawPreview(WorldState world,PlayerState local)
        {
            Vector2 mouse = bridge.MouseWorld;
            int x = Mathf.FloorToInt(mouse.x + .5f), y = Mathf.FloorToInt(mouse.y + .5f);
            bool ui = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool selected = bridge.BuildSelection.HasValue && local != null && local.hp > 0 && !ui;
            if (!selected || !ModularMachineCatalog.IsModular(bridge.BuildSelection.Value)) HideModularPreview();
            ghost.enabled = ghostArrow.enabled = footprint.enabled = selected;
            ghostRange.enabled = ghostLinkRange.enabled = hoverFootprint.enabled = hoverRing.enabled = false;
            focusedNetwork = 0;
            BuildingState hovered = null;
            if (!ui) foreach (var b in buildingStates.Values)
                if (DemoCatalog.Contains(b,x,y) && !NaturalInteriorHidden(DemoCatalog.CenterX(b), DemoCatalog.CenterY(b)))
                { hovered = b; break; }
            int rangeCount = 0, wireCount = 0;
            if (selected && ModularMachineCatalog.IsModular(bridge.BuildSelection.Value))
            {
                ghost.enabled = ghostArrow.enabled = footprint.enabled = false;
                DrawModularPreview(world, local, bridge.BuildSelection.Value, x, y);
            }
            else if (selected)
            {
                var kind = bridge.BuildSelection.Value;
                int rotation = ((bridge.BuildRotation % 4) + 4) % 4;
                previewBuilding.kind = kind; previewBuilding.x = x; previewBuilding.y = y; previewBuilding.rotation = rotation;
                bool valid = CanPreview(world,local,previewBuilding);
                Color color = valid ? Mint : Danger;
                ConfigureMachine(ghost,previewBuilding);
                ghost.color = new Color(color.r,color.g,color.b,.62f);
                ghost.transform.position = BuildingCenter(previewBuilding);
                var dir = new Vector2(DemoCatalog.DirectionX(rotation),DemoCatalog.DirectionY(rotation));
                ghostArrow.transform.position = (Vector3)(new Vector2(DemoCatalog.OutputX(previewBuilding),DemoCatalog.OutputY(previewBuilding)) - dir * .52f);
                ghostArrow.transform.localRotation = Quaternion.Euler(0,0,rotation * 90); ghostArrow.color = color;
                ghostArrow.enabled = HasOutput(kind);
                Outline(footprint,previewBuilding,color);
                if (DemoCatalog.IsPowerNode(kind))
                {
                    SupplyOutline(ghostRange,previewBuilding,new Color(color.r,color.g,color.b,.42f));
                    float linkRange=TechCatalog.LinkRange(kind);
                    if(linkRange>0) Circle(ghostLinkRange,BuildingCenter(previewBuilding),linkRange,new Color(color.r,color.g,color.b,.18f));
                    foreach (var node in powerNodes)
                    {
                        if (!ConnectsVisually(node,previewBuilding) || wireCount >= 16) continue;
                        if (!SegmentVisible(WireAnchor(previewBuilding),WireAnchor(node))) continue;
                        if (wireCount == previewWires.Count) previewWires.Add(NewLine("Possible node connection",9600,.027f,false));
                        Color linkColor = valid ? NetworkColor(node.networkId) : Danger; linkColor.a = .72f;
                        SagWire(previewWires[wireCount++],WireAnchor(previewBuilding),WireAnchor(node),linkColor);
                    }
                }
                else if (DemoCatalog.Demand(kind) > 0)
                {
                    var node = NearestSupplyNode(previewBuilding,0);
                    if (node != null) { focusedNetwork = node.networkId; ShowSupply(node,ref rangeCount); }
                }
            }
            else if (hovered != null)
            {
                focusedNetwork = hovered.networkId;
                Color color = hovered.networkId > 0 ? NetworkColor(hovered.networkId) : Amber; color.a = .85f;
                if (!ModularMachineCatalog.IsModular(hovered.kind)) Outline(hoverFootprint,hovered,color);
                var node = DemoCatalog.IsPowerNode(hovered.kind) ? hovered :
                    DemoCatalog.Demand(hovered.kind) > 0 && hovered.networkId > 0 ? NearestSupplyNode(hovered,hovered.networkId) : null;
                if (node != null && !ModularMachineCatalog.IsModular(hovered.kind)) ShowSupply(node,ref rangeCount);
            }
            for (int i = rangeCount; i < ranges.Count; i++) ranges[i].enabled = false;
            for (int i = wireCount; i < previewWires.Count; i++) previewWires[i].enabled = false;
            bool backpackHovered = DrawBackpackHover(world, local, x, y);
            if (!backpackHovered && !ui && !selected && hovered == null && world.nodes != null) foreach (var n in world.nodes)
                if (n.x == x && n.y == y && n.hp > 0 && n.stock > 0)
                {
                    hoverRing.enabled = true;
                    hoverRing.color = new Color(1f, .79f, .45f, .7f);
                    hoverRing.transform.localScale = new Vector3(.77f, .46f, 1f);
                    hoverRing.transform.position = new Vector3(x, y, 0);
                    break;
                }
        }

        bool DrawBackpackHover(WorldState world, PlayerState local, int x, int y)
        {
            GearState gear = null;
            var mouse = Mouse.current;
            Vector2 pointerPosition = mouse?.position.ReadValue() ?? Vector2.zero;
            var keyboard = Keyboard.current;
            bool aiming = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            if (droppedBackpacks.Count > 0 && local != null && local.hp > 0 && !bridge.BuildSelection.HasValue &&
                !aiming && mouse != null && WorldCamera.pixelRect.Contains(pointerPosition) && !bridge.WorldInputBlocked)
            {
                foreach (var pack in world.droppedBackpacks)
                {
                    if (pack?.gear == null || pack.x != x || pack.y != y || !droppedBackpacks.ContainsKey(pack.gear.id)) continue;
                    gear = pack.gear;
                    break;
                }
            }
            if (gear != null)
            {
                hoverRing.enabled = true;
                hoverRing.color = Amber;
                hoverRing.transform.localScale = new Vector3(.67f, .34f, 1f);
                hoverRing.transform.position = new Vector3(x, y - .10f, 0f);
                if (backpackHud == null) TryGetComponent(out backpackHud);
            }
            if (backpackHud != null) backpackHud.ShowDroppedBackpackHint(gear, pointerPosition);
            return gear != null;
        }

        BuildingState NearestSupplyNode(BuildingState consumer,int networkId)
        {
            BuildingState nearest = null; float radius=TechCatalog.SupplyRadius(consumer.kind); float distance=radius*radius;
            foreach (var node in powerNodes)
            {
                if (TechCatalog.EnergyTier(node) != TechCatalog.EnergyTier(consumer)) continue;
                if (ModularMachineCatalog.IsModular(consumer.kind) && ModularMachinePresentation.FindRole(consumer, MachineModuleRole.Energy) < 0) continue;
                if (ModularMachineCatalog.IsModular(consumer.kind) && !DemoCatalog.Contains(node,
                    ModularMachineCatalog.EnergyInputX(consumer), ModularMachineCatalog.EnergyInputY(consumer))) continue;
                if (networkId > 0 && node.networkId != networkId) continue;
                if (radius<=0 && !TouchingFootprints(consumer,node)) continue;
                float d = FootprintDistanceSquared(consumer,node);
                if (d > distance || (d == distance && nearest != null && node.id >= nearest.id)) continue;
                nearest = node; distance = d;
            }
            return nearest;
        }

        static float FootprintDistanceSquared(BuildingState a,BuildingState b)
        {
            float dx = Mathf.Max(0,Mathf.Max(a.x - b.x - DemoCatalog.Width(b),b.x - a.x - DemoCatalog.Width(a)));
            float dy = Mathf.Max(0,Mathf.Max(a.y - b.y - DemoCatalog.Height(b),b.y - a.y - DemoCatalog.Height(a)));
            return dx * dx + dy * dy;
        }

        void ShowSupply(BuildingState node,ref int count)
        {
            if (count == ranges.Count) ranges.Add(NewLine("Node footprint supply reach",-10600,.025f,true));
            Color color = NetworkEnergized(node.networkId) ? NetworkColor(node.networkId) : Amber; color.a = .34f;
            SupplyOutline(ranges[count++],node,color);
        }

        void Outline(LineRenderer line,BuildingState b,Color color)
        {
            float left = b.x - .48f, bottom = b.y - .48f;
            float right = b.x + DemoCatalog.Width(b) - .52f, top = b.y + DemoCatalog.Height(b) - .52f;
            footprintVertices[0] = new Vector3(left,bottom,0); footprintVertices[1] = new Vector3(right,bottom,0);
            footprintVertices[2] = new Vector3(right,top,0); footprintVertices[3] = new Vector3(left,top,0);
            footprintVertices[4] = footprintVertices[0];
            line.enabled = true; line.positionCount = footprintVertices.Length; line.SetPositions(footprintVertices);
            line.startColor = line.endColor = color;
        }

        void SupplyOutline(LineRenderer line,BuildingState node,Color color)
        {
            Vector2 center = BuildingCenter(node);
            float halfW = DemoCatalog.Width(node) * .5f, halfH = DemoCatalog.Height(node) * .5f;
            int arc = circleVertices.Length / 4;
            for (int i = 0; i < circleVertices.Length; i++)
            {
                int corner = i / arc;
                float angle = (corner + (float)(i % arc) / (arc - 1)) * Mathf.PI * .5f;
                Vector2 p = center + new Vector2(corner == 0 || corner == 3 ? halfW : -halfW,corner < 2 ? halfH : -halfH);
                float radius=TechCatalog.SupplyRadius(node.kind);
                circleVertices[i] = new Vector3(p.x + Mathf.Cos(angle) * radius,p.y + Mathf.Sin(angle) * radius,0);
            }
            line.enabled = true; line.positionCount = circleVertices.Length; line.SetPositions(circleVertices);
            line.startColor = line.endColor = color;
        }

        bool CanPreview(WorldState world,PlayerState player,BuildingState b)
        {
            if (ModularMachineCatalog.IsModular(b.kind)) return CanModularPreview(world, player, b.kind, b.x, b.y, out _);
            if (!PrimitiveCatalog.IsBuildUnlocked(world,b.kind)) return false;
            if (TechCatalog.RequiresWater(b.kind) && !TechCatalog.NearWater(world,b)) return false;
            if (world.buildings != null && world.buildings.Count >= WorldSimulator.MaxBuildings) return false;
            int w = DemoCatalog.Width(b), h = DemoCatalog.Height(b);
            for (int y = b.y; y < b.y + h; y++) for (int x = b.x; x < b.x + w; x++)
            {
                int ground = Ground(x, y);
                if (!WorldTiles.Inside(world, x, y) || ground < 0 || ground == 2) return false;
                if (FloorWorld.IsProtectedCell(world, x, y)) return false;
                long cell = WorldTiles.Key(x, y);
                if (occupied.Contains(cell) || blockedNodes.Contains(cell) || groundPickupCells.Contains(cell)) return false;
                if (world.survival!=null && world.survival.enabled)
                {
                    foreach (var plot in world.survival.plots) if (plot.x==x && plot.y==y) return false;
                    foreach (var c in world.survival.creatures) if (Mathf.Abs(c.x-x)<.8f && Mathf.Abs(c.y-y)<.8f) return false;
                }
                if (world.generatorVersion < WorldGeneration.NaturalOpeningVersion &&
                    Mathf.Abs(x - world.width / 2) <= 1 && Mathf.Abs(y - (world.height / 2 + 3)) <= 1) return false;
                if (b.kind != BuildKind.Belt)
                {
                    if (world.players != null) foreach (var p in world.players)
                        if (p.hp > 0 && Mathf.Abs(p.x - x) < .8f && Mathf.Abs(p.y - y) < .8f) return false;
                    if (world.zombies != null) foreach (var z in world.zombies)
                        if (Mathf.Abs(z.x - x) < .8f && Mathf.Abs(z.y - y) < .8f) return false;
                }
            }
            if ((new Vector2(b.x - player.x,b.y - player.y)).sqrMagnitude > 2.4f * 2.4f || !PreviewLineClear(player.x,player.y,b.x,b.y)) return false;
            var cost = DemoCatalog.BuildCost(b.kind);
            for (int i = 0; i < cost.Length; i++)
                if (cost[i] > 0 && (player.items == null || i >= player.items.Length || player.items[i] < cost[i])) return false;
            if (b.kind == BuildKind.Miner || b.kind == BuildKind.SteamMiner)
            {
                bool ore = false;
                if (world.nodes != null) foreach (var n in world.nodes)
                    if ((b.kind==BuildKind.SteamMiner ? EquipmentCatalog.UsesPickaxe(n.kind) :
                        n.kind==NodeKind.Ore || n.kind==NodeKind.CopperOre || n.kind==NodeKind.TinOre || n.kind==NodeKind.UraniumOre) && n.hp > 0 && n.stock > 0 && n.x >= b.x - 1 && n.x <= b.x + w && n.y >= b.y - 1 && n.y <= b.y + h)
                    { ore = true; break; }
                if (!ore) return false;
            }
            return true;
        }

        bool PreviewLineClear(float px,float py,int x,int y,BuildingState ignored = null)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(new Vector2(px,py),new Vector2(x,y))/.16f));
            int sx=Mathf.FloorToInt(px+.5f),sy=Mathf.FloorToInt(py+.5f);
            for (int i=1;i<=steps;i++)
            {
                int cx=Mathf.FloorToInt(px+(x-px)*i/steps+.5f),cy=Mathf.FloorToInt(py+(y-py)*i/steps+.5f);
                if ((cx==x && cy==y) || (cx==sx && cy==sy)) continue;
                if (ignored != null && DemoCatalog.Contains(ignored, cx, cy)) continue;
                int ground = Ground(cx, cy);
                long cell = WorldTiles.Key(cx, cy);
                if (!WorldTiles.Inside(groundWorld, cx, cy) || ground < 0 || ground == 2 ||
                    blockedNodes.Contains(cell) || solidBuildings.Contains(cell)) return false;
            }
            return true;
        }

        void Circle(LineRenderer line,Vector2 center,float radius,Color color)
        {
            line.enabled=true; int segments=circleVertices.Length; line.positionCount=segments;
            for (int i=0;i<segments;i++)
            { float a=i*(Mathf.PI*2f/segments); circleVertices[i]=new Vector3(center.x+Mathf.Cos(a)*radius,center.y+Mathf.Sin(a)*radius,0); }
            line.SetPositions(circleVertices);
            line.startColor=line.endColor=color;
        }

        void Spawn(string art,Vector2 at,Color color,float lifetime,float scale,Vector2 velocity)
        {
            Particle particle=null;
            foreach (var p in particles) if (!p.sprite.enabled) { particle=p; break; }
            if (particle==null)
            {
                if (particles.Count>=MaxParticles) return;
                particle=new Particle { sprite=NewSprite("Damage feedback",effectsRoot,9800) }; particles.Add(particle);
            }
            particle.sprite.enabled=true; particle.sprite.sprite=Art(art); particle.sprite.transform.position=at;
            particle.sprite.transform.localRotation=Quaternion.Euler(0,0,(stamp*47)%360);
            particle.sprite.sortingOrder=art=="blood" ? -11500 : 9800;
            particle.born=Time.unscaledTime; particle.lifetime=lifetime; particle.tint=color; particle.velocity=velocity; particle.scale=scale;
        }

        void DrawParticles()
        {
            foreach (var p in particles)
            {
                if (!p.sprite.enabled) continue;
                float age=(Time.unscaledTime-p.born)/p.lifetime;
                if (age>=1) { p.sprite.enabled=false; continue; }
                p.sprite.transform.position+=p.velocity*Time.unscaledDeltaTime;
                p.sprite.transform.localScale=Vector3.one*p.scale*(1+age*.3f);
                var c=p.tint; c.a*=1-age; p.sprite.color=c;
            }
            DrawWeaponEffects();
        }

        Sprite Machine(BuildKind kind,int rotation)
        {
            Sprite svg=DemoArt.Get("machine_"+(int)kind); if (svg!=null) return svg;
            if (kind == BuildKind.CharcoalKiln || kind == BuildKind.ClayKiln) return Art("industry_" + (int)BuildKind.StoneFurnace);
            if (kind==BuildKind.PowerPole) return Art("runtime_powerpole");
            if (kind==BuildKind.SolarPanel) return Art("runtime_solarpanel");
            if ((int)kind >= (int)BuildKind.Workbench) return Art("industry_" + (int)kind);
            return Art("build_"+MachineNames[Mathf.Clamp((int)kind,0,MachineNames.Length-1)]+"_"+rotation);
        }
        Sprite Art(string key) { Sprite sprite = DemoArt.Get(key); return sprite != null ? sprite : sprites.TryGetValue(key,out sprite) ? sprite : null; }

        static Transform NewRoot(string name,Transform parent)
        { var obj=new GameObject(name); obj.transform.SetParent(parent,false); return obj.transform; }

        SpriteRenderer NewSprite(string name,Transform parent,int order)
        {
            var obj=new GameObject(name); obj.transform.SetParent(parent,false);
            var renderer=obj.AddComponent<SpriteRenderer>(); renderer.sharedMaterial=spriteMaterial; renderer.sortingOrder=order;
            return renderer;
        }

        LineRenderer NewLine(string name,int order,float width,bool loop)
        {
            var obj=new GameObject(name); obj.transform.SetParent(effectsRoot,false);
            var line=obj.AddComponent<LineRenderer>(); line.sharedMaterial=effectMaterial;
            line.useWorldSpace=true; line.startWidth=line.endWidth=width; line.sortingOrder=order;
            line.loop=loop; line.numCapVertices=0; line.numCornerVertices=0; line.enabled=false;
            return line;
        }

        void OnEnable() { SetViewEnabled(true); }
        void OnDisable() { SetViewEnabled(false); }

        void SetViewEnabled(bool enabledView)
        {
            if (!initialized) return;
            bool show=enabledView && wasRunning;
            if (worldRoot!=null) worldRoot.gameObject.SetActive(show);
            if (minimapCanvas!=null) minimapCanvas.SetActive(show);
            if (MinimapRect!=null) MinimapRect.gameObject.SetActive(show);
            if (hurtOverlay!=null) hurtOverlay.gameObject.SetActive(show);
            if (weatherVeil!=null) weatherVeil.gameObject.SetActive(show);
            if (volume!=null) volume.enabled=enabledView;
            if (!enabledView)
            {
                observedWorld = null; observedElapsed = -1; observedTick = -1;
                if (backpackHud != null) backpackHud.ShowDroppedBackpackHint(null, Vector2.zero);
            }
        }

        void OnDestroy()
        {
            DisposeRooms();
            ResetGroundStreaming();
            foreach (var sprite in sprites.Values) if (sprite!=null) Destroy(sprite);
            if (spriteMaterial!=null) Destroy(spriteMaterial);
            if (terrainMaterial!=null) Destroy(terrainMaterial);
            if (effectMaterial!=null) Destroy(effectMaterial);
            if (spriteMeshMaterial!=null) Destroy(spriteMeshMaterial);
            if (expansionGroundMaterial!=null) Destroy(expansionGroundMaterial);
            if (mapTexture!=null) Destroy(mapTexture);
            if (powerTexture!=null) Destroy(powerTexture);
            if (industryTexture!=null) Destroy(industryTexture);
            if (volumeProfile!=null)
            {
                foreach (var component in volumeProfile.components) if (component!=null) Destroy(component);
                Destroy(volumeProfile);
            }
            if (worldRoot!=null) Destroy(worldRoot.gameObject);
            if (minimapCanvas!=null) Destroy(minimapCanvas);
            // These may have been adopted by the HUD Canvas. They remain view-owned.
            if (MinimapRect!=null) Destroy(MinimapRect.gameObject);
            if (hurtOverlay!=null) Destroy(hurtOverlay.gameObject);
            if (weatherVeil!=null) Destroy(weatherVeil.gameObject);
            if (volume!=null) Destroy(volume.gameObject);
            if (ownsCamera && WorldCamera!=null) Destroy(WorldCamera.gameObject);
            if (ownedAudioListener!=null) Destroy(ownedAudioListener);
        }

        Material spriteMeshMaterial;
        Material SpriteMeshMaterial()
        {
            if (spriteMeshMaterial!=null) return spriteMeshMaterial;
            spriteMeshMaterial=new Material(terrainMaterial);
            spriteMeshMaterial.mainTexture=spriteTexture;
            return spriteMeshMaterial;
        }
    }
}
