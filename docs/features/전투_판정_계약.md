### 전투 판정 계약

- 기능 설명: 플레이어 피해 수신·방어 결과·전투 사건의 A 소유 계약이다. `DefenseOutcome`, `IncomingAttackKind`, `DefenseResolution`은 [전투·인지·HUD 공통 계약](전투_인지_HUD_공통_계약.md)의 단일 namespace 타입을 사용한다. 패링 세분화·회피 무적·스태미너·강화 공격은 후속 기능이다.
- 동작 조건 및 처리 흐름:
  - `AttackId`는 공격 1회, `HitId`는 그 공격 안의 타격 순번, `Kind`는 공격 종류(`IncomingAttackKind`)다. 0과 `Unknown`은 미부여 상태다.
  - 기존 5인자 생성자는 유지하며 새 값을 0·0·`Unknown`으로 채운다. 기존 호출부는 수정하지 않았다.
  - 공격을 만드는 쪽(적·투사체 담당)이 생성 시 `AttackId`·`HitId`·`Kind`를 부여한다. 수신자는 같은 값을 판정 결과에 그대로 전파하며, 로그·UI 담당이 뒤늦게 ID를 만들지 않는다.
  - 수신자는 `Kind`가 `Unknown`이면 기존 방식(`KatanaDamageReceiver.IsFrontMelee` 등 공격자 컴포넌트 검사)으로 공격을 분류한다.
  - `PatternId`는 패턴 종류 이름이며 공격 1회 식별에 사용하지 않는다.
- 구현 상태: 0-1~0-5의 플레이어 계약과 입력 사건 발행은 유지한다. 2026-10-14 통합 과정에서 전역 중복 방어 타입을 `BlindBlade.Combat.Contracts`로 옮기고 A 구현이 이를 import하도록 정리했다. BambooDefense `DefenseShuriken`이 양수 AttackId/ProjectileId, HitId 1, Projectile 종류를 공급하고 resolver를 한 번 호출한다. Unity Editor 재컴파일 오류 0, Console 최신 표본 기존 경고 1건을 확인했으며 Play 동작은 미확인이다.

#### 방어 판정 결과와 진입점 (0-2)

- `IPlayerDefenseResolver.ResolveIncomingAttack(DamageContext)`는 공격 1회를 판정하고 `DefenseResolution`을 즉시 반환한다. 구현체는 Player의 `KatanaDamageReceiver`다. 호출자는 `GetComponentInParent<IPlayerDefenseResolver>()`로 찾는다.
- 결과가 `Hit`이면 구현체가 체력 감소·사망·피해 이벤트까지 처리한다. 호출자는 같은 접촉에 한 번만 호출하고 결과와 관계없이 `TakeDamage`를 다시 호출하지 않는다.
- 판정 순서: 비활성·피해량 0 이하·일시정지·시간 정지·사망이면 `Ignored`. 그 외에는 `Kind`로 방어 자격을 고른다. `Projectile`은 진입 방향이 전방 각도 안인지 보고 근접 자동 반격을 만들지 않는다. 그 외는 기존 전방 근접·보스 돌진·보스 원거리 규칙과 자동 반격을 사용한다. 가드 성공 시 패링 창 안이면 `PerfectParry`, 아니면 `Guard`이고 실패하면 `Hit`이다.
- `NormalParry`·`DodgeAvoided`·`Invulnerable`은 계약용 값이며 아직 반환하지 않는다. 패링 창 분리와 회피 무적 구현 때 추가한다.
- `DefenseResolution` 값: `Outcome`, `AttackId`·`HitId`(DamageContext에서 복사), `GuardInputId`(가드·패링 결과일 때 그 가드를 연 우클릭 InputId, 그 외 0), `AppliedDamage`(실제로 줄어든 체력, 체력보다 큰 피해는 남은 체력만큼), `ContactPoint`(DamageContext.Point), `GameTime`(Time.time), `RealTime`(Time.realtimeSinceStartupAsDouble).
- 호환: `TakeDamage`는 `ResolveIncomingAttack`을 호출하고 결과를 버린다. `TryDefendProjectile(context, out parried, allowCounter)`는 기존 호출부를 위한 경로로 유지한다. BambooDefense 수리검은 새 resolver를 한 번 호출하며 그 결과가 `Hit`이면 피해가 이미 적용돼 호출자가 `TakeDamage`를 다시 부르지 않는다.
- 반격 대상과 회피 ID는 해당 기능 구현 때 추가한다. 결과는 플레이어만 생성하므로 필드를 추가해도 읽는 쪽 코드는 깨지지 않는다.

