#include "pch.h"
#include "DiamondScene.h"
#include "Scene3DDevice.h"

#include <algorithm>
#include <cmath>
#include <cstring>

// Compiled by FxCompile into the intermediate directory, one entry point a file. Bytecode rather
// than source: D3DCompile is not something a packaged app may carry, and a shader that fails to
// compile should fail the build rather than the screen.
#include "DiamondVertex.h"
#include "DiamondPixel.h"
#include "SparkleVertex.h"
#include "SparklePixel.h"
#include "CompositeVertex.h"
#include "CompositePixel.h"

namespace Graphics3D
{
    namespace
    {
        constexpr float Pi = 3.14159265358979f;
        constexpr float ToRadians = Pi / 180.0f;

        // Where the assets are packaged, under Assets\Models. The names are the beta's own.
        constexpr wchar_t AssetFolder[] = L"Diamond";

        // The baked animation: 1441 frames of 42 floats at 240 frames a second, so the loop is
        // six seconds. The table carries the extra frame so the last one has a successor to
        // interpolate towards rather than wrapping.
        constexpr int FrameFloats = 42;
        constexpr float FramesPerSecond = 240.0f;

        // How long it takes to appear. The beta's own, which its draw advances as `z += dt / 0.22`
        // and multiplies into what the copy pass brings out - so the stone arrives rather than
        // being switched on. It doubles as the pause before the idle spin: the two were separate
        // numbers that turned out to be the same one.
        constexpr float EntrySeconds = 0.22f;

        bool ReadAsset(const wchar_t* name, std::vector<std::uint8_t>& data, std::wstring& error)
        {
            return ReadModelAsset(AssetFolder, name, data, error);
        }

        bool CreateVertexBuffer(ID3D11Device* device, const std::vector<std::uint8_t>& bytes,
            winrt::com_ptr<ID3D11Buffer>& buffer)
        {
            D3D11_BUFFER_DESC desc = {};
            desc.ByteWidth = static_cast<UINT>(bytes.size());
            desc.Usage = D3D11_USAGE_IMMUTABLE;
            desc.BindFlags = D3D11_BIND_VERTEX_BUFFER;

            D3D11_SUBRESOURCE_DATA initial = {};
            initial.pSysMem = bytes.data();

            return SUCCEEDED(device->CreateBuffer(&desc, &initial, buffer.put()));
        }

        bool CreateConstantBuffer(ID3D11Device* device, const std::vector<std::uint8_t>& bytes,
            winrt::com_ptr<ID3D11Buffer>& buffer)
        {
            D3D11_BUFFER_DESC desc = {};
            desc.ByteWidth = static_cast<UINT>(bytes.size());
            desc.Usage = D3D11_USAGE_IMMUTABLE;
            desc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;

            D3D11_SUBRESOURCE_DATA initial = {};
            initial.pSysMem = bytes.data();

            return SUCCEEDED(device->CreateBuffer(&desc, &initial, buffer.put()));
        }

        bool CreateDynamicBuffer(ID3D11Device* device, UINT size, winrt::com_ptr<ID3D11Buffer>& buffer)
        {
            D3D11_BUFFER_DESC desc = {};
            desc.ByteWidth = (size + 15) & ~15u;
            desc.Usage = D3D11_USAGE_DYNAMIC;
            desc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
            desc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;

            return SUCCEEDED(device->CreateBuffer(&desc, nullptr, buffer.put()));
        }

        void Upload(ID3D11DeviceContext* context, ID3D11Buffer* buffer, const void* data, size_t size)
        {
            D3D11_MAPPED_SUBRESOURCE mapped = {};
            if (SUCCEEDED(context->Map(buffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped)))
            {
                std::memcpy(mapped.pData, data, size);
                context->Unmap(buffer, 0);
            }
        }

        constexpr DXGI_FORMAT OffscreenFormat = DXGI_FORMAT_R8G8B8A8_UNORM;

        /// <summary>
        /// The most samples this adapter will give the offscreen, up to four.
        /// </summary>
        /// <remarks>
        /// The beta antialiases nothing - its EGL config asks for colour and depth and no samples
        /// at all - and gets away with it because a phone draws this three or four times larger on
        /// a screen dense enough to hide the rest. At 1x on a monitor the silhouette has nothing
        /// hiding it, and the shader smooths only the facet seams inside the stone, never its
        /// outline. So this is one place the port deliberately improves on the original.
        ///
        /// Multisampling rather than drawing it oversized: the samples of a pixel share one pixel
        /// shader invocation, and that shader is by far the expensive half of this scene.
        ///
        /// Four is where it stops because more is not visible at this size. Feature level 11,
        /// the only one the device accepts, guarantees four for this format, so the walk
        /// downwards is a guard rather than a path anything is expected to take.
        /// </remarks>
        UINT HighestSampleCount(ID3D11Device* device, DXGI_FORMAT format)
        {
            for (UINT count = 4; count > 1; count /= 2)
            {
                UINT quality = 0;
                if (SUCCEEDED(device->CheckMultisampleQualityLevels(format, count, &quality))
                    && quality > 0)
                {
                    return count;
                }
            }

            return 1;
        }

