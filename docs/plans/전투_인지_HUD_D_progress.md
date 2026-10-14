# 전투·인지·HUD D 진행 기록

- 갱신일: 2026-10-14
- 현재 단계: **D0 — 계약 초안 작성, 공동 합의 대기**
- 요청: 사용자가 “D0부터 진행해”라고 지시했다.
- 작업 경로: `C:\Users\JUNGLE\Documents\w07-blindblade-plus`
- 브랜치: `.git/HEAD` 직접 확인 `feature/ui`. Git CLI·커밋·푸시·브랜치 변경 없음.
- 계획: [D PRD](전투_인지_HUD_D_구현_PRD.md)
- 계약: [D0 계약 및 연결 요청](../contracts/전투_인지_HUD_D0_계약.md), 버전 `0.1.0-draft`
- 명세: [공통 계약](../features/전투_인지_HUD_공통_계약.md)

## 단계 현황

| 단계 | 상태 | 완료한 것 / 남은 조건 |
| --- | --- | --- |
| D0 | 초안 작성, 공동 합의 대기 | 타입·스키마·소유권·요청서 작성. A/B/C 합의·공급자 적용·각 작업 공간 확인 필요 |
| D1 | 미착수 | 세션/시계 구현, 명시적 Initialize/Shutdown |
| D2 | 미착수 | 시간 제어기, 상태 연결, A의 직접 시간 쓰기 이관 |
| D3 | 미착수 | 명시적 HUD 참조, 바, 실제 피해 비네팅 |
| D4 | 미착수 | C 결과 소비, 발소리, 첫 일반 패링 통합 |
| D5 | 미착수 | 로그 저장·전처리 구현 |
| D6 | 미착수 | 강화 공격·극한 회피 통합 |
| D7 | 미착수 | 공용 에셋 연결·수동 인수 |
| D8 | 미착수 | 기능별 명세 및 최종 인수인계. 진행 기록은 D0부터 갱신 |

## D0 실제 변경

### 신규 코드

`Assets/Scripts/Combat/Contracts/`에 아래 파일과 `.meta`, 폴더 `.meta`를 추가했다.

| 파일 | 역할 |
| --- | --- |
| CombatSessionContracts.cs | CombatSessionKey, CombatTimestamp, ICombatSessionClock |
| CombatEventContracts.cs | CombatEventIdentity, CombatEntityKey, CombatCorrelationIds |
| DefenseResolution.cs | IncomingAttackKind, DefenseOutcome, DefenseResolution |
| PlayerHudContracts.cs | StaminaSnapshot, IPlayerStaminaReadModel |
| ThreatPresentationContracts.cs | 위협/발소리 스냅샷, 초기 조회와 변경 사건 |
| CombatTimeContracts.cs | 요청/토큰/해제 인터페이스 |
| CombatTelemetryEnvelope.cs | 저장 DTO와 구분한 사건 메타데이터 |

모두 `BlindBlade.Combat.Contracts` namespace의 불변 값 또는 인터페이스다. MonoBehaviour, 실제 공급자, 전역 static 이벤트, 상태 저장기, 직렬화 에셋 참조는 추가하지 않았다. 기존 ActionId의 int 형식을 유지했다. 자동 생성 속성 기반 값 타입은 JsonUtility 저장 대상으로 사용하지 않는다.

### 문서

- `docs/contracts/전투_인지_HUD_D0_계약.md`: 기준선·소유권·필드 의미·시계·사건 순서·합의 대기·담당별 요청.
- `docs/contracts/combat-telemetry-v0.1.schema.json`: 로그 봉투 필드와 ID 범위. 사건 payload별 검증은 D5 대상이다.
- `docs/features/전투_인지_HUD_공통_계약.md`: FEATURE_SPEC 양식의 신규 명세.
- `docs/FEATURE_SPEC.md`: 개발 중인 공통 계약 등록.
- PRD: D0 진행과 남은 공동 확인을 체크박스로 반영.
- 이 progress: 새 담당자가 재개할 시작점 기록.

## 확인 결과

