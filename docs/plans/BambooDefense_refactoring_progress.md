# BambooDefense 리팩토링 진행 기록

## 재개할 때 먼저 읽기

- 최신 요청: 2026-10-14 사용자가 **S7**을 진행하도록 요청했고, 정리 범위로 문서·작은 잔여물, 근거 있는 레거시 코드, 기존 모드 씬 14개, 서드파티 미사용 에셋을 모두 승인했다.
- 계획: [BambooDefense 전환 리팩토링 계획](BambooDefense_전환_리팩토링_계획.md)
- 현재 결과: **S0~S7 계획 단계 모두 완료.** S7 뒤 빌드 의존 에셋 745개 변화 없음, 컴파일 오류·경고 0, Editor 흐름과 Windows 릴리스 빌드 실행 확인. 남은 일은 아래 미확인 항목(실제 입력·화면·사운드 확인 등)과 사용자의 커밋이다.
- S6 결과: Intro(빌드 0) 시작 → BambooDefense(빌드 1) → 게임오버 → 재도전. Windows 64비트 릴리스 빌드 성공·실행 확인, 개발 빌드에서 흐름 API 확인.
- S5 결과: 컴파일 오류 0, API 시나리오 전후 동일, Stage_1·BossBattle 점검, 실제 방어 루프 20초 오류 0.
- S1·S2 값 이전, S4 HUD 이전, S5 시간 정리를 다시 수행할 필요가 없다. 미확인은 아래 항목으로 별도 유지한다.
- S5 시작 시 Editor 상태: PID 61860/port 7800, BambooDefense, **Play=false, paused=false, compiling=false, dirty=false**, Console 오류 0. HEAD는 `refs/heads/refactoring` = `fcf3caaa3374389da629137fa32cb64a8c9c73ad`(S1~S4 커밋 포함, 파일 직접 읽기). 단, 이 Editor는 체크아웃 전 상태를 들고 있었다(아래 `S5 중 확인한 Editor 상태 문제`).
- S5 종료 시 Editor 상태: 같은 PID, BambooDefense를 디스크에서 다시 연 상태, **Play=false, compiling=false, dirty=false**, Missing Script 0, `PlayerSettings.runInBackground=false`, `Time.captureFramerate=0`, ProjectSettings.asset 해시 변화 없음.
- 작업 트리의 기존 미커밋 변경은 `ProjectSettings/Packages/com.unity.probuilder/Settings.json` 하나이며 S5와 무관하므로 건드리지 않는다.
- **이 컴퓨터에는 이전 근거 폴더(Baseline, BeforeS4, s2/s3/s4 json·png)가 없다.** UserSettings는 Git에서 제외되므로 전달되지 않은 것으로 본다. S5 직전 사본은 새로 만든 `BeforeS5/`를 기준으로 한다.
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
| S5 공유 전투 정리 | **완료** | Driver 시간 묶음 단일화, Driver 런타임 폴더 이동(GUID 유지), Coordinator 연계 예약 설정·취소 단일화, 행동 원본·애니메이션 경계 문서화. API 시나리오 16종 전후 비교 | 실제 마우스·패드 전투 입력, 회복 중 예약 분기(현재 값으로 도달 불가), 돌진 이동 프레임 비결정성 원인 |
| S6 정식 진입/빌드 | **완료** | IntroMenu 담당 1개로 정리·BambooDefense 연결, 빌드 목록 Intro·BambooDefense만 활성. 릴리스 빌드 성공(오류 0), 개발 빌드에서 시작·게임오버·재도전 2회 | 실제 마우스·패드 메뉴 조작, 릴리스 빌드의 화면·사운드 육안 확인 |
| S7 에셋/문서 정리 | **완료** | 문서 정리, 잔여 에셋 11·레거시 코드 2곳·고아 스크립트 15개, 기존 모드 씬 14개와 전용 에셋, 서드파티 미사용 1,831개(329MB) 정리. 모두 BeforeS7 사본 + OS 휴지통 | 빌드 의존 745개 동일, 시나리오 16종 동일, Editor 흐름·릴리스 빌드 실행 확인 |

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

## S5 공유 전투 정리

### 작은 작업 현황

| 작업 | 상태 | 내용 | 검증 |
| --- | --- | --- | --- |
| S5-0 기준선 | 완료 | BeforeS5 사본 24개·해시, Driver 시간 432조합, 고정 프레임 시나리오 13개 | 아래 기준 기록 |
| S5-1 Driver 시간 선택 단일화 | **완료** | `AttackTimingProfile` 4종과 `GetProfileTiming` 표 하나를 판정(Update)·표현(GetPresentationTiming)·베기 구간(GetStrikeWindow)이 함께 사용. 직렬화 필드 이름·값 유지, Header/InspectorName/Tooltip 보완 | 시나리오 1~12 사건 255줄 동일, 432조합 중 404 동일·28은 호출되지 않는 상태 조합 |
| S5-2 Driver 런타임 위치 | **완료** | `AssetDatabase.MoveAsset`으로 `Assets/Scripts/Player/PlayerActionDriver.cs`에 이동. GUID `16120d095e7649344ac473d5333b6a6c` 유지, 빈 `Player/Testing` 폴더(GUID `1e31ae7a10f69844cabd5f4058602cd1`)는 OS 휴지통으로 이동 | 컴파일 오류 0, Player 프리팹 Missing Script 0, Visual→Driver 참조와 시간 값 11개 유지, 프리팹 해시 동일 |
| S5-3 연계 예약 상태 정리 | **완료** | Coordinator의 예약 설정 5곳을 `QueueAttack`, 취소 12곳을 `ClearQueuedAttack`으로 모음. 중복 summary 정리, 들여쓰기 1줄 수정 | 시나리오 16종: 돌진 없는 14종 사건 동일, 돌진 2종은 도착 이후 동일. 432조합 동일 |
| S5-4 경계 문서화 | **완료** | 공격 시간 편집 위치, 행동 원본, 애니메이션이 결과에 주는 경계, 호환 경로 사용 근거를 `카타나_전투_V2.md`에 기록 | 문서 검토 |

### 기준 기록 방법

