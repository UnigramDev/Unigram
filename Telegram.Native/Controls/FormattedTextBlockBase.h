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
        FrameworkElement::LostFocus_revoker m_focusLostRevoker{};
        FrameworkElement::SizeChanged_revoker m_sizeChangedRevoker{};
        RichTextBlock::ContextMenuOpening_revoker m_contextMenuOpeningRevoker{};
        FrameworkElement::LayoutUpdated_revoker m_layoutUpdatedRevoker{};
        FrameworkElement::EffectiveViewportChanged_revoker m_effectiveViewportChangedRevoker{};

        uint64_t m_expandSelectionDeadline;

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
