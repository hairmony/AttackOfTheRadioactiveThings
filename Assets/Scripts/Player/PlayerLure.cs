using UnityEngine;

/// <summary>
/// Throw a <see cref="SpawnLure"/> prefab in the current move/facing direction (stealth distraction).
/// </summary>
public class PlayerLure : MonoBehaviour
{
    [SerializeField] GameObject lurePrefab;
    [SerializeField] float throwSpeed = 9f;
    [SerializeField] float spawnOffset = 0.45f;
    [SerializeField] float cooldownSeconds = 1.25f;

    float _nextThrowTime;

    /// <summary>True when a lure prefab is assigned (throw is possible when off cooldown and allowed to move).</summary>
    public bool HasLurePrefab => lurePrefab != null;

    public float CooldownDuration => cooldownSeconds;

    /// <summary>Unscaled seconds until the next throw is allowed.</summary>
    public float CooldownRemainingUnscaled => Mathf.Max(0f, _nextThrowTime - Time.unscaledTime);

    void Update()
    {
        if (Time.timeScale <= 0f)
            return;
        if (lurePrefab == null)
            return;

        if (PlayerControls.Instance == null || !PlayerControls.Instance.interactPressed)
            return;
        if (Time.unscaledTime < _nextThrowTime)
            return;

        var pm = GetComponent<PlayerMovement>();
        if (pm != null && !pm.canMove)
            return;

        Vector2 dir = GetThrowDirection(pm);
        Vector3 spawnPos = transform.position + (Vector3)(dir * spawnOffset);
        var go = Instantiate(lurePrefab, spawnPos, Quaternion.identity);
        if (go.TryGetComponent<Rigidbody2D>(out var rb))
            rb.linearVelocity = dir * throwSpeed;

        _nextThrowTime = Time.unscaledTime + cooldownSeconds;
    }

    static Vector2 GetThrowDirection(PlayerMovement pm)
    {
        if (pm != null)
        {
            if (pm.moveX != 0f || pm.moveY != 0f)
                return new Vector2(pm.moveX, pm.moveY).normalized;
            return (Vector2)pm.transform.right;
        }
        return Vector2.right;
    }
}
