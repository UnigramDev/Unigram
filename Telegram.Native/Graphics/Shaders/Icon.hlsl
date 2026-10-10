// The Android app's 3D icon shaders - vertex2.glsl, fragment4.glsl (the star) and fragment3.glsl
// (the coin), from TMessagesProj/src/main/assets/shaders - translated by hand. Unlike the
// diamond's there is no generator: they are short, and kept line for line with the GLSL so that a
// change upstream can be diffed across. What had to change is noted where it happens.

#pragma pack_matrix(column_major)

#define TYPE_COIN 1
#define TYPE_DEAL 3

cbuffer Uniforms : register(b0)
{
    float4x4 uMVPMatrix;
    float4x4 world;
    float3 gradientColor1;
    float spec1;
    float3 gradientColor2;
    float spec2;
    float3 normalSpecColor;
    float u_diffuse;

    // u_BackgroundTexture, sampled in screen space. Every host that shows the star in a sheet
    // fills that bitmap with one colour, so it is the colour that is passed.
    float3 backgroundColor;
    float normalSpec;

    float f_xOffset;
    float f_alpha;
    float golden;
    float white;
    float time;
    int type;
    int night;
    float uniformsPadding;
};

// Per draw, because the coin is four meshes and the shader tells them apart by this.
cbuffer Model : register(b1)
{
    int modelIndex;
    int3 modelPadding;
};

Texture2D u_Texture : register(t0);
Texture2D u_NormalMap : register(t1);

// GL_LINEAR with GL's default GL_REPEAT, which the normal map relies on: it is read at twice
// the texture coordinates, scrolling.
SamplerState textureSampler : register(s0);

struct VertexIn
{
    float3 vPosition : POSITION;
    float3 a_Normal : NORMAL;
    float2 a_TexCoordinate : TEXCOORD0;
};

// vertex2's `depth` varying is written and never read, so it is not carried.
struct Varyings
{
    float4 position : SV_Position;
    float3 vNormal : NORMAL;
    float2 vUV : TEXCOORD0;
    float3 modelViewVertex : TEXCOORD1;
};

Varyings VS(VertexIn input)
{
    Varyings output;
    output.vUV = input.a_TexCoordinate;
    output.vNormal = input.a_Normal;
    output.position = mul(uMVPMatrix, float4(input.vPosition, 1.0));
    output.modelViewVertex = input.vPosition;
    return output;
}

// fragment4.glsl

float4 premium_star(Varyings i)
{
    float3 cameraPosition = float3(0, 0, 100);
    float3 vLightPosition2 = float3(-400, 400, 400);
    float3 vLightPosition4 = float3(0, 0, 100);
    float3 vLightPositionNormal = float3(100, -200, 400);

    float3 vNormalW = normalize(mul(world, float4(i.vNormal, 0.0)).xyz);
    float3 vTextureNormal = normalize(u_NormalMap.Sample(textureSampler, (i.vUV + float2(-f_xOffset, f_xOffset)) * 2.0).xyz * 2.0 - 1.0);

    float3 finalNormal = normalize(vNormalW + vTextureNormal);

    float3 color = u_Texture.Sample(textureSampler, i.vUV).xyz;
    float3 viewDirectionW = normalize(cameraPosition);

    float3 angleW = normalize(viewDirectionW + vLightPosition2);
    float specComp2 = max(0., dot(vNormalW, angleW));
    specComp2 = pow(specComp2, max(1., 128.)) * spec1;

    angleW = normalize(viewDirectionW + vLightPosition4);
    float specComp3 = max(0., dot(vNormalW, angleW));
    specComp3 = pow(specComp3, max(1., 30.)) * spec2;

    float diffuse = max(dot(vNormalW, viewDirectionW), (1.0 - u_diffuse));

    float mixValue = distance(i.vUV, float2(1, 0));
    float4 gradientColorFinal = float4(lerp(gradientColor1, gradientColor2, mixValue), 1.0);

    angleW = normalize(viewDirectionW + vLightPositionNormal);
    float normalSpecComp = max(0., dot(finalNormal, angleW));
    normalSpecComp = pow(normalSpecComp, max(1., 128.)) * normalSpec;

    angleW = normalize(viewDirectionW + vLightPosition2);
    float normalSpecComp2 = max(0., dot(finalNormal, angleW));
    normalSpecComp2 = pow(normalSpecComp2, max(1., 128.)) * normalSpec;

    float4 normalSpecFinal = float4(normalSpecColor, 0.0) * (normalSpecComp + normalSpecComp2);
    float4 specFinal = float4(color, 0.0) * (specComp2 + specComp3);

    float4 fragColor = gradientColorFinal + specFinal + normalSpecFinal;
    return lerp(float4(backgroundColor, 1.0), fragColor, diffuse) * f_alpha;
}

