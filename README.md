# MaiChartManager-Alpha

某八个键音游谱面管理工具fork，兼容Sinmai-Alpha Mod

选择游戏 Package 后检测`Mods/Sinmai-Alpha.dll`，安装了 mod 时弹出兼容提示

### Sinmai-Alpha Mod适配
谱面设置的`启用谱面`按钮旁现有选择器，可选难度从游戏`Sinmai-Alpha/ExtraDifficulty`中读取，显示名称和圆点颜色由对应目录的`difficulty.json`决定。
同时可以设置谱面是否支持Hold夹Tap (宴会场功能)

### 转谱器选择

检查发现Alpha语法时，先询问“检测到谱面包含alpha内容，是否使用alpha转谱器？”。
选“是”后检查与实际转换都使用Alpha；选“否”都使用原版，按原版规则处理不支持的语法。
未检查到Alpha语法则直接使用原版MuConvert转谱

### Alpha预览

基本预览保持原有功能;Alpha预览启动独立的MajdataViewAlpha窗口，支持Alpha语法。