- 이 컴퓨터의 Editor는 백그라운드에서 Play 프레임이 진행되지 않는다. 검증 Play 동안만 `Application.runInBackground = true`로 진행시키고, 종료 직후 `PlayerSettings.runInBackground = false`로 되돌린다. ProjectSettings.asset SHA256 `83496A5D…3AC7`이 전후 동일한지 확인한다.
- Play 시작 직후 Director 컴포넌트를 Play 중에만 끄고 적이 0인 상태에서 기록한다. 씬은 저장하지 않는다.
- `s5-matrix-*.txt`: Player 상태 6종 × 대상 연계 타수 3 × 연계 타수 3 × 반격 여부 × 교체 2타 1타 시간 사용 여부 × 공격 종류 2 = 432개 결과. 반사로 바꾼 필드는 같은 eval 안에서 복구한다.
- `s5-timeline-*.txt`: `Time.captureFramerate = 60`으로 게임 시간을 1/60초에 고정하고 API로 1타 단독, 1→2타 정상/늦은/이른 입력, 2타→1타 순환, 회복 중 입력, 가드·정확한 패링·자동 반격, 일반 가드, 투사체 패링, 공격 중 가드, 가드 중 공격, 공격 중 후방 피격, 대상 돌진 2연계를 실행한 사건 기록이다.
- 실제 키보드·마우스·패드 입력이 아니다. API 결과로만 기록한다.
- 기준 결과 요약: 1타 시간 0.32/0.6초, 2타는 클립 5번 프레임(0.333초)에 연결, 이른 입력(0.05초) 거부, 반격 판정 종료 0.183초·행동 종료 0.35초, 대상 돌진 베기 종료 0.333초. 일반 공격 회복 시간이 현재 프리팹에서 0이라 06번 시나리오는 Recovery 상태가 나타나지 않고 자유 상태 입력으로 기록됐다.
- 시나리오는 Play 시작 프레임 1부터 1/60초로 고정하고 프레임 400에서 시작한다. 처음 기록(`s5-*-before.txt`, `s5-*-after.txt`)은 아래 Editor 상태 문제 이전·도중의 기록이라 비교 기준으로 쓰지 않는다.

### S5 중 확인한 Editor 상태 문제와 처리

- 이 Editor(PID 61860)는 10:07에 시작했고, 사용자가 14:03에 `pull`(MCP 패키지 제거)과 `refactoring` 체크아웃을 했다. Editor가 백그라운드라 S5 시작 시점까지 디스크 변경을 가져오지 않았다. 마지막 컴파일은 10:06이었다.
- S5-1 컴파일 전 Refresh에서 삭제 402·변경 17·추가 6을 처음 감지했고 `The open scene(s) have been modified externally` 대화상자가 메인 스레드를 막았다. 사용자가 직접 Reload를 눌렀다.
- Reload 시점에 옛 스크립트가 로드되어 있어 메모리의 씬에서 S2·S4가 추가한 `BambooDefenseDirector._spawner`와 `DefenseRecordHud._text`가 null이 됐다. 그 상태의 Play에서 Director 초기화 오류와 HUD NullReference가 났다. 디스크 씬은 BeforeS5와 같은 해시이며 저장하지 않았다.
- 새 스크립트 컴파일 후 dirty=false를 확인하고 디스크의 BambooDefense를 `EditorSceneManager.OpenScene`으로 다시 열었다. Spawner·Record·마커 12개 참조와 Missing Script 0을 확인했다. 체크아웃으로 바뀐 비스크립트 에셋은 씬·HUD 프리팹(변경 없는 `DefenseDirectionMarker`만 사용)·폰트·URP 설정뿐이라 추가 재가져오기는 하지 않았다.
- Player·Effects·Enemies·Combat 스크립트의 수정 시각은 모두 10:06 이전이다. 따라서 처음 기준 기록의 Player 전투 코드는 디스크 코드와 같았다. 그래도 비교는 정상 씬에서 원래 Driver로 다시 기록한 `*-before-reloaded.txt`를 기준으로 했다.
- 다른 컴퓨터에서 이어 받을 때도 Editor를 켠 채 브랜치를 바꿨다면 먼저 Refresh·컴파일을 끝내고 열린 씬을 디스크에서 다시 연 뒤 작업한다.

### S5-1 결과

- 변경: `PlayerActionDriver.cs`(S5-2 이후 `Assets/Scripts/Player/`). 공개 API(`GetPresentationTiming`, `GetStrikeWindow`, `GetPresentationElapsed`, `UseIaidoTimingForNextNormalAttack`)와 직렬화 필드 이름·.meta GUID는 그대로다. Player 프리팹·씬은 다시 저장하지 않았다.
- 시간 묶음: 1타·발도(`_iaido*`, `_strikeWindow`), 기본 2타(`_normal*`), 대상 돌진(`_targetedCombo*` + 기본 2타 판정 폭), 패링 반격(`_counter*`).
- 선택 규칙: 일반 공격은 반격 → 다음 공격 발도 예약·1타·1타 시간을 쓰는 교체 2타 → 기본 2타 순서다. 판정은 공격 시작 프레임에 고정하고 표현 조회는 현재 상태로 계산한다(기존과 같음). 발도 베기는 대상 연계 타수가 있으면 대상 돌진, 없으면 발도다.
- 의도한 차이: 대상 돌진 종류(`IaidoFinisher` + 연계 타수>0)를 `NormalAttack`·`IaidoDash` 상태에서 조회할 때 기존 식은 반격·1타 판정 폭을 섞었다. 새 식은 항상 대상 돌진(기본 2타) 폭을 쓴다. 이 종류를 조회하는 `PlayerCombatAudio`·`PlayerCombatEffects`·`PlayerAttackSlashVfx`는 SlashStarted 직후나 같은 행동 ID의 `IaidoSlash` 상태에서만 조회하므로 실제 경로의 결과는 같다. 432조합 중 해당 28개만 다르다.
- 검증 근거: `s5-matrix-before-reloaded.txt` ↔ `s5-matrix-after-reloaded.txt`(위 28개 외 동일), `s5-timeline-before-reloaded.txt` ↔ `s5-timeline-after-reloaded.txt`·`s5-timeline-after-repeat.txt`(1~12번 동일).
- 13번 대상 돌진의 돌진 이동 프레임은 같은 새 코드로 두 번 실행해도 16·21프레임으로 달랐다. Driver가 아닌 `PlayerMotor` 돌진 쪽 비결정성으로 보며 원인은 조사하지 않았다. 도착 후 베기 종료 19프레임·회복 10프레임은 모든 실행에서 같다.
- 되돌리기: `BeforeS5/Assets/Scripts/Player/Testing/PlayerActionDriver.cs`의 내용을 현재 Driver 파일에 덮어쓰고 컴파일한다. 필드 이름이 같아 에셋 이전은 필요 없다.

### S5-2 결과

