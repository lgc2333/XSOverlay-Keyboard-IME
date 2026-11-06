---
status: accepted
supersedes: []
superseded_by: []
amends: []
amended_by: []
---

# IngameIME 以动态库加自有 C 薄壳接入

仓库存放 IngameIME 的两个上游仓库作为子模块，构建出一个动态库 `IngameIME.dll`。插件通过一个自有的 C ABI 薄壳（`native/shim/shim.cpp`）调用它；薄壳与上游源码编进同一个库，整份库按 LGPL 2.1 发布，随插件一起分发。

## Considered Options

- 静态链接进插件 DLL：LGPL 2.1 第 6 节要求提供可重新链接的目标文件，插件就不再是一个可交付文件，而且插件会整体受 LGPL 约束；否决。
- 直接用 SWIG 或 P/Invoke 绑 C++ 类（`IngameIME_Sharp` 那条路）：C++ 名字修饰与 STL 类型跨 ABI 不稳，Mono 也不支持该绑定方式；否决。
- 上游共享库与自有薄壳分作两个 DLL：多一个文件；两个模块各带一份 CRT，只能靠两侧都钉同一个运行库来规避；为了让薄壳看见上游符号，还得导出整份库的全部符号；否决。
- 动态库 + 自有 C 薄壳，编进同一个库：采纳。

## Consequences

- 分发是两份：插件 DLL 与 `IngameIME.dll`。
- 薄壳源码随整份库按 LGPL 2.1 发布；仓库其余部分仍是 MIT。许可证全文与来源声明见 [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md)，许可证文本随仓库放在 `native/LICENSE-LGPL-2.1.txt`。
- 构建必须加 `-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreadedDLL`：上游默认把运行库写死为静态 `/MT`。
- 不需要 `CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS`：C++ 符号在同一个模块内解析，导出表只剩薄壳的 C 函数。
- `InputContext::Version` 是静态数据成员，没有导出，也无人引用；薄壳不得引用它。
- 薄壳的约定固定为：`cdecl`、UTF-8 文本、`void*` 句柄、回调经薄壳转成 C 函数指针。C++ 类型不出现在 C# 一侧。
