Shader "Custom/VoxelsAlpha" {
	Properties {
		_MainTex ("Albedo Atlas", 2D) = "white" {}
		_EmitTex ("Emission Atlas", 2D) = "black" {}
		_MetalTex ("Metallic Atlas", 2D) = "black" {}
		_SmoothTex ("Smoothness Atlas", 2D) = "black" {}
		[HDR]_SkyLight ("Sky Light Color", Color) = (1,1,1,1)
		[HDR]_BlockLight ("Block Light Color", Color) = (1,1,1,1)
	}
	SubShader {
		Tags { "RenderType"="Transparent" "Queue"="Transparent+100" }
		Cull Back
		ZWrite Off

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard noambient fullforwardshadows vertex:vert alpha:blend

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 4.0

		// Texture interpolators
		sampler2D _MainTex;
		sampler2D _SmoothTex;
		sampler2D _MetalTex;
		sampler2D _EmitTex;

		// Voxel light
		fixed4 _SkyLight;
		fixed4 _BlockLight;

		struct Input {
			float2 uv_MainTex;
			float2 uv2_EmitTex;
			float2 uv3_MetalTex;
			float2 uv4_SmoothTex;
			float3 vertexColor;
		};

		struct v2f {
           float4 pos : SV_POSITION;
           fixed4 color : COLOR;
         };
 
         void vert (inout appdata_full v, out Input o)
         {
             UNITY_INITIALIZE_OUTPUT(Input,o);
             o.vertexColor = v.color;
         }


		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		#pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void surf (Input IN, inout SurfaceOutputStandard o) {
			// Extract and calculate voxel light from vertex colors
			fixed3 voxelLight = lerp(_SkyLight.rgb * max(IN.vertexColor.r, 0.1), _BlockLight.rgb * IN.vertexColor.g, IN.vertexColor.g / max(IN.vertexColor.r, 1));

			// Extract and set albedo from atlas
			fixed4 albedo = tex2D (_MainTex, IN.uv_MainTex);
			o.Albedo = albedo.rgb;

			// Extract and set emission from atlas and voxel light 
			fixed4 emission = tex2D(_EmitTex, IN.uv2_EmitTex);
			o.Emission = emission.rgb + albedo.rgb * voxelLight;

			// Extract and set metallicity from atlas
			fixed metallic = Luminance(tex2D(_MetalTex, IN.uv3_MetalTex));
			o.Metallic = metallic;

			// Extract and set smoothness from atlas
			fixed smoothness = Luminance(tex2D(_SmoothTex, IN.uv4_SmoothTex));
			o.Smoothness = smoothness;

			// Set alpha to albedo alpha
			o.Alpha = albedo.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
