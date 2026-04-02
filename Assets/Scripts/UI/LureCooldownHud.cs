using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows lure readiness and cooldown from <see cref="PlayerLure"/> (radial fill + optional seconds text).
/// </summary>
[DisallowMultipleComponent]
public class LureCooldownHud : MonoBehaviour
{
    [SerializeField] PlayerLure lure;
    [Tooltip("Image Type should be Filled, Fill Method Radial 360. Fill amount shows remaining cooldown.")]
    [SerializeField] Image cooldownFillRadial;
    [SerializeField] Text cooldownSecondsText;
    [SerializeField] bool hideEntireHudIfNoLure = true;

    void Start()
    {
        TryBindLure();
        if (hideEntireHudIfNoLure && lure != null && !lure.HasLurePrefab)
            gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (lure == null)
            TryBindLure();

        if (lure == null || !lure.HasLurePrefab)
        {
            if (cooldownFillRadial != null)
                cooldownFillRadial.enabled = false;
            if (cooldownSecondsText != null)
                cooldownSecondsText.text = string.Empty;
            return;
        }

        float rem = lure.CooldownRemainingUnscaled;
        float total = lure.CooldownDuration;

        if (cooldownFillRadial != null)
        {
            bool onCd = rem > 0.02f;
            cooldownFillRadial.enabled = onCd;
            cooldownFillRadial.fillAmount = total > 0.001f ? Mathf.Clamp01(rem / total) : 0f;
        }

        if (cooldownSecondsText != null)
            cooldownSecondsText.text = rem > 0.05f ? rem.ToString("0.0") : string.Empty;
    }

    void TryBindLure()
    {
        if (lure == null)
            lure = FindFirstObjectByType<PlayerLure>();
    }
}
