# 真实 Vuplex 后台撤销与桌面前台请求调查（2026-11-08）

接续 [TSF 收尾调查](./26-11-08-4_tsf-retirement-investigation.md)。本轮恢复 SteamVR，在真实 XSOverlay 页面验证后台写回与撤销，没有修改产品代码或选择新的上下文架构。当前方案见[落地方案](archived-notes/README.md)，宿主生命周期继续遵循 [0050](../adr/0050-hold-the-host-focus-for-the-composer.md)。

## 环境与诊断

XSOverlay 进程为 `25096`，宿主 STA 为 `1704`，Unity 主线程为 `18452`。临时被动诊断升级至 `0.7.0`，临时桌面自测为 `0.2.0`。前者只观察，后者通过现有游戏 WebSocket API 接受固定动作，没有新增游戏监听端口。没有启动网页静态服务器。

部署前后主插件 SHA256 均为 `4C5EFE757C1049EA2405401D9F7A3511CCDB0463F2BE4D04942951A1545C6A23`，原生库均为 `C9BEC95B757EDF4581DE8B17B7FF40CE14CFF7E7296933E053311D6EA949D23C`。因此此前的原生 COM 修复仍未部署。旧诊断与日志已备份。

自测只接受空白、聚焦且宿主空闲的 composer，拒绝开启 VRC ChatBox 的页面。固定文字为 `p`、`a`，不发送正文，不查询或切换输入法中英模式。异步回调重新核对页面与宿主资格；自测观察异常不会额外请求取消。脚本恢复焦点或选区失败会使样本无效，外部 input、同名元素重建或文字不符合当前步骤时会停止操作。

独立 Chromium 的 17 项合成焦点门槛检查通过，覆盖上述防护。该环境不能建立真实后台顶层页，未计入后台写回证据。下述结论来自真实 Vuplex。

## 后台写回与原生撤销

20:29 先聚焦真实 composer，通过 `execCommand('insertText')` 写入 `p`，再调用 XSOverlay 实际 `KeyboardFocus.Release`。诊断 `seq=52–55` 确认游戏 scope 从 `hasFocus=True/webView=True` 变为 `False/False`；`seq=56–58` 随后记录 DOM 失焦和 composer-inactive。后续快照确认 `document.hasFocus()==false`、正文长度为 1。

后台动作临时选择同一个 composer，执行编辑命令后恢复先前 DOM 活动元素。本轮先前元素是 body。

| 操作              | 结果         | 操作结束后的状态          |
| ----------------- | ------------ | ------------------------- |
| `insertText('a')` | `pa`，长度 2 | 文档失焦，活动元素为 body |
| `undo`            | `p`，长度 1  | 文档失焦，活动元素为 body |
| `redo`            | `pa`，长度 2 | 文档失焦，活动元素为 body |
| 清理固定实验文字  | 空，长度 0   | fixture 删除，文档失焦    |

三个编辑命令均返回 true，未观察到外部 input。诊断 `seq=61–63` 记录相应 DOM input，期间没有新的 composer-active 消息；操作前后的游戏 scope 均未获得焦点。后台动作没有调用 `IWebView.SetFocused(true)`、`SendKey` 或重新授予游戏键盘焦点。

该样本证明真实 Vuplex 的后台 composer 可以通过浏览器编辑命令保留和使用原生撤销记录，支持继续保留 [0008](../adr/0008-inserttext-writeback.md) 的撤销通道。它没有验证物理 Ctrl+Z、真实 IME 迟到提交、宿主到页面的完整往返，或另一个 input 持有 DOM 焦点的情形。每次命令开始与回调时的 Windows 前台 HWND 相同，但没有覆盖中间时刻；不能据此断言区间没有前台变化。清理文字不等于清空浏览器撤销栈。

## 桌面请求的前台失败

20:33–20:35 通过固定自测命令三次请求微软拼音，产品继续执行一次正常前台获取。前两次在同一桌面前台下执行，第三次在电脑操作工具激活并点击 VSCode 文档标签后执行。

