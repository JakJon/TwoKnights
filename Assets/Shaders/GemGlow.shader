// The light round ONE gem or ore nugget painted into a stage's backdrop.
// GemGlow paints each gem's halo into a shared atlas — one texel per art
// pixel, rgb the colour of the light falling there (brightest channel at 1),
// alpha how much of it — and gives every gem its own renderer, so each one
// keeps its own time.
//
// A gem is dark most of the time. Once every _Every seconds (its own gap, set
// per renderer) it fades up and back down over _Lasts seconds, starting
// _Phase of the way into its cycle.
//
// Built like Custom/CanopyLight — plain CGPROGRAM, no LightMode tag — because
// that is the shader shape proven to draw in this project's URP 2D renderer.
// Unlit and additive: GemGlow rides _Master down with the global light so the
// curtain still takes the frame to black.
Shader "Custom/GemGlow"
{
    Properties
    {
        _MainTex ("Halo Atlas", 2D) = "black" {}

        // ADDITIVE in linear light, so the useful range is tiny. This is the
        // top of a glow, which a gem only reaches for a moment
        _Brightness ("Brightness", Range(0, 0.5)) = 0.03
        _Levels ("Dither Levels", Range(1, 32)) = 5
        _Lasts ("Glow Lasts (seconds)", Range(0.1, 10)) = 1.5

        // Per renderer, through a property block
        _Every ("Seconds Between Glows", Float) = 5
        _Phase ("Phase", Range(0, 1)) = 0

        _PixelSize ("World Units Per Art Pixel", Float) = 0.03125
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
        // Light only ever adds: a gem between glows draws nothing at all
        Blend One One

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
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float2 world  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;

            float _Brightness;
            float _Levels;
            float _Lasts;
            float _Every;
            float _Phase;
            float _PixelSize;
            float _Master;

            // Bayer 4x4 in closed form — the ordered dither the canopy and the
            // venture curtain both step on
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
                OUT.uv = IN.texcoord;
                OUT.world = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            float4 frag(v2f IN) : SV_Target
            {
                float4 glow = tex2D(_MainTex, IN.uv);

                // How far through its own cycle this gem is. Dark for all of it
                // but the first _Lasts seconds, which rise and fall in one
                // smooth swell (sin squared: no corner at either end)
                float every = max(_Every, 0.01);
                float t = frac(_Time.y / every + _Phase) * every;
                float swell = t < _Lasts ? sin(3.14159265 * t / max(_Lasts, 0.01)) : 0.0;
                swell *= swell;

                // The dither is laid on the ART's grid from world position — the
                // halos sit at odd places in their atlas, and the pattern must
                // line up with the backdrop and the canopy's shade
                float2 cell = floor(IN.world / _PixelSize);

                // Stepped AFTER the swell, so a glow grows outward from the gem
                // a dithered pixel at a time instead of brightening a fixed shape
                float amount = floor(glow.a * swell * _Levels + bayer4(cell)) / max(_Levels, 1.0);

                return float4(glow.rgb * (amount * _Brightness * _Master), 0);
            }
            ENDCG
        }
    }
}
