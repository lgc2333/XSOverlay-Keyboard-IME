---
status: accepted
supersedes: []
superseded_by: []
amends: []
amended_by: []
---

# 以 BepInEx 插件注入进程内，并使用 C# 实现

候选要显示在 XSOverlay 自己的键盘页上，而那是进程内的 Vuplex WebView，只有进程内代码才能同时拦截键盘事件和操作页面。因此插件以 BepInEx 5 注入 XSOverlay，用 C#（netstandard2.1）实现；外置程序和独立输入法两个方向都否决。

## Considered Options

- 外置程序 + 全局键盘钩子。否决：无法操作键盘页，也无法进入 XSOverlay 的焦点体系。
- 独立注册为系统输入法。否决：需求是给 XSOverlay 的键盘补输入法，不是取代用户已有输入法。
- BepInEx 插件。采纳。

## Consequences

- 受 Unity Mono 运行时限制，见 [0003](./0003-mono-classic-ccw.md)。
- 构建时引用游戏自带程序集，不能使用版本不符的桩包。
