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

        virtual void OnLoaded() override;
        virtual void OnUnloaded() override;

        virtual void OnSizeChanged(winrt::Windows::UI::Xaml::SizeChangedEventArgs const&) {}
        virtual void OnRasterizationScaleChanged(double rasterizationScale) {}
        virtual void OnViewportChanged(bool visible) {};

        void RegisterViewportChanged();
        void UnregisterViewportChanged();

    private:
        FrameworkElement::SizeChanged_revoker m_sizeChangedRevoker{};
        XamlRoot::Changed_revoker m_xamlRootChangedRevoker{};
        FrameworkElement::EffectiveViewportChanged_revoker m_effectiveViewportChangedRevoker{};

        double m_rasterizationScale{ 0 };
        bool m_visible{ false };

        void HandleSizeChanged(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::SizeChangedEventArgs const& e);
        void HandleXamlRootChanged(winrt::Windows::UI::Xaml::XamlRoot const& sender, winrt::Windows::UI::Xaml::XamlRootChangedEventArgs const& e);
        void HandleEffectiveViewportChanged(FrameworkElement const& sender, EffectiveViewportChangedEventArgs const& e);
    };
}

namespace winrt::Telegram::Native::Controls::factory_implementation
{
    struct AnimatedImageBase : AnimatedImageBaseT<AnimatedImageBase, implementation::AnimatedImageBase>
    {
    };
}