// Each specular term below is the same expression with a different light; it is written out
// in full, as the GLSL does, rather than folded into a helper, so that the two stay diffable.
float4 golden_star(Varyings i)
{
    float3 pos = i.modelViewVertex / 100.0 + .5;
    float specTexture = u_Texture.Sample(textureSampler, i.vUV).y * clamp(i.vNormal.z, 0.0, 1.0);

    float gradientMix = distance(pos.xy, float2(1, 1));
    gradientMix -= .05 * specTexture;
    gradientMix = clamp(gradientMix, 0.0, 1.0);
    float4 color = float4(lerp(gradientColor1, gradientColor2, gradientMix), 1.0);

    float3 norm = normalize(mul(world, float4(i.vNormal, 0.0)).xyz);

    float3 flecksNormal = normalize(1.0 - u_NormalMap.Sample(textureSampler, (i.vUV + .7 * float2(-f_xOffset, f_xOffset)) * 2.0).xyz);

    float3 lightPos = float3(-3., -3., 20.);
    float3 lightDir = normalize(lightPos - pos);
    float diffuse = max(dot(norm, lightDir), 0.0);

    float spec = 0.0;

    lightPos = float3(-1., .7, .2);
    spec += specTexture * clamp(2.0 * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 2.0), 0.0, 1.0) / 6.0;
    lightPos = float3(8., .7, .5);
    spec += specTexture * clamp(2.0 * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 2.0), 0.0, 1.0) / 6.0;

    lightPos = float3(-3., -3., .5);
    spec += clamp(2.0 * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 2.0), 0.0, 1.0) / 4.0;

    lightPos = float3(4., 3., 2.5);
    spec += clamp(1.5 * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 2.0), 0.0, 1.0) / 6.0;

    lightPos = float3(-33., .5, 30.);
    spec += clamp(2.0 * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 2.0), 0.0, 1.0) / 12.0;

    spec = clamp(spec, 0.0, 0.7);

    lightPos = float3(10., .5, 3.3);
    spec += lerp(0.8, 1.0, specTexture) * clamp(2.0 * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 2.0), 0.0, 1.0) / 8.0;

    lightPos = float3(-10., .5, 3.7);
    spec += lerp(0.8, 1.0, specTexture) * clamp(2.0 * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 2.0), 0.0, 1.0) / 8.0;

    lightPos = float3(.5, 12., 1.5);
    spec += lerp(0.8, 1.0, specTexture) * clamp(2.0 * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 2.0), 0.0, 1.0) / 8.0;

    spec = clamp(spec, 0.0, 0.9);

    color = lerp(float4(0.0, 0.0, 0.0, 1.0), color, .8 + .3 * diffuse);
    float4 specColor = float4(lerp(1.8, 2.0, specTexture) * gradientColor1, 1.0);
    color = lerp(color, specColor, spec);

    float flecksSpec = 0.0;
    lightPos = float3(1.2, -.2, .5);
    flecksSpec += clamp(2.0 * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 2.0), 0.0, 1.0);

    color = lerp(color, specColor,
        clamp(flecksSpec * abs(i.vNormal.z) * (flecksNormal.z), 0.2, 0.3) - .2
    );

    return lerp(color * f_alpha, float4(1.0, 1.0, 1.0, 1.0), white);
}

