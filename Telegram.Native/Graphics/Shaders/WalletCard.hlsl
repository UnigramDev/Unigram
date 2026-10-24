// The wallet card, drawn flat by WalletCardEffect into one surface: the face in the top half and
// the edge plate that gives the card its thickness in the bottom half. XAML tilts the result, so
// there is no projection here.
//
// Nothing reads derivatives: the card is drawn flat at a known scale, so they are constants and
// are worked out instead, and Direct2D runs this in branches whose condition differs across a
// pixel quad, where ddx and implicit-LOD sampling are not allowed.

Texture2D StarsTexture : register(t0);
Texture2D NoiseTexture : register(t1);
Texture2D QrTexture : register(t2);

// Filter and extend mode come from the resource textures: the noise wraps, the others clamp.
SamplerState StarsSampler : register(s0);
SamplerState NoiseSampler : register(s1);
SamplerState QrSampler : register(s2);

cbuffer Constants : register(b0)
{
    // In the card's units, and the pixels per unit it is drawn at.
    float2 cardSize;
    float scale;
    float cornerRadius;

    // The card's top left corner in the surface, in pixels, for the face and for the edge.
    float2 faceOrigin;
    float2 edgeOrigin;

    float time;
    float reflectionRotation;

    // Radians, pitch then yaw. The highlight follows the card; the surface tilt also drives the
    // stars' parallax and the edge's streak.
    float2 highlightTilt;
    float2 surfaceTilt;

    float qrAlpha;
    float qrPadding;

    // The QR chip, in units within the card.
    float4 qrRect;

    // 1 draws the lit band round the face's rim that reads as a bevel.
    float bevel;
    float3 padding;

    // Where the second palette has spread to: centre, radius - negative for everywhere at once -
    // and the share it has everywhere then.
    float4 reveal;

    // Gradient from and to, reflection, spark, bevel, edge, streak, ring.
    float4 paletteFrom[8];
    float4 paletteTo[8];
};

static const float PI = 3.14159265;

static const int ToneGradientFrom = 0;
static const int ToneGradientTo = 1;
static const int ToneReflection = 2;
static const int ToneSpark = 3;
static const int ToneBevel = 4;
static const int ToneEdge = 5;
static const int ToneStreak = 6;
static const int ToneRing = 7;

