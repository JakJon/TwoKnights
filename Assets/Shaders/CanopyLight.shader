// The forest canopy, drawn as a single overlay quad.
//
// The arena is lit from above by sun that has to get through a roof of leaves.
// This shader answers one question per pixel — how much roof is over this spot —
// and turns the answer into two things at once: shade the ground is lerped
// toward, and a little warm light where the leaves let the sun through.
//
// Both come out of one pass thanks to premultiplied blending:
//   result = src.rgb + dst * (1 - src.a)
// so writing a = shade and rgb = shadeColour*shade + sun gives
//   lerp(dst, shadeColour, shade) + sun
// in a single draw. Shade lerps toward a COLOUR rather than multiplying toward
// black, which is what makes deep forest shade read as cool green instead of
// as a dimmer switch.
//
// Everything is evaluated on the art's own pixel grid (see _PixelSize) and the
// shade is ordered-dithered on the way out, so the gradients step in the same
// quanta as the sprites they fall on rather than sliding smoothly underneath
// them. The dither is the same Bayer 4x4 the venture curtain dissolves on.
//
// IT IS MEANT TO BE ALMOST INVISIBLE. It is a hint that the fight is happening
// on a forest floor under cover, not a weather effect and not a picture in its
// own right. Every attempt to make it more legible than that — hard-edged
// foliage, light beams, a brighter clearing — has made the arena worse. If in
// doubt, turn it down.
//
// Deliberately built like Custom/WaveGlow — plain CGPROGRAM, no LightMode tag —
// because that is the shader shape already proven to draw in this project's
// URP 2D renderer. It is unlit on purpose: ArenaLighting rides _Master down
// with the global light so the curtain still takes the frame to black.
Shader "Custom/CanopyLight"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Shade)]
        // Darker than any pixel in the backdrop art on purpose: a shade colour
        // lighter than what it falls on LIFTS the dark corners instead of
        // deepening them, and the whole frame goes milky
        _ShadeColor ("Shade Colour", Color) = (0.02, 0.035, 0.032, 1)
        _ShadeStrength ("Shade Strength", Range(0, 1)) = 0.26
        _TopShade ("Extra Shade Toward Top", Range(0, 0.5)) = 0.04

        [Header(Breath)]
        // Swells and settles the cover. Zero — the default — is a dead-still
        // canopy, which is what the forest wants; the mine uses it to make its
        // dark hug the frame and let go again.
        _BreathAmount ("Breath Amount", Range(0, 1)) = 0
        _BreathPeriod ("Breath Period (seconds)", Range(0.5, 60)) = 11

        [Header(Sun)]
        _SunColor ("Sun Colour", Color) = (1, 0.93, 0.68, 1)

        [Header(Glade)]
        _GladeCenter ("Glade Centre (world xy)", Vector) = (0, -0.2, 0, 0)
        _GladeRadius ("Glade Radius (world xy)", Vector) = (7.5, 4.2, 0, 0)
        _GladeSoftness ("Glade Softness", Range(0.05, 1.5)) = 0.55
        _EdgeNoise ("Glade Edge Noise Scale", Range(0.02, 2)) = 0.22
        _EdgeRagged ("Glade Edge Raggedness", Range(0, 1)) = 0.3

        [Header(Dapple)]
        _DappleScale ("Dapple Scale", Range(0.05, 4)) = 0.9
        _DappleDetail ("Dapple Detail", Range(0, 1)) = 0.4
        _DappleCoverage ("Leaf Coverage", Range(0.05, 0.95)) = 0.46
        _DappleDepth ("Leaf Shadow Depth", Range(0, 1)) = 0.11
        _DappleLight ("Sun Through Gaps", Range(0, 0.2)) = 0.005

        [Header(Shafts)]
        _ShaftAngle ("Shaft Angle (radians)", Range(-3.15, 3.15)) = -1.05
        _ShaftScale ("Shaft Scale", Range(0.02, 2)) = 0.35
        _ShaftSharp ("Shaft Sharpness", Range(0, 0.95)) = 0.72
        _ShaftStrength ("Shaft Strength", Range(0, 0.2)) = 0.002

        [Header(Wind)]
        _WindAngle ("Wind Angle (radians)", Range(-3.15, 3.15)) = -0.2
        _WindSpeed ("Wind Speed (world units per second)", Range(0, 2)) = 0.12

        [Header(Pixel grid)]
        _PixelSize ("World Units Per Art Pixel", Float) = 0.03125
        _Levels ("Shade Dither Levels", Range(4, 256)) = 40

        _Master ("Master Strength", Range(0, 2)) = 1
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
        // Premultiplied: shade lerps the frame toward _ShadeColor, sun adds on top
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

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

            float4 _ShadeColor;
            float  _ShadeStrength;
            float  _TopShade;
            float  _BreathAmount;
            float  _BreathPeriod;

            float4 _GladeCenter;
            float4 _GladeRadius;
            float  _GladeSoftness;
            float  _EdgeNoise;
            float  _EdgeRagged;

            float  _DappleScale;
            float  _DappleDetail;
            float  _DappleCoverage;
            float  _DappleDepth;
            float  _DappleLight;

            float  _ShaftAngle;
            float  _ShaftScale;
            float  _ShaftSharp;
            float  _ShaftStrength;

            float  _WindAngle;
            float  _WindSpeed;

            float4 _SunColor;
            float  _PixelSize;
            float  _Levels;
            float  _Master;

            // Integer hash, not the usual frac(sin(dot(...))). The canopy drifts
            // one way forever, so lattice coordinates only ever grow: a sine hash
            // loses precision as its argument climbs and the noise degrades into
            // bands after a few minutes of play. This one is exact — it wraps at
            // 2^32 cells, which at the wind speeds here is thousands of years away.
            float hash21(float2 lattice)
            {
                uint2 q = asuint(int2(lattice));
                q *= uint2(1597334673u, 3812015801u);
                uint n = (q.x ^ q.y) * 1597334673u;
                return float(n >> 8) * (1.0 / 16777216.0);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Three octaves, then stretched about its midpoint. Raw fbm only ever
            // uses the middle of the 0..1 range, and every consumer below wants a
            // mask that actually reaches both ends.
            float fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                float norm = 0.0;
                for (int i = 0; i < 3; i++)
                {
                    sum += amp * vnoise(p);
                    norm += amp;
                    p = p * 2.07 + 19.3;
                    amp *= 0.5;
                }
                return saturate((sum / norm - 0.5) * 1.85 + 0.5);
            }

            // Bayer 4x4 in closed form — same ordered-dither matrix VentureCurtain
            // dissolves the frame on
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
                // Snap to the art's pixel grid FIRST, so every term below is
                // constant across an art pixel. The quad is oversized and follows
                // the camera, but the pattern is a function of world position, so
                // it stays pinned to the arena rather than to the screen.
                float2 cell = floor(IN.world / _PixelSize);
                float2 p = (cell + 0.5) * _PixelSize;

                // The canopy travels ONE way and never turns back. An oscillating
                // sway gives itself away the moment it reverses — the eye reads a
                // pattern being slid about overhead instead of weather. Nothing
                // loops: the flow grows without bound and the integer hash above
                // stays exact as far out as it will ever get.
                //
                // The layers move at different rates in the SAME direction, which
                // is parallax: heavy cover overhead barely shifts while the fine
                // leaf shadows under it travel.
                float2 windDir = float2(cos(_WindAngle), sin(_WindAngle));
                float2 flow = windDir * (_WindSpeed * _Time.y);

                // How much roof is overhead: 0 over the glade where the canopy
                // thins, 1 under the thick cover at the frame's edge. The ellipse
                // is warped by noise so its edge is leaf cover, not a vignette.
                float2 rel = (p - _GladeCenter.xy) / max(_GladeRadius.xy, 0.001);
                float ragged = (fbm((p - flow * 0.35) * _EdgeNoise) - 0.5) * _EdgeRagged;
                float canopy = smoothstep(1.0 - _GladeSoftness, 1.0 + _GladeSoftness, length(rel) + ragged);

                // One noise field, two readings: below coverage is a leaf casting
                // a shadow, above it is a gap letting the sun down. The finer
                // octave PERTURBS the coarse field rather than being averaged into
                // it — averaging pulls both toward the middle, and the middle is
                // the range the masks below throw away.
                float leaves = fbm((p - flow) * _DappleScale);
                float fine = fbm((p - flow * 1.35) * (_DappleScale * 2.7) + 41.0);
                leaves = saturate(leaves + (fine - 0.5) * _DappleDetail);

                float leaf = saturate((_DappleCoverage - leaves) / max(_DappleCoverage, 0.001));
                leaf = leaf * leaf * (3.0 - 2.0 * leaf);

                // Squared, so the sun arrives as distinct spots in the middle of a
                // gap. Left linear it spreads across every half-open patch at once
                // and the arena reads as fogged rather than dappled.
                float gap = saturate((leaves - _DappleCoverage) / max(1.0 - _DappleCoverage, 0.001));
                gap = gap * gap;

                // Shafts: noise squashed along one axis, so it smears into streaks
                // running the way the light falls. Kept at a strength you would
                // struggle to point at — this is texture, not a sunbeam.
                float2 dir = float2(cos(_ShaftAngle), sin(_ShaftAngle));
                float2 sp = float2(dot(p, float2(-dir.y, dir.x)) - dot(flow, windDir) * 0.5,
                                   dot(p, dir));
                float shaft = fbm(float2(sp.x, sp.y * 0.07) * _ShaftScale);
                shaft = smoothstep(_ShaftSharp, 1.0, shaft);

                // Deeper under the trees toward the top of the frame — a little
                // depth, so the arena reads as a floor rather than a flat plate
                float height = saturate((p.y - _GladeCenter.y) / max(_GladeRadius.y * 2.0, 0.001));

                // The cover breathes: a slow swell of the vignette and nothing
                // else. Only the big shape is modulated — pulsing the leaf
                // shadows too would set the whole floor throbbing, which is the
                // one thing a breath must not do to be missable.
                float breath = 1.0 + sin(_Time.y * (6.2831853 / max(_BreathPeriod, 0.01))) * _BreathAmount;

                // Leaf shadows land hardest on ground the sun actually reaches;
                // there is nothing to shadow inside the deep shade
                float shade = canopy * _ShadeStrength * breath
                            + leaf * _DappleDepth * (1.0 - canopy * 0.65)
                            + height * _TopShade;
                shade = saturate(shade) * _Master;

                // Dither on the art grid: the shade steps like hand-placed pixels
                // instead of banding across the frame
                shade = floor(shade * _Levels + bayer4(cell)) / _Levels;

                // Sun only where the canopy has actually opened up. Left smooth —
                // its whole range is a few thousandths, far below one dither step.
                float sun = (gap * _DappleLight + shaft * _ShaftStrength) * (1.0 - canopy) * _Master;

                return float4(_ShadeColor.rgb * shade + _SunColor.rgb * sun, shade);
            }
            ENDCG
        }
    }
}
