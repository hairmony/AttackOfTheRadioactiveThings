using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds credits UI from Markdown with optional image lines:
/// <c>![alt](path)</c>,
/// <c>![alt](path|width)</c> (layout width in UI units, height keeps aspect),
/// <c>![alt](path|widthxheight)</c> (e.g. <c>320x80</c> or <c>320 x 80</c>).
/// Path is relative to <see cref="Populate"/>’s art folder (default <c>Assets/Art</c>) in the Editor
/// (<see cref="AssetDatabase"/>). In player builds, use <see cref="Resources.Load"/> (or Addressables).
/// </summary>
public static class CreditsBlocks
{
    static readonly Regex RxImageLine = new Regex(
        @"^\s*!\[([^\]]*)\]\(\s*([^)]+?)\s*\)\s*$",
        RegexOptions.Compiled);

    enum Kind { Text, Image }

    struct Piece
    {
        public Kind kind;
        public string richText;
        public float? imageWidth;
        public float? imageHeight;
    }

    /// <param name="editorArtFolderPath">e.g. <c>Assets/Art</c>. If null/empty, only <see cref="Resources"/> is used.</param>
    public static void Populate(
        RectTransform root,
        TextAsset markdown,
        TMP_Text styleSource,
        string editorArtFolderPath = null)
    {
        if (root == null || markdown == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
            Object.Destroy(root.GetChild(i).gameObject);

        EnsureLayoutChain(root);

        var template = styleSource != null ? styleSource : root.GetComponentInChildren<TMP_Text>();
        string raw = CreditsMarkdown.StripHtmlComments(markdown.text ?? string.Empty);
        var pieces = SplitPieces(raw);
        foreach (var p in pieces)
        {
            if (p.kind == Kind.Text)
            {
                if (string.IsNullOrWhiteSpace(p.richText)) continue;
                AddTextBlock(root, p.richText, template);
            }
            else
                AddImageBlock(root, p.richText, p.imageWidth, p.imageHeight, template, editorArtFolderPath);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    static void EnsureLayoutChain(RectTransform root)
    {
        if (root.GetComponent<VerticalLayoutGroup>() == null)
        {
            var v = root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.spacing = 12f;
        }

        if (root.GetComponent<ContentSizeFitter>() == null)
        {
            var f = root.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
    }

    static List<Piece> SplitPieces(string markdown)
    {
        var list = new List<Piece>();
        var sb = new StringBuilder();
        foreach (var segment in markdown.Split('\n'))
        {
            var line = segment.TrimEnd('\r');
            var m = RxImageLine.Match(line.Trim());
            if (m.Success)
            {
                FlushText(list, sb);
                SplitImageLinePayload(m.Groups[2].Value.Trim(), out string path, out float? iw, out float? ih);
                list.Add(new Piece { kind = Kind.Image, richText = path, imageWidth = iw, imageHeight = ih });
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        FlushText(list, sb);
        return list;
    }

    static void FlushText(List<Piece> list, StringBuilder sb)
    {
        var s = sb.ToString();
        sb.Clear();
        if (string.IsNullOrWhiteSpace(s)) return;
        list.Add(new Piece { kind = Kind.Text, richText = s.TrimEnd() });
    }

    /// <summary>First <c>|</c> separates asset path from optional size: <c>480</c> or <c>480x200</c>.</summary>
    static void SplitImageLinePayload(string inner, out string path, out float? width, out float? height)
    {
        path = inner;
        width = null;
        height = null;
        if (string.IsNullOrEmpty(inner)) return;

        int bar = inner.IndexOf('|');
        if (bar < 0) return;

        path = inner[..bar].Trim();
        var spec = inner[(bar + 1)..].Trim();
        if (string.IsNullOrEmpty(spec)) return;

        TryParseImageSizeSpec(spec, out width, out height);
    }

    static void TryParseImageSizeSpec(string spec, out float? width, out float? height)
    {
        width = null;
        height = null;
        if (string.IsNullOrWhiteSpace(spec)) return;

        spec = spec.Trim();
        int xi = spec.ToLowerInvariant().IndexOf('x');
        if (xi >= 0)
        {
            var a = spec[..xi].Trim();
            var b = spec[(xi + 1)..].Trim();
            if (float.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out float fw) &&
                float.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out float fh) &&
                fw > 0f && fh > 0f)
            {
                width = fw;
                height = fh;
            }

            return;
        }

        if (float.TryParse(spec, NumberStyles.Float, CultureInfo.InvariantCulture, out float single) &&
            single > 0f)
            width = single;
    }

    static Sprite LoadSprite(string rawPath, string editorArtFolder)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return null;

        var rel = rawPath.Trim().Replace('\\', '/');
        if (rel.StartsWith("Assets/Art/", System.StringComparison.OrdinalIgnoreCase))
            rel = rel["Assets/Art/".Length..];
        else if (rel.StartsWith("Art/", System.StringComparison.OrdinalIgnoreCase))
            rel = rel["Art/".Length..];

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(editorArtFolder))
        {
            var sp = TryLoadSpriteUnderArtFolder(editorArtFolder.Trim().Replace('\\', '/').TrimEnd('/'), rel);
            if (sp != null) return sp;
        }
#endif
        string key = StripExtensionForResources(rel);
        var single = Resources.Load<Sprite>(key);
        if (single != null) return single;
        var all = Resources.LoadAll<Sprite>(key);
        return all != null && all.Length > 0 ? all[0] : null;
    }

#if UNITY_EDITOR
    /// <summary>Texture importers in &quot;Multiple&quot; mode have no main Sprite; sub-assets must be enumerated.</summary>
    static Sprite FirstSpriteInAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (o is Sprite sp)
                return sp;
        }

        return null;
    }

