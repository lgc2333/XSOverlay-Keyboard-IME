# XSOverlay Keyboard IME

[简体中文](README.md) | **繁體中文** | [日本語](README.ja.md) | [한국어](README.ko.md) | [English](README.en.md)

> [!NOTE]
> 本文的簡繁轉換由 LLM 完成。您可能會遇到部分詞語表述不同的情況。

給 XSOverlay 的 VR 鍵盤加上系統輸入法：在鍵盤頁的輸入框裡組字，組字串與候選顯示在鍵盤上，用鍵盤上的按鍵選候選，完成輸入後寫回輸入框。

## 為什麼做這個插件

VRChat 裡的中文輸入，試過的兩個方案都不夠用。

- [OVR Input Method Bridge](https://store.steampowered.com/app/3448100/OVR_Input_Method_Bridge/) 支援的輸入法種類少，介面也不順手，只算勉強可用。
- [Tapi](https://booth.pm/zh-cn/items/7047187) 內建一套以 Rime 為基礎的輸入方案，不與系統輸入法打通，依習慣養出來的詞庫帶不過去。

另有 [OVR Toolkit](https://store.steampowered.com/app/1068820/OVR_Toolkit/)，內建虛擬鍵盤，但已知有效能問題，因此未納入考慮；它能否輸入中文、社群有無現成方案，我同樣未實測。

正好我發現 XSOverlay 的鍵盤近期官方支援了 VRC ChatBox 功能。於是我有了這個想法：把它接上系統輸入法，養好的詞庫就能直接用。這個插件由此開始。

## 展示

待補

## 執行需求

### 系統 / 軟體版本

- Windows 10 或以上版本，64 位元。
- XSOverlay Beta 695，其他版本未經驗證。
- BepInEx 5.4.23.6，x64 版本，其他版本未經驗證。

### 輸入法

- 只支援實作了 TSF 的輸入法。本專案以 小狼毫（Rime Windows 版）驗證。
- 輸入法可能需要開啟行內預編輯，否則只看得到候選，看不到正在輸入的編碼。
  - 實測小狼毫必需，須在 `weasel.custom.yaml` 中設定 `style/inline_preedit: true`。

## 安裝

待補

## 設計取捨

本節的行為看起來像故障，其實是有意為之。

### 候選為什麼點不了

候選列只能看，點它不會有反應，也沒有翻頁按鈕和頁碼。

#### 替代方案

選候選、翻頁都請按鍵盤，實體鍵盤和鍵盤頁上的按鍵都可以，照你平常在實體鍵盤上用輸入法的習慣操作。插件只負責把按鍵轉交給輸入法，按鍵是什麼意思由輸入法自己決定。鍵位以你自己的輸入法設定為準；鍵盤頁上沒有的鍵按不了，請挑鍵盤上有的鍵用。

以我正在使用的 rime-frost（白霜拼音）方案的預設鍵位為例：

- 數字鍵、空格：選候選；`-`、`=`：翻頁
- `↑`、`↓`：換候選；`←`、`→`：移動組字串裡的游標；`[`、`]`：以詞定字；等等……

#### 為什麼這麼設計

這是刻意的取捨，為了在程式碼實作上最乾淨。本插件爭取透過公開 API 相容任意輸入法，且暫時不想針對任何輸入法做單獨適配，程式碼裡沒有「某某輸入法」的特例。這個取捨的弊端就體現在此。

點選候選要插件替你按鍵，但同一串鍵在不同輸入法裡的行為並不一樣：可能換候選，可能翻頁，也可能毫無反應。所以按鍵交回給你，輸入法怎麼解釋就怎麼執行。

頁碼同理，輸入法回報的頁碼不可靠，寧可不顯示也不顯示錯的。

### 為什麼候選列空著也在

候選列不顯示候選時也不消失。鍵盤視窗的高度因此不變，打字時鍵盤不會上下跳。

組字會在每次按鍵後開始或結束。候選列如果隨內容出現和消失，鍵盤就會反覆改變高度，VR 裡很難受。

### 輸入法的名字和中英切換什麼時候出現

鍵盤頁的輸入框獲得焦點時，候選列右側顯示目前輸入法的名字，空格左側出現中英切換鍵。輸入框失去焦點後兩者一起消失，空格恢復原寬。

插件只用遊戲自己那條輸入鏈路上的輸入法，不讀也不改別的視窗。所以在遊戲裡按 Win+Space 不會改變 VR 鍵盤的輸入法。

要換輸入法，目前只能在遊戲啟動前換好。VR 裡能切的只有中英；候選列的下拉框還沒做。

### 為什麼只在鍵盤頁的輸入框裡生效

組字要搶前景、替你按鍵，範圍一放寬就會管到別的視窗，風險比收益大。所以插件只接管鍵盤頁內建輸入框裡的按鍵，遊戲裡的其他輸入框、桌面、別的應用程式都不接管，以後也不打算支援。

### 為什麼組字時傳送鍵是灰的

組字串暫時放在輸入框裡，鍵盤頁見輸入框非空就會讓傳送鍵變成可點，不打一聲招呼就能把拼音發出去。所以插件在組字期間把它壓回停用。完成輸入或取消後恢復正常。

### 為什麼組字時會短暫搶走前景

組字要把按鍵送進輸入法，只能模擬全域按鍵，而模擬按鍵只會送到前景視窗。所以組字開始時，插件把自己的隱藏視窗提到前景，送完再還給你原本的視窗；中途被別的視窗搶走，就再搶一次。表現是遊戲短暫失去焦點，桌面上的焦點會跳一下再回來。要把按鍵送進輸入法沒有第二條路：要嘛自己實作一整套文字存放區，要嘛根本送不進去。

## 建置

需要 .NET SDK 10 或更高版本、CMake，以及 Visual Studio 的 C++ 建置工具。

先取得上游原始碼，再編譯原生函式庫：

```powershell
git submodule update --init
pwsh native/build.ps1
```

然後編譯插件：

```powershell
dotnet build -c Release
```

把插件與原生函式庫一起安裝進遊戲：

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
