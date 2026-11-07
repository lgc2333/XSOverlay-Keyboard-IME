---
status: accepted
amends: ['0035']
---

# 输入法下拉框：列出当前语言的档案，选中即激活

[0035](./0035-show-the-ime-only-while-the-composer-has-focus.md) 只把正在用的输入法名字画出来，用户没法换。路线图「未实现」第二条要求能在这几个输入法之间切换。

[0030](./0030-follow-the-foreground-language.md) 被否，是因为它**替用户**跟随系统语言、轮询别的窗口。这里的切换是用户自己在下拉框里点出来的显式动作，性质不同。

本决定：**列当前语言的 TSF 档案，用户选中哪条，就在宿主线程激活哪条。**

- 列表：`ITfInputProcessorProfiles::EnumLanguageProfiles` 列出当前语言的档案，每条的描述就是名字；没有描述的档案不列，所以索引是**过滤后**的顺序。只有 composer 有焦点时才列、才发给页面。
- 切换：`ActivateLanguageProfile(clsid, langid, guid)` 在本线程生效，随后重读名字、语言栏按钮与图标。索引就是枚举顺序，页面拿到的是同一个顺序。
- 组字中选中另一条：先把编码上屏（[0038](./0038-latched-modifiers-stay-with-the-page.md) 的规则），再激活；不让旧组字跟到新输入法里。
- 不做：不轮询、不读别的窗口、不切语言。换语言仍然只有 Win+空格 一条路。

## Considered Options

- `ActivateKeyboardLayout`：只换键盘布局，同一语言下的两个输入法换不动；否决。
- `ChangeCurrentLanguage`：换的是语言而不是输入法，会跳过用户的输入法；否决。
- `WM_INPUTLANGCHANGEREQUEST`：交给系统窗口消息，影响面是整机而不是本线程；否决。
- 继续只显示名字（[0035](./0035-show-the-ime-only-while-the-composer-has-focus.md) 现状）：用户没法换，还得回桌面；否决。
- 列当前语言档案并用 TSF 激活：采纳。

## Consequences

- 下拉框只列**当前语言**的输入法。中日英之间的切换仍要 Win+空格，那条路已经在 [0036](./0036-one-table-for-key-ownership.md) 里交给系统。
- 激活是线程级动作，只影响宿主窗口这一份输入上下文；桌面上的当前输入法不变。
- 档案可能在两次枚举之间变化（用户装/卸输入法），索引只在最近一次列表内有效；页面每次拿到的都是新的列表。
- 组字中换输入法会先上屏，和切语言同一条规则，代价与 [0038](./0038-latched-modifiers-stay-with-the-page.md) 相同。
