using System.Collections.Generic;
using UnityEngine;

// Reusable movement patterns for wolves (or any enemy) as world-space templates centered at origin.
// Waves can reference these directly and optionally Offset/Scale them.
public static class WolfMovementPatterns
{
    public static readonly List<Vector2> CounterclockwiseCircle;
    public static readonly List<Vector2> ClockwiseCircle;
    public static readonly List<Vector2> RectangleLoopCW;
    public static readonly List<Vector2> HorizontalSweep;
    public static readonly List<Vector2> CircleLeftThenRight;
    public static readonly Vector2 aboveLeftPLayer = new Vector2(-2, 7);
    public static readonly Vector2 aboveRightPlayer = new Vector2(2, 7);
    public static readonly Vector2 belowLeftPlayer = new Vector2(-2, -7);
    public static readonly Vector2 belowRightPlayer = new Vector2(2, -7);
    public static readonly Vector2 leftOfLeftPlayer = new Vector2(-12, 0);
    public static readonly Vector2 rightOfRightPlayer = new Vector2(12, 0);
    public static readonly Vector2 topLeftCorner = new Vector2(-12, 6);
    public static readonly Vector2 topRightCorner = new Vector2(12, 6);
    public static readonly Vector2 bottomLeftCorner = new Vector2(-12, -6);
    public static readonly Vector2 bottomRightCorner = new Vector2(12, -6);

    static WolfMovementPatterns()
    {
        CounterclockwiseCircle = GenerateCircle(radius: 6f, points: 16, cw: true, center: Vector2.zero);
        ClockwiseCircle = GenerateCircle(radius: 6f, points: 16, cw: false, center: Vector2.zero);
        RectangleLoopCW = GenerateRectangle(width: 20f, height: 8f, pointsPerSide: 4, clockwise: true);
        HorizontalSweep = new List<Vector2>
        {
            new Vector2(-12f, 2.5f),
            new Vector2(12f, 2.5f),
            new Vector2(12f, -1.5f),
            new Vector2(-12f, -1.5f)
        };

        var circleCenterLeft = new Vector2(-2, 2.5f);
        CircleLeftThenRight = new List<Vector2>();
        CircleLeftThenRight.AddRange(GenerateCircle(radius: 2.5f, points: 12, cw: false, center: new Vector2(-2, 3) + new Vector2(0, -3.5f), startAngleDegFromTop: 0f));
        CircleLeftThenRight.AddRange(new[] { circleCenterLeft, new Vector2(2, 2.5f) });
        CircleLeftThenRight.AddRange(GenerateCircle(radius: 2.5f, points: 12, cw: true, center: new Vector2(2, 3) + new Vector2(0, -3.5f), startAngleDegFromTop: 0f));
        CircleLeftThenRight.AddRange(new[] { new Vector2(2, 2.5f), circleCenterLeft });
    }

    // Utility: apply an offset to a pattern
    public static List<Vector2> Offset(List<Vector2> pattern, Vector2 offset)
    {
        if (pattern == null) return null;
        var list = new List<Vector2>(pattern.Count);
        for (int i = 0; i < pattern.Count; i++) list.Add(pattern[i] + offset);
        return list;
    }

    // Utility: scale a pattern uniformly
    public static List<Vector2> Scale(List<Vector2> pattern, float scale)
    {
        if (pattern == null) return null;
        var list = new List<Vector2>(pattern.Count);
        for (int i = 0; i < pattern.Count; i++) list.Add(pattern[i] * scale);
        return list;
    }

    // Single circle generator: includes center and start angle.
    // Angle is measured from the top (0 deg = center + Vector2.up * radius). Positive angles rotate CCW.
    private static List<Vector2> GenerateCircle(float radius, int points, bool cw, Vector2 center, float startAngleDegFromTop = 0f)
    {
        if (points < 3) points = 3;
        var list = new List<Vector2>(points);
        float dir = cw ? 1f : -1f;
        float step = Mathf.PI * 2f / points;
        float start = startAngleDegFromTop * Mathf.Deg2Rad;
        for (int i = 0; i < points; i++)
        {
            float ang = start + dir * step * i;
            // Using angle-from-top convention: x=sin, y=cos produces (0,1) at 0 degrees
            list.Add(center + new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * radius);
        }
        return list;
    }

    private static List<Vector2> GenerateRectangle(float width, float height, int pointsPerSide, bool clockwise)
    {
        // Rectangle centered at origin
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;
        var corners = new List<Vector2>
        {
            new Vector2(-halfW,  halfH), // TL
            new Vector2( halfW,  halfH), // TR
            new Vector2( halfW, -halfH), // BR
            new Vector2(-halfW, -halfH), // BL
        };
        if (!clockwise)
        {
            corners.Reverse();
        }

        if (pointsPerSide <= 1)
        {
            return corners;
        }

        var list = new List<Vector2>(pointsPerSide * 4);
        for (int c = 0; c < 4; c++)
        {
            Vector2 a = corners[c];
            Vector2 b = corners[(c + 1) % 4];
            for (int i = 0; i < pointsPerSide; i++)
            {
                float t = (float)i / pointsPerSide;
                list.Add(Vector2.Lerp(a, b, t));
            }
        }
        return list;
    }
}
