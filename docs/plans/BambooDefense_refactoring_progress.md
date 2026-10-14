# BambooDefense 리팩토링 진행 기록

## 재개할 때 먼저 읽기

- 최신 요청: 2026-10-14 사용자가 **리팩토링을 이어서 S4까지** 진행하도록 요청했다. **이번 실행은 S4에서 종료하며 S5~S7은 아직 실행하지 않았다.**
- 계획: [BambooDefense 전환 리팩토링 계획](BambooDefense_전환_리팩토링_계획.md)
- 현재 결과: **S3 수명/취소 Play 검증 완료, S4 HUD 프리팹 이전·컴파일·Play·화면 검증 완료. 다음 구현은 S5.**
- S1·S2 값 이전과 S4 HUD 이전을 다시 수행할 필요가 없다. 미확인은 아래 항목으로 별도 유지한다.
- 마지막 Editor 상태: BambooDefense, **Play=false, paused=false, compiling=false, dirty=false**. 현재 저장된 씬에 HUD 인스턴스와 참조가 있다.
- 이전 인계의 dirty=true 상태는 이번 재개 시 유지되지 않았다. 새 Editor PID 33420에서 같은 씬이 dirty=false인 것을 먼저 확인한 뒤 현재 디스크 상태를 BeforeS4에 복사했다. 이전 미저장 내용의 보존 여부는 소급 확인할 수 없다.
- 프로젝트 내부 수정 및 계획의 권장안은 이미 승인되었다. 기존 작업·연결은 보존한다.
- 현재 브랜치: `refactoring`(.git/HEAD 직접 읽기). AGENTS.md에 따라 **Git CLI·커밋·푸시·브랜치 변경 없음**.
- 테스트 코드 작성 금지. 이번 검증은 일시적인 Unity CLI eval·Play/API 조작·화면 확인으로 수행했으며 테스트 파일을 추가하지 않았다.
- 구현·에셋 이전·실행 검증은 사용자의 명시적 요청 범위다. UNITY_REFACTOR_CONTEXT.md의 최초 조사 전용 제한보다 이후 요청을 따른다.

## 단계 현황

| 단계 | 상태 | 변경 / 완료 근거 | 남은 확인 또는 다음 조건 |
| --- | --- | --- | --- |
| S0 기준선 | 기본 실행 기준 확보 | 375개 사본/해시, Play 스폰·피격·게임오버·Restart API | 실제 입력·전체 전투 경계는 최종 검증에 유지 |
| S1 Inspector/생성기 | 완료 | 기존 씬 생성 차단, 경계 기본값 23.6. 생성기 호출 전후 씬 bytes 동일 | 기존 씬의 조정값 보존 |
| S2 Director 책임 분리 | 구현·핵심 검증 완료, 자연 재사용 관찰 보류 | 스폰 값/참조 22/22 보존. 투척수 재사용 및 동일 근접 인스턴스 제어 재사용/HP30/돌진형 확인 | 특정 근접 인스턴스의 자연 재사용 미관찰 원인 미확정 |
| S3 수명/취소/재사용 | **완료** | 예약 취소·재활성화·실제 사망·Released 1회·재도전 초기화·이전 구독 0 확인 | 실제 하드웨어 전투 입력은 별도 |
| S4 HUD 에셋 이전 | **완료** | 두 Canvas와 12개 마커를 프리팹으로 이전. 전조 길이 단일 입력. 화면비 3종·편집 프리뷰·재도전 확인 | 기존 외형과 게임 판정 보존 |
| S5 공유 전투 정리 | 미착수 | 이번 요청 범위 밖 | 다음 재개 시 작은 변경부터 시작 |
| S6 정식 진입/빌드 | 미착수 | Intro·빌드 목록 유지 | 공유 변경 검증 후 전환 및 Windows 빌드 |
| S7 에셋/문서 정리 | 미착수 | 게임 에셋 삭제 없음 | 정식 빌드 확인 후 후보별 근거 확보 |

## 확정한 제품 범위

