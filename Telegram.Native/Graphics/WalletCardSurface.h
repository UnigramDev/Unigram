#pragma once

#include "Graphics/WalletCardSurface.g.h"

#include <winrt/Windows.Foundation.Numerics.h>
#include <windows.ui.composition.interop.h>

namespace winrt::Telegram::Native::Graphics::implementation
{
    struct WalletCardSurface : WalletCardSurfaceT<WalletCardSurface>
    {
        WalletCardSurface(winrt::UIComposition::CompositionGraphicsDevice const& device);

        static float Margin();

        winrt::UIComposition::CompositionDrawingSurface Surface();

        float HalfHeight();

        void SetLayout(winrt::Windows::Foundation::Size const& cardSize, float cornerRadius, winrt::Windows::Foundation::Rect const& qrRect, float scale);

        void SetPalettes(WalletCardPalette const& from, WalletCardPalette const& to);

        bool Draw(WalletCardFrame const& frame);

        void Close();

    private:
        bool EnsureEffect(ID2D1DeviceContext* context);

        winrt::UIComposition::CompositionDrawingSurface m_surface{ nullptr };
        winrt::com_ptr<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop> m_interop;

        // Made on m_device and remade when a session hands out another one, which is how a device
        // loss shows up here: the surface survives it, the effect does not.
        winrt::com_ptr<ID2D1Device> m_device;
        winrt::com_ptr<ID2D1Effect> m_effect;

        WalletCardPalette m_from{};
        WalletCardPalette m_to{};

        winrt::Windows::Foundation::Size m_cardSize{};
        winrt::Windows::Foundation::Rect m_qrRect{};
        float m_cornerRadius = 0;
        float m_scale = 1;
        int32_t m_halfWidth = 0;
        int32_t m_halfHeight = 0;
        bool m_closed = false;
    };
}

namespace winrt::Telegram::Native::Graphics::factory_implementation
{
    struct WalletCardSurface : WalletCardSurfaceT<WalletCardSurface, implementation::WalletCardSurface>
    {
    };
}
