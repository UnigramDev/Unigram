#include "pch.h"
#include "IconScene.h"
#include "Scene3DDevice.h"

#include <wincodec.h>

#include <algorithm>
#include <cmath>
#include <cstring>
#include <vector>

// Compiled by FxCompile into the intermediate directory. See DiamondScene.cpp.
#include "IconVertex.h"
#include "StarPixel.h"
#include "CoinPixel.h"

namespace Graphics3D
{
    namespace
    {
        constexpr float Pi = 3.14159265358979f;
        constexpr float ToRadians = Pi / 180.0f;

        // Under Assets\Models. The file names are the Android app's own, apart from
        // start_texture.png, which is its res/raw/start_texture.svg rasterised as Icon3D asks
        // SvgHelper to: 240 pixels square, white.
        constexpr wchar_t AssetFolder[] = L"Icon";

        constexpr const wchar_t* StarModels[] = { L"star.binobj" };
        constexpr const wchar_t* CoinModels[] = { L"coin_outer.binobj", L"coin_inner.binobj",
            L"coin_logo.binobj", L"coin_stars.binobj" };

        // Position, normal and texture coordinate, as floats.
        constexpr UINT VertexStride = 32;

        // Icon3D's TYPE_ constants, which fragment3 compares against.
        constexpr std::int32_t TypeStar = 0;
        constexpr std::int32_t TypeCoin = 1;
        constexpr std::int32_t TypeGoldenStar = 2;

        // The theme colours GLIconRenderer reads, at their defaults. Every sheet that shows the
        // star sets its colorKey1/2 to premiumGradient2 and premiumGradient1, and every Stars
        // screen to starsGradient1 and 2; the background bitmap is premiumGradient2 blended
        // halfway into dialogBackground, whose dark value is night.attheme's.
        constexpr std::uint32_t PremiumGradient1 = 0x55A5FF;
        constexpr std::uint32_t PremiumGradient2 = 0xA767FF;
        constexpr std::uint32_t StarsGradient1 = 0xFEC846;
        constexpr std::uint32_t StarsGradient2 = 0xEC920A;
        constexpr std::uint32_t LightDialogBackground = 0xFFFFFF;
        constexpr std::uint32_t DarkDialogBackground = 0x1E1E1E;

        // GLIconRenderer.onSurfaceChanged and setLookAtM.
        constexpr float FieldOfView = 53.13f;
        constexpr float NearPlane = 1.0f;
        constexpr float FarPlane = 200.0f;
        constexpr float EyeDistance = 100.0f;

        float Channel(std::uint32_t color, int shift)
        {
            return ((color >> shift) & 0xFF) / 255.0f;
        }

        void ToFloat3(std::uint32_t color, float (&out)[3])
        {
            out[0] = Channel(color, 16);
            out[1] = Channel(color, 8);
            out[2] = Channel(color, 0);
        }

        // ColorUtils.blendARGB, without the alpha.
        void Blend(std::uint32_t from, std::uint32_t to, float ratio, float (&out)[3])
        {
            for (int i = 0; i < 3; i++)
            {
                const int shift = 16 - 8 * i;
                out[i] = Channel(from, shift) * (1 - ratio) + Channel(to, shift) * ratio;
            }
        }

        // DataInputStream, which is big-endian.
        class BigEndianReader
        {
        public:
            explicit BigEndianReader(const std::vector<std::uint8_t>& data)
                : m_data(data)
            {
            }

            size_t Remaining() const noexcept { return m_data.size() - m_offset; }

            bool Int(std::int32_t& value)
            {
                if (Remaining() < 4)
                {
                    return false;
                }

                const std::uint8_t* p = m_data.data() + m_offset;
                value = static_cast<std::int32_t>((std::uint32_t(p[0]) << 24) | (std::uint32_t(p[1]) << 16)
                    | (std::uint32_t(p[2]) << 8) | std::uint32_t(p[3]));
                m_offset += 4;
                return true;
            }

            bool Float(float& value)
            {
                std::int32_t bits;
                if (!Int(bits))
                {
                    return false;
                }

                std::memcpy(&value, &bits, sizeof(value));
                return true;
            }

            bool Floats(std::vector<float>& values)
            {
                std::int32_t count;
                if (!Int(count) || count < 0 || static_cast<size_t>(count) > Remaining() / 4)
                {
                    return false;
                }

                values.resize(static_cast<size_t>(count));
                for (float& value : values)
                {
                    Float(value);
                }

                return true;
            }