- `PlayerActionDriver`는 Player 프리팹에서 실제 사용하는 런타임 컴포넌트이므로 `Testing` 이름의 폴더에서 Player 폴더로 옮겼다. 클래스·namespace·assembly는 그대로다(`Assets/Scripts`에 asmdef 없음).
- 스크립트 GUID를 참조하는 에셋은 Player.prefab 하나이며 파일 해시가 BeforeS5와 같다. Testing 폴더 GUID를 참조하는 에셋은 없었다.
- 명세 경로를 `1인칭_근접_교전`, `카타나_전투_V2`(2곳), `카타나_애니메이션_좌표계_보정`, `플레이어_시스템`에서 갱신했다. `플레이어_시스템_인수인계`의 다른 프로젝트 절대 경로 링크는 원래부터 깨져 있었고 S7 문서 정리 대상이라 그대로 두었다. 계획서의 최초 조사 표는 당시 기록이라 바꾸지 않았다.
- 되돌리기: Editor에서 `Assets/Scripts/Player/PlayerActionDriver.cs`를 `Assets/Scripts/Player/Testing/`(폴더 재생성)로 `AssetDatabase.MoveAsset`한다. 또는 BeforeS5의 `Testing.meta`·`Testing/PlayerActionDriver.cs(.meta)`를 복사하고 현재 파일·.meta를 지운다. GUID가 같아 프리팹 연결은 유지된다. 명세 경로도 함께 되돌린다.

### S5-3 결과

- 변경: `Assets/Scripts/Player/KatanaV2/KatanaCombatCoordinator.cs`만 수정했다. 직렬화 필드·공개 API·이벤트·실행 순서는 그대로다.
- 예약 설정(`QueueAttack`): 대상 돌진 중 입력, 1·2타 중 입력, 회복 중 입력, 자유 상태의 이른 연계 입력 2곳. 취소(`ClearQueuedAttack`): 일반 공격 시작, 반격 시작, 예약 실행 성공·다시 판단, 피격, 입력 소유권 상실, 연계 초기화, 일시정지, 가드 시작, 돌진 시작, 일반 공격 미적중·반격 종료, 대상 연계 종료.
- 동작 보존 근거: 기존 취소 지점마다 지우던 보조 값의 조합이 달랐지만, 보조 값 4개는 `_queuedAttack`이 참일 때만 읽는다. 예약을 다시 설정하는 5곳은 모두 보조 값과 행동 ID·준비 시각을 새로 쓴다. 유일한 예외 읽기인 대상 베기 시작의 준비 시각 갱신도 예약이 없으면 다음 설정에서 덮인다. 1타 5번 프레임 연결(`IsFirstAttackFollowupTransition`) 읽기는 예약 실행 중이라 취소 전에 끝난다.
- 기존 동작으로 남긴 것: 자유 상태 이른 연계 분기가 같은 조건으로 두 번 있다. 두 번째는 첫 번째 조건이 거짓일 때만 도달해 사실상 실행되지 않는다. 동작에 영향이 없어 지우지 않았다. `_queuedTargetedAttack` 이름은 실제 의미(준비 시각에 입력 판단 다시 실행)와 다르지만 이름 변경 범위를 넓히지 않고 주석과 매개변수 이름으로 설명했다.
- 회복 중 예약 분기는 현재 Player 프리팹의 일반 공격 회복 0, 피격 회복 0 때문에 API 시나리오로 도달하지 못했다. 코드 검토로만 확인했다.
- 검증 근거: `s5-3-timeline-before.txt` ↔ `s5-3-timeline-after.txt`, `s5-3-matrix-*.txt`. 추가 시나리오 14(공격 중 피격 뒤 납도 중 입력), 15(납도 중 입력), 16(실제 대상의 정확한 패링 → 자동 반격 돌진 → 반격 적중 0.067초·종료 0.35초).
- 되돌리기: `BeforeS5/Assets/Scripts/Player/KatanaV2/KatanaCombatCoordinator.cs`를 같은 경로에 복사하고 컴파일한다. 에셋 이전은 없다.

### S5-4 결과

- `docs/features/카타나_전투_V2.md` 맨 앞의 현재 계약에 시간 묶음표, 행동 상태 원본표, 애니메이션 경계, 호환 경로 확인을 정리했다.
- 애니메이션 경계: 칼날 판정은 Animator 평가 뒤 검 Transform 위치를 쓴다. 1타→2타 연결은 교체 1타 클립 5번 프레임 도달을 Visual이 관측하고 Coordinator가 결정한다. 클립 Idle 복귀 완료가 납도 생략과 다음 1타 시작 시점을 바꾼다. 교체 클립 유무와 `교체 2타에 1타 타이밍 사용`이 연계 규칙과 Driver 시간 묶음을 바꾼다.
- 효과음·접촉 효과·검기 VFX는 Driver의 베기 구간 시작을 그대로 사용한다. 표현만 따로 옮기는 설정은 없으며 이번 단계에서 추가하지 않았다.
- 호환 경로는 삭제하지 않았다. Relay(Animation Event 없음, `fireEvents=false`, Driver만 호출)와 `UseIaidoTimingForNextNormalAttack`(호출처 없음)은 S7 후보로 기록만 했다.

### S5 범위에서 하지 않은 것

- PlayerMotor·PlayerController 재작성, 범용 FSM/DI, 시간 배율 소유권 통합, 보스 의존(R12) 정리, 입력 어댑터(R11) 정리.
- 표현이 Coordinator에 1타 후속을 요청하는 경로의 구조 변경. 현재도 Visual은 5번 프레임 도달만 알리고 시작 여부는 Coordinator가 예약·행동 ID로 결정하므로 문서화로 정리했다.
- `교체 2타에 1타 타이밍 사용` 설정을 Visual에서 Driver로 옮기는 직렬화 이전. 클립 슬롯과 짝을 이루는 설정이라 이전 이득보다 프리팹·씬 override 이전 위험이 크다고 판단했다.
- Stage_2·Stage_3·TwinBossBattle의 Play 확인. 공유 변경은 Player 프리팹 컴포넌트 코드뿐이고 Build 5개 씬은 Driver·Coordinator·Visual 필드를 override하지 않는다(YAML 검색). 대표로 Stage_1·BossBattle만 아래처럼 확인했다.

### S5 공유 변경 점검 (다른 씬·실제 루프)

