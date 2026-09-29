# Claude Code에서 Codex로 그림 생성하기

이 PC의 동일한 Windows 사용자로 Codex CLI를 실행하면 저장된 ChatGPT 로그인을 재사용한다.
OAuth 토큰을 이 프로젝트에 복사하거나 Claude 프롬프트에 넣을 필요가 없다.

## 실행

프로젝트 루트에서 PowerShell로 실행한다.

```powershell
.\Generate-ImageWithCodex.ps1 -Prompt '투명 배경의 귀여운 복셀 기사 캐릭터를 그려 줘'
```

Claude Code는 위 스크립트에 사용자가 요청한 그림 설명을 전달한다. 프롬프트에 따옴표나 셸 문자가 있으면 PowerShell 문자열로 올바르게 이스케이프한다. 출력은 `GeneratedImages` 폴더에 저장하도록 요청하며, Codex가 보고한 실제 파일의 존재를 확인한 뒤 사용한다. 생성에는 수 분이 걸릴 수 있다.

**실행할 때 주의**
- 호출할 때 `2>&1`로 오류 출력을 합치지 않는다. Codex는 시작 메시지를 stderr로 내보내는데, Windows PowerShell 5.1에서 `2>&1`을 쓰면 이를 오류로 바꿔 스크립트의 `$ErrorActionPreference = 'Stop'` 때문에 바로 중단된다. (2026-09-29 실제로 발생)
- 오래 걸리므로 Claude Code에서는 백그라운드로 실행하고 완료 알림을 기다린다.

## codex 실행 파일 찾기

스크립트가 아래 순서로 `codex`를 찾는다. PATH에 없어도 된다.
1. PATH에 있는 `codex`
2. Codex 데스크톱 앱 설치 위치 `%LOCALAPPDATA%\OpenAI\Codex\bin\<버전 폴더>\codex.exe` 중 가장 최근 파일 (앱이 업데이트되면 버전 폴더 이름이 바뀌므로 이름을 고정하지 않고 검색)

둘 다 없으면 확인한 위치를 알려 주는 오류를 내고 멈춘다. 찾은 경로는 실행할 때 `codex: <경로>`로 출력된다.

## 인증과 실행 환경

- `codex login status`로 로그인 상태를 확인한다. 로그아웃 상태라면 사용자가 `codex login`으로 로그인한다.
- `codex`는 위 "codex 실행 파일 찾기" 순서로 찾는다. WSL이나 다른 OS 사용자에게 Windows 로그인이 자동 공유된다고 가정하지 않는다.
- 인증 캐시는 기본적으로 사용자 프로필의 `.codex`에 있으며 설정에 따라 OS 자격 증명 저장소를 사용할 수 있다. 내용을 읽거나 프로젝트로 복사하지 않는다.
- 이는 별도 Codex CLI 실행이다. 현재 열린 Codex 채팅에 메시지를 보내는 방식은 아니다.
- 내장 이미지 도구가 해당 CLI 세션에서 제공되지 않으면 실패 내용을 보고한다. API 키 방식으로 임의 전환하지 않는다.

## 확인 범위

2026-09-29: 이 PC에서 `codex login status`의 ChatGPT 로그인과 `image_generation` 기능 활성화, CLI 옵션을 확인했다.

2026-09-29: Claude Code에서 스크립트로 **실제 이미지 생성까지 확인**했다.
- Codex CLI `0.158.0-alpha.2.1` (데스크톱 앱에 포함, PATH에는 없음), ChatGPT 로그인의 내장 이미지 기능 사용 (유료 API 키 사용 안 함)
- 결과: `GeneratedImages/hanbok_voxel_girl_8260e2d4ffaa4ab786ba088f6f492bd5.png` (1.4 MB), 약 1분 20초
- 같은 날 스크립트에 codex 경로 자동 찾기를 추가하고, PATH에 없는 상태에서 앱 설치 위치를 찾는 것을 확인했다.

공식 문서:
- https://learn.chatgpt.com/docs/non-interactive-mode
- https://learn.chatgpt.com/docs/auth
