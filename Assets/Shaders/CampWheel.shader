// The camp menu's ring, drawn as one quad on the ground around the fire.
//
// Three things in one pass:
//   * the BAND — a see-through wash on the ground the menu items sit on. It
//     has no edge on either side. Nearest the knights it thins IN over
//     _InnerFade from _InnerRadius, and past _FadeRadius it thins out to
//     nothing at _OuterRadius, both on the dither. There is deliberately no
//     circle drawn round the knights (owner, 2026-10-13), and a hard inner edge
//     would draw one out of the glow. The outer side has none because the items
//     sit almost as far out as the frame is tall, and a hard edge there would
//     be cut off by the top and bottom of the screen;
//   * the DIVIDERS — one-pixel spokes where each item's slice begins and ends.
//     Spokes only: there is no rim;
//   * the GLOW — light on the band that points exactly where the left stick
//     points, strongest dead on that bearing and falling away either side.
//     CampfireMenu sets _Angle every frame and breathes _GlowStrength while the
//     stick is at rest.
//
// The slice the glow is in is lifted a little as a whole, so which item is
// picked stays readable even with the glow hard against a divider.
//
// Premultiplied, like Custom/CanopyLight:
//   result = src.rgb + dst * (1 - src.a)
// so the wash darkens through alpha while the glow and the slice light are
// added on top with no alpha of their own.
//
// Everything is worked out on the art's pixel grid (see _PixelSize) and the glow
// is ordered-dithered, so it steps in the same quanta as the sprites around it.
// Built like Custom/CanopyLight for the same reason that one is: plain
// CGPROGRAM, no LightMode tag, the shape already proven to draw in this
// project's URP 2D renderer.
Shader "Custom/CampWheel"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Band)]
        _Center ("Centre (world xy)", Vector) = (0, -0.3, 0, 0)
        _InnerRadius ("Inner Radius (world)", Float) = 3.2
        _InnerFade ("Thins In Over (world)", Float) = 0.6
        _FadeRadius ("Starts Thinning Out At (world)", Float) = 5.3
        _OuterRadius ("Gone By (world)", Float) = 6
        _BandColor ("Band Colour (alpha = how much it covers)", Color) = (0.01, 0.01, 0.02, 0.3)

        [Header(Dividers)]
        _LineColor ("Divider Colour (alpha = strength)", Color) = (1, 0.82, 0.5, 0.2)

        [Header(Selected slice)]
        _SelColor ("Slice Light (alpha = strength)", Color) = (1, 0.72, 0.36, 0.05)
        _SelStart ("Slice Start (radians)", Float) = 1.18
        _SelEnd ("Slice End (radians)", Float) = 2.36

        [Header(Glow)]
        _GlowColor ("Glow Colour", Color) = (1, 0.72, 0.36, 1)
        _GlowStrength ("Glow Strength", Range(0, 1)) = 0.3
        _GlowSpread ("Glow Spread (radians)", Range(0.05, 1.5)) = 0.4
        _Angle ("Glow Bearing (radians)", Float) = 1.5708

        [Header(Pixel grid)]
        _PixelSize ("World Units Per Art Pixel", Float) = 0.03125
        _Levels ("Glow Dither Levels", Range(4, 256)) = 24

        _Master ("Master Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
        }
        LOD 100

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            // One spoke per boundary between two items. Eight is every slot the
            // ring has: the five menu items plus the three hidden dev ones.
            #define MAX_DIVIDERS 8

            struct appdata_t
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 world  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _Center;
            float  _InnerRadius;
            float  _InnerFade;
            float  _FadeRadius;
            float  _OuterRadius;
            float4 _BandColor;

            float4 _LineColor;
            float  _Dividers[MAX_DIVIDERS];
            float  _DividerCount;

            float4 _SelColor;
            float  _SelStart;
            float  _SelEnd;

            float4 _GlowColor;
            float  _GlowStrength;
            float  _GlowSpread;
            float  _Angle;

            float  _PixelSize;
            float  _Levels;
            float  _Master;

            #define TAU 6.2831853

            // Into -pi..pi: the short way round from one bearing to another
            float wrapSigned(float a)
            {
                return a - TAU * floor((a + 3.14159265) / TAU);
            }

            // Into 0..2pi: how far round, anticlockwise, from a starting bearing
            float wrapTurn(float a)
            {
                return a - TAU * floor(a / TAU);
            }

            // Bayer 4x4 in closed form — the same matrix CanopyLight dithers on
            float bayer2(float2 a)
            {
                a = floor(a);
                return frac(a.x * 0.5 + a.y * a.y * 0.75);
            }

            float bayer4(float2 a)
            {
                return bayer2(0.5 * a) * 0.25 + bayer2(a);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.world = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            float4 frag(v2f IN) : SV_Target
            {
                // Snap to the art's pixel grid first, so the ring's edges, its
                // spokes and every step of the glow land on whole art pixels
                float2 cell = floor(IN.world / _PixelSize);
                float2 p = (cell + 0.5) * _PixelSize;

                float2 rel = p - _Center.xy;
                float r = length(rel);

                if (r < _InnerRadius || r > _OuterRadius) return float4(0, 0, 0, 0);

                // Neither side of the band is an edge. Whole art pixels drop out
                // on the dither as it thins, so it breaks up the way hand-placed
                // pixels would instead of going soft — and nothing is left that
                // reads as a circle round the knights.
                float dither = bayer4(cell);
                float thinIn = smoothstep(_InnerRadius, _InnerRadius + max(_InnerFade, 0.001), r);
                float thinOut = 1.0 - smoothstep(_FadeRadius, _OuterRadius, r);
                float thin = min(thinIn, thinOut);
                float band = step(dither, thin * 0.999);
                if (band <= 0.0) return float4(0, 0, 0, 0);

                float bearing = atan2(rel.y, rel.x);
                // Measured across the solid part only, so the glow sits where the
                // items do and not out in the thinning fringe
                float across = saturate((r - _InnerRadius) / max(_FadeRadius - _InnerRadius, 0.001));

                // ---- the glow: a lobe about the stick's bearing
                float off = wrapSigned(bearing - _Angle);
                float lobe = exp(-(off * off) / max(2.0 * _GlowSpread * _GlowSpread, 0.0001));
                // Fullest through the middle of the band and easing toward its two
                // rims, so it reads as light pooled on the ring and not as a wedge
                // cut out of it
                float body = smoothstep(0.0, 0.3, across) * (1.0 - smoothstep(0.65, 1.0, across));
                float glow = lobe * (0.3 + 0.7 * body) * _GlowStrength * _Master;
                glow = floor(glow * _Levels + dither) / _Levels;

                // ---- the picked slice, lifted as a whole
                float span = wrapTurn(_SelEnd - _SelStart);
                float inSlice = step(wrapTurn(bearing - _SelStart), span);

                // ---- spokes, one art pixel wide
                float halfPixel = _PixelSize * 0.5;
                float spoke = 0.0;
                for (int i = 0; i < MAX_DIVIDERS; i++)
                {
                    // Every slot is worked out and the unused ones multiplied
                    // away, rather than leaving the loop early: a fixed-length
                    // loop is the one shape every shader compiler agrees on
                    float live = step(i + 0.5, _DividerCount);
                    float2 dir = float2(cos(_Dividers[i]), sin(_Dividers[i]));
                    float along = dot(rel, dir);
                    float side = abs(dot(rel, float2(-dir.y, dir.x)));
                    spoke = max(spoke, live * step(0.0, along) * step(side, halfPixel));
                }
                float lineAlpha = spoke * _LineColor.a * _Master;

                // ---- put it together, premultiplied
                float wash = _BandColor.a * _Master;
                float3 rgb = _BandColor.rgb * wash;
                float alpha = wash;

                rgb += _SelColor.rgb * (_SelColor.a * inSlice * _Master);
                rgb += _GlowColor.rgb * glow;

                rgb = rgb * (1.0 - lineAlpha) + _LineColor.rgb * lineAlpha;
                alpha = alpha * (1.0 - lineAlpha) + lineAlpha;

                return float4(rgb, alpha);
            }
            ENDCG
        }
    }
}