        float Smoothstep(float t)
        {
            t = std::clamp(t, 0.0f, 1.0f);
            return t * t * (3.0f - 2.0f * t);
        }
    }

    /// <summary>
    /// Everything about the diamond that is the same in every panel showing one: shaders,
    /// geometry, the baked animation and pipeline state.
    /// </summary>
    /// <remarks>
    /// Shared through the device's <see cref="SceneCache"/>, so a chat with a dozen transfers
    /// holds one copy of the buffers and has the pixel shader compiled once. Never written once
    /// loaded, which is what makes sharing it safe; anything a frame writes is DiamondScene's.
    /// </remarks>
    struct DiamondResources
    {
        bool Load(ID3D11Device* device, std::wstring& error)
        {
            return LoadShaders(device, error)
                && LoadAssets(device, error)
                && CreateStates(device, error);
        }

        winrt::com_ptr<ID3D11VertexShader> diamondVertexShader;
        winrt::com_ptr<ID3D11PixelShader> diamondPixelShader;
        winrt::com_ptr<ID3D11InputLayout> diamondLayout;

        winrt::com_ptr<ID3D11VertexShader> sparkleVertexShader;
        winrt::com_ptr<ID3D11PixelShader> sparklePixelShader;
        winrt::com_ptr<ID3D11InputLayout> sparkleLayout;

        winrt::com_ptr<ID3D11VertexShader> compositeVertexShader;
        winrt::com_ptr<ID3D11PixelShader> compositePixelShader;

        winrt::com_ptr<ID3D11Buffer> diamondVertices;
        winrt::com_ptr<ID3D11Buffer> sparkleMain;
        winrt::com_ptr<ID3D11Buffer> sparkleSmall;

        winrt::com_ptr<ID3D11Buffer> planeBuffer;
        winrt::com_ptr<ID3D11Buffer> anchorBuffer;

        winrt::com_ptr<ID3D11DepthStencilState> depthOn;
        winrt::com_ptr<ID3D11DepthStencilState> depthOff;
        winrt::com_ptr<ID3D11BlendState> opaque;
        winrt::com_ptr<ID3D11BlendState> premultiplied;
        winrt::com_ptr<ID3D11RasterizerState> rasterizer;
        winrt::com_ptr<ID3D11SamplerState> sampler;

        std::vector<float> frames;          // 1441 frames of 42 floats
        std::uint32_t frameCount = 0;

        float camera[3] = { 0, 6, 1 };      // camera.bin: reference pitch, eye distance, scale
        std::vector<float> widths;          // widths.bin: 91 samples, one per degree
        float facetProjection[4] = { 1, 0, 224, 0.637f };

        UINT diamondVertexCount = 0;
        UINT sparkleMainCount = 0;
        UINT sparkleSmallCount = 0;

    private:
        bool LoadShaders(ID3D11Device* device, std::wstring& error);
        bool LoadAssets(ID3D11Device* device, std::wstring& error);
        bool CreateStates(ID3D11Device* device, std::wstring& error);
    };

