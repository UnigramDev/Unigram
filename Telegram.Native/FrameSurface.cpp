#include "pch.h"
#include "FrameSurface.h"
#if __has_include("FrameSurface.g.cpp")
#include "FrameSurface.g.cpp"
#endif

using namespace winrt::Windows::Storage::Streams;
using namespace winrt::Windows::UI::Composition;

namespace winrt::Telegram::Native::implementation
{
    FrameSurface::FrameSurface(CompositionDrawingSurface surface, winrt::com_ptr<ID2D1Factory1> const& factory, int32_t pixelWidth, int32_t pixelHeight, int32_t rotation)
        : m_surface(surface)
        , m_interop(surface.as<ABI::Windows::UI::Composition::ICompositionDrawingSurfaceInterop>())
        , m_multithread(factory.try_as<ID2D1Multithread>())
        , m_pixelWidth(pixelWidth)
        , m_pixelHeight(pixelHeight)
        , m_rotation(rotation)
    {
    }

    bool FrameSurface::Draw(IBuffer buffer)
    {
        std::lock_guard const guard(m_lock);

        if (m_closed || buffer == nullptr || buffer.Length() < static_cast<uint32_t>(m_pixelWidth * m_pixelHeight * 4))
        {
            return false;
        }
        else if (m_rotation != 0)
        {
            return DrawRotated(buffer);
        }

        winrt::com_ptr<IDXGISurface> dxgiSurface;
        POINT offset;

        if (FAILED(m_interop->BeginDraw(nullptr, __uuidof(IDXGISurface), dxgiSurface.put_void(), &offset)))
        {
            return false;
        }

        HRESULT result = E_NOINTERFACE;

        if (auto texture = dxgiSurface.try_as<ID3D11Texture2D>())
        {
            winrt::com_ptr<ID3D11Device> device;
            texture->GetDevice(device.put());

            winrt::com_ptr<ID3D11DeviceContext> context;
            device->GetImmediateContext(context.put());

            D3D11_BOX box{ static_cast<UINT>(offset.x), static_cast<UINT>(offset.y), 0, static_cast<UINT>(offset.x + m_pixelWidth), static_cast<UINT>(offset.y + m_pixelHeight), 1 };

            if (m_multithread)
            {
                m_multithread->Enter();
            }

            context->UpdateSubresource(texture.get(), 0, &box, buffer.data(), m_pixelWidth * 4, 0);

            if (m_multithread)
            {
                m_multithread->Leave();
            }

            result = S_OK;
        }

        if (FAILED(m_interop->EndDraw()) || FAILED(result))
        {
            return false;
        }

        return true;
    }

    bool FrameSurface::DrawRotated(IBuffer const& buffer)
    {
        winrt::com_ptr<ID2D1DeviceContext> context;
        POINT offset;

        if (FAILED(m_interop->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), context.put_void(), &offset)))
        {
            m_bitmap = nullptr;
            return false;
        }

        winrt::com_ptr<ID2D1Device> device;
        context->GetDevice(device.put());

        HRESULT result = S_OK;

        if (m_bitmap == nullptr || device != m_bitmapDevice)
        {
            D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_NONE, 0 };

            m_bitmap = nullptr;
            result = context->CreateBitmap(D2D1::SizeU(m_pixelWidth, m_pixelHeight), nullptr, 0, properties, m_bitmap.put());
            m_bitmapDevice = device;
        }

        if (SUCCEEDED(result))
        {
            D2D1_RECT_U rect = D2D1::RectU(0, 0, m_pixelWidth, m_pixelHeight);
            result = m_bitmap->CopyFromMemory(&rect, buffer.data(), m_pixelWidth * 4);
        }

        if (SUCCEEDED(result))
        {
            // Clockwise, as video metadata means it, then moved back into the surface.
            auto width = static_cast<float>(m_pixelWidth);
            auto height = static_cast<float>(m_pixelHeight);
            auto translation = m_rotation == 90 ? D2D1::Point2F(height, 0)
                : m_rotation == 180 ? D2D1::Point2F(width, height)
                : D2D1::Point2F(0, width);

            context->SetTransform(D2D1::Matrix3x2F::Rotation(static_cast<float>(m_rotation))
                * D2D1::Matrix3x2F::Translation(translation.x + offset.x, translation.y + offset.y));
            context->Clear(D2D1::ColorF(0, 0, 0, 0));
            context->DrawBitmap(m_bitmap.get());
        }

        if (FAILED(m_interop->EndDraw()) || FAILED(result))
        {
            m_bitmap = nullptr;
            return false;
        }

        return true;
    }

    CompositionDrawingSurface FrameSurface::Surface()
    {
        return m_surface;
    }

    void FrameSurface::Close()
    {
        std::lock_guard const guard(m_lock);

        if (m_closed)
        {
            return;
        }

        m_closed = true;
        m_bitmap = nullptr;
        m_bitmapDevice = nullptr;
        m_interop = nullptr;

        if (m_surface)
        {
            m_surface.Close();
            m_surface = nullptr;
        }
    }
}
