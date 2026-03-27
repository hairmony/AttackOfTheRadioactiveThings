using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
//public class AttackEvent : UnityEvent<float> { }

/// <summary>
/// The enemy brain. Owns the 4-state machine (Idle / Suspicious / Search / Chase)
/// and drives EnemyVisionCone and EnemyMovement — which know nothing about state.
///
///  IDLE        Passive. Vision cone is calm red.
///
///  SUSPICIOUS  Player is visible. Alert timer builds. Cone flashes orange.
///              Guard turns slowly toward the player.
///              → Player hides before timer fills  : enters SEARCH
///              → Timer fills                      : enters CHASE
///
///  SEARCH      Player was lost. Guard walks to the last-known position, then
///              sweeps its cone. Cone turns yellow.
///              → Player re-spotted                : back to SUSPICIOUS
///              → Search timer runs out            : returns to IDLE
///
///  CHASE       Full-speed pursuit + periodic attacks.
/// </summary>
[RequireComponent(typeof(EnemyVision))]
[RequireComponent(typeof(EnemyMovement))]
public class EnemyAI : MonoBehaviour
{
    // ── State ────────────────────────────────────────────────────────────────
    public enum State { Idle, Suspicious, Search, Chase }

    // ── Suspicious ───────────────────────────────────────────────────────────
    [Header("Suspicious")]
    [Tooltip("Seconds the player must remain visible before a chase triggers.")]
    [SerializeField] private float suspiciousDuration = 2f;
    [SerializeField] private float coneFlashFrequency = 8f;

    // ── Search ───────────────────────────────────────────────────────────────
    [Header("Search")]
    [Tooltip("Seconds spent searching before giving up and returning to Idle.")]
    [SerializeField] private float searchDuration = 5f;

    // ── Chase & Attack ───────────────────────────────────────────────────────
    [Header("Chase & Attack")]
    [SerializeField] private float attackRange = 0.65f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float attackInterval = 1f;

    // ── Optional ─────────────────────────────────────────────────────────────
    [Header("Optional")]
    [Tooltip("Disabled when the guard becomes Suspicious; re-enabled on return to Idle.")]
    [SerializeField] private MonoBehaviour patrolBehaviour;

    // ── Events ───────────────────────────────────────────────────────────────
    [Header("Events")]
    public UnityEvent onEnterSuspicious;
    public UnityEvent onEnterSearch;
    public UnityEvent onEnterChase;
    public UnityEvent onReturnIdle;
    //public AttackEvent onAttackPlayer = new AttackEvent();

    // ── Runtime ──────────────────────────────────────────────────────────────
    private EnemyVision _vision;
    private EnemyMovement _movement;

    private State _state = State.Idle;
    private float _suspiciousTimer;
    private float _searchTimer;
    private Vector2 _lastKnownPos;
    private float _attackCooldown;
    private Coroutine _flashRoutine;

    public State CurrentState => _state;

    // ────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _vision = GetComponent<EnemyVision>();
        _movement = GetComponent<EnemyMovement>();
    }

    private void Update()
    {
        switch (_state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Suspicious: UpdateSuspicious(); break;
            case State.Search: UpdateSearch(); break;
            case State.Chase: UpdateChase(); break;
        }
    }

    private void FixedUpdate()
    {
        switch (_state)
        {
            case State.Suspicious:
                _movement.RotateSuspicious(_vision.PlayerTransform.position);
                break;

            case State.Search:
                _movement.SearchMove(_lastKnownPos);
                break;

            case State.Chase:
                _movement.ChasePlayer(_vision.PlayerTransform.position);
                break;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Per-state Update logic
    // ────────────────────────────────────────────────────────────────────────

    private void UpdateIdle()
    {
        if (_vision.CanSeePlayer)
            EnterSuspicious();
    }

    private void UpdateSuspicious()
    {
        if (!_vision.CanSeePlayer)
        {
            EnterSearch();
            return;
        }

        _suspiciousTimer += Time.deltaTime;
        if (_suspiciousTimer >= suspiciousDuration)
            EnterChase();
    }

    private void UpdateSearch()
    {
        if (_vision.CanSeePlayer)
        {
            EnterSuspicious();    // re-spotted — suspicious timer resets
            return;
        }

        _searchTimer -= Time.deltaTime;
        if (_searchTimer <= 0f)
            EnterIdle();
    }

    private void UpdateChase()
    {
        if (_vision.PlayerTransform == null) return;

        if (_attackCooldown > 0f)
            _attackCooldown -= Time.deltaTime;

        float dist = Vector2.Distance(transform.position, _vision.PlayerTransform.position);
        if (dist <= attackRange && _attackCooldown <= 0f)
        {
            _attackCooldown = attackInterval;
            //onAttackPlayer?.Invoke(attackDamage);
        }
    }
    // ────────────────────────────────────────────────────────────────────────
    //  State transitions
    // ────────────────────────────────────────────────────────────────────────

    private void EnterIdle()
    {
        _state = State.Idle;
        StopFlash();
        _vision.SetConeColor(_vision.CalmConeColor);

        if (patrolBehaviour != null)
            patrolBehaviour.enabled = true;

        onReturnIdle?.Invoke();
    }

    private void EnterSuspicious()
    {
        bool fromIdle = _state == State.Idle;
        _state = State.Suspicious;
        _suspiciousTimer = 0f;

        if (fromIdle && patrolBehaviour != null)
            patrolBehaviour.enabled = false;

        StartFlash();
        onEnterSuspicious?.Invoke();
    }

    private void EnterSearch()
    {
        _state = State.Search;
        _lastKnownPos = _vision.PlayerTransform.position;
        _searchTimer = searchDuration;

        _movement.BeginSearch();
        StopFlash();
        _vision.SetConeColor(_vision.SearchConeColor);

        onEnterSearch?.Invoke();
    }

    private void EnterChase()
    {
        _state = State.Chase;
        StopFlash();
        _vision.SetConeColor(_vision.CalmConeColor);

        if (patrolBehaviour != null)
            patrolBehaviour.enabled = false;

        onEnterChase?.Invoke();
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Cone flash coroutine
    // ────────────────────────────────────────────────────────────────────────

    private void StartFlash()
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private void StopFlash()
    {
        if (_flashRoutine == null) return;
        StopCoroutine(_flashRoutine);
        _flashRoutine = null;
    }

    private IEnumerator FlashRoutine()
    {
        while (_state == State.Suspicious)
        {
            float t = (Mathf.Sin(Time.time * coneFlashFrequency) + 1f) * 0.5f;
            _vision.SetConeColor(Color.Lerp(_vision.CalmConeColor, _vision.SuspiciousFlashColor, t));
            yield return null;
        }
        _flashRoutine = null;
    }
}