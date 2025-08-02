using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private RectTransform _barRect;
    [SerializeField] private RectMask2D _mask;
    [SerializeField] private TMP_Text _hpIndicator;

    private int _maxHealth;
    private float _maxRightMask;
    private float _initialRightMask;

    public void Initialize(int maxHealth)
    {
        // x = left, w = top, y = bottom, z = right
        _maxHealth = maxHealth;
        _maxRightMask = _barRect.rect.width - _mask.padding.x;
        _hpIndicator.SetText($"{_maxHealth} / {_maxHealth}");
        _initialRightMask = _mask.padding.z;
    }

    public void SetValue(int newValue)
    {
        var targetWidth = newValue * _maxRightMask / _maxHealth;
        var newRightMask = _maxRightMask + _initialRightMask - targetWidth;
        var padding = _mask.padding;
        padding.z = newRightMask;
        _mask.padding = padding;
        _hpIndicator.SetText($"{newValue} / {_maxHealth}");

    }
}
