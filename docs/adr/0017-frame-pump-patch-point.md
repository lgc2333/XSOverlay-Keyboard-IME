---
status: accepted
supersedes: []
superseded_by: []
amends: []
amended_by: []
---

# 帧泵挂在游戏的 InputManager.Update 上

插件自己的 MonoBehaviour 活不过启动：游戏在 `Awake` 之后立即禁用并销毁 BepInEx 的加载器对象，插件的 `Start`、`Update`、`OnApplicationFocus` 都不再被调用，`OnDisable` 与 `OnDestroy` 照常触发。写回又必须回到 Unity 主线程。因此另加一个 Harmony 后缀补丁挂在 `XSOverlay.InputManager.Update` 上，每帧把主线程队列排空；补丁生命周期跟随进程，不随加载器对象销毁而撤销。

## Considered Options

- 用插件自己的 `Update`：实测从不被调用，否决。最初的实现就是这样，加载器销毁时又把补丁一并撤销，所有键盘事件因此静默失效。
- 用独立线程轮询队列：属于轮询，违反 [0005](./0005-event-driven.md)，否决。
- 加载器销毁时撤销补丁：接管会在启动后立刻失效，否决。
- `InputManager.Update` 后缀：采纳。

## Consequences

- 补丁点从一个变成两个。启动时必须确认两个补丁都属于本插件（按补丁 owner 校验，不能只看补丁数量），否则报错并保持空转。
- 主线程队列只在游戏每帧调用 `InputManager.Update` 时排空，写回的最坏延迟是一帧。
- 撤销只在 `OnApplicationQuit` 发生：加载器对象被销毁时不能撤销补丁。
