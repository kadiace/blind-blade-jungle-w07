# 전투·인지·HUD D0 — 공통 계약 및 연결 요청

- 작성: 2026-10-14, D
- 계약 버전: `0.1.0-draft`
- 로그 봉투 스키마: `0.1.0`
- 상태: D의 구현 가능한 초안 작성. A/B/C의 합의·적용·공급자 구현은 대기 중이다.
- 코드 위치: `Assets/Scripts/Combat/Contracts/`
- namespace: `BlindBlade.Combat.Contracts`
- 실행 순서와 인수 기준: [D PRD](../plans/전투_인지_HUD_D_구현_PRD.md)

## 1. 기준선과 소유권

현재 브랜치는 `.git/HEAD`를 직접 읽은 `feature/ui`다. Git CLI는 사용하지 않았다. Unity 버전은 `6000.3.22f1`, Pipeline은 manifest 기준 `0.8.0-exp.1`이다. 프로젝트 게임 코드는 기존 Assembly-CSharp 구성을 유지한다. 신규 asmdef는 추가하지 않는다.

현재 소스에는 AttackId/HitId, 새 스태미너 공급자, ThreatPresentationProvider, 시간 요청 제어기가 없다. 기존 DamageContext 생성자는 `(float, GameObject, Vector3, Vector3, string)`이며 PlayerActionEvent/PlayerAttackRequest의 ActionId는 `int`다. 신규 계약은 기존 타입을 교체하거나 현재 호출부를 바꾸지 않는다.

| 담당 | 단독 수정 범위 | 다른 담당자에게 제공할 것 |
| --- | --- | --- |
| A | PlayerController/Motor/Health/Contracts/Driver, KatanaV2, DamageContext, InputManager | 판정 결과·입력/행동 ID·스태미너 읽기·시간 요청 이관 |
| B | Defense 진행/스폰/예산/투사체, EnemyCombatService/Health/공격, Defense 전용 프리팹 | 원본 공격/타격/투사체 ID·수명 스냅샷·조회/제거/피해 API |
| C | 인지/윤곽선/감각·발자국 표현, 셰이더/머티리얼/렌더러 설정 | 표시 결과·발소리 후보·공통 색·파동 |
| D | 이번 Contracts, HUD 표시, 피격 비네팅, 세션/시간/로그, 공용 연결 도구/문서 | 계약 파일·세션 시계·시간 요청 API·HUD·로그 |

공용 에셋 저장 담당은 D다. A/B/C는 자기 작업 씬·Variant에서 확인한 뒤 연결 요청서로 전달한다. 공용 원본에 Apply하지 않는다.

### 공용 에셋 기준 해시

D0 시작 시 파일 SHA-256이다. 이번 단계는 해당 파일을 저장하지 않는다.

| 에셋 | SHA-256 |
| --- | --- |
| Assets/Scenes/Defense/BambooDefense.unity | FA598B8785939EC61A49501E5600237F721E30E2378E6014CFD9FCD1B790FFAE |
| Assets/Resources/Prefabs/Player.prefab | 1DF7C61BB8DE75BF85F6B60822761A5FDBC885241FAD8BD3D2C7F8B65015E02F |
| Assets/Scenes/Defense/UI/BambooDefenseHud.prefab | BF6671F92FCD92B9475BB722300D2024B30A0571E96C9CE3067173AC18E96C6D |
| Assets/Resources/Prefabs/GameManagers/GameStateManager.prefab | 4B1D668758BDEF227288B5200B3D7580C363001A578BB9C722C0A239EB73ED44 |

공용 에셋의 미저장 상태에 대한 팀 공동 확인은 미실시다.

## 2. 세션·식별·시각 규칙

| 타입 | 필드 | 의미 |
| --- | --- | --- |
| CombatSessionKey | SessionId:string, Generation:int | D가 새 세션마다 GUID N 소문자 문자열과 양수 세대를 부여 |
| CombatTimestamp | RealtimeSeconds, GameplaySeconds, ActiveUnscaledSeconds:double | 같은 시작점의 초 단위 경과 시간 |
| CombatEventIdentity | Session, SourceId:string, SourceSequence:long | 원본 사건 고유 키. 공급자가 사건 생성 시 부여 |
| CombatEntityKey | EntityId:string, SpawnGeneration:int | 세션 내 대상 식별과 풀 활성화 세대 |
| CombatCorrelationIds | InputId/AttackId/HitId/ProjectileId/DodgeId:long, ActionId:int | 전투 원인과 결과를 연결하는 ID |

