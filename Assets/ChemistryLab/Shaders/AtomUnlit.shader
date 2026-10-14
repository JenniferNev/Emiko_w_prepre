// Atom sphere: a flat element color that ignores world lighting, so an
// element looks the same in every room, light probe or baked lightmap.
// An optional view-dependent rim darkening keeps the ball reading as 3D
// (it depends only on the viewing angle, never on lights).
Shader "ChemistryLab/Atom Unlit"
{
    Properties
    {
        _Color ("Element Color", Color) = (1, 1, 1, 1)
        _RimStrength ("Rim Darkening", Range(0, 1)) = 0.35
        _RimPower ("Rim Width", Range(0.5, 8)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _RimStrength;
            half _RimPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float facing = saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                float rim = pow(1 - facing, _RimPower);
                return fixed4(_Color.rgb * (1 - _RimStrength * rim), 1);
            }
            ENDCG
        }
    }

    // VertexLit supplies a ShadowCaster pass, so atoms still cast shadows
    // onto the table (they never receive any themselves).
    Fallback "VertexLit"
}
