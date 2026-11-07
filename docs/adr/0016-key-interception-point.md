---
status: accepted
amended_by: ['0026', '0041']
---

# 在 XSOverlay 的 HandleKeypress 处拦截按键

接管必须在 XSOverlay 处理按键之前发生，否则组字中的回车会被 XSOverlay 的"回车释放键盘焦点"逻辑先吃掉，Esc 同理会直接释放焦点而不是取消组字。因此用 Harmony 前缀补丁挂在 `XSOverlay.InputManager.HandleKeypress`，判定为本插件所有时返回 false，其余情况原样放行。

判定为所有，需要同时满足：键盘焦点存在、焦点所属 overlay 是键盘 overlay（见 [0007](./0007-composer-scope.md)），并且该按键按下面的规则属于输入法。

## 按键规则

- 文本键（单个字符，空格除外）在焦点属于键盘 overlay 时总被接管；组字尚未开始时，它们用来启动组字。
- 控制键（回车、退格、方向键、翻页键、Esc、Delete）只在输入法确实在组字时接管，否则留在 XSOverlay 原路径。
- 判定"确实在组字"看两件事：组字宿主读到了组字串，或者已经收到输入法发布的 UI 元素，任一成立即可。只看 UI 元素会在输入法慢一拍时把整段组字丢掉，实测如此。
- 组字中的回车交给输入法，由输入法决定提交什么，通常是原始编码；退格与方向键用于修正组字。
- 组字中按下提交键（空格、回车）后输入法仍未提交时，宿主把组字串当作提交文本写回，不丢弃。用户定的语义是"组字时提交 raw 到输入框"。
- 非组字时，回车与空格走 XSOverlay 原逻辑：回车触发键盘页自己的发送，空格照常输入。

## Considered Options

- 补 `InputManager.HandleFocusedWebViewKeypress`（早先的路线）：它在回车与 Esc 的焦点释放之后才执行，组字中的回车根本到不了插件，否决。
- 在键盘页 JS 里拦截 keydown：页面同样拿不到已被 XSOverlay 先行消费的回车，且与 [0004](./0004-inject-page-script.md) 的注入范围不符，否决。
- 补 `HandleKeypress` 前缀：采纳。

## Consequences

- 依赖 XSOverlay 私有方法的类型名、方法名与参数签名。Steam 更新若改名，补丁会在启动时报错，日志可见；此时接管失效，XSOverlay 回到原路径，不会崩。
- 组字中 Esc 不释放键盘焦点，这是有意的：第一次 Esc 取消组字，取消之后再按才走 XSOverlay 的释放逻辑。
- 关闭接管只需把 `Takeover/Enabled` 置 false；补丁不再挂载，行为与未安装插件一致。
