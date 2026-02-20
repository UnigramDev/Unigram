#include "pch.h"
#include "AsyncMediaPlayerSwapChain.h"
#if __has_include("Media/AsyncMediaPlayerSwapChain.g.cpp")
#include "Media/AsyncMediaPlayerSwapChain.g.cpp"
#endif

#include <winrt/Windows.UI.Xaml.Media.h>
#include <windows.ui.xaml.media.dxinterop.h>

#include <sstream>
#include <iomanip>
#include <stdexcept>

namespace winrt::Telegram::Native::Media::implementation
{
    AsyncMediaPlayerSwapChain::AsyncMediaPlayerSwapChain(bool create)
    {
        if (create)
        {
            Create(false);
        }

        //m_suspending = Application::Current().Suspending({ this, &AsyncMediaPlayerSwapChain::OnSuspending });
    }

    AsyncMediaPlayerSwapChain::~AsyncMediaPlayerSwapChain()
    {
        Close();
    }

    //void AsyncMediaPlayerSwapChain::OnSuspending(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::Foundation::IInspectable const&)
    //{
    //    // When the app is suspended, UWP apps should call Trim so that the DirectX data is cleaned.
    //    if (m_device3)
    //    {
    //        m_device3->Trim();
    //    }
    //}

    void AsyncMediaPlayerSwapChain::Clear()
    {
        if (!m_swapChain)
        {
            return;
        }

        try
        {
            winrt::com_ptr<ID3D11Texture2D> backBuffer;
            winrt::check_hresult(m_swapChain->GetBuffer(0, IID_PPV_ARGS(backBuffer.put())));

            winrt::com_ptr<ID3D11RenderTargetView> renderTargetView;
            winrt::check_hresult(m_d3d11Device->CreateRenderTargetView(backBuffer.get(), nullptr, renderTargetView.put()));

            const float clearColor[4] = { 0.0f, 0.0f, 0.0f, 0.0f };
            m_deviceContext->ClearRenderTargetView(renderTargetView.get(), clearColor);

            winrt::check_hresult(m_swapChain->Present(0, 0));
        }
        catch (...)
        {
            // All the remote procedure calls must be wrapped in a try-catch block
        }
    }

    IVector<hstring> AsyncMediaPlayerSwapChain::SwapChainOptions() const
    {
        if (!m_loaded)
        {
            throw std::runtime_error("You must wait for the VideoView to be loaded before calling GetSwapChainOptions()");
        }

        IVector<hstring> options = winrt::single_threaded_vector<hstring>();

        std::wostringstream contextStream;
        contextStream << L"--winrt-d3dcontext=0x" << std::hex << reinterpret_cast<uintptr_t>(m_deviceContext.get());
        options.Append(contextStream.str());

        std::wostringstream swapChainStream;
        swapChainStream << L"--winrt-swapchain=0x" << std::hex << reinterpret_cast<uintptr_t>(m_swapChain.get());
        options.Append(swapChainStream.str());

        return options;
    }

