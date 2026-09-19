#pragma once

#include "Controls/DirectTextBlockBase.g.h"
#include "FrameworkElementEx.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Controls.h>

using namespace winrt::Windows::UI::Xaml;
using namespace winrt::Windows::UI::Xaml::Controls;

namespace winrt::Telegram::Native::Controls::implementation
{
    // The viewport subscription lives here rather than in the C# control: the framework
    // allocates a fresh event args and its wrapper on every raise, and a list scrolling past
    // dozens of subscribers turns that into hundreds of megabytes of garbage.
    struct DirectTextBlockBase : FrameworkElementEx<DirectTextBlockBase, DirectTextBlockBaseT<DirectTextBlockBase>>
    {
        DirectTextBlockBase() = default;

        virtual void OnViewportChanged(double left, double top, double right, double bottom) {}

        void RegisterViewportChanged();
        void UnregisterViewportChanged();

    private:
        // A token, not a revoker: a revoker unhooks from the destructor, which is the finalizer
        // thread once a managed subclass owns the outer object. See FrameworkElementEx.
        winrt::event_token m_effectiveViewportChangedToken{};

        void HandleEffectiveViewportChanged(FrameworkElement const& sender, EffectiveViewportChangedEventArgs const& args);
    };
}

namespace winrt::Telegram::Native::Controls::factory_implementation
{
    struct DirectTextBlockBase : DirectTextBlockBaseT<DirectTextBlockBase, implementation::DirectTextBlockBase>
    {
    };
}