    static Sprite TryLoadSpriteUnderArtFolder(string artRoot, string relativePath)
    {
        relativePath = relativePath.TrimStart('/');
        string combined = $"{artRoot}/{relativePath}";
        var direct = AssetDatabase.LoadAssetAtPath<Sprite>(combined);
        if (direct != null) return direct;
        var fromMulti = FirstSpriteInAsset(combined);
        if (fromMulti != null) return fromMulti;

        if (Path.HasExtension(combined))
            return null;

        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".PNG", ".JPG", ".JPEG" })
        {
            string p = combined + ext;
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null) return s;
            s = FirstSpriteInAsset(p);
            if (s != null) return s;
        }

        return null;
    }
#endif

    static string StripExtensionForResources(string p)
    {
        if (string.IsNullOrEmpty(p)) return p;
        p = p.TrimStart('/');
        if (p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return p[..^4];
        if (p.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase)) return p[..^4];
        if (p.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase)) return p[..^5];
        return p;
    }

    static void AddTextBlock(RectTransform parent, string markdownChunk, TMP_Text template)
    {
        var go = new GameObject("CreditsTextBlock", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (template != null)
        {
            tmp.font = template.font;
            tmp.fontSize = template.fontSize;
            tmp.color = template.color;
            tmp.alignment = template.alignment;
            tmp.fontStyle = template.fontStyle;
            tmp.characterSpacing = template.characterSpacing;
            tmp.lineSpacing = template.lineSpacing;
            tmp.material = template.fontMaterial;
        }

        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = true;
        tmp.raycastTarget = false;
        tmp.text = CreditsMarkdown.ToTmp(markdownChunk);

        var cf = go.AddComponent<ContentSizeFitter>();
        cf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        cf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
    }

    static float MaxImageDisplayWidth(RectTransform layoutRoot, TMP_Text template)
    {
        var panel = layoutRoot.parent as RectTransform;
        if (panel != null && panel.rect.width > 8f)
            return panel.rect.width * 0.92f;

        if (template != null)
        {
            float tw = template.rectTransform.rect.width;
            // Old setups used huge placeholder widths (e.g. 5000) — ignore for clamping.
            if (tw > 8f && tw < 4000f)
                return tw * 0.92f;
        }

        return 800f;
    }

    static void AddImageBlock(
        RectTransform parent,
        string path,
        float? widthOverride,
        float? heightOverride,
        TMP_Text template,
        string editorArtFolder)
    {
        var sprite = LoadSprite(path, editorArtFolder);
        if (sprite == null)
        {
            AddTextBlock(parent, $"> _(Missing image: `{path}` — put file under `{editorArtFolder ?? "Assets/Art"}/…` in Editor, or Resources for builds.)_", template);
            return;
        }

        var go = new GameObject("CreditsImageBlock", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = Color.white;

        float w = sprite.rect.width;
        float h = sprite.rect.height;
        float maxDisplay = MaxImageDisplayWidth(parent, template);

        if (widthOverride.HasValue && heightOverride.HasValue)
        {
            w = widthOverride.Value;
            h = heightOverride.Value;
        }
        else if (widthOverride.HasValue)
        {
            float tw = widthOverride.Value;
            if (w > 0.01f)
            {
                h *= tw / w;
                w = tw;
            }
            else
                w = tw;
        }
        else if (heightOverride.HasValue)
        {
            float th = heightOverride.Value;
            if (h > 0.01f)
            {
                w *= th / h;
                h = th;
            }
            else
                h = th;
        }
        else if (w > maxDisplay && w > 0.01f)
        {
            float s = maxDisplay / w;
            w *= s;
            h *= s;
        }

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = h;
        le.flexibleWidth = 0f;
    }
}
