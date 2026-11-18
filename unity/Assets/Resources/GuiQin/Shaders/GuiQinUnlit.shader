// 顶点色 × 贴图的简单透明着色器：天气、雾、蝴蝶、发光点等特效用同一个。
// 内置管线 / URP（含 2D 渲染器）都能用：没有光照，不带 LightMode 的 Pass 会被当成无光照 Pass 渲染。
// _Src/_Dst：普通混合 (5,10)；叠加发光 (5,1)。
Shader "GuiQin/Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst", Float) = 10
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend [_Src] [_Dst]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
    Fallback Off
}
