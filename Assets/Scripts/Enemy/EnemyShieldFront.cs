using UnityEngine;

/// <summary>
/// Blocks projectiles that hit from the frontal arc (e.g. shield). Uses this transform's <c>up</c> as facing (match <see cref="EnemyVision"/>).
/// </summary>
public class EnemyShieldFront : MonoBehaviour
{
    [Tooltip("Half-width of the protected frontal arc in degrees (90 ≈ full forward hemisphere in 2D).")]
    [SerializeField] [Range(15f, 90f)] private float frontalBlockHalfAngleDegrees = 72f;

    [Tooltip("Optional; defaults to this transform's up vector in world space.")]
    [SerializeField] private Transform facingReference;

    public bool BlocksIncomingDirection(Vector2 worldDirectionIntoEnemy)
    {
        Vector2 facing;
        if (facingReference != null)
            facing = (Vector2)facingReference.up;
        else
            facing = (Vector2)transform.up;

        facing.Normalize();
        Vector2 incoming = worldDirectionIntoEnemy.normalized;
        float limit = Mathf.Cos(frontalBlockHalfAngleDegrees * Mathf.Deg2Rad);
        return Vector2.Dot(incoming, facing) >= limit;
    }
}
