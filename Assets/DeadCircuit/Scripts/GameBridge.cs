using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
#if DEADCIRCUIT_NETCODE
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
#endif

namespace DeadCircuit
{
    // The simulator is the only writer of game state. Clients send intent, never inventory.
    public sealed partial class GameBridge : MonoBehaviour
    {
        public static GameBridge Instance { get; private set; }
        public WorldState World { get; private set; }
        public PlayerState LocalPlayer => World?.players?.Find(p => p.id == profileId);
        public bool IsRunning { get; private set; }
        public bool IsHost { get; private set; }
        public string Status { get; private set; } = "局域网合作 / 最多 4 位幸存者";
        public string HostIP { get; private set; } = "127.0.0.1";
        public string PlayerName { get; set; } = "幸存者";
        public BuildKind? BuildSelection { get; set; }
        public int BuildRotation { get; set; }
        public bool WorldInputBlocked => hud != null && hud.CapturesInput;
        public string SavePath => ResolveSavePath();
        public Vector2 MouseWorld
        {
            get
            {
                var camera = view == null ? Camera.main : view.WorldCamera;
                if (camera == null || Mouse.current == null) return Vector2.zero;
                var point = Mouse.current.position.ReadValue();
                return camera.ScreenToWorldPoint(new Vector3(point.x, point.y, -camera.transform.position.z));
            }
        }

