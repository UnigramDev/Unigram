#include "pch.h"
#include "FormattedTextBlockBase.h"
#if __has_include("Controls/FormattedTextBlockBase.g.cpp")
#include "Controls/FormattedTextBlockBase.g.cpp"
#endif

#include <functional>

#include <winrt/Windows.Devices.Input.h>
#include <winrt/Windows.UI.Xaml.Documents.h>

using namespace winrt::Windows::Devices::Input;
using namespace winrt::Windows::UI::Xaml::Documents;

namespace winrt::Telegram::Native::Controls::implementation
{
    void FormattedTextBlockBase::OnApplyTemplate()
    {
        __super::OnApplyTemplate();

        // The outgoing child is dropped here, so this is the only place that has to unhook from
        // it -- and the only one that can, being on the UI thread. See FrameworkElementEx.
        UnregisterTemplateEvents();

        if (auto textBlock = GetTemplateChild(L"TextBlock"))
        {
            m_textBlock = textBlock.as<RichTextBlock>();
            m_lostFocusToken = m_textBlock.LostFocus({ this, &FormattedTextBlockBase::HandleLostFocus });
            m_sizeChangedToken = m_textBlock.SizeChanged({ this, &FormattedTextBlockBase::HandleSizeChanged });
            m_contextMenuOpeningToken = m_textBlock.ContextMenuOpening({ this, &FormattedTextBlockBase::HandleContextMenuOpening });

            m_textBlock.AddHandler(UIElement::DoubleTappedEvent(), winrt::box_value(DoubleTappedEventHandler({ this, &FormattedTextBlockBase::HandleDoubleTapped })), true);
            m_textBlock.AddHandler(UIElement::TappedEvent(), winrt::box_value(TappedEventHandler({ this, &FormattedTextBlockBase::HandleTapped })), true);
        }
        else
        {
            __debugbreak();
        }
    }

    void FormattedTextBlockBase::RegisterLayoutChanged()
    {
        if (!m_layoutUpdatedToken)
        {
            m_layoutUpdatedToken = m_textBlock.LayoutUpdated({ this, &FormattedTextBlockBase::HandleLayoutUpdated });
        }
    }

    void FormattedTextBlockBase::UnregisterLayoutChanged()
    {
        if (m_layoutUpdatedToken)
        {
            m_textBlock.LayoutUpdated(m_layoutUpdatedToken);
            m_layoutUpdatedToken = {};
        }
    }

    void FormattedTextBlockBase::RegisterViewportChanged()
    {
        if (!m_effectiveViewportChangedToken)
        {
            m_effectiveViewportChangedToken = m_textBlock.EffectiveViewportChanged({ this, &FormattedTextBlockBase::HandleEffectiveViewportChanged });
        }
    }

    void FormattedTextBlockBase::UnregisterViewportChanged()
    {
        if (m_effectiveViewportChangedToken)
        {
            m_textBlock.EffectiveViewportChanged(m_effectiveViewportChangedToken);
            m_effectiveViewportChangedToken = {};
        }
    }

    void FormattedTextBlockBase::UnregisterTemplateEvents()
    {
        if (m_textBlock == nullptr)
        {
            return;
        }

        if (m_lostFocusToken)
        {
            m_textBlock.LostFocus(m_lostFocusToken);
            m_lostFocusToken = {};
        }

        if (m_sizeChangedToken)
        {
            m_textBlock.SizeChanged(m_sizeChangedToken);
            m_sizeChangedToken = {};
        }

        if (m_contextMenuOpeningToken)
        {
            m_textBlock.ContextMenuOpening(m_contextMenuOpeningToken);
            m_contextMenuOpeningToken = {};
        }

        UnregisterLayoutChanged();
        UnregisterViewportChanged();
    }

    void FormattedTextBlockBase::HandleLostFocus(const IInspectable&, const RoutedEventArgs&)
    {
        try
        {
            m_textBlock.Select(m_textBlock.ContentStart(), m_textBlock.ContentStart());
        }
        catch (...)
        {
            // All the remote procedure calls must be wrapped in a try-catch block
        }
    }

    void FormattedTextBlockBase::HandleSizeChanged(const IInspectable&, const SizeChangedEventArgs&)
    {
        UnregisterLayoutChanged();
        overridable().OnLayoutUpdated();
    }