    SceneMotion DiamondScene::Motion() const
    {
        SceneMotion motion;

        // All four read out of the beta, not chosen:
        //
        //   Ltg/f;.e        ValueAnimator.ofFloat(yaw, yaw + 360), LinearInterpolator,
        //                   setRepeatCount(-1), duration 10540.18445322793 ms
        //   g5.onTouchEvent  yaw += dx * (0.8 / density), pitch += dy * (0.8 / density)
        //
        // This is how long a turn takes and not which way it goes: the sign in that animator is
        // the beta's own yaw convention, which is not ours, and the direction was settled by
        // watching the phone instead. See Advance.
        motion.secondsPerTurn = 10.54018445322793f;

        // Long enough to be seen square-on before it turns away, which is how it reads on the
        // phone: the face carrying the anchored sparkle is the authored one, and the sparkle only
        // shows within twelve degrees of square - about a third of a second of each turn - so a
        // stone that starts turning immediately presents its least interesting angle first.
        //
        // The entry ramp, so it holds still for exactly as long as it is fading in. Raise it if
        // the sparkle should linger past that.
        motion.spinStartDelaySeconds = EntrySeconds;

        // Long enough that a kick reads as the stone being hurried along rather than as a jolt:
        // about a third of it survives half a second and it is spent inside two. Not a number
        // from the beta - the keystroke impulse is driven from somewhere the dex tooling could
        // not follow - so this and the size of each kick are both ours to settle by eye.
        motion.kickDecaySeconds = 0.5f;
        // One coefficient for both axes, and it is per **density-independent** pixel: the beta
        // divides 0.8 by the display density, so a drag covers the same angle on any screen. The
        // pointer positions this is fed arrive in XAML's logical pixels, which is the same thing.
        //
        // The gesture detector on the view carries its own pair - 0.5 for the yaw and 0.05 for
        // the pitch, per raw pixel - but the container handles the touch itself and that path
        // never runs on this screen. Taking the view's is why the stone barely tilted.
        motion.dragYawPerPixel = 0.8f;

        // Far below the beta's own 0.8, which tipped the stone over almost at a touch. The soft
        // limit supplies the shape of the resistance and this sets how much drag it takes to work
        // through it, so it is the one number here settled by eye rather than read: 45 degrees of
        // tilt costs about 330 logical pixels, and a face comes fully round at about 850.
        //
        // Steps smaller than about a halving are not felt - 0.8 to 0.5 was reported as no change
        // at all - so move it by thirds or not at all.
        motion.dragPitchPerPixel = 0.15f;
        // Not clamped. Nothing in the beta bounds the tilt - the accumulator is linear and the
        // renderer takes it raw - and on the phone a long drag does bring the top or bottom face
        // fully into view. So this is the angle the resistance builds towards rather than a wall
        // the drag stops at: ninety degrees is looking straight at that face.
        motion.pitchSoftLimitDegrees = 90.0f;

        // Ltg/f;.d - the release handler - captures both angles and runs them to zero over 600 ms
        // through an OvershootInterpolator at its default tension of 2.
        motion.settleSeconds = 0.6f;

        // Off, as it is on the phone: of the five callers of setIdleAnimationEnabled, four pass
        // false. It matters because the idle spin and the anchored sparkle fight each other by
        // design - sparkleShape.x bursts twice per six-second loop, and the visibility term only
        // opens within 12 degrees of square-on, so a turning stone hides almost every burst.
        motion.spinsByDefault = false;

        return motion;
    }

    bool DiamondScene::Load(ID3D11Device* device, SceneCache& cache, std::wstring& error)
    {
        m_resources = cache.GetOrCreate<DiamondResources>([&]() -> std::shared_ptr<DiamondResources>
        {
            auto resources = std::make_shared<DiamondResources>();
            return resources->Load(device, error) ? resources : nullptr;
        });

        if (m_resources == nullptr)
        {
            return false;
        }

        std::memcpy(m_uniforms.facetProjection, m_resources->facetProjection,
            sizeof(m_uniforms.facetProjection));

        // Per scene even though they are rewritten every frame: a shared one would work too, being
        // mapped with discard, but at a few hundred bytes it buys nothing worth the reasoning.
        if (!CreateDynamicBuffer(device, sizeof(DiamondUniforms), m_uniformBuffer)
            || !CreateDynamicBuffer(device, 16, m_instancingBuffer)
            || !CreateDynamicBuffer(device, sizeof(CompositeUniforms), m_compositeBuffer))
        {
            error = L"Diamond: constant buffers";
            return false;
        }

        return true;
    }