#### 플레이어 전투 사건 (0-3)

- `KatanaCombatCoordinator.Events`(`PlayerCombatEvents`)가 플레이어 1명의 사건 묶음이다. 전역 static이 아니며 Coordinator 생성 시 함께 만들어진다. 구독자는 자기 수명에 맞춰 구독·해제한다.
- 사건은 사실이 확정된 뒤 한 번 발행한다. 값은 불변 구조체이고 Unity 오브젝트 참조 대신 ID·위치·수치만 담는다. 구독자는 받은 값으로 전투 결과를 바꾸지 않는다. 보상 실행은 반환값이나 명시적 호출로 한다.

| 사건 | 값 | 발행 위치 | 상태 |
| --- | --- | --- | --- |
| DefenseResolved | DefenseResolution | KatanaDamageReceiver.ResolveIncomingAttack, TryDefendProjectile 성공 | 발행 중 |
| InputResolved | PlayerInputRecord | InputManager | 발행 중 |
| EmpowerChanged | PlayerEmpowerEvent(EmpowerId, Stage, EndReason, SourceAttackId, ActionId, 시각) | 강화 공격 구현 | 형태만 정의 |
| DodgeChanged | PlayerDodgeEvent(DodgeId, Stage, AttackId, Position, 시각) | 회피 구현 | 형태만 정의 |
| StaminaChanged | PlayerStaminaEvent(Current, Max, Delta, Reason, DodgeId, 시각) | 스태미너 구현 | 형태만 정의 |
| RevealPulseRequested | PlayerRevealPulseEvent(PulseId, Cause, AttackId, Center, 시각) | 퍼펙트 패링·극한 회피 구현 | 형태만 정의 |

- 새 resolver 경로의 접촉 1회에는 `DefenseResolved`가 정확히 한 번 나간다. 기존 bool 경로는 성공 시 `TryDefendProjectile`, 실패 시 `TakeDamage`가 한 번만 발행한다.
- 기존 `EventRaised`·`ParrySucceeded`·`DefenseSucceeded`·`DamageReceived`·`HealthChanged`는 표현 담당 호환을 위해 그대로 유지한다.

#### 입력 ID 기록 (0-4)