- Stage_1(`s5-smoke-stage1.txt`): 1타→2타가 0.333초에 연결됐다. 2타 시작 4프레임 뒤 씬 연출이 플레이어를 Paused로 바꿔 나머지 입력은 진행되지 않았다. Console 오류·경고 0.
- BossBattle(`s5-smoke-boss.txt`): 보스 대상 돌진 베기 종료 0.333초·회복 10프레임(BambooDefense와 같음), 일반 1타 판정 0.433초·종료 0.6초, 정확한 패링 뒤 자동 반격 판정 0.183초·종료 0.35초. BambooDefense와 같은 시간이다. Play 종료 때 `BossController.OnDisable → BossPresentation.StopPreparationSounds`에서 `Managers can only be created during Play Mode` 오류가 1건 났다. 보스 코드의 기존 종료 순서 문제로 보며 S5에서 수정하지 않았다.
- BambooDefense 실제 루프(`s5-defense-live.txt`, Director 켬, 플레이어 입력 없음): 준비 2초, 투척수 2→3, 준비 종료 약 5초 뒤 근접 적 등장, 수리검 피해로 HP 100→0, 생존 14.4초에 GameOver, HUD `생존 00:14    처치 0` 유지. Console 오류·경고 0.
- 점검 후 BambooDefense를 디스크에서 다시 열었다. Stage_1·BossBattle·BambooDefense·Player 프리팹 파일의 수정 시각과 해시는 바뀌지 않았다.

## S6 정식 진입·빌드

### 작은 작업 현황

| 작업 | 상태 | 내용 | 검증 |
| --- | --- | --- | --- |
| S6-0 조사·기준 | 완료 | BeforeS6 사본 11개·해시. IntroScene의 IntroMenu 2개가 같은 버튼을 공유함을 확인. Play 리스너 순서 IntroManager → Canvas, 실제로 Stage_1이 열림 | `s6-before-manifest.json` |
| S6-1 Intro 진입 | 완료 | `Canvas`의 IntroMenu `_gameSceneName` SampleScene → BambooDefense. `IntroManager`의 IntroMenu `m_Enabled` 1 → 0(값 Stage_1 유지). Unity API로 저장 | 활성 IntroMenu 1, 시작 → BambooDefense |
| S6-2 빌드 목록 | 완료 | 활성: IntroScene(0), BambooDefense(1). Stage_1·Stage_2·BossBattle 비활성. 항목 순서·경로·GUID는 그대로이며 원래부터 파일이 없는 비활성 항목 3개도 유지 | EditorBuildSettings 차이는 enabled 4개 |
| S6-3 Windows 빌드 | 완료 | 릴리스 `Builds/BambooDefense-S6/Release/`(153MB, 오류 0·경고 21). 검증용 개발 빌드 `Builds/BambooDefense-S6/DevVerify/`(오류 0·경고 20) | 아래 결과 |
| S6-4 문서 | 완료 | 인트로 메뉴·게임오버 재도전·대숲 방어 명세, 이 문서, 계획 상태 줄 | 문서 검토 |

### 결과

- 시작 경로가 리스너 등록 순서에 따라 갈리지 않는다. 시작·종료·음량 절단은 `Canvas`의 IntroMenu 하나가 담당한다.
- IntroScene 저장 때 Unity·TMP가 TMP 신규 필드(`m_characterHorizontalScale`, `m_TextStyleHashCode`)와 Canvas 셰이더 채널 플래그(0 → 25)를 다시 썼다. 헤드폰 안내 TMP의 `m_fontColor32`가 실제 색 원본인 `m_fontColor`(0.94, 0.91, 0.82)에 맞춰졌다. 화면 색은 그대로다.
- Editor Play(`s6-editor-flow.txt`): Intro → 시작 `onClick` → BambooDefense(빌드 1)·Playing → 입력 없이 게임오버(생존 약 14초) → 재도전 → 새 Director, 처치 0, 시간 배율 1, 커서 잠금. 재도전 2회.
- 릴리스 빌드(`s6-release-build-report.json`, `s6-release-player.log`): 씬은 level0(Intro)·level1(BambooDefense) 2개. 1280×720 창으로 20초 실행해 예외 없이 Intro를 표시하고 창 닫기로 정상 종료했다. 파이프라인 런타임이 없어 메뉴 클릭부터의 흐름은 릴리스에서 확인하지 않았다.
- 개발 빌드(`s6-dev-player-flow.txt`, `s6-dev-player.log`): 파이프라인 런타임 서버(`--runtime-path Builds/BambooDefense-S6/DevVerify`)로 시작 → BambooDefense(빌드 1) → 게임오버 → 재도전 2회를 확인했다. 빌드에서는 `SceneManager.LoadScene(경로)`로 재도전하며 buildIndex 1을 유지했다. 로그 2,008줄에 예외·오류 없음.
- **출처 미확인 입력:** 개발 빌드 2회차 세션에 흐름 스크립트가 호출하지 않는 가드·돌진·일반 공격과 처치 4가 기록됐다. 창이 앞에 있을 때 실제 입력이 들어간 것으로 보이지만 누가 조작했는지는 확인하지 못했다. 실제 입력 검증으로 기록하지 않는다.
- 빌드 경고 21건은 셰이더(`Trail.shadergraph`, `ScreenSpaceOutline.shader`) 경고, 기존 CS0414 미사용 필드, 릴리스의 파이프라인 비활성 안내다.

### 검증용 개발 빌드에서 임시로 바꾸고 되돌린 것

- `ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json`(원래 없음)을 `enableInBuilds=true`로 만들고 개발 빌드 뒤 파일과 빈 폴더를 지웠다. 현재 설정 조회값 `enableInBuilds=false`.
- 개발 빌드 중 Unity가 `ProjectSettings.asset`을 LF로 다시 쓰고 `m_BuildTargetBatching`에 Standalone(정적 1·동적 0) 항목을 추가했다. 항목이 없을 때의 유효값은 정적 1·동적 1이라 값이 달라진다. SerializedObject로 항목을 지우고 저장해 커밋 `fcf3caa` blob과 같은 내용으로 되돌렸다(LF/CRLF 차이는 `autocrlf=true`가 처리). 유효값도 1·1로 확인했다. `GraphicsSettings.asset`은 수정 시각만 바뀌고 내용은 커밋과 같다. 커밋 blob은 `.git` 객체를 직접 읽어 `git-fcf3caa/`에 꺼냈다(Git CLI 미사용).
- 개발 빌드가 남긴 빈 `Assets/Settings/Pipeline/Resources` 폴더와 .meta를 OS 휴지통으로 옮겼다.
- 릴리스 빌드는 이 변경 이전(15:05)에 만들었다. 릴리스 빌드 당시 메모리의 배칭 항목 여부는 확인하지 못했다.

