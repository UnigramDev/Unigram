#pragma once

#include "Scene3D.h"

#include <winrt/base.h>

#include <memory>

namespace Graphics3D
{
    struct DiamondResources;

    // Mirrors the Uniforms struct the shaders declare, field for field and in order. The comments
    // are theirs. Nothing may be inserted, reordered or padded differently.
    struct DiamondUniforms
    {
        Matrix4 model;
        Matrix4 projection;
        Matrix4 inverseModel;
        float parameters[4] = { 0, 0.72f, 1, 1 };   // time, refraction, brightness, sparkles
        float viewport[4] = { 0, 0, 17, 0 };        // width, height, optical plane count, flat mode
        // Everything from here to leftPavilionSweep is overwritten every frame from frames.bin,
        // in exactly this order - it is the order of the String[] the uniforms are uploaded from.
        float sparkleShape[4] = { 1, 0, 1, 1 };     // main layer scale, contour morph, core, star glow
        // y is a rotation applied to anchor 0 rather than an angle to ray-cast from, and z is the
        // horizontal correction the widths table feeds.
        float sparkleHalo[4] = { 1, 0, 1, 1 };      // glow scale, face rotation, horizontal correction, visibility
        float crownGradient[4] = { 60, 110, 440, 250 };
        float pavilionGradient[4] = { 60, 230, 440, 380 };
        float lightSweep[4] = { 0, 0, 0.5f, 0 };    // diagonal position, environment phase, transmission
        float facetProjection[4] = { 1, 0, 224, 0.637f };   // filled from facetProjection.bin
        float crownSweep[4] = { -400, 120, -200, 260 };
        float rightCrownSweep[4] = { -400, 120, -200, 260 };
        float leftCrownSweep[4] = { -400, 120, -200, 260 };
        float pavilionSweep[4] = { -400, 240, -200, 380 };
        float rightPavilionSweep[4] = { -400, 240, -200, 380 };
        float leftPavilionSweep[4] = { -400, 240, -200, 380 };
        float appearance[4] = { 0, 0, 0, 0 };       // palette identifier, whiten, reserved
        float referenceCrownFlash[4] = { 0, 0, 0, 0 };
        float referencePavilionFlash[4] = { 0, 0, 0, 0 };
    };

    // The copy pass fades and can whiten what it brings out. The fade is the entry ramp - see
    // Render - and the whiten goes unused, the panel being an ordinary UIElement whose own
    // Opacity covers what that was for.
    struct CompositeUniforms
    {
        float opacity = 1.0f;
        float white = 0.0f;
        float padding[2] = { 0, 0 };
    };

    /// <summary>
    /// The TON diamond, ported from the Android beta's own shaders and assets.
    /// </summary>
    /// <remarks>
    /// Three passes. The body goes into an offscreen target, depth-tested and opaque; the sparkle
    /// layers go over it with no depth, premultiplied; and a copy pass brings the result out, which
    /// is where the fade applies - fading the passes individually would show the sparkles through
    /// the stone.
    ///
    /// The numbers in here are read out of the beta, not chosen, and the ones that look arbitrary
    /// are the ones that matter most. `C:\Source\DiamondSpike\README.md` carries the derivation of
    /// every one of them, along with the dex reader that produced them; do not adjust anything
    /// here without reading it.
    /// </remarks>
    class DiamondScene final : public IScene3D
    {
    public:
        SceneMotion Motion() const override;

        bool Load(ID3D11Device* device, SceneCache& cache, std::wstring& error) override;
        void Resize(ID3D11Device* device, std::uint32_t width, std::uint32_t height) override;
        void Render(const SceneTarget& target, const ScenePose& pose) override;
        void SetVariant(int variant) override;

    private:
        // Plays frames.bin into the uniform block. Everything the shaders animate except the
        // model matrix and the sparkle's visibility comes from there, so this is most of the look.
        void Animate(float seconds);

        // Splined half-width for a yaw in radians, sampled the way the beta samples it: fold into
        // a quarter turn, scale to 0..90, then a cubic through four neighbours.
        float WidthAt(float yawRadians) const;

        // The extra horizontal scale on k[0]: normalises the silhouette back to its flat-on width,
        // faded out as the stone is pitched.
        float HorizontalSqueeze(float yawRadians, float pitchRadians) const;

        // The beta's own projection, element for element. `squeeze` belongs to k[0] alone - giving
        // it to k[5] as well makes the stone breathe towards and away from the camera.
        Matrix4 Projection(float aspect, float squeeze) const;

        void DrawSparkles(ID3D11DeviceContext* context, UINT vertexCount, UINT instanceCount,
            UINT baseInstance, ID3D11Buffer* buffer);

        // Everything that is the same for every diamond on the device. Null until loaded.
        std::shared_ptr<const DiamondResources> m_resources;

        winrt::com_ptr<ID3D11Buffer> m_uniformBuffer;
        winrt::com_ptr<ID3D11Buffer> m_instancingBuffer;
        winrt::com_ptr<ID3D11Buffer> m_compositeBuffer;

        winrt::com_ptr<ID3D11Texture2D> m_offscreen;
        winrt::com_ptr<ID3D11RenderTargetView> m_offscreenView;
        winrt::com_ptr<ID3D11DepthStencilView> m_depthView;

        // What the fade pass samples, and what the offscreen is resolved into. The same texture
        // as the offscreen when there are no samples to resolve.
        winrt::com_ptr<ID3D11Texture2D> m_resolved;
        winrt::com_ptr<ID3D11ShaderResourceView> m_offscreenSource;

        UINT m_samples = 1;

        DiamondUniforms m_uniforms;
        CompositeUniforms m_composite;

        // Which of the three palettes the fragment shader takes. See SetVariant.
        int m_palette = 0;

        std::uint32_t m_width = 0;
        std::uint32_t m_height = 0;
    };
}
