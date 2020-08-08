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
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
			float4x4 _ShadowMatrix;
			//sampler2D _ShadowMapCus;
            Texture2D _ShadowMapCus;
			float _Bias;
            SamplerComparisonState sampler_ShadowMapCus;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                
				
				
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);

				col.rgb = col.rgb * saturate(dot(lightDir, normal)) * _LightColor0.rgb;

                float bias = max(0.05f * (1.0f - dot(normal, lightDir)), 0.005f);

				float3 normalBias = normal * _Bias;
				float4 worldPos = i.worldPos;
				worldPos.xyz += normalBias;

				float4 lightPos = mul(_ShadowMatrix, worldPos);
				float3 lightPosProj = lightPos.xyz / lightPos.w;
				lightPosProj.z += 0.005f;
				//float fAtten = 0;
                float fAtten = _ShadowMapCus.SampleCmpLevelZero(sampler_ShadowMapCus, lightPosProj.xy, lightPosProj.z, 1);
				//fAtten /= 9.0f;
				col.rgb *= fAtten;
                return col;// - frac(fAtten);
            }
            ENDCG
        }
    }
}
