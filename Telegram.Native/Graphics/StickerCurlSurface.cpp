#include "pch.h"
#include "StickerCurlSurface.h"
#if __has_include("Graphics/StickerCurlSurface.g.cpp")
#include "Graphics/StickerCurlSurface.g.cpp"
#endif

#include "StickerCurlEffect.h"

#include <cmath>

using namespace winrt::GraphicsDirectX;
using namespace winrt::Windows::Storage::Streams;
using namespace winrt::UIComposition;

namespace winrt::Telegram::Native::Graphics::implementation
{
    // The curl's shape as a share of the sticker's side, and how far the eye is, tuned by eye.
    // A lifted point rises on screen by Rise of its height.
    static constexpr float CurlBend = 0.72f;
    static constexpr float CurlRadius = 0.3f;
    static constexpr float CurlEye = 3.2f;
    static constexpr float CurlRise = 0.15f;

    // Each factory takes the registration once; a view thread has a device, and a factory, of its
    // own.
    static std::mutex s_registeredLock;
    static std::vector<winrt::com_ptr<ID2D1Factory1>> s_registered;

    static bool EnsureRegistered(ID2D1DeviceContext* context)
    {
        winrt::com_ptr<ID2D1Factory> factory;
        context->GetFactory(factory.put());

        auto factory1 = factory.try_as<ID2D1Factory1>();
        if (factory1 == nullptr)
        {
            return false;
        }

        std::lock_guard const guard(s_registeredLock);

        for (auto const& registered : s_registered)
        {
            if (registered == factory1)
            {
                return true;
            }
        }

        if (FAILED(StickerCurlEffect::Register(factory1.get())))
        {
            return false;
        }

        s_registered.push_back(factory1);
        return true;
    }

    StickerCurlSurface::StickerCurlSurface(CompositionGraphicsDevice const& device, int32_t side)
        : m_side(std::max(side, 1))
        , m_extent(static_cast<int32_t>(std::ceil(std::max(side, 1) * (1 + 2 * Margin()))))
    {
        m_surface = device.CreateDrawingSurface2({ m_extent, m_extent }, DirectXPixelFormat::B8G8R8A8UIntNormalized, DirectXAlphaMode::Premultiplied);
        m_interop = m_surface.as<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop>();
    }

    float StickerCurlSurface::Margin()
    {
        // The leading edge lies flat and the far one comes the most off the card: at the
        // sharpest curl it is drawn about a quarter larger, and higher on screen.
        return 0.3f;
    }

    CompositionDrawingSurface StickerCurlSurface::Surface()
    {
        return m_surface;
    }

    void StickerCurlSurface::SetImage(IBuffer const& pixels, int32_t pixelWidth, int32_t pixelHeight, float dieCut)
    {
        if (m_closed || pixels == nullptr || pixelWidth <= 0 || pixelHeight <= 0 || pixels.Length() < static_cast<uint32_t>(pixelWidth * pixelHeight * 4))
        {
            return;
        }

        // A frame of a playing sticker comes every few milliseconds at the same size, and goes into
        // the bitmap already there rather than a new one.
        if (pixelWidth != m_pixelWidth || pixelHeight != m_pixelHeight)
        {
            m_art = nullptr;
        }

        m_pixels.assign(pixels.data(), pixels.data() + pixelWidth * pixelHeight * 4);
        m_pixelWidth = pixelWidth;
        m_pixelHeight = pixelHeight;
        m_dieCut = dieCut;

        m_artDirty = true;
        m_sheetValid = false;
    }

    bool StickerCurlSurface::EnsureResources(ID2D1DeviceContext* context)
    {
        winrt::com_ptr<ID2D1Device> device;
        context->GetDevice(device.put());

        if (device != m_device)
        {
            ReleaseResources();
            m_device = device;
        }

        D2D1_BITMAP_PROPERTIES1 target = { { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, nullptr };

        if (m_sheet == nullptr)
        {
            if (FAILED(context->CreateBitmap(D2D1::SizeU(m_extent, m_extent), nullptr, 0, target, m_sheet.put())))
            {
                return false;
            }

            m_sheetValid = false;
        }

        if (m_output == nullptr && FAILED(context->CreateBitmap(D2D1::SizeU(m_extent, m_extent), nullptr, 0, target, m_output.put())))
        {
            return false;
        }

        if (m_art == nullptr)
        {
            D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_NONE, nullptr };

            if (FAILED(context->CreateBitmap(D2D1::SizeU(m_pixelWidth, m_pixelHeight), m_pixels.data(), m_pixelWidth * 4, properties, m_art.put())))
            {
                return false;
            }

            m_artDirty = false;
            m_sheetValid = false;
        }
        else if (m_artDirty)
        {
            D2D1_RECT_U rect = D2D1::RectU(0, 0, m_pixelWidth, m_pixelHeight);

            if (FAILED(m_art->CopyFromMemory(&rect, m_pixels.data(), m_pixelWidth * 4)))
            {
                return false;
            }

            m_artDirty = false;
            m_sheetValid = false;
        }

        if (m_composite == nullptr)
        {
            // The die-cut: the coverage in white, blurred, then pushed through 10a - 150 so the
            // soft halo becomes a crisp rounded edge a few pixels out, under the art.
            if (FAILED(context->CreateEffect(CLSID_D2D1ColorMatrix, m_coverage.put()))
                || FAILED(context->CreateEffect(CLSID_D2D1GaussianBlur, m_blur.put()))
                || FAILED(context->CreateEffect(CLSID_D2D1ColorMatrix, m_edge.put()))
                || FAILED(context->CreateEffect(CLSID_D2D1Composite, m_composite.put())))
            {
                m_coverage = nullptr;
                m_blur = nullptr;
                m_edge = nullptr;
                m_composite = nullptr;
                return false;
            }

            m_coverage->SetValue(D2D1_COLORMATRIX_PROP_COLOR_MATRIX, D2D1::Matrix5x4F(
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 1,
                1, 1, 1, 0));

            m_blur->SetInputEffect(0, m_coverage.get());
            m_blur->SetValue(D2D1_GAUSSIANBLUR_PROP_BORDER_MODE, D2D1_BORDER_MODE_SOFT);

            m_edge->SetInputEffect(0, m_blur.get());
            m_edge->SetValue(D2D1_COLORMATRIX_PROP_COLOR_MATRIX, D2D1::Matrix5x4F(
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 10,
                1, 1, 1, -150.0f / 255));
            m_edge->SetValue(D2D1_COLORMATRIX_PROP_CLAMP_OUTPUT, TRUE);

            m_composite->SetInputCount(2);
            m_composite->SetInputEffect(0, m_edge.get());
        }

        if (m_curl == nullptr)
        {
            if (!EnsureRegistered(context) || FAILED(context->CreateEffect(CLSID_StickerCurlEffect, m_curl.put())))
            {
                m_curl = nullptr;
                return false;
            }

            m_curl->SetInput(0, m_sheet.get());
        }

        return true;
    }

