#define D2D_REQUIRES_SCENE_POSITION
#define D2D_INPUT_COUNT 4
#define D2D_INPUT0_SIMPLE
#define D2D_INPUT1_COMPLEX
#define D2D_INPUT2_COMPLEX
#define D2D_INPUT3_SIMPLE

#include "d2d1effecthelpers.hlsli"

D2D_PS_ENTRY(main)
{
    float4 position = D2DGetScenePosition();

    float y = D2DGetInput(0).x - 0.0625;
    float u = D2DSampleInputAtPosition(1, floor(position.xy / 2)).x - 0.5;
    float v = D2DSampleInputAtPosition(2, floor(position.xy / 2)).x - 0.5;
    float a = D2DGetInput(3).a;

    // TODO: Explicit reference to `a` is needed because of compiler optimizations.
    return float4(1.164 * y + 1.596 * v,
        1.164 * y - 0.392 * u - 0.813 * v,
        1.164 * y + 2.17 * u,
        a);
}