// The final blit: the diamond is drawn into an offscreen target and copied out with an opacity
// and a desaturate-to-alpha knob, which is how the app fades it in and out without touching the
// render itself.
//
// Translated from assets/shaders/diamond/fullscreenVertex.glsl and copyFragment.glsl.

Texture2D<float4> image : register(t0);
SamplerState imageSampler : register(s0);

cbuffer Composite : register(b0)
{
    float opacity;
    float white;
    float2 compositePadding;
};

struct FullscreenRaster
{
    float4 position : SV_Position;
    float2 uv       : TEXCOORD0;
};

FullscreenRaster VS(uint vertexId : SV_VertexID)
{
    FullscreenRaster result;
    result.uv = float2((vertexId << 1) & 2, vertexId & 2);

    // The GL original writes uv * 2 - 1 straight into clip space, which puts v=0 at the bottom.
    // D3D's texture origin is the top, so the y flip lands here rather than on the sampler.
    result.position = float4(result.uv.x * 2.0 - 1.0, 1.0 - result.uv.y * 2.0, 0.0, 1.0);
    return result;
}

float4 PS(FullscreenRaster inputData) : SV_Target
{
    float4 c = image.Sample(imageSampler, inputData.uv);
    return float4(lerp(c.rgb, c.aaa, white), c.a) * opacity;
}
