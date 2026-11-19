# Dead Circuit 独立扩展资源 · 第一批

## 实际交付

本批实际资产为 **30 个原创合成 WAV、26 类事件、3 个循环**。覆盖要求的全部20类：点击、打开、关闭、装备、卸下、装填开始、装填完成、手枪、霰弹枪、步枪、狙击、挥刀、命中、采集、浇水、播种、收获、雷、风、雨。另提供电火花、爆炸、碎石脚步、拒绝提示、拾取、机械循环。

**图像交付数为0。** 不存在可接入的装备/UI/群系/建筑/FX图像，也没有伪造 `atlas.json`。装备与配件只保存在真实图像请求提示词内，不能当作已完成的素材。

- 运行时音频：`Assets/DeadCircuit/Resources/Audio/Expansion/*.wav`。
- 音频接入清单：同目录 `audio_manifest.json`；包含每个文件的 `resourcePath`、事件分类、时长、loop、推荐音量、设计、随机种子、SHA256。
- 原始可复现源：本目录 `synthesize_audio.py`。依赖 NumPy；没有录音、音频模型或外部采样。
- 独立检查器：`verify_audio.py`。
- 过程证据：`Logs/ExpansionArt/route_attempt.json`、`route_receipt.json`、`audio_qa.json`、`batch_report.json`、`reproducibility.json`。

## 图像路由：真实失败，未降级

已按 `generate2dsprite` → `generate2dmedia` 使用真实 `route_media.py image` 请求，由 `codex-cli 0.162.1` 执行。runId：`99448171854444199b3575556412839c`；调用约26.1秒，在110秒内部/120秒外部限时以内。结果：

```text
error: local:codex-cli: RATE_LIMIT: the Codex CLI reports a quota or rate limit
exit_code: 1
```

没有产生图像；不能声称图像模型完成了生成。退出码不是 `no-route(3)`，因此按授权条件 **没有使用 codeart2d**，也没有二次尝试。`probe_route.py` 加了已有记录保护，不能自动重试。只调整临时目录和日志路径；没有更改认证、模型或 API keys。Forge ledger 留在本目录 `.forge/`，CLI临时文件与诊断数据库留在 `Logs/ExpansionArt/` 的忽略目录中，未加入提交。

`equipment_prompt.txt` 是实际提交的1024×1024请求源，按 `EquipmentCatalog` 保留19装备及7配件顺序。原请求8×8布局是生成用源布局，**并非已交付的运行时图集**。下一批成功后再缩放、切片和生成真实 `atlas.json`。

## 音频规格与接入

所有文件均为 **mono、44100Hz、PCM16 little-endian**。短事件0.085～1.50秒，雷4.50秒；风/雨6秒，机械4秒。音效是原创程序合成，**不是实录**。不同事件采用不同瞬态、滤波频带、共振模态、滑音和时序；随机噪声由每条独立种子复现，没有把同一声音改名重复交付。

Unity接入由主负责：可使用清单内 `Resources.Load<AudioClip>(resourcePath)` 的路径（省略扩展名）；保留原采样率、PCM、不做导入归一化。短事件建议 Decompress On Load；循环文件按 `loop: true` 设置 AudioSource.loop，不插入静音。风雨推荐低音量分层。最终音量与混音由实际游戏审听确定。

装填开始0.62秒和完成0.48秒是两个独立事件声，不代表游戏装填耗时；只能在现有逻辑的开始/完成事件触发。步枪文件为单发，沿用现有射速，不内置连发节奏。四种枪声的压力包络、频谱、机械层、尾声不同，具体数值见QA报告。

## QA与限制

运行 `verify_audio.py` 检查每条实际WAV文件头、格式、持续时间、幅度、直流偏置、文件/PCM唯一性、清单hash及循环边界；报告包含每条频谱重心与四频带能量。30个文件无空白、无重复PCM、无削波，最大峰值约0.870（低于0.88限值）。三个循环的首尾差及首尾局部斜率均为0 PCM LSB，边界10ms没有静音缺口。

已重复生成并逐文件校验SHA256确定性。**没有听音验收**：数值通过不等于听感通过，主仍需审听并在游戏内确认混音与触发。没有构建、启动Windows游戏、UnityPlay、GUI操作或Unity导入测试。

没有生成PNG，RGBA真alpha、64/32整网格、透明gutter、跨格spill及 `sheet_qc.py spill` 均为 **未运行/不适用本批**，不能写作通过。整套全量重绘仍待图像路由恢复或另外获得降级授权。

Doctor诊断中的3条WARN原文如下（来源均为 `Logs/ExpansionArt/doctor.json`；这些环境提示没有数值阈值）：

```text
python.interpreter [WARN]: this is the Microsoft Store alias, which may install or launch a different Python
python.path [WARN]: other python.exe on PATH: ~/AppData/Local/Programs/Python/Python312/python.exe; ~/AppData/Local/Microsoft/WindowsApps/python.exe
deps.scipy [WARN]: not installed; needed for faster labelling and keying (forge_core falls back to numpy)
```

实际执行始终使用指定完整Python路径（3.12.14），音频生成/QA只依赖NumPy，不安装或更换解释器/依赖。Doctor另报7条MISSING：resvg-py、五家图像API key和Grok CLI；Codex图像工具为UNKNOWN/未验证成功。详情原样保留于报告；不将环境状态写成全通过。

## 后续图像接入约定（仅规范，非已交付图像）

- 墨绿/冷青/琥珀工业生存风；参考现有 `Tools/generate_art.py` 的 INK `#192829`、DARK `#253b3b`、STEEL `#536a68`、LIGHT `#8ba39a`、CREAM `#ddd5ac`、RUST `#9b5338`、ORANGE `#e59a50`、TEAL `#80d8bc`。不改原主角。
- 装备19项及parts7项均遵循现有枚举顺序，最终图标64×64（或32×32）RGBA，透明gutter至少4px。不得只换颜色伪装不同物品。
- 最终 `atlas.json` 应逐项包含 `name,x,y,width,height,pixelsPerUnit,pivotX,pivotY`，PNG矩形原点为左上，pivot为矩形内归一化左下；中心图标pivot `(0.5,0.5)`，PPU沿用40。只有真实交付的图像才能列入sprites。
- UI panel/button/slot/windowframe应明确9-slice border；地面需按草甸、阔叶林、针叶林、荒漠、湿地、冻原区分；野外灌木、农作物、12类建筑屋顶标志与天气/武器小FX全部待做。FX要含frame顺序、帧时长、loop与共同pivot。

## 命令

在项目根目录使用指定完整Python路径运行，所有生成输出均在授权子目录：

```powershell
$expansionPython = 'C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1118-904-0_26.1118.904.0_x64__3k8sg7r9htsxt/dependencies/python/python.exe'
& $expansionPython -B Tools/ExpansionArt/synthesize_audio.py
& $expansionPython -B Tools/ExpansionArt/verify_audio.py
```

资源不依赖许可不明素材；源代码、参数、随机种子和清单随附，供本项目继续修改与接入。