- 정식 목표: **Intro → BambooDefense → 게임오버 → 현재 씬 재도전**. S6에서 진입을 바꾼다.
- 기존 Build 씬·보스·공유 에셋은 새 빌드 검증까지 보존한다. 참조/제작 도구 확인 없이 일괄 삭제하지 않는다.
- 기록은 현재 세션 화면 표시다. 최고 기록·세이브·메뉴 복귀 기능은 추가하지 않는다.
- Windows Standalone 64비트가 빌드 목표다. 키보드·마우스·패드 연결을 보존하며 실제 입력과 API 검증을 구분한다.
- 밸런스·피해·패링·회피·연계·시간 기준을 유지한다. 버그 수정이나 성능 최적화는 별도 근거를 기록한다.

## 현재 책임과 Inspector 위치

상세 수치와 연결은 [대숲 방어 명세](../features/대숲_방어.md)의 현재 상태/Inspector 표를 따른다.

| 소유자 | 책임 / 편집 위치 | 종료 처리 |
| --- | --- | --- |
| BambooDefenseDirector | 준비·생존 시간·처치 수, 투척 제한과 표시 사건 연결 | 사망 구독 해제, ThrowBudget.Clear |
| DefenseEnemySpawner | 스폰 수/증가량·반경·프리팹·UnitRoot, 활성/대기 적 및 사망 구독 | 생성한 적의 사망 구독 해제 |
| DefenseThrowBudget | 예약·비행 집계 및 수리검 Released 구독 | 예약·비행·다음 허가 시각 초기화, 구독 해제 |
| BambooThrower | 대기·전조·발사·이동 순환. 전조 길이는 Thrower 프리팹에서 한 번 입력 | OnDisable/재시작에서 코루틴·예약·자신의 비행 구독 정리 |
| DefenseShuriken | 실제 비행·피해·검기 및 1회 Released | 투척수 사망 후에도 기존 비행 수명 유지 |
| DefenseRecordHud | 직렬화된 Record 텍스트에 세션 기록 표시 | 게임 상태를 변경하지 않음 |
| ShurikenDirectionIndicator | 마커 슬롯의 전조/비행/페이드 상태, 거리별 표현 | 비활성화 시 Director·수리검 구독 해제, 마커 숨김 |
| BambooDefenseHud.prefab | 두 Canvas·기록 외형·마커 형태와 배치 | 런타임 UI 생성 경로 없음 |

- HUD 경로: `Assets/Scenes/Defense/UI/BambooDefenseHud.prefab`.
- 기록: DefenseRecordCanvas/Record의 TMP·RectTransform에서 글꼴·34 크기·색·상단 28을 편집한다.
- 방향: ShurikenDirectionCanvas/Marker_0~11을 다중 선택해 반경 230·아이콘 16·호 두께 4·각도 18·색을 함께 편집한다.
- 두 Canvas는 Overlay, sorting order 3, 기준 1920×1080, match 0.5를 유지한다.
- 깜빡임·페이드·거리/크기 배율은 씬의 ShurikenDirectionIndicator에서 편집한다. 전조 길이 중복 필드는 없다.
- Graphics 배열 길이가 표시 상한이다. 프리팹 마커 수를 바꾸면 씬 참조 배열도 연결한다. 모두 사용 중이면 추가 표시를 생략한다.
- 편집 모드에서 `준비 2`와 Marker_0을 미리 볼 수 있다. Play의 Awake는 모든 마커를 먼저 숨긴다. 0×0 마커 RectTransform은 중심 밖에 직접 메시를 그리는 기존 방식이다.
- `ThrowTelegraphed(position, duration)`으로 실제 전조 길이를 전달한다. 전조 취소 사건이나 공격 식별자는 추가하지 않았으며 기존 시간 만료 정책을 유지한다.
- GameOver는 새 투척 예약을 거절하고 기록을 유지한다. 기존 예약·비행은 씬 종료에서 정리되며 재도전은 새 세션이다.

## 이번 S3 검증 결과