- 활성 세션의 ID/세대, 사건 순번, 생성 세대는 양수다. 미해당 연결 ID는 `0`이다. 기본 구조체 값은 활성 세션·유효 사건으로 사용하지 않는다.
- EntityId는 종류 접두사 등으로 세션 내 충돌을 피한다. 예: `enemy/12`, `projectile/41`, `telegraph/7`, `footstep/<source>/<sequence>`.
- 적/투사체를 재사용할 때 SpawnGeneration을 증가시킨다. GetInstanceID만으로 영구 식별하지 않는다.
- SourceId는 세션 내 발행자마다 유일해야 한다. 단순 `B`를 여러 독립 발행자가 공유하지 않는다. 예: `B/projectile-registry`, `A/player`, `C/footsteps`.
- SourceSequence는 해당 발행자가 원본 사건마다 증가시킨다. 같은 사건 재전달에는 같은 키를 유지한다. 표시의 매 프레임 갱신은 원본 전투 사건을 새로 발행한 것으로 세지 않는다.
- ActionId는 기존 `int`를 보존한다. 새 ID의 카운터는 소유자가 관리하며 D가 수집 시 생성하지 않는다. 범위 초과 시 조용히 0/음수로 순환시키지 않는다.
- RealtimeSeconds는 정지·슬로모션에도 흐른다. GameplaySeconds는 전투에 적용된 배율을 따른다. ActiveUnscaledSeconds는 명시적 일시정지·게임오버 동안 멈추고 슬로모션 동안에는 실시간으로 흐른다. 세 시계 모두 세션 시작에서 0으로 맞춘다.
- 입력 원본 시각은 별도 `inputClockId`, `rawInputSeconds`, 변환 기준점으로 보존한다. Input System 이벤트 시각을 발생 시점 대신 수집 시점으로 덮어쓰지 않는다. 시계 제공자는 D1에서 구현한다.
- 구독은 주 스레드에서 연결하고 현재 스냅샷을 즉시 읽는다. 종료 직전 사건에는 발생 당시의 세션 키를 유지한다. 이전 키를 현재 키로 바꿔서 수락하지 않는다.

## 3. A — 판정·스태미너·시간 연결

### 방어 결과

`DefenseResolution(long attackId, long hitId, DefenseOutcome outcome)`은 A의 확정 결과다.

| DefenseOutcome | 처리 의미 |
| --- | --- |
| Unresolved | 기본값/미구현. 확정 결과 사건으로 발행하지 않음 |
| Hit | 방어되지 않은 타격. 실제 피해 적용 여부·양은 피해 결과로 별도 기록 |
| Guard | 일반 가드로 차단 |
| NormalParry | 일반 패링. 투사체 반사 또는 근접 추가타의 원인 |
| PerfectParry | 강화 준비의 원인. 접촉 즉시 광역 피해를 실행하지 않음 |
| DodgeAvoided | 회피 무적으로 회피. DodgeId로 극한 보상의 1회성을 관리 |
| Invulnerable | 회피 이외 무적으로 차단. 극한 회피 보상을 만들지 않음 |

`IncomingAttackKind`는 Unspecified/Melee/Projectile/Charge다. Charge를 패링할 수 있는지는 기획 결정 항목이다. 같은 HitId의 여러 Collider 접촉을 새로운 독립 타격으로 처리하지 않는다. 연속타는 HitId를 분리한다.

판정 진입점 시그니처는 **A가 기존 KatanaDamageReceiver/Coordinator 경로를 검토하여 확정할 대상**이다. 이번 초안에서는 미구현 resolver 인터페이스를 추가하지 않았다. DamageContext에 ID·공격 종류를 추가할 때 생성자 호환 방법과 B의 이관 목록을 함께 전달한다. 기존 bool 경로와 새 경로의 이중 피해를 막는 책임은 A/B다.

### 스태미너 표시