三次原 `SetForegroundWindow` 均返回 false，调用前后的前台均为其他进程，原 `Take` 也为 false。前两次目标宿主 HWND 为 `0xe09e8`，前台为 `0x3e001e`；第三次前台变为 VSCode 的 `0x90a3c`。活动档案保持小狼毫。诊断 `seq=84–88`、`95–99`、`107–111` 分别记录三次失败，`seq=101` 记录了桌面前台变化事件。

这组证据将本轮失败定位到未成功取得前台，未出现一次成功返回后再被其他窗口夺走的记录。它没有给出系统拒绝的具体原因。WebSocket 命令不等价于游戏收到真实桌面输入，VSCode 点击也不等价于用户从 VR 桌面浮窗操作；因此没有复现旧会话中“焦点往返后第三次成功”的完整对照，也不能代替真实 VR 点击采样。

## 真实 VR 首次前台获取

用户手柄恢复后，在新进程 `12712` 中完成真实点击对照，宿主 STA 为 `21876`，Unity 主线程为 `13968`。两次初始选择同一微软拼音项目均失败；用户从 VR 桌面浮窗点击其他普通窗口，再返回键盘选择同一项，切换成功。三次页面请求均为 `index=1`，分别对应诊断 `seq=179`、`198`、`241`。

| 阶段               | 原 `SetForegroundWindow` | 即时前台与结果                                                                        |
| ------------------ | ------------------------ | ------------------------------------------------------------------------------------- |
| 初始请求 1、2      | false                    | 前后均为 `steamwebhelper` 的 `SDL_app` 窗口，原 `Take=false`，活动项保持小狼毫        |
| 桌面操作后的请求 3 | true                     | 从 `ChatGPT` 的 `Chrome_WidgetWin_1` 窗口切到宿主，原 `Take=true`，活动项变为微软拼音 |

前两次原生调用为 `attempt=1/2`，对应 `seq=185–188`、`205–208`。第三次为 `attempt=3`，对应 `seq=248–251`；随后原产品归还前台，再执行一次成功的 `Take`，这是同一次切换中的宿主调用，不是第四次页面选择。`seq=267` 的真实档案身份确认微软拼音已激活，不能只以页面选中标记判断。

前两次调用的 `lastInputTick` 均为 `60518000`。桌面操作后记录前台变化事件，第三次调用的值为 `60623218`。桌面交互同时改变了前台窗口和最后输入时间，本次未隔离这两个变量；最后输入时间也不能单独识别接收输入的进程。

因此本样本已区分首次失败的两种解释：失败调用没有成功取得前台后再被夺走，而是原生调用直接返回 false，前台保持原窗口。Windows 拒绝的具体条件仍未确定，不能据此选择等待、重试、合成按键或扩大输入法激活范围。此前桌面 WebSocket 自测未能复现的完整“先失败、焦点往返后成功”对照，本次由真实 VR 操作取得。

## 物理输入的边界

安装版 `Vuplex.WebView.dll` 的 SHA256 为 `61E055CE3AFBE24A0DA8947B77B8B2BB3300EE4D04BB537ED58B56819C5F997F`。其 IL 确认 `NativeKeyboardListener` 从 Unity `Input.inputString` 和 `GetKeyDown` 产生键事件，`KeyboardManager` 再向自己的 `_focusedWebViewPrefab` 调用 `KeyDown/KeyUp`。`WebView_FocusChanged` 会更新该目标。这条路径独立于 XSOverlay 的 `KeyboardInputFocusManager`。

静态路径不能证明隐藏宿主持有前台时 Unity 实际收到哪些物理事件。现有 hold 自测仅抑制租约 Restore，不是 0050 的完整实现；宿主 `WM_CHAR` 只积存文字，现有交付仍依赖 session 送键。后续实体键盘对照只观察路由，没有实现普通字符写回或物理编辑键转发。

后续准备了独立的物理输入观察探针，只在真实 US 档案、空白 composer、宿主空闲和 VRC 关闭时，通过一次原前台获取开始。它临时保留该宿主前台，同时观察窗口键消息、`NativeKeyboardListener` 到 `KeyboardManager` 的事件，以及实际 WebView 的 `KeyDown/KeyUp`；记录固定键分类、目标身份、Unity 焦点和后台运行状态，不记录正文。原生失焦立即撤销资格，阻止排队操作重新夺焦；收尾不发送取消键。

