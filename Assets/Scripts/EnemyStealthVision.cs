using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[System.Serializable]
public class StealthAttackEvent : UnityEvent<float> { }

/// <summary>
/// Stealth guard: draws a vision cone, spots the player with LOS, warns for a configurable time
/// (cone flashes), then chases and triggers attacks at a range/interval.
/// Vision uses transform.up (sprite &quot;top&quot;); the cone is parented and rotates with the enemy.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyStealthVision : MonoBehaviour
{
    public enum State
    {
        Idle,
        Warning,
        Chasing
    }

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float visionRange = 6f;
    [Tooltip("Full cone angle in degrees.")]
    [SerializeField] private float visionAngleDegrees = 70f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Warning")]
    [SerializeField] private float warningDurationSeconds = 2f;
    [SerializeField] private float coneFlashFrequency = 8f;
    [SerializeField] private Color calmConeColor = new Color(1f, 0.2f, 0.2f, 0.25f);
    [SerializeField] private Color warningConeFlashColor = new Color(1f, 0.4f, 0f, 0.55f);

    [Header("Chase & attack")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float attackRange = 0.65f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float attackIntervalSeconds = 1f;
    [SerializeField] private float chaseStopDistance = 0.15f;
    [Tooltip("Optional: disabled when chase starts (e.g. EnemyPatrol on this object).")]
    [SerializeField] private MonoBehaviour patrolBehaviourToDisable;

    [Header("Facing")]
    [Tooltip("Turn speed during the warning window so the player can slip out of the cone / LOS.")]
    [SerializeField] private float warningTurnDegreesPerSecond = 95f;
    [Tooltip("Turn speed while chasing — snappier lock-on after the warning completes.")]
    [SerializeField, FormerlySerializedAs("rotateTowardsPlayerDegreesPerSecond")]
    private float chaseTurnDegreesPerSecond = 360f;

    [Header("Cone mesh")]
    [SerializeField] private int coneMeshSegments = 24;
    [SerializeField] private string coneSortingLayer = "Default";
    [SerializeField] private int coneSortingOrder = -10;
    [SerializeField] private Material coneMaterial;

    [Header("Events")]
    public UnityEvent onWarningStarted;
    public UnityEvent onWarningCancelled;
    public UnityEvent onChaseStarted;
    public StealthAttackEvent onAttackPlayer = new StealthAttackEvent();

    private Rigidbody2D _body;
    private MeshFilter _coneMeshFilter;
    private MeshRenderer _coneRenderer;
    private Mesh _coneMesh;
    private MaterialPropertyBlock _propBlock;

    private Transform _player;
    private State _state = State.Idle;
    private float _warningTimer;
    private float _attackCooldown;
    private Coroutine _flashRoutine;

    public State CurrentState => _state;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.bodyType = RigidbodyType2D.Kinematic;
        BuildConeVisuals();
    }

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null)
            _player = p.transform;

        RebuildConeMesh();
    }

    private void OnValidate()
    {
        visionRange = Mathf.Max(0.1f, visionRange);
        visionAngleDegrees = Mathf.Clamp(visionAngleDegrees, 1f, 359f);
        warningTurnDegreesPerSecond = Mathf.Max(0f, warningTurnDegreesPerSecond);
        chaseTurnDegreesPerSecond = Mathf.Max(0f, chaseTurnDegreesPerSecond);
        coneMeshSegments = Mathf.Clamp(coneMeshSegments, 3, 64);
        if (Application.isPlaying && _coneMesh != null)
            RebuildConeMesh();
    }

    private void Update()
    {
        if (_player == null)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null)
                _player = p.transform;
            return;
        }

        bool seen = IsPlayerInVisionCone();

        switch (_state)
        {
            case State.Idle:
                if (seen)
                    BeginWarning();
                break;

            case State.Warning:
                if (!seen)
                {
                    CancelWarning();
                }
                else
                {
                    _warningTimer += Time.deltaTime;
                    if (_warningTimer >= warningDurationSeconds)
                        BeginChase();
                }
                break;

            case State.Chasing:
                if (_attackCooldown > 0f)
                    _attackCooldown -= Time.deltaTime;

                float d = Vector2.Distance(transform.position, _player.position);
                if (d <= attackRange && _attackCooldown <= 0f)
                {
                    _attackCooldown = attackIntervalSeconds;
                    onAttackPlayer?.Invoke(attackDamage);
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (_player == null)
            return;

        if (_state == State.Warning || _state == State.Chasing)
            RotateTowardsPlayer();

        if (_state != State.Chasing)
            return;

        Vector2 to = (Vector2)_player.position - _body.position;
        float dist = to.magnitude;
        if (dist <= chaseStopDistance)
            return;

        to /= dist;
        Vector2 next = _body.position + to * (chaseSpeed * Time.fixedDeltaTime);
        _body.MovePosition(next);
    }

    private void RotateTowardsPlayer()
    {
        Vector2 to = (Vector2)_player.position - _body.position;
        if (to.sqrMagnitude < 0.0001f)
            return;

        to.Normalize();
        float targetDeg = Mathf.Atan2(-to.x, to.y) * Mathf.Rad2Deg;
        float turnSpeed = _state == State.Warning ? warningTurnDegreesPerSecond : chaseTurnDegreesPerSecond;
        float nextDeg = Mathf.MoveTowardsAngle(_body.rotation, targetDeg, turnSpeed * Time.fixedDeltaTime);
        _body.MoveRotation(nextDeg);
    }

    private void BeginWarning()
    {
        _state = State.Warning;
        _warningTimer = 0f;
        onWarningStarted?.Invoke();
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashConeRoutine());
    }

    private void CancelWarning()
    {
        _state = State.Idle;
        _warningTimer = 0f;
        onWarningCancelled?.Invoke();
        StopFlashingRestoreCalm();
    }

    private void BeginChase()
    {
        _state = State.Chasing;
        StopFlashingRestoreCalm();
        if (patrolBehaviourToDisable != null)
            patrolBehaviourToDisable.enabled = false;
        onChaseStarted?.Invoke();
    }

    private IEnumerator FlashConeRoutine()
    {
        while (_state == State.Warning)
        {
            float pulse = (Mathf.Sin(Time.time * coneFlashFrequency) + 1f) * 0.5f;
            SetConeColor(Color.Lerp(calmConeColor, warningConeFlashColor, pulse));
            yield return null;
        }

        SetConeColor(calmConeColor);
        _flashRoutine = null;
    }

    private void StopFlashingRestoreCalm()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
        SetConeColor(calmConeColor);
    }

    private bool IsPlayerInVisionCone()
    {
        Vector2 origin = transform.position;
        Vector2 toPlayer = (Vector2)_player.position - origin;
        float dist = toPlayer.magnitude;
        if (dist > visionRange || dist < 0.001f)
            return false;

        Vector2 forward = transform.up;
        float half = visionAngleDegrees * 0.5f;
        float ang = Vector2.Angle(forward, toPlayer);
        if (ang > half)
            return false;

        RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, dist, obstacleMask);
        return hit.collider == null;
    }

    private void BuildConeVisuals()
    {
        var child = new GameObject("VisionCone");
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;

        _coneMeshFilter = child.AddComponent<MeshFilter>();
        _coneRenderer = child.AddComponent<MeshRenderer>();
        _coneMesh = new Mesh { name = "StealthVisionCone" };
        _coneMeshFilter.sharedMesh = _coneMesh;

        if (coneMaterial != null)
            _coneRenderer.sharedMaterial = coneMaterial;
        else
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh != null)
                _coneRenderer.sharedMaterial = new Material(sh);
        }

        _coneRenderer.sortingLayerName = coneSortingLayer;
        _coneRenderer.sortingOrder = coneSortingOrder;
        _propBlock = new MaterialPropertyBlock();
        SetConeColor(calmConeColor);
    }

    private void RebuildConeMesh()
    {
        if (_coneMesh == null)
            return;

        int segments = coneMeshSegments;
        var verts = new Vector3[segments + 2];
        var uvs = new Vector2[segments + 2];
        var tris = new int[segments * 3];

        verts[0] = Vector3.zero;
        uvs[0] = Vector2.zero;
        float halfRad = visionAngleDegrees * 0.5f * Mathf.Deg2Rad;
        float start = -halfRad;
        float end = halfRad;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float a = Mathf.Lerp(start, end, t);
            float x = -Mathf.Sin(a) * visionRange;
            float y = Mathf.Cos(a) * visionRange;
            verts[i + 1] = new Vector3(x, y, 0f);
            uvs[i + 1] = new Vector2(t, 1f);
        }

        int vi = 0;
        for (int i = 0; i < segments; i++)
        {
            tris[vi++] = 0;
            tris[vi++] = i + 1;
            tris[vi++] = i + 2;
        }

        _coneMesh.Clear();
        _coneMesh.vertices = verts;
        _coneMesh.uv = uvs;
        _coneMesh.triangles = tris;
        _coneMesh.RecalculateBounds();
    }

    private void SetConeColor(Color c)
    {
        if (_coneRenderer == null)
            return;
        _coneRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_Color", c);
        _coneRenderer.SetPropertyBlock(_propBlock);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
        Vector3 o = transform.position;
        Vector3 up = transform.up;
        float half = visionAngleDegrees * 0.5f;
        Vector3 left = (Quaternion.AngleAxis(half, Vector3.forward) * up).normalized * visionRange;
        Vector3 right = (Quaternion.AngleAxis(-half, Vector3.forward) * up).normalized * visionRange;
        Gizmos.DrawLine(o, o + left);
        Gizmos.DrawLine(o, o + right);
    }
}