`IPlayerStaminaReadModel.Current`는 `StaminaSnapshot(Current, Maximum)`을 반환하고 변경 후 `Changed`를 한 번 발행한다. D는 이 계약만 읽는다. 소모·환불·회복·고갈에 따른 행동 제한은 A가 소유한다. Current는 0~Maximum, Maximum은 양수인 공급자 값을 전달한다.

HP는 기존 `PlayerHealth.CurrentHealth/MaxHealth/HealthChanged`를 재사용한다. 실제 피격은 `DamageReceived`를 사용한다. A가 기존 사건 의미를 변경하면 이관 계약을 알려야 한다. 치명타에서 SetDead가 DamageReceived보다 먼저 발생하는 현재 순서를 D가 고려한다.

### 시간 요청

- A는 `ICombatTimeRequests.RequestSlowMotion(CombatTimeRequest)`를 호출하고 반환 토큰을 보관한다.
- 요청은 현재 세션, 유일한 OwnerId, Reason, 유한한 `0 < Scale <= 1`을 담는다. 0 배율은 A의 슬로모션 요청으로 허용하지 않는다.
- RequestId 0 토큰은 거부를 뜻한다. A는 요청 실패를 전투 성공으로 오인하지 않는다.
- `Release(token)`은 실제 해제에서 true, 중복/이전 세션/없는 토큰에서 false다.
- 준비→공격 전환 때 기존 토큰을 유지할지 새 이유로 교체할지는 A/D 결정 대상이다. 같은 프레임 교체 때문에 잠깐 정상 속도로 돌아가지 않도록 D2에서 전환 순서를 정한다.
- GameOver/명시적 정지는 D의 세션 상태 연결로 처리한다. A는 직접 timeScale을 쓰지 않는다. 정책 구현은 D2다.

**A 전달 요청:** DamageContext 확장 설계, 입력 수락/거부 시각과 ID, 스태미너 공급자, 확정 판정 사건, 강화 준비/시작/종료 이유, PlayerMotor 시간 쓰기 이관. 현재는 문서로 준비했으며 실제 전달·응답은 미확인이다.

## 4. B — 물리 사실·공격 원본·종료 정보

- B가 AttackId, 개별 HitId, ProjectileId, 적 EntityId/SpawnGeneration을 생성한다.
- 원시 접촉은 무적 중에도 A에게 전달하고 `attack_contact`로 분리한다. 최종 방어 결과를 B가 추측해서 기록하지 않는다.
- 생성/활성화/반사/통과/제거에는 세션 키·원본 사건 키·대상 키·위치 스냅샷·소유권·종료 이유가 필요하다.
- Released 뒤 파괴된 Transform을 D가 읽지 않도록 마지막 위치를 **파괴 전에** 복사해서 제공한다. 종료/예산 반환의 1회성은 B가 보장한다.
- 투사체 일반 반사, 적대 일괄 제거, 광역 피해의 명령 API는 B가 기존 서비스와 맞춰 제안한다. D0에서 빈 성공 구현을 추가하지 않는다.
- 초기 현재 목록 조회를 제공하거나 C가 같은 레지스트리에서 현재 목록을 조회할 수 있어야 한다.
- 텔레그래프 유지 시 발사 전 위협 키와 ProjectileId 매핑, 최대 동시 위협 수를 제공한다.

**B 전달 요청:** 위 수명 스냅샷, API 시그니처, 풀 세대 규칙, 최대 동시 위협·예산, Play 종료 오류 재현 여부. 문서 준비 상태이며 전달·응답은 미확인이다.

## 5. C — 위협·발소리 표시 결과

`IThreatPresentationProvider`는 ThreatChanged/FootstepChanged 사건과 GetCurrentThreats/GetCurrentFootsteps 초기 조회를 제공한다. 반환 목록의 내부 컬렉션을 D가 수정하거나 장기 보관하지 않는다. 제공자는 자신의 컬렉션 수명·갱신 시점을 명시한다.

### ThreatPresentationSnapshot

