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

    float y = D2DGetInput(0).x;
    float u = D2DSampleInputAtPosition(1, position.xy / 2).x - 0.5;
    float v = D2DSampleInputAtPosition(2, position.xy / 2).x - 0.5;
    float a = D2DGetInput(3).a;

    // TODO: Explicit reference to `a` is needed because of optimizations.
    return float4(y + 1.403 * v, y - 0.344 * u - 0.714 * v, y + 1.77 * u, a);
}