Shader "Custom/RadialGradientSkybox"
{
    Properties
    {
        _CenterColor ("Center Color", Color) = (1, 1, 1, 1)
        _EdgeColor ("Edge Color", Color) = (0, 0, 0, 1)
        _MaxDistance ("Max Distance", Float) = 1.0
        _CenterPosition ("Center Position (World Space, X, Y, Z)", Vector) = (0, 0.5, 0.4, 0) // Центр в мировых координатах
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 direction : TEXCOORD0;
                float3 worldDirection : TEXCOORD1; // Добавляем мировое направление
            };

            fixed4 _CenterColor;
            fixed4 _EdgeColor;
            float _MaxDistance;
            float4 _CenterPosition; // Центр в мировых координатах (x, y, z, w — w игнорируется)

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.direction = normalize(v.vertex.xyz); // Нормализованное направление от камеры
                o.worldDirection = mul(unity_ObjectToWorld, v.vertex).xyz - _WorldSpaceCameraPos; // Мировое направление от камеры
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Преобразуем центр из мировых координат в нормализованное направление относительно камеры
                float3 worldCenter = _CenterPosition.xyz; // Центр в мировых координатах
                float3 cameraToCenter = worldCenter - _WorldSpaceCameraPos; // Вектор от камеры до центра
                float3 centerDirection = normalize(cameraToCenter); // Нормализуем для использования в skybox

                // Вычисляем расстояние между направлением пикселя и центром
                float distance = length(i.direction - centerDirection); // Евклидово расстояние в нормализованном пространстве
                float gradient = saturate(1.0 - (distance / _MaxDistance));
                return lerp(_EdgeColor, _CenterColor, gradient);
            }
            ENDCG
        }
    }
    FallBack "Skybox/Procedural"
}