# 跑跑卡丁车 / QQ飞车风格赛车

Unity 6000.6.0f1 街机赛车项目，场景、精细卡丁车、车手、赛道和特效在运行时程序化生成。

## 开始游玩

用 Unity 打开项目，打开 `Assets/KartGame/Scenes/RaceTrack.unity` 并点击 Play。主菜单可以选择赛道、车辆、配色、竞速/道具模式、难度和圈数。

| 按键 | 功能 |
| --- | --- |
| WASD / 方向键 | 加速、刹车/倒车、转向 |
| Shift + 方向 | 漂移蓄气；反打方向结束漂移 |
| Ctrl | 竞速赛释放氮气；道具赛使用前一个道具 |
| 松开后重新按 W / ↑ | 漂移结束或落地后的小喷；倒计时结束附近按下可触发起步加速 |
| R | 回到赛道（比赛中） |
| Esc | 暂停、继续、重新比赛或返回菜单 |
| Enter | 主菜单开始比赛 |

## 已实现内容

- 两条立体赛道：森林村庄、雪山谷地；包含坡道、倾斜弯道、桥梁、隧道、跳台、加速带。
- 四款有不同驾驶参数的程序化卡丁车、八套配色，以及车手、转动轮胎和车辆动态姿态。
- 六车比赛，三档 AI 难度，1/2/3/5 圈选择；AI 赛车线、漂移、小喷、氮气、超车和脱困。
- 竞速赛：漂移进度持续保留，满槽储存一瓶氮气，最多两瓶。
- 道具赛：双槽道具、加速器、导弹、水弹、香蕉、护盾、磁铁，AI 会拾取并使用道具。
- 起步灯、镜头巡场、排名、圈速、小地图、逆行提示、复位、完赛结算和本地最佳成绩。
- 程序化引擎声、漂移/加速声、音乐、轮胎印、烟雾、火花与喷焰。
- HDR 高亮压缩、轻量泛光、速度模糊和暗角；主菜单与比赛共用曝光处理。

## 开发与构建

`KartGame → Build Race Scene` 重建入口场景；`KartGame → Quick Race (Play)` 直接测试比赛。赛道和材质不再依赖旧的 Generated 资源。

运行时脚本位于 `Assets/KartGame/Scripts/` 的 Core、Kart、Track、Items、Race、UI 子目录；着色器位于 `Assets/KartGame/Shaders/`。

WebGL 导出入口为 `KartGame.Editor.PagesBuild.Build`，输出到 `Builds/WebGL`，自动包含运行时着色器。`docs/` 已更新为新版网页导出；本地预览可运行 `python -m http.server 8765 --directory docs`，打开 http://127.0.0.1:8765。在线游玩：[GitHub Pages](https://liyucheng1997.github.io/324_game-paopaokart-unity/)。当前版本：v1.1。

旧 Kenney Car Kit 资源保留在 `Assets/KartGame/Kenney/`，许可证为 CC0；新版车辆使用程序化模型。
