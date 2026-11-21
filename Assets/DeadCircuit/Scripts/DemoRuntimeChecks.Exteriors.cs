#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeadCircuit
{
    public sealed partial class DemoRuntimeChecks
    {
        // Opt-in, memory-only render fixtures. No network host, simulator ticks,
        // regular archive access or save calls are needed to exercise the view.
        IEnumerator CheckNaturalEntrances(GameBridge bridge)
        {
            bridge.enabled = false;
            var hud = bridge.GetComponent<DemoHud>();
            hud.enabled = false;
            foreach (var canvas in bridge.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            var view = bridge.GetComponent<DemoWorldView>();
            view.enabled = false;
            SetQaField(bridge, "profileId", "exterior-qa");
            SetQaField(bridge, "<IsHost>k__BackingField", false);
            SetQaField(bridge, "<IsRunning>k__BackingField", true);
            int cases = 0;
            foreach (var kind in new[] { SiteKind.HuntingCabin, SiteKind.Apartment })
            for (int rotation = 0; rotation < 4; rotation++)
            {
                WorldState world = ExteriorFixture(kind, rotation, out SiteState site, out PlayerState player);
                SetQaField(bridge, "<World>k__BackingField", world);
                QaViewCall(view, "ResetEntities");
                QaViewCall(view, "ResetGroundStreaming");
                yield return null;
                yield return RenderEntranceFixture(view, site);
                var exteriors = (IDictionary)QaField(view, "naturalExteriors");
                object exterior = exteriors[site.id];
                var shell = (MeshRenderer)QaField(exterior, "renderer");
                var root = (Transform)QaField(exterior, "root");
                var layout = (NaturalBuildingLayout)QaField(exterior, "layout");
                Require(root.gameObject.activeSelf, "outside retains the opaque building shell");
                var shellVertices = ((Mesh)QaField(exterior, "mesh")).vertices;
                foreach (var opening in layout.EntryOpenings)
                for (int i = 0; i < shellVertices.Length; i += 4)
                    Require(shellVertices[i].x >= opening.maxX - .0001f || shellVertices[i + 2].x <= opening.minX + .0001f ||
                        shellVertices[i].y >= opening.maxY - .0001f || shellVertices[i + 2].y <= opening.minY + .0001f,
                        "opaque roof/facade fragments cannot cover the ground-level entry lane");
                int entries = 0;
                foreach (var door in world.buildings)
                {
                    if (!layout.Entrance(door.x - site.x, door.y - site.y)) continue;
                    entries++;
                    var body = EntranceDoorBody(view, door.id);
                    Require(body.enabled && body.sortingOrder > shell.sortingOrder,
                        "real entrance door stays above roof/facade: " + kind + "/" + rotation);
                }
                Require(entries > 0, "fixture has an exterior entry");
                var guides = (MeshRenderer)QaField(exterior, "entranceRenderer");
                var mesh = (Mesh)QaField(exterior, "entrances");
                Require(guides.enabled && guides.sortingOrder > shell.sortingOrder && mesh.vertexCount >= entries * 16,
                    "door frames and two approach tiles stay above the shell projection");
                yield return CaptureEntrance(view, "entrance-" + kind + "-r" + rotation + "-closed");
                int clearVertices = mesh.vertexCount;
                Require(layout.EntranceOutward(site.doorX - site.x, site.doorY - site.y, out int dx, out int dy), "primary entry has an approach");
                var obstruction = new NodeState { id = world.nextId++, kind = NodeKind.Tree,
                    x = site.doorX + dx, y = site.doorY + dy, hp = 35, stock = 10 };
                world.nodes.Add(obstruction);
                yield return RenderEntranceFixture(view, site);
                Require(mesh.vertexCount < clearVertices, "paved guide stops before a real obstacle");
                world.nodes.Remove(obstruction);
                yield return RenderEntranceFixture(view, site);
                Require(mesh.vertexCount == clearVertices, "guide returns after obstacle removal");

                var primary = world.buildings.Find(b => b.x == site.doorX && b.y == site.doorY);
                Require(primary != null, "primary door is a real building");
                primary.setting = 1; // Explicit in-memory render fixture, not a player command.
                yield return RenderEntranceFixture(view, site);
                Require(EntranceDoorBody(view, primary.id).sprite.name == "door_open",
                    "entry overlay preserves actual open-door art");
                if (rotation == 0 || rotation == 2)
                    yield return CaptureEntrance(view, "entrance-" + kind + "-r" + rotation + "-open");

                player.x = site.doorX; player.y = site.doorY;
                yield return RenderEntranceFixture(view, site);
                Require(!root.gameObject.activeSelf, "stepping into entry reveals the interior");
                player.x = site.x + site.width * .5f; player.y = site.y - 4;
                yield return RenderEntranceFixture(view, site);
                Require(root.gameObject.activeSelf, "leaving closes the exterior again");
                cases++;
            }
            // Do not leave the bridge in a running state during process teardown.
            SetQaField(bridge, "<IsRunning>k__BackingField", false);
            Debug.Log("DEMO_EXTERIOR_QA_PASS cases=" + cases + " realDoor=guides=openState=cutaway=memoryOnly=true");
        }

        static WorldState ExteriorFixture(SiteKind kind, int rotation, out SiteState site, out PlayerState player)
        {
            var world = new WorldState { seed = 73149, generatorVersion = WorldGeneration.CurrentGeneratorVersion,
                ground = new string('1', 4096), biomes = new string('0', 4096),
                chunks = new ChunkWorldState { enabled = true } };
            for (int cy = 0; cy < 4; cy++) for (int cx = 0; cx < 4; cx++)
                world.chunks.records.Add(new WorldChunkState { x = cx, y = cy, ground = new string('1', 256), biomes = new string('0', 256) });
            var plan = SiteBlueprintCatalog.Get(kind);
            site = new SiteState { id = world.nextId++, kind = kind, x = 26, y = 26, rotation = rotation,
                width = SiteBlueprintCatalog.Width(plan, rotation), height = SiteBlueprintCatalog.Height(plan, rotation), layoutVersion = 1 };
            bool primary = false;
            for (int y = 0; y < site.height; y++) for (int x = 0; x < site.width; x++)
            {
                char cell = SiteBlueprintCatalog.At(plan, rotation, x, y);
                int gx = site.x + x, gy = site.y + y;
                if (cell == '#')
                    world.nodes.Add(new NodeState { id = world.nextId++, kind = NodeKind.Wall, x = gx, y = gy, hp = 160, stock = 14 });
                else if (cell == '+')
                {
                    world.buildings.Add(new BuildingState { id = world.nextId++, kind = BuildKind.Door, x = gx, y = gy,
                        rotation = x == 0 || x == site.width - 1 ? 1 : 0, footprintVersion = 1,
                        hp = WorldSimulator.BuildingMaxHealth(BuildKind.Door) });
                    if (!primary && (x == 0 || y == 0 || x == site.width - 1 || y == site.height - 1))
                    { site.doorX = gx; site.doorY = gy; primary = true; }
                }
                else if (SiteBlueprintCatalog.Crates.IndexOf(cell) >= 0)
                    world.nodes.Add(new NodeState { id = world.nextId++, kind = NodeKind.Crate, x = gx, y = gy, hp = 35, stock = 8 });
            }
            world.sites.Add(site);
            player = new PlayerState { id = "exterior-qa", name = "入口检查", x = site.x + site.width * .5f, y = site.y - 4 };
            world.players.Add(player);
            return world;
        }

        static IEnumerator RenderEntranceFixture(DemoWorldView view, SiteState site)
        {
            for (int i = 0; i < 4; i++)
            {
                QaViewCall(view, "LateUpdate");
                view.WorldCamera.orthographicSize = 8.5f;
                view.WorldCamera.transform.position = new Vector3(site.x + (site.width - 1) * .5f,
                    site.y + (site.height - 1) * .5f + (FloorWorld.MaxLevel(site.kind) + 1) * NaturalBuildingLayout.FloorRise * .5f, -10);
                yield return null;
            }
        }

        IEnumerator CaptureEntrance(DemoWorldView view, string label)
        {
            yield return new WaitForEndOfFrame();
            // Render the actual world camera explicitly; a hidden Windows
            // player can otherwise return a black swapchain screenshot.
            var target = RenderTexture.GetTemporary(1440, 900, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            try
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Require(RenderPipeline.SupportsRenderRequest(view.WorldCamera, request), "URP camera render request is available");
                RenderPipeline.SubmitRenderRequest(view.WorldCamera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                pixels.Apply();
                int visible = 0;
                foreach (var pixel in pixels.GetPixels32()) if (pixel.r + pixel.g + pixel.b > 90) visible++;
                Require(visible > 1440 * 900 / 8, "camera capture contains visible world geometry");
                File.WriteAllBytes(Path.Combine(output, label + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(pixels);
            }
        }

        static SpriteRenderer EntranceDoorBody(DemoWorldView view, int id) =>
            (SpriteRenderer)QaField(((IDictionary)QaField(view, "buildings"))[id], "body");
        static object QaField(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
        static void SetQaField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);
        static void QaViewCall(DemoWorldView view, string name) =>
            typeof(DemoWorldView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
    }
}
#endif
