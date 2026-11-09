# 任务栏 UIA 模式点击调查（2026-11-08）

任务栏 UIA 已在五个新进程的前台往返矩阵中完成 60 次实际切换，覆盖微软拼音和日语，也覆盖小狼毫初始输入项。立即释放 Shell 观察对象曾漏掉图像更新；随后增加真实图像更新的事件边界，12 次图像与实际输入全部一致，且不再使用点击与下一输入之间的固定间隔。建议采用“公开 TSF 优先，Shell 原图加任务栏 UIA 点击”的路径，产品集成仍须处理异步动作生命周期。

生产点击行为未改，仍遵循 [Shell 适配说明](archived-notes/input-mode-shell.md)：公开 TSF 按钮调用自身 `OnClick`，Shell 图像显示为 disabled。本轮只改隔离探针，不处理路线图的新问题。

## 实验方法

- 运行时使用 XSOverlay 自带的 Unity Mono，输入宿主直接编译插件的窗口、输入上下文和送键源码。
- UIA 工作在线程池外的独立 MTA，设置 `IUIAutomation2.AutoSetFocus=FALSE`，调用真实任务栏模式按钮的 `InvokePattern.Invoke`。
- 定位采用本地资源 `AutomationName_ImeButton`，匹配 `SystemTrayIcon` 和 `SystemTray.NormalButton`，用 `LanguageIndicatorHelpString` 排除语言选择按钮；目标不唯一时拒绝调用。
- 每次记录 Win32 前台与焦点，使用真实组字、提交或普通字符判断效果。HRESULT、图标与模式诊断值都不构成成功判据。
- 自动矩阵激活目标输入项，再调用 UIA 并通过生产 `KeyFeeder` 发送 A；测量后用 Enter 提交，再反向点击并发送 A。手动流程分两次点击面板的激活与 UIA 按钮。
- 诊断模式可关闭额外观察对象。探针不写中英状态、不预热输入、不重试点击；观测间隔只用于实验。