部署版本缺少源码中新加的 STA 动作入口，探针改用带固定标记的私有窗口消息。32 个必需成员已按已部署 DLL 核对，9 项 Chromium 数据保护检查与 25 项同款 Unity Mono 检查通过，后者包含 11 个实际方法的补丁安装和游戏帧入口的替代调用。独立 Mono 无法运行 `InputManager.Update` 的 Unity 静态依赖，该安装项留待游戏内核对。独立审查发现的两项清理问题已修正：失败启动保留原有缓冲；停止时先归还前台，再处理当前排队键消息，最后仅清理本轮拥有的固定 A。页面清理也只作用于原元素、独立 token 和固定 A 数据，不产生编辑或焦点事件。

22:23 部署 `0.1.0` 后启动进程 `24596`，宿主 STA 为 `11084`，Unity 主线程为 `25332`。初次自动选择 US 时原前台调用失败；VR 桌面交互后，原调用返回 true，`seq=128` 确认真实档案为类型 2、语言 `0x0409`、HKL `0x04090409`。随后观察启动再次成功取得宿主前台，对应 `seq=159–164`。

该版本的插件自身 `Update` 没有运行，主线程答复滞留，未请求用户按物理测试键。固定停止命令在 STA 正常归还前台，确认宿主缓冲与 composer 均为空。后续快照记录 `FrameCount=0`、`ListenerUpdates=5283`、`runInBackground=true`；这证明观察期间 Vuplex 的键盘监听仍在更新，不能把启动答复超时解释为整个 Unity 停止运行。页面最终收尾仍滞留，未计为完成。

项目原有 [UpdatePumpPatch](../../XSOverlayKeyboardIme/Game/Patches/UpdatePumpPatch.cs) 已注明游戏会销毁加载器对象。`0.1.1` 修正探针，沿同一 `InputManager.Update` 入口派发主线程工作，并要求已观察到帧计数后才允许取得前台。构建与隔离检查通过后，用户停止游戏，替换探针并经 Steam 启动进程 `25156`。其宿主 STA 为 `25448`，Unity 主线程为 `24888`，首次快照记录 `pumpTicks=6815`，确认真实游戏帧入口已运行。该 DLL 的 SHA256 为 `32206D601A28BF256E494061584D6D5A66D6E0E19B37184F63E46CFFE6A3EF75`。

22:36:37 在真实 US 档案下成功开始观察，composer 聚焦且为空，隐藏宿主持有前台，样本有效。用户依次按实体键盘 `A → Backspace → Left → Ctrl+Z`，每步均松开。

| 实体按键  | 隐藏宿主记录                           | Vuplex 监听与 WebView 送键 |
| --------- | -------------------------------------- | -------------------------- |
| A         | down/up、`WM_CHAR A`                   | 均未记录                   |
| Backspace | down/up、`WM_CHAR Backspace`           | 均未记录                   |
| Left      | down/up                                | 均未记录                   |
| Ctrl+Z    | Ctrl 与 Z 的 down/up、`WM_CHAR Ctrl-Z` | 均未记录                   |

上述消息均记录 `hostForeground=true`，对应原诊断 `seq=123–158`。22:37:24 快照记录 `FrameCount=2684`、`ListenerUpdates=2684`、`Invalid=false`、`Dropped=0`。用户随后操作其他窗口导致真实失焦，探针立即撤销资格，`EndReason=native-focus-lost`，未抑制取消或宿主送键。宿主固定 A 缓冲长度为 1，随后按观察所有权清空，页面始终为空，fixture 已移除。`unityFocused=true` 与 `runInBackground=true` 只说明相应 Unity 状态，不能证明物理按键进入 Unity 主窗口。

22:39:16 重新开始相同 US 空框观察。经原游戏 `SendKeyboardEvent` API 只发送一次固定 Backspace，原空闲路由进入 `InputManager.HandleKeypress` 和 `KeyboardInputFocusManager.SendKey`，后者返回 true，对应 `seq=179–183`。随后独立快照记录 `webview-KeyDown Backspace`、`webview-KeyUp Backspace` 各一次，均为相同页面、Unity 主线程和 `hostForeground=true`，未走宿主送键。客户端在动作发出后遇到空答复而报解析错误，未重放按键；现场结果以独立快照和原日志为准。

