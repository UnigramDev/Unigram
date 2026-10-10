#pragma once

#include "IconMotion.h"
#include "Scene3D.h"

#include <winrt/base.h>

#include <cstdint>
#include <memory>

namespace Graphics3D
{
    struct IconResources;

    // Mirrors the Uniforms block in Icon.hlsl, field for field and in order: 224 bytes.
    struct IconUniforms
    {
        Matrix4 mvp;
        Matrix4 world;
        float gradientColor1[3] = { 0, 0, 0 };
        float spec1 = 2.0f;
        float gradientColor2[3] = { 0, 0, 0 };
        float spec2 = 0.13f;
        float normalSpecColor[3] = { 1, 1, 1 };
        float diffuse = 1.0f;
        float backgroundColor[3] = { 0, 0, 0 };
        float normalSpec = 0.2f;
        float xOffset = 0;
        float alpha = 0;
        float golden = 0;
        float white = 0;
        float time = 0;
        std::int32_t type = 0;
        std::int32_t night = 0;
        float padding = 0;
    };

    static_assert(sizeof(IconUniforms) == 224, "IconUniforms must match the cbuffer in Icon.hlsl");

    /// <summary>
    /// The star and the coin from the Android app's Premium, Stars and Business screens - its
    /// Icon3D, ported mesh for mesh and shader for shader.
    /// </summary>
    /// <remarks>
    /// Source of truth is the open-source Android client, `org.telegram.ui.Components.Premium.GLIcon`
    /// with its assets; the beta's copies are byte-identical. `notes/icon-3d-component.md` says where
    /// each number came from.
    ///
    /// The star wears two looks through the same shader, which blends between them on `golden`: the
    /// purple Premium star and the golden Stars one. The coin is four meshes - rim, face, logo and
    /// the ring of stars - told apart in the shader by their index.
    ///
    /// One pass, multisampled and resolved into the back buffer, because the phone asks its EGL
    /// surface for sample buffers and an aliased silhouette at desktop sizes is plain to see.
    /// </remarks>
    class IconScene final : public IScene3D
    {
    public:
        // Scene3DPalette.Golden, which is the variant the panel hands over.
        static constexpr int GoldenVariant = 3;

        explicit IconScene(bool coin);

        SceneMotion Motion() const override;

        bool Load(ID3D11Device* device, SceneCache& cache, std::wstring& error) override;
        void Resize(ID3D11Device* device, std::uint32_t width, std::uint32_t height) override;
        void Render(const SceneTarget& target, const ScenePose& pose) override;
        void SetVariant(int variant) override;
        void SetDark(bool dark) override;

        bool Choreographs() const override { return true; }
        void Choreograph(const SceneGesture& gesture, float step) override;

    private:
        void UpdateColors();

        const bool m_coin;

        std::shared_ptr<const IconResources> m_resources;

        IconMotion m_motion;
        bool m_started = false;

        winrt::com_ptr<ID3D11Buffer> m_uniformBuffer;

        // The multisampled target, resolved into the back buffer. Null when the adapter offers
        // no samples, and then the scene draws straight into the back buffer.
        winrt::com_ptr<ID3D11Texture2D> m_color;
        winrt::com_ptr<ID3D11RenderTargetView> m_colorView;
        winrt::com_ptr<ID3D11DepthStencilView> m_depthView;
        UINT m_samples = 1;

        IconUniforms m_uniforms;

        bool m_golden = false;
        bool m_dark = false;

        std::uint32_t m_width = 0;
        std::uint32_t m_height = 0;
    };
}
