Shader "Unlit/ShadowReciver"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float depth : TEXCOORD3;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
			float4x4 _ShadowMatrix;
			//sampler2D _ShadowMapCus;
            Texture2D _ShadowMapCus;
			float _Bias;
            SamplerComparisonState sampler_ShadowMapCus;

            float _BiasArray[4];
            float4x4 _WorldToShadowAtlas[4];
            float4 _CullingSphere[4];

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.depth = UnityObjectToViewPos(v.vertex).z;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
				float4 viewPos = mul(UNITY_MATRIX_V, i.worldPos);
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
				col.rgb = col.rgb * saturate(dot(lightDir, normal)) * _LightColor0.rgb;

                float4 pixelDepth = abs(i.depth);
                float4 fComparison = pixelDepth > float4(_CullingSphere[0].w, _CullingSphere[1].w, _CullingSphere[2].w, _CullingSphere[3].w);
                float index = dot(float4(1, 1, 1, 1), fComparison);
                float fAtten = 1;
                if (index == 4)
                    fAtten = 1;
                else
                {
                    float3 normalBias = normal * _BiasArray[index];
                    float4 worldPos = i.worldPos;
                    worldPos.xyz += normalBias;
                    float4 lightPos = mul(_WorldToShadowAtlas[index], worldPos);
                    float3 lightPosProj = lightPos.xyz / lightPos.w;
                    fAtten = _ShadowMapCus.SampleCmpLevelZero(sampler_ShadowMapCus, lightPosProj.xy, lightPosProj.z);
                }
                col.rgb *= fAtten;
                return col;
            }
            ENDCG
        }
    }
}