float4 StarPS(Varyings i) : SV_Target
{
    return lerp(premium_star(i), golden_star(i), golden);
}

// fragment3.glsl

float4 CoinPS(Varyings i) : SV_Target
{
    float2 uv = i.vUV;
    if (modelIndex == 2) {
        uv *= 2.0;
        uv = frac(uv);
    }
    uv.x = 1.0 - uv.x;

    float diagonal = ((uv.x + uv.y) / 2.0 - .15) / .6;
    float3 baseColor;
    if (modelIndex == 0) {
        baseColor = lerp(
            float3(0.95686, 0.47451, 0.93725),
            float3(0.46274, 0.49411, 0.9960),
            diagonal
        );
    } else if (modelIndex == 3) {
        baseColor = lerp(
            float3(0.95686, 0.47451, 0.93725),
            float3(0.46274, 0.49411, 0.9960),
            diagonal
        );
        baseColor = lerp(baseColor, float3(1.0, 1.0, 1.0), .3);
    } else if (modelIndex == 1) {
        baseColor = lerp(
            float3(0.67059, 0.25490, 0.80000),
            float3(0.39608, 0.18824, 0.98039),
            diagonal
        );
    } else if (type == TYPE_DEAL) {
        baseColor = lerp(
            float3(0.91373, 0.62353, 0.99608),
            float3(0.67451, 0.58824, 1.00000),
            clamp((uv.y - .1) / .8, 0.0, 1.0)
        ) * 1.05;

        baseColor = lerp(baseColor, float3(1.0, 1.0, 1.0), .1 + .25 * u_Texture.Sample(textureSampler, i.vUV).a);
        if (night != 0) {
            baseColor = lerp(baseColor, float3(0.0, 0.0, 0.0), .06);
        }
    } else {
        baseColor = lerp(
            float3(0.91373, 0.62353, 0.99608),
            float3(0.67451, 0.58824, 1.00000),
            clamp((uv.y - .1) / .8, 0.0, 1.0)
        );

        baseColor = lerp(baseColor, float3(1.0, 1.0, 1.0), .1 + .45 * u_Texture.Sample(textureSampler, i.vUV).a);
        if (night != 0) {
            baseColor = lerp(baseColor, float3(0.0, 0.0, 0.0), .06);
        }
    }

    float3 pos = i.modelViewVertex / 100.0 + .5;
    float3 norm = normalize(mul(world, float4(i.vNormal, 0.0)).xyz);

    float3 flecksLightPos = float3(.5, .5, .5);
    float3 flecksLightDir = normalize(flecksLightPos - pos);
    float3 flecksReflectDir = reflect(-flecksLightDir, norm);
    float flecksSpec = pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), flecksReflectDir), 0.0), 8.0);
    float3 flecksNormal = normalize(u_NormalMap.Sample(textureSampler, (uv * 1.3 + float2(.02, .06) * time) * 2.0).xyz * 2.0 - 1.0);
    norm += flecksSpec * flecksNormal;
    norm = normalize(norm);

    float3 lightPos = float3(-3., -3., 20.);
    float3 lightDir = normalize(lightPos - pos);
    float diffuse = max(dot(norm, lightDir), 0.0);

    float spec = 0.0;

    lightPos = float3(-3., -3., .5);
    spec += 2.0 * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 2.0);

    lightPos = float3(-3., .5, 30.);
    spec += (modelIndex == 1 ? 1.5 : 0.5) * pow(max(dot(normalize(float3(0.0, 0.0, 0.0) - pos), reflect(-normalize(lightPos - pos), norm)), 0.0), 32.0);

    if (modelIndex != 0) {
        spec *= .25;
    }

    float3 color = baseColor;
    color *= .94 + .22 * diffuse;
    color = lerp(color, float3(1.0, 1.0, 1.0), spec);

    return float4(color, 1.0);
}