### S6에서 발견하고 고치지 않은 것

- Editor Play 종료 때 `DefenseShuriken.OnDisable → ShurikenDirectionIndicator.HandleReleased → PlayerMotor.ViewOrigin`에서 이미 파괴된 Transform을 읽어 `MissingReferenceException`이 1건 났다. 씬을 내릴 때 비행 중인 수리검과 플레이어의 파괴 순서에 따라 생기는 S3/S4 수명 문제다. 재도전 4회와 개발 빌드 종료에서는 나지 않았다. 별도 버그 수정으로 남긴다.
- 기존 `Stage_*`·`BossBattle`의 `NextSceneTrigger._nextSceneBuildIndex`·`BossSceneExit`(buildIndex+1)는 해당 씬이 빌드에 없어 정식 경로에서 실행되지 않는다. 기존 모드를 다시 빌드에 넣으면 인덱스가 바뀌므로 함께 조정해야 한다.

### 되돌리기

- IntroScene: `BeforeS6/Assets/Scenes/IntroEndingScene/IntroScene.unity`를 복사한다. 또는 Canvas IntroMenu 대상을 원래 값으로, IntroManager IntroMenu를 활성으로 되돌린다.
- 빌드 목록: `BeforeS6/ProjectSettings/EditorBuildSettings.asset`을 복사하거나 Stage_1·Stage_2·BossBattle 활성, BambooDefense 비활성으로 바꾼다.
- 빌드 산출물 `Builds/BambooDefense-S6/`는 Git 제외 폴더이며 지워도 된다.

## S7 에셋·레거시 코드·문서 정리

### 판단 기준

- 빌드 의존 범위(`s7-build-closure*.txt`): 활성 빌드 씬, 모든 `Resources`, `StreamingAssets`, `Plugins`, GraphicsSettings·QualitySettings 참조, 미리 로드 에셋, EditorBuildSettings 설정 객체에서 `AssetDatabase.GetDependencies(재귀)`로 계산했다. S7 전후 모두 745개이며 차이 0이다.
- 에셋 삭제 조건: 빌드 의존 범위 밖이고, 남는 프로젝트 에셋에서 GUID로 참조되지 않으며, 코드·도구의 경로·이름 문자열(`Shader.Find`, `LoadAssetAtPath`, 씬 이름) 사용처가 없을 것.
- 스크립트 삭제 조건: 남는 에셋의 GUID·UnityEvent 타입 이름 참조가 없고, 다른 코드가 타입 이름을 쓰지 않으며, `MenuItem`·`InitializeOnLoad`·`RuntimeInitializeOnLoadMethod`·에디터 창 같은 자동 진입점이 없을 것. 삭제 후 다시 검사해 더 이상 나오지 않을 때까지 반복했다.
- 서드파티는 스크립트·asmdef·셰이더와 include(`.cginc`·`.hlsl`)·셰이더 그래프·라이선스/설명 문서를 남겼다. 셰이더 include는 GUID 의존으로 잡히지 않기 때문이다(IntroTextSlice가 TMP `TMPro_Properties.cginc`를 씀).
- 모든 삭제는 파일과 .meta를 `BeforeS7/`에 같은 경로로 복사하고 GUID를 `s7-*-trashed.json`에 남긴 뒤 `AssetDatabase.MoveAssetsToTrash`로 OS 휴지통에 옮겼다. 커밋 `fcf3caa`에도 원본이 있다.

### 작은 작업 현황

| 작업 | 내용 | 근거 / 검증 |
| --- | --- | --- |
| S7-0 조사 | 빌드 밖 에셋 2,050개(340MB) 분류, 기존 모드 씬 14개·서드파티 데모 씬 38개, Resources 사용 현황 | `s7-outside-summary.txt`, `s7-nonbuild-scenes.txt`, `s7-resources-usage.txt` |
| S7-1 문서 | FEATURE_SPEC의 깨진 `발자국_시스템.md` 링크를 감각 표현으로 연결, 빈 행 제거, 현재 기능/기존 모드 기록 분리, 미등록 명세 4개 등록. 인수인계 문서를 현재 읽기 안내로 다시 씀 | 원문 `BeforeS7/docs/` |
| S7-2 잔여 에셋 | `Assets/Temp`(검증 캡처), FootstepTest 테스트 머티리얼 3, EffectTestInkBackdrop.mat, AlwaysVisibleOutlineMask.shader, Readme.asset·TutorialInfo, KatanaV2 PoseAligned 미사용 클립 2·컨트롤러 1, NewPlayerWeapon.controller, Pulse.wav | 각 GUID 참조 0, 이름 문자열 0. `s7-small-trashed.json` |
| S7-3a 레거시 코드 | `PlayerActionDriver.UseIaidoTimingForNextNormalAttack`와 플래그, Coordinator의 도달 불가 연계 분기 제거 | 시나리오 16종·432조합이 S5-3 결과와 동일(`s7-3-*.txt`) |
| S7-4 기존 모드 씬 | Build(Stage_1~3, BossBattle, TwinBossBattle), Map(MainMap, Stairwell, Stairwell_1, NavMesh), LevelDesign_HP(Stage_Test, NavMesh 6), TestScene 3, TutoScene, EndingScene, Tutorial 머티리얼·NavMesh, Scenes 루트의 NavMesh 4, EndingMonologue. 빌드 목록에서 삭제·누락 씬 항목 7개 제거 | 대상 폴더에 빌드 의존 파일 없음 확인, 남는 코드의 씬 이름 문자열 없음. `s7-oldscenes-trashed.json` |
| S7-3b 고아 스크립트 | 1차 13개(BossBattleHitEffects, EffectTestSword, FootstepTestDriver, CoroutineHelper, `Managers/InputManager.cs`의 PlayerInputController, BossRoomAudio, BossSceneExit, NextSceneTrigger, StageDoor, StairwellMusic, EndingSceneController, PlayerHealthBar, PlayerStaminaBar), 2차 2개(PlayerStamina, SceneTransitionFade), 비게 된 폴더 3개 | 3차 검사에서 에디터 도구 2개 외 후보 없음. 컴파일 오류·경고 0 |
| S7-5 서드파티 | Epic Toon FX 1,511, Eric VFX Studio 141, Free Slash VFX 68, Shurikens pack 111 = 1,831개(329MB)와 빈 폴더 89개 | 남은 서드파티 116개. `s7-thirdparty-delete.txt`, `-keep.txt`, `-empty-folders.txt` |

### 사고와 복구