- `InputManager`가 실제 누름마다 `InputId`를 1부터 발급하고 명령 처리 결과를 `PlayerInputRecord`로 발행한다. 같은 우클릭의 누름·누르고 있는 재진입·뗌은 같은 InputId를 사용한다.
- 상태 판정: 공격은 요청 직후 행동 ID가 바뀌면 `Started`(새 ActionId 기록), 수락됐지만 바뀌지 않으면 `Buffered`, 거부면 `Rejected`다. 방어는 요청 직후 가드 중이면 `Started`, 수락만 됐으면 `Buffered`, 거부면 `Rejected`다. 회피는 `Started`·`Rejected`, 우클릭 뗌은 항상 `Started`다.
- 누르고 있는 우클릭의 가드 재진입(`GuardHoldResume`)은 조건이 맞지 않으면 매 프레임 거부될 수 있어 실제로 가드가 시작된 경우에만 기록한다.
- `KatanaCombatCoordinator.BeginDefense(inputId)`는 요청한 InputId를 보관했다가 가드가 실제로 시작될 때 `GuardInputId`로 확정한다. 예약된 가드가 나중에 시작돼도 같은 InputId를 사용한다. 인자 없는 기존 호출은 0이다.
- 시각: `DeviceTime`은 Input System 콜백의 장치 입력 시각, `FrameRealTime`은 처리 프레임의 `Time.realtimeSinceStartupAsDouble`, `GameTime`은 `Time.time`이다. 장치 시각은 기록용이며 판정에 쓰지 않는다. 장치 시각과 실시간이 같은 기준인지는 Play에서 미확인이다.
- 예약 입력의 후속 기록: `Buffered`로 기록한 공격·가드 입력은 `InputManager.ResolveBufferedInputs`가 매 프레임 명령 처리 전에 확인한다. Coordinator의 `HasBufferedAttack`·`HasBufferedDefense`가 거짓이 되면 같은 InputId로 `Started` 또는 `BufferDropped`를 한 번 더 발행한다. 공격은 예약 당시와 행동 ID가 다르면 시작(새 ActionId 기록), 가드는 `GuardInputId`가 같으면 시작으로 본다. `DeviceTime`은 원래 누름 시각을 유지하고 `FrameRealTime`·`GameTime`은 확인한 프레임 시각이다.
- 후속 기록의 한계: 같은 프레임에 예약이 사라지고 입력과 무관한 공격(자동 반격)이 시작되면 시작으로 잘못 기록될 수 있다. 확인은 다음 `InputManager.Update`에서 하므로 기록 시각이 실제 시작보다 최대 1프레임 늦을 수 있다. 일시정지·게임오버 중에는 확인하지 않으며 씬 전환 시 추적을 초기화한다.
- 거부 사유: 1차에서 추가한다(2026-10-14 결정). 1차에서 공격 중 우클릭 거부, 스태미너 부족 회피 거부 등 거부 규칙이 새로 생기므로 사유 목록을 그때 한 번에 정한다. 기록 구조체에 필드를 추가해도 읽는 쪽 코드는 깨지지 않는다.
- 튜토리얼 재개 입력(`ApplyTutorialInput`)은 기록하지 않는다.

#### 시간 요청 계약 (0-5)

- 공식 BambooDefense 경로의 `Time.timeScale` 쓰기는 `CombatSessionRuntime`이 담당한다. `ICombatTimeRequests`와 세션 시간 런타임이 구현돼 있다.
- 플레이어 쪽 대상: `PlayerMotor` 대시 예고 슬로모션이 요청 토큰으로 연결됐다. 강화 준비 불릿 타임은 아직 연결하지 않았다. 요청 토큰은 대시 종료·취소·비활성화 경로에서 해제한다.
- 적용된 요청 계약:
  - 요청과 해제: 요청은 출처와 세계 배율을 받아 핸들을 반환하고, 해제는 같은 핸들을 두 번 해제해도 안전하다. 현재 적용 중인 세계 배율을 읽을 수 있어야 한다.
  - 겹침과 우선순위: 게임오버·일시정지(0)가 가장 우선한다. 재도전과 세션 종료 시 모든 요청을 무효로 하고 1로 초기화한다.
  - 플레이어 배율: 불릿 타임 중 플레이어는 세계보다 덜 느리다(2026-10-14 결정). 플레이어 배율은 플레이어 전투 설정의 Inspector 값으로 두고, 시간 제어기는 세계 배율만 쓴다. 플레이어 쪽이 현재 세계 배율로 이동·모션·판정·무적 타이머를 보정한다.
  - `GameStateManager`는 게임오버 정지를 `CombatSessionRuntime.SetFrozen`에 위임한다. `PlayerMotor`는 대시 예고 요청을 등록·해제하며 이전 전역 배율을 직접 저장·복원하지 않는다.

#### 퍼펙트 파동과 불릿 타임 (시야·월드 표현 구현, HUD·통합 시간 연동)

2026-10-14 사용자 결정이다. 파동 표현은 시야·월드 표현 담당이 구현하고, HUD·통합 담당은 시간 제어기를 만들 때 아래 시간 규칙에 필요한 실시간·게임 시간 구분을 제공한다.

- 발생: 퍼펙트 패링 또는 극한(퍼펙트) 회피가 확정되면 플레이어 주변에서 파동 이펙트가 퍼진다. 표현은 현재 웅크리기 파동을 참고한다.
- 파동의 퍼짐: 이펙트 반경 확장과 그에 맞춘 시야 공개 범위 확장은 T_ready 불릿 타임의 영향을 받지 않는다. 일시정지를 제외한 실시간으로 진행한다.
- 실루엣이 흐려짐: 파동으로 드러난 실루엣이 흐려지는 페이드는 불릿 타임의 영향을 받는다. 느려진 게임 시간으로 진행하므로 불릿 타임 중에는 실루엣이 더 오래 남는다.
- 시점: 퍼펙트 패링은 강화 준비와 불릿 타임이 시작되는 순간과 같다. 극한 회피는 불릿 타임 밖에서도, 강화 준비 중(준비 유지 결정)에도 생길 수 있다. 어느 경우든 같은 시간 규칙을 적용한다.

