using UnityEngine;

/// <summary>
/// Builds and renders a vision-cone mesh.
/// Exposes CanSeePlayer and a SetConeColor API so EnemyAI can drive visuals.
/// This component has zero knowledge of state — it only detects and draws.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyVision : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float visionRange = 6f;
    [Tooltip("Full cone angle in degrees.")]
    [SerializeField] private float visionAngleDegrees = 70f;
    [Tooltip("Player must be this much inside max range before detection triggers.")]
    [SerializeField] private float detectionRangeInset = 0.1f;
    [Tooltip("Player must be this many degrees inside cone edge before detection triggers.")]
    [SerializeField] private float detectionAngleInsetDegrees = 2f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Cone Visuals")]
    [SerializeField] private Color calmConeColor = new Color(1f, 0.20f, 0.20f, 0.25f);
    [SerializeField] private Color suspiciousFlashColor = new Color(1f, 0.55f, 0.00f, 0.55f);
    [SerializeField] private Color searchConeColor = new Color(1f, 0.90f, 0.00f, 0.35f);

    public Color CalmConeColor => calmConeColor;
    public Color SuspiciousFlashColor => suspiciousFlashColor;
    public Color SearchConeColor => searchConeColor;

    [Header("Cone Mesh")]
    [SerializeField] private int coneMeshSegments = 24;
    [SerializeField] private string coneSortingLayer = "Default";
    [SerializeField] private int coneSortingOrder = -10;
    [SerializeField] private Material coneMaterial;
    [SerializeField] private bool clipConeAgainstObstacles = true;
    [Tooltip("Layers that visually clip the cone mesh. Leave empty to use Obstacle Mask.")]
    [SerializeField] private LayerMask coneClipMask;
    [Tooltip("Small visual extension after hit point. Keep near 0 for stable clipping.")]
    [SerializeField] private float coneClipPadding = 0f;
    [Tooltip("Ignore trigger colliders when clipping cone rays.")]
    [SerializeField] private bool ignoreTriggerCollidersForConeClip = true;
    [Header("Debug")]
    [SerializeField] private bool debugDrawConeRays = false;
    [SerializeField] private Color debugRayNoHitColor = new Color(0.2f, 1f, 0.2f, 0.8f);
    [SerializeField] private Color debugRayHitColor = new Color(1f, 0.2f, 0.2f, 0.95f);

    // ── Public read-only ─────────────────────────────────────────────────────
    public bool CanSeePlayer { get; private set; }
    public Transform PlayerTransform { get; private set; }

    // ── Private ──────────────────────────────────────────────────────────────
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private Collider2D[] _selfColliders;
    private Vector3[] _debugRayEnds;
    private bool[] _debugRayHit;

    // ────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _selfColliders = GetComponentsInChildren<Collider2D>();
        BuildConeVisuals();
    }

    private void Start()
    {
        FindPlayer();
        RebuildConeMesh();
        SetConeColor(calmConeColor);
    }

    private void Update()
    {
        if (PlayerTransform == null) FindPlayer();
        CanSeePlayer = PlayerTransform != null && CheckLineOfSight();
        RebuildConeMesh();
    }

    private void OnValidate()
    {
        visionRange = Mathf.Max(0.1f, visionRange);
        visionAngleDegrees = Mathf.Clamp(visionAngleDegrees, 1f, 359f);
        detectionRangeInset = Mathf.Max(0f, detectionRangeInset);
        detectionAngleInsetDegrees = Mathf.Max(0f, detectionAngleInsetDegrees);
        coneMeshSegments = Mathf.Clamp(coneMeshSegments, 3, 64);
        coneClipPadding = Mathf.Max(0f, coneClipPadding);
        if (Application.isPlaying && _mesh != null)
            RebuildConeMesh();
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────────────────────────────────

    public void SetConeColor(Color c)
    {
        if (_meshRenderer == null) return;
        _meshRenderer.material.color = c;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Detection
    // ────────────────────────────────────────────────────────────────────────

    private bool CheckLineOfSight()
    {
        Vector2 origin = transform.position;
        Vector2 toPlayer = (Vector2)PlayerTransform.position - origin;
        float dist = toPlayer.magnitude;

        if (!IsInsideDetectionCore(toPlayer, dist)) return false;

        RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, dist, obstacleMask);
        return hit.collider == null;
    }

    private bool IsInsideDetectionCore(Vector2 toPoint, float dist)
    {
        if (dist < 0.001f) return false;

        float effectiveRange = Mathf.Max(0.01f, visionRange - detectionRangeInset);
        float effectiveHalfAngle = Mathf.Max(0.1f, (visionAngleDegrees * 0.5f) - detectionAngleInsetDegrees);

        if (dist > effectiveRange) return false;
        if (Vector2.Angle(transform.up, toPoint) > effectiveHalfAngle) return false;
        return true;
    }

    private void FindPlayer()
    {
        var go = GameObject.FindGameObjectWithTag(playerTag);
        if (go != null) PlayerTransform = go.transform;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Cone mesh
    // ────────────────────────────────────────────────────────────────────────

    private void BuildConeVisuals()
    {
        var child = new GameObject("VisionCone");
        child.transform.SetParent(transform, false);

        _meshFilter = child.AddComponent<MeshFilter>();
        _meshRenderer = child.AddComponent<MeshRenderer>();
        _mesh = new Mesh { name = "VisionConeMesh" };
        _meshFilter.sharedMesh = _mesh;

        _meshRenderer.sharedMaterial = coneMaterial != null
            ? coneMaterial
            : new Material(Shader.Find("Sprites/Default"));

        _meshRenderer.sortingLayerName = coneSortingLayer;
        _meshRenderer.sortingOrder = coneSortingOrder;
    }

    private void RebuildConeMesh()
    {
        if (_mesh == null) return;

        int n = coneMeshSegments;
        float halfRad = visionAngleDegrees * 0.5f * Mathf.Deg2Rad;

        var verts = new Vector3[n + 2];
        var uvs = new Vector2[n + 2];
        var tris = new int[n * 3];
        if (_debugRayEnds == null || _debugRayEnds.Length != n + 1)
        {
            _debugRayEnds = new Vector3[n + 1];
            _debugRayHit = new bool[n + 1];
        }

        verts[0] = Vector3.zero;
        uvs[0] = Vector2.zero;

        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float a = Mathf.Lerp(-halfRad, halfRad, t);
            Vector2 localDir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            float sampleDistance = visionRange;

            if (clipConeAgainstObstacles)
            {
                LayerMask mask = coneClipMask.value == 0 ? obstacleMask : coneClipMask;
                Vector2 worldDir = transform.TransformDirection((Vector3)localDir).normalized;
                sampleDistance = GetVisualClipDistance((Vector2)transform.position, worldDir, visionRange, mask, out bool clippedByHit);
                _debugRayHit[i] = clippedByHit;
            }
            else
            {
                _debugRayHit[i] = false;
            }

            verts[i + 1] = new Vector3(localDir.x * sampleDistance, localDir.y * sampleDistance, 0f);
            uvs[i + 1] = new Vector2(t, 1f);
            _debugRayEnds[i] = transform.TransformPoint(verts[i + 1]);
        }

        int vi = 0;
        for (int i = 0; i < n; i++)
        {
            tris[vi++] = 0;
            tris[vi++] = i + 1;
            tris[vi++] = i + 2;
        }

        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
    }

    private float GetVisualClipDistance(Vector2 origin, Vector2 worldDir, float maxDistance, LayerMask mask, out bool clippedByHit)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, worldDir, maxDistance, mask);
        float nearest = maxDistance;
        clippedByHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null) continue;
            if (IsSelfCollider(hit.collider)) continue;
            if (ignoreTriggerCollidersForConeClip && hit.collider.isTrigger) continue;

            float d = hit.distance + coneClipPadding;

            if (d < nearest)
            {
                nearest = d;
                clippedByHit = true;
            }
        }

        return Mathf.Clamp(nearest, 0f, maxDistance);
    }

    private bool IsSelfCollider(Collider2D col)
    {
        if (col == null || _selfColliders == null) return false;
        for (int i = 0; i < _selfColliders.Length; i++)
        {
            if (_selfColliders[i] == col) return true;
        }
        return false;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Gizmos
    // ────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
        Vector3 o = transform.position;
        float half = visionAngleDegrees * 0.5f;
        Gizmos.DrawLine(o, o + (Quaternion.AngleAxis(half, Vector3.forward) * transform.up).normalized * visionRange);
        Gizmos.DrawLine(o, o + (Quaternion.AngleAxis(-half, Vector3.forward) * transform.up).normalized * visionRange);

        if (!debugDrawConeRays || _debugRayEnds == null || _debugRayHit == null)
            return;

        int count = Mathf.Min(_debugRayEnds.Length, _debugRayHit.Length);
        for (int i = 0; i < count; i++)
        {
            Gizmos.color = _debugRayHit[i] ? debugRayHitColor : debugRayNoHitColor;
            Gizmos.DrawLine(o, _debugRayEnds[i]);
            Gizmos.DrawWireSphere(_debugRayEnds[i], 0.03f);
        }
    }
}