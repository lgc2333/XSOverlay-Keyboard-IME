---
status: accepted
amends: ['0027']
---

# 页面重建输入框不算失焦，用户离开才算

[0027](./0027-cancel-composition-on-caret-move.md) 让页面在组字期间盯着 composer 的失焦，失焦就取消组字。这条规则遇到一个不属于用户动作的情形：键盘页会重建自己的工具栏。

重建不是偶发。XSOverlay 在输入语言变化时给页面推 `KeyboardLayout` 订阅更新，在修饰键或锁定键状态变化时推 `KeyboardState`（`ApiHandler` 订阅了 `OnKeyboardFormatChanged` 与 `OnKeyboardStateChanged`），页面收到就重渲染工具栏，输入框随之被换掉。也就是说：切输入语言、按 Shift、按 CapsLock，都可能触发一次重建。注入脚本本来就有 `keepComposerFocus`，专为这种重建之后把焦点还给新输入框。

所以本决定：**失焦要看是谁失焦。** 失焦的元素已经不是当前 composer（被重建换掉了），不算用户离开，不取消组字；当前 composer 自己失焦（用户点了别处、键盘页关闭），按 0027 取消。

## Considered Options

- 任何失焦都取消：按 Shift 切换中英或提交原始编码时会打断组字，把正常路径也杀掉；否决。
- 重建时取消、其余失焦也取消（只看 `document.activeElement`）：与上一条等价，重建同样会打断组字；否决。
- 按「失焦的元素是不是当前 composer」区分：采纳。

## Consequences

- 重建之后组字继续：下一个输入法回调会把组字串重新拼进新输入框，用的仍是组字开始时记下的原值。
- 输入框被重建且页面没有补上新的输入框时，找不到 composer，仍然取消组字。
- 重建路径无法在控制台探针里复现（探针没有页面），因此这条只有代码推理与页面行为作依据。
