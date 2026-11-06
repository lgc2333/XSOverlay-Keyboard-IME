# XSOverlay Keyboard IME

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | **한국어** | [English](README.en.md)

> [!NOTE]
> 이 문서는 LLM이 번역했습니다. 오역이 있거나 원문과 어조가 많이 다를 수 있습니다.

> [!WARNING]
> 이 저장소는 아직 개발 단계입니다. 배포할 수 있는 빌드는 없고, 구현 방식도 계속 바뀌고 있습니다. 조금만 기다려 주세요.

XSOverlay의 VR 키보드에 시스템 IME를 붙입니다. 키보드 페이지의 입력란에서 조합 중인 문자열을 입력하고, 그 문자열과 후보를 키보드 위에 표시합니다. 후보는 키보드의 키로 고르고, 확정한 텍스트를 입력란에 다시 써 넣습니다.

## 이 Mod를 만든 이유

VRChat에서의 중국어 입력에는, 시도한 두 가지 방법이 모두 충분하지 않았습니다.

- [OVR Input Method Bridge](https://store.steampowered.com/app/3448100/OVR_Input_Method_Bridge/)는 지원하는 IME의 종류가 적고 UI도 불편해서, 겨우 쓸 만한 수준이었습니다.
- [Tapi](https://booth.pm/zh-cn/items/7047187)는 Rime 기반의 자체 입력 방식을 내장하고 있고, 시스템 IME와 연동되지 않습니다. 평소에 키워 온 사전을 그대로 가져다 쓸 수 없습니다.

또한 [OVR Toolkit](https://store.steampowered.com/app/1068820/OVR_Toolkit/)은 가상 키보드를 내장하고 있지만, 성능 문제가 알려져 있어 검토 대상에서 제외했습니다. 중국어 입력을 지원하는지, 커뮤니티에 기존 방법이 있는지는 저도 검증하지 않았습니다.

마침 XSOverlay 키보드가 최근 VRC ChatBox 기능을 공식 지원한다는 것을 알게 되었습니다. 그래서 시스템 IME를 연결해 키워 온 사전을 그대로 쓰자는 생각이 들었고, 이 Mod가 여기서 시작되었습니다.

## Showcase

준비 중

## 실행 요구 사항

- Windows 10 이상, 64비트.
- XSOverlay Beta 695. 다른 버전은 검증하지 않았습니다.
- BepInEx 5.4.23.6, x64 버전. 다른 버전은 검증하지 않았습니다.
- TSF를 구현한 IME만 지원합니다. 이 저장소는 小狼毫(Rime Windows 버전)로 검증했습니다.
- IME에서 인라인 프리 에디트(inline preedit)를 켜야 할 수도 있습니다. 끄면 후보만 보이고 입력 중인 코드는 보이지 않습니다. 검증해 보니 小狼毫에서는 필수였습니다. 小狼毫에서는 `weasel.custom.yaml`에 `style/inline_preedit: true`를 설정합니다.

## 설치

준비 중

## 로드맵

다음 단계는 VRC ChatBox 사용성 개선입니다. 현재 버전은 키보드 페이지에 후보 표시줄을 띄우는 데까지입니다.

- ChatBox 버튼을 켜면 조합 중인 문자열과 후보를 실시간으로 ChatBox에 보내고, 입력하는 동안 함께 표시합니다.
- 입력란에 포커스가 들어오면 VRChat에 입력 중 상태를 알리고, 포커스가 빠지면 취소합니다.

## 빌드

.NET SDK 10 이상, CMake, Visual Studio의 C++ 빌드 도구가 필요합니다.

먼저 업스트림 소스를 받고 네이티브 라이브러리를 빌드합니다.

```powershell
git submodule update --init
pwsh native/build.ps1
```

그다음 플러그인을 빌드합니다.

```powershell
dotnet build -c Release
```

플러그인과 네이티브 라이브러리를 게임에 함께 넣습니다.

```powershell
dotnet build -c Release -p:DeployToGame=true
```

저장소에 설정된 게임 경로는 `D:\SteamLibrary\steamapps\common\XSOverlay_Beta`입니다. 게임이 다른 곳에 있으면 `GameDir` 속성으로 경로를 지정합니다.

```powershell
dotnet build -c Release -p:DeployToGame=true -p:GameDir="D:\Steam\steamapps\common\XSOverlay_Beta"
```

## 감사의 말

- [Windmill-City/IngameIME-Common](https://github.com/Windmill-City/IngameIME-Common), [Windmill-City/IngameIME_Win32](https://github.com/Windmill-City/IngameIME_Win32): TSF 입력 컨텍스트와 조합 콜백. 이 의존성은 이 저장소의 C 심과 함께 `IngameIME.dll`로 빌드되며, 전체를 LGPL 2.1로 배포합니다. [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)를 참고하세요.
- DeepSeek V4.1 Flash & DeepSeek Harness: 이 프로젝트는 전부 大肥魚妈妈가 만들었습니다.

## 후원

**[후원하기](https://lgck.cc/sponsor)**

후원해 주시는 모든 분께 감사드립니다! 여러분의 후원이 계속 만들 힘이 됩니다!

## 연락처

- QQ: 3076823485
- QQ 그룹: [168603371](https://qm.qq.com/q/EikuZ5sP4G)
- Telegram: [@lgc2333](https://t.me/lgc2333)
- 이메일: <lgc2333@126.com>
