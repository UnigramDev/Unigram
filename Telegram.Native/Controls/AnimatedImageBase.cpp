#include "pch.h"
#include "AnimatedImageBase.h"
#if __has_include("Controls/AnimatedImageBase.g.cpp")
#include "Controls/AnimatedImageBase.g.cpp"
#endif

namespace winrt::Telegram::Native::Controls::implementation
{
    AnimatedImageBase::AnimatedImageBase()
    {
        // Never unhooked, see FrameworkElementEx.
        SizeChanged({ this, &AnimatedImageBase::HandleSizeChanged });
    }

    void AnimatedImageBase::RegisterViewportChanged()
    {
        if (!m_effectiveViewportChangedToken)
        {
            m_effectiveViewportChangedToken = EffectiveViewportChanged({ this, &AnimatedImageBase::HandleEffectiveViewportChanged });
        }
    }

    void AnimatedImageBase::UnregisterViewportChanged()
    {
        if (m_effectiveViewportChangedToken)
        {
            EffectiveViewportChanged(m_effectiveViewportChangedToken);
            m_effectiveViewportChangedToken = {};
        }
    }

    void AnimatedImageBase::HandleSizeChanged(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::SizeChangedEventArgs const& e)
    {
        overridable().OnSizeChanged(e.PreviousSize(), e.NewSize());
    }

    void AnimatedImageBase::HandleEffectiveViewportChanged(FrameworkElement const& sender, EffectiveViewportChangedEventArgs const& e)
    {
        auto visible = e.BringIntoViewDistanceX() < sender.ActualWidth() && e.BringIntoViewDistanceY() < sender.ActualHeight();
        if (visible != m_visible)
        {
            m_visible = visible;
            overridable().OnViewportChanged(visible);
        }
    }
}
