// Keystone/Lens: made from tools/shaderpack/lens.glsl by tools/unityshaders/port.py. Do not edit: edit the GLSL and run that again.
Shader "Keystone/Lens"
{
    Properties
    {
        _LensScene ("LensScene", 2D) = "" {}
        _LensBlur ("LensBlur", 2D) = "" {}
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
            #define Mod(x, y) ((x) - (y) * floor((x) / (y)))
            #define GreaterThan(a, b) ((a) > (b))
            float4 TexelFetch(Texture2D t, int2 p, int lod) { return t.Load(int3(p, lod)); }
            float4 TexelFetch(Texture3D t, int3 p, int lod) { return t.Load(int4(p, lod)); }
            int2 TextureSize(Texture2D t, int lod) { uint w, h; t.GetDimensions(w, h); return int2(w, h); }
            bool AllZero(float4 v) { return !any(v); }

            float4 _LensSheet;     // xy: 1 (the sheet as large as the screen), z: its depth, w: 1
            float4 _LensStage;     // x: 1 for the first of those, 2 for the second
            float4 _LensSize;     // xy: one pixel of the picture, as a part of it; zw: how many pixels wide and high it is
            float4 _LensFocus;     // x: the distance in focus, in metres, y: how wide, in pixels, the disc of something infinitely far off is across its half, z: the widest a disc is drawn (the same measure), w: how much further out each look is than the last (see the loop)
            float4 _LensMove;     // x: the part of a frame the shutter is open for (0: no smear), y: how many looks along the way, z: the furthest something is smeared, as a part of the picture, w: 1 if there is a blurred picture to put in
            float4 _LensMarks;     // x: how much darker the corners are, y: how far the colours part, z: how much the picture is bent, w: how grainy it is
            float4 _LensFrame;     // x: a number that is different in every frame, y: the picture's width over its height, z: how large a grain is, in pixels
            Texture2D _LensScene; SamplerState sampler_LensScene;     // the picture as the game drew it
            Texture2D _LensBlur; SamplerState sampler_LensBlur;     // rgb: the picture out of focus (half size), a: how much of it goes into the finished picture
            Texture2D _CameraMotionVectorsTexture; SamplerState sampler_CameraMotionVectorsTexture;
            Texture2D _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;

            float4 DepthFetch(int2 p, int lod)
            {
            #if UNITY_UV_STARTS_AT_TOP
                if (_ProjectionParams.x > 0.0) { uint w, h; _CameraDepthTexture.GetDimensions(w, h); p.y = (int)h - 1 - p.y; }
            #endif
                return _CameraDepthTexture.Load(int3(p, lod));
            }


            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 vs_TEXCOORD0 : TEXCOORD0;
            };

            v2f vert(float3 in_POSITION0 : POSITION)
            {
                v2f OUT = (v2f)0;
                // One triangle that covers the whole screen, given in the screen's own coordinates (-1 to 1 each way).
                OUT.pos = float4(in_POSITION0.xy * _LensSheet.xy, _LensSheet.z, _LensSheet.w);
                // (the first of the two goes is drawn into a picture of the lens's own, which lies as every picture does on this system;
                // the second into whatever the camera is drawing into, which may lie either way: see SPECIAL in tools/unityshaders/port.py)
                float way = 1.0;
#if UNITY_UV_STARTS_AT_TOP
                way = _LensStage.x < 1.5 ? -1.0 : _ProjectionParams.x;
#endif
                OUT.vs_TEXCOORD0 = float2(in_POSITION0.x * 0.5 + 0.5, in_POSITION0.y * way * 0.5 + 0.5);
                return OUT;
            }

            // What a lens, a shutter and a film do to a picture (Keystone's Lens.cs sets it going, once the game has
            // drawn everything but its own interface). The picture as it stands is copied, and drawn again from the copy
            // in two goes:
            //
            // 1. Out of focus. A point that is not at the distance the lens is focused on is drawn by a real lens as a
            //    disc, the wider the further it is from that distance and the wider the aperture. So for each pixel the
            //    picture round about is gathered up, out to the widest such disc: from each place looked at, as much as
            //    its own disc is wide enough to reach this pixel. (What lies behind does not spread over something
            //    nearer and sharp; what is nearer and blurred does spread over what lies behind it.) This is drawn at half
            //    size: it is a blur.
            // 2. The finished picture. The sharp picture with the blurred one put in where it counts; smeared along the
            //    way each thing has moved while the shutter was open (the game says how far each pixel has moved since
            //    the last frame); bent as a wide lens bends it, its colours parted a little towards the corners, its
            //    corners darker; and grainy as fast film is.

            // How far ahead of the camera what a pixel shows is, in metres.
            float ahead(float2 uv)
            {
                return 1.0 / (_ZBufferParams.z * SampleLod(_CameraDepthTexture, uv, 0.0).r + _ZBufferParams.w);
            }

            // Half the width of the disc a point that far off is drawn as, in pixels.
            float discOf(float d)
            {
                return min(abs(_LensFocus.y * (d - _LensFocus.x) / max(d, 0.01)), _LensFocus.z);
            }

            float4 outOfFocus(float2 uv)
            {
                float here = ahead(uv), mine = discOf(here);
                float3 sum = SampleLod(_LensScene, uv, 0.0).rgb;
                float looks = 1.0, reached = 0.0, radius = 1.0;
                // (round and round, a little further out each time, by the angle that never comes back on itself: the looks
                // end up spread evenly over the whole disc)
                float2 turn = float2(1.0, 0.0);
                const float2x2 further = transpose(float2x2(-0.7373688, 0.6754903, -0.6754903, -0.7373688));
                [loop] for (int n = 0; n < 96; n++)
                {
                    if (radius > _LensFocus.z) break;
                    turn = mul(further, turn);
                    float2 there = uv + turn * radius * _LensSize.xy;
                    float d = ahead(there), wide = discOf(d);
                    if (d > here) wide = min(wide, mine * 2.0);
                    float counts = smoothstep(radius - 1.0, radius + 1.0, wide);
                    sum += lerp(sum / looks, SampleLod(_LensScene, there, 0.0).rgb, counts);
                    looks += 1.0;
                    if (d < here * 0.97) reached = max(reached, counts);
                    radius += _LensFocus.w / radius;
                }
                return float4(sum / looks, max(smoothstep(0.6, 2.2, mine), reached));
            }

            float3 picture(float2 uv)
            {
                float3 sharp = SampleLod(_LensScene, uv, 0.0).rgb;
                if (_LensMove.w < 0.5) return sharp;
                float4 blurred = SampleLod(_LensBlur, uv, 0.0);
                return lerp(sharp, blurred.rgb, blurred.a);
            }

            float chance(float2 p)
            {
                float3 q = frac(((float3)(p.xyx)) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float4 frag(v2f IN) : SV_Target
            {
                float4 result = float4(0.0, 0.0, 0.0, 0.0);
                float2 uv = IN.vs_TEXCOORD0;
                if (_LensStage.x < 1.5) { result = outOfFocus(uv); return result; }

                // How far from the middle of the picture, as a part of the way to its corners (the lens is round: the picture's shape is allowed for).
                float2 out2 = (uv - 0.5) * float2(_LensFrame.y, 1.0);
                float corner = 0.25 * (_LensFrame.y * _LensFrame.y + 1.0), off = dot(out2, out2) / corner;
                // The bend of a wide lens: straight lines bow outward, most towards the corners (which stay where they are).
                float2 from = 0.5 + (uv - 0.5) * (1.0 + _LensMarks.z * off) / (1.0 + _LensMarks.z);
                // Its colours part a little towards the corners: red is drawn a touch larger than green, blue a touch smaller.
                float parted = _LensMarks.y * off;
                float2 red = 0.5 + (from - 0.5) * (1.0 + parted), blue = 0.5 + (from - 0.5) * (1.0 - parted);

                float3 colour;
                if (_LensMove.x > 0.0)
                {
                    // What moved while the shutter was open is smeared along the way it went.
                    float2 went = SampleLod(_CameraMotionVectorsTexture, from, 0.0).xy * _LensMove.x;
                    float smear = length(went);
                    if (smear > _LensMove.z) went *= _LensMove.z / smear;
                    colour = ((float3)(0.0));
                    int looks = int(_LensMove.y);
                    [loop] for (int n = 0; n < 16; n++)
                    {
                        if (n >= looks) break;
                        // (each look a little differently placed from pixel to pixel and frame to frame, so that few looks show as grain and not as copies)
                        float2 along = went * ((float(n) + chance(IN.pos.xy + _LensFrame.x * 7.31)) / _LensMove.y - 0.5);
                        if (parted > 0.0) colour += float3(picture(red + along).r, picture(from + along).g, picture(blue + along).b);
                        else colour += picture(from + along);
                    }
                    colour /= float(looks);
                }
                else if (parted > 0.0) colour = float3(picture(red).r, picture(from).g, picture(blue).b);
                else colour = picture(from);

                // Darker corners: less of the light that comes in at a slant gets through a lens, and less still with the aperture wide open.
                float dark = 1.0 + _LensMarks.x * off;
                colour /= dark * dark;

                // Grain: fast film is made of bigger crystals, and they show most in the middle tones.
                if (_LensMarks.w > 0.0)
                {
                    float2 crystal = floor(IN.pos.xy / _LensFrame.z);
                    float speck = chance(crystal + _LensFrame.x * 3.17) + chance(crystal * 1.37 + _LensFrame.x * 5.71 + 19.0) - 1.0;
                    float light = clamp(dot(colour, float3(0.2126, 0.7152, 0.0722)), 0.0, 1.0);
                    colour += colour * speck * _LensMarks.w * (1.4 - light) + ((float3)(speck * _LensMarks.w * 0.02));
                }
                result = float4(max(colour, 0.0), 1.0);
                return result;
            }
            ENDCG
        }
    }
}
