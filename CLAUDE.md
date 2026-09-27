# 开发约定

Unity 6000.6.0f1，编辑器位于 `F:\Unity\Editor\6000.6.0f1\Editor\Unity.exe`。

- 当前是新版运行时生成架构。`TrackBuilder.BuildScene` 创建仅含相机、太阳和 GameBootstrap 的入口场景；不要恢复旧的序列化赛道或已移除的根目录脚本。
- Scripts 子目录：Core（网格/纹理/音效）、Kart（模型/物理/AI/特效）、Track（赛道/环境）、Items、Race、UI。
- 车辆碰撞体零摩擦，抓地和漂移由脚本实现。新版车辆使用球体+车身碰撞体，路面有网格碰撞以支持坡道与倾斜弯道。
- 漂移攒气持续保留，最多两瓶；Ctrl 使用氮气或道具。松开再按油门可在漂移结束/落地后的窗口触发小喷。
- `KartController.SetupItems` 必须双向绑定道具组件；AddComponent 时 Awake 立即执行，不能依赖组件添加顺序获取车主。
- 菜单与比赛相机共用 PostFX，HDR 输出须经过高亮压缩。不要直接输出 HDR + 泛光到显示缓冲区。
- 运行时材质用 Shader.Find，构建前必须调用 TrackBuilder.IncludeRuntimeShaders。
- 比赛记录按赛道、模式和圈数分别存储。
- `Application.runInBackground = true` 保持失焦时模拟运行。
- Unity MCP 桥接状态：`C:\Users\liyuc\.unity-mcp\unity-mcp-status-*.json`；项目提供 `Tools/unity_call.py`（stdio MCP）和 `Tools/unity_direct.py`（本机 6400 端口直接桥接），后者支持 `{"code_file":"路径"}` 读取 execute_code 代码文件。
- `KartGame → Build Race Scene` 重建入口；`KartGame → Quick Race (Play)` 快速测试。
- WebGL 构建入口 `KartGame.Editor.PagesBuild.Build`，输出 `Builds/WebGL`，无压缩适配 GitHub Pages。
