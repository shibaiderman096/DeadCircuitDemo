# LandmarkArt — SVG 建筑标识、地板与室内道具

本目录美术全部由代码绘制 SVG（code-drawn, no image model），不是图像模型输出。
运行时 PNG、透明图集审查图、明暗底审查图和地板平铺图都由 SVG 经 resvg-py 栅格化。
4× 图由同一次渲染的 1× 结果做整数最近邻放大。Pillow/numpy 仅用于读取与独立验证，
不绘制、不修补、不合成任何 PNG。全部 SVG 无文字、嵌入位图、渐变、滤镜、
路径曲线、任意旋转或透明度；仅含整数坐标不透明矩形、分组和颜色类。

## 交付范围

- 57 个资源键：32 个 `landmark_sign_0..31`、6 个 `landmark_floor_0..5`、
  19 个 `landmark_prop_{char}`。
- 57 份独立 32×32 SVG，以及总图集 SVG、明暗底审查 SVG、3×3 地板平铺 SVG，合计 60 份。
- 图集 288×288，8 列，36px pitch。每个单元的 32×32 内容位于 `(2,2)`；
  单元四边各预留 2px 全透明区域，相邻内容之间共 4px 透明带。最后 7 个空单元全透明。
- 24 色完全沿用 `../ModularArt/Sources/palette.json`；不执行或导入旧生成器。
- 建筑标识供真实墙体建筑识别、门口或立面装饰使用；碰撞、真实墙体、占地、
  建筑生成和资源加载接入由主线程负责，此目录不包含这些逻辑。
- 导出不启动 Unity、Editor、Play、游戏或构建，不执行 Git 命令。

## 文件

| 路径 | 用途 |
|---|---|
| `landmark_svg.py` | 矩形美术定义与确定性 SVG/manifest 生成器 |
| `export.py` | 生成源、60 份 lint、SVG 渲染、独立校验与发布 |
| `verify.py` | 独立 XML/整数矩形像素真值、边距、平铺、metadata 和旧哈希校验 |
| `Sources/landmarks.svg` | 总图集可编辑 SVG |
| `Sources/landmark_*.svg` | 每个资源单独的 32×32 SVG |
| `Sources/manifest.json` | 完整建筑、地板、字符到资源键的语义映射 |
| `Sources/landmarks.layout.json` | 待发布 runtime JSON，结构与 modules.json 相同 |
| `Sources/palette.json` | 本次使用的既有 24 色快照 |
| `Sources/landmarks_review.svg` | 每个资源并列放在暗底和亮底；无文字 |
| `Sources/floor_tiling_review.svg` | 六类地板各自 3×3 平铺；无文字 |
| `Review/*_1x.png`, `Review/*_4x.png` | 原尺寸与 4× 最近邻审查图 |
| `Reports/protected_baseline.json` | 81个既有美术文件的保护哈希；禁止导出时自动重置，获授权的资源维护须逐项核对并显式更新 |
| `Reports/renderer_doctor.json` | 本机 resvg-py conformance doctor 结果 |
| `Reports/run_NNN/` | 每次导出的完整 lint/render 命令、独立校验及结果哈希 |
| `Reports/reproducibility_baseline.json` | 首次导出时的 73 个源/运行时/审查图文件哈希 |
| `Reports/run_NNN/repeat_export.json` | 后续导出对首次导出的逐文件字节一致性比较 |
| `Reports/latest_verify.json` | 最近成功导出的机器可读验证结果 |
| `Reports/visual_review.json` | 主智能体静态图审查记录与局限 |
| `codeart-meta.json` | SVG 源哈希、美术来源声明、渲染元数据和验证报告指针 |

2026-11-22按用户要求清理未使用素材时，核对所有保留切片的像素后，仅同步本次变更所涉及的保护哈希；历史run目录和未变动项目保持原值。world、icons、modules三份PNG导入设置的保护哈希此前落后于已提交的fba429a，本次核对其与HEAD一致后同步哈希，不修改导入设置或GUID。地标的57个资源、源图、运行图及复现基准均未改变。

运行时仅写入以下 4 个文件：

```text
Assets/DeadCircuit/Resources/Art/Expansion/landmarks.png
Assets/DeadCircuit/Resources/Art/Expansion/landmarks.json
Assets/DeadCircuit/Resources/Art/Expansion/landmarks.png.meta
Assets/DeadCircuit/Resources/Art/Expansion/landmarks.json.meta
```

JSON 严格使用 `width,height,sprites[{name,x,y,w,h}]`，`x,y` 是 PNG 左上角坐标；
不修剪、不旋转、不新增 runtime JSON 字段。`.meta` 延续模块图集导入约定，
最近邻过滤、关闭 mipmap、NPOT 不缩放，所有平台关闭纹理压缩。
GUID 初次确定性生成，重新导出时保留现有 GUID。

