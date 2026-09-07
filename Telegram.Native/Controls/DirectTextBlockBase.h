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
        FrameworkElement::EffectiveViewportChanged_revoker m_effectiveViewportChangedRevoker{};

        void HandleEffectiveViewportChanged(FrameworkElement const& sender, EffectiveViewportChangedEventArgs const& args);
    };
}

namespace winrt::Telegram::Native::Controls::factory_implementation
{
    struct DirectTextBlockBase : DirectTextBlockBaseT<DirectTextBlockBase, implementation::DirectTextBlockBase>
    {
    };
}
