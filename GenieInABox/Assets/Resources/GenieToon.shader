// Eenvoudige "toon"-shader voor alle 3D-onderdelen van Genie in a Box.
// - kleur (+ optionele textuur en vertexkleur)
// - zacht licht van boven (richting via _GenieLightDir, gezet door GenieBoxRig, zodat het licht
//   meedraait met de doos en niet met de camera)
// - gloed (_Glow) met rand-effect: hoe schuiner je kijkt, hoe meer het oplicht
// Staat in een Resources-map zodat Shader.Find hem ook in de build vindt.
Shader "GenieBox/Toon"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _Glow ("Glow", Range(0,4)) = 0
        _Ambient ("Ambient", Range(0,1)) = 0.45
        _UseVertexColor ("Use Vertex Color", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color, _GlowColor;
            float _Glow, _Ambient, _UseVertexColor;
            float4 _GenieLightDir;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 wp : TEXCOORD2; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = lerp(fixed4(1,1,1,1), v.color, _UseVertexColor);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 l = dot(_GenieLightDir.xyz, _GenieLightDir.xyz) > 0.01 ? normalize(_GenieLightDir.xyz) : normalize(float3(0.35, 1.0, 0.25));
                float diff = saturate(dot(n, l));
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float rim = pow(1.0 - saturate(dot(n, v)), 2.5);
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color * i.color;
                float3 c = tex.rgb * (_Ambient + (1.0 - _Ambient) * diff) + _GlowColor.rgb * _Glow * (0.35 + rim);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
