using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialBar : MonoBehaviour
{
    [SerializeField] private RectTransform _barRect;
    [SerializeField] private RectMask2D _mask;
    [SerializeField] private TMP_Text _specialIndicator;
    
    [SerializeField] public TMP_Text _multiplierIndicator;

    private int _maxSpecial;
    private float _maxRightMask;
    private float _initialRightMask;

    public void Initialize(int maxSpecial)
    {
        // x = left, w = top, y = bottom, z = right
        _maxSpecial = maxSpecial;
        _maxRightMask = _barRect.rect.width - _mask.padding.x;
        _specialIndicator.SetText("0%");
        _initialRightMask = _mask.padding.z;
    }

    public void SetValue(int newValue)
    {
        var targetWidth = newValue * _maxRightMask / _maxSpecial;
        var newRightMask = _maxRightMask - targetWidth + _initialRightMask;
        var padding = _mask.padding;
        padding.z = newRightMask;
        _mask.padding = padding;

        float percent = (float)newValue / _maxSpecial * 100f;
        _specialIndicator.SetText($"{newValue} / 1000");
    }
}