- S7-2에서 `Assets/Materials/FootstepTest` 폴더를 통째로 옮기면서 빌드가 쓰는 `Sprite.mat`(GUID `d9859716a952461db94ff1cc2aacb654`)도 함께 옮겼다. 후보 목록에는 없던 파일이다. Missing 검사에서 Footstep·Wave·EnemyProjectile_Basic 프리팹의 머티리얼 참조가 끊긴 것을 발견하고 `BeforeS7`에서 폴더 .meta와 함께 같은 GUID로 복원했다. 복원 후 Footstep 프리팹이 Sprite를 다시 참조하고, 삭제 파일 전체를 빌드 의존 목록과 대조해 다른 사례가 없음을 확인했다. 그 사이 빌드·저장은 하지 않았다.
- 이후 폴더 단위 삭제는 폴더 안 파일을 빌드 의존 목록과 먼저 대조한 뒤 수행했다.

### 남긴 것과 이유

- `PlayerAnimationEventRelay`: 행동 ID 추적으로 한 번 더 거르는 동작이 있어 직접 호출로 바꾸면 경계 동작이 달라질 수 있다.
- 보스·쌍둥이 보스 코드와 `Resources/Enemies`·`Resources/Prefabs`의 기존 모드 프리팹: 공용 전투 코드(R12의 `BossController` 의존)와 Resources 경로 로드 때문에 남겼다. Resources 안에 있어 빌드에 포함된다. `Resources/Prefabs` 110개 중 정식 씬에서 직접 닿는 것은 13개다. 줄이려면 코드의 경로 로드와 보스 의존을 먼저 정리해야 한다.
- `GameSceneInitializer`가 참조하는 입력 어댑터(R11)·`CorridorRoundController`·`TutorialGuide.cs`: 코드 참조가 있어 남겼다.
- 원래부터 비어 있던 폴더(`Art`, `TextMesh Pro`, `Editor/Maps`, `Resources/Manager`, `Settings/Sensory`)는 다른 도구가 경로를 기대할 수 있어 남겼다.
- `ProBuilder Data/Default Material Palette.asset`, `Editor/SoundControlWindow.uss`: 에디터 도구가 쓴다.

### 검증

- 빌드 의존 범위: 745개 동일. 남은 빌드 밖 에셋 158개(4MB: 스크립트·셰이더·에디터 도구·라이선스).
- Missing 검사(`s7-missing-check.txt`): 남은 Missing 참조 13건은 모두 S7 이전부터 있던 것이다. 참조 GUID가 S7에서 지운 1,914개 GUID에 없다. `KatanaV2_Responsive.controller`의 Player@Guard 모션, Free Slash VFX `Flying Particles.mat` 텍스처 1, `PC_Renderer.asset`의 프로브 볼륨 디버그 리소스 7이 해당한다. `DefaultVolumeProfile`의 null 하위 객체도 기존 상태다.
- 컴파일: 오류 0, 경고 0(기존 CS0414 경고도 이번 재컴파일 출력에는 없었다).
- API 시나리오 16종·432조합: S5-3 결과와 동일.
- Editor Play(`s7-editor-flow.txt`): Intro 시작 → BambooDefense → 게임오버 → 재도전 2회, Console 오류·경고 0.
- Windows 릴리스 빌드(`Builds/BambooDefense-S7/Release/`, `s7-release-build-report.json`): 성공, 오류 0·경고 21, 153.29MB. 20초 실행 로그 예외 0(`s7-release-player.log`). 빌드 크기는 Resources 구성이 같아 거의 그대로다.
- 이 Unity 버전은 **빌드할 때마다** `ProjectSettings.asset`의 `m_BuildTargetBatching`에 Standalone(정적 1·동적 0) 항목을 추가한다(S6 개발 빌드와 S7 릴리스 빌드 모두). 작업 트리를 커밋 내용과 같게 두려고 이번에도 항목을 지웠다. 이후 빌드하면 같은 3줄 차이가 다시 생긴다.
- 개발 빌드 API 흐름은 S7 뒤 다시 하지 않았다. 빌드 의존 에셋이 같고 Editor 흐름과 릴리스 실행을 확인했다.

### 되돌리기

- 특정 항목: `s7-<묶음>-trashed.json`에서 경로·GUID를 찾아 `BeforeS7/`의 파일과 .meta를 같은 경로로 복사하고 Refresh한다. OS 휴지통에서도 복원할 수 있다.
- 전체: `BeforeS7/Assets` 아래 전부를 프로젝트 `Assets`에 복사하고(덮어쓰기 대상은 Driver 1개), `BeforeS7/ProjectSettings/EditorBuildSettings.asset`을 복사한 뒤 컴파일한다. 문서는 `BeforeS7/docs/`.
- Coordinator 도달 불가 분기 복구: `RequestAttack`에서 `BeginSheathe(); _queuedOpeningAttack = true; return true; }` 블록 바로 뒤에 `if (strike > 1 && _clock < _nextComboAllowedAt) { QueueAttack(false, false, false, _nextComboAllowedAt); return true; }`를 다시 넣는다.

## 변경 파일

S7(미커밋): 위 S7 표의 삭제 대상, `Assets/Scripts/Player/PlayerActionDriver.cs`, `Assets/Scripts/Player/KatanaV2/KatanaCombatCoordinator.cs`, `ProjectSettings/EditorBuildSettings.asset`, `docs/FEATURE_SPEC.md`, `docs/features/`의 인수인계·카타나 전투 V2·대숲 방어·인트로 메뉴·감각 표현·이펙트 시스템과 기존 모드 명세 11개의 상태 줄, 이 문서와 계획서 상태 줄.

S6(미커밋):

- `Assets/Scenes/IntroEndingScene/IntroScene.unity`: 시작 담당 IntroMenu 정리·BambooDefense 연결.
- `ProjectSettings/EditorBuildSettings.asset`: 정식 빌드 목록.
- `docs/features/인트로_메뉴.md`, `게임오버_재도전.md`, `대숲_방어.md`, 이 진행 문서, 계획서 상태 줄.
- `ProjectSettings/ProjectSettings.asset`, `GraphicsSettings.asset`은 개발 빌드가 바꾼 뒤 커밋 내용으로 되돌렸다. 줄바꿈이나 수정 시각 때문에 Git이 수정으로 표시하면 내용 차이가 없는지 확인한다.

S1~S4 변경은 사용자가 `335f912`·`fcf3caa`로 커밋했다. S5 변경은 아래 S5 목록이며 **미커밋**이다(refactoring 브랜치 규칙상 Codex·Claude는 커밋하지 않는다).

