using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct MinimapEnemyKindStyle
{
    [Tooltip("Null = use default Enemy Icon (below).")]
    public Sprite icon;
    [Tooltip("If enabled, replaces default Enemy Color for this type. Alpha 0 is ignored (keeps default).")]
    public bool overrideColor;
    public Color color;
    [Tooltip("0 = use default Enemy Icon Size.")]
    [Min(0f)]
    public float iconSize;
}

/// <summary>
/// Draws player and enemy icons on top of a minimap <see cref="RawImage"/>, positioned from a north-aligned orthographic <see cref="Camera"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class MinimapWorldIcons : MonoBehaviour
{
    [Header("Minimap camera")]
    [Tooltip("Orthographic minimap camera (e.g. MinimapCamera). Auto-finds a GameObject named MinimapCamera if unset.")]
    [SerializeField] Camera minimapCamera;

    [Header("Player")]
    [SerializeField] Transform playerTransformOverride;
    [SerializeField] Sprite playerIcon;
    [SerializeField] Color playerColor = new Color(0.2f, 0.95f, 1f, 1f);
    [SerializeField] float playerIconSize = 14f;
    [SerializeField] bool rotatePlayerIconWithFacing = true;

    [Header("Enemies — defaults")]
    [SerializeField] Sprite enemyIcon;
    [SerializeField] Color enemyColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] float enemyIconSize = 12f;
    [SerializeField] bool rotateEnemyIconWithFacing;

    [Header("Enemies — per type (optional)")]
    [SerializeField] MinimapEnemyKindStyle watcherMinimap;
    [SerializeField] MinimapEnemyKindStyle sprinterMinimap;
    [SerializeField] MinimapEnemyKindStyle bulwarkMinimap;
    [Tooltip("How often to refresh the list of enemies (avoids scanning every frame).")]
    [SerializeField] float enemyRescanInterval = 0.25f;

    RectTransform _mapRect;
    RectTransform _overlayRoot;
    Image _playerImage;
    readonly List<Image> _enemyPool = new List<Image>();
    EnemyAI[] _enemyScratch = System.Array.Empty<EnemyAI>();
    float _nextEnemyScan;

    static Sprite _whiteSprite;

    void Awake()
    {
        _mapRect = (RectTransform)transform;
        if (minimapCamera == null)
        {
            var go = GameObject.Find("MinimapCamera");
            if (go != null)
                minimapCamera = go.GetComponent<Camera>();
        }

        BuildOverlayRoot();
        _playerImage = CreateIconImage("PlayerMinimapIcon", playerIcon, playerColor, playerIconSize);
    }

    void LateUpdate()
    {
        if (minimapCamera == null || _overlayRoot == null)
            return;

        _overlayRoot.SetAsLastSibling();

        Transform playerTf = playerTransformOverride;
        if (playerTf == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerTf = p.transform;
        }

        if (playerTf != null && TryWorldToOverlayLocal(playerTf.position, out Vector2 plocal))
        {
            _playerImage.rectTransform.anchoredPosition = plocal;
            _playerImage.enabled = true;
            ApplyIconSpriteAndColor(_playerImage, playerIcon, playerColor);
            SetIconSize(_playerImage.rectTransform, playerIconSize);
            if (rotatePlayerIconWithFacing)
                _playerImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, playerTf.eulerAngles.z);
            else
                _playerImage.rectTransform.localRotation = Quaternion.identity;
        }
        else
            _playerImage.enabled = false;

        if (Time.unscaledTime >= _nextEnemyScan)
        {
            _nextEnemyScan = Time.unscaledTime + Mathf.Max(0.05f, enemyRescanInterval);
            _enemyScratch = FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        EnsureEnemyPoolSize(_enemyScratch.Length);
        for (int i = 0; i < _enemyPool.Count; i++)
        {
            if (i < _enemyScratch.Length && _enemyScratch[i] != null)
            {
                var e = _enemyScratch[i];
                if (TryWorldToOverlayLocal(e.transform.position, out Vector2 elocal))
                {
                    var img = _enemyPool[i];
                    img.rectTransform.anchoredPosition = elocal;
                    img.enabled = true;
                    ResolveEnemyMinimapStyle(e, out Sprite esp, out Color ecol, out float esz);
                    ApplyIconSpriteAndColor(img, esp, ecol);
                    SetIconSize(img.rectTransform, esz);
                    if (rotateEnemyIconWithFacing)
                        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, e.transform.eulerAngles.z);
                    else
                        img.rectTransform.localRotation = Quaternion.identity;
                }
                else
                    _enemyPool[i].enabled = false;
            }
            else if (_enemyPool[i] != null)
                _enemyPool[i].enabled = false;
        }
    }

    void BuildOverlayRoot()
    {
        var go = new GameObject("MinimapIconOverlay", typeof(RectTransform));
        _overlayRoot = go.GetComponent<RectTransform>();
        _overlayRoot.SetParent(_mapRect, false);
        _overlayRoot.anchorMin = Vector2.zero;
        _overlayRoot.anchorMax = Vector2.one;
        _overlayRoot.pivot = new Vector2(0.5f, 0.5f);
        _overlayRoot.offsetMin = Vector2.zero;
        _overlayRoot.offsetMax = Vector2.zero;
        _overlayRoot.localScale = Vector3.one;
    }

    Image CreateIconImage(string objectName, Sprite sprite, Color color, float size)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(_overlayRoot, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        SetIconSize(rt, size);

        var img = go.GetComponent<Image>();
        ApplyIconSpriteAndColor(img, sprite, color);
        img.raycastTarget = false;
        img.maskable = true;
        return img;
    }

    static void SetIconSize(RectTransform rt, float size)
    {
        rt.sizeDelta = new Vector2(size, size);
    }

    static void ApplyIconSpriteAndColor(Image img, Sprite sprite, Color color)
    {
        img.sprite = sprite != null ? sprite : GetWhiteSprite();
        img.color = color;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
    }

    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;
        var tex = Texture2D.whiteTexture;
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        _whiteSprite.name = "MinimapWhiteSprite_Runtime";
        return _whiteSprite;
    }

    void EnsureEnemyPoolSize(int count)
    {
        while (_enemyPool.Count < count)
            _enemyPool.Add(CreateIconImage("EnemyMinimapIcon", enemyIcon, enemyColor, enemyIconSize));
    }

    void ResolveEnemyMinimapStyle(EnemyAI enemy, out Sprite sprite, out Color color, out float size)
    {
        sprite = enemyIcon;
        color = enemyColor;
        size = enemyIconSize;

        if (enemy == null)
            return;

        if (!enemy.TryGetComponent(out EnemyArchetype arch))
        {
            ApplyMinimapKindStyle(ref sprite, ref color, ref size, watcherMinimap);
            return;
        }

        switch (arch.Kind)
        {
            case EnemyKind.Watcher:
                ApplyMinimapKindStyle(ref sprite, ref color, ref size, watcherMinimap);
                break;
            case EnemyKind.Sprinter:
                ApplyMinimapKindStyle(ref sprite, ref color, ref size, sprinterMinimap);
                break;
            case EnemyKind.Bulwark:
                ApplyMinimapKindStyle(ref sprite, ref color, ref size, bulwarkMinimap);
                break;
            default:
                ApplyMinimapKindStyle(ref sprite, ref color, ref size, watcherMinimap);
                break;
        }
    }

    static void ApplyMinimapKindStyle(ref Sprite sprite, ref Color color, ref float size, MinimapEnemyKindStyle style)
    {
        if (style.icon != null)
            sprite = style.icon;
        if (style.overrideColor && style.color.a > 0.001f)
            color = style.color;
        if (style.iconSize > 0f)
            size = style.iconSize;
    }

    bool TryWorldToOverlayLocal(Vector3 world, out Vector2 local)
    {
        local = default;
        Vector3 vp = minimapCamera.WorldToViewportPoint(world);
        if (vp.z <= 0f)
            return false;
        if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
            return false;

        Rect r = _overlayRoot.rect;
        local.x = (vp.x - 0.5f) * r.width;
        local.y = (vp.y - 0.5f) * r.height;
        return true;
    }
}
