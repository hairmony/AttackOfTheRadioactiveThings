using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scrolls a <see cref="RectTransform"/> upward. Resets start position after layout/TMP so the roll begins
/// below the visible area (not mid-scroll). Disables a sibling <see cref="ScrollRect"/> on the same object if present — it fights manual motion.
/// </summary>
[DefaultExecutionOrder(100)]
public class CreditsScroll : MonoBehaviour
{
    [Header("Credits block")]
    [Tooltip("If null, uses this object's RectTransform.")]
    [SerializeField] RectTransform creditsContent;

    [Tooltip("Pixels per second (anchored/local Y).")]
    [SerializeField] float scrollSpeed = 45f;

    [Tooltip("When true, Y is set so content starts just below this rect (or below the content's parent).")]
    [SerializeField] bool placeBelowVisibleArea = true;

    [Tooltip("Usually the panel/mask that clips credits. If empty, uses the content's parent RectTransform.")]
    [SerializeField] RectTransform visibleArea;

    [SerializeField] float paddingBelow = 48f;

    [Tooltip("Used when placeBelowVisibleArea is off, or as fallback.")]
    [SerializeField] float startOffsetY = -1600f;

    [SerializeField] bool useUnscaledTime = true;

    [Tooltip("When the last line has scrolled past the top of the visible area, snap back to the start.")]
    [SerializeField] bool loop = true;

    [Tooltip("Add RectMask2D to visibleArea if missing so scrolling text stays inside the panel.")]
    [SerializeField] bool ensureRectMaskOnVisibleArea = true;

    [Tooltip("Hide credits content until the start position is applied (avoids one frame / shader flash at wrong layout).")]
    [SerializeField] bool hideContentUntilStartPositionApplied = true;

    [Header("Scrolling background (RawImage)")]
    [SerializeField] RawImage tiledBackground;
    [SerializeField] Vector2 uvScrollPerSecond;

    [Header("Parallax")]
    [SerializeField] RectTransform[] parallaxLayers;
    [SerializeField] float parallaxSpeedScale = 0.3f;
    [SerializeField] float[] parallaxSpeeds;

    Rect _backgroundUvBase;
    bool _hasBackgroundUvBase;
    Coroutine _resetCo;
    bool _positionReady;
    Vector2[] _parallaxStartAnchored;
    CanvasGroup _contentCanvasGroup;

    void Awake()
    {
        if (tiledBackground != null)
        {
            _backgroundUvBase = tiledBackground.uvRect;
            _hasBackgroundUvBase = true;
        }

        var sr = GetComponent<ScrollRect>();
        if (sr != null)
            sr.enabled = false;

        if (ensureRectMaskOnVisibleArea && visibleArea != null &&
            visibleArea.GetComponent<RectMask2D>() == null)
            visibleArea.gameObject.AddComponent<RectMask2D>();

        if (creditsContent == null)
            Debug.LogWarning(
                $"{nameof(CreditsScroll)} on '{name}': {nameof(creditsContent)} is not assigned — this object scrolls itself; assign the child credits column so the panel stays still.",
                this);

        if (hideContentUntilStartPositionApplied && creditsContent != null)
        {
            _contentCanvasGroup = creditsContent.GetComponent<CanvasGroup>();
            if (_contentCanvasGroup == null)
                _contentCanvasGroup = creditsContent.gameObject.AddComponent<CanvasGroup>();
        }
    }

