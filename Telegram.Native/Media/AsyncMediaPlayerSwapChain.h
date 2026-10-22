#pragma once

#include "Media/AsyncMediaPlayerSwapChain.g.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Controls.h>

#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <DirectXMath.h>

#include <atomic>
#include <functional>
#include <memory>
#include <mutex>
#include <vector>
#include <string>

using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::UI::Core;
using namespace winrt::Windows::UI::Xaml;
using namespace winrt::Windows::UI::Xaml::Controls;

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayerSwapChain : AsyncMediaPlayerSwapChainT<AsyncMediaPlayerSwapChain>
    {
        explicit AsyncMediaPlayerSwapChain(bool create = false);
        ~AsyncMediaPlayerSwapChain();

        // Non-copyable
        AsyncMediaPlayerSwapChain(const AsyncMediaPlayerSwapChain&) = delete;
        AsyncMediaPlayerSwapChain& operator=(const AsyncMediaPlayerSwapChain&) = delete;

        // Movable
        AsyncMediaPlayerSwapChain(AsyncMediaPlayerSwapChain&&) = default;
        AsyncMediaPlayerSwapChain& operator=(AsyncMediaPlayerSwapChain&&) = default;

        bool IsLoaded() const noexcept { return m_loaded; }

        void Clear();

        bool Create(bool subscribe = false);
        void Close();

        void Attach(SwapChainPanel const& panel, bool subscribe = false);
        void Detach(SwapChainPanel const& panel);
        void Detach();

        void UpdateSize();
        void UpdateScale();

        // For MediaVideoRenderer, which takes its own references, so they outlive a Close() racing
        // a frame.
        winrt::com_ptr<ID3D11Device> Device() const noexcept { return m_d3d11Device; }
        winrt::com_ptr<ID3D11DeviceContext> DeviceContext() const noexcept { return m_deviceContext; }
        winrt::com_ptr<IDXGISwapChain1> SwapChain() const noexcept { return m_swapChain; }
        HANDLE ContextMutex() const noexcept { return m_contextMutex; }

        // The size the panel wants, in physical pixels. Zero until a panel reports one.
        void TargetSize(int32_t& width, int32_t& height) const noexcept
        {
            width = m_targetWidth.load();
            height = m_targetHeight.load();
        }

        // Called on the panel's thread whenever the target size changes, so a paused renderer
        // can draw its frame again at the new size.
        void SizeChanged(std::function<void()> handler);

        // Builds a new device and swap chain once the old device is lost. They are created on the
        // calling thread, which may be any; swapping them in and handing them to the panel happens
        // on the panel's thread, the only one SetSwapChain may be called on, and the one Clear and
        // the size updates run on. Done is called there once the new ones are in place, with false
        // if the swap chain was closed meanwhile. False when nothing could be created yet, which
        // while a driver is resetting is worth trying again; true without doing anything once the
        // swap chain is closed.
        bool Recreate(std::function<void(bool)> done);

    private:
        struct Resources
        {
            winrt::com_ptr<ID3D11Device> device;
            winrt::com_ptr<ID3D11DeviceContext> context;
            winrt::com_ptr<IDXGISwapChain1> swapChain;
            winrt::com_ptr<IDXGISwapChain2> swapChain2;
            winrt::com_ptr<IDXGIDevice3> device3;
            HANDLE mutex{ nullptr };

            Resources() = default;
            Resources(const Resources&) = delete;
            Resources& operator=(const Resources&) = delete;

            ~Resources()
            {
                if (mutex != nullptr)
                {
                    CloseHandle(mutex);
                }
            }
        };

        static bool CreateResources(Resources& resources);
        void Assign(Resources& resources);

        // Hands the swap chain to the panel. On the panel's thread, under m_panelLock.
        void ShowLocked();

        void OnAttach(SwapChainPanel const& oldPanel, SwapChainPanel const& newPanel,
            bool subscribe);

        void OnDetach(SwapChainPanel const& oldPanel, winrt::event_token& scaleToken, winrt::event_token& sizeToken);

        void UpdateSize(int width, int height);
        void GetScale(float& scaleX, float& scaleY) const;

        void OnCompositionScaleChanged(SwapChainPanel const& sender, winrt::Windows::Foundation::IInspectable const& args);
        void OnSizeChanged(winrt::Windows::Foundation::IInspectable const& sender, SizeChangedEventArgs const& args);
        //void OnSuspending(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::Foundation::IInspectable const&);

        void BeginOnUIThread(UIElement element, DispatchedHandler handler);

    private:
        std::mutex m_panelLock;

        SwapChainPanel m_panel{ nullptr };
        winrt::com_ptr<ID3D11Device> m_d3d11Device;
        winrt::com_ptr<IDXGIDevice3> m_device3;
        winrt::com_ptr<IDXGISwapChain2> m_swapChain2;
        winrt::com_ptr<IDXGISwapChain1> m_swapChain;
        winrt::com_ptr<ID3D11DeviceContext> m_deviceContext;
        HANDLE m_contextMutex{ nullptr };

        bool m_loaded = false;

        // Set by Close, so that a recovery still in flight does not bring a closed swap chain back.
        std::atomic<bool> m_closed{ false };

        std::atomic<int32_t> m_targetWidth{ 0 };
        std::atomic<int32_t> m_targetHeight{ 0 };

        std::mutex m_sizeChangedLock;
        std::function<void()> m_sizeChanged;

        winrt::event_token m_compositionScaleChangedToken{};
        winrt::event_token m_sizeChangedToken{};
        //winrt::event_token m_suspending{};
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayerSwapChain : AsyncMediaPlayerSwapChainT<AsyncMediaPlayerSwapChain, implementation::AsyncMediaPlayerSwapChain>
    {
    };
}