일시정지된 Play에서 기존 API와 실제 EnemyHealth.TakeDamage 경로를 사용했다. 최소 허가 간격·근접 전조 우선 여부 등 진단용 변경은 Play에서만 적용했고 저장하지 않았다.

- 준비 중 / Director 비활성 / GameOver에서 새 예약 거절: 모두 true.
- 보유 예약 상태의 비활성화, 반복 비활성화, 활성 투척수 Activate 재시작 뒤 예약 수: 모두 0.
- 발사 뒤 투척수 비활성화: 수리검이 살아 있고 비행 수 1 유지.
- Release를 연속 호출: Released 1회, 비행 수 0.
- 실제 투척수 사망: 전조 예약 반환 0, 중복 피해를 줘도 처치 1회. 두 적 사망 후 처치 2.
- 발사 후 실제 투척수 사망: 기존 수리검 유지.
- GameOver 뒤 `생존 00:01    처치 2` 기록 보존.
- 예약 1 + 비행 1이 있는 상태에서 Restart API 실행 후 다음 프레임의 새 씬을 확인했다. **이전 예약 0·비행 0·적 사망 구독 0·수리검 종료 구독 0**.
- 새 Director 인스턴스, 처치 0·생존 시간 0·준비 표시·TimeScale 1·Playing 확인.
- Restart는 씬 로드 완료를 다음 조회에서 확인해야 한다. 호출 직후 같은 eval의 결과만으로 초기화를 판단하지 않는다.

## 이번 S4 이전과 검증 결과

- 기존 저장 수치/폰트/색을 읽고 live Editor API로 HUD 프리팹을 생성했다. 씬·프리팹 YAML을 직접 편집하지 않았다.
- 기존 DefenseRecordHud·ShurikenDirectionIndicator 컴포넌트와 .meta GUID를 유지했다. 기록의 외형 필드와 방향 UI의 형태 필드는 프리팹으로 옮겼다.
- 두 표시 코드의 GameObject/Canvas 런타임 생성 경로를 제거하고 직렬화된 텍스트·마커 참조를 사용한다.
- BambooDefenseHudAuthoring.Connect를 기존 씬 이전과 신규 씬 생성기가 함께 사용한다. 프리팹 에셋 자체가 기본 배치의 원본이다.
- 확인한 보존값: Canvas 2, Overlay/order 3, 1920×1080/match 0.5, 기존 폰트/34/회색 0.85, 상단 28/900×60, 마커 12/반경 230/아이콘 16/두께 4/호 18/기존 빨강/raycast false.
- 1.25초 전조 전달: 0.8초 시점 Telegraph, 1.26초 시점 Hidden. UI의 별도 전조 길이 필드 없음.
- 앞/오른쪽/뒤/왼쪽 회전: 0/270/180/90도. 거리 4/30에서 크기 배율 3/0.8, 비행 알파 1/0.6.
- 전조 13건 요청 시 슬롯/그래픽 12개 유지, 13번째 표시 생략. Released 이후 Fade, 0.31초 뒤 Hidden.
- 표시 담당자 재활성화 3회 뒤 Director 전조 구독 1개, 사건 1건에 활성 슬롯 1개.
- 실제 Game View를 **1280×720, 1024×768, 1680×720**로 변경하고 CanvasScaler 갱신 후 화면을 확인했다. 기록과 네 방향 표시가 화면 안에 보인다.
- 편집 모드 화면에서 준비 텍스트와 Marker_0 프리뷰를 확인했다. 검증용 Game View 크기는 원래 선택 index 0과 custom size 0개로 복구했다.
- 자연 게임오버의 상단 기록과 기존 재도전 UI를 확인했다. 재도전 후 Canvas 2개·마커 12개 유지, 기록 초기화 확인.
- MigrateHud 재실행과 기존 씬 Builder.Build 호출 전후 **씬 bytes 동일**, HUD 인스턴스 ID 동일, HUD 인스턴스 1개·dirty=false.
- 별도 PreviewScene의 신규 Director에 Connect를 두 번 실행: 같은 인스턴스, Canvas 2·Record 참조·마커 참조 12개. 실제 BambooDefense는 dirty=false 유지.
- 최종 HUD의 Missing Script/Reference 0, Director/Spawner 각각 1개. BeforeS4와 비교해 Spawner의 저장 블록이 완전히 동일함을 확인했다.
- 두 표시 스크립트 GUID 참조를 Assets의 모든 씬/프리팹에서 검색한 결과 BambooDefense만 사용했다.
- 이 단계에서 게임 에셋 삭제·공유 Player/애니메이션 수정·Intro/빌드 목록 변경은 하지 않았다.

