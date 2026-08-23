#include "pch.h"
#include "AnimatedImageBase.h"
#if __has_include("Controls/AnimatedImageBase.g.cpp")
#include "Controls/AnimatedImageBase.g.cpp"
#endif

namespace winrt::Telegram::Native::Controls::implementation
{
    AnimatedImageBase::AnimatedImageBase()
    {
        m_sizeChangedRevoker = SizeChanged(winrt::auto_revoke, { this, &AnimatedImageBase::HandleSizeChanged });
    }

    void AnimatedImageBase::OnLoaded()
    {
        if (auto xamlRoot = XamlRoot())
        {
            m_rasterizationScale = xamlRoot.RasterizationScale();

            // Weak, unlike the revokers whose source is this element: the XamlRoot lives as long as
            // the window, so a subscription that outlives the control is raised against an object
            // that is no longer there - overridable() then dereferences an outer that is gone, and
            // a window visibility change is enough to do it.
            m_xamlRootChangedRevoker = xamlRoot.Changed(winrt::auto_revoke, { get_weak(), &AnimatedImageBase::HandleXamlRootChanged });
        }
    }

    void AnimatedImageBase::OnUnloaded()
    {
        if (m_xamlRootChangedRevoker)
        {
            m_xamlRootChangedRevoker.revoke();
        }
    }

    void AnimatedImageBase::RegisterViewportChanged()
    {
        if (!m_effectiveViewportChangedRevoker)
        {
            m_effectiveViewportChangedRevoker = EffectiveViewportChanged(winrt::auto_revoke, { this, &AnimatedImageBase::HandleEffectiveViewportChanged });
        }
    }

    void AnimatedImageBase::UnregisterViewportChanged()
    {
        if (m_effectiveViewportChangedRevoker)
        {
            m_effectiveViewportChangedRevoker.revoke();
        }
    }

    void AnimatedImageBase::HandleSizeChanged(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::SizeChangedEventArgs const& e)
    {
        overridable().OnSizeChanged(e);
    }

    void AnimatedImageBase::HandleXamlRootChanged(winrt::Windows::UI::Xaml::XamlRoot const& sender, winrt::Windows::UI::Xaml::XamlRootChangedEventArgs const& e)
    {
        auto rasterizationScale = sender.RasterizationScale();
        if (rasterizationScale != m_rasterizationScale)
        {
            m_rasterizationScale = rasterizationScale;
            overridable().OnRasterizationScaleChanged(rasterizationScale);
        }
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
