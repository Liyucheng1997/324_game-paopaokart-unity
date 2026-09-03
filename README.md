# 跑跑卡丁车 Demo (PaoPao Kart)

Unity 6 制作的跑跑卡丁车 / QQ飞车风格赛车游戏 Demo。赛道、材质、特效全部由代码程序化生成，卡丁车模型使用 [Kenney Car Kit](https://kenney.nl/assets/car-kit)（CC0）。

![Unity](https://img.shields.io/badge/Unity-6000.6.0f1-black?logo=unity)

## 玩法

| 按键 | 功能 |
| --- | --- |
| WASD / 方向键 | 驾驶 |
| Shift（按住） | 漂移，为氮气蓄力 |
| Ctrl | 释放一瓶氮气（最多储存 2 瓶） |
| R | 重新开始比赛 |

- 3 圈计时赛，12 个检查点防抄近路，掉出赛道自动重生
- QQ飞车式氮气：漂移攒气**不清零**（底部常驻蓄力条），攒满一条变一瓶氮气（左上角显示），Ctrl 手动释放获得 1.6 秒喷射
- 3 个 AI 对手：过弯刹车、撞墙自救，同样会漂移攒气并在直道释放氮气
- 漂移轮胎痕迹、蓄力等级火花（橙→黄→蓝）、氮气排气火焰

## 快速开始

1. 用 Unity 6000.6.0f1（或更高的 Unity 6.x）打开本项目
2. 菜单栏 **KartGame → Build Race Scene** 一键生成赛道场景（`Assets/KartGame/Scenes/RaceTrack.unity`）
3. 点击 Play

## 项目结构

```
Assets/KartGame/
├── Scripts/          # 运行时脚本（车辆物理、AI、比赛管理、UI、特效）
├── Editor/           # TrackBuilder：程序化生成整条赛道场景
├── Kenney/           # Kenney Car Kit 卡丁车模型（CC0）
├── Generated/        # 构建时生成的网格/贴图/材质（可随时重建）
└── Scenes/           # 生成的比赛场景
```

## 技术要点

- 街机车辆物理：零摩擦碰撞体 + 纯脚本抓地/漂移（Unity 6 默认地面摩擦会吃掉推力）
- 赛道由 Catmull-Rom 样条生成路面网格、护栏、检查点与 AI 路径点
- 本项目通过 [MCP for Unity](https://github.com/CoplayDev/unity-mcp) 由 Claude Code 驱动 Unity 编辑器开发完成

🤖 Generated with [Claude Code](https://claude.com/claude-code)