## 离线复现

本次确认可用的完整 PowerShell 7 路径：

```text
C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1118-904-0_26.1118.904.0_x64__3k8sg7r9htsxt/dependencies/native/powershell/pwsh.exe
```

已存在的 Python 环境（未安装依赖、未更改旧导出器）：

```text
C:/Users/man/AppData/Local/Programs/Python/Python312/python.exe
Python 3.12.10
Pillow 12.3.0
numpy 2.5.4
resvg-py 0.5.0 / resvg 0.48.1
```

在 PowerShell 7 中执行：

```powershell
Set-Location -LiteralPath 'D:/unity/DeadCircuitDemo'
$landmarkPython = 'C:/Users/man/AppData/Local/Programs/Python/Python312/python.exe'
& $landmarkPython -B Tools/ExpansionArt/LandmarkArt/export.py
& $landmarkPython -B Tools/ExpansionArt/LandmarkArt/verify.py --report Tools/ExpansionArt/LandmarkArt/Reports/manual_verify.json
```

可再运行一次 `export.py`，查看新 `Reports/run_NNN/repeat_export.json` 的
`identical: true`。报告每次放入新编号目录；不删除旧的审查证据或渲染结果。
如果有意修改本目录 SVG 定义，比较报告会列出发生变化的文件，旧基线仍保留。

导出器默认使用现成 skill：

```text
C:/Users/man/.codex/skills/codeart2d/scripts/svg_render.py
```

需要在别处复现时，用 `--renderer <svg_render.py 的实际路径>` 指定同版本渲染器，
并使用安装上述依赖的 Python。无需 bundled Python，也无需安装到项目外目录。
首次在新机器配置渲染器时可加 `--doctor`，结果保存在本目录的新 run 报告里。

独立调用像素 lint 的示例：

```powershell
& $landmarkPython -B 'C:/Users/man/.codex/skills/codeart2d/scripts/svg_render.py' lint --svg Tools/ExpansionArt/LandmarkArt/Sources/landmarks.svg --profile pixel
```

## 审查图索引

`landmarks_review_1x.png`（608×352）和 `landmarks_review_4x.png`（2432×1408）
每行 8 项，按下表从左到右、从上到下排列；每项左暗底、右亮底。
底部细线蓝色=建筑标识、棕色=地板、绿色=道具。透明间隔在查看器中可能呈黑色，
那是 PNG 的透明区域，不是画进 SVG 的黑色背景。

`landmarks_atlas_1x.png`（288×288）与 `landmarks_atlas_4x.png`（1152×1152）
保留真正图集布局和透明间距。

`floor_tiling_review_1x.png`（312×208）与 `floor_tiling_review_4x.png`（1248×832）
按两行三列展示 floor 0..5，每类为 3×3 平铺。

### 建筑标识（索引 0..31）

| 序号 | 资源键 | 建筑 | 识别特征 |
|---:|---|---|---|
| 0 | landmark_sign_0 | Residence | 黄铜钥匙、木制门牌 |
| 1 | landmark_sign_1 | Shop | 红白遮棚、购物篮 |
| 2 | landmark_sign_2 | Warehouse | 叠放运输木箱、托盘 |
| 3 | landmark_sign_3 | Workshop | 开口扳手、套筒、工作台 |
| 4 | landmark_sign_4 | Clinic | 横向医疗牌、红十字、脉冲块 |
| 5 | landmark_sign_5 | Farmhouse | 麦穗、木制农舍牌 |
| 6 | landmark_sign_6 | GasStation | 红色油泵、油表、软管 |
| 7 | landmark_sign_7 | PoliceStation | 蓝色盾牌、金色警徽 |
| 8 | landmark_sign_8 | School | 打开的课本、校钟 |
| 9 | landmark_sign_9 | Apartment | 竖向砖面门禁、六扇窗格 |
| 10 | landmark_sign_10 | Greenhouse | 阶梯玻璃顶、绿色幼苗 |
| 11 | landmark_sign_11 | Substation | 双陶瓷绝缘柱、母线 |
| 12 | landmark_sign_12 | Restaurant | 餐盘、刀叉 |
| 13 | landmark_sign_13 | Pharmacy | 绿十字、双色胶囊 |
| 14 | landmark_sign_14 | HardwareStore | 羊角锤、螺母、洞洞板 |
| 15 | landmark_sign_15 | Supermarket | 满载商品的双轮推车 |
| 16 | landmark_sign_16 | Library | 三本立书、展开的书卷 |
| 17 | landmark_sign_17 | FireStation | 消防头盔、水枪头、红底 |
| 18 | landmark_sign_18 | Hospital | 高立柱医疗牌、担架 |
| 19 | landmark_sign_19 | ResearchLab | 烧瓶、直角分子连接 |
| 20 | landmark_sign_20 | PowerPlant | 双冷却塔、闪电 |
| 21 | landmark_sign_21 | WaterTreatment | 澄清罐、进水口、净水出口 |
| 22 | landmark_sign_22 | RadioStation | 广播桅杆、两组信号波 |
| 23 | landmark_sign_23 | Bunker | 铆钉装甲舱门、窥视缝、锁轮 |
| 24 | landmark_sign_24 | LumberMill | 原木端面、齿形锯片 |
| 25 | landmark_sign_25 | MineOffice | 矿井井架、绞轮、矿车 |
| 26 | landmark_sign_26 | TrainDepot | 双轨枕木、红绿信号机 |
| 27 | landmark_sign_27 | Barn | 红色谷仓、草料阁、干草捆 |
| 28 | landmark_sign_28 | HuntingCabin | 鹿角、露营提灯 |
| 29 | landmark_sign_29 | RangerStation | 三棵松树、林道指示牌 |
| 30 | landmark_sign_30 | Motel | 路边床铺灯牌、独立绿色灯 |
| 31 | landmark_sign_31 | RecyclingYard | 回收箱、三段直角回收箭头 |

