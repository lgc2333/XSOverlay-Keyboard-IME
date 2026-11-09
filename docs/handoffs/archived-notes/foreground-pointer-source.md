# 指针动作来源必须早于发送

> 历史归档（2026-11-10）：保留调查时的事实、方案与待验证项。当前实现以源码、测试、已接受的 [ADR](../../adr/) 和[路线图](../../ROADMAP.md)为准。

2026-11-09。只读核查当前安装程序集，SHA256 `88584707BAD341D0247E8AF1277CC86296DF3B2B796972E5CAEF03EF214B6F93`；与既有反编译来源一致。未运行游戏输入或修改产品。

## 真实托管链

`MouseInputDevice.CaptureInputTransitions()`轮询输入状态；`BeginPointerPress(...)`先执行`ActivePointerPressId = ++NextPointerPressId`，再调用`EnqueuePointerPressEvent(Begin)`进入设备FIFO。随后本帧还要计算姿态、同步空间和命中；`DispatchPendingInputTransitions()`出队，经`XSOEventSystem.EventPointerPress(...)`同步发布，Raycaster才捕获目标并发出WebView输入。

所以`Raycaster.OnPointerPress(...)`和Vuplex发送处都晚于已有队列，不能在那里把旧动作登记成“当前撤销代次”的新动作。最早已定位的冻结位置是`EnqueuePointerPressEvent(Begin)`入队之前；这只是托管层观察时间，不证明硬件或SteamVR内部发生顺序。

## 记录约束

- `NextPointerPressId`是设备实例字段；不同设备可以同时有PressId=1。单独PressId不是全局来源，设备枚举也不能自动区分实例替换。
- `PointerPressEvent`值只有InputSource、Button、PressId、InputMethod、Phase、UnscaledTimestamp，没有设备实例或撤销代次。现同步发布不自动把来源实例带进参数；接线必须保留原设备来源上下文，不能假装字段已存在。
- 入队前尚未完成本帧命中。此时先冻结动作身份／撤销代次，实际捕获目标后再绑定WebView／文档；不能用旧hover代替本帧目标，也不能在补绑时刷新原代次。
- Hold在0.25秒后入队，End/Cancel沿同一PressId；每个phase的新时间不是新授权。无活动press的Cancel还可能带旧字段，不能据此新建资格。
- WebView按下并不等待Hold：捕获后立即Prepare与SendDown。Prepare可先调用`SetFocused(true)`，能力降级路径还可在结束时发送Click。因此来源检查要覆盖准备、按下、后续阶段及fallback，不能只拦PointerDown。

## 原样方法切片对照

临时.NET夹具提取4个来源文件的16个原样切片，核验整文件／切片哈希与内容。预声明6种顺序：旧Begin排队后变更撤销代次、两设备同ID、Hold/End继承、活动Cancel、已结束后的inactive Cancel、没有Begin的新设备Cancel。共12次观察，发布时才登记有7次不符（含2次缺来源）；入队前只按PressId保存有1次错配；按设备实例＋PressId保存无错配。计数是构造轨迹，不是游戏发生率；单键冲突策略换成拒绝或保留旧值，也不能在同一键中表示两个来源。

原Cancel还证实会删除队列中的Begin，再发直接／排队Cancel；inactive Cancel沿旧ID发出，不产生新Begin。不能要求“发布过Begin才认识其取消”，也不能把迟到Cancel重新当新授权。

夹具在调用原Begin前快照、返回后绑定实际ID，显式传递设备调用上下文；未修改原方法体，未模拟方法内部跨线程调度。只替代Time／Unity等外围依赖，未提取轮询、命中或原生发送。没有执行Harmony钩子、生产授权、目标绑定或真实VR输入。

这些证据收敛了[注入方案](foreground-direct-injection.md)的接线位置；仍须验证实际生产身份传递、记录回收、源状态更新到Begin之间的时间边界、导航来源及完整手势保真，不能当成完整方案通过。

## 更早的时间字段仍不是严格排序凭据

安装SteamVR支持`GetTimeLastChanged(source)`：数字action更新先计算`changedTime = Time.realtimeSinceStartup + actionData.fUpdateTime`，再触发onStateDown；GetStateDown本身只读缓存。Valve将[fUpdateTime](https://github.com/ValveSoftware/openvr/blob/0924064316de3effbcd1acf1e309182a2deb1c05/headers/openvr.h#L5230-L5245)定义为事件相对now的负时间，并说明action结果保持到下次UpdateActionState。它提供比后续托管Begin更早的变更信息，但不是全部边沿队列或精确硬件时间保证。

现游戏只读按钮GetState与TriggerDepth.axis；后者以`>0.3f`／`<0.2f`形成滞回，不能拿数字TriggerClick的时间代替模拟阈值首次越界。公开changedTime是float；独立二进制32位模型3例中，假设撤销在3600.001秒，之前3600.00099与之后3600.00101的事件都可返回3600.0009765625。仅凭该值无法区分这两种顺序；模型不等于真实SteamVR误差／发生率，也不否定原始偏移或更新序列等其他候选。本轮未选择跨时钟授权算法，不能把“更早可观察”扩大成严格来源排序已解决。
