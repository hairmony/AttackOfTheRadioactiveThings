using UnityEngine;

/// <summary>2D orthographic camera: world points just outside the visible frustum (not at diagonal distance).</summary>
public static class OffScreenSpawn2D
{
    /// <summary>
    /// Random point slightly past one viewport edge — stays near the playable band instead of megaworlds away.
    /// </summary>
    public static Vector2 RandomBeyondView(Camera cam, float marginWorld)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return Vector2.zero;

        marginWorld = Mathf.Max(0.05f, marginWorld);
        Vector2 c = cam.transform.position;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        // how far past the edge (world units) — keeps spawns in a thin shell
        float past = Random.Range(marginWorld * 0.35f, marginWorld * 1.1f);
        // small stretch along parallel axis so not always middle-of-edge
        float alongPad = marginWorld * 0.35f;

        int edge = Random.Range(0, 4);
        switch (edge)
        {
            case 0: // right
                return new Vector2(c.x + halfW + past,
                    c.y + Random.Range(-halfH - alongPad, halfH + alongPad));
            case 1: // left
                return new Vector2(c.x - halfW - past,
                    c.y + Random.Range(-halfH - alongPad, halfH + alongPad));
            case 2: // top
                return new Vector2(c.x + Random.Range(-halfW - alongPad, halfW + alongPad),
                    c.y + halfH + past);
            default: // bottom
                return new Vector2(c.x + Random.Range(-halfW - alongPad, halfW + alongPad),
                    c.y - halfH - past);
        }
    }

    public static bool IsBeyondView(Vector2 world, Camera cam, float marginWorld)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return true;

        marginWorld = Mathf.Max(0f, marginWorld);
        Vector2 c = cam.transform.position;
        float halfH = cam.orthographicSize + marginWorld;
        float halfW = cam.orthographicSize * cam.aspect + marginWorld;
        return Mathf.Abs(world.x - c.x) > halfW || Mathf.Abs(world.y - c.y) > halfH;
    }
}
