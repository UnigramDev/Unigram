#include "pch.h"
#include "AsyncMediaPlayerSwapChain.h"
#if __has_include("Media/AsyncMediaPlayerSwapChain.g.cpp")
#include "Media/AsyncMediaPlayerSwapChain.g.cpp"
#endif

#include <winrt/Windows.UI.Xaml.Media.h>
#include <windows.ui.xaml.media.dxinterop.h>

#include <format>
#include <sstream>
#include <iomanip>
#include <stdexcept>

namespace winrt::Telegram::Native::Media::implementation
{
    namespace
    {
        // Held whenever this class touches the device context or swap chain that libvlc's vout
        // and decoder threads are also driving. A null mutex means Create() never got one, and
        // then this is what the class did before: no locking, rather than a deadlock.
        struct ContextLock
        {
            explicit ContextLock(HANDLE mutex) noexcept
                : m_mutex(mutex)
            {
                if (m_mutex != nullptr)
                {
                    WaitForSingleObjectEx(m_mutex, INFINITE, FALSE);
                }
            }

            ~ContextLock() noexcept
            {
                if (m_mutex != nullptr)
                {
                    ReleaseMutex(m_mutex);
                }
            }

            ContextLock(const ContextLock&) = delete;
            ContextLock& operator=(const ContextLock&) = delete;

