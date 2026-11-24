Shader "LittleGarage/CarSurface"
{
    Properties
    {
        _Color ("Body Color", Color) = (0.3, 0.6, 0.9, 1)
        _StateMap ("Surface State (R dirt, G wet, B foam, A polish)", 2D) = "black" {}
        _DirtColor ("Dirt", Color) = (0.33, 0.25, 0.16, 1)
        _WetTint ("Wet Tint", Color) = (0.72, 0.82, 0.9, 1)
        _FoamColor ("Foam", Color) = (0.96, 0.98, 1, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.55
        _Smoothness ("Smoothness", Range(0, 1)) = 0.62
        _DirtStrength ("Dirt Strength", Range(0, 1)) = 0.94
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _StateMap;
        fixed4 _Color;
        fixed4 _DirtColor;
        fixed4 _WetTint;
        fixed4 _FoamColor;
        half _Metallic;
        half _Smoothness;
        half _DirtStrength;

        struct Input
        {
            float2 uv_StateMap;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half4 state = tex2D(_StateMap, IN.uv_StateMap);
            half dirt = saturate(state.r) * _DirtStrength;
            half wet = saturate(state.g);
            half foam = saturate(state.b);
            half polish = saturate(state.a);

            half3 clean = _Color.rgb;
            half3 dusty = lerp(clean, _DirtColor.rgb, dirt);
            half3 wetLook = lerp(dusty, dusty * _WetTint.rgb * 1.18h, wet * 0.75h);
            half3 foamed = lerp(wetLook, _FoamColor.rgb, foam * 0.92h);
            half3 mirror = lerp(foamed, clean * 1.12h + 0.03h, polish * 0.55h);

            o.Albedo = mirror;
            o.Metallic = lerp(_Metallic * 0.35h, _Metallic, (1.0h - dirt) * (1.0h - foam));
            o.Smoothness = saturate(_Smoothness * (1.0h - dirt * 0.85h - foam * 0.6h)
                + wet * 0.24h + polish * 0.32h);
            o.Alpha = 1.0h;
        }
        ENDCG
    }

    FallBack "Standard"
}
