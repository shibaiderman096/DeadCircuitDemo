# 聚落分区：gen5 / v10 接线约定

## 与自然生态的关系

`SettlementGeneration` 根据 `WorldState.seed`、`generation.preset` 和 `generation.cityDensity` 采样聚落。草甸、阔叶林、针叶林、荒漠、湿地和冻原由原自然生态算法采样。建筑须同时符合聚落用途权重和 `SiteBlueprint.biomeMask`。例如，荒漠中的乡村会从适合荒漠的蓝图中选择建筑。

固定存档序号：`Unspecified=0`、`HighDensityCity=1`、`LowDensityCity=2`、`HighDensitySuburb=3`、`LowDensitySuburb=4`、`Rural=5`。`SettlementCatalog.All` 为包含零值的只读目录；每条定义的字段均为 readonly，提供中文 `name`。零值保留给 gen1–gen4 和旧存档，不自动回填。

## 连续聚落带与道路

聚落场由世界坐标上 384 格与 128 格的平滑值噪声按 72% / 28% 合成。0.37、0.46、0.56、0.66 四个阈值将乡村到市中心划为五档连续带。格点采用向下取整，支持负坐标。地形、蓝图、朝向、聚落身份和资源内容由坐标、seed 和生成设置决定。实体 ID 按全局 nextId 在提交区块时分配，其数值随访问顺序变化。

Balanced 偏移 0，Wilderness 偏移 −0.14，Urban 偏移 +0.12；cityDensity=1 无额外偏移，=2 再加 +0.09，=0 全部视为乡村。调整预设和密度会同时影响城市覆盖面积与建筑出现率，不会重画已保存的区块。

| 分区 | 道路间距（格） | 路宽（格） | 基础建筑概率 | 主要用途 |
| --- | ---: | ---: | ---: | --- |
| 高密度市区 | 32 | 2 | 82% | 公寓、医院、超市、餐饮和药店 |
| 低密度市区 | 64 | 2 | 66% | 公寓与住宅混合、商业和公共服务 |
| 高密度郊区 | 128 | 2 | 48% | 住宅、小商店、诊所和五金店 |
| 低密度郊区 | 256 | 1 | 30% | 住宅、少量小商业、农舍与林间小屋 |
| 乡村 | 512 | 1 | 14% | 农舍、谷仓、温室、林间小屋与林业/矿业设施 |

路网共享全局原点，保留 x/y=32、33 的教程十字主路。512 格乡村干道贯穿所有分区；较密的局部道路按分区增加，可在分区边缘终止。道路采样不改变自然 biome；道路地表如何覆盖水面沿用地形生成的调用方规则。

建筑概率另乘 `(0.9 + 0.1 * cityDensity)` 以及预设乘数（Balanced=1、Wilderness=0.75、Urban=1.08）。最高概率为 97.416%，所有合法设置下五档都严格递减。这是每区块一次的蓝图尝试率，最终实际建筑数还受水面、道路保留区、可达性和实体预算影响；不是每格摆放率，也不强行填满区块。

## 建筑用途权重

32 类建筑都有至少一个可用的“聚落 × 生态”组合。选择先执行 biomeMask 和零权重排除，再按剩余整数权重抽取；不先均匀随机再事后改名。高密市区不放农业建筑，乡村不放公寓/医院/超市；工业设施可混合进五类聚落，林场和矿务所仍受原生态限制。

自然生成时，公寓、医院和超市进一步限定在高密度或低密度市区，郊区及乡村的候选权重为零。此限制只作用于遗迹/建筑蓝图的自然选址，不影响玩家手动建造。

以下为代表性权重（列依次为高密市区、低密市区、高密郊区、低密郊区、乡村），完整权重见 `SettlementGeneration.Weights`：

| 建筑 | 高密市区 | 低密市区 | 高密郊区 | 低密郊区 | 乡村 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 住宅 Residence | 10 | 42 | 100 | 95 | 15 |
| 公寓 Apartment | 100 | 62 | 0 | 0 | 0 |
| 医院 Hospital | 70 | 30 | 0 | 0 | 0 |
| 小商店 Shop | 34 | 38 | 45 | 25 | 5 |
| 超市 Supermarket | 48 | 28 | 0 | 0 | 0 |
| 农舍 Farmhouse | 0 | 0 | 8 | 24 | 85 |
| 谷仓 Barn | 0 | 0 | 5 | 18 | 75 |
| 林间小屋 HuntingCabin | 0 | 1 | 8 | 30 | 80 |
| 工坊 Workshop | 8 | 12 | 12 | 10 | 8 |

