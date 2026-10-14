# Sinmai-Alpha 集成

选择游戏 Package 后检测 `Mods/Sinmai-Alpha.dll` 的程序集名称。安装了 mod 时弹出兼容提示；可以关闭当前提示或选“不再显示”，设置页可以重新打开提示。
扩展音符和特效由 MuConvert 转为原生游戏谱面及扩展命令，真正的上机输入与判定由 Sinmai-Alpha 提供。

谱面设置的“启用谱面”旁使用现有 Select 组件。可选难度从游戏 `Sinmai-Alpha/ExtraDifficulty` 的各子目录动态读取，显示名称和圆点颜色由对应目录的 `difficulty.json` 决定；新增难度无需改 MCM 代码。
每个难度使用同名 `.ExtraDifficulty.flag`；不会修改难度索引、流派、版本或成绩。旧整曲标记仍兼容 MASTER。

## 导入转谱器选择

普通导入和 maidata 替换使用 `MuConvert/` 中未修改的原版 MuConvert 子模块（`MuNET-OSS/MuConvert@0d4e697`，v0.4.2）；Alpha 转换器独立放在 `MuConvert-Alpha/`。两者分别生成 `MuConvert.dll` 与 `MuConvert.Alpha.dll`，模型、解析和输出彼此独立。导出及普通预览也按 MA2 实际内容选择对应转换器；Alpha 预览桥接器只引用 Alpha 转换器。
检查发现 Alpha 音符或演出语法时，先询问“检测到谱面包含alpha内容，是否使用alpha转谱器？”。
选“是”后检查与实际转换都使用 Alpha；选“否”都使用原版，按原版规则处理不支持的语法（包括原版宽松解析的警告行为），不静默回退。
批量导入逐曲保存本次选择；标题、作者及注释内的示例不会触发提示。标准 MA2 直接替换不经过转换器。
原版转换不会生成 Alpha 原始谱旁车；覆盖旧 Alpha 转换结果时移除旧旁车，避免预览误用。

## Alpha 预览

新版导入保存 `.alpha-preview.json`，包含原始 inote、实际谱面偏移和 MA2 SHA-256。
MuConvert 命令行转换 maidata 也生成相同数据。MA2 修改后旧旁车自动失效，直接读取当前 MA2；不要求重新导入。
基本预览保持原有功能；“Alpha 预览”启动独立 MajdataView 窗口，支持扩展音符、SV/HS 和演出表。
音乐由音频同步组件播放；窗口内停止按钮回到开头并暂停，关闭预览或退出 MCM 时释放本次播放器，不操作用户正在用的制谱器。
播放器按需启动，闲置时不会占用 Unity 内存。肆意生长实测预览峰值工作集约 0.9 GB；不能把完整 Unity 预览器视为低内存组件。

预览是编辑器演出，不能验证实际上机判定。支持 maidata 特效、随曲资源及 MA2 中的 MEDIA2 媒体时间轴；实际媒体仍须随谱面安装。
旧 MA2 无旁车时直接恢复原生音符、SSS、独立流、SV/HS、噪域及特效表。新导入优先使用哈希一致的原始数据，解析失败则回退到当前 MA2。瞬时（零长）噪域和滑条采用最小可解析时长预览。

点击 Alpha 预览立即打开居中窗口，使用普通浏览器的字体和旋转箭头显示加载状态，按钮旁不显示等待文字。实际加载分为七阶段：启动播放器、读取谱面、准备媒体、解析音符与特效、载入音频、建立谱面场景、绘制首帧。计数统一为 `Loading (n/7)`，同时显示当前步骤；快步骤自然跳过，不刻意等待。旋转遵循普通预览原动画：顺时针一秒一圈，每圈先加速再减速；宿主窗口和播放器共用同一条曲线。加载动画由独立界面线程驱动，Unity 解码音频或建立音符时也会旋转。宿主保留加载遮罩，收到播放器完成首帧绘制的信号后才切入画面，避免提前撤掉遮罩露出黑屏。完成后保持暂停，点击播放才开始。窗口关闭会取消加载并释放本次播放器；全屏和大小调整由同一个宿主窗口处理。外框遮罩固定为 1.0（全黑）。进度数字使用浏览器 Minimoon 字体，播放／暂停／停止图标保留原有留白和尺寸。窗口内复用普通谱面浏览器的 UI 层级、按钮贴图、字体和设置控件：底部播放/暂停、停止、进度条及倍率，右上全屏和 Settings。DISPLAY 包含音频偏移、按键/Touch 流速、背景遮罩和 Combo；VOLUME 包含 BGM、Answer、Judge、Slide、Break。默认流速 7、遮罩 0.6、BGM -3 dB、Answer +6 dB，其余 -10 dB。设置保存到用户 AppData 的 MaiChartManager/alpha-preview-settings.json；正音频偏移表示音乐延后。预览音频按 CueId 解析，以只读方式加载 ACB/AWB，兼容只读游戏资源。最近的错误和播放器日志位于系统临时目录 MaiChartManager/AlphaPreview。

## 构建完整包

1. 初始化子模块（`git submodule update --init --recursive`）。`MuConvert` 保留原版子模块，`MuConvert-Alpha` 指向独立的 `SaltNya/MuConvert-Alpha` 仓库，无需覆盖原版。先推送转换器的对应提交，再推送管理器对该提交的引用。原版 Simai.g4 的 ANTLR 4.13.1 生成文件位于 `Packaging/MuConvertParser`，由 `Directory.Build.targets` 引用，避开中文路径下的 Antlr4BuildTasks 问题，不修改原版子模块。
2. `pnpm install --frozen-lockfile`，在 `MaiChartManager/Front` 中 `pnpm build`。
3. 运行 `Packaging/PrepareAlphaPreview.ps1 -MajdataApp '<MajdataAlpha安装的App目录>'`。
   使用 MajdataAlpha 0.6.0 时，还需传入 `-AdjacentParser '<旧版已支持相邻键直线的MajdataEdit.dll路径>'`，以保留相邻键扩展。旧解析器必须在输出目录外，避免复制新版时覆盖输入。
   脚本核对已适配的解析器/播放器版本，只修改本项目下副本。
   解析器和 Unity 播放器放在外部 `AlphaPreview` 目录，不提交大型二进制到 Git。浏览器 UI 资源保存在 `Packaging/StandardPreviewUI`，由脚本复制到 `AlphaPreview/UI`，不嵌入 DLL。
4. `dotnet publish MaiChartManager/MaiChartManager.csproj -c Release -r win-x64 --self-contained true -o artifacts/MCM-Alpha`。

Sinmai-Alpha mod 项目单独位于 `C:/项目2/Sinmai-Alpha`，安装包布局及构建方法见其 README。