    bool AsyncMediaPlayerSwapChain::Create(bool subscribe)
    {
        // TODO: this whole code and player doesn't support device loss
        // This means that device loss CAN'T be recovered without creating a new
        // LibVLC/MediaPlayer instance and everything else associated.

        winrt::com_ptr<IDXGIFactory2> dxgiFactory;

        try
        {
            UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

#ifdef _DEBUG
            creationFlags |= D3D11_CREATE_DEVICE_DEBUG;

            try
            {
                winrt::check_hresult(CreateDXGIFactory2(DXGI_CREATE_FACTORY_DEBUG, IID_PPV_ARGS(dxgiFactory.put())));
            }
            catch (...)
            {
                winrt::check_hresult(CreateDXGIFactory2(0, IID_PPV_ARGS(dxgiFactory.put())));
            }
#else
            winrt::check_hresult(CreateDXGIFactory2(0, IID_PPV_ARGS(dxgiFactory.put())));
#endif

            m_d3d11Device = nullptr;
            UINT adapterIndex = 0;

            winrt::com_ptr<IDXGIAdapter1> adapter;
            while (SUCCEEDED(dxgiFactory->EnumAdapters1(adapterIndex, adapter.put())))
            {
                HRESULT hr = D3D11CreateDevice(
                    adapter.get(),
                    D3D_DRIVER_TYPE_UNKNOWN,
                    nullptr,
                    creationFlags,
                    nullptr,
                    0,
                    D3D11_SDK_VERSION,
                    m_d3d11Device.put(),
                    nullptr,
                    m_deviceContext.put());

                if (SUCCEEDED(hr))
                {
                    break;
                }

                adapter = nullptr;
                adapterIndex++;
            }

            if (!m_d3d11Device)
            {
                throw std::runtime_error("Could not create Direct3D11 device: No compatible adapter found.");
            }

            winrt::com_ptr<IDXGIDevice1> device;
            winrt::check_hresult(m_d3d11Device->QueryInterface(IID_PPV_ARGS(device.put())));

            // Create the swap chain
            DXGI_SWAP_CHAIN_DESC1 swapChainDesc = {};
            swapChainDesc.Width = 320;  // Placeholder size
            swapChainDesc.Height = 240;
            swapChainDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            swapChainDesc.Stereo = FALSE;
            swapChainDesc.SampleDesc.Count = 1;
            swapChainDesc.SampleDesc.Quality = 0;
            swapChainDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
            swapChainDesc.BufferCount = 2;
            swapChainDesc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
            swapChainDesc.Flags = 0;
            swapChainDesc.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;

            winrt::check_hresult(dxgiFactory->CreateSwapChainForComposition(
                m_d3d11Device.get(),
                &swapChainDesc,
                nullptr,
                m_swapChain.put()));

            device->SetMaximumFrameLatency(1);

            // This is necessary so we can call Trim() on suspend
            winrt::check_hresult(device->QueryInterface(IID_PPV_ARGS(m_device3.put())));

            winrt::check_hresult(m_swapChain->QueryInterface(IID_PPV_ARGS(m_swapChain2.put())));

            m_loaded = true;

            if (m_panel)
            {
                OnAttach(nullptr, m_panel, subscribe);
            }
            else
            {
                // It's important that private data for the device is set before the swap chain is passed to LibVLC
                UpdateSize();
            }

            return true;
        }
        catch (...)
        {
            Close();
            // TODO: Add logging
            // Telegram::Logger::Error(ex.ToString());
        }

        return false;
    }

    void AsyncMediaPlayerSwapChain::Close()
    {
        //if (m_suspending)
        //{
        //    Application::Current().Suspending(m_suspending);
        //}

        if (m_swapChain2)
        {
            m_swapChain2 = nullptr;
        }

        if (m_device3)
        {
            m_device3 = nullptr;
        }

        {
            std::lock_guard<std::mutex> lock(m_panelLock);
            OnAttach(m_panel, m_panel = nullptr, false);
        }

        if (m_swapChain)
        {
            m_swapChain = nullptr;
        }

        if (m_deviceContext)
        {
            m_deviceContext = nullptr;
        }

        if (m_d3d11Device)
        {
            m_d3d11Device = nullptr;
        }

        m_loaded = false;
    }

    void AsyncMediaPlayerSwapChain::Attach(SwapChainPanel const& panel, bool subscribe)
    {
        std::lock_guard<std::mutex> lock(m_panelLock);
        OnAttach(m_panel, m_panel = panel, subscribe);
    }

    void AsyncMediaPlayerSwapChain::OnAttach(SwapChainPanel const& oldPanel, SwapChainPanel const& newPanel, bool subscribe)
    {
        if (oldPanel)
        {
            // Capture tokens locally before async call to prevent race conditions
            auto scaleToken = m_compositionScaleChangedToken;
            auto sizeToken = m_sizeChangedToken;

            m_compositionScaleChangedToken = {};
            m_sizeChangedToken = {};

            // Detach from old panel
            BeginOnUIThread(oldPanel, [this, oldPanel, scaleToken, sizeToken]() mutable
                {
                    OnDetach(oldPanel, scaleToken, sizeToken);
                });
        }

        if (m_loaded && newPanel)
        {
            // Get the native interface
            auto panelNative = newPanel.as<ISwapChainPanelNative>();
            winrt::check_hresult(panelNative->SetSwapChain(m_swapChain.get()));

            if (subscribe)
            {
                m_compositionScaleChangedToken = newPanel.CompositionScaleChanged(
                    { this, &AsyncMediaPlayerSwapChain::OnCompositionScaleChanged });

                m_sizeChangedToken = newPanel.SizeChanged(
                    { this, &AsyncMediaPlayerSwapChain::OnSizeChanged });
            }

            UpdateScale();
            UpdateSize();
        }
    }