## 변경 파일

이전 S1~S3 변경도 작업 트리에 남아 있으므로 함께 인계한다.

- `Assets/Editor/BambooDefenseSceneBuilder.cs`: 기존 씬 재생성 차단, Spawner 및 HUD 프리팹 연결.
- `Assets/Editor/BambooDefenseRefactoring.cs`: 스폰 값 이전과 저장된 씬의 HUD 연결 이전 메뉴.
- `Assets/Editor/BambooDefenseHudAuthoring.cs` + .meta: S4 신규 HUD 배치·참조 연결 담당.
- `Assets/Scripts/Defense/DefenseArenaBoundary.cs`: S1 신규 기본 경계 23.6 및 단위.
- `Assets/Scripts/Defense/BambooDefenseDirector.cs`: 세션 상태와 연결, Playing 예약 제한, 실제 전조 길이 알림.
- `Assets/Scripts/Defense/DefenseEnemySpawner.cs` + .meta: S2 스폰/배치/적 재사용 및 사망 알림.
- `Assets/Scripts/Defense/DefenseThrowBudget.cs` + .meta: S2 예약·비행 및 종료 구독.
- `Assets/Scripts/Defense/BambooThrower.cs`: S3 공통 취소 경로, S4 전조 길이 전달.
- `Assets/Scripts/Defense/DefenseRecordHud.cs`, `ShurikenDirectionIndicator.cs`: S4 생성 UI 제거, 직렬화된 표시 참조 사용.
- `Assets/Scenes/Defense/BambooDefense.unity`: S2 Spawner 값 이전 및 S4 HUD 프리팹 인스턴스/참조.
- `Assets/Scenes/Defense/UI.meta`, `UI/BambooDefenseHud.prefab` + .meta: S4 신규 외형 에셋.
- 계획·이 진행 문서·`docs/features/대숲_방어.md`: 현재 책임, Inspector 위치, 검증과 재개 절차.

주요 신규 GUID: Spawner `14c4bf4e5ad8c964d96be4f5092aad87`, ThrowBudget `1328648fdae09d84a9ab6b9f2ce35b81`, 이전 도구 `250d2ce70656c624d937b643e37213ce`, HUD 연결 도구 `0a74a96c2a72f90498807f0f5f567052`, HUD 프리팹 `4fa9eb7340da5624ca64e8c9f59e836b`, UI 폴더 `05a42fdc43100e64b8632f6817a29648`. 기존 씬·표시 컴포넌트 GUID도 보존했다.

## 기준선·복구·실행 근거

공통 경로: `UserSettings/Refactoring/BambooDefense-20261014/`. UserSettings는 Git 무시 대상이므로 다른 컴퓨터에서 재개하면 이 폴더도 전달한다.

- 최초 기준 커밋: `a7b646ca3ded556de516c0fd3f4466ee96490782`(refs 직접 읽기). 미커밋 원본은 Baseline 사본이 기준이다.
- S4 직전의 코드·.meta·씬 29개를 BeforeS4에 복사했다. 이 폴더는 문서 사본을 포함하지 않는다.
- S4를 되돌릴 때는 BeforeS4의 관련 소스와 씬을 함께 복원하고 신규 HUD 인스턴스/에셋·연결 도구를 정리한다. 진행/명세의 S4 기록도 함께 갱신한다. 사용자 변경을 전체 리셋하지 않는다.