참고할 현재 웅크리기 파동 구조(웅크리기 입력은 제외 예정이다):

| 단계 | 코드 | 현재 시간 기준 |
| --- | --- | --- |
| 발행 | `PlayerController.EmitCrouchPulse` → `SensoryManager.Emit(SensoryEvent)` | - |
| 파동 이펙트 | `FootstepWave`(`SensoryEffect.Animate`) | `Time.deltaTime`. 불릿 타임에 느려진다 |
| 시야 공개 범위 확장·페이드 | `SensoryManager.PerceptionRequested` → `SensoryPerceptionMask.RegisterRegion`이 시작 시각을 `Time.time`으로 저장. 수명(`CrouchPulseInterval`) 동안 확장한 뒤 `_soundFadeDuration`(0.6초) 동안 페이드. 셰이더(`ScreenSpaceOutline.shader`)도 `_OutlinePerceptionTime`(`Time.time`)으로 같은 계산을 하고 `GetVisibility`가 C#에서 같은 규칙을 따른다 | `Time.time`. 확장과 페이드 모두 불릿 타임에 느려진다 |
| 지형 기억 | `TerrainMemory.RememberExpandingWave` | `Time.time`. 불릿 타임에 느려진다 |

- 현재 웅크리기 파동은 퍼짐과 실루엣 페이드가 모두 게임 시간을 쓰므로 위 시간 규칙과 다르다. 어떤 방식으로 구현하든 화면 표시(셰이더)와 C# 가시성 계산은 같은 시간 기준을 써야 화면과 판정이 어긋나지 않는다.
- 계획서와의 차이: 계획서는 파동 원인을 극한 회피와 강화 공격으로 적었으나 이 결정에서는 퍼펙트 패링과 극한 회피다. 강화 공격 발동 시 파동 여부는 강화 공격 연출을 만들 때 정한다.
- 플레이어 쪽 제공: `PlayerCombatEvents.RevealPulseRequested`(`PlayerRevealPulseEvent`)를 퍼펙트 패링·극한 회피가 확정될 때 한 번 발행한다. 값은 요청 ID(`PulseId`), 원인(`Cause`: `PerfectParry`·`PerfectDodge`), 원인 공격(`AttackId`), 파동 중심(`Center`, 요청 시점 플레이어 위치), 게임 시간·실시간이다. 현재는 형태만 정의했으며 발행은 퍼펙트 패링·극한 회피 구현 때 추가한다.

#### 결정 사항 (0-6)

`구현 완료`로 표시한 항목 외의 결정은 이후 단계에서 구현하며 현재 코드는 기존 동작이다. 다른 담당자와 함께 정할 항목은 해당 담당자 확인 전까지 플레이어 담당의 제안이다. `잠정`은 이후 조정할 수 있는 결정이다.

