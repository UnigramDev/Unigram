#pragma once

#include "Controls/AutomaticDragHelper.g.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Input.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/Windows.UI.Xaml.Input.h>

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::UI::Input;
using namespace winrt::Windows::UI::Xaml;
using namespace winrt::Windows::UI::Xaml::Controls;
using namespace winrt::Windows::UI::Xaml::Media;
using namespace winrt::Windows::UI::Xaml::Input;

namespace winrt::Telegram::Native::Controls::implementation
{
    struct RoutedEventHandler_revoker
    {
        RoutedEventHandler_revoker() noexcept = default;
        RoutedEventHandler_revoker(RoutedEventHandler_revoker const&) = delete;
        RoutedEventHandler_revoker& operator=(RoutedEventHandler_revoker const&) = delete;
        RoutedEventHandler_revoker(RoutedEventHandler_revoker&& other) noexcept
        {
            move_from(other);
        }

        RoutedEventHandler_revoker& operator=(RoutedEventHandler_revoker&& other) noexcept
        {
            move_from(other);
            return *this;
        }

        RoutedEventHandler_revoker(UIElement const& object, RoutedEvent event, winrt::Windows::Foundation::IInspectable handler) :
            m_object(object),
            m_event(std::move(event)),
            m_handler(std::move(handler))
        {
        }

        ~RoutedEventHandler_revoker() noexcept
        {
            revoke();
        }

        void revoke() noexcept
        {
            if (!m_object)
            {
                return;
            }

            if (auto object = m_object.get())
            {
                object.RemoveHandler(m_event, m_handler);
            }

            m_object = nullptr;
        }

        explicit operator bool() const noexcept
        {
            return static_cast<bool>(m_object);
        }
    private:
        void move_from(RoutedEventHandler_revoker& other)
        {
            if (this != &other)
            {
                revoke();
                std::swap(m_object, other.m_object);
                std::swap(m_event, other.m_event);
                std::swap(m_handler, other.m_handler);
            }
        }

        winrt::weak_ref<UIElement> m_object;
        RoutedEvent m_event{ nullptr };
        winrt::Windows::Foundation::IInspectable m_handler{};
    };

    // Enum to help with type traits
    enum class RoutedEventType
    {
        GettingFocus,
        LosingFocus,
        KeyDown,
        PointerMoved,
        PointerPressed,
        PointerReleased,
        PointerExited,
        PointerCanceled,
        PointerCaptureLost
    };

    template<RoutedEventType eventType>
    struct RoutedEventTraits
    {
    };

    template <>
    struct RoutedEventTraits<RoutedEventType::GettingFocus>
    {
        static RoutedEvent Event() { return UIElement::GettingFocusEvent(); }
        using HandlerT = TypedEventHandler<UIElement, GettingFocusEventArgs>;
    };

    template <>
    struct RoutedEventTraits<RoutedEventType::LosingFocus>
    {
        static RoutedEvent Event() { return UIElement::LosingFocusEvent(); }
        using HandlerT = TypedEventHandler<UIElement, LosingFocusEventArgs>;
    };

    template <>
    struct RoutedEventTraits<RoutedEventType::KeyDown>
    {
        static RoutedEvent Event() { return UIElement::KeyDownEvent(); }
        using HandlerT = KeyEventHandler;
    };

    template <>
    struct RoutedEventTraits<RoutedEventType::PointerPressed>
    {
        static RoutedEvent Event() { return UIElement::PointerPressedEvent(); }
        using HandlerT = PointerEventHandler;
    };

    template <>
    struct RoutedEventTraits<RoutedEventType::PointerMoved>
    {
        static RoutedEvent Event() { return UIElement::PointerMovedEvent(); }
        using HandlerT = PointerEventHandler;
    };

    template <>
    struct RoutedEventTraits<RoutedEventType::PointerReleased>
    {
        static RoutedEvent Event() { return UIElement::PointerReleasedEvent(); }
        using HandlerT = PointerEventHandler;
    };

