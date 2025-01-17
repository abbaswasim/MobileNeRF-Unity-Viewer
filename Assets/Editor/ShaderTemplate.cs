public static class ViewDependenceNetworkShader {
    public const string Template_Deferred = @"Shader ""MobileNeRF/ViewDependenceNetworkShader/OBJECT_NAME"" {
    Properties {
        tDiffuse0x (""Diffuse Texture 0"", 2D) = ""white"" {}
        tDiffuse1x (""Diffuse Texture 1"", 2D) = ""white"" {}
    }
    SubShader {
		Tags { ""LightMode"" = ""Deferred"" }
        Cull Off
        ZTest LEqual

        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include ""UnityCG.cginc""

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 rayDirection : TEXCOORD1;
            };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.rayDirection = -WorldSpaceViewDir(v.vertex);
                AXIS_SWIZZLE

                return o;
            }

            sampler2D tDiffuse0x;
            sampler2D tDiffuse1x;

			struct FragmentOutput
			{
					float4 gBuffer0 : SV_Target0;
					float4 gBuffer1 : SV_Target1;
					float4 gBuffer2 : SV_Target2;
			};

            FragmentOutput frag (v2f i) 
			{
                fixed4 diffuse0 = tex2D( tDiffuse0x, i.uv );
                if (diffuse0.r == 0.0) discard;
                fixed4 diffuse1 = tex2D( tDiffuse1x, i.uv );
                fixed4 rayDir = fixed4(normalize(i.rayDirection), 1.0);

                //deal with iphone
                diffuse0.a = diffuse0.a*2.0-1.0;
                diffuse1.a = diffuse1.a*2.0-1.0;
                rayDir.a = rayDir.a*2.0-1.0;

				FragmentOutput output;

				output.gBuffer0 = diffuse0;
				output.gBuffer1 = diffuse1;
				output.gBuffer2 = rayDir;

                return output;
            }
            ENDCG
        }
    }
}";

    public const string Template_Resolve = @"Shader ""MobileNeRF/ViewDependenceNetworkShader/OBJECT_NAME"" {
    Properties {
        weightsZero (""Weights Zero"", 2D) = ""white"" {}
        weightsOne (""Weights One"", 2D) = ""white"" {}
        weightsTwo (""Weights Two"", 2D) = ""white"" {}
    }
    SubShader {
        Cull Off
        ZTest LEqual

        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include ""UnityCG.cginc""

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;

                return o;
            }

            sampler2D _GBuffer0;
            sampler2D _GBuffer1;
            sampler2D _GBuffer2;

            UNITY_DECLARE_TEX2D(weightsZero);
            UNITY_DECLARE_TEX2D(weightsOne);
            UNITY_DECLARE_TEX2D(weightsTwo);

            half3 evaluateNetwork(fixed4 f0, fixed4 f1, fixed4 viewdir) {
                half intermediate_one[NUM_CHANNELS_ONE] = { BIAS_LIST_ZERO };
                int i = 0;
                int j = 0;

                for (j = 0; j < NUM_CHANNELS_ZERO; ++j) {
                    half input_value = 0.0;
                    if (j < 4) {
                    input_value =
                        (j == 0) ? f0.r : (
                        (j == 1) ? f0.g : (
                        (j == 2) ? f0.b : f0.a));
                    } else if (j < 8) {
                    input_value =
                        (j == 4) ? f1.r : (
                        (j == 5) ? f1.g : (
                        (j == 6) ? f1.b : f1.a));
                    } else {
                    input_value =
                        (j == 8) ? viewdir.r : (
                        (j == 9) ? viewdir.g : viewdir.b);
                    }
                    for (i = 0; i < NUM_CHANNELS_ONE; ++i) {
                    intermediate_one[i] += input_value * weightsZero.Load(int3(j, i, 0)).x;
                    }
                }

                half intermediate_two[NUM_CHANNELS_TWO] = { BIAS_LIST_ONE };

                for (j = 0; j < NUM_CHANNELS_ONE; ++j) {
                    if (intermediate_one[j] <= 0.0) {
                        continue;
                    }
                    for (i = 0; i < NUM_CHANNELS_TWO; ++i) {
                        intermediate_two[i] += intermediate_one[j] * weightsOne.Load(int3(j, i, 0)).x;
                    }
                }

                half result[NUM_CHANNELS_THREE] = { BIAS_LIST_TWO };

                for (j = 0; j < NUM_CHANNELS_TWO; ++j) {
                    if (intermediate_two[j] <= 0.0) {
                        continue;
                    }
                    for (i = 0; i < NUM_CHANNELS_THREE; ++i) {
                        result[i] += intermediate_two[j] * weightsTwo.Load(int3(j, i, 0)).x;
                    }
                }
                for (i = 0; i < NUM_CHANNELS_THREE; ++i) {
                    result[i] = 1.0 / (1.0 + exp(-result[i]));
                }
                return half3(result[0]*viewdir.a+(1.0-viewdir.a),
                            result[1]*viewdir.a+(1.0-viewdir.a),
                            result[2]*viewdir.a+(1.0-viewdir.a));
            }

			struct FragmentOutput
			{
					float4 gBuffer0 : SV_Target0;
					float4 gBuffer1 : SV_Target1;
					float4 gBuffer2 : SV_Target2;
					float4 gBuffer3 : SV_Target3;
			};

            FragmentOutput frag (v2f i) {
                fixed4 diffuse0 = tex2D( _GBuffer0, i.uv );
                if (diffuse0.r == 0.0) discard;
                fixed4 diffuse1 = tex2D( _GBuffer1, i.uv );
                fixed4 rayDir = fixed4(normalize(tex2D( _GBuffer2, i.uv )).rgb, 1.0);

                fixed4 fragColor;
                fragColor.rgb = evaluateNetwork(diffuse0, diffuse1, rayDir);
                fragColor.a = 1.0;

				FragmentOutput output;

				// Unity's gbuffer format
                // RT0, ARGB32 format: Diffuse color (RGB), occlusion (A).
				// RT1, ARGB32 format: Specular color (RGB), smoothness (A).
				// RT2, ARGB2101010 format: World space normal (RGB), unused (A).
				// RT3, ARGB2101010 (non-HDR) or ARGBHalf (HDR) format: Emission + lighting + lightmaps + reflection probes buffer.
				// Depth+Stencil buffer

				output.gBuffer0 = fixed4(0.0, 0.0, 0.0, 0.0);
				output.gBuffer1 = fixed4(0.0, 0.0, 0.0, 0.0);
				output.gBuffer2 = fixed4(0.0, 0.0, 0.0, 0.0);
				output.gBuffer3 = fragColor;

                return output;
            }
            ENDCG
        }
    }
}";
}
