Shader "Custom/OutlineZAlwaysStencil" // Изменил имя для ясности
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }

        // Pass 1: Записываем силуэт объекта в Stencil буфер
        Pass
        {
            Name "STENCIL_MASK"
            Cull Back // Рисуем передние грани
            ZWrite Off
            ZTest Always // Важно для рентгена
            ColorMask 0 // Не рисуем цвет

            Stencil
            {
                Ref 1 // Значение, которое записываем
                Comp Always // Всегда проходим тест сравнения
                Pass Replace // Заменяем значение в буфере на Ref (1)
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 pos : SV_POSITION;
            };

            // Простой вершинный шейдер, просто трансформирует вершины
            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            // Пустой фрагментный шейдер, т.к. ColorMask 0
            fixed4 frag(v2f i) : SV_Target {
                return 0;
            }
            ENDCG
        }

        // Pass 2: Рисуем смещенный контур, используя Stencil тест
        Pass
        {
            Name "OUTLINE_DRAW"
            Cull Front // Рисуем задние грани (раздутый силуэт)
            ZWrite Off
            ZTest Always // Важно для рентгена
            Blend SrcAlpha OneMinusSrcAlpha // Для прозрачности цвета

            Stencil
            {
                Ref 1 // Значение, с которым сравниваем
                Comp NotEqual // Рисуем только если значение в буфере НЕ равно Ref (1)
                Pass Keep // Не меняем значение в буфере
            }

            CGPROGRAM
            #pragma vertex vert_outline
            #pragma fragment frag_outline
            #include "UnityCG.cginc"

            struct appdata_outline {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f_outline {
                float4 pos : SV_POSITION;
                float outlineWidth : TEXCOORD0;
            };

            uniform fixed4 _OutlineColor;
            uniform float _OutlineWidth;

            v2f_outline vert_outline(appdata_outline v) {
                v2f_outline o;
                float4 pos = v.vertex;
                float3 normal = normalize(v.normal);
                // Смещаем вершину для контура
                pos.xyz += normal * _OutlineWidth;
                o.pos = UnityObjectToClipPos(pos);
                o.outlineWidth = _OutlineWidth;
                return o;
            }

            fixed4 frag_outline(v2f_outline i) : SV_Target {
                // Проверка на нулевую ширину для чистоты
                if (i.outlineWidth < 0.0001) {
                    return fixed4(0, 0, 0, 0); // Полностью прозрачный
                }
                // Возвращаем цвет контура
                return _OutlineColor;
            }
            ENDCG
        }
    }
    Fallback Off
}