        private:
            const std::vector<std::uint8_t>& m_data;
            size_t m_offset = 0;
        };

        /// <summary>
        /// ObjLoader: three float arrays - positions, texture coordinates, normals - and then a
        /// triangle list of index triples into them, expanded here into an interleaved buffer.
        /// </summary>
        /// <remarks>
        /// The texture coordinate's v is flipped, and an index outside its array reads zero, both
        /// as ObjLoader does it - coin_inner relies on the second. Its other indices are not
        /// checked there; here they reject the file.
        /// </remarks>
        bool ParseModel(const std::vector<std::uint8_t>& data, std::vector<float>& vertices)
        {
            BigEndianReader reader(data);

            std::vector<float> positions, coordinates, normals;
            std::int32_t count;

            if (!reader.Floats(positions) || !reader.Floats(coordinates) || !reader.Floats(normals)
                || !reader.Int(count) || count < 0 || static_cast<size_t>(count) > reader.Remaining() / 12)
            {
                return false;
            }

            const auto hasCoordinate = [&](std::int64_t index)
            {
                return index >= 0 && index < static_cast<std::int64_t>(coordinates.size());
            };

            vertices.clear();
            vertices.reserve(static_cast<size_t>(count) * 8);

            for (std::int32_t i = 0; i < count; i++)
            {
                std::int32_t position, texture, normal;
                reader.Int(position);
                reader.Int(texture);
                reader.Int(normal);

                const std::int64_t p = std::int64_t(position) * 3;
                const std::int64_t n = std::int64_t(normal) * 3;

                if (p < 0 || p + 2 >= static_cast<std::int64_t>(positions.size())
                    || n < 0 || n + 2 >= static_cast<std::int64_t>(normals.size()))
                {
                    return false;
                }

                vertices.insert(vertices.end(), positions.begin() + p, positions.begin() + p + 3);
                vertices.insert(vertices.end(), normals.begin() + n, normals.begin() + n + 3);

                // `index < 0 || index >= size ? 0 : textures.get(index)` for u, and the same with
                // `1 - ` for v - so a missing v is 0, not 1.
                const std::int64_t t = std::int64_t(texture) * 2;

                vertices.push_back(hasCoordinate(t) ? coordinates[static_cast<size_t>(t)] : 0.0f);
                vertices.push_back(hasCoordinate(t + 1) ? 1.0f - coordinates[static_cast<size_t>(t + 1)] : 0.0f);
            }

            return true;
        }

        // WIC needs COM on the calling thread, and the render thread is not otherwise a COM one.
        // Joining the MTA here is harmless: nothing else on that thread uses COM.
        struct ComScope
        {
            HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
            ~ComScope()
            {
                if (SUCCEEDED(hr))
                {
                    CoUninitialize();
                }
            }
        };

        /// <summary>
        /// Decodes a PNG into an RGBA texture, unpremultiplied.
        /// </summary>
        /// <remarks>
        /// The phone uploads its bitmaps premultiplied. Of the three textures that only matters to
        /// start_texture, whose colour the star reads - so it is baked with its colour channels
        /// equal to its coverage, which reads the same either way. flecks.png is opaque and only
        /// the alpha of coin_border.png is read.
        /// </remarks>
        bool LoadTexture(ID3D11Device* device, IWICImagingFactory* factory, const wchar_t* name,
            winrt::com_ptr<ID3D11ShaderResourceView>& view, std::wstring& error)
        {
            std::vector<std::uint8_t> data;
            if (!ReadModelAsset(AssetFolder, name, data, error))
            {
                return false;
            }

            error = std::wstring(L"Icon: could not decode ") + name;

            winrt::com_ptr<IWICStream> stream;
            winrt::com_ptr<IWICBitmapDecoder> decoder;
            winrt::com_ptr<IWICBitmapFrameDecode> frame;
            winrt::com_ptr<IWICFormatConverter> converter;

            if (FAILED(factory->CreateStream(stream.put()))
                || FAILED(stream->InitializeFromMemory(data.data(), static_cast<DWORD>(data.size())))
                || FAILED(factory->CreateDecoderFromStream(stream.get(), nullptr,
                    WICDecodeMetadataCacheOnDemand, decoder.put()))
                || FAILED(decoder->GetFrame(0, frame.put()))
                || FAILED(factory->CreateFormatConverter(converter.put()))
                || FAILED(converter->Initialize(frame.get(), GUID_WICPixelFormat32bppRGBA,
                    WICBitmapDitherTypeNone, nullptr, 0, WICBitmapPaletteTypeCustom)))
            {
                return false;
            }

            UINT width = 0, height = 0;
            if (FAILED(converter->GetSize(&width, &height)) || width == 0 || height == 0
                || width > 4096 || height > 4096)
            {
                return false;
            }

            std::vector<std::uint8_t> pixels(static_cast<size_t>(width) * height * 4);
            if (FAILED(converter->CopyPixels(nullptr, width * 4, static_cast<UINT>(pixels.size()), pixels.data())))
            {
                return false;
            }

            D3D11_TEXTURE2D_DESC desc = {};
            desc.Width = width;
            desc.Height = height;
            desc.MipLevels = 1;
            desc.ArraySize = 1;
            desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
            desc.SampleDesc.Count = 1;
            desc.Usage = D3D11_USAGE_IMMUTABLE;
            desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

            D3D11_SUBRESOURCE_DATA initial = {};
            initial.pSysMem = pixels.data();
            initial.SysMemPitch = width * 4;

            winrt::com_ptr<ID3D11Texture2D> texture;
            if (FAILED(device->CreateTexture2D(&desc, &initial, texture.put()))
                || FAILED(device->CreateShaderResourceView(texture.get(), nullptr, view.put())))
            {
                error = std::wstring(L"Icon: texture for ") + name;
                return false;
            }

            error.clear();
            return true;
        }

