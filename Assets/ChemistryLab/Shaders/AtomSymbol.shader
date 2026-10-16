// Element symbol for a TextMeshPro (3D) text placed at the center of an atom.
//
// - Billboard: the text is rebuilt facing the camera in the vertex shader,
//   so every player (and each VR eye) sees it upright and readable. It runs
//   on each viewer's own GPU: purely local, no scripts, no networking.
//   In VR both eyes face it toward the point between them, so there is no
//   stereo mismatch.
// - "Visible through its own sphere, hidden by everything else": the text
//   is drawn after the opaque sphere with its depth moved to the sphere's
//   front surface. It passes the depth test against its own sphere, but an
//   object in front of the sphere is closer still and hides it normally.
//
// Use with a TMP font asset's atlas (signed distance field in alpha).
// _SphereRadius is the atom sphere's radius in the symbol's local units
// (0.5 for Unity's built-in sphere or a Blender UV sphere of radius 0.5,
// when the text object has scale 1 under the atom).
Shader "ChemistryLab/Atom Symbol"
{
    Properties
    {
        _MainTex ("Font Atlas (TMP SDF)", 2D) = "white" {}
        _FaceColor ("Symbol Color", Color) = (0, 0, 0, 1)
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.45)) = 0.15
        _SphereRadius ("Sphere Radius (local)", Float) = 0.5
    }

    SubShader
    {
        // DisableBatching: dynamic/static batching would pre-transform the TMP
        // mesh into world space, destroying the object center and scale that
        // the billboard and depth trick below rely on.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _FaceColor;
            fixed4 _OutlineColor;
            half _OutlineWidth;
            float _SphereRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;   // TMP's vertex color (text Color field)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Object center and (uniform) scale in world space.
                float3 center = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float scale = length(unity_ObjectToWorld._m00_m10_m20);

                // Viewer position. In VR use the point between the eyes, so
                // both eyes see the letter with the same orientation.
                #if defined(USING_STEREO_MATRICES)
                    float3 eye = (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5;
                #else
                    float3 eye = _WorldSpaceCameraPos;
                #endif

                // Viewer-facing billboard: the text plane is kept square to the
                // line from the eye to the atom. (Copying the camera rotation
                // instead tilts the plane for atoms away from the screen center,
                // and one side of the letter then sinks into the sphere.)
                float3 toCam = eye - center;
                float camDist = max(length(toCam), 1e-5);
                float3 facing = toCam / camDist;
                // Unity's world is left-handed but cross() is the plain math
                // cross product, so the viewer's right is cross(facing, up):
                // camera looking down +Z -> facing (0,0,-1), up (0,1,0) ->
                // right (+1,0,0). The other order mirrors the text.
                float3 camUp = UNITY_MATRIX_V[1].xyz;        // follows head roll
                float3 right = cross(facing, camUp);
                right = dot(right, right) > 1e-6 ? normalize(right) : UNITY_MATRIX_V[0].xyz;
                float3 up = cross(right, facing);

                float3 worldPos = center
                    + right * (v.vertex.x * scale)
                    + up * (v.vertex.y * scale);
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1));

                // Depth of the sphere's front surface (plus 2%), so the symbol
                // wins against its own sphere but nothing else.
                float push = _SphereRadius * scale * 1.02;
                if (camDist > push)
                {
                    float4 front = mul(UNITY_MATRIX_VP, float4(worldPos + facing * push, 1));
                    o.pos.z = front.z / front.w * o.pos.w;
                }

                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Signed distance field: 0.5 is the glyph edge.
                float dist = tex2D(_MainTex, i.uv).a;
                float aa = max(fwidth(dist), 1e-4);
                float face = smoothstep(0.5 - aa, 0.5 + aa, dist);
                float outline = smoothstep(0.5 - _OutlineWidth - aa, 0.5 - _OutlineWidth + aa, dist);

                fixed4 faceColor = _FaceColor * i.color;
                fixed3 rgb = lerp(_OutlineColor.rgb, faceColor.rgb, face);
                float alpha = lerp(_OutlineColor.a * outline, faceColor.a, face);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
