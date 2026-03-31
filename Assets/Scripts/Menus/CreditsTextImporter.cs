using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Loads credits from a Markdown <see cref="TextAsset"/>.
/// If <see cref="blocksLayoutRoot"/> is set, builds text + images (see <see cref="CreditsBlocks"/>); otherwise fills a single <see cref="TMP_Text"/>.
/// </summary>
[DefaultExecutionOrder(-200)]
public class CreditsTextImporter : MonoBehaviour
{
    [Tooltip("Your credits Markdown file (.md). Drag it from the Project window.")]
    [FormerlySerializedAs("creditsFile")]
    [SerializeField] TextAsset creditsMarkdown;

    [Tooltip("Optional: RectTransform for block layout (VerticalLayoutGroup). Use for images — see CreditsBlocks. Assign CreditsScroll’s content here.")]
    [SerializeField] RectTransform blocksLayoutRoot;

    [Tooltip("Which TextMeshPro to fill (single-text mode), or style template for block mode (font, size, color).")]
    [SerializeField] TMP_Text creditsLabel;

    [Header("Credits images (block mode)")]
    [Tooltip("In the Editor, try AssetDatabase under this folder first. Markdown paths are relative to it, e.g. ![](Logos/studio) → Assets/Art/Logos/studio.png")]
    [SerializeField] bool loadImagesFromAssetsArt = true;

    [SerializeField] string artFolderPath = "Assets/Art";

    void OnEnable()
    {
        ApplyCreditsText();
    }

#if UNITY_EDITOR
    [ContextMenu("Apply credits from Markdown now")]
    void ApplyCreditsTextEditor() => ApplyCreditsText();
#endif

    void ApplyCreditsText()
    {
        if (creditsMarkdown == null) return;

        var tmp = creditsLabel != null ? creditsLabel : GetComponent<TMP_Text>();

        if (blocksLayoutRoot != null)
        {
            string artRoot = loadImagesFromAssetsArt && !string.IsNullOrWhiteSpace(artFolderPath)
                ? artFolderPath
                : null;
            CreditsBlocks.Populate(blocksLayoutRoot, creditsMarkdown, tmp, artRoot);
            if (tmp != null)
            {
                // Template on the same GO as blocksLayoutRoot: keep GO active (children live under it).
                if (tmp.rectTransform == blocksLayoutRoot)
                    tmp.enabled = false;
                else
                    tmp.gameObject.SetActive(false);
            }

            return;
        }

        if (tmp == null)
        {
            Debug.LogWarning($"{nameof(CreditsTextImporter)}: assign {nameof(blocksLayoutRoot)} + {nameof(creditsLabel)} for style, or {nameof(creditsLabel)}/TMP on this GameObject for text-only.", this);
            return;
        }

        tmp.gameObject.SetActive(true);
        string body = creditsMarkdown.text ?? string.Empty;
        body = CreditsMarkdown.ToTmp(body);

        tmp.richText = true;
        tmp.text = body;
    }
}
