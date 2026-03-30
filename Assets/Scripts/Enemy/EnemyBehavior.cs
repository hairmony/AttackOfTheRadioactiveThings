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
///  SUSPICIOUS  Player is visible. Alert timer builds. Cone flashes orange. Random from playerSpottedClips.
///              Guard turns slowly toward the player.
///              → Player hides before timer fills  : enters SEARCH
///              → Timer fills                      : enters CHASE
///
///  SEARCH      Player was lost. Guard walks to the last-known position, then
///              sweeps its cone. Cone turns yellow. Random chaseEndClips if search started after CHASE.
///              → Player re-spotted                : back to SUSPICIOUS (optional reSpottedAfterChaseClips)
///              → Search timer runs out            : returns to IDLE
///
///  CHASE       Full-speed pursuit + periodic attacks. Vision cone uses ChaseConeColor (strong red).
///              Optional chaseStartClips. If the player stays outside the cone for chaseLoseSightDuration, returns to SEARCH.
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
    [Tooltip("In Chase: if the player is outside the vision cone this long, enemy goes to Search.")]
    [SerializeField] private float chaseLoseSightDuration = 10f;

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

    // ── Audio ─────────────────────────────────────────────────────────────────
    [Header("Audio")]
    [Tooltip("Played when the player is spotted (Suspicious). Multiple = random each time.")]
    [SerializeField] private AudioClip[] playerSpottedClips;
    [Tooltip("When chase begins. Multiple = random.")]
    [SerializeField] private AudioClip[] chaseStartClips;
    [Tooltip("When chase ends (→ Search after cone timeout). Multiple = random.")]
    [SerializeField] private AudioClip[] chaseEndClips;
    [Tooltip("Re-spotted during Search that followed a chase. If none, uses Player Spotted Clips.")]
    [SerializeField] private AudioClip[] reSpottedAfterChaseClips;
    [Tooltip("Optional: play through this source. If unset, uses GameAudio SFX bus.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] [Range(0f, 1f)] private float spottedVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float chaseStartVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float chaseEndVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float reSpottedAfterChaseVolume = 1f;

    // ── Runtime ──────────────────────────────────────────────────────────────
    private EnemyVision _vision;
    private EnemyMovement _movement;

    private State _state = State.Idle;
    private float _suspiciousTimer;
    private float _searchTimer;
    private Vector2 _lastKnownPos;
    private float _attackCooldown;
    private float _chaseLoseSightTimer;
    private bool _searchFollowedChase;
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

        if (_vision.CanSeePlayer)
            _chaseLoseSightTimer = 0f;
        else
        {
            _chaseLoseSightTimer += Time.deltaTime;
            if (_chaseLoseSightTimer >= chaseLoseSightDuration)
            {
                _chaseLoseSightTimer = 0f;
                EnterSearch();
                return;
            }
        }

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
        _searchFollowedChase = false;
        StopFlash();
        _vision.SetConeColor(_vision.CalmConeColor);

        if (patrolBehaviour != null)
            patrolBehaviour.enabled = true;

        onReturnIdle?.Invoke();
    }

    private void EnterSuspicious()
    {
        bool fromIdle = _state == State.Idle;
        bool fromSearchAfterChase = _state == State.Search && _searchFollowedChase;

        _state = State.Suspicious;
        _suspiciousTimer = 0f;
        _searchFollowedChase = false;

        if (fromIdle && patrolBehaviour != null)
            patrolBehaviour.enabled = false;

        StartFlash();
        if (fromSearchAfterChase && HasAnyClip(reSpottedAfterChaseClips))
            PlayRandomOneShot(reSpottedAfterChaseClips, reSpottedAfterChaseVolume);
        else
            PlayRandomOneShot(playerSpottedClips, spottedVolume);
        onEnterSuspicious?.Invoke();
    }

    private void EnterSearch()
    {
        bool fromChase = _state == State.Chase;
        _state = State.Search;
        _searchFollowedChase = fromChase;
        _lastKnownPos = _vision.PlayerTransform.position;
        _searchTimer = searchDuration;

        _movement.BeginSearch();
        StopFlash();
        _vision.SetConeColor(_vision.SearchConeColor);

        if (fromChase)
            PlayRandomOneShot(chaseEndClips, chaseEndVolume);

        onEnterSearch?.Invoke();
    }

    private void EnterChase()
    {
        _state = State.Chase;
        _searchFollowedChase = false;
        _chaseLoseSightTimer = 0f;
        StopFlash();
        _vision.SetConeColor(_vision.ChaseConeColor);

        if (patrolBehaviour != null)
            patrolBehaviour.enabled = false;

        PlayRandomOneShot(chaseStartClips, chaseStartVolume);
        onEnterChase?.Invoke();
    }

    private static bool HasAnyClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null) return true;
        }
        return false;
    }

    private void PlayRandomOneShot(AudioClip[] clips, float volume)
    {
        if (!HasAnyClip(clips)) return;
        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null) count++;
        }
        int pick = Random.Range(0, count);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            if (pick-- == 0)
            {
                PlayOneShot(clips[i], volume);
                return;
            }
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (audioSource != null)
        {
            if (audioSource.outputAudioMixerGroup == null && GameAudio.SfxOutputGroup != null)
                audioSource.outputAudioMixerGroup = GameAudio.SfxOutputGroup;
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            return;
        }
        GameAudio.PlaySfx(clip, transform.position, volume);
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