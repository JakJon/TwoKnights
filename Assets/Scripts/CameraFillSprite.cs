using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class CameraFillSprite : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    private SpriteRenderer _sr;
    private int _lastWidth = -1;
    private int _lastHeight = -1;

    private void OnEnable()
    {
        _sr = GetComponent<SpriteRenderer>();
        Fit();
    }

    private void LateUpdate()
    {
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
        {
            Fit();
        }
    }

    public void Fit()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr == null || _sr.sprite == null) return;

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        float frustumHeight = cam.orthographic
            ? cam.orthographicSize * 2f
            : 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * cam.aspect;

        transform.localScale = Vector3.one;
        Vector2 spriteSize = _sr.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

        Vector3 scale = new Vector3(frustumWidth / spriteSize.x, frustumHeight / spriteSize.y, 1f);
        transform.localScale = scale;

        Vector3 localCenter = _sr.sprite.bounds.center;
        Vector3 camPos = cam.transform.position;
        transform.position = new Vector3(
            camPos.x - localCenter.x * scale.x,
            camPos.y - localCenter.y * scale.y,
            transform.position.z);
    }
}