该阳性对照确认实际 WebView 观察点有效，没有独立校准 `NativeKeyboardListener` 的硬件输入路径。四组实体键只证明本组条件下宿主收键、未观察到自动 WebView 转发；不能外推所有物理键或输入法。探针不交付宿主普通字符，不代表 0050 完整实现，仍需完整输入路由、剪贴板快捷键和真实 IME 迟到提交验证。当前队列排空也不能证明未来输入法处理结束。

22:41:46 固定停止命令成功，`Active=false`、`Invalid=false`、`hostForeground=false`，原前台归还已运行。页面 cleanup 成功、长度为零、无组字，宿主缓冲和排队键清理计数均为零。该组 `FrameCount=13448`、`ListenerUpdates=13448`，仅抑制一次原 Restore，未抑制取消或宿主送键。探针仍加载但观察已关闭。

## 共享上下文的窗口关联

后续三个独立进程 `21936`、`20436`、`13608` 各创建两个不显示的 HWND，以及绑定窗口 A 的一个原生上下文。先将窗口 B 关联到同一文档，再显式调用 `ITfThreadMgr.SetFocus`；随后解除窗口 A 的关联，再次显式设置文档焦点。两次均回读 `GetFocus`，通过 `IUnknown` 身份确认焦点确为该文档。

| 采样点                          | 活动 view 的 `GetWnd` |
| ------------------------------- | --------------------- |
| 初始状态                        | A                     |
| B 关联到同一文档后              | A                     |
| 显式设置并确认文档焦点后        | A                     |
| 解除 A 关联，再设置并确认焦点后 | A                     |

三次结果一致，关联变更、显式设焦及窗口查询均为 `S_OK`，进程正常退出。结束前恢复原窗口关联和 TSF 文档焦点，释放实验接口，再销毁上下文与隐藏窗口。前台 HWND 仅比较起终点相同，没有区间保证。所载独立 DLL 的 SHA256 为 `6EBD10021C909D34CB3DE5A89CC513E24C2C139A90F0117F5214670E9A8507C5`，包含此前的 COM 修复，没有部署到游戏。

源码与观察一致：[原生上下文](../../native/IngameIME_Win32/include/tf/TfInputContextImpl.hpp)把创建 HWND 保存为 `const`，[ContextOwner](../../native/IngameIME_Win32/src/TfContextOwner.cpp)的窗口查询、文字坐标映射与屏幕范围仍使用它，现有公共接口和 shim 没有更新 owner HWND 的入口。[AssociateFocus](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfthreadmgr-associatefocus) 提供窗口与文档的焦点关联；多窗口共享文档时要求应用显式设焦。[GetWnd](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfcontextowner-getwnd) 则报告当前文档对应的窗口，两者不是同一项状态。

因此，若要求保留同一上下文却让它报告窗口 B，仅改焦点关联不足，还需验证明确更新 owner 窗口的路径。托管 [HostWindow](../../XSOverlayKeyboardIme/Ime/HostWindow.cs) 也把 `WM_CHAR` 交给静态当前实例，任意宿主窗口销毁都会投递 `WM_QUIT`。轮换方案还须分别处理字符来源与线程退出，不能只替换窗口句柄。原生 `setActivated` 会调用 `TerminateComposition`，不能把激活开关用作默认失焦的替代。

该对照没有送键、消息循环、实际 Win32 焦点切换或真实输入法结果，未验证候选、模式保持、新旧组字隔离或回收。它仅确认现有实现中焦点关联与 owner 窗口报告的边界，没有选用窗口轮换方案。

## 页面模拟切换日语

用户再次重启后，21:41:53 启动的进程为 `18800`，宿主 STA 为 `25580`，Unity 主线程为 `4288`。页面控制探针 `0.1.2` 沿原 WebView 与 DOM 聚焦入口操作空白 composer，再点击真实输入法菜单中的日语项目。

初次请求的原 `SetForegroundWindow=false`，对应 `seq=60–61`。用户完成 VR 桌面浮窗交互后，重新聚焦并点击同一项目，原调用返回 true，宿主成为前台，对应 `seq=100–103`。`seq=108` 回读真实活动档案：语言 `0x0411`、service `03b5835f-f03c-411b-9ce2-aa23e1171e36`、profile `a76c93d9-5523-4e90-aafa-4db112f9ac76`，线程 HKL 为 `0x4110411`。该对照没有隔离系统拒绝条件，也没有改变原前台调用。

