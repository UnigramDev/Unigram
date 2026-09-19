#pragma once

#include "Controls/FormattedTextBlockBase.g.h"
#include "FrameworkElementEx.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/Windows.UI.Xaml.Input.h>
#include <winrt/Windows.UI.ViewManagement.h>

using namespace winrt::Windows::UI::Xaml;
using namespace winrt::Windows::UI::Xaml::Controls;
using namespace winrt::Windows::UI::Xaml::Media;
using namespace winrt::Windows::UI::Xaml::Input;
using namespace winrt::Windows::UI::ViewManagement;

namespace winrt::Telegram::Native::Controls::implementation
{
    struct FormattedTextBlockBase : FrameworkElementEx<FormattedTextBlockBase, FormattedTextBlockBaseT<FormattedTextBlockBase>>
    {
        FormattedTextBlockBase() = default;

        void OnApplyTemplate();

        virtual void OnLayoutUpdated() {}
        virtual void OnViewportChanged(double left, double top, double right, double bottom) {}

        void RegisterLayoutChanged();

        void RegisterViewportChanged();
        void UnregisterViewportChanged();

    private:
        RichTextBlock m_textBlock{ nullptr };

        // Tokens, not revokers: a revoker unhooks from the destructor, which is the finalizer
        // thread once a managed subclass owns the outer object, and XAML's remove_* is
        // thread-affine. Unhooking happens on the UI thread or not at all -- see
        // FrameworkElementEx and UnregisterTemplateEvents.
        winrt::event_token m_lostFocusToken{};
        winrt::event_token m_sizeChangedToken{};
        winrt::event_token m_contextMenuOpeningToken{};
        winrt::event_token m_layoutUpdatedToken{};
        winrt::event_token m_effectiveViewportChangedToken{};

        uint64_t m_expandSelectionDeadline{ 0 };

        void UnregisterLayoutChanged();
        void UnregisterTemplateEvents();

        void HandleLostFocus(const IInspectable&, const RoutedEventArgs&);
        void HandleSizeChanged(const IInspectable&, const SizeChangedEventArgs&);
        void HandleContextMenuOpening(const IInspectable&, const ContextMenuEventArgs& args);
        void HandleDoubleTapped(const IInspectable&, const DoubleTappedRoutedEventArgs& args);
        void HandleTapped(const IInspectable&, const TappedRoutedEventArgs& args);
        void HandleLayoutUpdated(const IInspectable&, const IInspectable&);
        void HandleEffectiveViewportChanged(FrameworkElement const& sender, EffectiveViewportChangedEventArgs const& e);

        void ExpandSelection();
    };
}

namespace winrt::Telegram::Native::Controls::factory_implementation
{
    struct FormattedTextBlockBase : FormattedTextBlockBaseT<FormattedTextBlockBase, implementation::FormattedTextBlockBase>
    {
    };
}
