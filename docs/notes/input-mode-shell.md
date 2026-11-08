# Shell 输入模式项适配

实现入口：[原生适配](../../native/shim/shell-mode-button.cpp)、[宿主按钮](../../XSOverlayKeyboardIme/Ime/InputModeButton.cs)。原图规则见 [0042](../adr/0042-hide-the-switch-without-an-icon.md)，已接受的 UIA 点击路径见 [0051](../adr/0051-use-taskbar-uia-for-shell-mode-clicks.md)，尚未接入生产。

## 支持范围

公开 TSF 模式项优先。只有 `GetItem(GUID_LBI_INPUTMODE)` 返回 `S_FALSE` 且对象为空，才使用 Shell；读取失败、图像为空或转换失败均不代表缺项。

Shell 适配目前只接受 x64 的 `InputSwitch.dll` `10.0.26100.9949`：

- SHA-256：`E9BF1D4FB9CD9E8C4DF675BA831AFA62CB487147F95CC2638F2820C665DE57C8`。
- 匹配 Microsoft PDB：GUID `8FC6B9C4-6219-B678-E2AA-EA6322C0AF7D`，age 1。
- CLSID：`b9bc2a50-43c3-41aa-a086-5db14e184bae`。
- 控制接口 IID：`b9bc2a50-43c3-41aa-a082-5db14e184bae`。
- 回调 IID：`b9bc2a50-43c3-41aa-a083-5db14e184bae`。

从 System32 加载 DLL，校验实际加载文件的散列，再校验控制虚表的 RVA：Init 槽 3 为 `0x27200`，SetCallback 槽 4 为 `0x221a0`，getter 槽 15 为 `0x19490`。不匹配即停止使用私有 ABI，公开 TSF 路径继续可用。没有取得可信存在证据时，不凭 TIP 类型补造图标。

## 宿主归属与生命周期

Shell getter 复制全局前台缓存，不能绑定指定线程。宿主在自己的前台租约内创建观察对象，初读后收集同一 STA 消息队列中的变化通知，再释放对象和租约。后台只保留该宿主的快照；profile 改变或 composer 失焦时清除快照。后续宿主按键可重新采样，不轮询。

调用 getter 与接受通知之前同时校验 Win32 前台、线程焦点和 Shell 缓存归属。这个精确版本中，控制对象 `+0x30` 指向 CTsfHandler；handler `+0x68` 是焦点线程 ID，`+0x70` 是焦点 HWND。两者由 `CTsfHandler::OnFocusChange(ULONG, HWND, ULONG)` 写入。偏移与虚表一样受散列校验保护，不能推广为版本范围。

## ABI 与资源

getter 返回 40 字节结构：tooltip 指针、HICON、disabled BOOL、hidden BOOL、glyph 指针、reserved 指针。tooltip 与 glyph 用 `CoTaskMemFree` 释放，HICON 用 `DestroyIcon` 释放；reserved 由此版本 getter 置零，不释放。

回调槽 3 至 10 依次为 `OnUpdateProfile(const profile*)`、`OnUpdateTsfFloatingFlags(ULONG)`、`OnProfileCountChange(UINT, BOOL)`、`OnShowHide(BOOL, BOOL, BOOL)`、`OnImeModeItemUpdate(const item*)`、`OnModalitySelected(int)`、`OnContextFlagsChange(ULONG)`、`OnTouchKeyboardManualInvoke()`。均返回 HRESULT，使用 STDMETHODCALLTYPE。

回调参数是借用指针，可能为空。适配只发失效通知，不保留或释放参数。`SetCallback(NULL)` 解除订阅。原生 sink 通过静态 cdecl 委托通知 Mono，遵循 [0024](../adr/0024-managed-delegate-callbacks.md)。

字形用原始字符与 Segoe Fluent Icons 显示。HICON 保留 alpha 与 AND mask，单色图标及 `TF_LBI_STYLE_TEXTCOLORICON` 使用页面前景色。美式布局的内容字段均为空，即使 hidden 为 0，也没有模式按钮。

## 点击限制与验证

