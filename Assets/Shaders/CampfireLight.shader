// The campfire's light, drawn as an overlay quad.
//
// One question per pixel — how much of the fire's light reaches this spot — and
// two things done with the answer: where it does not reach, the frame is lerped
// toward night; where it does, a little warm light is added. So the camp sits in
// a pool of firelight with the dark closing in at the edge of the frame,
// whichever map's floor is under it.
//
// Same premultiplied trick as Custom/CanopyLight:
//   result = src.rgb + dst * (1 - src.a)
// with a = shade and rgb = nightColour*shade + warm.
//
// CampfireScene draws it twice, the way ArenaLighting draws the canopy: once
// just above the backdrop at full strength, and once over everything at a
// fraction of it, so the menu's props sit IN the firelight without the ones
// furthest from the fire being lost. The knights stand under the full-strength
// pass, like the floor. The two passes differ only by _Master on a property
// block.
//
// _Flicker is the fire's breath: CampfireScene moves it a few percent either
// side of 1 and the pool of light swells and shrinks with it.
//
// Worked out on the art's pixel grid and ordered-dithered, so the falloff steps
// in art pixels instead of sliding under them as a smooth gradient. Built like
// Custom/CanopyLight — plain CGPROGRAM, no LightMode tag.
Shader "Custom/CampfireLight"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(The fire)]
        _FireCenter ("Fire Centre (world xy)", Vector) = (0, -0.3, 0, 0)
        _Reach ("Reach (world xy radii)", Vector) = (7.2, 5.2, 0, 0)
        _Core ("Fully Lit Core (fraction of reach)", Range(0, 0.9)) = 0.18
        _Flicker ("Flicker (about 1)", Range(0.5, 1.5)) = 1

        [Header(Night)]
        _ShadeColor ("Night Colour", Color) = (0.008, 0.01, 0.022, 1)
        _ShadeStrength ("Night Strength", Range(0, 1)) = 0.62

        [Header(Warmth)]
        _WarmColor ("Firelight Colour", Color) = (1, 0.55, 0.2, 1)
        _WarmStrength ("Firelight Strength", Range(0, 0.6)) = 0.1

        [Header(Pixel grid)]
        _PixelSize ("World Units Per Art Pixel", Float) = 0.03125
        _Levels ("Dither Levels", Range(4, 256)) = 28

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

            float4 _FireCenter;
            float4 _Reach;
            float  _Core;
            float  _Flicker;

            float4 _ShadeColor;
            float  _ShadeStrength;

            float4 _WarmColor;
            float  _WarmStrength;

            float  _PixelSize;
            float  _Levels;
            float  _Master;

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
                float2 cell = floor(IN.world / _PixelSize);
                float2 p = (cell + 0.5) * _PixelSize;

                // Distance from the fire in units of its reach: 0 at the fire, 1
                // where its light gives out. The flicker moves the reach, so the
                // whole pool swells and shrinks rather than just brightening.
                float2 rel = (p - _FireCenter.xy) / max(_Reach.xy * _Flicker, 0.001);
                float d = length(rel);

                float lit = 1.0 - smoothstep(_Core, 1.0, d);

                float dither = bayer4(cell);

                float shade = _ShadeStrength * (1.0 - lit) * _Master;
                shade = floor(shade * _Levels + dither) / _Levels;

                // Squared, so the warmth gathers at the fire and is nearly gone by
                // the time the night starts
                float warm = _WarmStrength * lit * lit * _Flicker * _Master;
                warm = floor(warm * _Levels * 2.0 + dither) / (_Levels * 2.0);

                return float4(_ShadeColor.rgb * shade + _WarmColor.rgb * warm, shade);
            }
            ENDCG
        }
    }
}