        const float StepSeconds = .05f;
        const int MaxPayload = 1024 * 1024;
        const ushort Port = 7777;
        string profileId;
        WorldSimulator simulator;
        DemoWorldView view;
        DemoHud hud;
        float accumulator, snapshotClock, inputClock, autoSaveClock, connectStarted;
        bool connecting, leaving;
        readonly Dictionary<ulong, string> peers = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, int> commandCounts = new Dictionary<ulong, int>();
        float rateClock;
        bool smokeHost, smokeClient, smokeFinished;
        bool qaRun;
        float smokeStarted, smokeFirstX, smokePeerFirstX = float.NaN;
        bool smokeMoved, smokeSawPeer, smokeSawGrid, smokeSawCargo, smokeSawResearch, smokeResearchSent;
        bool smokeSawEquipment;
        int smokeEquipmentStage;
        long lastSnapshotTick = -1;
#if DEADCIRCUIT_NETCODE
        NetworkManager network;
        UnityTransport transport;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameBridge>() == null) new GameObject("DeadCircuit / Game").AddComponent<GameBridge>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            qaRun = Array.IndexOf(Environment.GetCommandLineArgs(), "--demo-qa") >= 0;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            profileId = PlayerPrefs.GetString("DeadCircuit.Profile", "");
            if (!Guid.TryParse(profileId, out _))
            {
                profileId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString("DeadCircuit.Profile", profileId);
                PlayerPrefs.Save();
            }
            PlayerName = PlayerPrefs.GetString("DeadCircuit.Name", "幸存者");
            LoadAppearancePreference();
            try
            {
                var addresses = Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
                HostIP = string.Join(" / ", addresses.Select(a => a.ToString()));
                if (HostIP.Length == 0) HostIP = "127.0.0.1";
            }
            catch { HostIP = "127.0.0.1"; }
            view = gameObject.AddComponent<DemoWorldView>();
            view.Init(this);
            hud = gameObject.AddComponent<DemoHud>();
            hud.Init(this);
            var audio = gameObject.AddComponent<DemoAudio>();
            audio.Init(this);
            audio.SetVolumePreferences("DeadCircuit.MasterVolume", "DeadCircuit.EffectsVolume");
        }

        IEnumerator Start()
        {
            yield return null;
            var args = Environment.GetCommandLineArgs();
            qaRun = args.Contains("--demo-qa");
            smokeHost = args.Contains("--demo-smoke-host");
            smokeClient = args.Contains("--demo-smoke-client");
            if (smokeHost || smokeClient)
            {
                // Distinct process profiles permit a genuine host + client test on one PC.
                profileId = smokeHost ? "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" : "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
                PlayerName = smokeHost ? "联机测试主机" : "联机测试客户端";
                smokeStarted = Time.realtimeSinceStartup;
                if (smokeHost) StartHost(73149, false); else Join("127.0.0.1", PlayerName);
            }
        }

        public void StartHost(int seed, bool load)
        {
            if (IsRunning || connecting || leaving) return;
            try
            {
                if (load)
                {
                    if (!File.Exists(SavePath)) { Status = "还没有存档，请先创建世界。"; return; }
                    if (new FileInfo(SavePath).Length > WorldArchive.MaxBytes) throw new InvalidDataException("存档过大");
                    simulator = new WorldSimulator(JsonUtility.FromJson<WorldState>(File.ReadAllText(SavePath, Encoding.UTF8)));
                }
                else
                {
                    if (!smokeHost && !qaRun && pendingGeneration == null) activeWorldId = Guid.NewGuid().ToString("N");
                    simulator = smokeHost ? new WorldSimulator(CreateSmokeState(seed)) : pendingGeneration != null ?
                        new WorldSimulator(WorldGeneration.Generate(pendingGeneration)) : new WorldSimulator(seed);
                }
                World = simulator.State;
                foreach (var p in World.players) p.connected = false;
                simulator.AddPlayer(profileId, CleanName(PlayerName), AppearanceCode);
                IsHost = true;
#if DEADCIRCUIT_NETCODE
                SetupNetwork();
                network.NetworkConfig.ConnectionData = TicketBytes();
                transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
                if (!network.StartHost()) throw new IOException("7777 端口无法监听，可能已有主机运行");
                RegisterMessages();
                peers[NetworkManager.ServerClientId] = profileId;
                IsRunning = true;
                Status = "主机已就绪 · UDP 7777 · " + HostIP;
#else
                IsRunning = true;
                Status = "开发模式：联机包尚未安装";
#endif
                ResetClocks();
                RememberName();
            }
            catch (Exception e) { Status = "创建世界失败：" + e.Message; AbortSession(); }
        }

        public void Join(string ip, string name)
        {
            if (IsRunning || connecting || leaving) return;
            if (!IPAddress.TryParse((ip ?? "").Trim(), out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            { Status = "请输入主机的 IPv4 地址，例如 192.168.1.8"; return; }
            PlayerName = CleanName(name);
            try
            {
#if DEADCIRCUIT_NETCODE
                SetupNetwork();
                IsHost = false;
                World = null;
                lastSnapshotTick = -1;
                transport.SetConnectionData(address.ToString(), Port);
                network.NetworkConfig.ConnectionData = TicketBytes();
                connecting = true;
                connectStarted = Time.unscaledTime;
                Status = "正在连接 " + address + ":7777 …";
                if (!network.StartClient()) throw new IOException("无法启动网络客户端");
                RegisterMessages();
                RememberName();
#else
                Status = "请先安装 Netcode for GameObjects 并重新编译。";
#endif
            }
            catch (Exception e) { Status = "连接失败：" + e.Message; AbortSession(); }
        }

        void ResetClocks()
        {
            accumulator = snapshotClock = inputClock = autoSaveClock = 0;
            smokeMoved = smokeSawPeer = smokeSawGrid = smokeSawCargo = smokeSawResearch = smokeResearchSent = false;
            smokeSawEquipment = false; smokeEquipmentStage = 0;
            smokeFirstX = LocalPlayer?.x ?? 0;
        }

        public void Send(PlayerCommand command)
        {
            if (!IsRunning || command == null || command.action == "appearance") return;
            command.playerId = profileId;
            if (IsHost) simulator.Apply(command);
#if DEADCIRCUIT_NETCODE
            else if (network != null && network.IsConnectedClient)
            {
                var json = JsonUtility.ToJson(command);
                if (json.Length > 1024) return;
                using (var writer = new FastBufferWriter(4096, Allocator.Temp))
                {
                    writer.WriteValueSafe(json);
                    network.CustomMessagingManager.SendNamedMessage("dc.cmd.v10", NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
                }
            }
#endif
        }

        void Update()
        {
            if (connecting && Time.unscaledTime - connectStarted > 15)
            { Status = "连接超时。请检查主机 IP、同一局域网和防火墙的专用网络权限。"; AbortSession(); }
            if (!IsRunning) { SmokeUpdate(); return; }
            inputClock += Time.unscaledDeltaTime;
            if (!(smokeHost || smokeClient || qaRun)) ReadInput();
            if (IsHost)
            {
                accumulator += Mathf.Min(Time.unscaledDeltaTime, .25f);
                while (accumulator >= StepSeconds)
                {
                    simulator.Step(StepSeconds);
                    accumulator -= StepSeconds;
                }
                snapshotClock += Time.unscaledDeltaTime;
                if (snapshotClock >= .2f) { snapshotClock = 0; BroadcastWorld(); }
                autoSaveClock += Time.unscaledDeltaTime;
                if (autoSaveClock > 30) { autoSaveClock = 0; SaveInternal(false); }
                rateClock += Time.unscaledDeltaTime;
                if (rateClock >= 1) { commandCounts.Clear(); rateClock = 0; }
            }
            SmokeUpdate();
        }

        void ReadInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            var player = LocalPlayer;
            if (player == null) return;
            if (keyboard.f5Key.wasPressedThisFrame) Save();
            bool blocked = (hud != null && hud.CapturesInput) || !Application.isFocused;
            Vector2 move = Vector2.zero;
            var aim = (MouseWorld - new Vector2(player.x, player.y)).normalized;
            if (!blocked)
            {
                move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)).normalized;
                var keys = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key, keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key, keyboard.digit0Key };
                var hotbar = TechCatalog.Hotbar(World.era);
                for (int i = 0; i < keys.Length && i < hotbar.Length; i++)
                    if (keys[i].wasPressedThisFrame && TechCatalog.IsUnlocked(World, hotbar[i])) BuildSelection = hotbar[i];
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    if (BuildSelection.HasValue) BuildRotation = (BuildRotation + 1) % 4;
                    else Action("reload");
                }
                if (keyboard.qKey.wasPressedThisFrame && !BuildSelection.HasValue)
                    Action("switch", player.activeSlot == 0 ? 1 : 0);
                if (mouse.rightButton.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame) BuildSelection = null;
                if (keyboard.eKey.wasPressedThisFrame) Action("interact");
                if (keyboard.fKey.wasPressedThisFrame) Action("eat");
                if (keyboard.cKey.wasPressedThisFrame) Action("craft", keyboard.leftCtrlKey.isPressed ? 1 : 0);
                if (keyboard.xKey.wasPressedThisFrame) Action("dismantle");
                if (keyboard.gKey.wasPressedThisFrame) Action("repair");
                if (mouse.leftButton.isPressed)
                {
                    if (BuildSelection.HasValue)
                    { if (mouse.leftButton.wasPressedThisFrame) Action("build", (int)BuildSelection.Value); }
                    else if (inputClock >= StepSeconds) Action("attack");
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0 && view?.WorldCamera != null)
                    view.WorldCamera.orthographicSize = Mathf.Clamp(view.WorldCamera.orthographicSize - scroll * .012f, 6, 16);
            }
            if (inputClock >= StepSeconds)
            {
                Send(new PlayerCommand { moveX = move.x, moveY = move.y, aimX = aim.x, aimY = aim.y, sprint = !blocked && keyboard.leftShiftKey.isPressed,
                    aiming = !blocked && !BuildSelection.HasValue && mouse.rightButton.isPressed });
                inputClock = 0;
            }
        }

        void Action(string action, int arg = 0)
        {
            Vector2 point = MouseWorld;
            Send(new PlayerCommand { action = action, x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y), arg = arg,
                rotation = BuildRotation, aimX = point.x - (LocalPlayer?.x ?? 0), aimY = point.y - (LocalPlayer?.y ?? 0),
                aiming = !BuildSelection.HasValue && Mouse.current != null && Mouse.current.rightButton.isPressed });
        }

        public void Save() => SaveInternal(true);
        void SaveInternal(bool notify)
        {
            if (!IsRunning || !IsHost || simulator == null) { if (notify) Status = "只有房主能保存世界。"; return; }
            try
            {
                if (activeWorldId != null && !smokeHost && !smokeClient && !qaRun) Archive.Write(activeWorldId, simulator.State);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                    string temporary = SavePath + ".tmp";
                    File.WriteAllText(temporary, JsonUtility.ToJson(simulator.State), new UTF8Encoding(false));
                    if (File.Exists(SavePath)) File.Replace(temporary, SavePath, null); else File.Move(temporary, SavePath);
                }
                if (notify) Status = "世界已保存 · " + DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception e) { Status = "保存失败：" + e.Message; Debug.LogWarning(Status); }
        }

        public void Leave()
        {
            if (leaving) return;
            if (IsHost && IsRunning) SaveInternal(false);
            Status = "已返回主菜单；房主世界会自动保存。";
            AbortSession();
        }

        void AbortSession()
        {
            IsRunning = connecting = false;
            IsHost = false;
            simulator = null;
            World = null;
            BuildSelection = null;
            peers.Clear();
            commandCounts.Clear();
#if DEADCIRCUIT_NETCODE
            if (network != null)
            {
                leaving = true;
                network.Shutdown();
                var old = network.gameObject;
                network = null;
                Destroy(old);
                StartCoroutine(AllowRestart());
            }
#endif
        }
        IEnumerator AllowRestart() { yield return null; yield return null; leaving = false; }
        void RememberName()
        {
            if (smokeHost || smokeClient || qaRun) return;
            PlayerPrefs.SetString("DeadCircuit.Name", CleanName(PlayerName));
            PlayerPrefs.Save();
        }
        static string CleanName(string value)
        {
            value = new string((value ?? "").Where(c => !char.IsControl(c) && c != '<' && c != '>').ToArray()).Trim();
            return value.Length == 0 ? "幸存者" : value.Substring(0, Math.Min(12, value.Length));
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused && IsRunning) Send(new PlayerCommand());
        }
        void OnApplicationQuit() { if (IsHost) SaveInternal(false); }