Shell 的 `ClickImeModeItem` 把点击投到原生 worker，返回成功不表示输入法已处理。getter 仍只返回缓存，点击消息不携带宿主身份，也没有完成确认。隔离验证中，同 STA 的完成消息不能稳定取得微软拼音和日语输入法点击后的图像；重建对象也会引入旧状态。因此生产代码不接入 Shell 点击，Shell 模式项显示为 disabled。公开 TSF 按钮仍使用该项自己的 `OnClick`。

2026-11-08 用生产源码分别在 .NET 10 与 XSOverlay 自带 Unity Mono 中验证：小狼毫使用公开原图，可原生点击两次还原；微软拼音显示原始字形，日语输入法显示原始字形，两者禁用点击；美式布局隐藏。恢复目标与宿主分属不同线程。禁用点击和后台刷新均未改变前台或快照，profile 切换清除旧图，结束后恢复原输入项与前台。

页面浏览器回归覆盖原图布局、系统字体字形、固定占位、disabled 无音效与原生消息、主题着色。上述隔离验证不替代 VR 验收，也不证明其他系统 DLL 版本兼容。

后续[任务栏 UIA 调查](../handoffs/26-11-08-2_taskbar-uia-mode-toggle.md)已在前台往返矩阵验证 60 次实际切换。Invoke 返回后立即释放观察对象会漏掉图像更新；后续 12 次按实际图像更新收尾，图像与真实输入一致。异步动作与宿主输入生命周期尚需集成，未接入生产点击路径。

## 尚未启用的 UIA 模块

[InputModeSwitch](../../XSOverlayKeyboardIme/Ime/InputModeSwitch.cs) 已实现 [0051](../adr/0051-use-taskbar-uia-for-shell-mode-clicks.md) 的按钮协调，通过 [IInputModePlatform](../../XSOverlayKeyboardIme/Ime/IInputModePlatform.cs) 隔离 Windows 接口。[TaskbarModeButton](../../XSOverlayKeyboardIme/Ime/TaskbarModeButton.cs) 保留 STA 上的 Shell 观察对象，在独立 MTA 中调用[原生 UIA 适配](../../native/shim/taskbar-mode-button.cpp)。当前 [ImeHost](../../XSOverlayKeyboardIme/Ime/ImeHost.cs) 仍使用原有采样路径，Shell 按钮保持 disabled；待 [0050](../adr/0050-hold-the-host-focus-for-the-composer.md) 验收后再接入。

- `Refresh(profile)` 优先打开公开 TSF 项，明确缺项才打开 Shell 与 UIA 来源。来源回调均在宿主 STA 执行。
- `Click()` 只禁用当前按钮，期间重复点击被忽略。调用返回后立即收尾，不等待图像变化，普通输入不经过该模块。
- UIA 使用本机资源定位唯一模式按钮，排除语言选择器，检查 Invoke 能力、可用与可见状态。自动设焦关闭，请求超时配置为 2 秒与 20 秒，设置后读回确认；没有整体硬超时。
- 定位完成后回到 STA 校验完整输入项与宿主资格，Invoke 前再次校验原生焦点。取消不能撤回已交给 Explorer 的动作。
- `Reset()` 废弃请求与来源代次，取消尚未获准的动作并清除旧图。`Dispose()` 同时阻止迟到刷新重新打开原生项。Shell 系统事件与点击完成分别处理。
- 接入时沿 composer 前台租约保留来源，并在失焦、切换输入项和退出时先使旧请求失效。工作线程不持有 Shell 或 TSF 对象，STA 不等待线程退出。

2026-11-08 新模块在 .NET 10 与 XSOverlay 自带 Unity Mono 中各完成 8 次实际切换，拼音与日语均得到交替的直通字符或组字。Mono 另验证了 STA 拒绝授权后恢复可点击、请求期间立即送键，以及销毁来源后抑制迟到完成。授权拒绝不是实际 Invoke 内部失败；缺失或重复的任务栏目标尚未做实机故障注入。按钮策略由可移植测试覆盖，VR 页面与提权进程仍待集成验收。
