# 注入适配的编辑基线

> 历史归档（2026-11-10）：保留调查时的事实、方案与待验证项。当前实现以源码、测试、已接受的 [ADR](../../adr/) 和[路线图](../../ROADMAP.md)为准。

2026-11-09。这是是否接管原生编辑责任之前的调查证据，不是已接受的产品设计；方向与未解项见[直接注入](foreground-direct-injection.md)。

本地 headless Chromium 158.0.8127.18 直接加载安装版 speech-view、toolbar、state、CSS 与字体；业务和 focus callbacks 为观察 stub。实际 input 宽700px、字体20px Roboto Flex／Noto Sans。13例未修改任何安装文件，20个服务文件前后hash相同；不覆盖完整页面、Vuplex/CEF、原宿主或VR。

| 操作                              | 该次原生结果                                                      |
| --------------------------------- | ----------------------------------------------------------------- |
| 点击 `abcdefgh` 边界3             | caret 3/3                                                         |
| 正／反向拖选2↔7；Shift-click      | 2/7；保留 forward／backward                                       |
| 双击 `alpha beta gamma` 的beta    | 6/11，包含尾随空格                                                |
| 触摸点击边界3                     | caret 3/3                                                         |
| Tab由mic进入／Shift+Tab由send返回 | composer全选0/8；各发一次focus request                            |
| Tab由composer离开                 | send获焦                                                          |
| 发送pending                       | composer只读但可获焦；禁用mic/send/clear；Tab跳到pin              |
| 按clear                           | 安装pointerdown处理保留composer焦点，callback一次；真实业务未执行 |
| `a🙂éz` 上三次ArrowRight          | UTF-16偏移1、3、5                                                 |
| 已横向滚动的长文本中点击边界187   | scrollLeft1258，caret187/187                                      |

原run保留18/21检查、`pass:false`：双击原期望6/10与真实6/11不符；另两项的滚动前提无效，计算点落在input外。没有改断言重跑。独立滚动修正先观察到真实滚动且点在input内，再点击，9/9通过；这不抹去原失败，也不证明所有字体、布局或CEF行为相同。两次运行均无浏览器／服务错误或非本地请求。

## 标准坐标能力

另四例只读对照中，`document.caretPositionFromPoint(x,y)` 返回实际INPUT及值内偏移，ASCII／emoji／希伯来文／已横向滚动分别为3、3、3、187，与同点原生点击完全一致；查询未改选区。`caretRangeFromPoint` 四例只返回外DIV/0，不能替代。20个服务文件前后hash相同，无浏览器／服务／请求错误。

[CSSOM View](https://drafts.csswg.org/cssom-view/#dom-document-caretpositionfrompoint)明确文本控件分支返回控件及UTF-16偏移。安装libcef文件版本为`137.0.17+gf354b0e+chromium-137.0.7151.104`；[同版Chromium](https://chromium.googlesource.com/chromium/src/+/137.0.7151.104/third_party/blink/renderer/core/dom/document.idl#202)公开该API且功能为stable。上述运行仍为headless158，不证明安装CEF运行配置或所有命中边界。坐标定位因此已有标准候选，不必先自写字宽算法；它不执行聚焦、拖选取消、双击分词或Tab。

接管责任若获同意，下一隔离原型须对照这些结果，并补取消、两手竞争、复杂文字、动态导航及实际CEF。不能把`focus()+dispatchEvent()`视为已保留它们；不能用此局部基线支持完整来源／撤销协议或真实VR验收。