    bool DiamondResources::LoadShaders(ID3D11Device* device, std::wstring& error)
    {
        error = L"Diamond: shader creation failed";

        if (FAILED(device->CreateVertexShader(g_DiamondVertex, sizeof(g_DiamondVertex), nullptr,
            diamondVertexShader.put())))
        {
            return false;
        }

        // 48 bytes a vertex: position, normal and surface as float4.
        const D3D11_INPUT_ELEMENT_DESC diamondElements[] =
        {
            { "POSITION", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0,  0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
            { "NORMAL",   0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 16, D3D11_INPUT_PER_VERTEX_DATA, 0 },
            { "TEXCOORD", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 32, D3D11_INPUT_PER_VERTEX_DATA, 0 },
        };

        if (FAILED(device->CreateInputLayout(diamondElements, ARRAYSIZE(diamondElements),
            g_DiamondVertex, sizeof(g_DiamondVertex), diamondLayout.put())))
        {
            return false;
        }

        if (FAILED(device->CreatePixelShader(g_DiamondPixel, sizeof(g_DiamondPixel), nullptr,
            diamondPixelShader.put())))
        {
            return false;
        }

        if (FAILED(device->CreateVertexShader(g_SparkleVertex, sizeof(g_SparkleVertex), nullptr,
            sparkleVertexShader.put())))
        {
            return false;
        }

        // 32 bytes a vertex: two contours to morph between, then a material whose x is the layer.
        const D3D11_INPUT_ELEMENT_DESC sparkleElements[] =
        {
            { "POSITION", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0,  0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
            { "TEXCOORD", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 16, D3D11_INPUT_PER_VERTEX_DATA, 0 },
        };

        if (FAILED(device->CreateInputLayout(sparkleElements, ARRAYSIZE(sparkleElements),
            g_SparkleVertex, sizeof(g_SparkleVertex), sparkleLayout.put())))
        {
            return false;
        }

        if (FAILED(device->CreatePixelShader(g_SparklePixel, sizeof(g_SparklePixel), nullptr,
            sparklePixelShader.put())))
        {
            return false;
        }

        if (FAILED(device->CreateVertexShader(g_CompositeVertex, sizeof(g_CompositeVertex), nullptr,
            compositeVertexShader.put())))
        {
            return false;
        }

        if (FAILED(device->CreatePixelShader(g_CompositePixel, sizeof(g_CompositePixel), nullptr,
            compositePixelShader.put())))
        {
            return false;
        }

        error.clear();
        return true;
    }

    bool DiamondResources::LoadAssets(ID3D11Device* device, std::wstring& error)
    {
        std::vector<std::uint8_t> data;

        // Raw buffers, which is why there is no parser here: vertices.bin is a vertex buffer and
        // planes.bin is seventeen float4s.
        if (!ReadAsset(L"vertices.bin", data, error)) { return false; }
        if (!CreateVertexBuffer(device, data, diamondVertices))
        {
            error = L"Diamond: vertices.bin buffer";
            return false;
        }
        diamondVertexCount = static_cast<UINT>(data.size() / 48);

        if (!ReadAsset(L"main.bin", data, error)) { return false; }
        if (!CreateVertexBuffer(device, data, sparkleMain))
        {
            error = L"Diamond: main.bin buffer";
            return false;
        }
        sparkleMainCount = static_cast<UINT>(data.size() / 32);

        if (!ReadAsset(L"small.bin", data, error)) { return false; }
        if (!CreateVertexBuffer(device, data, sparkleSmall))
        {
            error = L"Diamond: small.bin buffer";
            return false;
        }
        sparkleSmallCount = static_cast<UINT>(data.size() / 32);

        // Each frame is the nine vec4 uniforms the shader names - crownGradient,
        // pavilionGradient, lightSweep, then the six sweeps - followed by sparkleShape and the
        // anchored sparkle's halo scale.
        if (!ReadAsset(L"frames.bin", data, error)) { return false; }
        if (data.size() % (FrameFloats * 4) != 0 || data.size() < FrameFloats * 4 * 2)
        {
            error = L"Diamond: frames.bin is not a whole number of 42-float frames";
            return false;
        }

        frameCount = static_cast<std::uint32_t>(data.size() / (FrameFloats * 4));
        frames.resize(data.size() / 4);
        std::memcpy(frames.data(), data.data(), data.size());

        // Seventeen planes of the convex hull, exactly the size the shaders declare.
        if (!ReadAsset(L"planes.bin", data, error)) { return false; }
        if (data.size() != 17 * 16)
        {
            error = L"Diamond: planes.bin is not 17 float4";
            return false;
        }
        if (!CreateConstantBuffer(device, data, planeBuffer))
        {
            error = L"Diamond: planes.bin buffer";
            return false;
        }

        // Eight authored { position, normal } pairs. They used to be ray-cast against the hull in
        // the vertex shader, which is most of why sparkleVertex shrank.
        if (!ReadAsset(L"anchors.bin", data, error)) { return false; }
        if (data.size() != 8 * 32)
        {
            error = L"Diamond: anchors.bin is not 8 position/normal pairs";
            return false;
        }
        if (!CreateConstantBuffer(device, data, anchorBuffer))
        {
            error = L"Diamond: anchors.bin buffer";
            return false;
        }

        // One float4: the reference camera the authored facet drawings are projected through.
        if (!ReadAsset(L"facetProjection.bin", data, error)) { return false; }
        if (data.size() != 16)
        {
            error = L"Diamond: facetProjection.bin is not one float4";
            return false;
        }
        std::memcpy(facetProjection, data.data(), data.size());

        // Reference pitch, eye distance and scale.
        if (!ReadAsset(L"camera.bin", data, error)) { return false; }
        if (data.size() != 12)
        {
            error = L"Diamond: camera.bin is not three floats";
            return false;
        }
        std::memcpy(camera, data.data(), data.size());

        // The apparent half-width per degree of a quarter turn, so the silhouette does not pulse.
        if (!ReadAsset(L"widths.bin", data, error)) { return false; }
        if (data.size() < 4 * 4)
        {
            error = L"Diamond: widths.bin is too short to spline";
            return false;
        }
        widths.resize(data.size() / 4);
        std::memcpy(widths.data(), data.data(), data.size());

        return true;
    }

    bool DiamondResources::CreateStates(ID3D11Device* device, std::wstring& error)
    {
        error = L"Diamond: pipeline state";

        D3D11_DEPTH_STENCIL_DESC depth = {};
        depth.DepthEnable = TRUE;
        depth.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
        depth.DepthFunc = D3D11_COMPARISON_LESS_EQUAL;      // GL_LEQUAL, as the beta sets it

        if (FAILED(device->CreateDepthStencilState(&depth, depthOn.put())))
        {
            return false;
        }

        depth.DepthEnable = FALSE;
        depth.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ZERO;

        if (FAILED(device->CreateDepthStencilState(&depth, depthOff.put())))
        {
            return false;
        }

        D3D11_BLEND_DESC blend = {};
        blend.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;

        if (FAILED(device->CreateBlendState(&blend, opaque.put())))
        {
            return false;
        }

        // The sparkle and composite shaders both return colour already multiplied by alpha.
        blend.RenderTarget[0].BlendEnable = TRUE;
        blend.RenderTarget[0].SrcBlend = D3D11_BLEND_ONE;
        blend.RenderTarget[0].DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
        blend.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
        blend.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
        blend.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
        blend.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;

        if (FAILED(device->CreateBlendState(&blend, premultiplied.put())))
        {
            return false;
        }

        // Culling is off because the mesh's winding is not documented anywhere and a convex solid
        // with a depth test resolves to its front surface either way.
        D3D11_RASTERIZER_DESC raster = {};
        raster.FillMode = D3D11_FILL_SOLID;
        raster.CullMode = D3D11_CULL_NONE;
        raster.DepthClipEnable = TRUE;
        raster.MultisampleEnable = TRUE;

        if (FAILED(device->CreateRasterizerState(&raster, rasterizer.put())))
        {
            return false;
        }

        D3D11_SAMPLER_DESC samplerDesc = {};
        samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
        samplerDesc.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDesc.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDesc.MaxLOD = D3D11_FLOAT32_MAX;

        if (FAILED(device->CreateSamplerState(&samplerDesc, sampler.put())))
        {
            return false;
        }

        error.clear();
        return true;
    }

    void DiamondScene::Resize(ID3D11Device* device, std::uint32_t width, std::uint32_t height)
    {
        m_offscreen = nullptr;
        m_offscreenView = nullptr;
        m_resolved = nullptr;
        m_offscreenSource = nullptr;
        m_depthView = nullptr;

        m_width = width;
        m_height = height;

        if (width == 0 || height == 0)
        {
            return;
        }

        m_samples = HighestSampleCount(device, OffscreenFormat);

        // The body and the sparkles go in here first so that the copy pass can fade the result as
        // a whole. Fading them where they are drawn would show the sparkles through the stone.
        D3D11_TEXTURE2D_DESC desc = {};
        desc.Width = width;
        desc.Height = height;
        desc.MipLevels = 1;
        desc.ArraySize = 1;
        desc.Format = OffscreenFormat;
        desc.SampleDesc.Count = m_samples;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = D3D11_BIND_RENDER_TARGET;

        // A multisampled texture cannot be read by the fade pass, which samples an ordinary
        // Texture2D. So with samples there are two textures and a resolve between them, and
        // without them the one texture is both.
        if (m_samples == 1)
        {
            desc.BindFlags |= D3D11_BIND_SHADER_RESOURCE;
        }

        if (FAILED(device->CreateTexture2D(&desc, nullptr, m_offscreen.put())))
        {
            return;
        }

        device->CreateRenderTargetView(m_offscreen.get(), nullptr, m_offscreenView.put());

        if (m_samples == 1)
        {
            m_resolved = m_offscreen;
        }
        else
        {
            D3D11_TEXTURE2D_DESC resolved = desc;
            resolved.SampleDesc.Count = 1;
            resolved.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;

            if (FAILED(device->CreateTexture2D(&resolved, nullptr, m_resolved.put())))
            {
                return;
            }
        }

        device->CreateShaderResourceView(m_resolved.get(), nullptr, m_offscreenSource.put());

        // Depth carries the same number of samples as the colour it is bound beside.
        D3D11_TEXTURE2D_DESC depth = desc;
        depth.Format = DXGI_FORMAT_D32_FLOAT;
        depth.BindFlags = D3D11_BIND_DEPTH_STENCIL;

        winrt::com_ptr<ID3D11Texture2D> depthTexture;
        if (SUCCEEDED(device->CreateTexture2D(&depth, nullptr, depthTexture.put())))
        {
            device->CreateDepthStencilView(depthTexture.get(), nullptr, m_depthView.put());
        }
    }

    /// <summary>
    /// Which of the three palettes the fragment shader takes.
    /// </summary>
    /// <remarks>
    /// 0 is the authored blue stone and is what every live view uses. 2 is the one the beta calls
    /// the "Cool diamond" - a saturated blue with an inner glow - and it reaches the screen
    /// through a different path entirely: `Ltg/d;` renders it into an off-screen pbuffer and
    /// reads it back as a bitmap, which is how a message bubble shows one without a GL surface
    /// of its own. 1 is an icy white, whose sparkle keeps only the circular halo.
    /// </remarks>
    void DiamondScene::SetVariant(int variant)
    {
        m_palette = variant;
    }

    float DiamondScene::WidthAt(float yawRadians) const
    {
        if (m_resources->widths.size() < 4)
        {
            return 1.0f;
        }

        // IEEEremainder folds the yaw into +-pi/4; 360 * |that| / pi lands it in 0..90.
        const float quarter = Pi / 2.0f;
        const float folded = yawRadians - quarter * std::floor(yawRadians / quarter + 0.5f);

        float position = std::clamp(360.0f * std::fabs(folded) / Pi, 0.0f, 89.0f);

        const int i = static_cast<int>(position);
        const float t = position - i;

        const auto at = [this](int k)
        {
            return m_resources->widths[static_cast<size_t>(
                std::clamp(k, 0, static_cast<int>(m_resources->widths.size()) - 1))];
        };

        // The beta's own cubic: value, a tangent built from the neighbours, and a curvature term.
        const float p0 = at(i), p1 = at(i + 1);
        const float slope = (p1 - p0) * 3.0f - ((p1 - at(i - 1)) / 2.0f) * 2.0f - (at(i + 2) - p0) / 2.0f;

        return p0 + (p1 - p0) * t + slope * t * (1.0f - t);
    }

    float DiamondScene::HorizontalSqueeze(float yawRadians, float pitchRadians) const
    {
        if (m_resources->widths.empty())
        {
            return 1.0f;
        }

        // s = 1 + (1 - smoothstep(a)) * ((1 - 0.035 * sin(2|fold|)^2) * widths[0] / width - 1)
        //
        // so at rest it is exactly the width normalisation - the silhouette held at its flat-on
        // width - and it fades to 1 as the stone is pitched, over |sin(pitch + camera[0])|
        // running from sin(0.25) to sin(0.96).
        const float quarter = Pi / 2.0f;
        const float fold = yawRadians - quarter * std::floor(yawRadians / quarter + 0.5f);

        const float wobble = std::sin(std::fabs(fold) * 2.0f);
        const float flatten = 1.0f - 0.035f * wobble * wobble;

        const float low = std::sin(0.25f), high = std::sin(0.96f);
        const float a = (std::fabs(std::sin(pitchRadians + m_resources->camera[0])) - low) / (high - low);
        const float ease = Smoothstep(a);

        const float width = WidthAt(yawRadians);
        if (width <= 0.0f)
        {
            return 1.0f;
        }

        return 1.0f + (1.0f - ease) * ((flatten * m_resources->widths[0] / width) - 1.0f);
    }

    Matrix4 DiamondScene::Projection(float aspect, float squeeze) const
    {
        // D3D depth is [0, 1]. The GLSL ends its vertex shaders with a remap to GL's [-1, 1] that
        // the translated shaders deliberately drop, so this is the matrix Metal was handed.
        //
        // Right-handed: depth DECREASES with z, so the viewer sits at +z. That is not a
        // preference, it is what both shaders assume - `view` is the literal float3(0, 0, 1) in
        // diamondFragment, and sparkleVertex keeps an instance only when its facet normal has a
        // positive world z. Built the other way round, the depth test resolves the far surface
        // while the shading and the sparkle gating pick the near one, so the sparkles anchor to
        // the side facing away and appear to travel against the body.
        //
        // `s` is CONSTANT: the beta divides by the reference width 0.9975 and multiplies by
        // widths[0] - the table's own first sample, indexed literally, not the splined one.
        const float baseWidth = m_resources->widths.empty() ? 0.9975f : m_resources->widths[0];
        const float distance = m_resources->camera[1];
        const float inverseScale = 1.0f / m_resources->camera[2];
        const float s = std::max(1.0f, 1.0f / aspect) * (1.52f / (0.9975f / baseWidth));

        Matrix4 r;
        r.at(0, 0) = (1.0f / (aspect * s)) * squeeze;
        r.at(1, 1) = 1.0f / s;
        r.at(2, 1) = (0.12f / s) * (-inverseScale / distance);
        r.at(2, 2) = -inverseScale / 6.0f;
        r.at(2, 3) = -inverseScale / distance;
        r.at(3, 1) = (0.12f / s) * inverseScale;
        r.at(3, 2) = 0.5f * inverseScale;
        r.at(3, 3) = inverseScale;
        return r;
    }

    void DiamondScene::Animate(float seconds)
    {
        if (m_resources->frameCount < 2)
        {
            return;
        }

        const auto loop = static_cast<float>(m_resources->frameCount - 1);

        float position = std::fmod(seconds * FramesPerSecond, loop);
        if (position < 0.0f)
        {
            position += loop;
        }

        const auto frame = static_cast<std::uint32_t>(position);
        const float blend = position - static_cast<float>(frame);

        const float* current = m_resources->frames.data() + static_cast<size_t>(frame) * FrameFloats;
        const float* next = m_resources->frames.data() + static_cast<size_t>(frame + 1) * FrameFloats;

        float values[FrameFloats];
        for (int i = 0; i < FrameFloats; ++i)
        {
            values[i] = current[i] + (next[i] - current[i]) * blend;
        }

        // The first thirty-six are the nine float4s, in the order DiamondUniforms declares them.
        std::memcpy(&m_uniforms.crownGradient, values, 36 * sizeof(float));
        std::memcpy(m_uniforms.sparkleShape, values + 36, 4 * sizeof(float));
        m_uniforms.sparkleHalo[0] = values[40];
    }

    void DiamondScene::DrawSparkles(ID3D11DeviceContext* context, UINT vertexCount,
        UINT instanceCount, UINT baseInstance, ID3D11Buffer* buffer)
    {
        Upload(context, m_instancingBuffer.get(), &baseInstance, sizeof(baseInstance));

        UINT stride = 32, offset = 0;
        context->IASetVertexBuffers(0, 1, &buffer, &stride, &offset);
        context->DrawInstanced(vertexCount, instanceCount, 0, 0);
    }

    void DiamondScene::Render(const SceneTarget& target, const ScenePose& pose)
    {
        if (m_resources == nullptr || m_offscreenView == nullptr || m_depthView == nullptr)
        {
            return;
        }

        auto context = target.context;

        // Three axes, in this order, because that is what Ltg/e;.c does: setIdentityM, then
        // rotateM about Z by -roll, about X by +(pitch + the camera's reference pitch), and about
        // Y by +yaw. Matrix.rotateM post-multiplies, so the model is Rz * Rx * Ry.
        //
        // Beta 8 was two axes with both angles negated and no reference pitch, and beta 11
        // changed all three of those things at once. The reference pitch is the visible one: the
        // stone now sits tilted by it at rest rather than square-on. The dropped negation is the
        // subtle one - it inverts the yaw convention, which is why the drag had to flip sign to
        // stay following the finger.
        const Matrix4 model =
            Matrix4::RotationZ(-pose.roll * ToRadians)
            * Matrix4::RotationX((pose.pitch * ToRadians) + m_resources->camera[0])
            * Matrix4::RotationY(pose.yaw * ToRadians);

        m_uniforms.model = model;
        m_uniforms.inverseModel = model.Transposed();   // rotation only, so transpose is inverse

        // The anchored sparkle's visibility, as Ltg/g;.onDrawFrame computes it: two smoothsteps
        // multiplied. The second is how square-on the stone is, faded over pi/15 - which is why
        // it shows for well under a second of each turn, not for half of it. The first gates on
        // the turn rate: 2pi/15 divided by the angular speed is how long the sparkle would have
        // to play, and 0.596 rad/s is exactly the idle speed, so at rest it is 1 and a fast drag
        // suppresses it.
        const float speed = std::fabs(pose.yawPerSecond * ToRadians);
        const float rate = ((0.418879f / std::max(speed, 0.001f)) - 0.06f) / 0.34f;
        const float square = std::cos(pose.pitch * ToRadians) * std::cos(pose.yaw * ToRadians);
        const float head = 1.0f - std::acos(std::clamp(square, -1.0f, 1.0f)) / 0.20944f;

        m_uniforms.sparkleHalo[3] = Smoothstep(rate) * Smoothstep(head);

        const float yawRadians = pose.yaw * ToRadians;
        const float pitchRadians = pose.pitch * ToRadians;

        const float squeeze = HorizontalSqueeze(yawRadians, pitchRadians);

        m_uniforms.projection = Projection(pose.Aspect(), squeeze);

        // The same squeeze the projection puts on k[0]. The sparkles are placed in the stone's
        // own space, so without it they hold their width while the body narrows and drift off
        // the facets they are pinned to.
        m_uniforms.sparkleHalo[2] = squeeze;
        m_uniforms.appearance[0] = static_cast<float>(m_palette);

        // lightSweep, both gradients and the six sweep float4s all come out of frames.bin, so
        // nothing here writes them.
        Animate(pose.time);

        m_uniforms.parameters[0] = pose.time;

        // Fades in over its first moment on screen, which is both what the beta does and what
        // covers the cost of getting here: a device, a swap chain, three targets and a pixel
        // shader for the driver to compile all stand between being shown and the first frame, so
        // arriving at nothing and rising is kinder than arriving late at full strength.
        m_composite.opacity = std::clamp(pose.time / EntrySeconds, 0.0f, 1.0f);
        m_uniforms.viewport[0] = static_cast<float>(target.width);
        m_uniforms.viewport[1] = static_cast<float>(target.height);

        Upload(context, m_uniformBuffer.get(), &m_uniforms, sizeof(m_uniforms));
        Upload(context, m_compositeBuffer.get(), &m_composite, sizeof(m_composite));

        D3D11_VIEWPORT viewport = {};
        viewport.Width = static_cast<float>(target.width);
        viewport.Height = static_cast<float>(target.height);
        viewport.MaxDepth = 1.0f;

        context->RSSetViewports(1, &viewport);
        context->RSSetState(m_resources->rasterizer.get());

        const float transparent[4] = { 0, 0, 0, 0 };
        context->ClearRenderTargetView(m_offscreenView.get(), transparent);
        context->ClearDepthStencilView(m_depthView.get(), D3D11_CLEAR_DEPTH, 1.0f, 0);

        ID3D11RenderTargetView* offscreen[] = { m_offscreenView.get() };
        context->OMSetRenderTargets(1, offscreen, m_depthView.get());

        // b0 uniforms, b1 planes, b2 anchors, b3 the instancing base. The sparkle vertex shader
        // is the only stage that reads b3, but binding all four everywhere costs nothing.
        ID3D11Buffer* shared[] = { m_uniformBuffer.get(), m_resources->planeBuffer.get(),
                                   m_resources->anchorBuffer.get(), m_instancingBuffer.get() };
        context->VSSetConstantBuffers(0, 4, shared);
        context->PSSetConstantBuffers(0, 4, shared);

        // The body, opaque and depth-tested.
        context->OMSetBlendState(m_resources->opaque.get(), nullptr, 0xFFFFFFFF);
        context->OMSetDepthStencilState(m_resources->depthOn.get(), 0);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->IASetInputLayout(m_resources->diamondLayout.get());
        context->VSSetShader(m_resources->diamondVertexShader.get(), nullptr, 0);
        context->PSSetShader(m_resources->diamondPixelShader.get(), nullptr, 0);

        UINT stride = 48, offset = 0;
        ID3D11Buffer* vertices[] = { m_resources->diamondVertices.get() };
        context->IASetVertexBuffers(0, 1, vertices, &stride, &offset);
        context->Draw(m_resources->diamondVertexCount, 0);

        // Over the top, no depth: they are screen-space quads pinned to hull points, and the
        // vertex shader already decided which ones face the viewer.
        context->OMSetBlendState(m_resources->premultiplied.get(), nullptr, 0xFFFFFFFF);
        context->OMSetDepthStencilState(m_resources->depthOff.get(), 0);
        context->IASetInputLayout(m_resources->sparkleLayout.get());
        context->VSSetShader(m_resources->sparkleVertexShader.get(), nullptr, 0);
        context->PSSetShader(m_resources->sparklePixelShader.get(), nullptr, 0);

        // Two draws, which in the beta are one loop over the two sparkle vertex arrays with a
        // branch picking the instance count. main.bin is instance 0: the single large anchored
        // sparkle, layers 0, 1 and 2, the ones sparkleHalo and sparkleShape steer. small.bin is
        // instances 1 to 7: the twinkles, which pulse out of phase and are gated on their own
        // facet facing the viewer.
        DrawSparkles(context, m_resources->sparkleMainCount, 1, 0, m_resources->sparkleMain.get());
        DrawSparkles(context, m_resources->sparkleSmallCount, 7, 1, m_resources->sparkleSmall.get());

        // Down to one sample before anything reads it, which is the whole of the edge
        // antialiasing: the body is drawn opaque into a target cleared to transparent, so
        // averaging a silhouette pixel's samples gives exactly the partial coverage that
        // premultiplied compositing wants.
        if (m_samples > 1)
        {
            context->ResolveSubresource(m_resolved.get(), 0, m_offscreen.get(), 0,
                OffscreenFormat);
        }

        // Copy out, which is where the fade applies.
        ID3D11RenderTargetView* back[] = { target.view };
        context->OMSetRenderTargets(1, back, nullptr);
        context->OMSetBlendState(m_resources->premultiplied.get(), nullptr, 0xFFFFFFFF);
        context->OMSetDepthStencilState(m_resources->depthOff.get(), 0);
        context->IASetInputLayout(nullptr);
        context->VSSetShader(m_resources->compositeVertexShader.get(), nullptr, 0);
        context->PSSetShader(m_resources->compositePixelShader.get(), nullptr, 0);

        ID3D11Buffer* composite[] = { m_compositeBuffer.get() };
        ID3D11ShaderResourceView* source[] = { m_offscreenSource.get() };
        ID3D11SamplerState* samplers[] = { m_resources->sampler.get() };

        context->PSSetConstantBuffers(0, 1, composite);
        context->PSSetShaderResources(0, 1, source);
        context->PSSetSamplers(0, 1, samplers);
        context->Draw(3, 0);

        // Unbound before the next frame renders into it, or the offscreen is a target and a
        // source at once and the runtime drops one of the two.
        ID3D11ShaderResourceView* none[] = { nullptr };
        context->PSSetShaderResources(0, 1, none);
    }
}