- Session + Target(EntityId, SpawnGeneration)으로 표시를 식별한다. Revision은 대상의 이번 세대에서 양수로 증가한다.
- IsActive=false는 해당 표시 수명의 종료다. 같은 세대가 다시 활성화되지 않는다. 종료는 더 큰 Revision으로 전달하고 초기 활성 목록에서 제외한다.
- IsEligible은 인지 자격, IsInCamera는 합의한 카메라 판단이다. WorldVisible/UiVisible이 최종 표현 플래그다. D는 앞의 값들로 별도 거리·차폐 규칙을 만들지 않는다.
- WorldVisible와 UiVisible은 동시에 true가 되지 않는다. 부적격/종료 대상의 둘 다 false를 C가 보장한다.
- UiDirection은 카메라 기준 UI 평면의 방향 `(x=오른쪽, y=위)`이다. 유효 방향은 정규화하고, 카메라 바로 뒤 등 투영이 모호한 경우의 방향은 C가 안정적으로 정한다.
- Color의 alpha는 1, Alpha는 최종 0~1 불투명도로 전달한다. D는 같은 대상을 자기 색상 곡선으로 재계산하지 않는다.
- 통과·반사 후 UI 재표시 금지 판단은 B 수명을 받아 C가 확정한다. D도 종료된 세대와 마지막 Revision을 유지하여 늦은 갱신을 무시한다.

### FootstepPresentationSnapshot

- EventIdentity는 실제 발소리 1회의 키다. Emitter는 적 생성 세대다. Origin과 OccurredAt은 사건 발생 값으로 고정한다.
- 내부 Presentation.Target은 발소리별 유일 키다. 적 키를 그대로 쓰면 여러 발걸음이 합쳐지므로 별도 발소리 ID를 쓴다.
- 카메라 회전 시 Presentation/Revision만 갱신한다. 발소리 Origin을 적의 현재 위치로 덮어쓰지 않는다.
- 표시 수명은 D 설정의 소유다. C가 표시 후보를 언제 제거할지 같은 설정값/시계를 연결받아 합의한다. D3/D4에서 한 타이머만 권위 있게 사용하며 C/D 각각 다른 수명 값을 만들지 않는다.
- 적 비활성/재사용 시 해당 세대의 종료 결과를 발행한다. 초기 조회에는 현재 유효한 사건만 포함한다.

**C 전달 요청:** 위 공급자 적용 여부, 종료/초기 조회 순서, 거리·차폐·파동 우선순위, 발소리 수명 연결 방법, 용량 초과 시 표시 우선순위. 문서 준비 상태이며 전달·응답은 미확인이다.

## 6. 로그 봉투와 수명

코드의 CombatTelemetryEnvelope는 원본 사건의 Identity/OccurredAt/Correlation/EventType만 가진다. JsonUtility로 그대로 저장하는 DTO가 아니다. D5가 사건별 typed payload와 명시적 저장 DTO를 작성한다. 이번 JSON Schema는 봉투의 규격이며 payload별 검증은 D5에서 추가한다.

필수 저장 필드는 [JSON Schema](combat-telemetry-v0.1.schema.json)를 따른다. `eventSequence`는 D 저장기가 수신 시 부여하는 세션 내 순번, SourceSequence는 공급자의 발생 순번이다. 발생 시각과 기록 시각을 모두 남기며 수신 순번을 실제 발생 순서로 단정하지 않는다.

| 종류 | 권위 있는 공급자 | 최소 payload 의미 |
| --- | --- | --- |
| input_received/accepted/rejected | A | 입력 종류, raw 시각·시계 ID, 수락/거부 이유 |
| attack_contact | B | 원시 접촉 위치·공격 종류·공격자·피격자 |
| defense_resolved | A | DefenseOutcome, 무적 원인, 관련 InputId/DodgeId |
| damage_applied | 피해 소유자 A/B | 실제 차감량, 적용 전/후 값, 사망 여부 |
| resource_changed | A | 자원 종류, 변경 전/후, 최대치, 원인 |
| empowered_ready/started/ended, extreme_dodge | A | ActionId/DodgeId, 상태와 종료 이유 |
| projectile_lifecycle/enemy_lifecycle | B | 대상/세대, 수명 단계, 위치, 소유권/종료 이유 |
| footstep/reveal_pulse | C 또는 명시한 원본 소유자 | 사건 원점·발생 이유. 중계 사건은 원본 키 유지 |
| session_started/state_changed/session_ended/time_scale_changed/diagnostic | D | 상태·요청 수·실효 배율·종료/누락 이유 |