    void OnEnable()
    {
        _positionReady = false;
        ResetBackgroundUv();
        if (_resetCo != null)
            StopCoroutine(_resetCo);

        // CreditsTextImporter uses earlier execution order (-200), so blocks exist here. Apply start
        // position in the same frame before the first render; coroutine yielded first before = 1-frame flash.
        var target = TargetContentRect;
        if (hideContentUntilStartPositionApplied && target != null)
        {
            if (_contentCanvasGroup == null)
            {
                _contentCanvasGroup = target.GetComponent<CanvasGroup>();
                if (_contentCanvasGroup == null)
                    _contentCanvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            }

            _contentCanvasGroup.alpha = 0f;
            _contentCanvasGroup.interactable = false;
            _contentCanvasGroup.blocksRaycasts = false;
        }

        ApplyInitialCreditsPlacement();

        if (hideContentUntilStartPositionApplied && _contentCanvasGroup != null)
        {
            _contentCanvasGroup.alpha = 1f;
            _contentCanvasGroup.interactable = true;
            _contentCanvasGroup.blocksRaycasts = true;
        }

        _resetCo = StartCoroutine(ApplyStartAfterLayout());
    }

    void OnDisable()
    {
        if (_resetCo != null)
        {
            StopCoroutine(_resetCo);
            _resetCo = null;
        }
    }

