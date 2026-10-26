#pragma once

#include "Controls/AnimatedImageBase.g.h"
#include "FrameworkElementEx.h"

#include <winrt/Windows.Foundation.h>

using namespace winrt::UIXaml;
using namespace winrt::UIXaml::Controls;
using namespace winrt::UIXaml::Media;

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
        // A token, not a revoker: a revoker unhooks from the destructor, which is the finalizer
        // thread once a managed subclass owns the outer object. See FrameworkElementEx.
        winrt::event_token m_effectiveViewportChangedToken{};

        bool m_visible{ false };

        void HandleSizeChanged(winrt::Windows::Foundation::IInspectable const&, winrt::UIXaml::SizeChangedEventArgs const& e);
        void HandleEffectiveViewportChanged(FrameworkElement const& sender, EffectiveViewportChangedEventArgs const& e);
    };
}

namespace winrt::Telegram::Native::Controls::factory_implementation
{
    struct AnimatedImageBase : AnimatedImageBaseT<AnimatedImageBase, implementation::AnimatedImageBase>
    {
    };
}
