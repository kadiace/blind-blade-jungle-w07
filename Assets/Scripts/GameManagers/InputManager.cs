using System;

using UnityEngine;

using UnityEngine.InputSystem;

/// <summary>현재 플레이어의 실제 입력과 게임플레이 입력 허용 정책을 관리한다.</summary>
[DefaultExecutionOrder(-40)]
public sealed class InputManager : MonoBehaviour
{
    [Header("커서")]
    [SerializeField] private bool _lockCursorOnClick = true;

    [Header("패드 시점")]
    [SerializeField, Min(1f)] private float _gamepadLookDegreesPerSecond = 180f;

    [Header("입력 상태")]
    private GameStateManager _state;
    private KatanaCombatCoordinator _combat;
    private PlayerMotor _motor;
    private InputSystem_Actions _actions;
    private InputAction _crouchHold;
    private InputAction _crouchToggle;
    private InputAction _guard;
    private InputAction _dodge;
    private bool _crouchToggled;
    private int _nextInputId;
    private int _guardInputId;
    private double _attackPressTime;
    private double _guardPressTime;
    private double _guardReleaseTime;
    private double _dodgePressTime;
    private int _bufferedAttackInputId;
    private int _bufferedAttackActionId;
    private double _bufferedAttackPressTime;
    private int _bufferedGuardInputId;
    private double _bufferedGuardPressTime;

    public bool IsInitialized => _state != null;
    public bool IsGameplayInputEnabled { get; private set; }
    public event Action<bool> GameplayInputEnabledChanged;

    void Awake()
    {
        _actions = new InputSystem_Actions();
        _crouchHold = new InputAction("CrouchHold", InputActionType.Button);
        _crouchHold.AddBinding("<Keyboard>/leftCtrl");
        _crouchHold.AddBinding("<Keyboard>/rightCtrl");
        _crouchToggle = new InputAction("CrouchToggle", InputActionType.Button, "<Keyboard>/c");
        _guard = new InputAction("Guard", InputActionType.Button, "<Mouse>/rightButton");
        _dodge = new InputAction("Dodge", InputActionType.Button, "<Keyboard>/space");
        _crouchToggle.AddBinding("<Gamepad>/buttonEast");
        _guard.AddBinding("<Gamepad>/leftShoulder");
        _dodge.AddBinding("<Gamepad>/buttonSouth");
        InputAction attack = _actions.Player.Attack;
        for (int i = 0; i < attack.bindings.Count; i++)
            if (attack.bindings[i].path.StartsWith("<Gamepad>"))
                attack.ApplyBindingOverride(i, "<Gamepad>/rightShoulder");

        // 프레임 단위 폴링보다 정확한 장치 입력 시각을 기록용으로만 보관한다.
        attack.performed += RecordAttackPress;
        _guard.performed += RecordGuardPress;
        _guard.canceled += RecordGuardRelease;
        _dodge.performed += RecordDodgePress;
    }

    void Update()
    {
        InputDeviceDisplay.PollButtons();
        PlayerController player = Game.Player;
        if (!IsGameplayInputEnabled || player == null || player.IsPaused) return;
        if (InputDeviceDisplay.CancelPressed)
        {
            ClearHeldInput();
            SetCursor(false);
            return;
        }
        if (!Application.isFocused) return;
        if (_lockCursorOnClick && !InputDeviceDisplay.IsGamepad && Cursor.lockState != CursorLockMode.Locked)
        {
            if (_actions.Player.Attack.WasPressedThisFrame() || _guard.WasPressedThisFrame()) SetCursor(true);
            return;
        }
        if (_crouchToggle.WasPressedThisFrame()) _crouchToggled = !_crouchToggled;
        player.SetCrouchInput(_crouchHold.IsPressed() || _crouchToggled);
        player.SetMovementInput(_actions.Player.Move.ReadValue<Vector2>());
        Vector2 look = _actions.Player.Look.ReadValue<Vector2>();
        if (_actions.Player.Look.activeControl != null && _actions.Player.Look.activeControl.device is Gamepad)
            _motor.AddGamepadLookInput(look, _gamepadLookDegreesPerSecond, Time.deltaTime);
        else
            player.AddLookInput(look);

        ResolveBufferedInputs(player);

        // 새 우클릭은 공격을 가드로 전환하고, 계속 누르는 우클릭은 공격 완료 뒤 가드로 복귀시킨다.
        if (_guard.WasPressedThisFrame() && !player.IsGuarding)
        {
            _guardInputId = ++_nextInputId;
            RequestDefense(player, PlayerInputKind.GuardPress);
        }
        else if (_guard.IsPressed() && !player.IsGuarding
            && player.CurrentState == PlayerActionState.Free)
            RequestDefense(player, PlayerInputKind.GuardHoldResume);

        if (_guard.WasReleasedThisFrame())
        {
            _combat.EndDefense();
            PublishInput(_guardInputId, PlayerInputKind.GuardRelease, PlayerInputStatus.Started, 0, _guardReleaseTime);
        }
        if (_dodge.WasPressedThisFrame()) RequestDodge(player);
        if (_actions.Player.Attack.WasPressedThisFrame()) RequestAttack(player);
    }