## JIS 键码来源与两条送键路径

21:49:37、21:49:56、21:50:07 在进程 `18800` 的真实页面依次模拟平／片假、変換、無変換按钮的 `pointerdown/up`。日语活动档案已确认，三键均可见，composer 为空并拥有游戏键盘焦点，VRC ChatBox 关闭，无修饰键或待处理输入。

| 按钮     | 安装布局的扫描码字符串 | 页面与游戏收到的值  | 托管关联记录                  |
| -------- | ---------------------- | ------------------- | ----------------------------- |
| 平／片假 | `70`                   | `70`，即 `VK_F`     | `jisAttempt=1`，`seq=135–145` |
| 変換     | `79`                   | `79`，即 `VK_O`     | `jisAttempt=2`，`seq=148–158` |
| 無変換   | `7B`                   | `235`，未命名枚举值 | `jisAttempt=3`，`seq=161–172` |

三次 `ImeHost.TryAcceptKey=false`，原游戏随后把日文 `webValue` 标签交给 `KeyboardInputFocusManager.SendKey`，该函数返回 true。后续快照的 composer 长度均为零，无组字或待处理输入。`SendKey=true` 只表示已调用 WebView 送键，不保证浏览器插入文字；探针也保留 `deliveryConfirmed=false`。这是合成指针事件触发的真实页面及托管路由证据，没有证明标准 `KANA/CONVERT/NONCONVERT` 已送达输入法。

来源已按安装版代码核对：

1. 游戏安装目录下 `XSOverlay_Data/StreamingAssets/Plugins/KeyboardLayout/Keyboard.json` 的 `jisRows` 保存上述扫描码字符串。`KeyboardHelper.TryResolveKeycode` 按十六进制解析扫描码，再调用 `MapVirtualKeyExW(..., 3, hkl)`。`PopulateKeyInfo` 在转换失败时保留原 `keycode`，成功时改写为虚拟键码枚举名称或数值。
2. 页面 `keyboard-layout.js` 的 `hexMaybe` 仅执行 `String(code)`，`keyboard-view-keys.js` 原样形成按钮 ID 和事件值，另携带日文标签。实际事件已确认是 `70/79/235`，不是标准专用虚拟键码 `21/28/29`。
3. `InputManager.HandleKeypress` 使用 `Enum.TryParse<VirtualKeyCode>`，将数值字符串按十进制解释。没有游戏 WebView 焦点时，原模拟器按该虚拟键码调用 `SendInput`；有焦点时，`HandleFocusedWebViewKeypress` 优先使用 `webValue`，发送标签。

因此，转换失败后残留的扫描码 `70/79` 被再解释为 `F/O`，是 XSOverlay 自己的键码处理缺陷。真实事件在插件判定前已带这些值，插件未接管，原游戏继续处理；插件没有生成或纠正这些键码。按用户要求，此上游缺陷仅记入路线图，不修复，也不继续调查修复方案。[0048](../adr/0048-route-jis-ime-keys-through-the-host.md) 的标准码接管是另一层已实现行为，不能修复这些上游值。

本轮没有在布局生成当时记录 `MapVirtualKeyExW` 的返回值与 HKL，`70/79` 对应转换失败分支的判断来自安装版源码、既有映射对照和实际事件，不能据此确定 Windows 为何没有返回标准专用码。`235` 的实际 OS 事件与输入法解释也未闭合，不将它一起定为上述错误。

用户补充，之前的 `f/a` 出现在 VSCode，其他桌面程序是否复现未知。桌面路径将 `70` 注入为 `F`，能解释 `f` 的来源方向；当次仍无完整关联记录。無変換的 `a` 不能由本轮 `235` 或 `79 → O` 推出。composer 聚焦样本走的是标签路径，其空框结果不否定 VSCode 报告。按用户后续要求，JIS 专用键移出插件待解决项，`a` 来源未定仅保留为证据边界。

