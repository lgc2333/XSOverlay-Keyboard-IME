# 2026-11-10 首开前台失败追查

本轮接续[五项缺陷调查](roadmap-defects-2026-11-10.md)，追查 `0xF2` 状态与首次点击的前台获取，不修改产品策略。

根因已定位到插件的空闲判断：它把全部虚拟键的异步 down 状态当作持续按键，但 Windows 日文 NLS 可以用该状态保留模式虚拟键。`0xF2` 因此持续阻止 Alt 回退，资格获取失败使输入法下拉框隐藏。

## 已确认的失败路径

干净启动后，原生 Vuplex 点击再次产生可信 `pointerdown`，目标为 `keyboard-voice-input`。宿主收到资格请求，但普通前台获取失败，空闲检查返回 false，随后采样只列出 `242`。页面最终失焦，下拉框继续隐藏。

| 观察器时间（ms） | 事件                                                 |
| ---------------: | ---------------------------------------------------- |
|        30682.098 | `ImeHost.AcquireFocus` 开始                          |
|        30695.374 | 普通前台获取失败后，`Unchanged=true`                 |
|        30700.910 | `KeysAndMenusIdle=false`，随后采样为 `pressed:[242]` |
|        30706.583 | 获取失败，`EndFocus` 结束资格                        |

本轮使用 XSOverlay 进程 `33684`，前台保持为 Steam。产品 DLL 的 SHA256 为 `E3C9CEFDFC69EBE329E6922929167ECAA1248694804B18EFD51FD4E9B026FE10`，MVID 为 `065b31a6-7cb3-4fc1-948d-3dec8126908a`。这与前次调查版本相同，未部署后来完成的四项修复。

调查时的[空闲检查](../../../XSOverlayKeyboardIme/Ime/ForegroundLease.cs)遍历 `1..255`，任一异步键状态高位非零就停止。因此，本轮再次确认了该状态阻止 Alt 回退的路径，但没有复现用户点击桌面浮窗后的完整动作。

前次调查的独立 `AllowSetForegroundWindow` 对照已证明：只授权当次 XSOverlay PID，普通获取便成功，输入法名称随即出现。[前台算法](../../../XSOverlayKeyboardIme/Takeover/ForegroundAcquisition.cs)先尝试普通获取，仅失败后才检查空闲状态。因此，已有前台资格时可以绕过这个失败点。

## 状态跨越 XSOverlay 退出和启动

独立进程每隔约 200ms 读取异步键状态，不注入按键、不请求前台，也不读写输入法模式。

| 采样阶段                                             | 样本数 | 每个样本的全部 down 键 | `0xF2` 原始值 |
| ---------------------------------------------------- | -----: | ---------------------- | ------------- |
| 原 XSOverlay 进程运行时，约 8 秒                     |     39 | `[242]`                | `0x8000`      |
| 确认 XSOverlay 已退出、Steam `Running=0` 后，约 5 秒 |     25 | `[242]`                | `0x8000`      |
| 新 XSOverlay 启动后、点击之前，约 4 秒               |     19 | `[242]`                | `0x8000`      |

该状态不依赖当次 XSOverlay 进程继续存活，重启插件没有清除它。这不能证明最初是谁产生该状态，也不能证明有人仍按着实体键。

`MapVirtualKeyExW(0xF2, MAPVK_VK_TO_VSC_EX, hkl)` 返回 `0`，只表示所用布局没有该转换，不足以判定虚拟键无效或应当忽略。

## `0xF2` 同时是日文 IME 虚拟键

Windows SDK `10.0.26100.0` 的 `WinUser.h` 定义 `VK_OEM_COPY=0xF2`，`Ime.h` 同时定义 `VK_DBE_HIRAGANA=0xF2` 和 `VK_DBE_ALPHANUMERIC=0xF0`。它不是功能键 F2，后者为 `0x71`。

历史运行日志先记录 `[ime] feed LeftControl down`，随后记录 `osCtrl=True` 和 `modifiers=Control`。插件喂入 `Capital press` 后，同一个输入 tick 出现以下低级键盘事件：

|    tick | 事件                      | 虚拟键 | flags            |
| ------: | ------------------------- | ------ | ---------------- |
| 1514046 | key up，`message=0x101`   | `0xF0` | `0x90`，injected |
| 1514046 | key down，`message=0x100` | `0xF2` | `0x10`，injected |

