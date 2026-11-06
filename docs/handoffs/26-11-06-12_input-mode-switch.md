# 中英切换按钮（2026-11-06）

接续 [26-11-06-11](./26-11-06-11_faithful-callbacks.md)。上一轮把中英切换入口列为 VR 验证的前置条件。这一轮做出来了：按钮镜像输入法自己的语言栏项，按下即按它。代码未提交，控制台探针跑通，真机未验证。

## 现状一句话

页面多了一个切换按钮，画的是 Windows 输入指示器画的那个图标；按下去由输入法自己切换中英，插件不读也不写模式。

## 决定

- [0028](../adr/0028-mirror-the-ime-language-bar-item.md)：镜像语言栏项并调用它的 `OnClick`。它取代 [0012](../adr/0012-synthesized-modifier-key.md)（运行时合成中英键），修正 [0018](../adr/0018-candidate-bar-display-only.md)（候选仍不可点，可点的只有这一个开关）。
- 状态：`accepted`；真机未验证这件事写在 0028 的后果里。

## 调查结论

三件事推翻了原设想，都写进 0028 的 Considered Options：

- 用户框的图标是 **Win11 输入指示器**，不是通知区图标；它对应的是输入法的 TSF 语言栏项，不是 `Shell_NotifyIcon`。
- 本机托盘枚举到 0 个图标：任务栏自动隐藏，且 Win11 托盘走 XAML，`ToolbarWindow32` 那条老路取不到。
- 小狼毫托盘图标的**单左键无处理**（0.17.4 的 `SystemTraySDK.cpp` 只认右键与双击），真正响应点击的是语言栏项：`GetTooltipString` 写着"左键切换模式"。

实测过、被否决的两条路：`ImmSetConversionStatus` 写不动小狼毫（`setconv` 场景，返回 False，状态不变）；写 TSF 转换模式隔间同理，小狼毫只对被外部写入的 `KEYBOARD_OPENCLOSE` 隔间取反状态，那是按输入法适配。

## 本轮实现

- `Interop/TsfInterfaces.cs`：补 `ITfThreadMgr`、`ITfLangBarItemMgr`、`IEnumTfLangBarItems`、`ITfLangBarItem`、`ITfLangBarItemButton`、`TF_LANGBARITEMINFO`。
- `Interop/GdiNative.cs`：把图标读成像素用的 GDI 声明。
- `Ime/LanguageBarButton.cs`：在宿主线程枚举语言栏项，挑**能画出图标**的那个（系统的"语言/更正/键盘/帮助"四项都画不出图标），读图标、点它。
- `Ime/LanguageBarIcon.cs`：BGRA 转 RGBA，算指纹用于比对。
- `Ime/ImeHost.cs` 与 `HostWindow`：400 毫秒轮询比对指纹后推送；按下走窗口消息回到宿主线程。
- `Page/`：`SwitchIconPayload` 把像素 Base64 化，`KeyboardPage` 缓存并重放，页面消息 `xso-keyboard-ime switch` 触发按下。
- `web/`：候选栏右侧加按钮，canvas 按原始像素画、样式表用 `image-rendering: pixelated` 最近邻放大到固定 32px；没有图标时退化为「中/英」字样。样式表是 `candidate-bar.css`，由 build.mjs 的 `loader: { '.css': 'text' }` 作为文本内联进 bundle（注入的页面没有资源服务）。

## 代码审查

按仓库技能跑了两轴审查（固定点 `c6a12bf`，改动未提交）。两边都提了实料，全部已修：

- 标准轴：页面文件被改坏（`BarStyle` 成了占位字符串、`index.ts` 丢了 `showSwitchIcon` 导出，图标永远到不了页面）；命名与词表撞车（`InputMode*` 指语言栏项，页面用 `showInputMode` 指切换按钮）；`GdiNative` 有死声明；`SwitchIconPayload` 手拼 JSON 且没走 InvariantCulture；重试分支是死代码（`Open()` 失败时定时器根本没启动）；`TimerMessage` 从不 `Post` 却与真消息并列；0028 是 `proposed` 却已把 0012 置为 `superseded`。
- 规格轴：图标尺寸只取 `GetIcon` 给的那一张（TSF 没有"再要一帧更大的"入口，取注册表路径就是按输入法适配），已写进 0028 的 Considered Options；CSS 抽取不是本轮要求，但既然做了就必须接上 esbuild。
- 修法：C# 改名 `LanguageBarButton` / `LanguageBarIcon` / `SwitchIconPayload`，页面改名 `showSwitchIcon` 与 `requestSwitch`，消息改为 `xso-keyboard-ime switch`；宿主无论有没有找到按钮都启动 400 毫秒轮询；`PublishSwitchIcon` 的日志只在第一次缺图时打。

## 验证（控制台探针）

`switch` 场景：按下前 `IM CONVERSION 0x9a0 native=False`，按下 `OnClick` 返回 `hr=0x0`，图标指纹从 `83785454` 变成 `fb98d3b5`，随后打 `nihc` 组字并提交「你好」。

枚举到的语言栏项（本机）：

| 项  | 描述             | 样式    | 画图标 |
| --- | ---------------- | ------- | ------ |
| 0   | 语言             | 0x20000 | 否     |
| 1   | 更正             | 0x10010 | 否     |
| 2   | 键盘             | 0x20000 | 否     |
| 3   | 帮助             | 0x20000 | 否     |
| 4   | WeaselTSF Button | 0x30002 | 是     |

## 未定

- 图标是 ICO，按 `SM_CXSMICON` 加载，尺寸随 DPI 走（100% 是 16×16）；页面放大 2 倍显示。是否要更大，等真机看效果。
- 400 毫秒的轮询延迟在 VR 里是否可感，未测。
- 语言栏项因输入法而异：没有带图标的项时按钮退化，此项要在真机上换个输入法看一眼。

## 待办

- 真机验证：按钮出现、图标正确、按下能切、切换后能打中文。
- [0027](../adr/0027-cancel-composition-on-caret-move.md) 仍未落地。
- 既有基线表复测。
