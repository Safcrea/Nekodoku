Shader "AllIn1SpringsToolkit/Demo/FakeLight" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Hardness ("Hardness", Range(0.01, 2)) = 1.0
        _SpecularAmount ("Specular Amount", Range(0.01, 1)) = 0.5
        _SpecularIntensity ("Specular Intensity", Range(0, 1)) = 1
        [HDR] _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _ShadowAmount ("Shadow Amount", Range(0, 1)) = 0.5
        _LightMult ("Light Multiplier", Range(0, 10)) = 1
        [HDR] _SecondaryColor ("Secondary Color", Color) = (1, 1, 1, 1)
        _SecondColorBlend ("Second Color Blend", Range(0, 1)) = 0
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color, _SecondaryColor;
            float _Hardness;
            float _SpecularAmount, _SpecularIntensity;
            float4 _SpecularColor;
            float _ShadowAmount, _LightMult, _SecondColorBlend;

            // Global light direction vector
            float3 _All1SpringLightDir = float3(0.5, 0.5, 0.5);

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target {
                // Diffuse lighting
                float3 normal = normalize(i.worldNormal);
                float NdotL = dot(-_All1SpringLightDir, normal);
                NdotL = NdotL * _Hardness + 1.0 - _Hardness;
                float diffuse = max(_ShadowAmount, NdotL);

                // Specular highlight
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 reflectDir = reflect(_All1SpringLightDir, normal);
                float spec = pow(max(0.0, dot(viewDir, reflectDir)), _SpecularAmount * 128.0);

                // Sample texture
                float4 texColor = tex2D(_MainTex, i.uv);

                // Combine lighting and texture
                const float4 baseTintColor = lerp(_Color, _SecondaryColor, _SecondColorBlend);
                float4 finalColor = texColor * baseTintColor * _LightMult * diffuse + _SpecularColor * spec * _SpecularIntensity;

                return finalColor;
            }
            ENDCG
        }
    }
}