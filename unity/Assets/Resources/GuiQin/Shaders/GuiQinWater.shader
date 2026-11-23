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
        _VillageDetail ("Village Water Detail", Float) = 0
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
            float _VillageDetail;
            float4 _BridgeFoam, _StepsFoam, _WheelFoam, _FallFoam;

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

            // 岸边局部水花：碎泡随水推进，从中心往外自然减弱。
            float foamPatch(float2 p, float4 patch, float2 flow, float speed, float phase)
            {
                float2 offset = p - patch.xy;
                float2 area = offset / patch.zw;
                float fade = saturate(1.0 - dot(area, area));
                if (fade <= 0.0) return 0.0;
                float along = dot(offset, flow);
                float across = dot(offset, float2(-flow.y, flow.x));
                float folds = sin(across * 0.31 + phase) + 0.55 * sin(across * 0.71);
                float wave = along * 0.62 - _T * speed + folds + phase;
                float crest = 0.5 + 0.5 * sin(wave);
                float trail = 0.5 + 0.5 * sin(wave - 0.9);
                float breaks = smoothstep(0.18, 0.75, 0.5 + 0.5 * sin(across * 0.52 + along * 0.12 + phase));
                return fade * sqrt(fade) * (0.8 * pow(crest, 3.2) + 0.2 * pow(trail, 2.0)) * breaks;
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
                // 向内采样区分河岸和河心，让折射在岸边渐弱。
                float2 edgeRadius = 6.0 * f / _MaskSize.xy;
                float inner = min(min(S(_MainTex, uv + float2(edgeRadius.x, 0)).r, S(_MainTex, uv - float2(edgeRadius.x, 0)).r),
                                  min(S(_MainTex, uv + float2(0, edgeRadius.y)).r, S(_MainTex, uv - float2(0, edgeRadius.y)).r));
                float core = smoothstep(0.28, 0.85, inner);
                float2 across = float2(-dir.y, dir.x);
                float2 drift = dir * spd * _T;
                float2 water = (pxp - drift) / f;
                float axial = dot(water, dir), lateral = dot(water, across);
                float wave = sin(lateral * 0.21 + axial * 0.075) * sin(axial * 0.16 - lateral * 0.08);
                float2 off = (across * wave + dir * sin(axial * 0.12 + lateral * 0.34) * 0.35) * f;
                off *= lerp(0.12, vert ? 1.2 : 0.82, core);
                float2 buv = uv + float2(off.x, -off.y) / _MaskSize.xy;
                float3 col = S(_BgTex, buv).rgb;

                // 流纹分两层；瀑布在纵向拉长，平水的第二层在河心更明显。
                float4 t0, t1;
                float2 tilePx = pxp / f;
                if (vert)
                {
                    t0 = flowLayer(_TileV, tilePx, 0.92, drift / f, 1.02);
                    t1 = flowLayer(_TileV, tilePx, 0.57, drift / f, 1.65);
                }
                else
                {
                    t0 = flowLayer(_TileH, tilePx, 0.95, drift / f, 0.68);
                    t1 = flowLayer(_TileH, tilePx + float2(57.0, 23.0), 0.68, drift / f, 1.35);
                }
                float wet = smoothstep(0.15, 0.74, m);
                col = lerp(col, t0.rgb, t0.a * wet * (vert ? 0.69 : 0.54));
                col = lerp(col, t1.rgb, t1.a * wet * (vert ? 0.34 : 0.26 * core));

                // 泡沫主要留在水侧软边，碎段顺着流向移动。
                float bank = (1.0 - core) * smoothstep(0.21, 0.70, m);
                float breaks = sin(axial * 0.18 + sin(lateral * 0.31)) * 0.66 + sin(axial * 0.51 - lateral * 0.41) * 0.34;
                float foamLine = bank * smoothstep(0.37, 0.84, breaks);
                float foamStrength = vert ? 0.17 : lerp(0.23, 0.11, _VillageDetail);
                col = lerp(col, float3(0.88, 0.97, 0.96), foamLine * foamStrength);

                if (_VillageDetail > 0.5)
                {
                    float foam = foamPatch(pf, _BridgeFoam, float2(0.55, 0.84), 3.8, 0.3) * 1.02;
                    foam += foamPatch(pf, _StepsFoam, float2(-0.94, 0.34), 3.2, 2.4) * 1.13;
                    foam += foamPatch(pf, _WheelFoam, float2(-0.52, 0.85), 5.0, 4.6) * 1.23;
                    foam += foamPatch(pf, _FallFoam, float2(0.12, 0.99), 6.9, 1.2) * 1.31;
                    float water = smoothstep(0.08, 0.64, m);
                    col = lerp(col, float3(0.91, 0.98, 0.95), saturate(foam) * water);
                }
                return float4(col, m);
            }
            ENDCG
        }
    }
    Fallback Off
}