| 파일 / 경로 | 내용 |
| --- | --- |
| Baseline/ | 최초 원본 375개, 6,445,991 bytes |
| baseline-manifest.json | 최초 원본 경로·크기·SHA256 |
| baseline-play.json / baseline-gameover.json | 최초 준비·게임오버 실행 기준 |
| s2-field-comparison.json | 이전한 스폰 값/참조 22/22 일치 |
| s2-play.json | 생존 70.484초, 투척수 6·근접 20, 처치 0→2 |
| s2-reuse.json | 자연 관찰: 투척수 재사용 true, 특정 근접 재사용 false, 예약 1+비행 2 |
| s2-controlled-reuse.json | 동일 근접 인스턴스 재사용 true·HP30·돌진형·active1/idle0 |
| BeforeS4/ | 이번 재개 시 S4 이전 사본 29개 |
| s3-lifetime.json | 예약 권한·취소·재활성화·Release 1회 |
| s3-death-unload-retry.json | 실제 사망·기록·비행 보존·씬 종료 구독/예산 정리·재도전 |
| s4-hud-checks.json | 저장값·직렬화 참조·Play 기록·방향·거리·풀·페이드·구독 결과 |
| s4-authoring-checks.json | 이전 재실행/Builder no-op, 신규 Director 연결, 편집 모드 상태 |
| s4-aspect-ratios.json | 실제 Screen 크기·Canvas 배율과 Game View 복구 결과 |
| s4-hud-16x9.png / s4-hud-4x3.png / s4-hud-21x9.png | 화면비별 실제 HUD 캡처 |
| s4-editor-preview.png / s4-gameover.png | 편집 프리뷰 및 게임오버 기록 화면 |
| s4-file-manifest.json | S4 관련 코드·씬·프리팹 경로/크기/SHA256 |
| s4-final-state.json | 최종 clean 씬·참조/구성 수·Spawner 저장값 보존·Console 상태 |

## 미확인과 제한

- 실제 키보드·마우스·패드 조작으로 하는 패링·회피·연계의 전체 경계 검증은 미실행이다. API 검증을 실제 입력 검증으로 표기하지 않는다.
- **S2 자연 근접 재사용 추가 관찰**은 남아 있다. 생존 163.646초에 특정 근접 인스턴스의 자연 재사용을 보지 못했지만, 도달 가능한 위치에서 제어한 재사용은 동일 인스턴스/HP30/돌진형으로 성공했다. 원인은 미확정이며 스폰 산식을 임의로 바꾸지 않았다.
- 자연 재사용을 조사할 때 active/idle, next spawn 시각, 플레이어 위치·생존, NavMesh 샘플/완전 경로 조건을 함께 수집한다. Baseline 원본과 비교한다.
- 정식 Windows 빌드는 아직 수행하지 않았다. BambooDefense는 Build Settings에서 여전히 비활성이고 Intro는 기존 진입이다.
- 과거 이력의 중복/오래된 설명 및 FEATURE_SPEC의 발자국 시스템 문서 링크 정리는 S7에서 처리한다.
- 제품 질문은 이미 권장안으로 확정됐다. 새 작업 충돌이 생기면 해당 근거를 따로 기록한다.

## 다른 터미널에서 재개하는 순서

1. 이 문서, AGENTS.md, FEATURE_SPEC.md, 대숲 방어 명세와 계획의 S5~S7을 읽는다. 다음 단계 요청을 확인한 뒤 진행한다. 이번 요청은 S4에서 끝났다.
2. .git/HEAD를 직접 읽고 브랜치 규칙을 따른다. 현재 refactoring에서는 Git CLI를 사용하지 않는다.
3. Unity 연결과 active scene/dirty/Play를 읽기 전용으로 확인한다. 마지막 상태는 clean이지만 이후 사용자 편집이 있으면 먼저 보존한다. 씬을 재생성하지 않는다.
4. S5 전에 PlayerActionDriver·PlayerController·PlayerMotor·KatanaCombatCoordinator·AnimationEventRelay 및 Player 프리팹/클립 사용처를 읽는다. 우선 공격 종류별 시간 선택 중복을 작은 변경으로 정리한다. 범용 FSM/DI나 PlayerMotor 전체 재작성은 하지 않는다.
5. 공격/연계/패링/회피/사망/재도전 경계를 기존 값과 비교하고 관련 명세를 갱신한다. 공유 변경은 유지 대상 Stage/Boss에서도 확인한다. .meta, ActionId, Animation Event/UnityEvent 이름과 직렬화 참조를 보존한다.
6. S6에서 실제 Intro 시작 수신자와 Build Settings/Profile을 조사해 정식 진입을 연결하고 Windows 빌드 흐름을 검증한다.
7. S7은 빌드 검증 뒤 참조/문자열/Resources/제작 도구 근거가 있는 후보만 정리한다. 각 작은 작업 직후 이 문서를 갱신한다.

