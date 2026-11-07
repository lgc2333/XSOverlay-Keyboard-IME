# Shell 输入模式项适配

实现入口：[原生适配](../native/shim/shell-mode-button.cpp)、[宿主按钮](../XSOverlayKeyboardIme/Ime/InputModeButton.cs)。产品规则见 [0042](./adr/0042-hide-the-switch-without-an-icon.md)。

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

回调参数是借用指针，可能为空。适配只发失效通知，不保留或释放参数。`SetCallback(NULL)` 解除订阅。原生 sink 通过静态 cdecl 委托通知 Mono，遵循 [0024](./adr/0024-managed-delegate-callbacks.md)。

字形用原始字符与 Segoe Fluent Icons 显示。HICON 保留 alpha 与 AND mask，单色图标及 `TF_LBI_STYLE_TEXTCOLORICON` 使用页面前景色。美式布局的内容字段均为空，即使 hidden 为 0，也没有模式按钮。

## 点击限制与验证

Shell 的 `ClickImeModeItem` 把点击投到原生 worker，返回成功不表示输入法已处理。getter 仍只返回缓存，点击消息不携带宿主身份，也没有完成确认。隔离验证中，同 STA 的完成消息不能稳定取得微软拼音和日语输入法点击后的图像；重建对象也会引入旧状态。因此生产代码不接入 Shell 点击，Shell 模式项显示为 disabled。公开 TSF 按钮仍使用该项自己的 `OnClick`。

2026-11-08 用生产源码分别在 .NET 10 与 XSOverlay 自带 Unity Mono 中验证：小狼毫使用公开原图，可原生点击两次还原；微软拼音显示原始字形，日语输入法显示原始字形，两者禁用点击；美式布局隐藏。恢复目标与宿主分属不同线程。禁用点击和后台刷新均未改变前台或快照，profile 切换清除旧图，结束后恢复原输入项与前台。

页面浏览器回归覆盖原图布局、系统字体字形、固定占位、disabled 无音效与原生消息、主题着色。上述隔离验证不替代 VR 验收，也不证明其他系统 DLL 版本兼容。