| 결정일 | 항목 | 결정 | 결정 범위 | 구현 시 변경 |
| --- | --- | --- | --- | --- |
| 2026-10-14 | 공격 중 우클릭 | 공격 중에는 가드·패링을 받지 않고 회피만 허용한다 | 플레이어 단독 | `KatanaCombatCoordinator.BeginDefense`의 공격 취소 후 가드 전환과 가드 예약 제거, 회피 시 공격 취소. 현재는 미적중 공격이면 공격을 취소하고 가드와 패링 창을 연다 |
| 2026-10-14 | 근접 돌진 공격 | 근접 베기와 같이 가드·일반 패링(자동 돌진)·퍼펙트 패링 대상이다 | 플레이어 + 적·투사체(확인 필요) | 방어 자격에 돌진 포함(`Kind = Charge` 또는 `EnemyChargeAttack` 검사). 적·투사체 담당은 패링 시 돌진의 쳐내기 반응을 제공한다. 현재는 가드해도 피해를 받는다 |
| 2026-10-14 | 무적과 극한 회피 | 무적은 회피 상태를 말하며, 회피 중 알맞은 시간에 공격이 닿으면 극한 회피다. 회피 무적이 가드·패링보다 먼저 판정된다 | 플레이어 + 적·투사체(확인 필요) | 회피 무적 원인 추가, `DodgeAvoided` 반환 |
| 2026-10-14 | 가드 판정 창 순서 | 누름 시점부터 퍼펙트 패링 → 일반 패링 → 가드 순서다. 퍼펙트·일반 패링 창의 시작·끝 시각은 Inspector에서 조정하며 퍼펙트 시작 기본값은 0초다 | 플레이어 단독 | 현재 단일 `_parryWindow`(0.25초)를 두 창으로 분리, `NormalParry` 반환 |
| 2026-10-14 | 극한 회피 구간 | 회피 시작부터 극한 회피 구간, 그 뒤 무적 구간이다. 극한 회피 구간의 시작·끝과 무적 끝은 Inspector에서 조정하며 극한 시작 기본값은 0초다. 극한 구간에 닿으면 `DodgeAvoided`(환불·파동 1회), 그 뒤 무적 구간은 피해만 없고 보상이 없다 | 플레이어 + 적·투사체(확인 필요) | 현재 회피(0.18초)에는 무적이 없다 |
| 2026-10-14 | 반격 후 무적 중 접촉 | 피해만 없고 극한 회피·패링 보상은 없다(`Invulnerable`) | 플레이어 + 적·투사체(확인 필요) | 근접 일반 패링의 자동 돌진 뒤 무적 원인 추가 |
| 2026-10-14 | `Ignored` 결과 | 사망·일시정지·시간 정지 등으로 판정하지 않은 접촉은 `Hit` 대신 `Ignored`를 반환한다 | 플레이어 + 적·투사체(확정안으로 전달) | 구현 완료 |
| 2026-10-14 | 피해 적용 위치 | `ResolveIncomingAttack`이 `Hit`일 때 체력까지 줄인다. 호출자는 결과와 관계없이 `TakeDamage`를 다시 부르지 않는다 | 플레이어 + 적·투사체(확정안으로 전달) | 구현 완료 |
| 2026-10-14 | 강화 공격 발동 시점(잠정) | 투사체 제거·광역 피해는 연출 타이밍에 맞추며 강화 공격의 타격 순간에 가깝게 둔다. 정확한 시점은 연출과 함께 정한다 | 플레이어 + 적·투사체 | 계획서 권장안(좌클릭 수락 순간)과 다르다. 타격 전 공격이 취소될 때의 처리를 함께 정해야 한다 |
| 2026-10-14 | T_ready | 좌클릭 입력 대기 제한으로만 쓰며 일시정지를 제외한 실시간으로 잰다. 수락 뒤에는 강화 공격이 끝날 때까지 불릿 타임을 유지한다 | 플레이어 + HUD·통합(확인 필요) | |
| 2026-10-14 | 강화 준비 중 피격·회피(잠정) | 준비 상태를 유지한다. 만료나 사용으로만 끝난다 | 플레이어 + HUD·통합 | 이후 조정 가능 |
| 2026-10-14 | 강화 대상 없음(잠정) | 적과 적대 투사체가 없어도 좌클릭하면 강화 공격을 발동한다 | 플레이어 + 적·투사체 | 이후 조정 가능 |
| 2026-10-14 | 불릿 타임에서 느려지는 대상 | 세계와 플레이어 모두 느리되 플레이어는 덜 느리다. 두 배율은 Inspector에서 조정한다 | 전원(확인 필요) | 플레이어 이동·Animator·공격 판정·무적 타이머가 같은 플레이어 시간을 쓰도록 바꿔야 한다 |
| 2026-10-14 | 입력 거부 사유 | 1차에서 추가한다 | 플레이어 단독 | |
| 2026-10-14 | 퍼펙트 파동 | 퍼펙트 패링·극한 회피 때 플레이어 주변에 파동이 퍼진다. 퍼짐은 불릿 타임 영향 없음, 실루엣 페이드는 불릿 타임 영향 받음 | 플레이어(사건) + 시야·월드 표현(구현) + HUD·통합(시간 연동), 확인 필요 | 위 "퍼펙트 파동과 불릿 타임" 절. 강화 공격 발동 시 파동은 연출 때 결정 |
| 2026-10-14 | 웅크리기 입력 | 웅크리기 입력 전부(Ctrl 유지, C 토글, 패드 동쪽 버튼 토글)를 제외한다 | 플레이어 단독 | `InputManager`의 웅크리기 입력 처리 제거 |