- 기존 소스 검색: 새 판정/ID/스태미너/표시 공급자의 구현이 아직 확인되지 않았다.
- JSON: PowerShell JSON 파싱 성공. JSON Schema 전체 의미 검증과 사건별 payload 검증은 미실시다.
- Unity 연결: status는 `STATUS_NO_INSTANCES`. pipeline list는 isRunning=true이나 서버 미연결, SafeMode=false를 보고했다. OS 프로세스에는 Unity Editor가 없고 기존 Editor.log 마지막은 종료 기록이었다.
- Unity 컴파일: 6000.3.22f1 batch 실행 성공, 종료 코드 0, 컴파일 오류 0. 고유 C# 경고 1건은 기존 `KatanaDebugOverlay.cs:16`의 `_showCombatFeedback` 미사용(CS0414)이다. 같은 경고가 로그에 두 번 출력됐으므로 고유 건수로 기록한다.
- 검증 명령: `unity run <프로젝트 경로> --editor-version 6000.3.22f1 --timeout 240 --format json -- -nographics -logFile <아래 로그 경로>`.
- 검증 근거: `UserSettings/CombatHud/D0-20261014/unity-compile.log`, `verification.json`. UserSettings는 다른 작업 공간에 자동 전달되지 않으므로 필요한 근거 파일은 별도로 전달한다.
- Play·실제 입력·화면·음향: 미실시. D0에는 런타임 연결이 없다.
- 공용 씬/프리팹: 직접 저장하지 않았고 기준 에셋 4개의 SHA-256이 모두 시작 시 값과 일치한다.
- 테스트 코드: 작성·실행하지 않았다.

### 컴파일 확인 중 발생한 폰트 자동 저장

- Editor 종료 콜백에서 TMP가 `Assets/Fonts/ChosunCentennial_otf SDF.asset`의 동적 폰트 데이터를 정리하고 저장했다. 종료 로그와 해당 파일 갱신 시각으로 확인했다.
- 실행 전 이 폰트의 해시·내용 사본을 확보하지 못했다. D0 이전 내용과 정확히 비교할 수 없으므로 기존 사용자 변경을 추정해 복원하지 않았다.
- `.git/index`를 읽기 전용으로 직접 해석하여 확인한 현재 폰트 blob은 인덱스 blob과 다르다. 이것만으로 전체 차이가 이번 실행에서 생겼다고 단정할 수 없다. Git CLI·스테이징 변경은 없다.
- 현재 폰트 사본은 `UserSettings/CombatHud/D0-20261014/ChosunCentennial_after_compile.asset`에 보존했다. 이 파일은 D0 계약 기능의 의도된 변경에 포함하지 않는다.
- 다음 Editor 실행 검증 전에는 동적 폰트처럼 종료 시 자동 저장되는 파일도 사본/해시를 확보한다. 폰트의 원래 내용이 확보되면 자동 저장 부분만 대조해 복원한다.

## 합의·요청 상태

| 대상 | 준비한 요청 | 상태 |
| --- | --- | --- |
| A | ID 확장, 스태미너 읽기, 판정 사건, 시간 이관, 강화 상태/시간 의미 | 문서 준비, 전달·응답 미확인 |
| B | ID/세대/수명 스냅샷, 조회/제거/피해 API, 종료 순서, 최대 위협 수 | 문서 준비, 전달·응답 미확인 |
| C | 초기 전체 조회, 표시 방향/색/순서, 발소리 고정 원점·수명 공유 | 문서 준비, 전달·응답 미확인 |
| 전원 | 같은 계약 버전, 시계/기획 결정, 작업 공간·미저장 상태 | 공동 확인 대기 |

실제 외부 메시지 전송은 하지 않았다. D가 계약 파일을 작성한 것과 네 담당자가 합의한 것을 구분한다. D0 전체 종료 조건은 아직 충족하지 않았다.

## 다음 작업과 재개 방법

1. AGENTS.md → FEATURE_SPEC → PRD → 이 progress → D0 계약을 읽는다.
2. 현재 파일에서 `BlindBlade.Combat.Contracts`를 검색해 A/B/C가 추가로 연결했는지 확인한다. 신규 작업이 있으면 덮어쓰지 말고 차이를 계약 변경 기록에 반영한다.
3. D0 계약의 DC01~DC11에 실제 답변·담당·일시·버전을 기록한다. 미구현 API를 임의의 성공 값으로 채우지 않는다.
4. 각 작업 공간의 채택 계약과 컴파일을 확인한 뒤 D0 완료로 전환한다.
5. D1은 CombatSessionKey 생성/세대, 공통 시계와 입력 시계 기준점, GameSceneInitializer Initialize/Shutdown 순서부터 구현한다. 현재 Game.InitializeSession 안의 상태 Initialize가 이전 세션 Shutdown을 수행하는 순서를 먼저 검토한다.

현재 열린 Editor와 미저장 편집은 없다(OS 프로세스 확인 기준). batch 확인은 씬 저장을 요청하지 않는다. 공용 에셋 원본 해시는 D0 계약 1절에 있으며, 이번 신규 계약은 GUID가 연결된 런타임 컴포넌트가 없으므로 D0에서 되돌릴 때는 D0 신규 파일·문서와 문서 목록의 해당 변경만 대상으로 한다. 기존 전투 코드·공용 에셋은 되돌리기 대상이 아니다.
