# XSOverlay Keyboard IME

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | **日本語** | [한국어](README.ko.md) | [English](README.en.md)

> [!NOTE]
> この文書は LLM による翻訳です。誤訳や、原文とニュアンスが異なる表現が含まれている可能性があります。

> [!WARNING]
> このリポジトリは開発中です。配布できるビルドはまだなく、実装方法も流動的です。もうしばらくお待ちください。

XSOverlay の VR キーボードにシステムの IME を組み込みます。キーボードページの入力欄で未確定の文字列を入力し、その文字列と候補をキーボード上に表示します。候補はキーボードのキーで選び、確定したテキストを入力欄へ書き戻します。

## この Mod を作った理由

VRChat での中国語入力には、試した 2 つの方法がいずれも不十分でした。

- [OVR Input Method Bridge](https://store.steampowered.com/app/3448100/OVR_Input_Method_Bridge/) は対応する IME の種類が少なく、UI も使いにくいため、ぎりぎり使える程度でした。
- [Tapi](https://booth.pm/zh-cn/items/7047187) は Rime をベースにした独自の入力方式を内蔵し、システムの IME とは連携しません。普段から育ててきた辞書をそのまま使うことはできません。

また [OVR Toolkit](https://store.steampowered.com/app/1068820/OVR_Toolkit/) は仮想キーボードを内蔵していますが、パフォーマンスの問題が知られているため検討対象から外しました。中国語入力を扱えるか、コミュニティに既存の方法があるかは、私も検証していません。

ちょうどその頃、XSOverlay のキーボードが公式に VRC ChatBox 機能をサポートしたことに気づきました。そこでシステムの IME を接続し、育てた辞書をそのまま使えるようにする、というアイデアが生まれました。この Mod はそこから始まっています。

## Showcase

準備中

## 動作環境

- Windows 10 以降、64 ビット。
- XSOverlay Beta 695。ほかのバージョンは未検証です。
- BepInEx 5.4.23.6、x64 版。ほかのバージョンは未検証です。
- TSF を実装した IME のみ対応します。本リポジトリでは 小狼毫（Rime Windows 版）で検証しています。
- IME でインライン入力（inline preedit）を有効にする必要がある場合があります。無効だと候補しか表示されず、入力中のコードは見えません。検証では小狼毫で必須でした。小狼毫では `weasel.custom.yaml` に `style/inline_preedit: true` を設定します。

## インストール

準備中

## ロードマップ

次は VRC ChatBox の使い勝手を改善します。現在のバージョンは、キーボードページに候補バーを表示するところまでです。

- ChatBox ボタンを有効にすると、未確定の文字列と候補をリアルタイムで ChatBox に送り、入力中も同期して表示します。
- 入力欄にフォーカスが入ると VRChat に「入力中」を通知し、フォーカスが外れると解除します。

## ビルド

.NET SDK 10 以降、CMake、Visual Studio の C++ ビルドツールが必要です。

先に上流のソースを取得し、ネイティブライブラリをビルドします：

```powershell
git submodule update --init
pwsh native/build.ps1
```

次にプラグインをビルドします：

```powershell
dotnet build -c Release
```

プラグインとネイティブライブラリをゲームに入れます：

```powershell
dotnet build -c Release -p:DeployToGame=true
```

リポジトリで設定済みのゲームのパスは `D:\SteamLibrary\steamapps\common\XSOverlay_Beta` です。ゲームが別の場所にある場合は、`GameDir` プロパティでパスを指定します：

```powershell
dotnet build -c Release -p:DeployToGame=true -p:GameDir="D:\Steam\steamapps\common\XSOverlay_Beta"
```

## 謝辞

- [Windmill-City/IngameIME-Common](https://github.com/Windmill-City/IngameIME-Common), [Windmill-City/IngameIME_Win32](https://github.com/Windmill-City/IngameIME_Win32)：TSF の入力コンテキストとコンポジションのコールバック。この依存は本リポジトリの C シムと一緒に `IngameIME.dll` へ組み込まれ、全体を LGPL 2.1 で配布します。[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を参照してください。
- DeepSeek V4.1 Flash & DeepSeek Harness：本プロジェクトはすべて大肥魚妈妈が構築しました。

## スポンサー

**[スポンサーになる](https://lgck.cc/sponsor)**

支援してくださる皆様に感謝します！皆様の支援が、創作を続ける力になります！

## 連絡先

- QQ：3076823485
- QQ グループ：[168603371](https://qm.qq.com/q/EikuZ5sP4G)
- Telegram：[@lgc2333](https://t.me/lgc2333)
- メール：<lgc2333@126.com>
