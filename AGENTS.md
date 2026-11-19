# Dead Circuit Demo Agent Guide

## 项目总览

这是一个 Unity 6.6 的 2D 像素风生存与自动化建造合作演示项目。当前构建场景是 `Assets/DeadCircuit/Scenes/DeadCircuitDemo.unity`。项目使用 URP、uGUI、TextMesh Pro、新输入系统和 Netcode for GameObjects。

- `Assets/DeadCircuit/Scripts/Core/DemoState.cs` 定义世界、幸存者、设备、物品、丧尸及玩家指令等状态类型。
- `Assets/DeadCircuit/Scripts/Core/WorldSimulator*.cs` 是不依赖 Unity 的模拟核心，采用 50 毫秒固定 tick。新建世界使用可向正负坐标扩展的稀疏区块；旧世界保留 64×64。涵盖战斗、建造、供电、生产、物流和存档校验。
- `Assets/DeadCircuit/Scripts/GameBridge.cs` 连接 Unity、模拟器、存档与联机传输，是游戏指令进入模拟器的入口。联机代码以 `DEADCIRCUIT_NETCODE` 条件编译，改动时检查 Standalone scripting define 和 Package Manager 配置。
- `Assets/DeadCircuit/Scripts/Presentation/DemoWorldView.cs` 绘制世界和小地图；`DemoHud.cs` 创建 HUD、菜单及输入界面。
- `Assets/DeadCircuit/Resources/Art/` 保存图集和地形素材；`Tools/generate_art.py` 是这些素材的确定性 Pillow 生成源。
- `Assets/DeadCircuit/Scripts/DemoRuntimeChecks.cs` 提供 opt-in 编辑器／开发版运行时 UI 检查；`Tools/CoreChecks/` 是独立 .NET 核心模拟检查程序。

场景、模拟核心、联机桥接、表现层和生成素材各有明确边界；改动前先找到负责该行为的源文件，再沿调用路径核对行为。

## 实施约定

1. 世界状态以 `WorldSimulator` 为唯一权威。玩家操作通过 `GameBridge.Send` 和模拟器指令处理；HUD 与世界视图读取状态并呈现，通过桥接请求改变世界。
2. 保持模拟核心的 Unity 无关性；纯规则、状态和算法优先落在 `Core/`，UI 与 Unity API 留在桥接或表现层。
3. `BuildKind`、`ItemKind` 等枚举整数值保留已有序号。当前保存格式v13、工业物品40槽、建筑目录49项；新世界启用原始、水力及蒸汽研究，generator8生成含煤、石灰石与耐火黏土的群系资源。按用户2026-11-20指示，后续开发不考虑旧存档：不为新阶段新增迁移、历史研究旁路或旧世界补矿；验证新世界与当前格式的校验、深拷贝和恢复。既有历史代码不在本次主动清理范围。原始／水力／蒸汽研究与生产链见 DEVELOPMENT_OUTLINE.md；聚落和楼层见 LANDMARK_BUILDINGS.md、SETTLEMENTS.md 和 FLOORS.md。
4. 保持整数全局格坐标与固定 50 毫秒 tick。新世界每区块16×16，使用 WorldTiles 查询地形/生态、64位全局键与负坐标向下取整；width/height=64仅代表原教程元数据，不能作为区块世界边界。旧世界维持64×64。设备占地、旋转、供电覆盖及物流须覆盖跨区块、负坐标和守恒行为；详见 SEASONS_CHUNKS.md。
5. 需要重生成图集时运行 `python Tools/generate_art.py`；脚本会写入 `Assets/DeadCircuit/Resources/Art/`。编辑生成素材时同步维护生成脚本。
6. 默认使用 PowerShell 7 处理 Windows 命令；任务开始时重新核验编辑器、命令行、Git 和运行时状态。Unity Editor 命令需明确指定目标项目。
7. 检查当前目录的 Git 状态后再改文件；提交时仅包含本次任务修改，保留其他未提交工作，绝不从此项目推送到远程。

## 工具与验证