### 地板（索引 32..37）

| 资源键 | 材质 |
|---|---|
| landmark_floor_0 | 住宅木地板 |
| landmark_floor_1 | 商业瓷砖 |
| landmark_floor_2 | 公共灰地砖 |
| landmark_floor_3 | 工业格栅 |
| landmark_floor_4 | 乡村旧木泥地 |
| landmark_floor_5 | 特殊金属 |

### 道具（索引 38..56）

字符顺序精确为 `bsthnmpkglruvodfwea`。

| 字符 | 资源键 | 道具 |
|---|---|---|
| b | landmark_prop_b | 床 |
| s | landmark_prop_s | 货架 |
| t | landmark_prop_t | 桌 |
| h | landmark_prop_h | 椅 |
| n | landmark_prop_n | 柜台 |
| m | landmark_prop_m | 机器 |
| p | landmark_prop_p | 泵 |
| k | landmark_prop_k | 罐 |
| g | landmark_prop_g | 种植槽 |
| l | landmark_prop_l | 书架 |
| r | landmark_prop_r | 无线电 |
| u | landmark_prop_u | 发电机 |
| v | landmark_prop_v | 面板 |
| o | landmark_prop_o | 炉灶 |
| d | landmark_prop_d | 病床 |
| f | landmark_prop_f | 橱柜 |
| w | landmark_prop_w | 武器架 |
| e | landmark_prop_e | 终端 |
| a | landmark_prop_a | 水槽 |

## 验证覆盖和局限

1. 60 份 SVG 均执行 codeart2d `lint --profile pixel`。
2. 另用 XML 白名单验证仅允许 svg/style/g/rect，所有几何数字都是整数，
   所有颜色都来自既有调色板，全部 57 个独立源限制在 32×32 画布内。
3. 从 XML 矩形建立内存真值，逐像素与 resvg 输出比较；真值不保存为 PNG。
4. 检查 runtime JSON 字段、顺序、坐标、32×32 内容区、全部 57 个键唯一且有图。
5. 检查透明间距和空单元、无半透明像素、无调色板外颜色、无重复图块。
6. 六种地板均为全覆盖，左右及上下边缘逐像素相同；额外比较 54 次平铺实例。
7. 审查 SVG 与 PNG 逐像素相同，4× 审查 PNG 必须等于 1× 最近邻放大。
8. 元数据 GUID 无旧资源冲突、最近邻过滤、mipmap/压缩关闭。
9. 每次导出前后检查 81 个旧文件哈希；重复导出比较全部 73 个交付文件。

本美术子任务完成了上述离线检查和静态图审查，未启动 Unity 或游戏，未操作 Git。
主线程随后回报的实际集成验收与子任务自行执行的检查分别记录，见下节。

## 主线程验收回报与收尾

主线程于 2026-11-19 在本会话确认：

- 已目视通过 1×、4× review。
- 已检查离线 32 类真实生成布局拼合图及医疗、工业细节，效果通过。
- Unity 实际导入及加载检查为 57/57 个键，全部 32px、运行时 Sprite PPU 32、Point 过滤。
- `Play=false`，未进行 Play 运行验收。

以上集成结果来源为主线程回报，未由本子任务额外启动 Editor 重跑。
在收到回报后，已再次直接读取运行时 4 个资源文件并比较 SHA-256：
它们仍与本任务首次导出的字节完全相同，不覆盖主线程的验证成果。

可复现性结果：`Reports/run_002/repeat_export.json` 为 `status=pass`、
`identical=true`、`comparedFiles=73`、`changed=[]`。
最终独立检查为 `Reports/final_verify.json`；81 个保护文件 SHA-256 均未变化。
无离线校验 WARN/FAIL；后续 Play 运行与最终统一 Git 提交由主线程处理。
