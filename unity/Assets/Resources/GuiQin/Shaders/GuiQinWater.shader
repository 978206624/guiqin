// 水面着色器（对应网页版 waterDraw 的折射 + 岸边水花 + 三层流动纹理 + 中心快流层）。
// 叠在地图底图上面的一张 Sprite：_MainTex = 水面蒙版（R 通道 = 水的不透明度，软边），
// _BgTex = 地图底图（用来做折射），_FlowTex = 流向图（R,G = 方向*0.5+0.5，B = 流速/128，A = 竖向瀑布标记），
// _TileH / _TileV = 流动纹理（横向 / 瀑布用），_MaskSize = (蒙版宽, 蒙版高, 蒙版宽/1000, 0)，_T = 时间（秒）。
// 坐标约定：JS 里蒙版像素 y 向下，这里把 uv.y 翻过来换成 y 向下的像素坐标再算，公式和网页版一致。
Shader "GuiQin/Water"
{
    Properties
    {
        _MainTex ("Mask", 2D) = "white" {}
        _BgTex ("Background", 2D) = "black" {}
        _FlowTex ("Flow", 2D) = "gray" {}
        _TileH ("Tile H", 2D) = "white" {}
        _TileV ("Tile V", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BgTex;
            sampler2D _FlowTex;
            sampler2D _TileH;
            sampler2D _TileV;
            float4 _MaskSize;
            float _T;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 全部用 tex2Dlod（贴图都没 mipmap）：避免在分支里采样引发的导数警告/报错
            float4 S (sampler2D t, float2 uv) { return tex2Dlod(t, float4(uv, 0.0, 0.0)); }

            // 一层流动纹理：纹理坐标 = 像素坐标/缩放 - 流向*速度*倍速（和 JS 的 pattern 平移一致），256 像素一个周期
            float4 flowLayer (sampler2D tex, float2 pxp, float sc, float2 flow, float spm)
            {
                float2 pc = pxp / sc - flow * spm;
                return S(tex, pc / 256.0);
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float m = S(_MainTex, uv).r;
                if (m < 0.02) discard;

                float4 fl = S(_FlowTex, uv);
                float2 dir = fl.rg * 2.0 - 1.0;
                float spd = fl.b * 128.0;
                bool vert = fl.a > 0.5;

                float f = _MaskSize.z;
                float2 pxp = float2(uv.x, 1.0 - uv.y) * _MaskSize.xy;     // 蒙版像素，y 向下
                float2 pf = pxp / f;                                       // 除掉 f：和 JS 里的 xf / yf 一样（按 1000 宽为准）
                float A = 1.15 * f;

                float2 off;
                float2 offV = float2(A * 1.7 * sin(pf.y * 0.5 - _T * 9.0 + pf.x * 0.2), 0.0);
                float2 offH = float2(A * sin(pf.y * 0.16 + _T * 1.7 + pf.x * 0.05), A * sin(pf.x * 0.13 + _T * 1.35 + pf.y * 0.04));
                off = vert ? offV : offH;
                // float2(A * sin(pf.y * 0.16 + _T * 1.7 + pf.x * 0.05), A * sin(pf.x * 0.13 + _T * 1.35 + pf.y * 0.04));
                float2 buv = uv + float2(off.x, -off.y) / _MaskSize.xy;
                float3 col = S(_BgTex, buv).rgb;

                // 岸边水花：蒙版亮度在中间的像素（软边）混白色，随时间脉动
                float e = 1.0 - abs(2.0 * m - 1.0);
                float foamOn = (e > 0.27 && !vert) ? 1.0 : 0.0;
                float fo = e * (0.5 + 0.5 * sin(_T * 1.8 + pf.x * 0.09 + pf.y * 0.07)) * 0.7 * foamOn;
                col = lerp(col, float3(1, 1, 1), fo);

                // 三层流动纹理（缩放、倍速、透明度）
                float2 fv = dir * spd * _T;
                float k = vert ? 1.0 : 0.7;
                float vf = vert ? 1.0 : 0.0;
                float4 t0 = lerp(flowLayer(_TileH, pxp, 1.0, fv, 1.0), flowLayer(_TileV, pxp, 1.0, fv, 1.0), vf);
                col = lerp(col, t0.rgb, t0.a * 0.8 * k);
                float4 t1 = lerp(flowLayer(_TileH, pxp, 1.55, fv, 0.62), flowLayer(_TileV, pxp, 1.55, fv, 0.62), vf);
                col = lerp(col, t1.rgb, t1.a * 0.55 * k);
                float4 t2 = lerp(flowLayer(_TileH, pxp, 0.7, fv, 1.35), flowLayer(_TileV, pxp, 0.7, fv, 1.35), vf);
                col = lerp(col, t2.rgb, t2.a * 0.3 * k);

                // 水面中央（离岸远）再叠一层 1.7 倍速，岸边保持慢
                float2 r = 5.0 * f / _MaskSize.xy;
                float c = min(min(S(_MainTex, uv + float2(r.x, 0)).r, S(_MainTex, uv - float2(r.x, 0)).r),
                              min(S(_MainTex, uv + float2(0, r.y)).r, S(_MainTex, uv - float2(0, r.y)).r));
                c = smoothstep(0.45, 0.6, c) * (1.0 - vf);
                float4 tc = flowLayer(_TileH, pxp, 1.0, fv, 1.7);
                col = lerp(col, tc.rgb, tc.a * 0.38 * c);
                return float4(col, m);
            }
            ENDCG
        }
    }
    Fallback Off
}
