// A torch's light: a small warm pool round the flame, added to the floor
// round the pillar it hangs on.
//
// Additive (Blend One One) — the torch only ever brings light. TorchGlow puts
// it on the Background layer just under the pillar, so it lights the floor and
// not the pillar's own art, and the knights and enemies on Default are not tinted.
//
// The falloff is stepped into a few rings and ordered-dithered between them on
// the art's pixel grid, the same Bayer 4x4 the canopy and the campfire use, so
// the pool steps in art pixels instead of being a smooth airbrushed blob.
//
// Also the mirrors' glow (MirrorGlow). _HalfSize turns the pool from a circle
// round a point into a glow round a rectangle: the falloff is measured from the
// rectangle's edge, so a tall pane's light hugs its sides as evenly as its ends.
// A torch leaves it at zero, which is the plain circle.
//
// The centre arrives on a property block rather than from the object matrix:
// sprites batch, and a batched sprite's matrix is the identity.
//
// Built like Custom/CanopyLight — plain CGPROGRAM, no LightMode tag.
Shader "Custom/TorchGlow"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Glow Colour", Color) = (1, 0.72, 0.18, 1)
        _Center ("Flame Centre (world xy)", Vector) = (0, 0, 0, 0)
        _Radius ("Reach (world units)", Float) = 1.4
        _HalfSize ("Lit Rectangle Half Size (world xy)", Vector) = (0, 0, 0, 0)
        _Intensity ("Intensity", Range(0, 1)) = 0.07
        _Rings ("Rings", Range(2, 32)) = 7
        _PixelSize ("World Units Per Art Pixel", Float) = 0.03125
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
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 world  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _Color;
            float4 _Center;
            float  _Radius;
            float4 _HalfSize;
            float  _Intensity;
            float  _Rings;
            float  _PixelSize;

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
                // Snap to the art's pixel grid first, so every art pixel takes one value
                float2 cell = floor(IN.world / _PixelSize);
                float2 p = (cell + 0.5) * _PixelSize;

                // Squared falloff: a bright little core round the flame that
                // drops away fast, the way a small flame lights a floor
                float2 outside = max(abs(p - _Center.xy) - _HalfSize.xy, 0.0);
                float d = length(outside) / max(_Radius, 0.001);
                float f = saturate(1.0 - d);
                f = f * f;

                // Into rings, dithered at the seams between them
                f = floor(f * _Rings + bayer4(cell)) / _Rings;

                return float4(_Color.rgb * (f * _Intensity), 0);
            }
            ENDCG
        }
    }
}
