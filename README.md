# DeskPlant 桌面盆栽

从 [GitHub Releases](https://github.com/2832141205gzc-pixel/deskplant/releases) 下载 `DeskPlant-Windows.zip`，解压后双击 `DeskPlant.exe`。支持 Windows 10（1607 或更新）和 Windows 11。也可以按下文的构建说明生成 `output/windows/DeskPlant.exe`。盆栽第一次出现在主屏幕右下方。

- 按住盆栽拖动。透明区域的鼠标操作会传到下面的窗口。
- 点击「浇水」，蓝色水壶会倾斜，将水滴倒向盆土，水分增加最多 25 个百分点。水分每小时下降 4 个百分点，关闭应用期间也会计算。
- 右键点击盆栽，选择「查看生长进度」查看当前品种的阶段、百分比和速度。
- 成熟后点盆栽下方的「移入花园」，也可以通过右键菜单收纳。花园里每株各占一块地，同品种相邻摆放。
- 在花园点「选种」，再点击所选品种的「种下」。观赏植物有玉树、虎尾兰、紫叶酢浆草、芦荟、薰衣草和矮生月季。任意时候都可以右键选择「打开花园」查看地块。
- 花园使用石砌花圃，果园使用木框地块，可采摘的地块带暖色边框。仓库按果实库存和种出的植物分架陈列。[Figma 界面设计](https://www.figma.com/design/ByNoVCJh2fN8jZEeO7GZcl)
- 花园右上角的「设置」可切换「动态效果」和「静态效果」。动态模式下，桌面和花园里的植物会轻微摆动。静态模式保留浇水的水分更新和按钮提示，偏好在重启后保留。
- 从花园种下鸡蛋果、菠萝、蓝莓、草莓、金桔或矮生小番茄，结果后点「移入果园」。同品种地块相邻摆放，每块地有一株作物。
- 成熟作物移入果园时带一颗熟果。随后果实逐颗成熟，满株后暂停。鸡蛋果最多 3 颗，每颗 25 分钟。菠萝最多 3 颗，每颗 30 分钟。草莓最多 4 颗，每颗 11 分 15 秒。金桔最多 4 颗，每颗 17 分 30 秒。蓝莓最多 5 颗，每颗 12 分钟。矮生小番茄最多 5 颗，每颗 10 分钟。
- 地块显示熟果数和下一颗的倒计时。点「采摘」收走这株全部熟果，枝上的青果继续按原倒计时长。「全部采摘」会收走果园里所有熟果。满株采完后，下一颗重新开始计时。动态模式下会看到手套采摘。
- 关闭应用时继续计算结果时间。打开「长满自动采摘」后，每株长满才会收，重新打开应用会补收离线期间长满的轮次。仓库按品种显示果实堆，插图最多画 24 颗，标签显示准确库存。仓库也显示花园和果园的植物数量。
- 可在桌面右键选「铲除盆栽」，也可以在花园或果园的卡片上铲除一株。植物进入「回收站」，7 天内可恢复到原位置。桌面成长和果园结果倒计时会在回收期间暂停，到期后自动清理。
- 「图鉴」是一本双页植物册，收录 12 种植物的简介、学名和种植记录。通过「上一页」「下一页」、键盘 Page Up / Page Down 或目录切换品种。左页展示选中的生长阶段，种到对应阶段后解锁图版。现有盆栽、地块、仓库和回收站可补录曾经达到的阶段。
- 桌面盆栽水分低于 25% 或手动采摘的果园有熟果时，Windows 托盘会提醒。在桌面或托盘右键菜单的「提醒设置」可分别关闭。
- 从「仓库」进入「出售果实换金币」，选择品种和数量出售。金币可在「商店」购买花盆和水壶，购买后点击「装备」。花盆款式应用到桌面盆栽，水壶款式用于动态浇水效果。

## 测试版

双击 `output/windows/DeskPlant-Test.exe`，右键选择「打开测试工具」。可以指定品种和成长阶段、增减花园植物和果园地块，以及增减仓库果实。「每块地成熟一颗」和「让全园立即长满」可以快速预览果园的结果与采摘。替换桌面盆栽时，原盆栽会移入回收站。

测试版存档在 `%LOCALAPPDATA%\DeskPlant-Test\state.txt`，可以与正式版同时运行。在 PowerShell 中用 `./build.ps1 -Test` 构建测试版。
- 拖动浇水按钮左边的双箭头，向左放大，向右缩小。范围为 60% 至 180%。右键菜单「调整大小」可以直接选用固定比例。
- 通过右键菜单可移回右下角、隐藏或退出。盆栽隐藏后，可以从 Windows 通知区域恢复，或再次运行 `DeskPlant.exe`。

水分达到 65% 时以 2 倍速度生长，25% 至 65% 以下为 1 倍，低于 25% 为 0.5 倍。持续保持 2 倍速度时，约 45 分钟进入成长中，约 3 小时成熟。离线时会根据水分随时间下降的情况分段计算成长。

位置、水分、进度、大小、当前品种和花园收藏保存在 `%LOCALAPPDATA%\DeskPlant\state.txt`。上一版的玉树会保留已有成长进度和大小。盆栽使用系统自带的 .NET Framework，图片已嵌入可执行文件，启动时可以离线使用。

## 构建

在 PowerShell 运行 `./build.ps1`，将使用 Windows 自带的 .NET Framework C# 编译器构建 `output/windows/DeskPlant.exe`。

## 盆栽素材

玉树、虎尾兰和紫叶酢浆草使用内置 ImageGen。芦荟、薰衣草、矮生月季、金桔和矮生小番茄由 Figma AI 绘制，素材均保留透明通道。[Figma 素材板](https://www.figma.com/design/HVftj9R3IA5YpCQaHNPVI2?node-id=2-2)

浇水壶使用项目提供的 `assets/watering-can.png`。

`assets/jade-plant.png`（成熟）

> Create one original desktop plant sprite on a genuinely transparent background. A compact jade plant with thick, overlapping rounded green leaves, in a softly textured warm terracotta pot. Tender hand-painted botanical illustration with tactile ceramic, subtle paper grain, and soft natural light. Front-facing with a slightly elevated view of the pot rim, centered compact silhouette, all leaves and the pot fully visible with generous transparent padding. Keep the illustration legible at 180 pixels tall. No ground, cast shadow, lettering, icons, frame, or other objects.

`assets/jade-seedling.png`（幼苗）

> Edit the reference sprite to show the exact same potted jade plant as a newly rooted seedling. Keep the original terracotta pot, its shape, front-facing angle, color, tactile watercolor texture, dark soil, lighting, and bottom center. Replace only the mature foliage with one short slender green stem and two small pairs of tender rounded jade leaves, the upper pair slightly smaller. The seedling above the rim must be less than half the pot's height. Keep the whole pot and seedling visible and isolated on genuine transparency. Preserve the soft hand-painted botanical look. No floor, cast shadow, background, labels, or extra objects.

`assets/jade-young.png`（成长中）

> Edit the mature jade plant reference into its distinct intermediate growth stage for the same desktop sprite set. Keep exactly the same terracotta pot, dark soil, front-facing camera angle, proportions, position, soft watercolor texture, and lighting. Replace only the dense mature canopy with a young upright jade with two uneven branching stems and around eight substantial rounded leaves, clearly open spaces between branches. Foliage above the rim should be about three quarters of the pot body height and clearly shorter and sparser than the image reference. Keep all edges visible and the isolated plant on genuine transparency. No ground, background, cast shadow, text, or additional objects.

`assets/snake-plant-stages.png`（虎尾兰的两个阶段）

> Create a single two-frame sprite sheet for a cozy Windows desktop gardening app, landscape canvas, exact two equal-width columns of the same potted snake plant (Dracaena trifasciata, 虎尾兰) at two ages. Left column: juvenile, three small folded upright blades. Right column: mature, seven to nine longer upright sword-shaped blades, variegated dark-sage zigzags and muted cream edges. Both frames have the very same short matte ivory ceramic pot with a subtle vertical rib texture, same pot width, same front-facing camera angle with slightly visible soil, same base height, and centered at the exact quarter and three-quarter horizontal positions. Entire pots and plants fully visible. Leave broad empty transparent margins including an empty transparent center gutter; the sprites must not touch. Gentle, detailed hand-painted watercolor botanical style, readable as a small desktop icon, warm soft light, natural shapes. Genuine transparent background only, no ground, no cast shadows, no labels, no text, no grid, no panels, no other props.

`assets/purple-oxalis-stages.png`（紫叶酢浆草的两个阶段）

> Create one two-frame sprite sheet for a cozy Windows desktop gardening app on a wide landscape canvas, two equal-width columns. Subject: the same potted purple shamrock (Oxalis triangularis, 紫叶酢浆草) at two ages. LEFT: a small early plant with three purple triangular trifoliate leaves on a few short slender stems, no blooms. RIGHT: a mature airy arrangement of 8 to 12 deep plum butterfly-shaped trifoliate leaves plus several pale pink five-petaled oxalis flowers on taller slender stems. Both frames have the same muted sage green matte glazed ceramic pot, same pot width and height and placement, viewed front-facing with a slight view of the soil. Bottom-align the two pots and center them at exact quarter and three-quarter horizontal positions. Keep the whole objects visible with a wide transparent center gutter. Detailed tactile hand-painted botanical watercolor, softly lit and recognizable at small size. Genuine transparent background, no ground, no cast shadow, no text or labels, no boxes, no other props.
