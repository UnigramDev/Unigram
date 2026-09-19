#include "pch.h"
#include "DirectTextBlockBase.h"
#if __has_include("Controls/DirectTextBlockBase.g.cpp")
#include "Controls/DirectTextBlockBase.g.cpp"
#endif

namespace winrt::Telegram::Native::Controls::implementation
{
    void DirectTextBlockBase::RegisterViewportChanged()
    {
        if (!m_effectiveViewportChangedToken)
        {
            m_effectiveViewportChangedToken = EffectiveViewportChanged({ this, &DirectTextBlockBase::HandleEffectiveViewportChanged });
        }
    }

    void DirectTextBlockBase::UnregisterViewportChanged()
    {
        if (m_effectiveViewportChangedToken)
        {
            EffectiveViewportChanged(m_effectiveViewportChangedToken);
            m_effectiveViewportChangedToken = {};
        }
    }

    void DirectTextBlockBase::HandleEffectiveViewportChanged(FrameworkElement const& sender, EffectiveViewportChangedEventArgs const& args)
    {
        auto viewport = args.EffectiveViewport();

        overridable().OnViewportChanged(viewport.X, viewport.Y, viewport.X + viewport.Width, viewport.Y + viewport.Height);
    }
}
