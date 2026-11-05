---
status: accepted
supersedes: []
superseded_by: []
amends: []
amended_by: []
---

# 只通过 TSF 接入系统输入法

选中的输入法是普通 TSF 文本服务，组字与候选只通过 TSF 暴露。插件因此只实现 TSF 客户端，不直接驱动 librime／rime.dll：后者绕过用户实际安装的输入法，把可用范围绑死在一个引擎上，也拿不到系统候选窗的一致语义。

## Considered Options

- 直接调用 librime，自行渲染候选。否决：绑死单一输入法，且与用户其他输入法互斥。
- 通过 TSF 接入。采纳。

## Consequences

- 插件需要自建 TSF 组字上下文，见 [0006](./0006-composition-host.md)。
- 只支持 TSF 输入法；纯 IMM32 的输入法不在支持范围内。
