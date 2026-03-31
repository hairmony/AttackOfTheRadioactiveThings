using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Enemy brain: watcher/sprinter/bulwark. Sprinters summoned by a watcher use <see cref="EnemySprinterDeployment"/> + deploy/sweep/chase/leave (no Idle).
/// </summary>
[RequireComponent(typeof(EnemyVision))]
[RequireComponent(typeof(EnemyMovement))]
public class EnemyAI : MonoBehaviour
{
    public enum State
    {
        Idle, Suspicious, Search, Chase,
        SprinterDeploy, SprinterSweep, SprinterChase, SprinterLeave
    }

    [Header("Suspicious")]
    [SerializeField] private float suspiciousDuration = 2f;
    [SerializeField] private float coneFlashFrequency = 8f;

    [Header("Search")]
    [SerializeField] private float searchDuration = 5f;

    [Header("Chase & Attack")]
    [SerializeField] private float attackRange = 0.65f;
    [SerializeField] private float attackDamage = 1f;
    public float AttackDamage => attackDamage;
    [SerializeField] private float attackInterval = 1f;
    [Tooltip("Watcher/Bulwark chase: lose sight this long before Search.")]
    [SerializeField] private float chaseLoseSightDuration = 10f;

    [Header("Optional")]
    [SerializeField] private MonoBehaviour patrolBehaviour;

    [Header("Events")]
    public UnityEvent onEnterSuspicious;
    public UnityEvent onEnterSearch;
    public UnityEvent onEnterChase;
    public UnityEvent onReturnIdle;

