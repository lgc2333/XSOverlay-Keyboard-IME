# XSOverlay Keyboard IME

[简体中文](README.md) | **繁體中文** | [日本語](README.ja.md) | [한국어](README.ko.md) | [English](README.en.md)

> [!NOTE]
> 本文的簡繁轉換由 LLM 完成。您可能會遇到部分詞語表述不同的情況。

> [!WARNING]
> 本專案仍在開發階段，暫無可用成品，實作方式也仍在變動。請耐心等待。

給 XSOverlay 的 VR 鍵盤加上系統輸入法：在鍵盤頁的輸入框裡組字，組字串與候選顯示在鍵盤上，用鍵盤上的按鍵選候選，完成輸入後寫回輸入框。

## 為什麼做這個 Mod

VRChat 裡的中文輸入，試過的兩個方案都不夠用。

- [OVR Input Method Bridge](https://store.steampowered.com/app/3448100/OVR_Input_Method_Bridge/) 支援的輸入法種類少，介面也不順手，只算勉強可用。
- [Tapi](https://booth.pm/zh-cn/items/7047187) 內建一套以 Rime 為基礎的輸入方案，不與系統輸入法打通，依習慣養出來的詞庫帶不過去。

另有 [OVR Toolkit](https://store.steampowered.com/app/1068820/OVR_Toolkit/)，內建虛擬鍵盤，但已知有效能問題，因此未納入考慮；它能否輸入中文、社群有無現成方案，我同樣未實測。

正好我發現 XSOverlay 的鍵盤近期官方支援了 VRC ChatBox 功能。於是我有了這個想法：把它接上系統輸入法，養好的詞庫就能直接用。這個 Mod 由此開始。

## Showcase

待補

## 執行需求

- Windows 10 或以上版本，64 位元。
- XSOverlay Beta 695，其他版本未經驗證。
- BepInEx 5.4.23.6，x64 版本，其他版本未經驗證。
- 只支援實作了 TSF 的輸入法。本專案以 小狼毫（Rime Windows 版）驗證。
- 輸入法可能需要開啟行內預編輯，否則只看得到候選，看不到正在輸入的編碼。實測小狼毫必需，須在 `weasel.custom.yaml` 中設定 `style/inline_preedit: true`。

## 安裝

待補

## 路線圖

下一步改善 VRC ChatBox 體驗。目前版本只在鍵盤頁上顯示候選列。

- ChatBox 按鈕開啟後，組字串與候選即時送進 ChatBox，輸入過程中同步顯示。
- 輸入框取得焦點時向 VRChat 回報正在輸入，失去焦點時取消。

## 建置

需要 .NET SDK 10 或更高版本、CMake，以及 Visual Studio 的 C++ 建置工具。

先取得上游原始碼，再編譯原生函式庫：

```powershell
git submodule update --init
pwsh native/build.ps1
```

然後編譯外掛：

```powershell
dotnet build -c Release
```

把外掛與原生函式庫一起安裝進遊戲：

```powershell
dotnet build -c Release -p:DeployToGame=true
```

專案設定的遊戲路徑為 `D:\SteamLibrary\steamapps\common\XSOverlay_Beta`。遊戲不在該路徑時，用 `GameDir` 屬性指定：

```powershell
dotnet build -c Release -p:DeployToGame=true -p:GameDir="D:\Steam\steamapps\common\XSOverlay_Beta"
```

## 致謝

- [Windmill-City/IngameIME-Common](https://github.com/Windmill-City/IngameIME-Common), [Windmill-City/IngameIME_Win32](https://github.com/Windmill-City/IngameIME_Win32)：TSF 輸入上下文與組字回呼。此相依性與本專案的 C 薄殼一起編進 `IngameIME.dll`，整份依 LGPL 2.1 散布，見 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
- DeepSeek V4.1 Flash & DeepSeek Harness：本專案全程由大肥魚媽媽建構。

## 贊助

**[贊助我](https://lgck.cc/sponsor)**

感謝大家的贊助！你們的贊助是我繼續創作的動力！

## 聯絡

- QQ：3076823485
- QQ 群：[168603371](https://qm.qq.com/q/EikuZ5sP4G)
- Telegram：[@lgc2333](https://t.me/lgc2333)
- 電子郵件：<lgc2333@126.com>
