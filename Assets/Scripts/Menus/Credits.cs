using UnityEngine;

public class CreditsScroll : MonoBehaviour
{
    public float startY = 0;
    public float scrollSpeed = 2;

    void OnEnable()
    {
        Vector3 pos = transform.localPosition;
        pos.y = startY;
        transform.localPosition = pos;
    }

    void Update()
    {
        Vector3 pos = transform.localPosition;
        pos.y += scrollSpeed * Time.deltaTime;
        transform.localPosition = pos;
    }
}
