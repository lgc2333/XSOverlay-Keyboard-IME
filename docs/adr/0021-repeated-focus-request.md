---
status: accepted
---

# 同一 overlay 的重复键盘焦点请求直接放行

键盘页的输入框每次获得焦点都会向 XSOverlay 请求键盘焦点，而且每次带一个新的 focusId。XSOverlay 发现已有焦点就 `Release("replaced")` 再重新授予；收回时会 `SetFocused(false)`，输入框当场失焦，失焦又触发下一次请求，于是两边来回夺焦点，表现是青色描边闪烁后消失、输入框看起来"没有焦点"。插件因此在 XSOverlay 的键盘焦点管理器上加前缀补丁：同一个 overlay 重复请求时直接返回成功，不收回也不重授。

## Considered Options

- 让页面不再重复请求：页面由 XSOverlay 提供，要么改它的脚本，要么拦截它的发送通道，比补丁更侵入。
- 接受闪烁：用户报告为缺陷，且会导致输入框随机失焦。
- 给 XSOverlay 的焦点管理器加前缀补丁：采纳。

## Consequences

- 影响面限于"同一个 overlay 已持有键盘焦点时再次请求"这一种情况；换输入框、换 overlay 的请求照旧走 XSOverlay 原逻辑。
- 补丁只挡重复授予。真正的失焦若来自别处（桌面点击、输入框失焦上报），仍按 XSOverlay 原逻辑处理。
- 这是唯一一处改动 XSOverlay 自身行为的补丁。XSOverlay 方法改名会让它失效，因此启动时的补丁校验也检查这一处，缺失时只报警告。