当时宿主线程 HKL 为 `0x4110411`。之后还有 71 条低级键事件，最后一条距离 `0xF2` down 约 54.829 秒，没有 `0xF2` up。观察器最终在该 down 后约 166.721 秒到期。

另一独立会话也在 `Capital press` 后记录同 tick 的 `0xF0` up 和 `0xF2` down；随后约 89.719 秒内有 81 条其他低级键事件，没有 `0xF2` up。两次都符合下面的 NLS 路径，事件的前台 PID 不代表发送者 PID。

微软[日文键盘布局示例](https://github.com/microsoft/Windows-driver-samples/blob/d49bb7d72f3651952b2b6e4ad28d14fe2247eb69/input/layout/fe_kbds/jpn/101/kbd101.c#L483-L497)把 `VK_CAPITAL` 接到 NLS 模式处理，并列出 `KBDNLS_ALPHANUM`、`KBDNLS_HIRAGANA` 和 `KBDNLS_KATAKANA`。这支持模式事件的关联，不说明异步状态寿命。

## 本机 Windows 实现闭合了异步状态链

静态核对本机系统文件，并使用 PE 内的 RSDS 标识匹配[微软公开 PDB](https://msdl.microsoft.com/download/symbols/win32kbase.pdb/7BD4193FFA935A0483172A97C84B455D1/win32kbase.pdb)。没有调用输入法状态 API、注入按键或安装内核调试器。下表中的地址全部为文件 RVA。

| 文件                                 | 版本或标识                                           | SHA256                                                             |
| ------------------------------------ | ---------------------------------------------------- | ------------------------------------------------------------------ |
| `C:\Windows\System32\win32kbase.sys` | `10.0.26100.9549`                                    | `27CB5E74B2E05C3A2C10D00CC216285F9DCA48854C6CFB57DF5E4D3731EC9AA3` |
| `win32kbase.pdb`                     | GUID `7BD4193F-FA93-5A04-8317-2A97C84B455D`，age `1` | `9E193C394833B463839D5E0BAE548C9A475253FBB157C4ED78BC7BEF9A1F4C1E` |
| `C:\Windows\System32\kbdjpn.dll`     | `10.0.26100.2454`                                    | `5293B7B0453858137B32A8CF5F294FC2AC9E3BB673FA050C47C2D1600176737A` |

本机日文布局注册项 `00000411` 指向 `KBDJPN.DLL`。其 `KbdNlsLayerDescriptor` 返回 RVA `0x2000` 的描述符，Capital 条目位于 RVA `0x2840`。Ctrl 对应 action `6`；`win32kbase!GenerateNlsVkKey` 的分派表 RVA `0x265410` 将 action `6` 指向 `NlsHiraganaModeProc`，RVA 为 `0x222ea0`。

这条处理链执行以下动作：

1. `xxxProcessKeyEvent` 在 `0x118f78` 调用 `xxxKENLSProcs`，返回零便丢弃事件，否则继续调用 `xxxKeyEventEx`。
2. `NlsHiraganaModeProc` 检查 key up 标志，key up 直接返回零。key down 选择 `0xF2`，必要时从 `f0 f2 f1 00` 表查找旧 NLS 模式键并发送其 key up，再把原事件改写为 `0xF2` down。
3. `xxxKeyEventEx` 经 `xxxUpdateGlobalsAndSendKeyEvent` 调用 `UpdateAsyncKeyState`。后者在会话状态的 `+0x3bfc` 表设置该键的 down 位。
4. `_GetAsyncKeyState` 读取同一会话表的同一 down 位，存在时返回 `0x8000`。

关键指令如下，省略中间指令，目标符号按匹配 PDB 标注：

```asm
; NlsHiraganaModeProc: key up 返回 0，key down 改写为 F2
00222eca: test ax, ax
00222ecd: js   0x222fa5
00222ed8: mov  r12d, 0xf2
00222f41: or   cx, ax              ; ax = 0x8000，旧模式 key up
00222f6d: call xxxKeyEventEx
00222f72: or   word ptr [rbx + 2], r12w
00222fa5: xor  eax, eax

; UpdateAsyncKeyState: 设置会话异步 down 位
0017a880: movzx ecx, byte ptr [rax + rbx + 0x3bfc]
0017a888: bts   ecx, r15d
0017a88c: mov   byte ptr [rax + rbx + 0x3bfc], cl

; _GetAsyncKeyState: 从同一表生成返回值的最高位
00172705: mov ecx, 0xffff8000
0017270a: and sil, byte ptr [rax + rdx + 0x3bfc]
00172716: or  ax, cx
00172719: test sil, sil
0017271c: cmove ax, r15w           ; 没有 down 位时恢复原返回值
```

触发键松开不产生配对的 `0xF2` up。`NlsAlphanumericModeProc` 则有相反方向的旧模式释放路径，可以在后续 NLS 模式事件中释放 `0xF2`。该位属于会话异步状态，应用退出不需要清除它，也不能据此推断当前输入法中英模式。

先前查到的 ReactOS 实现只证明线程队列状态具有模式语义，不能直接解释异步查询。本次确认的是 Windows 本机写入和读取同一异步表的完整路径，补齐了这个证据缺口。

## 归因

来源分类为插件缺陷：空闲检查把模式虚拟键状态当作持续按键，错误地阻止了前台回退。Windows 的 NLS 状态保留是系统基线，页面隐藏只是资格获取失败的结果。

历史 Ctrl+Capital、成对的旧模式 up／新模式 down、本机 NLS 映射与跨进程重启采样相互吻合。历史捕获没有内核调用栈，不能声称确认了那一次事件的精确内核分派或发送者 PID；这个边界不改变空闲判断把两种语义混用的缺陷。

调查建议重新界定 [0055](../../adr/0055-use-one-alt-pair-after-a-denied-focus-request.md) 的空闲判定范围，并保留真实按键与活动菜单的保护。具体键集合随后由 [0063](../../adr/0063-exclude-nls-mode-keys-from-the-foreground-idle-check.md) 决定。

本轮已移除临时观察器，核对全部原插件 DLL 的哈希，并经 Steam 重启 XSOverlay。没有输入正文、发送 OSC、切换输入法模式或修改产品代码。

## 修复与真机验收

随后实现 [0063](../../adr/0063-exclude-nls-mode-keys-from-the-foreground-idle-check.md)：所有输入项统一排除 `0xF0/0xF1/0xF2`，保留其余键、鼠标按钮、菜单和资格检查。同码 `OEM_ATTN/FINISH/COPY` 不再阻止 Alt 回退，这是已接受的取舍。

部署当前完整构建，包含此前四项修复。产品 SHA256 为 `BB2BB0B2EEEAB8FE0D5C1BE7363156780B96050218314ACCBDEA810ACC4D9050`，运行程序集 MVID 为 `20dd9863-15f3-4556-86f4-e2471dfe914a`。

XSOverlay 进程 `41128` 从关闭状态打开键盘，首次原生 Vuplex 点击命中空输入框，页面记录可信指针事件。前台原为 Steam，`0xF2=8000`，全部 down 键仍仅为 `[242]`。

| 观察器时间（ms） | 结果                                            |
| ---------------: | ----------------------------------------------- |
|        46904.647 | 开始获取，普通 `SetForegroundWindow` 返回 false |
|        46912.362 | `KeysAndMenusIdle=true`，随后采样仍为 `[242]`   |
|        46918.205 | 再次确认原前台及资格未变                        |
|        46925.112 | Alt 批次已送入 `2/2`，再次确认资格未变          |
|             随后 | 第二次前台请求成功，宿主取得前台，资格为 active |

页面快照确认 `focused=true`、`chipHidden=false`，名称为“小狼毫”，列表有四个输入项。异步 down 键仍只有 `[242]`，没有残留 Alt 或其他修饰键。

关闭键盘后资格变为 inactive，前台归还 Steam。重新打开并首次点击，普通前台请求直接成功，未再注入 Alt；页面再次显示输入法列表。

167 项单元测试与 50 项浏览器回归通过，Release 构建零警告、零错误。回归覆盖三个模式码、混合按住键、低位状态及空闲检查期间撤销资格或改变前台身份。

外部切窗验收未完成：自动化工具刷新窗口并重试后仍报 `failed to activate captured window`。实体按住键与活动菜单的真机阻断也未执行，相关策略由单元测试覆盖，不能替代真实输入验收。

验收未输入正文、发送 OSC 或读写输入法模式。临时观察器已移除，其他原插件哈希未变，保留修复版并经 Steam 重启 XSOverlay。

参考：[虚拟键表](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes)、[GetAsyncKeyState](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate)、[MapVirtualKeyExW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-mapvirtualkeyexw)、[低级键盘事件结构](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct)。