    void FormattedTextBlockBase::HandleContextMenuOpening(const IInspectable&, const ContextMenuEventArgs& args)
    {
        args.Handled(true);
    }

    static inline uint32_t DoubleClickTime()
    {
        static UISettings s_settings;
        return s_settings.DoubleClickTime();
    }

    static inline void QueueCallbackForCompositionRendering(std::function<void()> callback)
    {
        try
        {
            auto renderingEventToken = std::make_shared<winrt::event_token>();
            *renderingEventToken = CompositionTarget::Rendering([renderingEventToken, callback](auto&, auto&) {

                // Detach event or Rendering will keep calling us back.
                CompositionTarget::Rendering(*renderingEventToken);

                callback();
                });
        }
        catch (const winrt::hresult_error& e)
        {
            // DirectUI::CompositionTarget::add_Rendering can fail with RPC_E_WRONG_THREAD if called while the Xaml Core is being shutdown,
            // and there is evidence from Watson that such calls are made in real apps (see Bug 13554197).
            // Since the core is being shutdown, we no longer care about whatever work we wanted to defer to CT.Rendering, so ignore this error.
            if (e.to_abi() != RPC_E_WRONG_THREAD) { throw; }
        }
    }

    void FormattedTextBlockBase::HandleDoubleTapped(const IInspectable&, const DoubleTappedRoutedEventArgs& args)
    {
        if (args.PointerDeviceType() == PointerDeviceType::Mouse)
        {
            m_expandSelectionDeadline = GetTickCount64() + DoubleClickTime();
        }

    }

    void FormattedTextBlockBase::HandleTapped(const IInspectable&, const TappedRoutedEventArgs& args)
    {
        // If a double tap is followed by a single tap, then it's a triple tap (duh)
        if (args.PointerDeviceType() == PointerDeviceType::Mouse && GetTickCount64() < m_expandSelectionDeadline)
        {
            m_expandSelectionDeadline = GetTickCount64() + DoubleClickTime();
            QueueCallbackForCompositionRendering(
                [strongThis = get_strong()]
                {
                    strongThis->ExpandSelection();
                }
            );
        }
    }

    static inline DependencyObject FindParent(DependencyObject obj)
    {
        if (obj == nullptr)
        {
            return nullptr;
        }

        if (obj.try_as<RichTextBlock>() || obj.try_as<Paragraph>())
        {
            return obj;
        }
        else if (auto element = obj.try_as<TextElement>())
        {
            return FindParent(element.ElementStart().Parent());
        }
        return nullptr;
    }

    void FormattedTextBlockBase::ExpandSelection()
    {
        if (m_textBlock.SelectionStart() != nullptr && m_textBlock.SelectionEnd() != nullptr)
        {
            if (m_textBlock.SelectionStart() != nullptr && m_textBlock.SelectionEnd() != nullptr)
            {
                auto startBlock = FindParent(m_textBlock.SelectionStart().Parent());
                auto endBlock = FindParent(m_textBlock.SelectionEnd().Parent());

                if (startBlock == endBlock && startBlock != nullptr)
                {
                    try
                    {
                        if (auto element = startBlock.try_as<TextElement>())
                        {
                            m_textBlock.Select(element.ContentStart(), element.ContentEnd());
                        }
                        else if (auto block = startBlock.try_as<RichTextBlock>())
                        {
                            m_textBlock.Select(block.ContentStart(), block.ContentEnd());
                        }
                    }
                    catch (...)
                    {
                        // All the remote procedure calls must be wrapped in a try-catch block
                    }
                }
            }
        }
    }

    void FormattedTextBlockBase::HandleLayoutUpdated(const IInspectable&, const IInspectable&)
    {
        UnregisterLayoutChanged();
        overridable().OnLayoutUpdated();
    }

    void FormattedTextBlockBase::HandleEffectiveViewportChanged(FrameworkElement const& sender, EffectiveViewportChangedEventArgs const& e)
    {
        double viewportRight = e.EffectiveViewport().X + e.EffectiveViewport().Width;
        double viewportBottom = e.EffectiveViewport().Y + e.EffectiveViewport().Height;

        overridable().OnViewportChanged(e.EffectiveViewport().X, e.EffectiveViewport().Y, viewportRight, viewportBottom);
    }
}