#### 1차 구현 중 결정할 항목

다른 담당자와의 계약이 아닌 플레이어 내부 입력 처리이므로 전투 구현 때 실제 조작을 보며 정한다.

- 우클릭을 누르고 있는 동안의 가드 재진입: 현재 `InputManager.Update`는 우클릭을 누르고 있고, 가드 중이 아니며, 상태가 Free이면 매 프레임 `BeginDefense`를 다시 호출해 가드와 패링 창을 새로 연다. 이 조건은 누르고 있는 중에 가드가 끝나거나 아직 시작되지 못한 경우에 생긴다. 예: 가드 중 좌클릭 공격 후 공격이 끝남, 가드 중 막지 못한 피격(전방 120도 밖, 현재의 돌진), 누른 시점에 돌진·회복 중이라 가드를 시작하지 못함. 사용자 방향: 가드 중 공격한 뒤에는 우클릭을 새로 누른 입력에 패링 판정을 준다.
- 가드 중 막지 못한 피격의 가드 해제: 현재 `PlayerHealth.TakeDamage` → `PlayerController.NotifyHitReaction`이 가드를 끝낸다. 사용자 방향: 피격으로 가드가 풀리지 않게 하는 쪽을 검토한다.

#### 결정 대기 (0-6)

| 항목 | 현재 코드의 가정 | 결정할 담당 |
| --- | --- | --- |
| AttackId 발급 소유자와 세션 초기화 | 미정. 0이면 미부여 | 적·투사체, HUD·통합 |
| 계약 타입 파일 위치 | `PlayerContracts.cs`(플레이어 소유) | 플레이어, HUD·통합 |
| 투사체 자동 반격 | `Kind = Projectile`이면 없음(잠정). 투사체 패링 연출이 바뀔 수 있어 확정안이 아니다. 현재 `Projectile`을 넘기는 호출부가 없어 동작 영향은 없다 | 플레이어, 적·투사체 |
| 시간 요청 인터페이스 | 위 0-5 요청 사항 | HUD·통합 |

#### Kind 값

| 값 | 의미 |
| --- | --- |
| Unknown | 미부여. 기존 생성자 호출 |
| Melee | 적 근접 베기 |
| Charge | 적 돌진 접촉. 근접 베기와 같은 방어 대상으로 결정(2026-10-14, 적·투사체 확인 필요) |
| Projectile | 수리검·원거리 투사체 |
| PlayerOwned | 플레이어 칼날·검기·반사 투사체가 적에게 준 피해 |

#### 관련 코드

