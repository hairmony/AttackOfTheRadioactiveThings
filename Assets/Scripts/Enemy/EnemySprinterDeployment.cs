using UnityEngine;

/// <summary>
/// One-shot payload on summoned sprinters: set by <see cref="EnemyArchetype"/> before <see cref="EnemyArchetype.WakeFromWatcherSummon"/>.
/// Read by <see cref="EnemyAI"/> in <c>Start</c>, then removed.
/// </summary>
public class EnemySprinterDeployment : MonoBehaviour
{
    public Vector2 InvestigationCenter { get; private set; }
    public float ApproachRingMin { get; private set; }
    public float ApproachRingMax { get; private set; }
    public float SweepOrbitRadius { get; private set; }
    public float OffscreenMargin { get; private set; }

    public void Setup(
        Vector2 investigationCenter,
        float approachRingMin,
        float approachRingMax,
        float sweepOrbitRadius,
        float offscreenMargin)
    {
        InvestigationCenter = investigationCenter;
        ApproachRingMin = Mathf.Max(0.05f, approachRingMin);
        ApproachRingMax = Mathf.Max(ApproachRingMin + 0.05f, approachRingMax);
        SweepOrbitRadius = Mathf.Max(0.1f, sweepOrbitRadius);
        OffscreenMargin = Mathf.Max(0.1f, offscreenMargin);
    }
}
