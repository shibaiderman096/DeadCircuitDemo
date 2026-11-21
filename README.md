# Dead Circuit Demo

Dead Circuit Demo is a Unity 6.6 top-down 2D pixel-art survival, automation, and co-op game demo. Start in a procedurally generated wasteland and build toward increasingly capable production systems. The project is under active development; features and validation coverage may change.

## What is here

- A fixed-step simulation core kept independent of Unity presentation code.
- Sparse 16×16 world chunks that can expand across positive and negative coordinates.
- Survival, exploration, combat, construction, power, production, and logistics systems.
- A staged research and industrial progression, with project documentation describing implemented and planned scope separately.
- Optional Netcode for GameObjects integration behind the `DEADCIRCUIT_NETCODE` scripting define.

## Getting started

1. Install Unity **6000.6.8f1**.
2. Open this folder as a Unity project and allow packages to resolve.
3. Open `Assets/DeadCircuit/Scenes/DeadCircuitDemo.unity` and press **Play**.

The project’s configured build scene is `Assets/DeadCircuit/Scenes/DeadCircuitDemo.unity`.

## Checks

The simulation checks are a standalone .NET project:

```powershell
dotnet run --project Tools/CoreChecks/CoreChecks.csproj --configuration Release
```

This runs the checks available in the current checkout; it does not replace testing the Unity scene or multiplayer behavior in the Editor.

## Art generation

The tracked game atlases have deterministic generation scripts. With Python and Pillow installed, run:

```powershell
python Tools/generate_art.py
```

## Project notes

- [Development outline](DEVELOPMENT_OUTLINE.md)
- [Biome generation](BIOMES.md)
- [Seasons and chunk worlds](SEASONS_CHUNKS.md)
- [Settlements](SETTLEMENTS.md)
- [Floors](FLOORS.md)
- [Landmark buildings](LANDMARK_BUILDINGS.md)

## License

No license is currently included. Public visibility on GitHub does not grant permission to reuse this project’s code or assets.

---

## 中文

《废土回路》是一个使用 Unity 6.6 制作的俯视角 2D 像素风生存、自动化与合作游戏演示。玩家从程序生成的废土世界起步，逐步建设更复杂的生产体系。项目仍在开发中，功能与验证范围可能继续变化。

### 项目内容

- 与 Unity 表现层分离的固定步长模拟核心。
- 可向正负坐标扩展的稀疏 16×16 区块世界。
- 生存、探索、战斗、建造、供电、生产与物流系统。
- 分阶段的研究与工业发展；文档会区分已实现内容和规划内容。
- Netcode for GameObjects 联机集成由 `DEADCIRCUIT_NETCODE` 脚本定义控制。

### 运行项目

1. 安装 Unity **6000.6.8f1**。
2. 用 Unity Hub 打开本目录并等待依赖包解析。
3. 打开 `Assets/DeadCircuit/Scenes/DeadCircuitDemo.unity`，点击 **Play**。

### 模拟核心检查

在 PowerShell 7 中运行：

```powershell
dotnet run --project Tools/CoreChecks/CoreChecks.csproj --configuration Release
```

这只运行当前检出版本中已有的核心检查，不能代替 Unity 场景或联机实测。

### 图集生成

安装 Python 和 Pillow 后，可运行 `python Tools/generate_art.py` 确定性地生成项目图集。

### 许可证

仓库当前未附带许可证。仓库公开不代表他人可以自由复用其中的代码或美术资源。