项目仓库声明 Unity MCP for Unity（`com.coplaydev.unity-mcp`）；只有当前会话实际提供对应 MCP 工具时才调用它。若 Unity CLI 已安装，可先运行 `unity status --project-path 'D:/unity/DeadCircuitDemo'` 查看项目编辑器是否连接，再用 `unity command --help` 查看此版本可用的 Editor 操作。执行 Editor 命令时传入 `--project-path 'D:/unity/DeadCircuitDemo' --caller plugin --skill unity-cli`，并在操作前确认目标、命令参数和潜在写入。Unity CLI 工具目录随连接的 Editor 及安装包变化，以实时列表为准。

主要可调用工具：

- Unity Editor：项目版本见 `ProjectSettings/ProjectVersion.txt`；运行或打包前检查活动 Editor 及构建设置。
- Unity CLI：通过 shell 驱动已连接的 Editor、查询场景与资产、查看控制台、运行检查或构建。调用可用子命令前先读取其帮助。
- .NET：`Tools/CoreChecks/CoreChecks.csproj` 目标框架是 `net8.0`。有 .NET 8 SDK 时运行 `dotnet run --project Tools/CoreChecks/CoreChecks.csproj --configuration Release`。传入参数会按检查项名称作不区分大小写的子串过滤。
- 若主机只有 .NET 10 runtime，可在 PowerShell 7 设置 `$env:DOTNET_ROLL_FORWARD = 'Major'` 后运行 `dotnet Tools/CoreChecks/bin/Release/net8.0/CoreChecks.dll`。这只执行已有程序集，不会从源码重建；重跑前先确认该程序集不早于模拟核心和检查源码。
- Python 与 Pillow：运行 `python Tools/generate_art.py` 生成项目图集。生成前确认只需更新 `Assets/DeadCircuit/Resources/Art/`。
- 在线 Editor 命令按需覆盖资产导入、场景、GameObject、组件、脚本、运行／Play Mode、控制台、视图截图、测试和构建；通过 `unity command --tags --project-path 'D:/unity/DeadCircuitDemo' --caller plugin --skill unity-cli` 发现当前连接暴露的完整分类。实际命令名、包和连接状态随环境变化。
- 构建配置：构建场景登记于 `ProjectSettings/EditorBuildSettings.asset`；保留原有目标、场景和网络 define。

环境会变化。2026-11-19 确认 Unity CLI 1.0.1 报告为最新版本，本机 Unity Editor 为 6000.6.8f1，Unity CLI 能连接该项目。Unity 随附的 `D:/unity/6000.6.8f1/Editor/Data/DotNetSdk/dotnet.exe` 提供 .NET SDK 8.0.318；可用它从源码运行 `run --project Tools/CoreChecks/CoreChecks.csproj -c Release`。优先从源码重建，避免把过期二进制的检查结果误当作当前验证。

## Skills 路由

仅在当前会话可用时加载相应 skill，并遵循 skill 自身步骤。

- `unity:unity-cli`：任何 Unity CLI、Editor、场景／资产操作、检查或构建任务。
- `unity:ui`：开始 HUD、菜单、Canvas 或其他 Unity UI 改动前；本项目 HUD 使用 uGUI 和 TextMesh Pro。
- `unity:ui-ugui`：`unity:ui` 确认 UI 工作属于 uGUI 后，编辑场景 Canvas、RectTransform 或现有 HUD 时。
- `unity:2d-pixel-perfect`：明确排查像素模糊、抖动或像素对齐问题时。场景使用连续相机缩放，世界视图明确不使用 Pixel Perfect Camera；修改前先保留并验证相机缩放行为。
- `generate2dsprite`：委托或制作新的游戏用角色、道具或特效图像时；要保持现有图集时，也同步更新 `Tools/generate_art.py`。
- `mattpocock-skills:tdd`：修复模拟规则或增加核心行为时，为该行为补充针对性的可执行检查。
- `unity:unity-package-management`：仅在 Unity Package Manager 配置或包依赖确需变更时。
- `unity:optimize-web`：任务目标明确包含 WebGL／Web 平台构建优化时。
