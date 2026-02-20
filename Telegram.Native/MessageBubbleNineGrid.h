#pragma once

#include "MessageBubbleNineGrid.g.h"

#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Xaml.h>
#include <windows.ui.xaml.media.dxinterop.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.Graphics.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <winrt/Windows.Graphics.Effects.h>
#include <winrt/Windows.Graphics.Imaging.h>
#include <windows.graphics.interop.h>
#include <windows.graphics.effects.interop.h>

using namespace winrt::Windows::UI::Composition;
using namespace winrt::Windows::UI::Xaml;

namespace abi
{
    using namespace ABI::Windows::Foundation;
    using namespace ABI::Windows::Graphics::DirectX;
    using namespace ABI::Windows::UI::Composition;
}

namespace winrt::Telegram::Native::implementation
{
    // TODO: consider creating a base class, use it for ChatBackgroundPattern as well
    // TODO: this type is not currently exposed through IDL as we just return CompositionEffectBrush
    struct MessageBubbleNineGrid : MessageBubbleNineGridT<MessageBubbleNineGrid>
    {
        MessageBubbleNineGrid(CompositionGraphicsDevice device, winrt::com_ptr<ID2D1Factory1> d2dFactory, Compositor compositor, XamlRoot xamlRoot, CompositionDrawingSurface surface, CompositionEffectBrush effect, float topLeftRadius, float topRightRadius, float bottomRightRadius, float bottomLeftRadius);

        ~MessageBubbleNineGrid();

        CompositionEffectBrush Effect();

        static constexpr float s_width = 40.f;
        static constexpr float s_height = 40.f;
        static constexpr float s_insets = 15.f;

    private:
        Compositor m_compositor;
        XamlRoot m_xamlRoot;
        CompositionGraphicsDevice m_compositionDevice;
        winrt::com_ptr<ID2D1Factory1> m_d2dFactory;
        winrt::com_ptr<abi::ICompositionDrawingSurfaceInterop> m_surface;
        CompositionNineGridBrush m_brush;
        CompositionEffectBrush m_effect;

        float m_topLeftRadius;
        float m_topRightRadius;
        float m_bottomRightRadius;
        float m_bottomLeftRadius;
        double m_rasterizationScale;

        winrt::event_token m_xamlRootChanged;
        winrt::event_token m_renderingDeviceReplaced;

        void OnXamlRootChanged(XamlRoot const& sender, XamlRootChangedEventArgs const& args);
        void OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&);

        HRESULT Invalidate(double rasterizationScale);
    };
}

// No need since the type has no public constructor
//namespace winrt::Telegram::Native::factory_implementation
//{
//    struct MessageBubbleNineGrid : MessageBubbleNineGridT<MessageBubbleNineGrid, implementation::MessageBubbleNineGrid>
//    {
//    };
//}
