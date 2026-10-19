// A sticker rolling onto the wallet card, or peeled off it: the part behind the contact line is
// lifted off the card, bent round a cylinder and then straight, so it comes closer to the eye and
// turns away from the light.
//
// Drawn by StickerCurlEffect, which runs it with the input and the output in the same pixel space,
// so the scene position of a pixel is also its position in the input.
//
// The sheet is mapped forward as a mesh would be - a point of the art goes to the screen - and
// this inverts that mapping per pixel. The bend stays under a right angle, so the sheet never folds
// over itself on screen and every pixel has at most one point of the art behind it.

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    // The sheet's centre, in pixels, and its side.
    float2 center;
    float side;

    // 1 lifts all of it but the leading edge, 0 is flat on the card.
    float curl;

    // The way the sheet moves: its leading edge is the one that touches first.
    float2 direction;

    // The largest angle the sheet turns through, in radians, the radius of the bend, the eye's
    // distance from the card, and how far a lifted point rises on screen per pixel of height.
    float bend;
    float radius;
    float eye;
    float rise;

    float2 padding;
};

float4 main(float4 clipSpaceOutput : SV_POSITION, float4 sceneSpaceOutput : SCENE_POSITION, float4 texelSpaceInput0 : TEXCOORD0) : SV_Target
{
    float2 screen = sceneSpaceOutput.xy - center;
    float halfSide = side * 0.5;

    float contact = -halfSide + side * curl;
    float sinBend = sin(bend);
    float cosBend = cos(bend);

    float2 across = float2(-direction.y, direction.x);

    // The height of the point is not known until the point is, and the other way round: a few
    // rounds of guessing the height, unprojecting with it and reading the height back converge,
    // because the eye is far away compared with how high anything rises.
    float z = 0;
    float behind = 0;
    float along = 0;
    float sideways = 0;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float scale = eye / (eye - z);
        float2 flat = float2(screen.x, screen.y + z * rise) / scale;

        along = dot(flat, direction);
        sideways = dot(flat, across);

        float projected = contact - along;
        if (projected <= 0)
        {
            behind = projected;
            z = 0;
        }
        else if (projected <= radius * sinBend)
        {
            float angle = asin(projected / radius);
            behind = radius * angle;
            z = radius * (1 - cos(angle));
        }
        else
        {
            float rest = (projected - radius * sinBend) / cosBend;
            behind = radius * bend + rest;
            z = radius * (1 - cosBend) + rest * sinBend;
        }
    }

    // Arc length behind the contact line, back to a distance across the art.
    float position = contact - behind;
    float2 art = position * direction + sideways * across;

    if (abs(art.x) > halfSide || abs(art.y) > halfSide)
    {
        return float4(0, 0, 0, 0);
    }

    float shade = behind > 0 ? 0.62 + 0.38 * cos(min(behind / radius, bend)) : 1;

    float2 uv = texelSpaceInput0.xy + (center + art - sceneSpaceOutput.xy) * texelSpaceInput0.zw;
    float4 color = InputTexture.Sample(InputSampler, uv);

    // Premultiplied, so only the colour darkens.
    color.rgb *= shade;
    return color;
}