    /// <summary>
    /// 현재 우클릭 _guardInputId로 방어를 요청하고 가드 시작·예약·거부 결과를 kind 입력 기록으로 발행한다.
    /// 누르고 있는 입력의 재진입은 매 프레임 거부될 수 있으므로 가드가 실제로 시작된 경우에만 기록한다.
    /// </summary>
    private void RequestDefense(PlayerController player, PlayerInputKind kind)
    {
        bool accepted = _combat.BeginDefense(_guardInputId);
        PlayerInputStatus status = player.IsGuarding ? PlayerInputStatus.Started
            : accepted ? PlayerInputStatus.Buffered : PlayerInputStatus.Rejected;
        if (kind == PlayerInputKind.GuardHoldResume && status != PlayerInputStatus.Started) return;
        if (status == PlayerInputStatus.Buffered && _combat.HasBufferedDefense)
        {
            _bufferedGuardInputId = _guardInputId;
            _bufferedGuardPressTime = _guardPressTime;
        }
        PublishInput(_guardInputId, kind, status, 0, _guardPressTime);
    }

    /// <summary>
    /// 새 InputId로 공격을 요청하고 행동 ID 변화로 즉시 시작·예약·거부를 구분해 입력 기록으로 발행한다.
    /// 즉시 시작된 경우에만 player의 새 행동 ID를 기록한다.
    /// </summary>
    private void RequestAttack(PlayerController player)
    {
        int inputId = ++_nextInputId;
        int previousActionId = player.CurrentActionId;
        bool accepted = _combat.RequestAttack();
        bool started = accepted && player.CurrentActionId != previousActionId;
        PlayerInputStatus status = started ? PlayerInputStatus.Started
            : accepted ? PlayerInputStatus.Buffered : PlayerInputStatus.Rejected;
        if (status == PlayerInputStatus.Buffered && _combat.HasBufferedAttack)
        {
            _bufferedAttackInputId = inputId;
            _bufferedAttackActionId = player.CurrentActionId;
            _bufferedAttackPressTime = _attackPressTime;
        }
        PublishInput(inputId, PlayerInputKind.Attack, status, started ? player.CurrentActionId : 0, _attackPressTime);
    }

    /// <summary>
    /// 예약된 공격·가드 입력이 Coordinator에서 처리됐으면 같은 InputId로 Started 또는 BufferDropped 후속 기록을 발행한다.
    /// 공격은 예약 당시와 player의 행동 ID가 다르면 시작, 가드는 GuardInputId가 같으면 시작으로 보며 판정 상태는 바꾸지 않는다.
    /// </summary>
    private void ResolveBufferedInputs(PlayerController player)
    {
        if (_bufferedAttackInputId != 0 && !_combat.HasBufferedAttack)
        {
            bool started = player.CurrentActionId != _bufferedAttackActionId;
            PublishInput(_bufferedAttackInputId, PlayerInputKind.Attack,
                started ? PlayerInputStatus.Started : PlayerInputStatus.BufferDropped,
                started ? player.CurrentActionId : 0, _bufferedAttackPressTime);
            _bufferedAttackInputId = 0;
        }

        if (_bufferedGuardInputId != 0 && !_combat.HasBufferedDefense)
        {
            bool started = _combat.GuardInputId == _bufferedGuardInputId;
            PublishInput(_bufferedGuardInputId, PlayerInputKind.GuardPress,
                started ? PlayerInputStatus.Started : PlayerInputStatus.BufferDropped, 0, _bufferedGuardPressTime);
            _bufferedGuardInputId = 0;
        }
    }

    /// <summary>새 InputId로 player의 회피를 요청하고 시작 또는 거부 결과를 입력 기록으로 발행한다.</summary>
    private void RequestDodge(PlayerController player)
    {
        int inputId = ++_nextInputId;
        bool accepted = player.RequestDodge();
        PublishInput(inputId, PlayerInputKind.Dodge,
            accepted ? PlayerInputStatus.Started : PlayerInputStatus.Rejected, 0, _dodgePressTime);
    }

    /// <summary>
    /// inputId·kind·status·actionId와 장치 입력 시각 deviceTime, 현재 프레임의 실시간·게임 시간으로 입력 기록을 만든다.
    /// 만든 기록을 Coordinator의 InputResolved 사건으로 발행하며 입력 처리 결과는 바꾸지 않는다.
    /// </summary>
    private void PublishInput(int inputId, PlayerInputKind kind, PlayerInputStatus status, int actionId, double deviceTime)
    {
        _combat.Events.RaiseInputResolved(new PlayerInputRecord(inputId, kind, status, actionId,
            deviceTime, Time.realtimeSinceStartupAsDouble, Time.time));
    }

    /// <summary>context의 장치 입력 시각을 마지막 공격 누름 시각으로 저장한다.</summary>
    private void RecordAttackPress(InputAction.CallbackContext context) => _attackPressTime = context.time;

