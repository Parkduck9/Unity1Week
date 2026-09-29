# Unity 3D 게임 개발 계획서

## 개요
- **장르**: 3D 복셀 캐릭터 아이템 수집 게임
- **목표**: 4x4 타일맵 위에서 아이템 3개를 모두 모으면 클리어, 맵 밖으로 떨어지면 게임 오버
- **환경**: Unity 6 LTS / URP / Input System

## 확정 사항
| 항목 | 결정 |
|---|---|
| 카메라 | 플레이어를 따라가는 카메라 |
| 아이템 | 기본 3개 |
| UI | 아이템 개수, `GAME OVER` / `CLEAR` 문구, 재시작 카운트다운 |
| 입력 | WASD / 방향키 이동, Space 점프 |
| 상태 관리 | GameManager가 Playing / GameOver / Clear 상태 관리 |

## 개발 단계

### 0. Unity 3D 프로젝트 생성
- Unity 6 LTS, URP 템플릿
- Input System 패키지 설정
- 폴더 구조: `Scripts`, `Prefabs`, `Materials`, `Animations`, `Scenes`

### 1. 3D 복셀 캐릭터 생성
- Cube를 조합해 캐릭터 제작
- 파츠 분리: 머리 / 몸 / 왼팔 / 오른팔 / 왼다리 / 오른다리 (애니메이션용)
- 프리팹으로 저장

### 2. 4x4 타일맵 + 이동 / 점프 + 따라가는 카메라
- 1 unit 크기 타일로 4x4 맵 구성, 가운데 대각선 2칸은 구멍 (타일 14개, 초록 체커무늬)
- Rigidbody 기반 캐릭터 이동(WASD / 방향키, 초당 3칸), 점프(Space, 높이 1.2칸)
- 이동 방향으로 캐릭터 회전, 시작 시 카메라 쪽을 바라봄
- 따라가는 카메라: 고정 각도 쿼터뷰로 부드럽게 따라감 (W = 화면 위쪽)

### 3. Idle / Walk / Jump 애니메이션
- Animator Controller 구성: Idle ↔ Walk, Any → Jump
- 파츠 Transform 키프레임으로 애니메이션 제작
  - Walk: 팔·다리 교차 스윙
  - Jump: 팔 올리기, 다리 모으기

### 4. 아이템 추가
- 아이템 3개를 타일 위에 배치
- 회전 + 위아래로 떠다니는 연출
- Trigger Collider로 획득 → 개수 증가
- UI: `아이템 0 / 3`

### 5. 게임 종료 / 성공 판정 (GameManager)
- 상태: `Playing` → `GameOver` 또는 `Clear`
- 실패: 캐릭터 `y < -5` → `GAME OVER` 문구 표시
- 성공: 아이템 3개 모두 획득 → `CLEAR` 문구 표시
- 종료되면 플레이어 입력 막기

### 6. 3초 후 재시작
- 종료 후 카운트다운 표시: `3 → 2 → 1`
- 코루틴으로 기다린 뒤 `SceneManager.LoadScene`으로 현재 씬 다시 불러오기

## 스크립트 구성 (예정)
| 스크립트 | 역할 |
|---|---|
| `PlayerController` | 이동, 점프, 애니메이션 파라미터 전달 |
| `FollowCamera` | 플레이어 따라가기 |
| `Item` | 회전 연출, 획득 처리 |
| `GameManager` | 상태 관리, 승패 판정, 재시작 |
| `UIManager` | 아이템 개수, 결과 문구, 카운트다운 표시 |