    bool StickerCurlSurface::Draw(float curl, winrt::Windows::Foundation::Numerics::float2 const& direction, float bend)
    {
        if (m_closed || m_pixels.empty())
        {
            return false;
        }

        winrt::com_ptr<ID2D1DeviceContext> context;
        POINT offset;

        if (FAILED(m_interop->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), context.put_void(), &offset)))
        {
            return false;
        }

        bool drawn = false;

        if (EnsureResources(context.get()))
        {
            winrt::com_ptr<ID2D1Image> target;
            context->GetTarget(target.put());
            context->SetDpi(96, 96);

            auto extent = static_cast<float>(m_extent);
            auto side = static_cast<float>(m_side);

            if (!m_sheetValid)
            {
                m_coverage->SetInput(0, m_art.get());
                m_composite->SetInput(1, m_art.get());
                m_blur->SetValue(D2D1_GAUSSIANBLUR_PROP_STANDARD_DEVIATION, m_dieCut);

                // Fitted into the sticker's side, in the middle of the room around it.
                auto fit = std::min(side / m_pixelWidth, side / m_pixelHeight);

                context->SetTarget(m_sheet.get());
                context->SetTransform(D2D1::Matrix3x2F::Scale(fit, fit)
                    * D2D1::Matrix3x2F::Translation((extent - m_pixelWidth * fit) / 2, (extent - m_pixelHeight * fit) / 2));
                context->Clear(D2D1::ColorF(0, 0, 0, 0));
                context->DrawImage(m_composite.get());

                m_sheetValid = true;
            }

            auto length = std::sqrt(direction.x * direction.x + direction.y * direction.y);
            auto x = length > 0.0001f ? direction.x / length : 0.0f;
            auto y = length > 0.0001f ? direction.y / length : 1.0f;

            // The sheet is the sticker and the edge drawn round it.
            auto sheet = side + 2 * m_dieCut;

            m_curl->SetValue(StickerCurlProperty_Sheet, D2D1::Vector4F(extent / 2, extent / 2, sheet, std::clamp(curl, 0.0f, 1.0f)));
            m_curl->SetValue(StickerCurlProperty_Motion, D2D1::Vector4F(x, y, CurlBend * bend, sheet * CurlRadius));
            m_curl->SetValue(StickerCurlProperty_View, D2D1::Vector2F(sheet * CurlEye, CurlRise));

            // Into a bitmap of its own first, untransformed: the shader reads scene positions as
            // input positions, which only holds with no transform or offset on the way.
            context->SetTarget(m_output.get());
            context->SetTransform(D2D1::Matrix3x2F::Identity());
            context->Clear(D2D1::ColorF(0, 0, 0, 0));
            context->DrawImage(m_curl.get());

            context->SetTarget(target.get());
            context->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<float>(offset.x), static_cast<float>(offset.y)));
            context->Clear(D2D1::ColorF(0, 0, 0, 0));
            context->DrawBitmap(m_output.get());

            drawn = true;
        }

        if (FAILED(m_interop->EndDraw()) || !drawn)
        {
            // Whatever failed is remade from scratch on the next frame.
            ReleaseResources();
            return false;
        }

        return true;
    }

    void StickerCurlSurface::Trim()
    {
        // The effects hold the bitmaps as their inputs, so they go too.
        ReleaseResources();
    }

    void StickerCurlSurface::ReleaseResources()
    {
        m_curl = nullptr;
        m_composite = nullptr;
        m_edge = nullptr;
        m_blur = nullptr;
        m_coverage = nullptr;
        m_output = nullptr;
        m_sheet = nullptr;
        m_art = nullptr;
        m_device = nullptr;
        m_sheetValid = false;
    }

    void StickerCurlSurface::Close()
    {
        if (m_closed)
        {
            return;
        }

        m_closed = true;
        ReleaseResources();
        m_pixels.clear();
        m_pixels.shrink_to_fit();
        m_interop = nullptr;

        if (m_surface)
        {
            m_surface.Close();
            m_surface = nullptr;
        }
    }
}