定位参考 [SnowDesktop 固定版本源码](https://github.com/FreeFallingSnow/SnowDesktop/blob/34e719f169f48c2f8a317f38db2401af7ce2cd51/src/system/status_bar/status_bar_input_method_native.h#L60)。该项目的模式左键使用私有 Shell 点击，不能将它当成同一 UIA 点击路径的先例。

原生探针先缓冲文本，函数返回后才逐行写入托管日志。原生 `START`、`BEFORE Invoke` 的打印时间不能与宿主回调直接排序，也不能据此认定状态在 Invoke 之前已经改变。托管层另有不缓冲的 `UIA NATIVE CALL begin/returned`，可用于定位实际调用区间。

## 观测结果

以下时间均为 2026-11-08 本机时间，表内记录摘录足以解释各项结论。

| 实验时间                     | 条件                                                               | 实际结果                                                      |
| ---------------------------- | ------------------------------------------------------------------ | ------------------------------------------------------------- |
| 04:09:45                     | 原宿主，Invoke 后观测 1000 ms                                      | 首次与反向均为普通 `a`，第一次按键前已回到英文                |
| 04:18:00、04:19:11           | 普通 Win32 EDIT，无 IngameIME；分别延后、立即输入                  | 日语输入后得到 `あ`，反向得到 `a`                             |
| 04:27:50                     | 原宿主，移除默认 IMM 关联，立即输入                                | 两次均为普通 `a`                                              |
| 04:30:17                     | 将普通 EDIT 整窗移到屏幕外                                         | `あ` / `a`                                                    |
| 04:32:21、04:34:08           | 原屏外 popup，分别无 IngameIME、增加原生 EDIT 子控件               | 两次均为普通 `a`                                              |
| 04:38:40                     | 普通 EDIT，增加相同的辅助 STA 面板                                 | `あ` / `a`                                                    |
| 04:45:10                     | 用户手动多次点击保留焦点 UIA，再按实体键                           | 记录到 `あ`、`ああ`、`あああ`，英文时为 `a`；用户未观察到回跳 |
| 04:47:47                     | 两步手动流程，UIA 返回后立即用生产送键发送 A                       | `あ` / `a`                                                    |
| 04:49:30                     | 新进程冷态矩阵，激活后观测 300 ms                                  | `あ` / `a`                                                    |
| 04:50:08                     | 新进程冷态矩阵，激活后观测 4000 ms                                 | 两次均为普通 `a`                                              |
| 04:53:46                     | 激活后增加面板与宿主的焦点往返，再运行冷态矩阵                     | 焦点往返成功，两次仍为普通 `a`                                |
| 04:54:09                     | 同版探针，改按两步手动流程，立即自动发送 A                         | 首次取得 `PREEDIT composing=True text=あ`                     |
| 05:04:20、05:06:33、05:07:25 | 初始小狼毫；先建立代理观察连接，再激活日语输入法                   | 三次均为 `あ` / `a`                                           |
| 05:05:54、05:06:59、05:07:51 | 初始小狼毫；先激活日语输入法，再建立代理观察连接                   | 三次均为普通 `a` / `a`                                        |
| 05:10:33                     | 初始微软拼音；仅提前创建公开语言栏管理对象，代理连接仍在激活后建立 | `あ` / `a`                                                    |
| 05:11:23                     | 初始微软拼音；保留原来的先激活再连接顺序，无提前创建               | `あ` / `a`                                                    |

04:09:45 实验中，宿主观测时间与按键记录如下，证明这一例回跳不依赖测试按键：

```text
9758.372  IMM open=True  conv=0x219
9771.578  IMM open=False conv=0x219
10777.987 MATRIX FIRST A
11105.152 composing=False preedit= directDelta=a
```

04:53:46 的焦点对照也没有消除失败：

```text
11789.623 MATRIX FOCUS ROUNDTRIP panel=True
12111.141 MATRIX FOCUS ROUNDTRIP host=True
12425.075 IMM open=False conv=0x219
12858.879 composing=False preedit= directDelta=a
13536.222 composing=False preedit= directDelta=a
```

## 当前归因边界

- 已观察到关闭额外诊断的冷态失败，额外模式读取不是失败的必要条件。
- 无 IngameIME 的原 popup 也失败，IngameIME 不是该现象的必要条件。普通 EDIT 的成功尚不是完全匹配的宿主替代实验。
- 延长激活后的等待、延长 Invoke 后的等待、移除默认 IMM 关联、增加一次焦点往返，都未消除失败。
- 屏外普通 EDIT 和辅助 STA 面板均有成功记录，屏外位置或多一个面板线程不能单独解释失败。
- 两步手动流程配合生产送键成功，立即合成按键及键消息排空并非必然失败。

04:45:10、04:47:47、04:54:09 三次手动流程均先打开 Shell 代理观察对象，再激活输入法；冷态矩阵相反。手动流程初次采样还记录到旧代理状态，随后收到档案与模式通知并收敛。

05:04 至 05:08 的六次新进程按两种顺序交替运行，全部记录初始输入项为小狼毫，仅交换代理连接与档案激活的顺序。其余定位、Invoke、送键和观测间隔相同，全部正常退出并恢复初始输入项。先连接的三次成功，先激活的三次失败，说明观察对象的建立不能视为对实验完全无影响。

代理连接依次调用公开 `TF_CreateLangBarMgr`、`GetThreadMarshalInterface`，获取受二进制版本校验保护的私有代理接口，再订阅代理事件。尚未拆分这些步骤的影响，也未排除耗时差异。成功的 04:49:30 矩阵仍是先激活再连接，顺序不是已证实的唯一决定因素。

05:10 与 05:11 的新进程初始输入项变为微软拼音，两种配置均成功。前一次仅提前创建并保留公开 `ITfLangBarMgr`，不提前获取私有代理；后一次没有该操作。由于初始输入项与前六次不同，这两次不能证明公开管理对象能够替代私有观察连接。

这些手动成功与 04:49:30 成功矩阵在首次输入前均记录 `conv=0x19`；04:09:45、04:50:08、04:53:46 的失败记录为 `conv=0x219`。这只是初始状态的相关性，不能写这些标志、按输入法增加分支，或用它们判定生产点击是否成功。

## 生产条件下的前台往返

后续矩阵移除私有模式代理和 IMM 模式读取，保留生产公开档案通知。每次 UIA 返回后归还面板前台，观测 300 ms 并确认面板仍在前台，再重新取得宿主前台发送 A。测量后 Enter 提交，再反向点击。300 ms 是实验观察间隔，不能加入产品作为完成契约。

| 新进程时间 | 条件                                                                           | 实际结果                                |
| ---------- | ------------------------------------------------------------------------------ | --------------------------------------- |
| 05:33:48   | 初始微软拼音，无图像观察对象；拼音先测                                         | 两款各三轮，12 次切换成功               |
| 05:44:31   | 初始微软拼音，保留公开模式项和 Shell 图像观察                                  | 12 次切换成功，图像与随后输入一致       |
| 05:47:33   | 初始微软拼音，日语先测；直接编译生产 `ShellModeButton`，每次点击完成采图后释放 | 12 次切换成功，图像与随后输入一致       |
| 05:49:54   | 初始小狼毫，日语先测；其余同上一项                                             | 12 次切换成功，其中一次拼音快照仍是旧图 |

后两项按生产顺序创建 `ImeContext → ImeProfiles → ImeProfileNotifications`，没有提前建立私有诊断连接。05:49 的初始小狼毫由新进程创建上下文后实际读取确认，结束后恢复实验前的微软拼音。

四组均无前台、焦点或档案校验中止，48 次下一输入均重新取得宿主前台，最后均为 `EXIT=0`。拼音在普通 `a` 与 `composing=True preedit=a` 之间切换；日语在普通 `a` 与 `composing=True preedit=あ` 之间切换。首次方向可能不同，不能固定假定首次点击必须进入日语。

`SetForegroundWindow` 跨线程恢复可能尚未在函数返回时完成，因此即时的 `foreground returned=False` 不是失败判据；本组使用随后观察期的前台校验。短生命周期实验的输入仍绕过 `CompositionSession`，只复用 `KeyFeeder`，没有验证产品的每键收尾。

### 已定位的图像竞争

05:49:54 的第二轮拼音记录如下，均为同一宿主档案与焦点：

```text
29266.611 UIA NATIVE CALL begin action=1
29303.791 UIA NATIVE CALL returned hr=0x00000000
29306.546 PRODUCTION SHELL SAMPLE read=True glyph= tooltip=中文模式
29308.618 PRODUCTION SHELL disposed before foreground return
29629.100 MATRIX NEXT INPUT reacquired=True
29949.629 MATRIX OUTCOME composing=False preedit= directDelta=a
```

在该项释放前没有收到对应的新图像通知。下一次 UIA 定位也读到任务栏的英语提示。图像滞后属于观察对象提前释放造成的插件显示风险，不能据此否定实际点击，亦不能把旧图当成模式判据。

[Invoke 官方契约](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomationinvokepattern-invoke)要求调用尽快返回，没有输入法切换或 Shell 图像更新的完成保证。[AutoSetFocus](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomation2-put_autosetfocus)默认会为动作设置焦点，本实验显式关闭它。

### 事件驱动的完成边界

05:56:29 的新进程保留生产初始化顺序与短生命周期的生产 Shell 对象，初始为微软拼音。每次点击前采样；动作返回后，继续泵同一 STA，直到现有 `InputModeView.Key` 改变，再采样并释放观察对象、归还前台。两项条件的先后顺序不固定，分别记录。本组最终字形均发生变化，但该键还包含 Enabled 与 tooltip，产品应改用单独的图像身份比较。

下一输入由面板的真实 `WM_ACTIVATE` 事件触发，随即重新取得宿主前台发送 A，移除了原来归还前台后的 300 ms 间隔。档案激活与输入结果测量仍有实验观察期，不能声称整个实验没有观察间隔。

拼音和日语交替三轮，12 次均取得图像变化、面板激活和宿主重新取焦。最终图像与普通 `a`、拼音 `preedit=a`、日语 `preedit=あ` 逐项一致，没有 deadline、焦点或档案校验中止。记录摘录：

```text
16746.915 UIA NATIVE CALL returned hr=0x00000000
16756.130 MATRIX Shell display update observed after invocation began
16757.228 PRODUCTION SHELL SAMPLE glyph= tooltip=英语模式
16891.585 MATRIX panel activation observed; next input without fixed gap
16900.258 MATRIX NEXT INPUT reacquired=True
17218.255 MATRIX OUTCOME composing=False preedit= directDelta=a
17584.793 UIA NATIVE CALL returned hr=0x00000000
17593.235 MATRIX Shell display update observed after invocation began
17594.825 PRODUCTION SHELL SAMPLE glyph= tooltip=中文模式
18059.380 MATRIX OUTCOME composing=True preedit=a directDelta=
28177.666 EXIT=0
```

本组记录到调用返回与确认图像变化之间相隔约 2.6–9.2 ms，包含日志与消息处理开销，不是系统延迟上界。3 秒 deadline 只用于实验失败退出，本组未触发；它既不表示成功，也不是拟采用的固定等待。

这支持将图像更新作为本机候选完成信号，但 Shell 通知没有动作 ID，仍须防止初始化通知、旧动作回调或无图像变化的动作误完成新请求。不能把本机成功提升为公共 API 的系统保证。

## 落地边界

- 公开 TSF 模式项继续优先，使用该项的原图与 `OnClick`。只有公开项明确缺失，才考虑 Shell 图像加任务栏 UIA 点击；按能力选择，不按输入法名称分支。
- UIA 在独立 MTA 中运行，使用本机资源定位唯一任务栏模式按钮，要求可用的 Invoke 能力并关闭自动设焦。找不到可信目标时可以 disabled；单次点击调用失败后恢复可点击，不持续禁用、不自动补点。
- STA 保存宿主档案与动作代次，调用前后校验前台、线程焦点与档案。档案改变、composer 失焦或宿主退出时废弃结果，不抢回用户的新窗口。
- 模式点击期间只短暂禁用该按钮，忽略重复点击。后续输入按用户操作时机立即发送，不因 UIA 请求排队或延迟。原图与字形仍走现有提取路径，不能读取中英模式决定按键归属。
- 图像更新由实际通知与可信采样驱动，独立于请求收尾，不等图像变化才结束请求。观察对象沿 composer 前台租约存续，避免返回即释放漏掉通知，生产仍需验证取消与失败边界。
- [输入宿主调查](./26-11-08-1_roadmap-input-defects.md#按实际结果收尾的受限实验)另已确认过早归还前台和迟到普通字符未交付。其生命周期修复是产品集成的前置项，本报告的 UIA 成功不等于生产完整输入链已通过。

UIA 的公开调用不依赖新增私有 Shell ABI；原图读取仍受现有精确版本校验约束。桌面隔离探针不能代替提权游戏进程、实际 VR 页面、任务栏不可用和不同 Windows 版本的验收。头显休息期间不启动 SteamVR 或 XSOverlay。

后续访谈已接受展示与点击分源，记录为 [0051](../adr/0051-use-taskbar-uia-for-shell-mode-clicks.md)。[0050](../adr/0050-hold-the-host-focus-for-the-composer.md) 另将租约改为随 composer 焦点存续，因此每次点击收集图像后归还前台的实验流程不再是拟采用的产品生命周期。输入不因点击排队，图标异步更新；按钮处理中短暂禁用，单次点击失败恢复可点击。本轮不增加自定义硬超时，产品尚未接入 UIA。

### 实施顺序与放行

1. 先完成独立的宿主输入生命周期修复及验收，避免 UIA 已成功却仍因每键过早归还前台而无输入。此项由[输入修复方案](archived-notes/host-lifecycle.md)跟踪。
2. 将 UIA 定位和 Invoke 封装为独立 MTA 动作，STA 只接收准备、返回和失败结果，不跨 apartment 传递 TSF、Shell 或 UIA 对象。检查设置 API 的返回值并读回自动设焦与超时配置；定位完成后由 STA 再校验档案、composer 和动作代次，允许执行前再检查前台。
3. 将 [InputModeButton](../../XSOverlayKeyboardIme/Ime/InputModeButton.cs) 的同步 pending 计数改为有身份的请求生命周期。每个请求保存宿主档案、代次与处理中状态，只收尾一次，通过宿主消息处理迟到结果。Shell 图像订阅沿前台租约存续，不以图像变化决定点击是否结束。
4. [ImeHost](../../XSOverlayKeyboardIme/Ime/ImeHost.cs) 不因 UIA 点击暂存或延迟后续输入，点击期间到达的普通键仍立即走既有送键路径。失焦或关闭后不再恢复普通键，修饰键释放继续遵循既有不抢前台规则。失效前尚未 Invoke 的请求取消，已进入 Invoke 的动作不能宣称已撤回。
5. 档案改变、composer 失焦或退出时使代次失效，废弃旧图与迟到结果。先停止接受工作，等 worker 不再访问宿主后释放资源，期间 STA 继续泵消息，不能同步 Join。
6. 验收失败目标、匹配不唯一、无图像变化、快速重复点击、点击中立即输入或切档案、外部夺焦、composer 关闭及退出。确认找不到可信目标与单次点击失败的禁用行为不同，失败不自动重试或补发切换键。旧请求的迟到完成结果不得更新新请求，当前宿主与输入项的可信系统事件仍正常更新图标；不能撤销已投递给 Explorer 的动作。
7. 最后在提权 Unity Mono 的真实 VR 页面复测图像、切换后的首键、连续输入和前台恢复。调整租约时还须同时观察宿主与 Vuplex 物理键盘路径，检查重复输入与快捷键丢失，详见[宿主生命周期验收](archived-notes/host-lifecycle.md)。通过后才把相应 Shell 按钮从 disabled 改为可用；其他系统版本继续受原图适配范围约束。

当前代码的 `Reset` 没有异步动作身份，不能复用没有动作 ID 的 `ModeCompleteMessage` 收尾新请求。按钮处理中状态不能误用于阻塞输入。这些是新增 UIA 生命周期所需的接缝，不是已完成的产品修改。

### UIA 超时的契约范围

官方 [ConnectionTimeout](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomation2-put_connectiontimeout) 与 [TransactionTimeout](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomation2-put_transactiontimeout) 约束提供者请求的等待，默认分别为 2 秒与 20 秒，没有给出整段定位加 Invoke 的总时限。[Invoke](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomationinvokepattern-invoke) 要求尽快返回，但实际行为取决于实现。文档没有保证超时会撤回已投递动作。

现有探针未检查超时与 AutoSetFocus 设置调用的 HRESULT，日志记录的是请求值，不能作为配置成功的证明。60 次成功调用实测约 28–78 ms，中位数约 38 ms；此统计不覆盖系统无响应，也不是按钮禁用总时长上界。

最终决定先使用 UIA 自带超时，不增加自定义总时限，避免为尚未复现的挂起增加执行清理机制。用户接受系统一直不返回时按钮可能持续 disabled，普通输入不受影响。失败或超时均不证明系统动作已撤销，图标继续以可信系统事件为准，旧请求的迟到完成回调另行废弃。

## 独立模块实现

用户随后确认先完成 UIA 模块，待 0050 验收后启用。实现及接入约束见[适配说明](archived-notes/input-mode-shell.md#尚未启用的-uia-模块)，生产采样路径与 Shell disabled 行为保留。

新模块分别在 .NET 10 与 XSOverlay 自带 Unity Mono 中运行 8 次实际切换。两组均交替观察到拼音 `preedit=a`、日语 `preedit=あ` 与普通 `a`，前台始终由独立宿主持有。每次调用返回便发送下一 A，没有等待图像变化；多次记录到图像在恢复点击后才更新。

Mono 额外验证 STA 拒绝执行授权，原生返回 `E_ABORT` 后目标仍可点击；请求期间的 A 在约 7 ms 内送出并产生组字，早于请求返回。另一次在请求处理中销毁来源，迟到完成被抑制，前台保持不变。授权拒绝没有进入 Invoke，不能替代实际 Invoke 内部失败、任务栏缺失或匹配重复的故障验证。

首轮 .NET 实验因探针漏处理提前到达的就绪状态而未进入切换，由实验 deadline 退出；修正探针后两组切换矩阵通过。实验计时器只用于退出探针，产品模块没有新增整体超时。
