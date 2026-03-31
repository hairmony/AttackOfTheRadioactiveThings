using UnityEngine;

/// <summary>
/// Handles all Rigidbody2D movement for the enemy.
/// EnemyAI calls the public methods each FixedUpdate depending on current state.
/// No state logic lives here — this is purely "how to move."
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Chase")]
    [SerializeField] public float chaseSpeed = 3.5f;
    [SerializeField] public float chaseStopDistance = 0.15f;
    [SerializeField] public float chaseTurnSpeed = 360f;

    [Header("Suspicious")]
    [Tooltip("Turn speed while suspicious — slow, giving the player time to react.")]
    [SerializeField] public float suspiciousTurnSpeed = 95f;

    [Header("Search")]
    [Tooltip("Fraction of chaseSpeed used while walking to the last-known position.")]
    [SerializeField] public float searchSpeedMultiplier = 0.6f;
    [Tooltip("Distance at which the guard is considered 'arrived' at last-known position.")]
    [SerializeField] public float searchArrivalDistance = 0.35f;
    [Tooltip("Degrees per second the guard sweeps when scanning on the spot.")]
    [SerializeField] public float scanSpeed = 55f;
    [Tooltip("Half-angle swept in each direction during on-spot scanning.")]
    [SerializeField] public float scanHalfAngle = 50f;

    // ── Private ──────────────────────────────────────────────────────────────
    private Rigidbody2D _body;

    private Vector2 _sprinterStaleChaseTarget;

    // Scan state — reset by EnemyAI whenever search begins
    private bool _arrivedAtLastKnown;
    private float _scanBaseRotation;
    private float _scanOffset;
    private float _scanVelocity;

    // ────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.bodyType = RigidbodyType2D.Kinematic;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Public movement methods — called by EnemyAI from FixedUpdate
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Rotate smoothly toward the player at suspicious turn speed.</summary>
    public void RotateSuspicious(Vector2 targetPosition)
        => RotateTowards(targetPosition, suspiciousTurnSpeed);

    /// <summary>Rotate and move toward the player at full chase speed.</summary>
    public void ChasePlayer(Vector2 targetPosition)
    {
        RotateTowards(targetPosition, chaseTurnSpeed);

        Vector2 to = targetPosition - _body.position;
        float dist = to.magnitude;
        if (dist <= chaseStopDistance) return;

        _body.MovePosition(_body.position + (to / dist) * (chaseSpeed * Time.fixedDeltaTime));
    }

    /// <summary>Planar move + turn (deploy / leave paths).</summary>
    public void WalkTowards(Vector2 worldTarget, float moveSpeed, float turnSpeedDegreesPerSec)
    {
        Vector2 to = worldTarget - _body.position;
        float dist = to.magnitude;
        if (dist < 0.0001f) return;
        to /= dist;
        float targetDeg = Mathf.Atan2(-to.x, to.y) * Mathf.Rad2Deg;
        _body.MoveRotation(Mathf.MoveTowardsAngle(
            _body.rotation, targetDeg, turnSpeedDegreesPerSec * Time.fixedDeltaTime));
        _body.MovePosition(_body.position + to * (moveSpeed * Time.fixedDeltaTime));
    }

    /// <summary>Call when entering chase so stale target starts at the player.</summary>
    public void ResetSprinterStaleChase(Vector2 worldPosition) => _sprinterStaleChaseTarget = worldPosition;

    /// <summary>
    /// Chase toward a target that lags behind the real position — overshoots corners, easier to juke.
    /// </summary>
    public void ChasePlayerWithStaleTarget(Vector2 trueTarget, float maxApproachUnitsPerSecond)
    {
        float step = Mathf.Max(0.01f, maxApproachUnitsPerSecond) * Time.fixedDeltaTime;
        _sprinterStaleChaseTarget = Vector2.MoveTowards(_sprinterStaleChaseTarget, trueTarget, step);
        ChasePlayer(_sprinterStaleChaseTarget);
    }

    /// <summary>
    /// Call this once when entering the Search state to reset internal scan data.
    /// </summary>
    public void BeginSearch()
    {
        _arrivedAtLastKnown = false;
        _scanOffset = 0f;
        _scanVelocity = scanSpeed;
    }

    /// <summary>
    /// Walk to lastKnownPos, then sweep the cone left/right.
    /// Returns true once the guard has arrived and is actively scanning.
    /// </summary>
    public bool SearchMove(Vector2 lastKnownPos)
    {
        if (!_arrivedAtLastKnown)
        {
            Vector2 toTarget = lastKnownPos - _body.position;
            float dist = toTarget.magnitude;

            if (dist <= searchArrivalDistance)
            {
                _arrivedAtLastKnown = true;
                _scanBaseRotation = _body.rotation;
                _scanOffset = 0f;
                _scanVelocity = scanSpeed;
            }
            else
            {
                Vector2 dir = toTarget / dist;
                float targetDeg = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
                _body.MoveRotation(Mathf.MoveTowardsAngle(
                    _body.rotation, targetDeg, suspiciousTurnSpeed * Time.fixedDeltaTime));
                _body.MovePosition(_body.position +
                    dir * (chaseSpeed * searchSpeedMultiplier * Time.fixedDeltaTime));
            }

            return false;
        }

        // On-spot sweep
        _scanOffset += _scanVelocity * Time.fixedDeltaTime;

        if (_scanOffset >= scanHalfAngle)
        {
            _scanOffset = scanHalfAngle;
            _scanVelocity = -scanSpeed;
        }
        else if (_scanOffset <= -scanHalfAngle)
        {
            _scanOffset = -scanHalfAngle;
            _scanVelocity = scanSpeed;
        }

        _body.MoveRotation(_scanBaseRotation + _scanOffset);
        return true; // is scanning
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Internal helpers
    // ────────────────────────────────────────────────────────────────────────

    private void RotateTowards(Vector2 targetPosition, float degreesPerSecond)
    {
        Vector2 to = targetPosition - _body.position;
        if (to.sqrMagnitude < 0.0001f) return;

        to.Normalize();
        float targetDeg = Mathf.Atan2(-to.x, to.y) * Mathf.Rad2Deg;
        _body.MoveRotation(Mathf.MoveTowardsAngle(
            _body.rotation, targetDeg, degreesPerSecond * Time.fixedDeltaTime));
    }
}