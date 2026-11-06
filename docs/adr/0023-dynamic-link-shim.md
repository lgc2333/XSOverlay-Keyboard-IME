---
status: accepted
supersedes: []
superseded_by: []
amends: []
amended_by: []
---

# IngameIME 以动态库加自有 C 薄壳接入

仓库存放 IngameIME 的两个上游仓库作为子模块，构建出 `IngameIME_Shared.dll`；插件再通过一个自有的 C ABI 薄壳（约 135 行）调用它。薄壳与共享库随插件一起分发，放在插件 DLL 旁边。

## Considered Options

- 静态链接进插件 DLL：LGPL 2.1 第 6 节要求提供可重新链接的目标文件，插件就不再是一个可交付文件；否决。
- 直接用 SWIG 或 P/Invoke 绑 C++ 类（`IngameIME_Sharp` 那条路）：C++ 名字修饰与 STL 类型跨 ABI 不稳，Mono 也不支持该绑定方式；否决。
- 动态库 + 自有 C 薄壳：采纳。

## Consequences

- 分发从一份变成三份：插件 DLL、`IngameIME_Shared.dll`、`iime_shim.dll`。
- 构建共享库必须加 `-DCMAKE_WINDOWS_EXPORT_ALL_SYMBOLS=ON` 与 `-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreadedDLL`。上游默认把运行库写死为静态 `/MT`，两个 DLL 各带一份 CRT 堆时，跨 DLL 释放 `std::string` 会崩。
- `InputContext::Version` 是静态数据成员，上述导出机制不导出它，薄壳不得引用。
- 薄壳的约定固定为：`cdecl`、UTF-8 文本、`void*` 句柄、回调经薄壳转成 C 函数指针。C++ 类型不出现在 C# 一侧。