        // At most four, as for the diamond: more is not visible at these sizes, and feature level
        // 11 guarantees four for a 32-bit target.
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

        // The back buffer's format, which the multisampled target must share to be resolved into it.
        constexpr DXGI_FORMAT TargetFormat = DXGI_FORMAT_B8G8R8A8_UNORM;

        // Matrix.perspectiveM, but with D3D's [0, 1] depth. Right-handed, viewer at +z, like the
        // look-at it is paired with.
        Matrix4 Perspective(float aspect)
        {
            const float f = 1.0f / std::tan(FieldOfView * ToRadians / 2.0f);

            Matrix4 r;
            r.at(0, 0) = f / aspect;
            r.at(1, 1) = f;
            r.at(2, 2) = FarPlane / (NearPlane - FarPlane);
            r.at(2, 3) = -1.0f;
            r.at(3, 2) = NearPlane * FarPlane / (NearPlane - FarPlane);
            r.at(3, 3) = 0.0f;
            return r;
        }
    }

    struct IconMesh
    {
        winrt::com_ptr<ID3D11Buffer> vertices;
        UINT count = 0;

        // modelIndex, which fragment3 tells the coin's parts apart by.
        winrt::com_ptr<ID3D11Buffer> model;
    };

    /// <summary>
    /// Everything about one model that is the same in every panel showing it. Never written once
    /// loaded; see DiamondResources.
    /// </summary>
    struct IconResources
    {
        bool Load(ID3D11Device* device, bool coin, std::wstring& error);

        winrt::com_ptr<ID3D11VertexShader> vertexShader;
        winrt::com_ptr<ID3D11PixelShader> pixelShader;
        winrt::com_ptr<ID3D11InputLayout> layout;

        std::vector<IconMesh> meshes;

        winrt::com_ptr<ID3D11ShaderResourceView> texture;
        winrt::com_ptr<ID3D11ShaderResourceView> normalMap;

        winrt::com_ptr<ID3D11SamplerState> sampler;
        winrt::com_ptr<ID3D11DepthStencilState> depth;
        winrt::com_ptr<ID3D11RasterizerState> rasterizer;
    };

    // Two types only so that the device's cache, which is keyed by type, holds one of each.
    struct StarResources : IconResources
    {
    };

    struct CoinResources : IconResources
    {
    };