    void ApplyInitialCreditsPlacement()
    {
        var target = TargetContentRect;
        if (target == null) return;

        var parent = target.parent as RectTransform;
        if (parent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        Canvas.ForceUpdateCanvases();
        ApplyStartPosition();
    }

    IEnumerator ApplyStartAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        var target = TargetContentRect;
        if (target != null)
        {
            var parent = target.parent as RectTransform;
            if (parent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);
            Canvas.ForceUpdateCanvases();
        }

        // Block credits (VLG + TMP blocks + images) need another beat for preferred heights / sprites.
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (target != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);

        ApplyStartPosition();

        // End of frame: image / layout catch-up; refine start without hiding (already on-screen).
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        if (target != null)
        {
            var parent = target.parent as RectTransform;
            if (parent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        }

        ApplyStartPosition();
        CaptureParallaxStart();
        _positionReady = true;
    }

    void CaptureParallaxStart()
    {
        if (parallaxLayers == null || parallaxLayers.Length == 0)
        {
            _parallaxStartAnchored = null;
            return;
        }

        _parallaxStartAnchored = new Vector2[parallaxLayers.Length];
        for (int i = 0; i < parallaxLayers.Length; i++)
        {
            if (parallaxLayers[i] == null) continue;
            _parallaxStartAnchored[i] = parallaxLayers[i].anchoredPosition;
        }
    }

    void ApplyStartPosition()
    {
        var target = TargetContentRect;
        if (target == null)
        {
            var pos = transform.localPosition;
            pos.y = startOffsetY;
            transform.localPosition = pos;
            return;
        }

        if (placeBelowVisibleArea)
        {
            var parent = target.parent as RectTransform;
            var view = visibleArea != null ? visibleArea : parent;
            if (parent != null && view != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(target);
                if (parent != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
                Canvas.ForceUpdateCanvases();

                // Anchor-agnostic: place the scroll content pivot exactly at "just below viewport bottom".
                Vector3[] corners = new Vector3[4];
                view.GetWorldCorners(corners);
                Vector3 bottomCenterWorld = (corners[0] + corners[3]) * 0.5f;
                Vector3 axisUp = corners[1] - corners[0];
                if (axisUp.sqrMagnitude > 1e-8f)
                    axisUp.Normalize();
                else
                    axisUp = Vector3.up;

                Vector3 desiredPivotWorld = bottomCenterWorld - axisUp * paddingBelow;
                target.position += desiredPivotWorld - target.position;
                return;
            }
        }

        var p = target.anchoredPosition;
        p.y = startOffsetY;
        target.anchoredPosition = p;
    }

    void ResetBackgroundUv()
    {
        if (tiledBackground != null && _hasBackgroundUvBase)
            tiledBackground.uvRect = _backgroundUvBase;
    }

    RectTransform TargetContentRect => creditsContent != null ? creditsContent : transform as RectTransform;

    void JumpToStart()
    {
        ApplyStartPosition();
        ResetBackgroundUv();

        if (_parallaxStartAnchored == null || parallaxLayers == null) return;

        for (int i = 0; i < parallaxLayers.Length && i < _parallaxStartAnchored.Length; i++)
        {
            if (parallaxLayers[i] == null) continue;
            parallaxLayers[i].anchoredPosition = _parallaxStartAnchored[i];
        }
    }

    /// <summary>
    /// Bottom of credits column in <paramref name="view"/>'s local space. With child blocks, uses each child’s
    /// <see cref="RectTransform"/> corners (layout size). Single-child TMP-only mode may use <see cref="TMP_Text.textBounds"/>.
    /// </summary>
    static float ContentBottomYInViewLocal(RectTransform content, RectTransform view)
    {
        if (content.childCount == 0)
        {
            var tmp = content.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.ForceMeshUpdate(ignoreActiveState: true);
                Bounds tb = tmp.textBounds;
                if (tb.size.sqrMagnitude < 1e-6f)
                    return float.NegativeInfinity;

                var localBottom = new Vector3(tb.center.x, tb.min.y, tb.center.z);
                var worldBottom = tmp.transform.TransformPoint(localBottom);
                return view.InverseTransformPoint(worldBottom).y;
            }

            var solo = new Vector3[4];
            content.GetWorldCorners(solo);
            float my = float.MaxValue;
            for (int i = 0; i < 4; i++)
                my = Mathf.Min(my, view.InverseTransformPoint(solo[i]).y);
            return my;
        }

        float minY = float.MaxValue;
        bool any = false;
        for (int i = 0; i < content.childCount; i++)
        {
            var ch = content.GetChild(i) as RectTransform;
            if (ch == null || !ch.gameObject.activeSelf)
                continue;

            var c = new Vector3[4];
            ch.GetWorldCorners(c);
            for (int j = 0; j < 4; j++)
            {
                minY = Mathf.Min(minY, view.InverseTransformPoint(c[j]).y);
                any = true;
            }
        }

        if (!any)
            return float.NegativeInfinity;
        return minY;
    }

    bool CreditsFullyPastViewportTop()
    {
        var content = TargetContentRect;
        var view = visibleArea != null ? visibleArea : content?.parent as RectTransform;
        if (content == null || view == null) return false;

        if (content.childCount > 0)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float contentBottomY = ContentBottomYInViewLocal(content, view);
        if (float.IsNegativeInfinity(contentBottomY))
            return false;

        return contentBottomY >= view.rect.yMax;
    }

    void Update()
    {
        if (!_positionReady)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        var rt = TargetContentRect;
        if (rt != null)
        {
            var p = rt.anchoredPosition;
            p.y += scrollSpeed * dt;
            rt.anchoredPosition = p;
        }
        else
        {
            var pos = transform.localPosition;
            pos.y += scrollSpeed * dt;
            transform.localPosition = pos;
        }

        ScrollBackgroundUv(dt);
        ScrollParallax(dt);

        if (loop && CreditsFullyPastViewportTop())
            JumpToStart();
    }

    void ScrollBackgroundUv(float dt)
    {
        if (tiledBackground == null || uvScrollPerSecond == Vector2.zero) return;

        var r = tiledBackground.uvRect;
        r.x += uvScrollPerSecond.x * dt;
        r.y += uvScrollPerSecond.y * dt;
        tiledBackground.uvRect = r;
    }

    void ScrollParallax(float dt)
    {
        if (parallaxLayers == null) return;

        for (int i = 0; i < parallaxLayers.Length; i++)
        {
            if (parallaxLayers[i] == null) continue;

            float speed = scrollSpeed * parallaxSpeedScale;
            if (parallaxSpeeds != null && i < parallaxSpeeds.Length)
                speed = parallaxSpeeds[i];

            var p = parallaxLayers[i].anchoredPosition;
            p.y += speed * dt;
            parallaxLayers[i].anchoredPosition = p;
        }
    }
}
