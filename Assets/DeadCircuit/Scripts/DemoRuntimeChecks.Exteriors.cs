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
            int cases = 0, internalDoors = 0, internalDoorSamples = 0;
            float[] doorwaySamples = { -1, -.49f, 0, .49f, 1 };
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
                var shellVertices = ((Mesh)QaField(exterior, "mesh")).vertices;
                foreach (var opening in layout.EntryOpenings)
                for (int i = 0; i < shellVertices.Length; i += 4)
                    Require(shellVertices[i].x >= opening.maxX - .0001f || shellVertices[i + 2].x <= opening.minX + .0001f ||
                        shellVertices[i].y >= opening.maxY - .0001f || shellVertices[i + 2].y <= opening.minY + .0001f,
                        "opaque roof/facade fragments cannot cover the ground-level entry lane");
                var guides = (MeshRenderer)QaField(exterior, "entranceRenderer");
                var mesh = (Mesh)QaField(exterior, "entrances");
                void RequireDoorOrientation(BuildingState door, bool verticalWall)
                {
                    var body = EntranceDoorBody(view, door.id);
                    float expected = ((door.rotation & 2) + (verticalWall ? 1 : 0)) * 90f;
                    Require(Mathf.Abs(Mathf.DeltaAngle(body.transform.localEulerAngles.z, expected)) < .01f,
                        "doorAlignment: door follows its wall in both states: " + kind + "/" + rotation +
                        " door=" + door.x + "," + door.y + " setting=" + door.setting);
                    Require(body.transform.localPosition == Vector3.zero && body.transform.localScale == Vector3.one,
                        "doorAlignment: rotated door remains centred at one-cell scale");
                    var preview = (SpriteRenderer)QaField(view, "ghost");
                    typeof(DemoWorldView).GetMethod("ConfigureMachine", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(view, new object[] { preview, door });
                    Require(Mathf.Abs(Mathf.DeltaAngle(preview.transform.localEulerAngles.z, expected)) < .01f &&
                        preview.transform.localPosition == Vector3.zero && preview.transform.localScale == Vector3.one,
                        "doorAlignment: build preview follows the same wall axis");
                }
                void RequireExteriorDetails()
                {
                    Require(root.gameObject.activeSelf, "outside retains the opaque building shell: " + kind + "/" + rotation);
                    int entries = 0;
                    foreach (var door in world.buildings)
                    {
                        if (!layout.Entrance(door.x - site.x, door.y - site.y)) continue;
                        entries++;
                        var body = EntranceDoorBody(view, door.id);
                        Require(body.enabled && body.gameObject.activeInHierarchy && body.sortingOrder > shell.sortingOrder,
                            "real entrance door stays above roof/facade: " + kind + "/" + rotation);
                        Require(layout.EntranceOutward(door.x - site.x, door.y - site.y, out int dx, out _),
                            "doorAlignment: exterior entry has a traversal axis");
                        RequireDoorOrientation(door, dx != 0);
                    }
                    Require(entries > 0, "fixture has an exterior entry");
                    Require(guides.enabled && guides.sortingOrder > shell.sortingOrder && mesh.vertexCount == entries * 8,
                        "noApproachTiles: each exterior entry has only threshold and frame quads: " + kind + "/" + rotation);
                    var vertices = mesh.vertices;
                    for (int i = 0; i < vertices.Length; i += 4)
                    {
                        bool atEntry = false;
                        foreach (var door in world.buildings)
                        {
                            int x = door.x - site.x, y = door.y - site.y;
                            if (!layout.Entrance(x, y)) continue;
                            bool withinFrame = true;
                            for (int j = 0; j < 4; j++)
                                withinFrame &= Mathf.Abs(vertices[i + j].x - x) <= .6251f &&
                                    Mathf.Abs(vertices[i + j].y - y) <= .6251f;
                            if (withinFrame) { atEntry = true; break; }
                        }
                        Require(atEntry, "noApproachTiles: quad stays within an exterior door centre +/- .625: " +
                            kind + "/" + rotation + " quad=" + i / 4);
                    }
                }
                RequireExteriorDetails();
                if (rotation == 0)
                    yield return CaptureEntrance(view, "entrance-" + kind + "-r0-closed");

                var primary = world.buildings.Find(b => b.x == site.doorX && b.y == site.doorY);
                Require(primary != null, "primary door is a real building");
                float outsideX = player.x, outsideY = player.y;
                for (int setting = 0; setting <= 1; setting++)
                {
                    primary.setting = setting; // Explicit in-memory render fixture, not a player command.
                    yield return RenderEntranceFixture(view, site);
                    RequireExteriorDetails();
                    Require(EntranceDoorBody(view, primary.id).sprite.name ==
                        (setting == 1 ? "door_open" : "machine_" + (int)BuildKind.Door),
                        "entry overlay preserves actual door art: setting=" + setting);
                    player.x = site.doorX; player.y = site.doorY;
                    yield return RenderEntranceFixture(view, site);
                    Require(!root.gameObject.activeSelf, "stepping into entry reveals the interior: setting=" + setting);
                    player.x = outsideX; player.y = outsideY;
                    yield return RenderEntranceFixture(view, site);
                    RequireExteriorDetails();
                }

                // Read the existing room mask independently of Contains, whose
                // doorway rounding and cutaway behaviour are under test.
                var interior = (bool[])QaField(layout, "interior");
                bool InteriorAt(int x, int y) => x >= 0 && y >= 0 && x < site.width && y < site.height &&
                    interior[x + y * site.width];
                int caseInternalDoors = 0;
                for (int y = 0; y < site.height; y++) for (int x = 0; x < site.width; x++)
                {
                    if (layout.Cell(x, y) != '+' || layout.Entrance(x, y) || !layout.Covers(x, y)) continue;
                    var door = world.buildings.Find(b => b.kind == BuildKind.Door && b.x == site.x + x && b.y == site.y + y);
                    Require(door != null, "internalDoors: covered internal doorway has a real door");
                    bool horizontal = InteriorAt(x - 1, y) && InteriorAt(x + 1, y);
                    bool vertical = InteriorAt(x, y - 1) && InteriorAt(x, y + 1);
                    Require(horizontal != vertical, "internalDoors: doorway has two InteriorAt neighbours on one traversal axis: " +
                        kind + "/" + rotation + " door=" + x + "," + y);
                    int dx = horizontal ? 1 : 0, dy = vertical ? 1 : 0;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int gx = door.x + dx * side, gy = door.y + dy * side;
                        Require(!world.nodes.Exists(n => n.x == gx && n.y == gy) &&
                            !world.buildings.Exists(b => b.x == gx && b.y == gy),
                            "internalDoors: adjacent InteriorAt sample is enterable");
                    }
                    for (int setting = 0; setting <= 1; setting++)
                    {
                        door.setting = setting; // Never tick the simulator or start a host.
                        foreach (float offset in doorwaySamples)
                        {
                            player.x = door.x + dx * offset; player.y = door.y + dy * offset;
                            yield return RenderEntranceFixture(view, site);
                            Require(layout.Contains(player.x - site.x, player.y - site.y) && !root.gameObject.activeSelf,
                                "internalDoors: room/door samples retain the same cutaway: " + kind + "/" + rotation +
                                " door=" + x + "," + y + " setting=" + setting + " offset=" + offset);
                            RequireDoorOrientation(door, horizontal);
                            internalDoorSamples++;
                            if (kind == SiteKind.Apartment && rotation == 0 && caseInternalDoors == 0 && setting == 1 && offset == 0)
                                yield return CaptureEntrance(view, "entrance-Apartment-r0-internal-door-open");
                        }
                        player.x = outsideX; player.y = outsideY;
                        yield return RenderEntranceFixture(view, site);
                        RequireExteriorDetails();
                    }
                    door.setting = 0;
                    caseInternalDoors++; internalDoors++;
                }
                Require(kind != SiteKind.Apartment || caseInternalDoors > 0,
                    "internalDoors: every Apartment rotation exercises an internal doorway: rotation=" + rotation);
                cases++;
            }
            Require(cases == 8 && internalDoors > 0 && internalDoorSamples == internalDoors * 10,
                "internalDoors: eight fixtures cover five traversal samples in both door states");
            // Do not leave the bridge in a running state during process teardown.
            SetQaField(bridge, "<IsRunning>k__BackingField", false);
            Debug.Log("DEMO_EXTERIOR_QA_PASS cases=" + cases + " internalDoors=" + internalDoors +
                " internalDoorSamples=" + internalDoorSamples + " noApproachTiles=true doorAlignment=true realDoor=guides=openState=cutaway=memoryOnly=true");
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
