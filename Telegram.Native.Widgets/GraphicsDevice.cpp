#include "pch.h"
#include "GraphicsDevice.h"

using namespace winrt::UIComposition;

namespace Widgets
{
    GraphicsDevice& GraphicsDevice::Current()
    {
        static GraphicsDevice s_current;
        return s_current;
    }

    GraphicsDevice::GraphicsDevice()
    {
        D2D1_FACTORY_OPTIONS options{};
        winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, __uuidof(ID2D1Factory1), &options, m_d2dFactory.put_void()));

        winrt::com_ptr<ID3D11Device> d3d;
        UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
        HRESULT hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags, nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, nullptr);
        if (FAILED(hr))
        {
            winrt::check_hresult(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, flags, nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, nullptr));
        }

        winrt::check_hresult(m_d2dFactory->CreateDevice(d3d.as<IDXGIDevice>().get(), m_d2dDevice.put()));
        winrt::check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), reinterpret_cast<IUnknown**>(m_dwrite.put())));
        m_wic = winrt::create_instance<IWICImagingFactory>(CLSID_WICImagingFactory);
    }

    CompositionGraphicsDevice GraphicsDevice::CompositionDevice(Compositor const& compositor)
    {
        std::lock_guard guard(m_lock);

        for (auto const& [owner, device] : m_devices)
        {
            if (owner == compositor)
            {
                return device;
            }
        }

        CompositionGraphicsDevice device{ nullptr };
#ifdef WINUI3
        // The Windows App SDK declares the interop with the projected type, not an ABI one.
        ICompositionGraphicsDevice created{ nullptr };
        winrt::check_hresult(compositor.as<winrt::UICompositionInterop::ICompositorInterop>()->CreateGraphicsDevice(m_d2dDevice.get(), &created));
        device = created.as<CompositionGraphicsDevice>();
#else
        winrt::check_hresult(compositor.as<ABI::Windows::UI::Composition::ICompositorInterop>()->CreateGraphicsDevice(
            m_d2dDevice.get(), reinterpret_cast<ABI::Windows::UI::Composition::ICompositionGraphicsDevice**>(winrt::put_abi(device))));
#endif

        m_devices.emplace_back(compositor, device);
        return device;
    }
}
