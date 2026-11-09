---
status: accepted
supersedes: ['0006']
amended_by: ['0050', '0059']
---

# 组字宿主改用 IngameIME

[0006](./0006-composition-host.md) 让插件自建 TSF 上下文：隐藏 RichEdit 当文本存储，组字串与提交从 IMM 和控件文本里读，候选靠自己挂的 COM sink。实测表明这套读取路径的每一环都在猜：组字串要看用户是否开了行内预编辑，提交要比较控件文本与基线，候选要维护 sink 与元素队列。IngameIME 把这四件事都变成回调，插件只保留线程、窗口与喂键。

## Considered Options

- 保留自建 RichEdit 宿主，见 [0006](./0006-composition-host.md)：能跑，但上述三处读取都靠推断；否决。
- 自己实现 `ITextStoreACP`：掌控力最强，但需实现二十多个方法，且正是 IngameIME 已经做完的事；否决。
- 直接调 librime：绑死单一输入法，[0001](./0001-tsf-only.md) 已否决。
- 用 IngameIME 托管输入上下文：采纳。

## Consequences

- 组字串、候选、提交、输入法模式全部改走回调；候选是 UTF-8 字符串列表，非 BMP 无损。11 类按键与真实控件的行为对照见 [26-11-06-7](../archived/handoffs/26-11-06-7_ingameime-validation.md)。
- 插件不再实现任何 TSF 接口，也不再读 IMM。`ITfUIElementSink`、候选元素读取、`ImmGetCompositionStringW`、控件文本基线全部删除。
- 宿主仍需要一个隐藏窗口与 STA 线程：输入上下文绑定在窗口上，按键消息与组字都落在该线程。
- 喂键仍走全局 `SendInput`，且每次按键前都要断言宿主窗口在前台。抢占前台的行为必须记录并在结束时恢复，[0014](./0014-vr-session-scope.md) 的支持范围不变。
- 输入法处于英文模式时，按键原样直通为 `WM_CHAR`，不再经过控件文本。宿主窗口自己收集这些字符作为「未组字文本」写回，取代原来的控件文本读取。
- 组字串仍取决于输入法的行内预编辑，[0013](./0013-inline-preedit-dependency.md) 不变。