    /// <summary>context의 장치 입력 시각을 마지막 우클릭 누름 시각으로 저장한다.</summary>
    private void RecordGuardPress(InputAction.CallbackContext context) => _guardPressTime = context.time;

    /// <summary>context의 장치 입력 시각을 마지막 우클릭 뗌 시각으로 저장한다.</summary>
    private void RecordGuardRelease(InputAction.CallbackContext context) => _guardReleaseTime = context.time;

    /// <summary>context의 장치 입력 시각을 마지막 회피 누름 시각으로 저장한다.</summary>
    private void RecordDodgePress(InputAction.CallbackContext context) => _dodgePressTime = context.time;

    void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        ClearHeldInput();
        SetCursor(false);
    }

    void OnDisable()
    {
        Shutdown();
    }

    void OnDestroy()
    {
        Shutdown();
        _actions.Dispose();
        _crouchHold.Dispose();
        _crouchToggle.Dispose();
        _guard.Dispose();
        _dodge.Dispose();
    }

    /// <summary>state와 Game.Player의 장치를 연결한다. 초기 입력은 차단하고 Playing 전환을 기다린다.</summary>
    public void Initialize(GameStateManager state)
    {
        Shutdown();
        _state = state;
        _combat = Game.Player.GetComponent<KatanaCombatCoordinator>();
        _motor = Game.Player.GetComponent<PlayerMotor>();
        _state.StateChanged += HandleState;
        _actions.Player.Enable();
        _crouchHold.Enable();
        _crouchToggle.Enable();
        _guard.Enable();
        _dodge.Enable();
    }

    /// <summary>state가 Playing이면 입력을 허용하고 그 외에는 명령 전달과 유지 입력을 차단한다.</summary>
    private void HandleState(GameplayState state)
    {
        SetGameplayInputEnabled(state == GameplayState.Playing);
    }

    /// <summary>allowed와 게임 상태로 입력 허용 여부를 결정한다. 차단 시 유지 입력을 정리하고 활성화 시 커서를 잠근 뒤 정책 변경을 알린다.</summary>
    public void SetGameplayInputEnabled(bool allowed)
    {
        bool enabled = allowed && _state != null && _state.State == GameplayState.Playing;
        if (!enabled) ClearHeldInput();
        if (IsGameplayInputEnabled == enabled) return;
        IsGameplayInputEnabled = enabled;
        if (enabled) SetCursor(true);
        GameplayInputEnabledChanged?.Invoke(enabled);
    }

    /// <summary>Game.Player의 이동·웅크리기·가드·차지·패링 시도를 정리하고 입력 토글과 모터 입력을 초기화한다.</summary>
    private void ClearHeldInput()
    {
        _crouchToggled = false;
        PlayerController player = Game.Player;
        if (player == null) return;
        player.SetMovementInput(Vector2.zero);
        player.SetCrouchInput(false);
        player.EndGuard();
        player.EndCharge();
        if (_combat != null) _combat.CancelParryAttempt();
        if (_motor != null) _motor.ClearInput();
    }

    /// <summary>locked와 Inspector 설정으로 커서 잠금·표시 상태를 변경한다. 잠금 관리가 꺼져 있으면 상태를 유지한다.</summary>
    private void SetCursor(bool locked)
    {
        if (!_lockCursorOnClick) return;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>튜토리얼 재개에 사용한 command를 복구된 플레이어에 한 번 전달한다. 회피 방향은 현재 이동 입력을 사용한다.</summary>
    public void ApplyTutorialInput(TutorialResumeInput command)
    {
        PlayerController player = Game.Player;
        if (!IsGameplayInputEnabled || player == null || player.IsPaused || !Application.isFocused) return;
        SetCursor(true);
        player.SetMovementInput(_actions.Player.Move.ReadValue<Vector2>());
        switch (command)
        {
            case TutorialResumeInput.Attack:
                _combat.RequestAttack();
                break;
            case TutorialResumeInput.Defense:
                _combat.BeginDefense();
                break;
            case TutorialResumeInput.Dodge:
                player.RequestDodge();
                break;
            case TutorialResumeInput.Crouch:
                player.SetCrouchInput(true);
                break;
        }
    }

    /// <summary>입력 액션과 상태 구독·플레이어 장치 참조를 해제한다. 매니저는 다음 씬에서 재사용한다.</summary>
    public void Shutdown()
    {
        SetGameplayInputEnabled(false);
        if (_state != null) _state.StateChanged -= HandleState;
        _state = null;
        _combat = null;
        _motor = null;
        // 매니저는 다음 씬에서도 재사용되므로 이전 플레이어의 예약 입력 추적을 남기지 않는다.
        _bufferedAttackInputId = 0;
        _bufferedGuardInputId = 0;
        if (_actions != null) _actions.Player.Disable();
        if (_crouchHold != null) _crouchHold.Disable();
        if (_crouchToggle != null) _crouchToggle.Disable();
        if (_guard != null) _guard.Disable();
        if (_dodge != null) _dodge.Disable();
        SetCursor(false);
    }
}