S5:

- `Assets/Scripts/Player/PlayerActionDriver.cs` + .meta: S5-1 시간 묶음 단일화, S5-2에서 `Player/Testing`에서 이동(GUID 유지). `Assets/Scripts/Player/Testing.meta`와 빈 폴더는 OS 휴지통으로 이동.
- `Assets/Scripts/Player/KatanaV2/KatanaCombatCoordinator.cs`: S5-3 연계 예약 설정·취소 단일화.
- `docs/features/카타나_전투_V2.md`: 현재 공격 시간 계약, 행동 원본, 애니메이션 경계, 호환 경로 확인. Driver 경로 2곳.
- `docs/features/플레이어_시스템.md`, `1인칭_근접_교전.md`, `카타나_애니메이션_좌표계_보정.md`: Driver 경로·역할.
- `docs/features/대숲_방어.md`, 이 진행 문서, 계획서 상태 줄.

S1~S4(참고):

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

**현재 컴퓨터에는 아래 표의 Baseline~s4 항목이 없다.** S4 이전 작업을 한 컴퓨터에 남아 있을 수 있다. 이 컴퓨터에서 만든 S5 근거는 다음과 같다.

| 파일 / 경로 | 내용 |
| --- | --- |
| BeforeS5/ + s5-before-manifest.json | S5 직전 코드·.meta·Player 프리팹·BambooDefense·관련 문서 24개(2,343,589 bytes)와 SHA256 |
| s5-matrix-before-reloaded.txt / -after-reloaded.txt / -after-repeat.txt | S5-1 전후 Driver 시간 432조합 |
| s5-timeline-before-reloaded.txt / -after-reloaded.txt / -after-repeat.txt | S5-1 전후 고정 프레임 시나리오 13종 |
| s5-3-matrix-before.txt / -after.txt, s5-3-timeline-before.txt / -after.txt | S5-3 전후 432조합, 시나리오 16종 |
| s5-smoke-stage1.txt / s5-smoke-boss.txt / s5-defense-live.txt | 다른 씬 점검과 실제 방어 루프 |
| s5-matrix-before.txt / s5-timeline-before.txt / s5-*-after.txt | Editor 상태 문제 이전·도중 기록. 비교 기준 아님 |
| BeforeS6/ + s6-before-manifest.json | S6 직전 IntroScene·빌드 설정·IntroMenu·BambooDefense·관련 문서 11개와 SHA256 |
| s6-editor-flow.txt | Editor Play: Intro 시작 → BambooDefense → 게임오버 → 재도전 2회 |
| s6-release-build-report.json / s6-release-player.log | 릴리스 빌드 보고서와 실행 로그 |
| s6-dev-player-flow.txt / s6-dev-player.log | 개발 빌드 런타임 API 흐름과 플레이어 로그 |
| git-fcf3caa/ | `.git` 객체에서 꺼낸 커밋본 ProjectSettings·GraphicsSettings·EditorBuildSettings(비교용) |
| BeforeS7/ | S7에서 지운 모든 파일·.meta와 문서·설정 사본(3,849개, 338MB), 수정 전 Driver, 문서·빌드 설정 사본 |
| s7-*-trashed.json | 묶음별(small, oldscenes, scripts1, scripts2, thirdparty) 삭제 경로·GUID·파일 수 |
| s7-build-closure(-before/-after).txt, s7-outside-*.txt | 빌드 의존 범위와 범위 밖 목록(전후) |
| s7-thirdparty-delete/keep/empty-folders.txt, s7-orphan-scripts.txt, s7-missing-check.txt | 서드파티 판정, 고아 스크립트, Missing 검사 |
| s7-3-*.txt, s7-editor-flow.txt, s7-release-* | S7 뒤 시나리오·흐름·빌드 근거 |

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
- S6에서 Windows 릴리스·개발 빌드를 만들었다. 릴리스 빌드에서 메뉴 클릭·게임오버·재도전을 사람이 직접 조작한 확인, 화면(윤곽선·폰트·HUD)·FMOD 사운드의 육안·청취 확인은 미실시다. 개발 빌드 2회차의 전투 입력은 출처 미확인이다.
- Editor Play 종료 때 비행 중 수리검의 `Released` 처리에서 파괴된 플레이어 Transform을 읽는 `MissingReferenceException`(S3/S4 수명 경계)이 남아 있다.
- 과거 이력의 중복/오래된 설명 및 FEATURE_SPEC의 발자국 시스템 문서 링크 정리는 S7에서 처리한다.
- 제품 질문은 이미 권장안으로 확정됐다. 새 작업 충돌이 생기면 해당 근거를 따로 기록한다.
- S5 검증은 모두 API 호출이다. 실제 마우스·키보드·패드로 하는 1·2타 연계, 가드 유지·해제, 패링, 회피 검기는 미확인이다.
- Coordinator의 회복 중 예약 분기는 현재 Player 프리팹 값(일반 공격 회복 0, 피격 회복 0)으로 도달하지 않아 코드 검토로만 확인했다.
- 대상 돌진·반격 돌진의 이동 프레임은 같은 코드에서도 실행마다 16~21프레임으로 달랐다. PlayerMotor 쪽 원인은 조사하지 않았다.
- BossBattle Play 종료 때 `BossController.OnDisable`의 `Managers` 접근 오류(기존 문제로 추정)는 수정하지 않았다. Stage_2·Stage_3·TwinBossBattle은 Play 확인을 하지 않았다.
- Driver의 Inspector 이름·설명은 바뀌었지만 Player 프리팹을 다시 저장하지 않았다. Unity가 다음에 프리팹을 저장하면 필드 선언 순서대로 YAML 순서가 바뀔 수 있으며 값은 이름으로 유지된다.

## 다른 터미널에서 재개하는 순서