| 파일 경로 | 클래스 / 메서드 | 역할 |
| --- | --- | --- |
| Assets/Scripts/Combat/Contracts/DefenseResolution.cs | IncomingAttackKind | 공통 공격 종류 |
| Assets/Scripts/Combat/DamageContext.cs | DamageContext(5인자) | 기존 호출 호환. 식별값 0, Unknown |
| Assets/Scripts/Combat/DamageContext.cs | DamageContext(8인자) | 공격 식별값과 종류를 포함한 문맥 생성 |
| Assets/Scripts/Combat/Contracts/DefenseResolution.cs | DefenseOutcome, DefenseResolution | 공통 방어 결과 값 |
| Assets/Scripts/Player/PlayerContracts.cs | IPlayerDefenseResolver, PlayerCombatEvents | A 소유 resolver와 전투 사건 |
| Assets/Scripts/Player/PlayerContracts.cs | PlayerEmpowerEvent, PlayerDodgeEvent, PlayerStaminaEvent, PlayerRevealPulseEvent, PlayerInputRecord와 단계·원인·사유 열거형 | 플레이어 전투 사건 값 |
| Assets/Scripts/Player/PlayerContracts.cs | PlayerCombatEvents / Raise* | 사건 묶음과 발행 메서드 |
| Assets/Scripts/Player/KatanaV2/KatanaCombatCoordinator.cs | Events, GuardInputId, HasBufferedDefense, BeginDefense(inputId), StartDefense | 사건 묶음 제공, 가드를 연 입력 ID 확정, 예약 가드 여부 제공 |
| Assets/Scripts/GameManagers/InputManager.cs | RequestDefense, RequestAttack, RequestDodge, ResolveBufferedInputs, PublishInput, Record*Press/Release | 입력 ID 발급, 장치 입력 시각 기록, 처리 결과와 예약 입력 후속 결과 발행 |
| Assets/Scripts/Player/KatanaV2/KatanaDamageReceiver.cs | ResolveIncomingAttack | Kind로 방어 자격 선택, 가드·패링 판정, 실패 시 체력 감소, 결과 반환 |
| Assets/Scripts/Player/KatanaV2/KatanaDamageReceiver.cs | TakeDamage, TryDefendProjectile | 기존 호출 호환 경로 |
| Assets/Scripts/Player/KatanaV2/KatanaDamageReceiver.cs | IsFrontProjectile, PublishResolution | 투사체 진입 방향 검사, 결과 생성과 DefenseResolved 발행 |

#### 생성 위치와 전환 대상

| 위치 | 용도 | 담당 |
| --- | --- | --- |
| Assets/Scripts/Enemies/Attack/EnemyMeleeAttack.cs / TryHit | 근접 베기 | 적·투사체 |
| Assets/Scripts/Enemies/Attack/EnemyChargeHitbox.cs / TryApplyDamage | 돌진 | 적·투사체 |
| Assets/Scripts/Defense/DefenseShuriken.cs / Launch, ProcessHit | 세션 공격/투사체 ID 발급과 단일 resolver 호출 | B |
| Assets/Scripts/Defense/DefenseSwordWave.cs | 검기 → 적 | 적·투사체 |
| Assets/Scripts/Enemies/EnemyCombatService.cs | 플레이어 공격 → 적 | 적·투사체 |
| BossController, BeastBossAttack, TwinBossProjectile, EnemyRangedAttack, EnemyProjectile | 기존 모드 보스·원거리 | 전환 대상 아님 |

- 반사 시 문맥을 다시 만드는 곳(`EnemyProjectile`, `TwinBossProjectile`)은 새 값을 함께 복사해야 ID가 유지된다. 현재는 기존 생성자를 사용하므로 0으로 초기화된다.

#### 연결 및 협업

- 연결된 기능: [카타나 전투 V2](카타나_전투_V2.md), [플레이어 피격 및 체력](플레이어_피격_체력.md), [적 공격 패턴](적_공격_패턴.md), [적 전투 및 체력](적_전투_체력.md), [대숲 방어](대숲_방어.md).
- 공유하는 코드 및 상태: `DamageContext`는 적 → 플레이어, 플레이어 → 적 피해에 모두 사용한다. 기존 5인자 생성자를 유지하고 새 공통 enum/result type을 참조한다.
- 중복 또는 충돌 사항: `DefenseOutcome`·`DefenseResolution`·`IncomingAttackKind`를 공통 namespace로 통합했다. BambooDefense 수리검은 중앙 세션 발급기를 사용하지만 레거시 공격 호출부는 아직 Unknown/0이다.
- 미확인 및 결정 대기: 일반 패링·회피 무적·스태미너 동작과 강화 시간 요청은 후속 기능이다. 적 재사용 세대는 `EnemyHealth.SpawnGeneration`에 기록한다.
- 승인 필요 사항 및 결과: 2026-10-14 사용자 요청으로 0-1~0-4를 진행하고 0-6의 플레이어 담당 결정을 기록했다. 0-5 인터페이스는 HUD·통합 담당 배포 대기이며 사용자가 통보한다. 다른 담당자 확인이 필요한 항목은 결정 표의 결정 범위와 결정 대기 표에 표시했다.