    bool IconResources::Load(ID3D11Device* device, bool coin, std::wstring& error)
    {
        error = L"Icon: shader creation failed";

        const D3D11_INPUT_ELEMENT_DESC elements[] =
        {
            { "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0,  0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
            { "NORMAL",   0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0 },
            { "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT,    0, 24, D3D11_INPUT_PER_VERTEX_DATA, 0 },
        };

        if (FAILED(device->CreateVertexShader(g_IconVertex, sizeof(g_IconVertex), nullptr, vertexShader.put()))
            || FAILED(device->CreateInputLayout(elements, ARRAYSIZE(elements), g_IconVertex,
                sizeof(g_IconVertex), layout.put())))
        {
            return false;
        }

        const HRESULT pixel = coin
            ? device->CreatePixelShader(g_CoinPixel, sizeof(g_CoinPixel), nullptr, pixelShader.put())
            : device->CreatePixelShader(g_StarPixel, sizeof(g_StarPixel), nullptr, pixelShader.put());

        if (FAILED(pixel))
        {
            return false;
        }

        const wchar_t* const* names = coin ? CoinModels : StarModels;
        const size_t count = coin ? std::size(CoinModels) : std::size(StarModels);

        std::vector<std::uint8_t> data;
        std::vector<float> vertices;

        for (size_t i = 0; i < count; i++)
        {
            if (!ReadModelAsset(AssetFolder, names[i], data, error))
            {
                return false;
            }

            if (!ParseModel(data, vertices) || vertices.empty())
            {
                error = std::wstring(L"Icon: could not parse ") + names[i];
                return false;
            }

            IconMesh mesh;
            mesh.count = static_cast<UINT>(vertices.size() * sizeof(float) / VertexStride);

            D3D11_BUFFER_DESC desc = {};
            desc.ByteWidth = static_cast<UINT>(vertices.size() * sizeof(float));
            desc.Usage = D3D11_USAGE_IMMUTABLE;
            desc.BindFlags = D3D11_BIND_VERTEX_BUFFER;

            D3D11_SUBRESOURCE_DATA initial = {};
            initial.pSysMem = vertices.data();

            const std::int32_t index[4] = { static_cast<std::int32_t>(i), 0, 0, 0 };

            D3D11_BUFFER_DESC modelDesc = {};
            modelDesc.ByteWidth = sizeof(index);
            modelDesc.Usage = D3D11_USAGE_IMMUTABLE;
            modelDesc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;

            D3D11_SUBRESOURCE_DATA modelInitial = {};
            modelInitial.pSysMem = index;

            if (FAILED(device->CreateBuffer(&desc, &initial, mesh.vertices.put()))
                || FAILED(device->CreateBuffer(&modelDesc, &modelInitial, mesh.model.put())))
            {
                error = std::wstring(L"Icon: buffers for ") + names[i];
                return false;
            }

            meshes.push_back(std::move(mesh));
        }

        {
            ComScope com;

            winrt::com_ptr<IWICImagingFactory> factory;
            if (FAILED(CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER,
                IID_PPV_ARGS(factory.put()))))
            {
                error = L"Icon: no imaging factory";
                return false;
            }

            if (!LoadTexture(device, factory.get(), coin ? L"coin_border.png" : L"start_texture.png", texture, error)
                || !LoadTexture(device, factory.get(), L"flecks.png", normalMap, error))
            {
                return false;
            }
        }

        error = L"Icon: pipeline state";

        D3D11_SAMPLER_DESC samplerDesc = {};
        samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
        samplerDesc.AddressU = D3D11_TEXTURE_ADDRESS_WRAP;
        samplerDesc.AddressV = D3D11_TEXTURE_ADDRESS_WRAP;
        samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
        samplerDesc.MaxLOD = D3D11_FLOAT32_MAX;

        // GL's defaults, which Icon3D never changes: depth test less-than, no blending - the
        // shaders write their own alpha - and no culling.
        D3D11_DEPTH_STENCIL_DESC depthDesc = {};
        depthDesc.DepthEnable = TRUE;
        depthDesc.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
        depthDesc.DepthFunc = D3D11_COMPARISON_LESS;

        D3D11_RASTERIZER_DESC raster = {};
        raster.FillMode = D3D11_FILL_SOLID;
        raster.CullMode = D3D11_CULL_NONE;
        raster.DepthClipEnable = TRUE;
        raster.MultisampleEnable = TRUE;

        if (FAILED(device->CreateSamplerState(&samplerDesc, sampler.put()))
            || FAILED(device->CreateDepthStencilState(&depthDesc, depth.put()))
            || FAILED(device->CreateRasterizerState(&raster, rasterizer.put())))
        {
            return false;
        }

        error.clear();
        return true;
    }

    IconScene::IconScene(bool coin)
        : m_coin(coin)
        , m_motion(coin)
    {
        UpdateColors();
    }

    SceneMotion IconScene::Motion() const
    {
        // Nothing for the host to do: the scene choreographs itself, so no spin, no drag
        // coefficients and no settle.
        return SceneMotion();
    }

    bool IconScene::Load(ID3D11Device* device, SceneCache& cache, std::wstring& error)
    {
        if (m_coin)
        {
            m_resources = cache.GetOrCreate<CoinResources>([&]() -> std::shared_ptr<CoinResources>
            {
                auto resources = std::make_shared<CoinResources>();
                return resources->Load(device, true, error) ? resources : nullptr;
            });
        }
        else
        {
            m_resources = cache.GetOrCreate<StarResources>([&]() -> std::shared_ptr<StarResources>
            {
                auto resources = std::make_shared<StarResources>();
                return resources->Load(device, false, error) ? resources : nullptr;
            });
        }

        if (m_resources == nullptr)
        {
            return false;
        }

        D3D11_BUFFER_DESC desc = {};
        desc.ByteWidth = sizeof(IconUniforms);
        desc.Usage = D3D11_USAGE_DYNAMIC;
        desc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
        desc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;

        if (FAILED(device->CreateBuffer(&desc, nullptr, m_uniformBuffer.put())))
        {
            error = L"Icon: constant buffer";
            return false;
        }

        return true;
    }

    void IconScene::Resize(ID3D11Device* device, std::uint32_t width, std::uint32_t height)
    {
        m_color = nullptr;
        m_colorView = nullptr;
        m_depthView = nullptr;

        m_width = width;
        m_height = height;

        if (width == 0 || height == 0)
        {
            return;
        }

        m_samples = HighestSampleCount(device, TargetFormat);

        D3D11_TEXTURE2D_DESC desc = {};
        desc.Width = width;
        desc.Height = height;
        desc.MipLevels = 1;
        desc.ArraySize = 1;
        desc.Format = TargetFormat;
        desc.SampleDesc.Count = m_samples;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = D3D11_BIND_RENDER_TARGET;

        if (m_samples > 1)
        {
            if (FAILED(device->CreateTexture2D(&desc, nullptr, m_color.put()))
                || FAILED(device->CreateRenderTargetView(m_color.get(), nullptr, m_colorView.put())))
            {
                m_color = nullptr;
                m_colorView = nullptr;
                return;
            }
        }

        D3D11_TEXTURE2D_DESC depth = desc;
        depth.Format = DXGI_FORMAT_D32_FLOAT;
        depth.BindFlags = D3D11_BIND_DEPTH_STENCIL;

        winrt::com_ptr<ID3D11Texture2D> depthTexture;
        if (SUCCEEDED(device->CreateTexture2D(&depth, nullptr, depthTexture.put())))
        {
            device->CreateDepthStencilView(depthTexture.get(), nullptr, m_depthView.put());
        }
    }

    void IconScene::SetVariant(int variant)
    {
        m_golden = variant == GoldenVariant;
        UpdateColors();
    }

    void IconScene::SetDark(bool dark)
    {
        m_dark = dark;
        UpdateColors();
    }

    void IconScene::UpdateColors()
    {
        // GLIconRenderer.updateColors, with `golden` either nothing or everything.
        ToFloat3(m_golden ? StarsGradient1 : PremiumGradient2, m_uniforms.gradientColor1);
        ToFloat3(m_golden ? StarsGradient2 : PremiumGradient1, m_uniforms.gradientColor2);
        Blend(PremiumGradient2, m_dark ? DarkDialogBackground : LightDialogBackground, 0.5f,
            m_uniforms.backgroundColor);

        // onSurfaceCreated's isDarkBackground: a dimmer, broader highlight on a dark sheet.
        m_uniforms.spec1 = m_dark ? 1.0f : 2.0f;
        m_uniforms.spec2 = m_dark ? 0.2f : 0.13f;

        m_uniforms.golden = m_golden ? 1.0f : 0.0f;
        m_uniforms.night = m_dark ? 1 : 0;
        m_uniforms.type = m_coin ? TypeCoin : m_golden ? TypeGoldenStar : TypeStar;
    }

    void IconScene::Choreograph(const SceneGesture& gesture, float step)
    {
        if (!m_started)
        {
            m_started = true;

            // The hosts of the purple star and of the coin play the entrance - the Premium sheet
            // with a 100 ms delay, the Business screen with 200 - and the Stars screens showing
            // the golden star do not.
            if (!m_golden)
            {
                m_motion.Enter(m_coin ? 0.2f : 0.1f);
            }
        }

        // In the order the view sees them: down, scroll, up, then the tap its detector reports
        // after the up.
        if (gesture.pressed)
        {
            m_motion.Press();
        }

        if (gesture.dragX != 0 || gesture.dragY != 0)
        {
            m_motion.Drag(gesture.dragX, gesture.dragY);
        }

        if (gesture.released)
        {
            m_motion.Release();
        }

        if (gesture.tapped)
        {
            m_motion.Tap(gesture.tapX, gesture.tapY);
        }

        m_motion.Advance(step);

        // Icon3D.draw steps these per frame - enterAlpha by 16 / 220, xOffset by 0.0005 - so
        // at the sixty frames a second they assume, a 220 ms fade and 0.03 a second of drift.
        m_uniforms.alpha = std::min(1.0f, m_uniforms.alpha + step / 0.22f);
        m_uniforms.xOffset = std::fmod(m_uniforms.xOffset + step * 0.03f, 1.0f);
        m_uniforms.time += step;
    }

    void IconScene::Render(const SceneTarget& target, const ScenePose& pose)
    {
        if (m_resources == nullptr || m_depthView == nullptr || m_uniformBuffer == nullptr)
        {
            return;
        }

        auto* context = target.context;
        const IconResources& resources = *m_resources;

        // GLIconRenderer.onDrawFrame: the model is lifted, tipped about x and turned about y,
        // both negated, and seen from 100 units down +z.
        Matrix4 rotation;
        rotation.at(3, 1) = m_motion.Lift();
        rotation = rotation
            * Matrix4::RotationX(-m_motion.AngleY() * ToRadians)
            * Matrix4::RotationY(-m_motion.AngleX() * ToRadians);

        Matrix4 view;
        view.at(3, 2) = -EyeDistance;

        m_uniforms.mvp = Perspective(pose.Aspect()) * (view * rotation);
        m_uniforms.world = rotation;

        D3D11_MAPPED_SUBRESOURCE mapped = {};
        if (SUCCEEDED(context->Map(m_uniformBuffer.get(), 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped)))
        {
            std::memcpy(mapped.pData, &m_uniforms, sizeof(m_uniforms));
            context->Unmap(m_uniformBuffer.get(), 0);
        }

        ID3D11RenderTargetView* output = m_colorView ? m_colorView.get() : target.view;

        if (m_colorView)
        {
            const float transparent[4] = { 0, 0, 0, 0 };
            context->ClearRenderTargetView(output, transparent);
        }

        context->ClearDepthStencilView(m_depthView.get(), D3D11_CLEAR_DEPTH, 1.0f, 0);
        context->OMSetRenderTargets(1, &output, m_depthView.get());
        context->OMSetDepthStencilState(resources.depth.get(), 0);
        context->OMSetBlendState(nullptr, nullptr, 0xFFFFFFFF);
        context->RSSetState(resources.rasterizer.get());

        D3D11_VIEWPORT viewport = {};
        viewport.Width = static_cast<float>(m_width);
        viewport.Height = static_cast<float>(m_height);
        viewport.MaxDepth = 1.0f;
        context->RSSetViewports(1, &viewport);

        context->IASetInputLayout(resources.layout.get());
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

        ID3D11Buffer* uniforms = m_uniformBuffer.get();
        context->VSSetShader(resources.vertexShader.get(), nullptr, 0);
        context->VSSetConstantBuffers(0, 1, &uniforms);
        context->PSSetShader(resources.pixelShader.get(), nullptr, 0);
        context->PSSetConstantBuffers(0, 1, &uniforms);

        ID3D11ShaderResourceView* textures[] = { resources.texture.get(), resources.normalMap.get() };
        ID3D11SamplerState* sampler = resources.sampler.get();
        context->PSSetShaderResources(0, 2, textures);
        context->PSSetSamplers(0, 1, &sampler);

        for (const IconMesh& mesh : resources.meshes)
        {
            ID3D11Buffer* vertices = mesh.vertices.get();
            ID3D11Buffer* model = mesh.model.get();
            const UINT stride = VertexStride, offset = 0;

            context->IASetVertexBuffers(0, 1, &vertices, &stride, &offset);
            context->PSSetConstantBuffers(1, 1, &model);
            context->Draw(mesh.count, 0);
        }

        if (m_colorView)
        {
            winrt::com_ptr<ID3D11Resource> backBuffer;
            target.view->GetResource(backBuffer.put());
            context->ResolveSubresource(backBuffer.get(), 0, m_color.get(), 0, TargetFormat);
        }
    }
}