#if DEADCIRCUIT_NETCODE
        [Serializable] sealed class JoinTicket { public int version = WorldSimulator.SaveVersion; public int appearance; public string id, name; }
        byte[] TicketBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(new JoinTicket
            { id = profileId, name = CleanName(PlayerName), appearance = AppearanceCode }));
        void SetupNetwork()
        {
            var root = new GameObject("LAN / Netcode + Unity Transport");
            transport = root.AddComponent<UnityTransport>();
            network = root.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = true, TickRate = 20 };
            network.ConnectionApprovalCallback = Approve;
            network.OnClientConnectedCallback += Connected;
            network.OnClientDisconnectCallback += Disconnected;
            network.OnTransportFailure += TransportFailed;
        }
        void RegisterMessages()
        {
            network.CustomMessagingManager.RegisterNamedMessageHandler("dc.cmd.v10", ReceiveCommand);
            network.CustomMessagingManager.RegisterNamedMessageHandler("dc.world.v10", ReceiveWorld);
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            response.Pending = false;
            try
            {
                if (request.Payload == null || request.Payload.Length > 512) throw new InvalidDataException("加入资料无效");
                var ticket = JsonUtility.FromJson<JoinTicket>(Encoding.UTF8.GetString(request.Payload));
                if (ticket == null || ticket.version != WorldSimulator.SaveVersion || !Guid.TryParse(ticket.id, out var guid)) throw new InvalidDataException("Demo 版本不一致");
                if (!AppearanceCatalog.IsValid(ticket.appearance)) throw new InvalidDataException("主角外观预设无效");
                string id = guid.ToString("N");
                if (request.ClientNetworkId != NetworkManager.ServerClientId && peers.Values.Contains(id)) throw new InvalidDataException("此角色已在游戏中，请关闭重复连接");
                if (peers.Count >= 4) throw new InvalidDataException("房间已满（最多4人）");
                if (World.players.Count >= WorldSimulator.MaxPlayers && World.players.All(p => p.id != id)) throw new InvalidDataException("此世界已有8个存档角色，请新建世界");
                peers[request.ClientNetworkId] = id;
                var joined = simulator.AddPlayer(id, CleanName(ticket.name), ticket.appearance);
                if (smokeHost)
                {
                    for (int i = 0; i < joined.items.Length; i++) joined.items[i] = 60;
                    joined.x = 25; joined.y = 27;
                }
                response.Approved = true;
            }
            catch (Exception e) { response.Approved = false; response.Reason = e.Message; }
        }
        void Connected(ulong clientId)
        {
            if (network == null) return;
            if (network.IsServer)
            {
                if (peers.TryGetValue(clientId, out var id)) simulator.SetConnected(id, true);
                snapshotClock = .2f;
            }
            else if (clientId == network.LocalClientId) Status = "已连接，正在同步世界…";
        }
        void Disconnected(ulong clientId)
        {
            if (network == null || leaving) return;
            if (network.IsServer)
            {
                if (peers.TryGetValue(clientId, out var id)) simulator?.SetConnected(id, false);
                peers.Remove(clientId);
            }
            else
            {
                string reason = network.DisconnectReason;
                Status = string.IsNullOrWhiteSpace(reason) ? "与主机断开连接。可使用相同角色重新加入。" : "无法加入：" + reason;
                AbortSession();
            }
        }
        void TransportFailed() { Status = "网络启动失败，请检查端口 7777 是否被占用。"; AbortSession(); }
        void ReceiveCommand(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || !peers.TryGetValue(sender, out var id) || reader.Length > 4096) return;
            commandCounts.TryGetValue(sender, out int count);
            commandCounts[sender] = count + 1;
            if (count >= 60) return;
            try
            {
                reader.ReadValueSafe(out string json);
                if (json.Length > 1024) return;
                var command = JsonUtility.FromJson<PlayerCommand>(json);
                if (command == null) return;
                command.playerId = id;
                simulator.Apply(command);
            }
            catch (Exception e) { Debug.LogWarning("Ignored malformed LAN command: " + e.GetType().Name); }
        }
        void ReceiveWorld(ulong sender, FastBufferReader reader)
        {
            if (IsHost || sender != NetworkManager.ServerClientId) return;
            try
            {
                reader.ReadValueSafe(out int size);
                if (size <= 0 || size > MaxPayload || size > reader.Length - reader.Position) return;
                byte[] bytes = new byte[size];
                reader.ReadBytesSafe(ref bytes, size);
                using (var input = new MemoryStream(bytes))
                using (var zip = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[8192];
                    int read;
                    while ((read = zip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + read > 8 * 1024 * 1024) throw new InvalidDataException("snapshot too large");
                        output.Write(buffer, 0, read);
                    }
                    var next = JsonUtility.FromJson<WorldState>(Encoding.UTF8.GetString(output.ToArray()));
                    if (next == null || next.version != WorldSimulator.SaveVersion || next.tick < lastSnapshotTick) return;
                    int extent=64;
                    if(next.width!=extent || next.height!=extent || next.ground?.Length!=extent*extent || next.biomes?.Length!=extent*extent ||
                        (ChunkStreaming.Enabled(next) && (next.chunks.records==null || next.chunks.records.Count>81))) return;
                    if (next.players == null || next.players.Any(p => p == null || !AppearanceCatalog.IsValid(p.appearance))) return;
                    lastSnapshotTick = next.tick;
                    World = next;
                    if (!IsRunning) { IsRunning = true; connecting = false; ResetClocks(); }
                    Status = "局域网已同步 · " + World.players.Count(p => p.connected) + "/4 人";
                }
            }
            catch (Exception e) { Debug.LogWarning("Ignored invalid LAN snapshot: " + e.GetType().Name); }
        }
