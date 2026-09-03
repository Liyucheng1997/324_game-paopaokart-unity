# 跑跑卡丁车 Demo — 开发笔记

Unity 6000.6.0f1 卡丁车赛车游戏（QQ飞车/跑跑卡丁车风格）。编辑器位于 `F:\Unity\Editor\6000.6.0f1\Editor\Unity.exe`。

## 常用操作

- **重建赛道场景**：Unity 菜单 `KartGame → Build Race Scene`（TrackBuilder 会删除并重建 `Assets/KartGame/Generated/` 与 `Assets/KartGame/Scenes/RaceTrack.unity`）
- **MCP 驱动 Unity**：本仓库 `.mcp.json` 注册了 `UnityMCP`（stdio，`uvx --from mcpforunityserver mcp-for-unity`）。会话中若 MCP 工具未加载，可用 `python Tools/unity_call.py <tool> '<json>'`（或 `@args.json`）直接调用：`read_console`、`execute_menu_item`、`manage_editor`（play/stop）、`execute_code`（需 `"action":"execute"`）、`refresh_unity`
- 桥接就绪判断：`~\.unity-mcp\unity-mcp-status-*.json` 存在且 `"reason":"ready"`（端口 6400）。若编辑器切到了 HTTP 模式，可用 `-executeMethod MCPForUnity.Editor.McpCiBoot.StartStdioForCi` 启动编辑器强制回 stdio
- Unity 以管理员运行时启动会弹「Administrator Privileges Detected」对话框，点 Ignore Warning

## 玩法与设计约定

- 操作：WASD 驾驶，**Shift** 漂移，**Ctrl** 释放氮气，R 重开
- 氮气是 QQ飞车式：漂移蓄力**持久不清零**（`nitroChargeTime` 2.4s/瓶），满条转一瓶，最多存 2 瓶（左上角槽位），Ctrl 消耗一瓶获得 1.6s 喷射。改玩法时以 QQ飞车/跑跑卡丁车惯例为准
- AI（KartAI）：按前方弯道曲率刹车、墙面雷达避让、卡死自动倒车；32–55° 弯道漂移蓄力，直道 (<14°) 自动放氮气

## 关键技术决定（勿回退）

- **所有碰撞体零摩擦**（`Generated/Slick.asset`），抓地/漂移全部脚本实现——Unity 6 默认地面摩擦 ≈1.0，会吃掉油门推力导致 AI 原地不动
- **路面网格仅视觉**（无碰撞体），车跑在平面 Ground 上——薄网格碰撞体会把车楔在路面下
- 车身 BoxCollider center.y=0.33，使车根节点停在路面高度 0.02
- 程序化网格：路面三角形绕序已修正（法线朝上）；护栏双面需要复制顶点块（共享顶点会把法线平均成零而发黑）
- `RaceManager.Awake` 设置 `Application.runInBackground = true`——否则编辑器失焦时游戏冻结（会坑 MCP 自动化测试）
- 特效（KartEffects）全部运行时代码生成，不依赖序列化资源

## 测试技巧

- 用 `execute_code` 给玩家车挂临时 `KartAI` 代驾跑图（脚本固定转向会把车怼死在墙上：零速度时转向无效）；注意 AI 代驾会自动放氮气、跑完比赛后玩家 `ControlEnabled` 会被锁
- 截图：`PrintWindow` 抓 Unity 主窗口即可（见历史会话 capture_unity.ps1 模式）

## 素材

- 卡丁车模型：Kenney Car Kit（CC0），`Assets/KartGame/Kenney/`。玩家=kart-oopi，AI=oodi/ooli/oobi。TrackBuilder 自动按尺寸缩放适配（目标长度 2.2，底部贴地），FBX 缺失时回退为方块车
