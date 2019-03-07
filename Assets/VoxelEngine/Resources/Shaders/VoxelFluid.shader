// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/VoxelsFluid" {
	Properties {
        _Color ("Color", Color) = (1,1,1,1)
		_ShoreColor ("Shore Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture", 2D) = "white" {}
		_ShoreTex ("Shore Texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _InvFade ("Soft Factor", Range(0.01,3.0)) = 1.0
		_WaveA ("Wave A (dir, steepness, wavelength)", Vector) = (1,0,0.5,10)
		_WaveB ("Wave B", Vector) = (0,1,0.25,20)
		[HDR]_SkyLight ("Sky Light Color", Color) = (1,1,1,1)
		[HDR]_BlockLight ("Block Light Color", Color) = (1,1,1,1)
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
       
        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard vertex:vert alpha:fade nolightmap noambient
 
        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0
 
        sampler2D _MainTex;
		sampler2D _ShoreTex;

		// Voxel light
		fixed4 _SkyLight;
		fixed4 _BlockLight;
 
        struct Input {
            float2 uv_MainTex;
			float2 uv_MainTex_ST;
			float2 uv_ShoreTex;
            float4 screenPos;
            float eyeDepth;
			float3 vertexColor;
        };
 
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
		fixed4 _ShoreColor;
		float4 _WaveA, _WaveB;
 
        sampler2D_float _CameraDepthTexture;
        float4 _CameraDepthTexture_TexelSize;
       
        float _InvFade;

		float3 GerstnerWave (float4 wave, float3 p, inout float3 tangent, inout float3 binormal) {
		    float steepness = wave.z;
		    float wavelength = wave.w;
		    float k = 2 * UNITY_PI / wavelength;
			float c = sqrt(9.8 / k);
			float2 d = normalize(wave.xy);
			float f = k * (dot(d, p.xz) - c * _Time.y);
			float a = steepness / k;
			
			//p.x += d.x * (a * cos(f));
			//p.y = a * sin(f);
			//p.z += d.y * (a * cos(f));

			tangent += float3(
				-d.x * d.x * (steepness * sin(f)),
				d.x * (steepness * cos(f)),
				-d.x * d.y * (steepness * sin(f))
			);
			binormal += float3(
				-d.x * d.y * (steepness * sin(f)),
				d.y * (steepness * cos(f)),
				-d.y * d.y * (steepness * sin(f))
			);
			return float3(
				d.x * (a * cos(f)),
				a * sin(f),
				d.y * (a * cos(f))
			);
		}
 
        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            COMPUTE_EYEDEPTH(o.eyeDepth);
			o.vertexColor = v.color;

			float4 worldPos = mul( unity_ObjectToWorld, v.vertex);
			float sinX = (sin(worldPos.x + _Time.z) - 1) * 0.1;
			float sinZ = ( sin(worldPos.z + _Time.z) - 1) * 0.1;
			v.vertex.y += sinX + sinZ;
        }
 
        void surf (Input IN, inout SurfaceOutputStandard o) {
            // Albedo comes from a texture tinted by color
			IN.uv_MainTex.y += floor(_Time.z) * 0.0625;
            fixed4 mainSample = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			fixed4 shoreSample = tex2D (_ShoreTex, IN.uv_ShoreTex) * _ShoreColor;

			// Sample depth information
			float rawZ = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(IN.screenPos));
            float sceneZ = LinearEyeDepth(rawZ);
            float partZ = IN.eyeDepth;
 
            float fade = 1.0;
            if ( rawZ > 0.0 ) // Make sure the depth texture exists
                fade = saturate(_InvFade * (sceneZ - partZ));

			// Calculate final albedo
			fixed3 albedo = lerp(shoreSample.rgb, mainSample.rgb, fade);

			// Extract and calculate voxel light from vertex colors
			fixed3 voxelLight = lerp(_SkyLight.rgb * max(IN.vertexColor.r, 0.1), _BlockLight.rgb * IN.vertexColor.g, IN.vertexColor.g / max(IN.vertexColor.r, 1));
			o.Emission = albedo.rgb * voxelLight;

            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
 
            o.Alpha = lerp(shoreSample.a, mainSample.a, fade);
			o.Albedo = albedo;
        }
        ENDCG
    }
	FallBack "Diffuse"
}
