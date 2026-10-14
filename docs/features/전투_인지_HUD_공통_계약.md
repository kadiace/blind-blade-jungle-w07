### 전투·인지·HUD 공통 계약

- 기능 설명: A/B/C/D가 공유할 세션·사건 식별·판정 결과·표시·시간 요청의 연결 규격을 제공한다. 전투 판정과 상태 변경은 각 담당자의 구현이 소유한다.
- 동작 조건 및 처리 흐름: D가 세션 문맥을 제공하고 A/B가 원본 ID를 부여한다. A는 방어 결과, B는 물리 수명, C는 표시 결과를 확정한다. D는 결과를 표시·기록하고 시간 요청을 합성한다.
- 구현 상태: 2026-10-14 D0의 신규 계약 타입 7개 파일과 JSON 로그 봉투 스키마 v0.1 초안을 작성했다. Unity 6000.3.22f1 batch 컴파일 오류 0, 기존 코드의 고유 경고 1건을 확인했다. 기존 런타임 연결·씬 변경·계약의 공동 합의는 미완료다. 실제 확인 결과와 TMP 종료 저장의 영향은 [D 진행 기록](../plans/전투_인지_HUD_D_progress.md)에 기록한다.

#### 관련 코드

| 파일 경로 | 클래스 / 메서드 | 역할 |
| --- | --- | --- |
| Assets/Scripts/Combat/Contracts/CombatSessionContracts.cs | CombatSessionKey, CombatTimestamp, ICombatSessionClock | 세션 세대와 공통 시계 계약 |
| Assets/Scripts/Combat/Contracts/CombatEventContracts.cs | CombatEventIdentity, CombatEntityKey, CombatCorrelationIds | 사건·생성 세대·입력/공격 식별 |
| Assets/Scripts/Combat/Contracts/DefenseResolution.cs | DefenseOutcome, IncomingAttackKind, DefenseResolution | A가 확정할 방어 결과 값 |
| Assets/Scripts/Combat/Contracts/PlayerHudContracts.cs | StaminaSnapshot, IPlayerStaminaReadModel | A의 스태미너를 D가 읽는 접점 |
| Assets/Scripts/Combat/Contracts/ThreatPresentationContracts.cs | ThreatPresentationSnapshot, FootstepPresentationSnapshot, IThreatPresentationProvider | C의 최종 표시 결과와 초기 조회 |
| Assets/Scripts/Combat/Contracts/CombatTimeContracts.cs | CombatTimeRequest, CombatTimeToken, ICombatTimeRequests | D 시간 제어기에 대한 요청·해제 |
| Assets/Scripts/Combat/Contracts/CombatTelemetryEnvelope.cs | CombatTelemetryEnvelope | 원본 사건의 메타데이터. 실제 저장 DTO는 D5에서 구현 |
| docs/contracts/combat-telemetry-v0.1.schema.json | 로그 봉투 스키마 | 파일 레코드의 필수 식별·시각·버전 필드 |

#### 연결 및 협업

- 연결된 기능: 대숲 방어, 카타나 전투 V2, 플레이어 피격·체력, 게임오버, 감각 표현.
- 공유하는 코드 및 상태: 신규 타입은 `BlindBlade.Combat.Contracts` namespace에 둔다. `DamageContext`와 `PlayerContracts`는 A 소유로 유지한다. HP는 기존 PlayerHealth API를 사용한다.
- 중복 또는 충돌 사항: 현재 검색에서 동일 namespace/type의 구현이 확인되지 않았다. 기존 코드가 신규 계약을 사용하게 하는 변경은 D0에 포함하지 않는다. 스태미너·새 판정 경로·C 공급자는 아직 없다.
- 승인 필요 사항 및 결과: 사용자가 2026-10-14 D0 진행을 요청했다. 공통 계약의 의미와 A/B/C API 변경은 [계약 및 결정 기록](../contracts/전투_인지_HUD_D0_계약.md)의 합의 대기 항목으로 남긴다. 외부 메시지 전송이나 공동 합의 완료로 처리하지 않는다.

#### 확인 결과

- Unity batch 종료 코드 0. KatanaDebugOverlay._showCombatFeedback의 CS0414 경고가 기존 파일에서 발생했다. D0 신규 코드의 컴파일 오류는 없다.
- 공용 씬/Player/HUD/GameStateManager 프리팹 4개는 시작 시 SHA-256과 일치한다.
- batch 종료 중 TMP 동적 폰트 자동 저장이 발생했다. 실행 전 해당 폰트의 내용 사본이 없어 정확한 변경 범위는 미확인이다. 원본 복원 여부를 추정해 덮어쓰지 않았으며 진행 기록에 남겼다.
- 전투 동작·HUD·시간 제어·로그 저장·Unity 직렬화 연결은 D1 이후 구현 대상이다.
