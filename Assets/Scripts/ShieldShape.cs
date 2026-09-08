using System.Collections.Generic;
using UnityEngine;

// Guardian order: the shield's silhouette is what the upgrades buy. Length stretches
// the bar along its face; curve bows it back around the knight so it covers a wider
// slice of the orbit circle. Sprite and collider are regenerated from the same arc, so
// the shape that stops an arrow is exactly the shape on screen.
//
// Lives on the shield GameObject (tagged "Shield", carrying ShieldOrbit). Tower Shield
// and Curved Aegis each add it on demand and can arrive in either order.
public class ShieldShape : MonoBehaviour
{
    // Mirrors the authored shield sprites: a 6x32 px bar sitting 1 px outboard of the
    // canvas centre, imported at 32 px/unit. At length 1 with no curve the body colour
    // and the whole emblem — the knight's band and the gold diamond around it — come
    // out pixel for pixel, so a reshaped shield still reads as that knight's rather
    // than turning into an anonymous slab. The last three rows at each tip are close
    // but not identical: the authored bar tapers 4-5-6 px wide and carries eight bevel
    // dots there, where the generated cap goes 4-6 and has none. Nothing can be done
    // about that from here — the tips are wherever the upgrades put them.
    private const float PixelsPerUnit = 32f;
    private const float BarThicknessPixels = 6f;
    private const float BarLengthPixels = 32f;
    private const float BarOutboardOffsetPixels = 1f;
    private const float CornerRadiusPixels = 2f; // reproduces the authored cap taper
    private const float OutlinePixels = 1f;
    private const int PaddingPixels = 2;

    private static readonly Color32 OutlineColor = new Color32(0, 0, 0, 255);
    private static readonly Color32 BodyColor = new Color32(48, 57, 79, 255);      // #30394F
    private static readonly Color32 TrimColor = new Color32(212, 162, 74, 255);    // #D4A24A
    private static readonly Color32 LeftBandColor = new Color32(156, 56, 40, 255); // #9C3828
    private static readonly Color32 RightBandColor = new Color32(46, 111, 216, 255); // #2E6FD8
    private static readonly Color32 TransparentColor = new Color32(0, 0, 0, 0);

    // The authored shield carries a diamond of colour at its midpoint: a band in the
    // knight's own colour, tipped above and below with gold. In the bar's own frame
    // that figure is exactly |along| + |across| <= 4, with the outermost ring of the
    // diamond gold and everything inside it the band — which is why two comparisons
    // reproduce shield.aseprite here rather than a lookup table. It is anchored to the
    // midpoint and sized in pixels, so a longer shield carries the same emblem rather
    // than a stretched one.
    private const float BandRadiusPixels = 3.5f;
    private const float TrimRadiusPixels = 4.5f;

    // Which knight this shield hangs off, resolved once from the ancestor tag.
    private Color32 _bandColor = LeftBandColor;

    // Arc sampling: enough segments that the collider outline reads as a smooth bow
    private const int ArcSamples = 24;

    private SpriteRenderer _renderer;
    private CapsuleCollider2D _capsule;
    private PolygonCollider2D _polygon;
    private Texture2D _texture;
    private Sprite _sprite;
    private bool _initialised;

    // Collider baseline lifted off the prefab so the authored padding survives upgrades
    private float _colliderHalfThickness;
    private float _colliderLengthRatio = 1f;
    private Vector2 _colliderCentre;

    private float _lengthMultiplier = 1f;
    private float _curveRadius; // shield-local units; 0 = straight, smaller = tighter bow

    public float LengthMultiplier => _lengthMultiplier;
    public float CurveRadius => _curveRadius;

    // Straight-line reach from tip to tip, in shield-local units. This is the figure the
    // upgrade buys and the one a player reads as "length" — it stays put when the shield
    // bows, so curving is pure gain rather than a trade.
    private float SpanUnits => BarLengthPixels / PixelsPerUnit * _lengthMultiplier;

    // Total angle of the orbit circle the shield wraps, in degrees. A straight bar wraps
    // nothing, so it reads 0.
    public float ArcDegrees => _curveRadius <= 0f
        ? 0f
        : ArcLengthForSpan(SpanUnits, _curveRadius) / _curveRadius * Mathf.Rad2Deg;

    // Absolute, not incremental: a tier states the shape it wants outright, so
    // re-applying one can't compound.
    public void SetShape(float lengthMultiplier, float curveRadius)
    {
        _lengthMultiplier = Mathf.Max(0.1f, lengthMultiplier);
        _curveRadius = Mathf.Max(0f, curveRadius);
        Rebuild();
    }

    // Bending a bar of fixed length drags its tips inward and costs span, so instead the
    // arc is grown until the chord across it matches the span the tier paid for:
    // chord = 2R*sin(L / 2R)  =>  L = 2R*asin(chord / 2R).
    private static float ArcLengthForSpan(float span, float radius)
    {
        if (radius <= 0f) return span;

        float sine = span / (2f * radius);
        // A bow can't span more than its own diameter; past that it's a half-circle
        if (sine >= 1f) return radius * Mathf.PI;

        return 2f * radius * Mathf.Asin(sine);
    }

