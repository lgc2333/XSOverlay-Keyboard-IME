# 显式编辑注入：有限验证

> 历史归档（2026-11-10）：保留调查时的事实、方案与待验证项。当前实现以源码、测试、已接受的 [ADR](../../adr/) 和[路线图](../../ROADMAP.md)为准。

2026-11-09。[注入方向](foreground-direct-injection.md)下的可丢弃实验，未采纳产品编辑责任，未实施产品。

同一同步函数接收文档／动作／撤销代次，执行实际focus和选区，再返回原记录。内部坐标用标准caretPositionFromPoint，不依赖另送原生点击；Tab在受控DOM中显式导航。安装模块／CSS／字体不变，业务回调为观察桩，运行于headless Chromium158，非整页、安装CEF或VR。

13个native/injected独立context对照的末态一致：光标3/3、正反拖选2/7及方向、Shift点击、双击6/11、已滚动长文本187/187、触摸点、Tab进入全选／离开／跳过禁用按钮、clear一次且保焦、复杂字素1/3/5。比较仅覆盖焦点、文本、选区、回调；没有证明原生事件信任、兼容事件或完整业务等价。

另6项本地模拟验证旧源拒绝／fresh接受、旧move/clear隔离、错误文档拒绝和本地cancel回收；没有调用原STA，没有真实Reload或VR取消。

## 边界反例

独立6例仍为pass:false、仅1例末态一致，未改旧结果或调参重跑。

| 情形               | 原生／临时操作                        |
| ------------------ | ------------------------------------- |
| 从3拖出左右边界    | 原生0/3或3/8；临时仍3/3               |
| 长文本拖出右边     | 原生本次3/72；临时3/3                 |
| RTL从0三次右移     | 原生0/0/0；逻辑字素替代1/2/3          |
| 混排从3三次右移    | 两者4/5/6，仅此例一致                 |
| 动态disabled输入框 | 临时focus未成功却回报资格，且改变选区 |

因此标准坐标能力降低了适配成本，但不等于完整编辑器。必须补控件外选择／滚动、实际focus后验校验；不能把逻辑字素递增当作通用视觉箭头。普通编辑键保留原生的接线仍需核验，不能借此省略来源与撤销检查。

## 窄修订仍未闭合

另存候选，未覆盖原版或失败：内部clamp让短文本出界拖选匹配；长文本仍原生3/72、临时3/68。按住右边界外但不再移动时，原生从72继续到80，修订版在1秒观察窗仍68；clamp不包含原生自动滚动。

disabled、同步重定向focus、focus期间换节点三例，经实际焦点／连接／身份校验后均0资格回报。只验证这一误报防护；disabled时原生清空focus而修订保留旧焦点，未声称全部末态一致。独立7例整体仍pass:false，5例检查通过、2例失败，未调参重跑；观察窗不是产品延时方案。

前两次对照各服务20个安装文件，窄修订19个，前后hash均相同，无浏览器／服务／非本地请求错误。以上不替代完整输入生产者覆盖、实际CEF、旧通知全部副作用隔离、自然失焦首次返回及真实VR验收。完整拖选／滚动保真仍须方案，不把这版作为产品实现起点。

## 可复用的原生选区能力

另做两版Chromium（137.0.7151.119／158.0.8127.18）各6例独立对照。公开`Selection.modify()`在RTL、混排及复杂字素移动／扩展上与原生一致，能替代已被反例否定的逻辑偏移。但长文本扩展虽同为3/103，原生scrollLeft=361、API=0；至行尾同为3/192，原生1258、API=0。共8/12完整末态一致，候选仍未通过；22个安装文件不变、错误及非本地请求0。CfT137不是安装CEF137.0.7151.104的运行证据。

同版Chromium源码解释了边界：`Selection.modify`调用原生SelectionModifier，但标记为`kSystem`；原生移动为`kUser`，后者才走RevealSelection。它可减少自写视觉算法，不能提供持续拖选或原生滚动。两版`execCommand`的五种Move命令均不支持／不执行，SelectAll正对照成功；不能借内部命令补齐。

依据：[Selection API](https://w3c.github.io/selection-api/#dom-selection-modify)、[Chromium137 modify](https://chromium.googlesource.com/chromium/src/+/137.0.7151.104/third_party/blink/renderer/core/editing/dom_selection.cc#429)、[用户滚动分支](https://chromium.googlesource.com/chromium/src/+/137.0.7151.104/third_party/blink/renderer/core/editing/frame_selection.cc#342)。完整编辑责任仍待选择，不把“有原生选区API”升级为“有完整编辑适配器”。

这些箭头对照用于判别公开选区能力，不是替换实体编辑键的决定；[0054](../../adr/0054-write-ordinary-text-through-host-results.md)的浏览器默认编辑路由仍有效。即使接管聚焦交互，也不能据此把普通文字、删除、撤销一并改为脚本编辑。

## 滚动仍需独立方案

只读核查同版Chromium，原生拖选由[持续动画会话](https://chromium.googlesource.com/chromium/src/+/137.0.7151.104/third_party/blink/renderer/core/page/autoscroll_controller.cc#458)按最后鼠标位置反复更新选区和滚动，release、pressed失效或对象消失时停止；不是每次move设一次偏移。另一条选区揭示路径使用内部caret几何。公开`input.scrollIntoView()`针对[元素框](https://chromium.googlesource.com/chromium/src/+/137.0.7151.104/third_party/blink/renderer/core/dom/element.cc#1938)，`scrollLeft`只是数值滚动原语；本次未找到直接接管原生拖选会话的页面接口。不能把缺口简化成补一次scrollIntoView，也不能把内部C++当作公开可调用API。

这不是新增运行证据，没有验证安装CEF、touch/pen或完整失焦／导航停止链。若接管责任获接受，须明确持续推进、取消、目标失效与两手竞争的完整方案，并与原生对照；本次不选择动画策略或靠等待调绿旧样本。

另有[同步回调的条件性反例](foreground-injection-callbacks.md)：若允许页面回调再入临时操作入口，旧函数可在返回后覆盖新选区／owner；未证实该再入在实际接线中可达，不据此添加产品机制。