- 같은 피해를 raw 접촉, 판정, 피해 적용 3건으로 합산하지 않는다. 피해 통계는 damage_applied, 방어 통계는 defense_resolved를 사용한다.
- 중복 키는 `(SessionId, Generation, SourceId, SourceSequence)`다. AttackId/HitId는 원인 연결용이며 사건 중복 키가 아니다.
- ConfigVersion은 A/B/C/D 설정 스냅샷 manifest의 식별값이다. 세션 중 설정을 바꾸면 버전을 바꾸고 적용 시각을 남긴다. 파일 해시/직렬화 형식은 D5에서 구현한다.
- 정수 ID는 최대 Int64 범위다. 분석기는 정수를 그대로 읽어야 하며 JavaScript Number로 큰 ID를 변환하여 정밀도를 잃지 않게 한다.
- 입력 원본 시각·시계 기준점은 입력 payload/세션 메타데이터에 보관한다. 외부 시계를 무조건 Unity 실시간과 같은 기준으로 간주하지 않는다.
- UI 표시 갱신은 기본적으로 매 프레임 전투 로그로 저장하지 않는다. 실제 발소리/수명/판정 사건과 개발용 표현 진단을 구분한다.
- GameOver는 세션 내 상태 변경이다. 현재 치명타의 후속 DamageReceived까지 수집한 뒤 씬 이탈/재도전/초기화 해제에서 로그를 닫는다.
- 로그에는 UnityEngine.Object 참조가 없다. 영구 기록에는 ID와 위치·수치 복사본만 사용한다.

## 7. 결정 기록과 합의 상태

| ID | 항목 | D 권장안/현재 상태 | 공동 확인자 |
| --- | --- | --- | --- |
| DC01 | 타입 경로·namespace·버전 | 이번 초안으로 작성, 소비자 적용 대기 | A/B/C/D |
| DC02 | 시계 | 전투: 게임시간, 준비 대기: 정지 제외 실시간, 입력: 원본 변환 보존 | 전원 |
| DC03 | T_ready | 입력 대기에만 사용, 수락 후 공격 종료까지 슬로모션 권장 | A/D |
| DC04 | 피격/회피/만료 취소 | A의 상태 전이·종료 이유 표 필요 | A/D |
| DC05 | 느려지는 대상 | 첫 통합은 세계/플레이어 동일 배율 권장 | A/C/D |
| DC06 | 시간 합성 | 정지 우선, 슬로모션 최소 배율, fixedDeltaTime 기준 유지 권장 | A/B/D |
| DC07 | 차폐·거리·파동 | C의 한 결과를 월드/UI가 공유, 세부 규칙 대기 | C/D |
| DC08 | 텔레그래프 | 유지 여부·발사 전후 ID·용량 대기 | B/C/D |
| DC09 | 발소리 수명·초과 표시 | D 설정을 공유하고 타이머 중복 금지, C 우선순위 필요 | C/D |
| DC10 | Charge 패링·반사 목표 소실·적 없는 강화 공격 | A/B 결정 문서 연결 필요 | A/B |
| DC11 | 공용 에셋 미저장 상태·기준선 | 파일 해시 확보, 팀 작업 공간/미저장 상태는 미확인 | 전원 |

현재 모든 공동 확인은 미완료다. 문서 작성으로 합의 상태를 완료로 바꾸지 않는다. 합의 시 담당·일시·결론·변경 버전을 이 표에 남긴다. 채팅/메일을 자동 전송하지 않았으며, 이 문서를 팀에 전달할 자료로 사용한다.

## 8. 다음 담당자의 실행 순서

1. 위 계약 파일과 스키마를 같은 버전으로 받는다. Assets의 신규 파일은 `.meta`와 함께 전달한다.
2. A/B/C는 요청 목록에 구현 가능 여부와 기존 API와의 차이를 기록한다.
3. D는 답변을 반영해 초안을 갱신하고, 공동 합의와 각 작업 공간 컴파일 확인 후 채택 버전을 고정한다.
4. D1의 세션/시계 구현을 시작한다. 미결정 정책에 의존하는 D2/D6은 해당 결정 후 연결한다.

이 문서는 D가 준비할 수 있는 D0 산출물이다. 네 작업 공간의 공동 합의·컴파일 통과는 별도의 D0 종료 조건으로 남아 있다.