    void AsyncMediaPlayerSwapChain::Detach(SwapChainPanel const& panel)
    {
        std::lock_guard<std::mutex> lock(m_panelLock);
        if (m_panel == panel)
        {
            OnDetach(panel, m_compositionScaleChangedToken, m_sizeChangedToken);
            m_panel = nullptr;
        }
    }

    void AsyncMediaPlayerSwapChain::Detach()
    {
        std::lock_guard<std::mutex> lock(m_panelLock);
        if (m_panel != nullptr)
        {
            OnDetach(m_panel, m_compositionScaleChangedToken, m_sizeChangedToken);
            m_panel = nullptr;
        }
    }

    void AsyncMediaPlayerSwapChain::OnDetach(SwapChainPanel const& oldPanel, winrt::event_token& scaleToken, winrt::event_token& sizeToken)
    {
        auto panelNative = oldPanel.as<ISwapChainPanelNative>();
        panelNative->SetSwapChain(NULL);

        if (scaleToken.value != 0)
        {
            oldPanel.CompositionScaleChanged(scaleToken);
            scaleToken = {};
        }

        if (sizeToken.value != 0)
        {
            oldPanel.SizeChanged(sizeToken);
            sizeToken = {};
        }
    }

    void AsyncMediaPlayerSwapChain::OnCompositionScaleChanged(SwapChainPanel const&, winrt::Windows::Foundation::IInspectable const&)
    {
        if (m_loaded)
        {
            UpdateScale();
        }
    }

    void AsyncMediaPlayerSwapChain::OnSizeChanged(IInspectable const&, SizeChangedEventArgs const&)
    {
        if (m_loaded)
        {
            UpdateSize();
        }
    }

    void AsyncMediaPlayerSwapChain::UpdateSize()
    {
        if (!m_panel || !m_swapChain)
        {
            if (m_swapChain)
            {
                // It's important that private data for the device is set before the swap chain is passed to LibVLC
                UpdateSize(320, 240);
            }
            return;
        }

        int w = static_cast<int>(m_panel.ActualWidth() * m_panel.CompositionScaleX());
        int h = static_cast<int>(m_panel.ActualHeight() * m_panel.CompositionScaleY());

        UpdateSize(w, h);
    }

    void AsyncMediaPlayerSwapChain::UpdateSize(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        try
        {
            winrt::check_hresult(m_swapChain->SetPrivateData(SWAPCHAIN_WIDTH_GUID, sizeof(int), &width));
            winrt::check_hresult(m_swapChain->SetPrivateData(SWAPCHAIN_HEIGHT_GUID, sizeof(int), &height));
        }
        catch (...)
        {
            // Handle error if needed
        }
    }

    void AsyncMediaPlayerSwapChain::UpdateScale()
    {
        if (!m_panel)
        {
            return;
        }

        // TODO: experiment
        // CompositionScale changes when the SwapChainPanel is inside a ScrollViewer and ZoomLevel changes.
        // We don't want this to happen, so let's try to use XamlRoot.RasterizationScale instead.

        float scaleX, scaleY;

        if (auto xamlRoot = m_panel.XamlRoot())
        {
            scaleX = static_cast<float>(xamlRoot.RasterizationScale());
            scaleY = static_cast<float>(xamlRoot.RasterizationScale());
        }
        else
        {
            scaleX = m_panel.CompositionScaleX();
            scaleY = m_panel.CompositionScaleY();
        }

        DXGI_MATRIX_3X2_F matrix = {};
        matrix._11 = 1.0f / scaleX;
        matrix._22 = 1.0f / scaleY;

        winrt::check_hresult(m_swapChain2->SetMatrixTransform(&matrix));
    }

    void AsyncMediaPlayerSwapChain::BeginOnUIThread(UIElement element, DispatchedHandler handler)
    {
        auto dispatcher = element.Dispatcher();
        if (dispatcher.HasThreadAccess())
        {
            handler();
        }
        else
        {
            dispatcher.RunAsync(CoreDispatcherPriority::Normal, handler);
        }
    }
}
