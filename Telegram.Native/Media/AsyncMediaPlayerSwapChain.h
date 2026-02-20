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

        IVector<hstring> SwapChainOptions() const;

        bool Create(bool subscribe = false);
        void Close();

        void Attach(SwapChainPanel const& panel, bool subscribe = false);
        void Detach(SwapChainPanel const& panel);
        void Detach();

        void UpdateSize();
        void UpdateScale();

    private:
        void OnAttach(SwapChainPanel const& oldPanel, SwapChainPanel const& newPanel,
            bool subscribe);

        void OnDetach(SwapChainPanel const& oldPanel, winrt::event_token& scaleToken, winrt::event_token& sizeToken);

        void UpdateSize(int width, int height);

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

        bool m_loaded = false;

        winrt::event_token m_compositionScaleChangedToken{};
        winrt::event_token m_sizeChangedToken{};
        //winrt::event_token m_suspending{};

        // Private data GUIDs for LibVLC
        static constexpr GUID SWAPCHAIN_WIDTH_GUID = {
            0xf1b59347, 0x1643, 0x411a, {0xad, 0x6b, 0xc7, 0x80, 0x17, 0x7a, 0x06, 0xb6}
        };

        static constexpr GUID SWAPCHAIN_HEIGHT_GUID = {
            0x6ea976a0, 0x9d60, 0x4bb7, {0xa5, 0xa9, 0x7d, 0xd1, 0x18, 0x7f, 0xc9, 0xbd}
        };
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayerSwapChain : AsyncMediaPlayerSwapChainT<AsyncMediaPlayerSwapChain, implementation::AsyncMediaPlayerSwapChain>
    {
    };
}