安装文件证据版本：`Keyboard.json` SHA256 为 `DF90A2CFFDF45A731B502EA52BBC6C3716C2555F1B71C53329BDB64CB1454282`，`XSOverlay_Data/Managed/Assembly-CSharp.dll` 为 `88584707BAD341D0247E8AF1277CC86296DF3B2B796972E5CAEF03EF214B6F93`。未修改安装布局或游戏程序集。

## 收尾与下一步

20:29 自测收尾时，固定文字与 fixture 已清理，没有启用 hold，输入法档案仍为初始小狼毫。当时真实 JIS 三键、VR 首次切换及物理输入尚未采样。

Steam 客户端进程仍在运行，但电脑操作工具没有取得其可操作主窗口，显式启动也返回 `launched app did not expose a targetable window`。随后用户从 Steam 停止 XSOverlay。20:49 确认进程退出、Steam Running=0 后，已备份最终日志并将主动自测 DLL 移出插件目录，再经 Steam 启动。

新进程为 `12712`，仅加载主插件、原生库和被动诊断 `0.7.0`，主插件与原生库哈希不变。新进程不保留实验撤销历史，被动观察窗口也已刷新。最终日志中的低级按键钩子曾报告成功解除，但没有前台事件钩子的退出解除记录，不能将该退出清理分支记为已验证。原被动诊断的备份可用于回滚。

用户随后完成上述真实 VR 首次切换对照。经用户再次停止游戏，已装入独立的页面控制探针 `0.1.0` 并经 Steam 重启。该探针只注册固定页面动作，不拦截送键或归还前台；构建与独立审查通过，复用安装版按键模块的 16 项 Chromium 检查通过。检查覆盖三颗 JIS 键各一次事件、释放重复计时器，以及非空、组字、VRC 开启和失焦拒绝，不代表真实宿主交付。主插件与原生库哈希不变，COM 修复仍未部署。

`0.1.0` 的首个页面快照因引用部署版本中不存在的 `_keyboardFormat` 字段失败，没有分派测试键。`0.1.1` 将该字段标为可选；随后核对已部署程序集的 17 个必需成员。页面已加载但未聚焦时，旧就绪门槛也会拒绝聚焦模拟。`0.1.2` 分开记录 overlay 可见性和页面确认状态，聚焦模拟沿用 WebView 的 `SetFocused(true)` 加 DOM focus，不把它记作取得 Windows 前台。

`0.1.2` 的 SHA256 为 `ED3C800E636419728BAC1C003E27A5C4347363D7E9C236ADD69A86EE49A3939E`。用户因网卡问题重启电脑后，确认 SteamVR 已恢复、XSOverlay 未运行，再经 Steam 启动进程 `14532`。真实页面确认已到，overlay 生命周期运行且源已就绪，但游戏仍报告 `isVisible=false`，因此聚焦和按键动作继续被门槛拒绝。该状态不构成 JIS 或宿主交付样本。部署主插件没有 `_keyboardFormat`，也不能用这份运行时样本验收源码中的布局按钮修复。

后续进程 `18800` 已完成上述日语切换和 JIS 三键页面采样。收尾快照的 composer 仍为空、无组字，宿主队列为空。当前列表将日语放在首位，JIS 仍有一个中英按钮，这些属于旧部署基线，不能用于验收源码中的列表与布局按钮修复。已确认的上游 JIS 键码问题停止修复调查，物理输入、普通字符来源、未知候选归属和旧上下文回收继续调查。上述实验不构成产品修复或新增 ADR。

进程 `25156` 已完成 US 实体键观察及网页送键阳性对照，并完成上述停止清理。22:44 通过页面原菜单恢复初始小狼毫，`seq=219` 回读真实活动档案为语言 `0x0804`、service `a3f4cded-b1e9-41ee-9ca6-7b4d0de6cb0a`、profile `3d02cab6-2b8e-4781-ba20-1c9267529467`；页面随后确认小狼毫选中、正文为空、无组字或排队按键。主插件、原生库、被动诊断和页面控制探针哈希保持原值。物理观察 DLL 尚未移出，留待下一次必要停机时一并清理；当前没有活动观察或前台租约。后续继续验证完整物理路由、真实迟到提交归属与上下文回收，JIS 上游问题不再列为待办。
