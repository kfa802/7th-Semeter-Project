Shader "Medialogy/Lake Surface Details"
{
    Properties
    {
        _ShoreMask ("Terrain shoreline", 2D) = "white" {}
        _HasShore ("Has terrain shoreline", Float) = 0
        _ShoreBounds ("Local shoreline bounds", Vector) = (-5,-5,10,10)
        _IsIce ("Ice mode", Float) = 1
        _Amount ("Freeze or dryness", Range(0,1)) = 0
        _CrackScale ("Cracks per world unit", Float) = 2
        _IceTint ("Ice tint", Color) = (0.48,0.72,0.81,1)
        _WetSoil ("Wet soil", Color) = (0.12,0.085,0.055,1)
        _DrySoil ("Dry soil", Color) = (0.48,0.34,0.21,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_ShoreMask);
            SAMPLER(sampler_ShoreMask);
            CBUFFER_START(UnityPerMaterial)
                float _IsIce, _Amount, _CrackScale, _HasShore;
                float4 _ShoreBounds;
                float4 _IceTint, _WetSoil, _DrySoil;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float fog:TEXCOORD3; float2 shoreUV:TEXCOORD4; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                o.shoreUV=(input.positionOS.xz-_ShoreBounds.xy)/_ShoreBounds.zw;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            float2 Hash(float2 p)
            {
                return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);
            }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p);
                f=f*f*(3.0-2.0*f);
                return lerp(lerp(Hash(i).x,Hash(i+float2(1,0)).x,f.x),
                    lerp(Hash(i+float2(0,1)).x,Hash(i+float2(1,1)).x,f.x),f.y);
            }
            float Clouds(float2 p)
            {
                return Noise(p)*0.57+Noise(p*2.07+17.3)*0.29+Noise(p*4.13+38.1)*0.14;
            }
            // Voronoi edge distance: irregular connected cracks instead of a tiled image.
            float Edge(float2 p)
            {
                float2 cell=floor(p), f=frac(p), nearest=0, bestCell=0;
                float best=100;
                for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
                {
                    float2 g=float2(x,y), r=g+Hash(cell+g)-f;
                    float d=dot(r,r);
                    if(d<best) { best=d; nearest=r; bestCell=g; }
                }
                float edge=100;
                for(int j=-1;j<=1;j++) for(int i=-1;i<=1;i++)
                {
                    float2 g=bestCell+float2(i,j), r=g+Hash(cell+g)-f;
                    float2 delta=r-nearest;
                    if(dot(delta,delta)>0.00001)
                        edge=min(edge,dot(0.5*(nearest+r),normalize(delta)));
                }
                return edge;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.world.xz * _CrackScale;
                float edge=Edge(p);
                float grain=Hash(floor(p*90)).x;
                float width=max(fwidth(edge),0.001);
                float cracks=1-smoothstep(0.012,0.012+width,edge);
                float3 baseColor;
                float gloss;
                float frost=0;
                float iceRelief=0;
                if(_IsIce>0.5)
                {
                    float2 shore=SAMPLE_TEXTURE2D(_ShoreMask,sampler_ShoreMask,input.shoreUV).rg;
                    clip(shore.g-0.5);
                    float variation=Clouds(p*0.7+17.0);
                    float threshold=(_HasShore>0.5)
                        ? saturate(shore.r+(variation-0.5)*0.09)
                        : variation;
                    clip(_Amount*1.12-0.01-threshold);
                    // Warp and selectively fade the cell edges to break up the regular net.
                    float2 iceP=p*0.48;
                    float2 warp=float2(Clouds(iceP*0.8),Clouds(iceP*0.8+41.7))-0.5;
                    float iceEdge=Edge(iceP+warp*0.65);
                    float aa=max(fwidth(iceEdge),0.0008);
                    float thinCrack=1-smoothstep(0.002,0.004+aa,iceEdge);
                    float visibility=smoothstep(0.36,0.67,Clouds(iceP*1.7+24.0));
                    float iceCrack=thinCrack*visibility;
                    float halo=(1-smoothstep(0.006,0.06,iceEdge))*visibility;
                    frost=smoothstep(0.36,0.72,Clouds(iceP*0.65+9.0));
                    float cloud=Clouds(iceP*2.6+31.0);
                    float3 clearIce=lerp(_IceTint.rgb,float3(0.17,0.28,0.31),0.45);
                    baseColor=lerp(clearIce,float3(0.67,0.76,0.77),frost*0.65);
                    baseColor*=0.92+cloud*0.16;
                    baseColor=lerp(baseColor,float3(0.75,0.84,0.85),halo*0.12+iceCrack*0.38);
                    gloss=lerp(0.94,0.48,frost);
                    iceRelief=Clouds(iceP*5.0)*0.004;

                }
                else
                {
                    baseColor=lerp(_WetSoil.rgb,_DrySoil.rgb,_Amount);
                    baseColor*=0.9+grain*0.2;
                    baseColor*=1-cracks*_Amount*0.75;
                    gloss=lerp(0.7,0.08,_Amount);
                }
                float3 n=normalize(input.normal);
                if(_IsIce>0.5)
                {
                    float3 dx=ddx(input.world),dy=ddy(input.world);
                    float3 r1=cross(dy,n),r2=cross(n,dx);
                    float det=dot(dx,r1);
                    float3 gradient=sign(det)*(ddx(iceRelief)*r1+ddy(iceRelief)*r2);
                    n=normalize(max(abs(det),0.000001)*n-gradient);
                }
                float3 view=GetWorldSpaceNormalizeViewDir(input.world);
                if(dot(n,view)<0) n=-n;
                Light light=GetMainLight(TransformWorldToShadowCoord(input.world));
                float diffuse=saturate(dot(n,light.direction));
                float3 halfDir=SafeNormalize(light.direction+view);
                float spec=pow(saturate(dot(n,halfDir)),lerp(12,160,gloss))*gloss;
                float attenuation=light.distanceAttenuation*light.shadowAttenuation;
                float3 color=baseColor*(SampleSH(n)+light.color*diffuse*attenuation);
                color+=light.color*spec*attenuation*0.5;
                if(_IsIce>0.5)
                {
                    float fresnel=0.04+0.96*pow(1-saturate(dot(n,view)),5);
                    half3 reflection=GlossyEnvironmentReflection(reflect(-view,n),
                        input.world,lerp(0.12,0.48,frost),1.0);
                    float reflectionWeight=fresnel*lerp(0.8,0.35,frost);
                    color=color*(1-reflectionWeight)+reflection*reflectionWeight;
                }
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
}