    private void EnsureInitialised()
    {
        if (_initialised) return;
        _initialised = true;

        _renderer = GetComponent<SpriteRenderer>();
        _capsule = GetComponent<CapsuleCollider2D>();
        _bandColor = ResolveBandColor();

        if (_capsule != null)
        {
            // The authored capsule is deliberately more generous than the sprite; keep
            // that padding proportional instead of hard-coding a hitbox here.
            _colliderHalfThickness = _capsule.size.x * 0.5f;
            _colliderLengthRatio = _capsule.size.y / (BarLengthPixels / PixelsPerUnit);
            _colliderCentre = _capsule.offset;
        }
        else
        {
            _colliderHalfThickness = BarThicknessPixels * 0.5f / PixelsPerUnit;
            _colliderCentre = new Vector2(BarOutboardOffsetPixels / PixelsPerUnit, 0f);
        }
    }

    // The shield hangs somewhere under its knight, so the tag is read off whichever
    // ancestor carries one. Falling back to red matches the shield art as it stood
    // before the right knight was repainted, which is the least surprising answer for
    // anything that ends up holding a shield without being a tagged knight.
    private Color32 ResolveBandColor()
    {
        for (Transform t = transform; t != null; t = t.parent)
        {
            if (t.CompareTag("PlayerRight")) return RightBandColor;
            if (t.CompareTag("PlayerLeft")) return LeftBandColor;
        }

        return LeftBandColor;
    }

    private void Rebuild()
    {
        EnsureInitialised();

        // Everything downstream works in arc length; the span-preserving correction is
        // applied once, here, so the sprite and the collider can't disagree about it.
        float arcUnits = ArcLengthForSpan(SpanUnits, _curveRadius);
        float halfLengthPixels = arcUnits * PixelsPerUnit * 0.5f;
        float radiusPixels = _curveRadius * PixelsPerUnit;

        RebuildSprite(halfLengthPixels, radiusPixels);
        RebuildCollider(_curveRadius);
    }

    // ---- Visual ----

    private void RebuildSprite(float halfLength, float radius)
    {
        if (_renderer == null) return;

        float halfThickness = BarThicknessPixels * 0.5f;
        CentreLineBounds(halfLength, radius, halfThickness,
            out float minX, out float minY, out float maxX, out float maxY);

        // Snapping the origin to whole pixels keeps the generated grid in phase with
        // every other sprite in the scene — off-grid art shimmers when the shield turns.
        int originX = Mathf.FloorToInt(minX);
        int originY = Mathf.FloorToInt(minY);
        int width = Mathf.CeilToInt(maxX) - originX;
        int height = Mathf.CeilToInt(maxY) - originY;

        var pixels = new Color32[width * height];
        for (int j = 0; j < height; j++)
        {
            float y = originY + j + 0.5f;
            for (int i = 0; i < width; i++)
            {
                float x = originX + i + 0.5f;
                float distance = BarDistance(new Vector2(x, y), halfLength, radius, halfThickness,
                                             out float across, out float along);

                Color32 colour;
                if (distance > 0f)
                {
                    colour = TransparentColor;
                }
                else if (distance > -OutlinePixels)
                {
                    colour = OutlineColor;
                }
                else
                {
                    float diamond = Mathf.Abs(along) + Mathf.Abs(across);
                    colour = diamond <= BandRadiusPixels ? _bandColor
                           : diamond <= TrimRadiusPixels ? TrimColor
                           : BodyColor;
                }

                pixels[j * width + i] = colour;
            }
        }

        DisposeGenerated();

        _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        _texture.SetPixels32(pixels);
        _texture.Apply();

        // Pivot back onto the transform origin so the shield's face stays at the same
        // orbit radius however the bitmap around it grows.
        var pivot = new Vector2(-originX / (float)width, -originY / (float)height);
        _sprite = Sprite.Create(_texture, new Rect(0f, 0f, width, height), pivot,
            PixelsPerUnit, 0, SpriteMeshType.FullRect);
        _renderer.sprite = _sprite;
    }

    // Signed distance from a point to the bar, in pixels. The point is first unrolled
    // into the bar's own frame — `across` through the thickness, `along` as arc length
    // from the midpoint — so straight and curved bars share one rasteriser and their
    // caps come out identical.
    private static float BarDistance(Vector2 point, float halfLength, float radius, float halfThickness)
    {
        return BarDistance(point, halfLength, radius, halfThickness, out _, out _);
    }