#endif
        void BroadcastWorld()
        {
#if DEADCIRCUIT_NETCODE
            if (network == null || !network.IsServer || network.ConnectedClientsIds.Count <= 1) return;
            foreach (ulong client in network.ConnectedClientsIds)
            {
                if (client == NetworkManager.ServerClientId || !peers.TryGetValue(client, out var playerId)) continue;
                byte[] raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(WorldSnapshot.ForPlayer(World, playerId)));
                if (raw.Length > 8 * 1024 * 1024) { Debug.LogError("Local world snapshot exceeds raw budget"); continue; }
                byte[] bytes;
                using (var output = new MemoryStream())
                {
                    using (var zip = new DeflateStream(output, System.IO.Compression.CompressionLevel.Fastest, true)) zip.Write(raw, 0, raw.Length);
                    bytes = output.ToArray();
                }
                if (bytes.Length > MaxPayload) { Debug.LogError("World exceeds snapshot budget"); continue; }
                using (var writer = new FastBufferWriter(bytes.Length + 4, Allocator.Temp))
                {
                    writer.WriteValueSafe(bytes.Length);
                    writer.WriteBytesSafe(bytes);
                    network.CustomMessagingManager.SendNamedMessage("dc.world.v10", client, writer, NetworkDelivery.ReliableFragmentedSequenced);
                }
            }
