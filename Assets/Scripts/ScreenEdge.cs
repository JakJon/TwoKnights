using UnityEngine;

/// <summary>
/// "Has this gone off screen yet", asked of the main camera. For things that leave
/// the field on their own and are tidied up the moment the player can no longer see
/// them: a body Heavy Swing threw, a bomb a sword sent rolling back.
/// </summary>
public static class ScreenEdge
{
    /// <summary>True once <paramref name="bounds"/> lies wholly outside the main
    /// camera's view. False when there is no orthographic camera to ask, so callers
    /// keep a lifetime backstop of their own.</summary>
    public static bool IsPast(Bounds bounds)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return false;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector2 centre = cam.transform.position;

        return bounds.max.x < centre.x - halfW || bounds.min.x > centre.x + halfW
            || bounds.max.y < centre.y - halfH || bounds.min.y > centre.y + halfH;
    }
}
