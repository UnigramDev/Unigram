#pragma once

#include "Controls/AnimatedImageBase.g.h"
#include "FrameworkElementEx.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Media.h>

using namespace winrt::Windows::UI::Xaml;
using namespace winrt::Windows::UI::Xaml::Controls;
using namespace winrt::Windows::UI::Xaml::Media;

namespace winrt::Telegram::Native::Controls::implementation
{
    struct AnimatedImageBase : FrameworkElementEx<AnimatedImageBase, AnimatedImageBaseT<AnimatedImageBase>>
    {
        AnimatedImageBase();

        virtual void OnSizeChanged(winrt::Windows::Foundation::Size const&, winrt::Windows::Foundation::Size const&) {}
        virtual void OnViewportChanged(bool visible) {};

        void RegisterViewportChanged();
        void UnregisterViewportChanged();

    private:
        FrameworkElement::SizeChanged_revoker m_sizeChangedRevoker{};
        FrameworkElement::EffectiveViewportChanged_revoker m_effectiveViewportChangedRevoker{};

        bool m_visible{ false };

        void HandleSizeChanged(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::SizeChangedEventArgs const& e);
        void HandleEffectiveViewportChanged(FrameworkElement const& sender, EffectiveViewportChangedEventArgs const& e);
    };
}

namespace winrt::Telegram::Native::Controls::factory_implementation
{
    struct AnimatedImageBase : AnimatedImageBaseT<AnimatedImageBase, implementation::AnimatedImageBase>
    {
    };
}