    // The unrolled coordinates come back out because the rasteriser paints the band and
    // its gold trim in this frame — the emblem has to bow with the bar, not sit in a
    // fixed rectangle of the bitmap.
    private static float BarDistance(Vector2 point, float halfLength, float radius, float halfThickness,
                                     out float across, out float along)
    {
        if (radius <= 0f)
        {
            across = point.x - BarOutboardOffsetPixels;
            along = point.y;
        }
        else
        {
            var centre = new Vector2(BarOutboardOffsetPixels - radius, 0f);
            Vector2 relative = point - centre;
            float halfSweep = halfLength / radius;
            float angle = Mathf.Atan2(relative.y, relative.x);

            if (Mathf.Abs(angle) <= halfSweep)
            {
                across = relative.magnitude - radius;
                along = angle * radius;
            }
            else
            {
                // Past an end: keep unrolling along the tangent so the cap is the same
                // rounded-rectangle end a straight bar gets.
                float sign = Mathf.Sign(angle);
                float endAngle = sign * halfSweep;
                var outward = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
                var tangent = new Vector2(-outward.y, outward.x) * sign;
                Vector2 fromEnd = relative - outward * radius;
                across = Vector2.Dot(fromEnd, outward);
                along = sign * (halfLength + Vector2.Dot(fromEnd, tangent));
            }
        }

        return RoundedRectDistance(across, along, halfThickness, halfLength, CornerRadiusPixels);
    }

    private static float RoundedRectDistance(float across, float along, float halfAcross, float halfAlong, float radius)
    {
        float qx = Mathf.Abs(across) - (halfAcross - radius);
        float qy = Mathf.Abs(along) - (halfAlong - radius);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - radius;
    }

    // Bounding box of the bar's centre line, then fattened by the thickness. Sampling
    // rather than solving keeps this correct for any sweep the tiers ask for.
    private static void CentreLineBounds(float halfLength, float radius, float halfThickness,
        out float minX, out float minY, out float maxX, out float maxY)
    {
        minX = minY = float.MaxValue;
        maxX = maxY = float.MinValue;

        for (int i = 0; i <= ArcSamples; i++)
        {
            Vector2 point = CentreLinePoint(i / (float)ArcSamples, halfLength, radius);
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }

        float margin = halfThickness + PaddingPixels;
        minX -= margin;
        minY -= margin;
        maxX += margin;
        maxY += margin;
    }

    private static Vector2 CentreLinePoint(float t, float halfLength, float radius)
    {
        if (radius <= 0f)
            return new Vector2(BarOutboardOffsetPixels, Mathf.Lerp(-halfLength, halfLength, t));

        float halfSweep = halfLength / radius;
        float angle = Mathf.Lerp(-halfSweep, halfSweep, t);
        var centre = new Vector2(BarOutboardOffsetPixels - radius, 0f);
        return centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    // ---- Collider ----

    private void RebuildCollider(float radiusUnits)
    {
        // Solve the collider's arc from the collider's own span target rather than
        // scaling the visual arc: stretching an arc length does not stretch the chord it
        // spans by the same factor, which would quietly leave the hitbox short.
        float halfLength = ArcLengthForSpan(SpanUnits * _colliderLengthRatio, radiusUnits) * 0.5f;
        float halfThickness = _colliderHalfThickness;

        if (_polygon == null)
        {
            _polygon = gameObject.AddComponent<PolygonCollider2D>();
            _polygon.isTrigger = true;
        }
        // One trigger only, or every block would fire its callbacks twice
        if (_capsule != null) _capsule.enabled = false;

        List<Vector2> path;
        if (radiusUnits <= 0f)
        {
            path = new List<Vector2>
            {
                _colliderCentre + new Vector2(-halfThickness, -halfLength),
                _colliderCentre + new Vector2(halfThickness, -halfLength),
                _colliderCentre + new Vector2(halfThickness, halfLength),
                _colliderCentre + new Vector2(-halfThickness, halfLength)
            };
        }
        else
        {
            // Trace the outboard face one way and the inboard face back, closing the band
            float clampedRadius = Mathf.Max(radiusUnits, halfThickness * 1.5f);
            float halfSweep = halfLength / clampedRadius;
            Vector2 centre = _colliderCentre + new Vector2(-clampedRadius, 0f);

            path = new List<Vector2>((ArcSamples + 1) * 2);
            for (int i = 0; i <= ArcSamples; i++)
            {
                float angle = Mathf.Lerp(-halfSweep, halfSweep, i / (float)ArcSamples);
                path.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (clampedRadius + halfThickness));
            }
            for (int i = ArcSamples; i >= 0; i--)
            {
                float angle = Mathf.Lerp(-halfSweep, halfSweep, i / (float)ArcSamples);
                path.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (clampedRadius - halfThickness));
            }
        }

        _polygon.pathCount = 1;
        _polygon.SetPath(0, path);
        _polygon.offset = Vector2.zero;
    }

    private void DisposeGenerated()
    {
        if (_sprite != null) Destroy(_sprite);
        if (_texture != null) Destroy(_texture);
        _sprite = null;
        _texture = null;
    }

    private void OnDestroy()
    {
        DisposeGenerated();
    }
}