        private:
            HANDLE m_mutex;
        };
    }

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
            ContextLock lock(m_contextMutex);

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
            // Not a lifecycle mistake, whatever the message inherited from LibVLCSharp said: the
            // only way to get here is Create() having failed, and it logs which call went wrong.
            throw std::runtime_error("The Direct3D swap chain is not available");
        }

        IVector<hstring> options = winrt::single_threaded_vector<hstring>();

        std::wostringstream contextStream;
        contextStream << L"--winrt-d3dcontext=0x" << std::hex << reinterpret_cast<uintptr_t>(m_deviceContext.get());
        options.Append(contextStream.str());

        std::wostringstream swapChainStream;
        swapChainStream << L"--winrt-swapchain=0x" << std::hex << reinterpret_cast<uintptr_t>(m_swapChain.get());
        options.Append(swapChainStream.str());

        // d3d11va would open against the device above, fail on the ID3D11VideoDevice it needs, and
        // release a reference to it on the way out that it never took. Don't let it start.
        if (!m_hardwareDecoding)
        {
            options.Append(L"--avcodec-hw=none");
        }

        return options;
    }

    bool AsyncMediaPlayerSwapChain::Create(bool subscribe)
    {
        m_closed = false;

        Resources resources;

        if (!CreateResources(resources))
        {
            Close();
            return false;
        }

        Assign(resources);
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

    bool AsyncMediaPlayerSwapChain::CreateResources(Resources& resources)
    {
        winrt::com_ptr<IDXGIFactory2> dxgiFactory;

        // Every call below is into the display driver and any of them can fail while the driver is
        // resetting. The HRESULT on its own doesn't say which one did, and the whole function used
        // to fail silently, so the failure only surfaced later as an unrelated-looking error from
        // SwapChainOptions. Name the stage as we go and log it.
        const wchar_t* stage = L"CreateDXGIFactory2";

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

            stage = L"D3D11CreateDevice";

            UINT adapterIndex = 0;

            // This device is handed to libvlc, whose DXVA decoder queries ID3D11VideoDevice off it
            // and whose vout allocates decoder textures on it. An adapter that can do neither --
            // the Basic Render Driver, or a display driver that exposes no video support -- fails
            // every hardware decode, and each failed attempt releases a reference to this device
            // that libvlc never took. So take the first adapter that can decode, and fall back to
            // the first one that merely works, so such a machine still gets a picture.
            winrt::com_ptr<ID3D11Device> fallbackDevice;
            winrt::com_ptr<ID3D11DeviceContext> fallbackContext;

            winrt::com_ptr<IDXGIAdapter1> adapter;
            while (SUCCEEDED(dxgiFactory->EnumAdapters1(adapterIndex, adapter.put())))
            {
                DXGI_ADAPTER_DESC1 desc{};
                const bool software = SUCCEEDED(adapter->GetDesc1(&desc))
                    && (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) != 0;

                HRESULT hr = D3D11CreateDevice(
                    adapter.get(),
                    D3D_DRIVER_TYPE_UNKNOWN,
                    nullptr,
                    creationFlags,
                    nullptr,
                    0,
                    D3D11_SDK_VERSION,
                    resources.device.put(),
                    nullptr,
                    resources.context.put());

                if (SUCCEEDED(hr))
                {
                    winrt::com_ptr<ID3D11VideoDevice> videoDevice;
                    if (!software && SUCCEEDED(resources.device->QueryInterface(IID_PPV_ARGS(videoDevice.put()))))
                    {
                        resources.hardwareDecoding = true;
                        break;
                    }

                    LOGGER_WARNING(L"Adapter {} ({}) can't decode video", adapterIndex, desc.Description);

                    if (!fallbackDevice)
                    {
                        fallbackDevice = resources.device;
                        fallbackContext = resources.context;
                    }
                }

                // put() overwrites without releasing, so anything a failed attempt left behind has
                // to go before the next adapter is tried.
                resources.device = nullptr;
                resources.context = nullptr;

                adapter = nullptr;
                adapterIndex++;
            }

            if (!resources.device && fallbackDevice)
            {
                resources.device = std::move(fallbackDevice);
                resources.context = std::move(fallbackContext);
            }

            if (!resources.device)
            {
                throw std::runtime_error("Could not create Direct3D11 device: No compatible adapter found.");
            }

            // Everything below is what libvlc requires of a host that supplies its own device --
            // see its reference integration, doc/libvlc/d3d11_swapr.cpp. The vout and the DXVA
            // decoder each run on their own thread and drive this context, so it needs the
            // runtime lock, and it needs a mutex published where libvlc's modules look for it:
            // d3d11va reads the handle back off the context and hands it to ffmpeg as the lock
            // around DecoderBeginFrame/SubmitDecoderBuffers/DecoderEndFrame. Without it libvlc
            // logs "No mutex found to lock the decoder" and that sequence runs unguarded.
            stage = L"ID3D11Multithread";

            winrt::com_ptr<ID3D11Multithread> multithread;
            winrt::check_hresult(resources.device->QueryInterface(IID_PPV_ARGS(multithread.put())));
            multithread->SetMultithreadProtected(TRUE);

            stage = L"CreateMutexEx";

            resources.mutex = CreateMutexEx(nullptr, nullptr, 0, SYNCHRONIZE);
            winrt::check_bool(resources.mutex != nullptr);

            stage = L"SetPrivateData";

            winrt::check_hresult(resources.context->SetPrivateData(CONTEXT_MUTEX_GUID,
                sizeof(resources.mutex), &resources.mutex));

            stage = L"IDXGIDevice1";

            winrt::com_ptr<IDXGIDevice1> device;
            winrt::check_hresult(resources.device->QueryInterface(IID_PPV_ARGS(device.put())));

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

            stage = L"CreateSwapChainForComposition";

            winrt::check_hresult(dxgiFactory->CreateSwapChainForComposition(
                resources.device.get(),
                &swapChainDesc,
                nullptr,
                resources.swapChain.put()));

            device->SetMaximumFrameLatency(1);

            stage = L"IDXGIDevice3";

            // This is necessary so we can call Trim() on suspend
            winrt::check_hresult(device->QueryInterface(IID_PPV_ARGS(resources.device3.put())));

            stage = L"IDXGISwapChain2";

            winrt::check_hresult(resources.swapChain->QueryInterface(IID_PPV_ARGS(resources.swapChain2.put())));

            return true;
        }
        catch (winrt::hresult_error const& ex)
        {
            LOGGER_ERROR(L"{} failed: 0x{:08X}, {}", stage, static_cast<uint32_t>(ex.code()), ex.message().c_str());
        }
        catch (std::exception const& ex)
        {
            LOGGER_ERROR(L"{} failed: {}", stage, winrt::to_hstring(ex.what()).c_str());
        }
        catch (...)
        {
            LOGGER_ERROR(L"{} failed", stage);
        }

        return false;
    }


    void AsyncMediaPlayerSwapChain::Assign(Resources& resources)
    {
        m_swapChain2 = std::move(resources.swapChain2);
        m_device3 = std::move(resources.device3);
        m_swapChain = std::move(resources.swapChain);
        m_deviceContext = std::move(resources.context);
        m_d3d11Device = std::move(resources.device);
        m_hardwareDecoding = resources.hardwareDecoding;

        if (m_contextMutex != nullptr)
        {
            CloseHandle(m_contextMutex);
        }

        m_contextMutex = resources.mutex;
        resources.mutex = nullptr;
    }

    bool AsyncMediaPlayerSwapChain::Recreate(std::function<void(bool)> done)
    {
        if (m_closed)
        {
            return true;
        }

        auto resources = std::make_shared<Resources>();

        if (!CreateResources(*resources))
        {
            return false;
        }

        SwapChainPanel panel{ nullptr };

        {
            std::lock_guard<std::mutex> lock(m_panelLock);
            panel = m_panel;
        }

        auto apply = [self = get_strong(), resources, done]()
            {
                bool applied = false;
                SwapChainPanel panel{ nullptr };

                {
                    std::lock_guard<std::mutex> lock(self->m_panelLock);

                    if (!self->m_closed)
                    {
                        self->Assign(*resources);
                        self->m_loaded = true;

                        if (self->m_panel && self->m_panel.Dispatcher().HasThreadAccess())
                        {
                            self->ShowLocked();
                        }
                        else
                        {
                            // A panel attached since Recreate looked is shown the new swap chain on
                            // its own thread.
                            panel = self->m_panel;

                            if (!panel)
                            {
                                self->UpdateSize();
                            }
                        }

                        applied = true;
                    }
                }

                if (panel)
                {
                    self->BeginOnUIThread(panel, [self]()
                        {
                            std::lock_guard<std::mutex> lock(self->m_panelLock);

                            if (!self->m_closed && self->m_panel)
                            {
                                self->ShowLocked();
                            }
                        });
                }

                if (done)
                {
                    done(applied);
                }
            };

        if (panel)
        {
            BeginOnUIThread(panel, apply);
        }
        else
        {
            apply();
        }

        return true;
    }

    void AsyncMediaPlayerSwapChain::ShowLocked()
    {
        try
        {
            winrt::check_hresult(m_panel.as<ISwapChainPanelNative>()->SetSwapChain(m_swapChain.get()));
            UpdateScale();
        }
        catch (winrt::hresult_error const& ex)
        {
            LOGGER_ERROR(L"SetSwapChain failed: 0x{:08X}", static_cast<uint32_t>(ex.code()));
        }

        UpdateSize();
    }

    void AsyncMediaPlayerSwapChain::Close()
    {
        // Under the lock a recovery swaps the new device and swap chain in with, so that one in
        // flight either finds this closed or is done before it starts.
        std::lock_guard<std::mutex> lock(m_panelLock);

        m_closed = true;

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

        OnAttach(m_panel, m_panel = nullptr, false);

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

        // Last, once nothing can still be waiting on it: libvlc reads the handle but never owns
        // it, so releasing the context does not release this.
        if (m_contextMutex != nullptr)
        {
            CloseHandle(m_contextMutex);
            m_contextMutex = nullptr;
        }

        m_loaded = false;
        m_hardwareDecoding = false;
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

        const int32_t previousWidth = m_targetWidth.exchange(width);
        const int32_t previousHeight = m_targetHeight.exchange(height);

        if (previousWidth != width || previousHeight != height)
        {
            std::function<void()> handler;
            {
                std::lock_guard<std::mutex> lock(m_sizeChangedLock);
                handler = m_sizeChanged;
            }

            if (handler)
            {
                handler();
            }
        }
    }

    void AsyncMediaPlayerSwapChain::SizeChanged(std::function<void()> handler)
    {
        std::lock_guard<std::mutex> lock(m_sizeChangedLock);
        m_sizeChanged = std::move(handler);
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
