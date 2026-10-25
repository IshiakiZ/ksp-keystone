// Keystone/FarMotion: made from tools/shaderpack/farmotion.glsl by tools/unityshaders/port.py. Do not edit: edit the GLSL and run that again.
Shader "Keystone/FarMotion"
{
    Properties
    {
        _FmFar ("FmFar", 2D) = "" {}
        _FmCloudsPic ("FmCloudsPic", 2D) = "" {}
        _FmCloudsDist ("FmCloudsDist", 2D) = "" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One Zero
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma only_renderers d3d11 glcore metal vulkan
            #include "UnityCG.cginc"

            // (the GLSL has each of the game's matrices as its four columns; HLSL has them as rows)
            #define hlslcc_mtx4x4unity_ObjectToWorld transpose(unity_ObjectToWorld)
            #define hlslcc_mtx4x4unity_WorldToObject transpose(unity_WorldToObject)
            #define hlslcc_mtx4x4unity_MatrixVP transpose(UNITY_MATRIX_VP)
            #define hlslcc_mtx4x4unity_MatrixV transpose(UNITY_MATRIX_V)
            #define SampleLod(S, uv, lod) S.SampleLevel(sampler##S, uv, lod)
            #define SampleFlat(S, uv) S.SampleLevel(sampler##S, uv, 0.0)
            #define SampleGradient(S, uv, dx, dy) S.SampleGrad(sampler##S, uv, dx, dy)
            #define Mod(x, y) ((x) - (y) * floor((x) / (y)))
            #define GreaterThan(a, b) ((a) > (b))
            #define GreaterThanEqual(a, b) ((a) >= (b))
            #define LessThan(a, b) ((a) < (b))
            #define LessThanEqual(a, b) ((a) <= (b))
            float4 TexelFetch(Texture2D t, int2 p, int lod) { return t.Load(int3(p, lod)); }
            float4 TexelFetch(Texture3D t, int3 p, int lod) { return t.Load(int4(p, lod)); }
            int2 TextureSize(Texture2D t, int lod) { uint w, h; t.GetDimensions(w, h); return int2(w, h); }
            bool AllZero(float4 v) { return !any(v); }
            // (one of a vector's numbers, chosen by a variable, read and given a new value: see chosen in tools/unityshaders/port.py)
            float Pick(float2 v, int n) { return n <= 0 ? v.x : v.y; }
            float Pick(float3 v, int n) { return n <= 0 ? v.x : n == 1 ? v.y : v.z; }
            float Pick(float4 v, int n) { return n <= 0 ? v.x : n == 1 ? v.y : n == 2 ? v.z : v.w; }
            float2 Put(float2 v, int n, float x) { return float2(n == 0 ? x : v.x, n == 1 ? x : v.y); }
            float3 Put(float3 v, int n, float x) { return float3(n == 0 ? x : v.x, n == 1 ? x : v.y, n == 2 ? x : v.z); }
            float4 Put(float4 v, int n, float x) { return float4(n == 0 ? x : v.x, n == 1 ? x : v.y, n == 2 ? x : v.z, n == 3 ? x : v.w); }
            // gl_FragCoord: which pixel is being drawn, its rows counted from the bottom, as every picture's are read here. Direct3D counts
            // the rows of what it draws into from the top, and the game draws into its pictures upside down to make up for it (saying so with
            // _ProjectionParams.x at -1): so where it is drawing straight to the screen, the row is turned over, or each picture read by pixel
            // (the camera's depth, the smoke drawn small) would be read upside down.
            float4 FragCoord(float4 pos)
            {
            #if UNITY_UV_STARTS_AT_TOP
                if (_ProjectionParams.x > 0.0) pos.y = _ScreenParams.y - pos.y;
            #endif
                return pos;
            }

            float4 _FmSheet;     // xy: 1 (the sheet as large as the screen), z: its depth, w: 1
            float4 _FmLook;     // x, y: the camera's projection without the smoothing's shake, its scale across and up ([0][0], [1][1]); z, w: how far off its middle it is ([0][2], [1][2])
            float4 _FmRight;     // the camera's three axes in the scene (units): to its right, up, and the way it looks
            float4 _FmUp;
            float4 _FmAhead;
            float4 _FmNearZ;     // x, y: what turns the near camera's depth d into how far ahead along its axis a thing is, 1 / (x d + y); z: 1 where there is a far camera; w: 1 if depths run backwards (nearest 1, nothing 0)
            float4 _FmFarZ;     // x, y: the same for the far camera's depth
            float4 _FmNowX;     // rows 0, 1 and 3 of what takes a place, given from the camera, to the picture now (its projection without the shake, times its turn)
            float4 _FmNowY;
            float4 _FmNowW;
            float4 _FmWasX;     // and the same a frame ago, from where the camera was then
            float4 _FmWasY;
            float4 _FmWasW;
            float4 _FmGoesX;     // where a place fixed to the planet, given from the camera now, was a frame ago, given from the camera then: rows of the planet's turn since (xyz) and of its move from the camera's (w)
            float4 _FmGoesY;
            float4 _FmGoesZ;
            float4 _FmSea;     // xyz: the planet's middle, from the camera (m); w: the radius of its sea's level (nothing: no sea)
            float4 _FmSeaAt;     // x: how high the camera is above the sea's level (m; under it, less than nothing); y: (testing) 1 writes every pixel, the near camera's own solid things too
            float4 _FmClouds;     // x: 1 if this frame's clouds are given; y: how much of what is behind them they must hide to be followed instead of it
            Texture2D _FmFar; SamplerState sampler_FmFar;     // the far camera's depths of its solid things, as it drew them this frame
            Texture2D _FmCloudsPic; SamplerState sampler_FmCloudsPic;     // the clouds, as laid on the picture: a, how much shows through them
            Texture2D _FmCloudsDist; SamplerState sampler_FmCloudsDist;     // r: how far off along the ray they are, on the whole, km
            Texture2D _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 vs_TEXCOORD0 : TEXCOORD0;
            };

            v2f vert(float3 in_POSITION0 : POSITION)
            {
                v2f OUT = (v2f)0;
                // One triangle that covers the whole screen, given in the screen's own coordinates (-1 to 1 each way).
                OUT.pos = float4(in_POSITION0.xy * _FmSheet.xy, _FmSheet.z, _FmSheet.w);
                // (drawn into whatever the camera is drawing into, which may lie either way: as for the lens's second go, above; Natural
                // Light's veil and the sea's lens in the water the same; and Cinema Camera's look, whose picture came out upside down on
                // Direct3D without it, the camera drawing into a picture of its own there (the PC, 2026-10-20))
                float way = 1.0;
#if UNITY_UV_STARTS_AT_TOP
                way = _ProjectionParams.x;
#endif
                OUT.vs_TEXCOORD0 = float2(in_POSITION0.x * 0.5 + 0.5, in_POSITION0.y * way * 0.5 + 0.5);
                return OUT;
            }

            // How far what lies beyond the near camera has moved across the picture since the last frame, written into the game's picture
            // of how far each pixel has moved (its motion vectors), which a smoothing between frames goes by (TUFX's temporal
            // antialiasing, on the flight's near camera), and Keystone's lens when it smears.
            //
            // The game draws that picture from the near camera's own solid things alone. Where the game draws its scene with two cameras
            // (OpenGL: the near one to 400 m, the far one from there on), everything the far camera drew is not in it: the ground beyond
            // 400 m, the sea, the clouds and the sky were all taken for things on the near camera's farthest plane, 400 m off. With the
            // camera still that is no matter. Climbing at 300 m/s, a frame's five metres move a place 400 m off some eight pixels on a
            // picture 720 lines tall (seventeen at 600 m/s), and the sea 2 to 20 km off a pixel or two or a fraction of one: the
            // smoothing fetched each pixel's past from as far from where it was, threw it out as not like the pixels round about, and
            // showed the frame as drawn, shaken as it shakes the near camera in eight places in turn. The sea between the clouds at 14-16 km swung by up to 117 of 255 from one frame to the
            // next in that pattern, and with the smoothing put to another kind it did not (the main chat's films, 2026-10-18). And the
            // sea, which is see-through and drawn after the solid things, is not in it on either camera: with one camera (Direct3D) its
            // pixels were given the motion of the bottom under it.
            //
            // So, for each pixel, the nearest of: the near camera's own solid thing there (whose motion the game has, and which is left
            // as it is), the far camera's (from its depths, kept as it drew them), the sea (the sphere of the sea's level: its waves
            // are metres where the distances are kilometres), and, where a mod hands them over, clouds that hide most of what is
            // behind them; each taken as fixed to the planet (where it was a frame ago: as far back as the planet has moved and turned
            // in the scene since, which is how far the game moved the world under a craft that flies fast, at each of its steps), and
            // where there is nothing at all, the sky, which only the camera's own turning moves.

            float4 frag(v2f IN) : SV_Target
            {
                float4 result = float4(0.0, 0.0, 0.0, 0.0);
                float2 uv = IN.vs_TEXCOORD0;
                // (the way this pixel looks, as long as one metre along the camera's axis: a place one metre ahead is at (x + [0][2]) / [0][0]
                // across, where x runs from -1 to 1 over the picture)
                float2 c = (uv * 2.0 - 1.0 + _FmLook.zw) / _FmLook.xy;
                float3 way = _FmAhead.xyz + _FmRight.xyz * c.x + _FmUp.xyz * c.y;
                float along = length(way);
                bool backwards = _FmNearZ.w > 0.5;
                float d = SampleLod(_CameraDepthTexture, uv, 0.0).r;
                bool own = backwards ? d > 0.0 : d < 1.0;
                // (how far off along the ray the solid thing here is: nothing found, as good as endless)
                float t = 1e30;
                if (own) t = along / (_FmNearZ.x * d + _FmNearZ.y);
                else if (_FmNearZ.z > 0.5)
                {
                    float f = SampleLod(_FmFar, uv, 0.0).r;
                    if (backwards ? f > 0.0 : f < 1.0) t = along / (_FmFarZ.x * f + _FmFarZ.y);
                }
                // The sea, where its level is nearer: from above, where the ray first meets the sphere; from under the water, where it leaves it.
                // (The camera's height above it, h, is handed over worked out in double precision: |middle|^2 - R^2 is h (2R + h), and the
                // nearer meeting is that over b + the root, which loses nothing where the ray only grazes the sea, far off.)
                bool sea = false;
                if (_FmSea.w > 0.0)
                {
                    float3 n = way / along;
                    float b = dot(n, _FmSea.xyz), h = _FmSeaAt.x, q = h * (2.0 * _FmSea.w + h);
                    float disc = b * b - q;
                    if (disc >= 0.0)
                    {
                        float root = sqrt(disc);
                        float meets = h >= 0.0 ? (b > 0.0 ? q / (b + root) : -1.0) : b + root;
                        if (meets > 0.0 && meets < t) { t = meets; sea = true; }
                    }
                }
                // (the near camera's own solid thing in front of the sea: the game's motion for it stands, the craft's own among them)
                if (own && !sea && _FmSeaAt.y < 0.5) discard;
                { [loop] for (int once = _FmClouds.x > 0.5 ? 0 : 1; once < 1; once++)
                {
                    float hides = 1.0 - SampleLod(_FmCloudsPic, uv, 0.0).a;
                    float at = SampleLod(_FmCloudsDist, uv, 0.0).r * 1000.0;
                    if (hides > _FmClouds.y && at > 0.0 && at < t) t = at;
                } }
                float4 here = t < 1e29 ? float4(way * (t / along), 1.0) : float4(way, 0.0);
                float4 was = t < 1e29 ? float4(dot(_FmGoesX, here), dot(_FmGoesY, here), dot(_FmGoesZ, here), 1.0) : here;
                float2 now = float2(dot(_FmNowX, here), dot(_FmNowY, here)) / max(dot(_FmNowW, here), 1e-6);
                float2 before = float2(dot(_FmWasX, was), dot(_FmWasY, was)) / max(dot(_FmWasW, was), 1e-6);
                // (in widths and heights of the picture: half of what the two differ by, which runs from -1 to 1)
                float2 moved = clamp((now - before) * 0.5, ((float2)(-1.0)), ((float2)(1.0)));
#if UNITY_UV_STARTS_AT_TOP
                moved.y = -moved.y;
#endif
                result = float4(moved, 0.0, 1.0);
                return result;
            }
            ENDCG
        }
    }
}