#endif
        }

        // An isolated opt-in LAN fixture preserves the normal primitive start and user save.
        static WorldState CreateSmokeState(int seed)
        {
            var state = new WorldSimulator(seed).State;
            state.era = TechEra.LowVoltage;
            // The opt-in LAN fixture starts at LV without pretending it ran a water factory.
            state.waterVersion = 0;
            // Synthetic progression belongs only to the opt-in LV transport fixture.
            // It is not a gameplay unlock or evidence of a completed industrial playthrough.
            state.primitiveResearch = 255;
            state.steamResearch = 255;
            state.steamCoked = 6; state.steamRefractory = 8; state.steamPigIron = 6;
            state.steamSteel = 4; state.steamPlates = 4; state.steamFittings = 4; state.steamDriven = 4;
            state.buildings.Clear(); state.cargo.Clear(); state.powerNetworks.Clear();
            state.nodes.RemoveAll(n => n.x >= 23 && n.x <= 39 && n.y >= 25 && n.y <= 33);
            char[] ground = state.ground.ToCharArray();
            for (int y = 25; y <= 33; y++) for (int x = 23; x <= 39; x++)
                ground[x + y * state.width] = (char)('0' + (int)GroundKind.Floor);
            state.ground = new string(ground);
            state.nodes.Add(new NodeState { id = state.nextId++, x = 24, y = 30, kind = NodeKind.Ore, stock = 200, hp = 35 });
            AddSmokeBuilding(state, BuildKind.Workbench, 24, 26);
            AddSmokeBuilding(state, BuildKind.Miner, 25, 30);
            AddSmokeBuilding(state, BuildKind.Belt, 27, 30);
            AddSmokeBuilding(state, BuildKind.Smelter, 28, 30);
            AddSmokeBuilding(state, BuildKind.Belt, 30, 30);
            AddSmokeBuilding(state, BuildKind.Assembler, 31, 30);
            AddSmokeBuilding(state, BuildKind.Belt, 34, 30);
            AddSmokeBuilding(state, BuildKind.Chest, 35, 30);
            var generator = AddSmokeBuilding(state, BuildKind.Generator, 27, 26);
            generator.items[(int)ItemKind.Fuel] = 8;
            AddSmokeBuilding(state, BuildKind.PowerPole, 31, 28);
            AddSmokeBuilding(state, BuildKind.CircuitAssembler, 36, 27);
            return state;
        }

        static BuildingState AddSmokeBuilding(WorldState state, BuildKind kind, int x, int y)
        {
            var building = new BuildingState
            {
                id = state.nextId++, kind = kind, x = x, y = y, footprintVersion = 1,
                hp = WorldSimulator.BuildingMaxHealth(kind), items = new int[DemoCatalog.ItemNames.Length]
            };
            var recipe = TechCatalog.Recipes.FirstOrDefault(r => r.building == kind && r.era <= state.era);
            if (recipe != null && kind != BuildKind.Workbench) building.recipe = recipe.id;
            state.buildings.Add(building);
            return building;
        }

        void SmokeUpdate()
        {
            if (!(smokeHost || smokeClient) || smokeFinished) return;
            float age = Time.realtimeSinceStartup - smokeStarted;
            if (IsRunning && LocalPlayer != null)
            {
                if (World.players.Count(p => p.connected) >= 2) smokeSawPeer = true;
                if (World.version == WorldSimulator.SaveVersion && World.powerNetworks != null && World.powerNetworks.Count > 0 &&
                    World.buildings.Any(b => b.kind == BuildKind.Assembler && DemoCatalog.Width(b) == 3 && b.powerRatio > 0)) smokeSawGrid = true;
                if (World.cargo != null && World.cargo.Any(c => c.progress > 0 && c.progress < 1)) smokeSawCargo = true;
                if (World.era >= TechEra.MediumVoltage) smokeSawResearch = true;
                if (smokeClient) SmokeEquipment(age);
                if (smokeClient && age > 7 && !smokeResearchSent && LocalPlayer.interactCooldown <= 0)
                {
                    smokeResearchSent = true;
                    Send(new PlayerCommand { action = "research", arg = (int)TechEra.MediumVoltage });
                }
                if (smokeHost)
                {
                    var peer = World.players.Find(p => p.id == "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
                    if (peer != null)
                    {
                        if (HasSmokeEquipment(peer)) smokeSawEquipment = true;
                        if (float.IsNaN(smokePeerFirstX)) smokePeerFirstX = peer.x;
                        else if (Mathf.Abs(peer.x - smokePeerFirstX) > .5f) smokeMoved = true;
                    }
                }
                if (smokeClient && age > 10 && age < 14 && inputClock >= StepSeconds)
                { Send(new PlayerCommand { moveX = 1, aimX = 1 }); inputClock = 0; }
                if (Mathf.Abs(LocalPlayer.x - smokeFirstX) > .5f) smokeMoved = true;
                if (smokeClient && age >= 14 && age < 14.2f) Send(new PlayerCommand());
                if (smokeHost && age > 16 && age < 16.2f) SaveInternal(false);
            }
            if (age < (smokeHost ? 26 : 18)) return;
            bool passed = IsRunning && smokeSawPeer && World.tick > 100 && smokeMoved && smokeSawGrid && smokeSawCargo && smokeSawResearch && smokeSawEquipment;
            Debug.Log((passed ? "DEMO_SMOKE_PASS" : "DEMO_SMOKE_FAIL") + " role=" + (smokeHost ? "host" : "client") + " tick=" + World?.tick + " peers=" + World?.players.Count + " moved=" + smokeMoved + " grid=" + smokeSawGrid + " cargo=" + smokeSawCargo + " sharedResearch=" + smokeSawResearch + " equipment=" + smokeSawEquipment + (passed ? "" : " equipmentStage=" + smokeEquipmentStage + " notice=" + LocalPlayer?.notice + " status=" + Status));
            smokeFinished = true;
            Application.Quit(passed ? 0 : 10);
        }

        static bool HasSmokeEquipment(PlayerState p)
        {
            var gun = EquipmentCatalog.Find(p, p.weaponId);
            var helmet = EquipmentCatalog.Find(p, p.armorIds[0]);
            return gun != null && gun.kind == GearKind.Pistol && gun.magazine > 0 &&
                gun.attachments[(int)AttachmentSlot.Optic] == (int)PartKind.RedDot &&
                helmet != null && helmet.kind == GearKind.ClothHood;
        }

        void SmokeEquipment(float age)
        {
            var p = LocalPlayer;
            var gun = p.gear?.FirstOrDefault(g => g.kind == GearKind.Pistol);
            var helmet = p.gear?.FirstOrDefault(g => g.kind == GearKind.ClothHood);
            // Advance only after a snapshot acknowledges the prior command. A local
            // cooldown of zero can still belong to the pre-command snapshot.
            if (smokeEquipmentStage == 0 && age > 1 && p.interactCooldown <= 0)
            { Send(new PlayerCommand { action="gearcraft",arg=(int)GearKind.Pistol }); smokeEquipmentStage=1; }
            else if (smokeEquipmentStage == 1 && gun != null && p.interactCooldown <= 0)
            { Send(new PlayerCommand { action="equip",arg=gun.id }); smokeEquipmentStage=2; }
            else if (smokeEquipmentStage == 2 && gun != null && p.weaponId == gun.id && p.interactCooldown <= 0)
            { Send(new PlayerCommand { action="partcraft",arg=(int)PartKind.RedDot }); smokeEquipmentStage=3; }
            else if (smokeEquipmentStage == 3 && gun != null && p.parts[(int)PartKind.RedDot] > 0 && p.interactCooldown <= 0)
            { Send(new PlayerCommand { action="attach",arg=gun.id,x=(int)PartKind.RedDot }); smokeEquipmentStage=4; }
            else if (smokeEquipmentStage == 4 && gun != null && gun.attachments[(int)AttachmentSlot.Optic] == (int)PartKind.RedDot && p.interactCooldown <= 0)
            { Send(new PlayerCommand { action="reload" }); smokeEquipmentStage=5; }
            else if (smokeEquipmentStage == 5 && gun != null && gun.magazine > 0 && p.interactCooldown <= 0)
            { Send(new PlayerCommand { action="gearcraft",arg=(int)GearKind.ClothHood }); smokeEquipmentStage=6; }
            else if (smokeEquipmentStage == 6 && helmet != null && p.interactCooldown <= 0)
            { Send(new PlayerCommand { action="equip",arg=helmet.id }); smokeEquipmentStage=7; }
            if (HasSmokeEquipment(p)) smokeSawEquipment=true;
        }
    }
}
