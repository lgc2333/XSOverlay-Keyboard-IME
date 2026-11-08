# 回车

路由规则见 [0047](../adr/0047-route-page-consumed-return-through-the-host.md)。这里记本轮「按回车没反应」的缺陷与修复状态。

## 缺陷

- 回车路由先加载原页按键模块，成功后才请求宿主。模块地址写成 `/UserInterface/Default/Keyboard/js/keyboard-input.js`，真实键盘页却从 `/apps/UserInterface/Default/Keyboard/Keyboard.html` 加载。现场只读 GET 确认前者返回 404，补上 `/apps` 后返回 200。
- 加载失败后不会请求宿主，已阻止的原事件也不会重放；失败 Promise 被缓存，后续 VR 回车继续失败。回车音效模块使用同一错误地址。
- 浏览器回归服务器也提供无 `/apps` 的地址，掩盖了该缺陷。服务器必须提供真实路径，旧路径与未知模块返回 404。

## 已实现

- 修正 [native-keyboard-input](../../web/src/native-keyboard-input.ts) 与 [return-feedback](../../web/src/return-feedback.ts) 的模块地址。
- 修正[测试服务器](../../web/tests/server.mjs)的映射。
- 保留现有按压、重复、音效、触觉与回车归属；错误后不重放按键。

## 证据

- 现有三条回车浏览器测试通过，但送键是 mock，不覆盖真实宿主往返。
- 按真实路由重跑隔离实验：DOM Enter 的空闲与组字两项通过；pointer 与 touch 四项返回零请求、零发送，并记录 `Return input failed`。只在调查脚本的内存 bundle 中补全地址后，六项全部通过。宿主答复、焦点释放与最终发送仍为模拟边界。
- 被动诊断捕获组字期间与空格提交后的回车：页面收到 Return 的 pointer、touch 按下与抬起，composer 与 document 均有焦点，但没有任何宿主 Return 请求或判定；用户确认两者均无反应。最初捕获的一次回车发生在 composer 失焦、游戏焦点为 false 时，不属于报告的有焦点场景。

## 验收

- DOM Enter、pointer、touch 在空闲与组字时共六种路径：空闲恰好走一次原发送，组字恰好请求一次宿主且保持 composer 焦点。
- 真实 VR 页面核对宿主答复、提交、音效、触觉与最终发送，集中安排一次验收。