    template <>
    struct RoutedEventTraits<RoutedEventType::PointerExited>
    {
        static RoutedEvent Event() { return UIElement::PointerExitedEvent(); }
        using HandlerT = PointerEventHandler;
    };

    template <>
    struct RoutedEventTraits<RoutedEventType::PointerCanceled>
    {
        static RoutedEvent Event() { return UIElement::PointerCanceledEvent(); }
        using HandlerT = PointerEventHandler;
    };

    template <>
    struct RoutedEventTraits<RoutedEventType::PointerCaptureLost>
    {
        static RoutedEvent Event() { return UIElement::PointerCaptureLostEvent(); }
        using HandlerT = PointerEventHandler;
    };

    template<RoutedEventType eventType, typename traits = RoutedEventTraits<eventType>>
    inline RoutedEventHandler_revoker AddRoutedEventHandler(UIElement const& object, typename traits::HandlerT const& callback, bool handledEventsToo)
    {
        auto handler = winrt::box_value<typename traits::HandlerT>(callback);
        auto event = traits::Event();
        object.AddHandler(event, handler, handledEventsToo);
        return { object, event, handler };
    }

    struct AutomaticDragHelper : AutomaticDragHelperT<AutomaticDragHelper>
    {
        AutomaticDragHelper(const UIElement& pUIElement, bool shouldAddInputHandlers);

        void StartDetectingDrag();
        void StopDetectingDrag();

    private:
        // The standard Windows mouse drag box size is defined by SM_CXDRAG and SM_CYDRAG.
        // UIElement uses the standard box size with dimensions multiplied by this constant.
        // This arrangement is in place as accidentally triggering a drag was deemed too easy while
        // selecting several items with the mouse in quick succession.
        const double UIELEMENT_MOUSE_DRAG_THRESHOLD_MULTIPLIER = 2.0;

        UIElement m_pOwnerNoRef;
        bool m_shouldAddInputHandlers = false;

        bool m_isCheckingForMouseDrag = false;
        Point m_lastMouseLeftButtonDownPosition{};

        bool m_isLeftButtonPressed = false;

        RoutedEventHandler_revoker m_dragDropPointerPressedToken{};
        RoutedEventHandler_revoker m_dragDropPointerMovedToken{};
        RoutedEventHandler_revoker m_dragDropPointerReleasedToken{};
        RoutedEventHandler_revoker m_dragDropPointerCaptureLostToken{};
        RoutedEventHandler_revoker m_dragDropHoldingToken{};

        // Begin tracking the mouse cursor in order to fire a drag start if the pointer
        // moves a certain distance away from m_lastMouseLeftButtonDownPosition.
        void BeginCheckingForMouseDrag(const Pointer& pPointer);

        // Stop tracking the mouse cursor.
        void StopCheckingForMouseDrag(const Pointer& pPointer);

        // Return true if we're tracking the mouse and newMousePosition is outside the drag
        // rectangle centered at m_lastMouseLeftButtonDownPosition (see IsOutsideDragRectangle).
        bool ShouldStartMouseDrag(Point newMousePosition);

        // Returns true if testPoint is outside of the rectangle
        // defined by the SM_CXDRAG and SM_CYDRAG system metrics and
        // dragRectangleCenter.
        bool IsOutsideDragRectangle(Point testPoint, Point dragRectangleCenter);

        void RegisterDragPointerEvents();

        void HandlePointerPressedEventArgs(const IInspectable& sender, const PointerRoutedEventArgs& args);

        void HandlePointerMovedEventArgs(const IInspectable& sender, const PointerRoutedEventArgs& args);

        void HandlePointerReleasedEventArgs(const IInspectable& sender, const PointerRoutedEventArgs& args);

        void HandlePointerCaptureLostEventArgs(const IInspectable& sender, const PointerRoutedEventArgs& args);

        void UnregisterEvents();
    };
}

namespace winrt::Telegram::Native::Controls::factory_implementation
{
    struct AutomaticDragHelper : AutomaticDragHelperT<AutomaticDragHelper, implementation::AutomaticDragHelper>
    {
    };
}
