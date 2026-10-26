#include "pch.h"
#include "FrameSurface.h"
#if __has_include("FrameSurface.g.cpp")
#include "FrameSurface.g.cpp"
#endif

using namespace winrt::Windows::Storage::Streams;
using namespace winrt::UIComposition;

namespace winrt::Telegram::Native::implementation
{
    // Released when its thread ends, but not when the process does: ExitProcess has ended every
    // other thread by then, the driver's own among them, and a release that waits on one of them
    // never returns - the process stays alive with only this thread, still holding its instance.
    struct UploadCache
    {
        winrt::com_ptr<ID3D11Texture2D> Texture;
        winrt::com_ptr<ID3D11Device> Device;
        UINT Width = 0;
        UINT Height = 0;

        ~UploadCache()
        {
            // Looked up here rather than cached in a static: a static's first-use lock is one
            // more thing a thread killed by ExitProcess could still be holding.
            using PFN_RtlDllShutdownInProgress = BOOLEAN(NTAPI*)();
            auto shutdownInProgress = reinterpret_cast<PFN_RtlDllShutdownInProgress>(GetProcAddress(GetModuleHandle(L"ntdll.dll"), "RtlDllShutdownInProgress"));

            if (shutdownInProgress == nullptr || shutdownInProgress())
            {
                Texture.detach();
                Device.detach();
            }
        }
    };

    // Per thread, because surfaces are drawn on their view's thread only, under Direct2D's lock over
    // the device. It grows to the largest frame drawn, and a replaced device gets a new one.
    static ID3D11Texture2D* UploadTexture(winrt::com_ptr<ID3D11Device> const& device, UINT width, UINT height)
    {
        thread_local UploadCache s_cache;

        auto& s_texture = s_cache.Texture;
        auto& s_device = s_cache.Device;
        auto& s_width = s_cache.Width;
        auto& s_height = s_cache.Height;

        if (s_device != device)
        {
            s_texture = nullptr;
            s_device = device;
            s_width = 0;
            s_height = 0;
        }

        if (s_texture == nullptr || width > s_width || height > s_height)
        {
            D3D11_TEXTURE2D_DESC description{};
            description.Width = std::max(width, s_width);
            description.Height = std::max(height, s_height);
            description.MipLevels = 1;
            description.ArraySize = 1;
            description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            description.SampleDesc.Count = 1;
            description.Usage = D3D11_USAGE_DEFAULT;
            description.BindFlags = D3D11_BIND_SHADER_RESOURCE;

            s_texture = nullptr;

            if (FAILED(device->CreateTexture2D(&description, nullptr, s_texture.put())))
            {
                s_texture = nullptr;
                return nullptr;
            }

            s_width = description.Width;
            s_height = description.Height;
        }

        return s_texture.get();
    }

    FrameSurface::FrameSurface(CompositionDrawingSurface surface, winrt::com_ptr<ID2D1Factory1> const& factory, int32_t pixelWidth, int32_t pixelHeight, int32_t rotation)
        : m_surface(surface)
        , m_interop(surface.as<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop>())
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

            if (m_multithread)
            {
                m_multithread->Enter();
            }

            auto width = static_cast<UINT>(m_pixelWidth);
            auto height = static_cast<UINT>(m_pixelHeight);

            // Not straight into the surface's texture: UpdateSubresource into it on the UI thread
            // held about a frame of memory per draw for as long as drawing went on, Flush or not.
            // And one texture for the thread rather than one per surface: surfaces come and go by
            // the hundred, and the memory of each texture created and released with one was kept.
            auto upload = UploadTexture(device, width, height);
            if (upload)
            {
                D3D11_BOX box{ 0, 0, 0, width, height, 1 };
                context->UpdateSubresource(upload, 0, &box, buffer.data(), width * 4, 0);
                context->CopySubresourceRegion(texture.get(), 0, static_cast<UINT>(offset.x), static_cast<UINT>(offset.y), 0, upload, 0, &box);

                result = S_OK;
            }

            if (m_multithread)
            {
                m_multithread->Leave();
            }
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