### PowerShell 연결

이전 터미널 8538은 닫혔고 그 ID에 의존하지 않는다. CLI 1.0.0-beta.5, Editor 6000.3.22f1, Pipeline 0.8.0-exp.1, 마지막 PID 33420/port 7800이었다. 연결 정보는 매번 다시 조회한다.

```powershell
Set-Location 'C:/Users/JUNGLE/Documents/w07-blindblade-plus'
$unityCli = 'C:/Program Files/Unity Hub/resources/cli/unity.exe'
& $unityCli status --format json
& $unityCli command console_status --caller plugin --skill unity-cli --project-path 'C:/Users/JUNGLE/Documents/w07-blindblade-plus' --format json
& $unityCli command eval --caller plugin --skill unity-cli --project-path 'C:/Users/JUNGLE/Documents/w07-blindblade-plus' --format json 'return new { playing=UnityEditor.EditorApplication.isPlaying, compiling=UnityEditor.EditorApplication.isCompiling, scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, dirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty };'
```

- Unity command에는 caller/skill/project-path를 명시한다. UI 작업은 ui-ugui 스킬을 따른다.
- PowerShell의 native argument 전달에서 C# 문자열의 큰따옴표가 제거될 수 있다. 짧은 eval을 쓰거나 큰따옴표를 native 인자용으로 이스케이프한다.
- capture_game_view의 save_path는 이 버전에서 Assets 아래로 정규화된다. 검증 캡처를 게임 에셋으로 남기지 않는다. 이번 캡처는 UserSettings의 로컬 근거로 정리했다.
- rg는 PATH에서 찾지 못했으므로 Get-ChildItem/Select-String을 사용했다.
- 과거 제한 환경에서는 status가 STATUS_NO_INSTANCES를 반환했지만 이번 환경에서는 직접 연결됐다. 연결 실패만으로 Editor를 새로 띄우거나 씬 파일을 직접 재생성하지 않는다.
- 설치된 shell은 --no-pager를 지원하지 않는다. CLI 업그레이드를 수행하지 않았다.

## 마지막 검증과 인계 문장

- 2026-10-14 04:58:34 UTC Console ground truth: **compilationFailed=false, compiling=false, consoleErrors=0, consoleWarnings=1**.
- 현재 Console의 경고는 기존 씬 재생성을 차단하는 Builder 검증에서 의도적으로 발생했다. Pipeline 누적 기록에는 기존 Input Manager 사용 중단 예정 안내도 남아 있다. 콘솔을 지우지 않았다.
- 현재 씬은 저장되어 있으며 Play/일시정지/컴파일 모두 꺼져 있다. 게임 에셋 삭제·빌드·Git CLI는 수행하지 않았다.

> `docs/plans/BambooDefense_refactoring_progress.md`를 먼저 읽고 S5부터 이어서 작업해 주세요. S3 수명 검증과 S4 HUD 프리팹 이전·화면/재도전 검증은 끝났고 씬은 저장된 상태입니다. S1/S2 스폰 값이나 S4 HUD를 다시 이전하지 마세요. S2 자연 근접 재사용 관찰과 실제 입력/빌드 검증은 문서의 미확인 항목을 유지해 주세요. 프로젝트 내부 수정과 권장안은 승인되어 있으며 현재 refactoring 브랜치에서는 Git CLI·커밋을 하지 않습니다.