## API 与接线

新增文件 `Assets/DeadCircuit/Scripts/Core/SettlementGeneration.cs`：

- `SettlementKind Sample(WorldState world, int x, int y)`：按**世界格坐标**查询聚落，不要求区块已生成。
- `bool Street(WorldState world, int x, int y)`：gen5 道路覆盖层；调用方只在 generatorVersion >= 5 时调用。
- `double SiteChance(WorldState world, SettlementKind kind)`：分区与设置组合后的尝试概率，Unspecified 或未知分区为 0。
- `bool ShouldPlaceSite(WorldState world, int x, int y)`：以世界格坐标做稳定概率判定，蓝图生成使用区块中心 `(chunkX*16+8, chunkY*16+8)`。
- `int BlueprintWeight(SettlementKind settlement, SiteKind kind, BiomeKind biome)`：含原 biomeMask 排除规则的权重。
- `SiteBlueprint SelectBlueprint(WorldState world, int x, int y, SettlementKind settlement, BiomeKind biome)`：确定性加权选择，无候选时返回 null。

`SettlementCatalog.Street(world,x,y)` 为主线程当前道路接线提供同义转发入口。world 参数为空时抛 ArgumentNullException；缺少 generation 设置时采用 Balanced、cityDensity=1。自然生态采样仍由原 ChunkTerrain 负责，不由本模块重新解释。

生成与存档的接线分布在以下入口。

1. 在 `SiteState` 新增 `public SettlementKind settlement;`，保留旧值默认 0，并实现 v10 保存/复制/校验。
2. `CurrentGeneratorVersion` 与新世界切换为 gen5；gen3 / gen4 的街道和蓝图分支保持原样。
3. 在 `ChunkTerrain.cs` 的 Street 入口仅对 gen5 调用新 Street；不得改动自然 Sample 的噪声通道或参数。
4. 在 `Checks.Main` 注册 `SettlementChecks()`。

`ChunkTerrain.Sites.cs` 已在原 `AddBlueprintSite` 入口添加 `generatorVersion >= 5` 分流，调用独立的 `AddSettlementBlueprintSite`。入口判断之后的 gen4 原方法内容保留不变。gen5 在区块中心抽取概率和候选，落位时要求建筑锚点仍处在同一聚落且符合原 biomeMask，并将聚落写入 `SiteState.settlement`。旧 `district` 继续表达工业/市区/郊区/荒野归类；工业与五种聚落身份可以并存。建筑实体、门、箱子和入口通路继续沿用已有蓝图的占位和可达性规则。楼层由主线程独立实现，本模块不推定任何楼层字段或楼层算法。

## 纯 .NET 检查

`Tools/CoreChecks/SettlementChecks.cs` 测试目录序号、五类覆盖、连续性、同 seed 顺序无关、负数/极值坐标、预设/密度单调影响、道路密度、概率顺序与统计抽样、32 类用途筛选、六生态 × 五聚落的候选抽取与权重分布，并实际生成五类建筑、比较不同设置下的自然 biome 原始字节、逆序生成有符号区块对比几何与资源。采用公开接口，不使用 Unity、Editor、Play Mode 或游戏构建。

2026-11-20 增加公开权重边界检查：公寓、医院、超市在 biomeMask 允许的生态中仅对两种市区分区有权重，郊区与乡村在全部十种生态下均为零；定向聚落检查 12/12 通过。

所有 fixture 显式指定 generatorVersion；旧版 golden 为 32 类蓝图基线（9cc7b4d）接入 gen5 前捕获的输出，分别锁定 gen3 与 gen4，涵盖正负 seed、三种 preset、三个 density 共 576 个区块的地形、生态、蓝图、门、墙、箱子、资源量、血量和 ID。新楼层字段不属于旧版本输出指纹。字段采样支持 int 极值；实际 gen5 地形 fixture 留在地面世界范围，楼层保留坐标域由主线程楼层检查覆盖。

注册后可运行以下命令。

```powershell
& 'D:/unity/6000.6.8f1/Editor/Data/DotNetSdk/dotnet.exe' run --project Tools/CoreChecks/CoreChecks.csproj -c Release -- settlement
```

注册前，使用仓库外临时 .NET 项目，引用 Core 源文件与本检查文件，以临时 partial Checks 入口调用同一套检查，不改仓库 Program.cs。
