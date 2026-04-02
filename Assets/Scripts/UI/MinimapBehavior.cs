using UnityEngine;

/// <summary>
/// Keeps a minimap orthographic camera aligned to world axes so the map always faces "north"
/// (+Y up on the render texture), even when parented under a rotating player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class MinimapBehavior : MonoBehaviour
{
    void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
    }
}
