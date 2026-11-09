---
status: accepted
amends: ['0039']
---

# 键盘输入项激活请求限定在 XSOverlay 进程

[0039](./0039-switch-imes-from-the-bar.md) 的后台线程激活在取得前台时发生真实回退，证据见[输入法状态问题交接](../archived/handoffs/26-11-07-5_confirmed-ime-state-defects.md)。插件发出的激活请求最多作用于 XSOverlay 进程，Windows 共享输入法设置自身造成的桌面联动遵循系统行为。

取得前台后调用 `ITfInputProcessorProfileMgr.ActivateProfile`，使用 `TF_IPPMF_FORPROCESS` 请求进程范围激活。失败时记录并刷新真实状态，不升级为桌面范围的 `TF_IPPMF_FORSESSION`，也不添加等待或重试。API 的作用范围见[官方文档](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfinputprocessorprofilemgr-activateprofile)。

名称、选中标记与语言栏图标以宿主真实活动项为准，查询失败时显示亮度稍低的「未知输入法」，隐藏选中标记和旧图标，列表可用时仍能打开下拉菜单。否决默认档案回退，它会将显示问题与真实切换失败混在一起。

同语言切换不一定产生语言消息，因此订阅 `ITfInputProcessorProfileActivationSink`，收到通知后重新查询宿主真实活动项。sink 在原生 shim 中实现，通过 [0024](./0024-managed-delegate-callbacks.md) 的静态委托边界通知托管代码，不使用轮询或托管 COM 回调对象。

2026-11-07 用生产源码分别在 .NET 10 和 XSOverlay 自带 Unity Mono 中验证：小狼毫、微软拼音、美式键盘、日语微软输入法均激活成功；归还前台给其他进程、再次取得前台并发送 A 后，真实活动项仍为所选项。同语言小狼毫与微软拼音互切、跨语言切换均收到原生 sink 通知。未启用的微软五笔不在列表内。

同次隔离验证还通过生产 `KeyFeeder` 与 `CompositionSession` 发送真实 Win+空格，在两种运行时均循环切换四项。送键返回时真实活动项仍为旧项，随后 sink 收到新项通知，下一次取得前台并输入 A 后保留新项。探针只在送键后泵消息观察异步结果，生产路径未增加等待。

本机共享输入设置仍让桌面 HKL 随语言变成 `08040804`、`04090409`、`04110411`，即使请求使用 `TF_IPPMF_FORPROCESS`。插件接受这项系统联动，不主动扩大作用范围。部署后的 VR 验收仍待完成，当前隔离验证不替代它。

切换旧组字的结果和重新激活后的输入法模式仍遵循 [0041](./0041-no-recovery-for-native-ime-results.md)。
