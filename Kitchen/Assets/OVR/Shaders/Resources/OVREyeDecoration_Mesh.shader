
Shader "Custom/OVREyeDecoration_Mesh" 
{	
	// Shader code pasted into all further CGPROGRAM blocks
	CGINCLUDE
	
	#include "UnityCG.cginc"
	
	struct appdata_t {
		float4 vertex : POSITION;
		fixed4 color : COLOR;
	};	
	
	struct v2f 
	{
		float4 pos : POSITION;
		fixed4 color : COLOR;
	};
	
	v2f vert( appdata_t v ) 
	{
		v2f o;
		o.pos = v.vertex;
		o.color = v.color;
		return o;
	} 
	
	half4 frag(v2f i) : COLOR 
	{
		return i.color;
	}

	ENDCG 
	
Subshader {
 Pass {
 	  Blend Zero SrcColor
	  ZTest Always Cull Off ZWrite Off
	  Fog { Mode off }      

      CGPROGRAM
      #pragma fragmentoption ARB_precision_hint_fastest
      #pragma vertex vert
      #pragma fragment frag
      ENDCG
  }
  
}

Fallback off
	
} // shader