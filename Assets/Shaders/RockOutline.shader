// The glowing ring around an enemy rock. RockOutline draws this on a quad a few
// texels bigger than the rock's sprite, one sorting order under the rock, so
// the rock always sits on top of it.
//
// The ring is a CIRCLE round the middle of the sprite, not a trace of the
// rock's edge: a pixel-art rock has flat runs along its sides, and a rim that
// follows them comes out lumpy and flat-sided. Every pixel asks how far it is
// from the middle, on the ROCK'S OWN texel grid, so the circle steps in the
// same pixels as the art it surrounds and turns with the rock as it flies:
//
//   * from the rock's edge out to _Radius + _Width: the rim, solid colour,
//     never pulses. Any gap between a flat run of the rock and the circle is
//     filled with rim, so the outside edge is round whatever the art does.
//   * _GlowWidth texels beyond that: the glow, in _GlowSteps flat rings that
//     fade outward and swell gently in time. Rings rather than a smooth ramp
//     so it reads as pixel art, not as a soft-focus filter.
//
// Unlit on purpose: the rock itself is lit, and in the mine's half light a
// grey rock on a grey floor is exactly what this is here to fix. RockOutline
// rides _Master down with the global light instead, so the curtain still takes
// the frame to black between waves.
//
// Built like Custom/CanopyLight — plain CGPROGRAM, no LightMode tag — because
// that is the shader shape proven to draw in this project's URP 2D renderer.
Shader "Custom/RockOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Rim Colour", Color) = (1,1,1,1)

        // In texels of the rock's art. The rock draws at a quarter scale, so
        // four of these are one arena pixel
        _Width ("Rim Width (texels)", Range(0.5, 8)) = 1.6
        _GlowWidth ("Glow Width (texels)", Range(0, 10)) = 3
        _GlowStrength ("Glow Strength", Range(0, 1)) = 0.6
        _GlowSteps ("Glow Bands", Range(1, 6)) = 3

        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.35
        _PulsePeriod ("Pulse Period (seconds)", Range(0.1, 5)) = 0.8

        // Per renderer, from RockOutline: where in the texture the sprite sits
        // (texel min xy, texel max xy), the circle's middle and radius in
        // texels (x, y, radius), and the texture's size (w, h, 1/w, 1/h)
        _SpriteTexels ("Sprite Texels", Vector) = (0,0,1,1)
        _Circle ("Circle", Vector) = (0.5,0.5,0.5,0)
        _TexSize ("Texture Size", Vector) = (1,1,1,1)
        _Phase ("Pulse Phase (seconds)", Float) = 0

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
        // Premultiplied, so the rim can cover what is under it while the glow
        // mostly adds light to it
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
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Width;
            float _GlowWidth;
            float _GlowStrength;
            float _GlowSteps;
            float _PulseAmount;
            float _PulsePeriod;
            float4 _SpriteTexels;
            float4 _Circle;
            float4 _TexSize;
            float _Phase;
            float _Master;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                return OUT;
            }

            // Whether one texel of the art is part of the rock. Anything outside
            // the sprite's own rectangle is empty, whatever else the texture
            // holds there
            float Solid(float2 cell)
            {
                if (cell.x < _SpriteTexels.x || cell.y < _SpriteTexels.y ||
                    cell.x >= _SpriteTexels.z || cell.y >= _SpriteTexels.w)
                    return 0;
                float a = tex2Dlod(_MainTex, float4((cell + 0.5) * _TexSize.zw, 0, 0)).a;
                return step(0.5, a);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // The texel of the rock's art this pixel falls in; everything
                // below is worked out per texel, never per screen pixel
                float2 cell = floor(IN.uv * _TexSize.xy);

                // The rock draws itself here
                if (Solid(cell) > 0) return 0;

                // Texel centre to the middle of the rock
                float d = distance(cell + 0.5, _Circle.xy);
                float rimEdge = _Circle.z + _Width;

                // The rim: solid and steady, the part that has to be seen
                if (d <= rimEdge)
                    return fixed4(_Color.rgb * _Master, _Master);

                if (d > rimEdge + _GlowWidth) return 0;

                // The glow: flat rings fading outward, swelling in time
                float t = saturate((d - rimEdge) / max(_GlowWidth, 0.001));
                float steps = max(1, round(_GlowSteps));
                float band = 1 - floor(t * steps) / steps;

                float wave = 0.5 + 0.5 * sin((_Time.y - _Phase) * 6.2831853 / max(_PulsePeriod, 0.01));
                float pulse = 1 - _PulseAmount * wave;

                float glow = _GlowStrength * band * pulse * _Master;

                // Half cover, half added light: tints a bright floor, lights a
                // dark one
                return fixed4(_Color.rgb * glow, glow * 0.5);
            }
            ENDCG
        }
    }
}
