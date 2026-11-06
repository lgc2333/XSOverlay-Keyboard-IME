---
status: superseded
supersedes: []
superseded_by: ['0028']
amends: []
amended_by: []
---

# 中英切换键在运行时合成进布局数据，不修改布局文件

VR 键盘的 Shift 是锁定式，不适合当中英切换键，因此需要一个独立按键。布局文件是所有 overlay 应用共享的，直接修改会被 Steam 更新覆盖，也会污染用户自定义布局。所以在渲染前把该键插入布局数据，并从空格键扣掉等宽；JIS 布局不插入。

## Consequences

- 注入逻辑要在布局缺少左 Alt 或空格键时优雅退化，而不是抛出异常。
- 该键与游戏文件的版本兼容无关，Steam 更新不会让它失效。