float RoundRect(float2 p, float2 halfSize, float r)
{
    float2 q = abs(p) - (halfSize - r);
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

float2 RoundRectNormal(float2 p, float2 halfSize, float r)
{
    float2 q = abs(p) - (halfSize - r);
    float2 n = (q.x > 0.0 && q.y > 0.0) ? normalize(q) : (q.x > q.y ? float2(1.0, 0.0) : float2(0.0, 1.0));
    return n * float2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
}

float Hash(float value)
{
    return frac(sin(value * 127.1) * 43758.5453);
}

float3 LinearToSrgb(float3 value)
{
    value = max(value, 0.0);
    float3 linearPart = value * 12.92;
    float3 encoded = 1.055 * pow(value, 1.0 / 2.4) - 0.055;
    return value <= 0.0031308 ? linearPart : encoded;
}

float3 ApplySaturation(float3 value)
{
    float luminance = dot(value, float3(0.2126, 0.7152, 0.0722));
    return saturate(lerp(luminance, value, 1.6));
}

float Noise(float2 uv)
{
    return NoiseTexture.SampleLevel(NoiseSampler, uv, 0).r;
}

// How much of the second palette a point of the card, in its units, wears.
float RevealShare(float2 position)
{
    if (reveal.z < 0.0)
    {
        return reveal.w;
    }

    float aa = 0.6 / scale;
    return 1.0 - smoothstep(reveal.z - aa, reveal.z + aa, distance(position, reveal.xy));
}

float3 Tone(int index, float share)
{
    return lerp(paletteFrom[index].rgb, paletteTo[index].rgb, share);
}

// Premultiplied chip and socket, in the chip's own 50 x 38 coordinates. pixel is the size of a
// surface pixel in those coordinates.
float4 QrChip(float2 cp, float2 surface, float pixel, float3 ringTone)
{
    const float2 center = float2(293.5, 101.0);
    const float2 halfSize = float2(25.0, 19.0);
    const float radius = 9.0;
    float2 local = cp - center;
    float d = RoundRect(local, halfSize, radius);
    float aa = max(0.75 * pixel, 0.001);
    float socket = (1.0 - smoothstep(0.0, 1.2, d)) * step(0.0, d) * 0.10;
    if (d >= 2.0)
    {
        return float4(0.0, 0.0, 0.0, socket);
    }

    float2 q = local / halfSize;
    float horizon = 0.10 + 0.55 * surface.y - 0.18 * surface.x;
    float sky = 1.0 - smoothstep(horizon - 0.55, horizon + 0.85, q.y + 0.18 * q.x);
    float3 metal = lerp(float3(0.66, 0.78, 0.93), float3(0.955, 0.975, 1.0), sky);
    float brush = Noise(cp * float2(0.012, 1.4));
    float brushFine = Noise(cp * float2(0.03, 3.1));
    float grain = (brush - 0.5) * 0.020 + (brushFine - 0.5) * 0.014;
    metal *= 1.0 + grain;

    float slide = 0.10 + 0.95 * surface.x - 0.55 * surface.y + 0.12 * sin(time * 0.45);
    float across = dot(q, normalize(float2(1.0, -0.45)));
    float soft = exp(-pow((across - slide) / 0.55, 2.0));
    float sharp = exp(-pow((across - slide) / 0.10, 2.0));
    metal = lerp(metal, 1.0, soft * 0.22 + sharp * (0.30 + grain * 6.0));

    float inner = RoundRect(local, halfSize - 1.0, radius - 1.0);
    float2 n = RoundRectNormal(local, halfSize - 1.0, radius - 1.0);
    float2 light = normalize(float2(-0.55 - 0.7 * surface.x, -0.85 + 0.6 * surface.y));
    float fresnel = smoothstep(-5.0, 0.0, inner);
    metal = lerp(metal, float3(0.98, 0.99, 1.0), fresnel * 0.18);
    float chamfer = smoothstep(-1.3, -0.1, inner);
    metal -= chamfer * dot(n, light) * 0.12;

    float2 qrUV = (cp - float2(282.5, 90.0)) / 22.0;
    float ink = QrTexture.SampleLevel(QrSampler, qrUV, 0).a;
    float lip = QrTexture.SampleLevel(QrSampler, qrUV - float2(0.0, 1.0 / 22.0), 0).a;
    float upper = QrTexture.SampleLevel(QrSampler, qrUV + float2(0.0, 0.55 / 22.0), 0).a;
    metal = lerp(metal, float3(0.90, 0.93, 0.96), lip * (1.0 - ink));
    float3 inkColor = float3(0.459, 0.486, 0.514);
    inkColor = lerp(inkColor * 0.80, inkColor, upper);
    metal = lerp(metal, inkColor, ink);

    float ringMask = smoothstep(-1.0 - aa, -1.0 + aa, d);
    float3 ring = ringTone * (0.92 + 0.16 * dot(n, light));
    float3 chip = lerp(saturate(metal), ring, ringMask);
    float cover = 1.0 - smoothstep(-aa, aa, d);
    return float4(chip * cover, cover + socket * (1.0 - cover));
}

// The slab is a stack of slices from back (0) to front (1); a plate is one of them.
float4 Edge(float2 uv, float2 surface)
{
    const float t = 0.5;

    float2 cardHalf = cardSize * 0.5;
    float cardRadius = min(cornerRadius, min(cardHalf.x, cardHalf.y));
    float2 p = uv * cardSize - cardHalf;
    float d = RoundRect(p, cardHalf, cardRadius);
    float aa = 0.6 / scale;
    float cover = 1.0 - smoothstep(-aa, aa, d);
    float share = RevealShare(uv * cardSize);
    float2 n = RoundRectNormal(p, cardHalf, cardRadius);
    float diffuse = 0.5 + 0.5 * dot(n, normalize(float2(-0.35, -1.0)));
    float lines = 1.0 + (Hash(floor(t * 9.0) + 3.1) - 0.5) * 0.05;
    float3 edge = Tone(ToneEdge, share) * (0.62 + 0.62 * diffuse) * lines;
    float2 along = float2(-n.y, n.x);
    float run = dot(p, along) / max(cardHalf.x, 1.0);
    float streak = exp(-pow((run - (surface.x * 0.9 - surface.y * 0.7)) / 0.38, 2.0));
    edge += Tone(ToneStreak, share) * streak * 0.18 * (0.35 + 0.65 * diffuse);
    float3 edgeColor = saturate(ApplySaturation(LinearToSrgb(edge)) * float3(1.05933, 0.912212, 0.968627));
    return float4(edgeColor * cover, cover);
}

float4 Face(float2 uv, float2 surface)
{
    float safeWidth = max(cardSize.x, 1.0);
    float safeHeight = max(cardSize.y, 1.0);
    float share = RevealShare(uv * float2(safeWidth, safeHeight));

    float2 cardPosition = float2((uv.x - 0.5) * 2.0, (0.5 - uv.y) * 2.0 * safeHeight / safeWidth);
    float radius = length(cardPosition);
    float centerFade = smoothstep(0.018, 0.090, radius);
    float2 radial = radius > 0.0001 ? cardPosition / radius : float2(1.0, 0.0);
    float2 tangent = float2(-radial.y, radial.x);

    const float radialFrequencyScale = 0.65;
    const float finishDetail = 0.05;
    float ringFrequency = 180.0 * radialFrequencyScale;
    float waveFrequency = 560.0 * radialFrequencyScale;
    float ringIndex = floor(radius * ringFrequency);

    // cardPosition grows by 2 / width per pixel on both axes, so this is the length of radius's
    // gradient everywhere but the centre.
    float radialFootprint = 2.0 / (safeWidth * scale) * radialFrequencyScale;
    float ringFilter = 1.0 - smoothstep(0.35, 1.10, radialFootprint * 180.0);
    float waveFilter = 1.0 - smoothstep(1.15, 3.10, radialFootprint * 560.0);
    float fineFinish = lerp(0.5, Hash(ringIndex), ringFilter)
        + 0.35 * sin(radius * waveFrequency + Hash(ringIndex * 0.37) * 6.2831853072) * waveFilter;
    const float finishVisibility = 0.5;
    float radialFinish = (fineFinish - 0.5) * finishDetail * finishVisibility * centerFade;

    float colorBlend = saturate(0.30 + uv.y * 0.50 + uv.x * 0.18);
    colorBlend = colorBlend * colorBlend * (3.0 - 2.0 * colorBlend);
    float3 gradient = lerp(Tone(ToneGradientFrom, share), Tone(ToneGradientTo, share), colorBlend);

    float brushedGrain = Noise(uv * float2(1.4, 8.0));
    float mediumGrain = Noise(uv * float2(4.0, 3.0));
    float fineGrain = Noise(uv * float2(19.0, 13.0));
    float materialVariation = radialFinish * 0.48
        + (brushedGrain - 0.5) * 0.035
        + (mediumGrain - 0.5) * 0.035
        + (fineGrain - 0.5) * 0.018;
    gradient *= 1.0 + materialVariation;

    float2 normalizedTilt = clamp(float2(highlightTilt.y / 0.24, highlightTilt.x / 0.17), -1.0, 1.0);
    float2 idleDirection = normalize(float2(1.0, -safeHeight / safeWidth));
    float2 idlePerpendicular = float2(-idleDirection.y, idleDirection.x);
    float2 tiltDirection = -normalizedTilt;
    float rotationTurn = clamp(dot(tiltDirection, idlePerpendicular) / max(abs(idlePerpendicular.y), 0.0001), -1.0, 1.0);
    float reflectionAngle = rotationTurn * 1.5707963268 - reflectionRotation;
    float sineAngle = sin(reflectionAngle);
    float cosineAngle = cos(reflectionAngle);
    float2 keyDirection = float2(
        idleDirection.x * cosineAngle - idleDirection.y * sineAngle,
        idleDirection.x * sineAngle + idleDirection.y * cosineAngle);
    float tangentAlignment = dot(tangent, keyDirection);
    float roughnessNoise = radialFinish * 0.18 + (brushedGrain - 0.5) * 0.025 + (mediumGrain - 0.5) * 0.025;
    float roughness = clamp(0.20 + roughnessNoise, 0.13, 0.30);
    float wedgeWidth = lerp(0.30, 0.44, (roughness - 0.13) / 0.17);
    float centerProgress = saturate(radius / 0.275);
    float centerCurve = centerProgress * centerProgress * centerProgress
        * (centerProgress * (centerProgress * 6.0 - 15.0) + 10.0);
    float centerFadeReflection = lerp(0.10, 1.0, centerCurve);
    float coreWedge = exp(-(tangentAlignment * tangentAlignment) / max(2.0 * wedgeWidth * wedgeWidth, 0.0001));
    float haloWidth = wedgeWidth * 1.70;
    float haloWedge = exp(-(tangentAlignment * tangentAlignment) / max(2.0 * haloWidth * haloWidth, 0.0001));
    float radialWedge = lerp(coreWedge, haloWedge, 0.42) * centerFadeReflection;
    float signedLobe = dot(radial, keyDirection);
    float lobeBalance = lerp(0.72, 1.0, 0.5 + 0.5 * signedLobe);
    float fibreReflection = smoothstep(0.47, 0.86, brushedGrain) * 0.58 + smoothstep(0.56, 0.91, mediumGrain) * 0.42;
    float ringReflection = clamp(0.78 + radialFinish * 2.4 + fibreReflection * 0.28, 0.42, 1.30);
    float studioReflection = radialWedge * lobeBalance * ringReflection;
    gradient += Tone(ToneReflection, share) * studioReflection * 0.32;

    float2 normalizedSurfaceTilt = clamp(float2(surfaceTilt.y / 0.24, surfaceTilt.x / 0.17), -1.0, 1.0);
    const float starDepthPoints = 4.0;
    float2 starParallax = float2(normalizedSurfaceTilt.x, -normalizedSurfaceTilt.y) * starDepthPoints / float2(safeWidth, safeHeight);
    float4 star = StarsTexture.SampleLevel(StarsSampler, uv + starParallax, 0);
    float starPhase = star.g / max(star.a, 0.001);
    float twinkle = 0.6 + 0.4 * sin(time * 1.7 + starPhase * 6.28318);
    float highlightCoverage = smoothstep(0.16, 0.58, radialWedge * lobeBalance);
    float starAlpha = 0.5 * star.a * (0.72 + 0.28 * colorBlend) * twinkle * highlightCoverage;
    float3 lifted = gradient <= 0.25 ? ((16.0 * gradient - 12.0) * gradient + 4.0) * gradient : sqrt(gradient);
    gradient = lerp(gradient, lifted, starAlpha);
    float3 spark = Tone(ToneSpark, share) * starAlpha * starAlpha * (0.4 + 0.4 * colorBlend);

    gradient += (fineGrain - 0.5) * 0.014;
    gradient *= 1.0 - 0.14 * smoothstep(0.965, 1.0, uv.y);

    float2 halfSize = float2(safeWidth, safeHeight) * 0.5;
    float radiusPoints = min(cornerRadius, min(halfSize.x, halfSize.y));
    float2 p = uv * float2(safeWidth, safeHeight) - halfSize;
    float roundedDistance = RoundRect(p, halfSize, radiusPoints);

    if (bevel > 0.0)
    {
        float2 n = RoundRectNormal(p, halfSize, radiusPoints);
        float band = smoothstep(-1.1, -0.35, roundedDistance);
        float2 light = normalize(float2(-0.45 - 0.9 * surface.x, -0.9 + 0.7 * surface.y));
        gradient += Tone(ToneBevel, share) * band * (0.03 + 0.26 * max(dot(n, light), 0.0));
    }

    float edgeWidth = 0.6 / scale;
    float coverage = 1.0 - smoothstep(-edgeWidth, edgeWidth, roundedDistance);

    // Written into a UNORM surface, so the encoding is done here.
    float3 outputColor = ApplySaturation(LinearToSrgb(gradient + spark));

    if (qrAlpha > 0.0 && all(qrRect.zw > 0.0))
    {
        float2 position = uv * float2(safeWidth, safeHeight);
        float2 chipScale = qrRect.zw / float2(50.0, 38.0);
        float2 margin = chipScale * 2.0;
        if (all(position >= qrRect.xy - margin) && all(position <= qrRect.xy + qrRect.zw + margin))
        {
            float2 cp = (position - qrRect.xy) / chipScale + float2(268.5, 82.0);
            float pixel = 1.0 / (scale * chipScale.x);
            float3 ringTone = Tone(ToneRing, share);
            float4 chip = QrChip(cp, surface, pixel, ringTone) * qrAlpha;
            outputColor = outputColor * (1.0 - chip.a) + chip.rgb;
        }
    }

    return float4(outputColor * coverage, coverage);
}

float4 main(float4 clipSpaceOutput : SV_POSITION, float4 sceneSpaceOutput : SCENE_POSITION) : SV_Target
{
    float2 surface = clamp(float2(surfaceTilt.y / 0.30, surfaceTilt.x / 0.20), -1.0, 1.0);
    float2 cardPixels = cardSize * scale;

    // The halves have equal margins, so they meet halfway between the face's bottom and the edge's top.
    if (sceneSpaceOutput.y >= (faceOrigin.y + cardPixels.y + edgeOrigin.y) * 0.5)
    {
        return Edge((sceneSpaceOutput.xy - edgeOrigin) / cardPixels, surface);
    }

    return Face((sceneSpaceOutput.xy - faceOrigin) / cardPixels, surface);
}
