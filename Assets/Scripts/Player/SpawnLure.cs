using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// World object thrown by the player; registered while active so <see cref="EnemyVision"/> can spot it.
/// </summary>
[DisallowMultipleComponent]
public class SpawnLure : MonoBehaviour
{
    public static readonly List<SpawnLure> Active = new List<SpawnLure>(8);

    [Tooltip("Destroy this many seconds after spawn (0 = never).")]
    [SerializeField] float lifetimeSeconds = 45f;

    void OnEnable()
    {
        if (!Active.Contains(this))
            Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    void Start()
    {
        if (lifetimeSeconds > 0f)
            Destroy(gameObject, lifetimeSeconds);
    }
}