    [Header("Audio")]
    [SerializeField] private AudioClip[] playerSpottedClips;
    [SerializeField] private AudioClip[] chaseStartClips;
    [SerializeField] private AudioClip[] chaseEndClips;
    [Tooltip("Played on Suspicious after the first time this guard has gone suspicious (any route).")]
    [FormerlySerializedAs("reSpottedAfterChaseClips")]
    [SerializeField] private AudioClip[] reSuspiciousClips;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] [Range(0f, 1f)] private float spottedVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float chaseStartVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float chaseEndVolume = 1f;
    [FormerlySerializedAs("reSpottedAfterChaseVolume")]
    [SerializeField] [Range(0f, 1f)] private float reSuspiciousVolume = 1f;

    private EnemyVision _vision;
    private EnemyMovement _movement;
    private EnemyArchetype _archetype;

    private State _state = State.Idle;
    private float _suspiciousTimer;
    private float _searchTimer;
    private Vector2 _lastKnownPos;
    private float _attackCooldown;
    private float _chaseLoseSightTimer;
    private bool _searchFollowedChase;
    private Coroutine _flashRoutine;

    private bool _playedFirstSuspiciousSting;

    /// <summary>Watcher: sprinter roll uses count &gt;= 2 (first chase never spawns sprinters).</summary>
    private int _watcherChaseEnterCount;

    private bool _sprinterFlow;
    private Vector2 _sprInvestigateCenter;
    private Vector2 _sprDeployTarget;
    private Vector2 _sprLeaveTarget;
    private float _sprSweepRadius;
    private float _sprSweepTheta;
    private float _sprOffscreenMargin;
    private float _sprinterChaseBlindTimer;

    public State CurrentState => _state;

    private void Awake()
    {
        _vision = GetComponent<EnemyVision>();
        _movement = GetComponent<EnemyMovement>();
        TryGetComponent(out _archetype);
    }

    private void Start()
    {
        if (_archetype != null && _archetype.Kind == EnemyKind.Sprinter)
            TryBeginSprinterSummonedFlow();
    }

    private void TryBeginSprinterSummonedFlow()
    {
        var dep = GetComponent<EnemySprinterDeployment>();
        if (dep != null)
        {
            _sprinterFlow = true;
            _sprInvestigateCenter = dep.InvestigationCenter;
            _sprOffscreenMargin = dep.OffscreenMargin;
            _sprSweepRadius = dep.SweepOrbitRadius;
            PickSprinterDeployTarget(dep.ApproachRingMin, dep.ApproachRingMax);
            _sprSweepTheta = Random.Range(0f, Mathf.PI * 2f);
            _state = State.SprinterDeploy;
            Destroy(dep);
        }
        else
        {
            _sprinterFlow = true;
            _sprInvestigateCenter = transform.position;
            _sprSweepRadius = 2.5f;
            _sprOffscreenMargin = _archetype != null ? _archetype.OffscreenMarginWorld : 2f;
            _state = State.SprinterSweep;
            _sprSweepTheta = Random.Range(0f, Mathf.PI * 2f);
            StartFlash();
            _vision.SetConeColor(_vision.SuspiciousFlashColor);
        }

        if (patrolBehaviour != null)
            patrolBehaviour.enabled = false;
    }

    private void PickSprinterDeployTarget(float rMin, float rMax)
    {
        float ang = Random.Range(0f, Mathf.PI * 2f);
        float rad = Random.Range(rMin, rMax);
        _sprDeployTarget = _sprInvestigateCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
    }

    private void Update()
    {
        if (IsSprinterFlow())
        {
            UpdateSprinterFlowVisionSway();
            UpdateSprinterFlow();
            return;
        }

        RefreshVisionIdleSway();
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
        if (IsSprinterFlow())
        {
            FixedSprinterFlow();
            return;
        }

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

    private bool IsSprinterFlow() => _sprinterFlow;

    private void UpdateSprinterFlowVisionSway()
    {
        if (_state == State.SprinterSweep && _archetype != null)
        {
            _vision.SetIdleConeSway(true,
                _archetype.SprinterSweepConeSwayDegrees,
                _archetype.SprinterSweepConeSwaySpeed);
        }
        else
            _vision.SetIdleConeSway(false);
    }

    private void UpdateSprinterFlow()
    {
        switch (_state)
        {
            case State.SprinterDeploy:
                if (_vision.CanSeePlayer)
                    EnterSprinterChase();
                break;
            case State.SprinterSweep:
                if (_vision.CanSeePlayer)
                    EnterSprinterChase();
                break;
            case State.SprinterChase:
                UpdateSprinterChase();
                break;
            case State.SprinterLeave:
                if (OffScreenSpawn2D.IsBeyondView(transform.position, Camera.main, _sprOffscreenMargin))
                    Destroy(gameObject);
                break;
        }
    }

    private void FixedSprinterFlow()
    {
        float moveSpeed = _movement.chaseSpeed * _movement.searchSpeedMultiplier;
        float turn = _movement.suspiciousTurnSpeed;

        switch (_state)
        {
            case State.SprinterDeploy:
                if (Vector2.Distance((Vector2)transform.position, _sprDeployTarget)
                    <= (_archetype != null ? _archetype.SprinterDeployArriveDistance : 0.42f))
                    EnterSprinterSweep();
                else
                    _movement.WalkTowards(_sprDeployTarget, moveSpeed, turn);
                break;
            case State.SprinterSweep:
            {
                float w = _archetype != null ? _archetype.SprinterSweepAngularSpeed : 52f;
                _sprSweepTheta += w * Mathf.Deg2Rad * Time.fixedDeltaTime;
                Vector2 orbit = _sprInvestigateCenter
                    + new Vector2(Mathf.Cos(_sprSweepTheta), Mathf.Sin(_sprSweepTheta)) * _sprSweepRadius;
                _movement.WalkTowards(orbit, moveSpeed * 0.85f, turn);
                break;
            }
            case State.SprinterChase:
                if (_vision.PlayerTransform != null)
                {
                    _movement.ChasePlayerWithStaleTarget(
                        _vision.PlayerTransform.position,
                        _archetype != null ? _archetype.SprinterStaleChaseApproachPerSecond : 2.45f);
                }
                break;
            case State.SprinterLeave:
                _movement.WalkTowards(_sprLeaveTarget, _movement.chaseSpeed * 1.05f, turn * 1.1f);
                break;
        }
    }

    private void UpdateSprinterChase()
    {
        if (_vision.PlayerTransform == null) return;

        float blind = _archetype != null ? _archetype.SprinterChaseLoseSightDuration : 0.4f;
        if (_vision.CanSeePlayer)
            _sprinterChaseBlindTimer = 0f;
        else
        {
            _sprinterChaseBlindTimer += Time.deltaTime;
            if (_sprinterChaseBlindTimer >= blind)
                EnterSprinterLeave();
        }

        if (_attackCooldown > 0f)
            _attackCooldown -= Time.deltaTime;
        float dist = Vector2.Distance(transform.position, _vision.PlayerTransform.position);
        if (dist <= attackRange && _attackCooldown <= 0f)
            _attackCooldown = attackInterval;
    }

    private void EnterSprinterSweep()
    {
        _state = State.SprinterSweep;
        StartFlash();
        _vision.SetConeColor(_vision.SuspiciousFlashColor);
    }

    private void EnterSprinterChase()
    {
        StopFlash();
        _state = State.SprinterChase;
        _sprinterChaseBlindTimer = 0f;
        _vision.SetConeColor(_vision.ChaseConeColor);
        if (_vision.PlayerTransform != null)
            _movement.ResetSprinterStaleChase(_vision.PlayerTransform.position);
        PlayRandomOneShot(chaseStartClips, chaseStartVolume);
        onEnterChase?.Invoke();
    }

    private void EnterSprinterLeave()
    {
        StopFlash();
        _state = State.SprinterLeave;
        _vision.SetConeColor(_vision.SearchConeColor);
        _sprLeaveTarget = OffScreenSpawn2D.RandomBeyondView(Camera.main, _sprOffscreenMargin);
    }

    private void UpdateIdle()
    {
        if (PlayerIsDetected())
            EnterSuspicious();
    }

    private void UpdateSuspicious()
    {
        if (!PlayerIsDetected())
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
        if (PlayerIsDetected())
        {
            EnterSuspicious();
            return;
        }

        _searchTimer -= Time.deltaTime;
        if (_searchTimer <= 0f)
            EnterIdle();
    }

    private void UpdateChase()
    {
        if (_vision.PlayerTransform == null) return;

        if (PlayerIsDetected())
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
            _attackCooldown = attackInterval;
    }

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

        _state = State.Suspicious;
        _suspiciousTimer = 0f;
        _searchFollowedChase = false;

        if (fromIdle && patrolBehaviour != null)
            patrolBehaviour.enabled = false;

        StartFlash();
        if (!_playedFirstSuspiciousSting && HasAnyClip(playerSpottedClips))
        {
            PlayRandomOneShot(playerSpottedClips, spottedVolume);
            _playedFirstSuspiciousSting = true;
        }
        else if (HasAnyClip(reSuspiciousClips))
            PlayRandomOneShot(reSuspiciousClips, reSuspiciousVolume);
        else if (HasAnyClip(playerSpottedClips))
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
        if (_archetype != null && _archetype.Kind == EnemyKind.Watcher)
            TryWatcherReinforcementsOnEnterChase();
        onEnterChase?.Invoke();
    }

    private void TryWatcherReinforcementsOnEnterChase()
    {
        if (_archetype == null || _vision.PlayerTransform == null) return;

        _watcherChaseEnterCount++;
        _archetype.SpawnWatcherBulwarksOnEnterChase(transform);

        if (_watcherChaseEnterCount < 2) return;
        if (Random.value > _archetype.SprinterReinforceChanceAfterFirstCycle) return;
        _archetype.SpawnWatcherSprinterReinforcements(transform, _vision.PlayerTransform.position, Camera.main);
    }

    private bool PlayerIsDetected()
    {
        if (_archetype != null && _archetype.Kind == EnemyKind.Bulwark)
            return OmnidirectionalLosAware();
        return _vision.CanSeePlayer;
    }

    private bool OmnidirectionalLosAware()
    {
        if (_vision.PlayerTransform == null) return false;
        float d = Vector2.Distance(transform.position, _vision.PlayerTransform.position);
        if (d > _vision.GetOmniLosAwarenessDistance()) return false;
        return _vision.HasUnobstructedLineToPlayer();
    }

    private void RefreshVisionIdleSway()
    {
        if (_archetype != null && _archetype.IdleVisionConeSway && _state == State.Idle)
            _vision.SetIdleConeSway(true, _archetype.IdleSwayDegrees, _archetype.IdleSwaySpeed);
        else
            _vision.SetIdleConeSway(false);
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
        while (ShouldFlashSuspiciousCone())
        {
            float t = (Mathf.Sin(Time.time * coneFlashFrequency) + 1f) * 0.5f;
            _vision.SetConeColor(Color.Lerp(_vision.CalmConeColor, _vision.SuspiciousFlashColor, t));
            yield return null;
        }
        _flashRoutine = null;
    }

    private bool ShouldFlashSuspiciousCone()
    {
        return _state == State.Suspicious
            || (_sprinterFlow && _state == State.SprinterSweep);
    }
}
