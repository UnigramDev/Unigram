// Front face of the wallet card: Figma angular gradient (matrix-accurate)
// + soft-light stars masked by the same gradient + brushed-metal grain.
//
// Port of CardShaders.swift (Metal / SceneKit shader modifier) to a Direct2D
// custom effect, driven from C# through Win2D's PixelShaderEffect.
//
// Two things had to change on the way across, both consequences of there being
// no PBR stage here to hand values to:
//
//  * The iOS stops are LINEAR (SceneKit lights in linear and outputs sRGB).
//    D2D composites in the target's own space, so the sRGB stops go in
//    directly - #0079FF / #169AF9. They are the same two colors: converting
//    0x79/255 through the sRGB curve gives 0.1893, the sample's literal.
//  * `_surface.emission` / `_surface.metalness` have no equivalent, so the
//    environment reflection is a pair of explicit specular lobes below,
//    placed to match the studio image the sample builds in
//    CardTextures.environmentImage() (key softbox + cool fill).
//
// Everything else is the same arithmetic in the same order.

#define D2D_INPUT_COUNT 2
#define D2D_INPUT0_COMPLEX  // overlay: static content (texts, icons, QR), premultiplied
#define D2D_INPUT1_COMPLEX  // stars: A = coverage, G = per-star twinkle phase

#include "d2d1effecthelpers.hlsli"

// Both inputs are card-sized and drawn 1:1, so they are COMPLEX only to get
// D2DGetInputCoordinate - the UV that the geometry shader modifier supplied on
// iOS. Sampler offset stays 0; see WalletCardView.CreateShader.

float2 cardSize;        // 370 x 220 design points
float  gradientPhase;   // accumulated on the CPU, in gradient turns
float  time;            // seconds, drives the star twinkle
float2 lightDir;        // tilt, -1..1 per axis; slides the specular lobes
float  gloss;           // 0..1, how much environment sheen to add
float  cornerRadius;    // design points
float  aaScale;         // device pixels per design point, for the corner AA

// --- Brushed-metal grain -------------------------------------------------
//
// The sample bakes this into a 256x256 texture (a hash, box-blurred along x to
// make streaks, contrast restored, fine grain mixed back in). Reproduced here
// procedurally instead of as a third input: a wrapped texture read needs
// SamplerCoordinateMapping.Unknown, which makes D2D request an unbounded source
// rect on every tile. Same construction, same character, no sampling hazard.

float hash(float2 v)
{
    return frac(sin(dot(v, float2(127.1, 311.7))) * 43758.5453);
}

// Interpolated, not point-sampled: the iOS texture is read through a linear
// sampler, and floor()ing the hash instead makes the streaks read as hard
// scanlines at display scale.
float valueNoise(float2 t)
{
    float2 i = floor(t);
    float2 f = t - i;
    f = f * f * (3.0 - 2.0 * f);

    float a = hash(i);
    float b = hash(i + float2(1, 0));
    float c = hash(i + float2(0, 1));
    float d = hash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float brushed(float2 t)
{
    float acc = 0;
    [unroll]
    for (int k = -4; k <= 4; k++)
    {
        acc += valueNoise(t + float2(k * 2.0, 0.0));
    }

    float v = acc / 9.0;
    v = 0.5 + (v - 0.5) * 3.4;                      // restore contrast
    return saturate(0.68 * v + 0.32 * valueNoise(t + 17.0));
}

D2D_PS_ENTRY(main)
{
    float2 uv = D2DGetInputCoordinate(0).xy;
    float4 overlay = D2DSampleInput(0, uv);         // premultiplied
    float4 star = D2DSampleInput(1, uv);

    // --- Angular gradient. Figma: conic from 90deg, stops #0079FF/#169AF9,
    // --- transform matrix(7.6533 -11.859 19.944 12.225 185 110), det = 330.078.
    float2 p = (uv - 0.5) * cardSize;
    float2 q = float2(12.225 * p.x - 19.944 * p.y, 11.859 * p.x + 7.6533 * p.y) * (1.0 / 330.078);
    float turn = atan2(q.x, -q.y) * 0.1591549431;   // clockwise from 12 o'clock, in turns
    float s = turn - 0.25 + gradientPhase;          // permanent spin; tilt accelerates it
    float tri = 1.0 - abs(2.0 * frac(2.0 * s) - 1.0);
    tri = tri * tri * (3.0 - 2.0 * tri);
    // Soften the conic convergence point: melt into the mid color near center.
    tri = lerp(0.5, tri, smoothstep(4.0, 60.0, length(p)));
    float3 grad = lerp(float3(0.0, 0.47451, 1.0), float3(0.08627, 0.60392, 0.97647), tri);

    // Soft sheen where the conic rays converge (visible in the Figma render).
    float centerDist = length(p / (cardSize * 0.5));
    grad += float3(0.012, 0.028, 0.05) * exp(-centerDist * centerDist * 2.5);

    float grain = brushed(uv * cardSize * float2(2.08, 2.10));
    grad *= 0.985 + 0.03 * grain;

    // --- Stars: white @ 50% soft-light, masked by the same angular gradient
    // --- (tri), with a per-star sine twinkle on top.
    float phase = star.g / max(star.a, 0.001);
    float twinkle = 0.6 + 0.4 * sin(time * 1.7 + phase * 6.28318);
    float starAlpha = 0.5 * star.a * tri * twinkle;
    float3 lifted = lerp(sqrt(grad), ((16.0 * grad - 12.0) * grad + 4.0) * grad, step(grad, 0.25));
    grad = lerp(grad, lifted, starAlpha);
    float3 spark = float3(0.55, 0.8, 1.0) * starAlpha * starAlpha * (0.4 + 0.4 * tri);

    // Dither to avoid banding on the large smooth gradient.
    grad += (hash(floor(uv * cardSize * 9.5)) - 0.5) * 0.012;

    // --- Compose static content (texts, icons, QR button) on top.
    float3 composed = grad * (1.0 - overlay.a) + overlay.rgb;

    // --- Environment stand-in. On a flat card the reflected direction shifts
    // --- linearly with tilt, so the studio lobes just slide with lightDir.
    float2 r = uv - 0.5 + lightDir * 0.55;
    float2 k = (r - float2(-0.16, -0.24)) * float2(0.62, 1.90);
    float2 f = (r - float2(0.30, 0.12)) * float2(0.85, 2.20);
    float3 sheen = exp(-dot(k, k) * 3.2) * float3(0.85, 0.92, 1.00) * 0.17
                 + exp(-dot(f, f) * 4.0) * float3(0.62, 0.78, 1.00) * 0.09;
    // Brushing breaks the highlight up, but only a little: at full modulation the
    // grain stops reading as metal and starts reading as banding.
    sheen *= (0.78 + 0.44 * grain) * gloss * (1.0 - 0.55 * overlay.a);

    // --- Rounded-rect coverage. SceneKit got the shape from the extruded
    // --- SCNShape; here it is a signed distance, which also antialiases the
    // --- corners without a D2D layer in the way (and the effect output has to
    // --- be premultiplied, hence the multiply).
    float2 e = abs(p) - (cardSize * 0.5 - cornerRadius);
    float dist = length(max(e, 0.0)) + min(max(e.x, e.y), 0.0) - cornerRadius;
    float coverage = saturate(0.5 - dist * aaScale);

    return float4(saturate(composed + spark + sheen) * coverage, coverage);
}