1. 이 문서, AGENTS.md, FEATURE_SPEC.md, 대숲 방어 명세, 카타나 전투 V2 맨 앞의 현재 계약을 읽는다. 계획 단계 S0~S7은 끝났으므로 새 요청을 확인한 뒤 진행한다.
2. .git/HEAD를 직접 읽고 브랜치 규칙을 따른다. 현재 refactoring에서는 Git CLI를 사용하지 않는다. S5·S6·S7 변경은 미커밋이므로 사용자가 커밋했는지 refs·작업 트리 파일로 확인한다. S7은 삭제가 많으므로 커밋 전에 Git 변경 목록이 `s7-*-trashed.json`과 맞는지 보는 것이 좋다.
3. Unity 연결과 active scene/dirty/Play를 읽기 전용으로 확인한다. Editor를 켠 채 브랜치·pull이 있었다면 Refresh와 컴파일을 먼저 끝내고, dirty=false를 확인한 뒤 열린 씬을 디스크에서 다시 연다. 씬을 재생성하지 않는다.
4. S7 전에 S6 빌드(`Builds/BambooDefense-S6/Release/week06-blindblade.exe`)로 사람이 메뉴→전투→게임오버→재도전을 직접 해 보면 실제 입력·화면·사운드 미확인을 줄일 수 있다. S7 후보 정리 뒤에는 같은 방식으로 다시 빌드한다. 개발 빌드 API 확인이 필요하면 `RuntimePipelineConfig.json`을 임시로 만들고, 끝나면 이 문서의 S6 절처럼 ProjectSettings를 커밋 내용과 비교해 되돌린다.
5. S7은 빌드 검증 뒤 참조/문자열/Resources/제작 도구 근거가 있는 후보만 정리한다. S5에서 기록한 후보: `PlayerAnimationEventRelay`(Animation Event 없음, Driver만 호출), `PlayerActionDriver.UseIaidoTimingForNextNormalAttack`(호출처 없음), Coordinator의 도달하지 않는 이른 연계 분기, `플레이어_시스템_인수인계.md`의 다른 프로젝트 절대 경로. 각 작은 작업 직후 이 문서를 갱신한다.

### PowerShell 연결

이전 터미널 8538은 닫혔고 그 ID에 의존하지 않는다. CLI 1.0.0-beta.5, Editor 6000.3.22f1, Pipeline 0.8.0-exp.1이다. S4는 PID 33420, S5는 이 컴퓨터의 PID 61860/port 7800이었다. 연결 정보는 매번 다시 조회한다.

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
- S5에서 쓴 방법: 긴 C#은 scratchpad의 .cs 파일로 저장해 `command eval_file --file <경로>`로 실행했다. 큰따옴표 이스케이프 문제가 없다. 컴파일은 `AssetDatabase.Refresh()` + `CompilationPipeline.RequestScriptCompilation()` 뒤 `Library/ScriptAssemblies/Assembly-CSharp.dll` 수정 시각과 `console_status`의 groundTruth(ageMs·compiling)로 끝을 확인했다. `recompile` 명령은 30초 제한에 걸렸다.
- Editor가 백그라운드면 Play 프레임이 진행되지 않는다. Play 중에만 `Application.runInBackground = true`를 쓰고 Play 종료 뒤 `PlayerSettings.runInBackground = false`로 되돌린 다음 ProjectSettings.asset 해시를 비교한다. `EditorApplication.update` 콜백은 백그라운드에서 호출되지 않아 쓸 수 없었다.
- CLI 요청이 `Main thread operation timed out`으로 실패하면 Unity 모달 대화상자를 의심한다. 창 제목은 Win32 `EnumWindows`로 읽기 전용 확인할 수 있다. 버튼은 사용자에게 확인받는다.

## 마지막 검증과 인계 문장

- 2026-10-14 05:46 UTC 마지막 컴파일 직후: **compilationFailed=false, compiling=false, consoleErrors=0, consoleWarnings=1**. 경고는 기존 `KatanaDebugOverlay._showCombatFeedback` CS0414다.
- S5 종료 시 Editor Console: 오류 1(05:50 BossBattle 점검 종료 때의 `Managers can only be created during Play Mode`), 경고 0. 그 뒤 BambooDefense 실제 루프 Play에서는 새 오류·경고가 없었다.
- Console 누적 기록에는 S5 이전부터 있던 `t.GetParent() == nullptr` Assertion 5건과 MCP 패키지 잔여 경고, Editor 상태 문제 중의 Director 초기화 오류·HUD NullReference, BossBattle 종료 오류가 남아 있다. 콘솔을 지우지 않았다.
- 씬·프리팹은 저장하지 않았고 BambooDefense를 dirty=false로 다시 열어 둔 상태다. 게임 에셋 삭제·빌드·Git CLI는 수행하지 않았다.

> (S7 이후) `docs/plans/BambooDefense_refactoring_progress.md`를 먼저 읽어 주세요. BambooDefense 전환 리팩토링 계획 S0~S7이 끝났습니다. 정식 경로는 Intro → BambooDefense → 게임오버 → 재도전이고, 기존 모드 씬·미사용 서드파티 에셋은 S7에서 정리했습니다(BeforeS7 사본·OS 휴지통·커밋 fcf3caa로 복구 가능). S5~S7 변경은 미커밋입니다. 남은 확인은 실제 입력·화면·사운드, S2 자연 근접 재사용, Play 종료 시 수리검 MissingReference, Resources의 기존 모드 프리팹 정리(보스 의존 정리 선행)입니다. refactoring 브랜치에서는 Git CLI·커밋을 하지 않습니다.
>
> (S6 이후 기록) `docs/plans/BambooDefense_refactoring_progress.md`를 먼저 읽고 S7부터 이어서 작업해 주세요. S6에서 Intro → BambooDefense → 게임오버 → 재도전을 정식 경로로 연결했고, Windows 릴리스 빌드 성공과 개발 빌드 API 흐름 확인을 마쳤습니다. 기존 Stage·Boss 씬은 빌드 목록에서만 비활성이며 파일은 남아 있습니다. S5·S6 변경은 미커밋입니다. S7은 참조·문자열·Resources·제작 도구 근거가 있는 후보만 정리하고, 정리 묶음마다 컴파일·Play·빌드로 확인해 주세요. 실제 입력·화면·사운드 확인은 미확인 항목으로 유지해 주세요.
>
> (S5 이후 기록) `docs/plans/BambooDefense_refactoring_progress.md`를 먼저 읽고 S6부터 이어서 작업해 주세요. S5(Driver 시간 묶음 단일화·런타임 폴더 이동, Coordinator 연계 예약 정리, 전투 경계 문서화)는 API 시나리오 전후 비교로 끝났고 변경은 아직 커밋되지 않았습니다. S1/S2 스폰 값, S4 HUD, S5 시간 정리를 다시 하지 마세요. 실제 입력·빌드 검증과 S2 자연 근접 재사용 관찰은 미확인 항목으로 유지해 주세요. Editor를 켠 채 브랜치를 바꿨다면 컴파일 후 열린 씬을 디스크에서 다시 연 뒤 시작하세요. 현재 refactoring 브랜치에서는 Git CLI·커밋을 하지 않습니다.
