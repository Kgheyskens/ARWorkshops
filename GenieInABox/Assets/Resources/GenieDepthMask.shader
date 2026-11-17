// Onzichtbare "occluder": tekent niets (ColorMask 0) maar schrijft wel diepte.
// Wordt VOOR de rest getekend (Queue Geometry-10). Alles wat erachter ligt wordt daardoor verborgen,
// en op die plek zie je gewoon het camerabeeld. Zo lijkt de binnenkant van de doos enkel zichtbaar
// door het open luik: de truc achter de "holle doos".
Shader "GenieBox/DepthMask"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-10" }
        Pass
        {
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert (float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            fixed4 frag () : SV_Target { return 0; }
            ENDCG
        }
    }
}
