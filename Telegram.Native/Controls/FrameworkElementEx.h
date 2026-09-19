#pragma once

#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Media.h>

template <typename TDerived, typename TBase>
struct FrameworkElementEx : TBase
{
    using Super = TBase;
    using This = TDerived;

    FrameworkElementEx()
    {
        // Never unhooked, and a revoker must not be used here: the event source is this very
        // object, so the handlers die with it, while ~FrameworkElementEx runs on the finalizer
        // thread whenever a managed subclass owns the outer object -- and XAML's remove_* is
        // thread-affine, so revoking there fails with RPC_E_WRONG_THREAD and fail-fasts.
        this->Loaded({ this, &FrameworkElementEx::HandleChanged });
        this->Unloaded({ this, &FrameworkElementEx::HandleChanged });
    }

    bool IsConnected() const noexcept { return m_loaded; }
    bool IsDisconnected() const noexcept { return m_unloaded; }

    virtual void OnLoaded() {}
    virtual void OnUnloaded() {}

private:
    bool m_loaded{ false };
    bool m_unloaded{ false };

    void HandleChanged(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        // TODO: unfortunately FrameworkElement.Parent returns null
        // whenever the control is a DataTemplate root or similar,
        // hence we're forced to use VisualTreeHelper here, but I'm quite sure it's slower.

        auto parent = GetParent();
        if (parent != nullptr && !m_loaded)
        {
            m_loaded = true;
            m_unloaded = false;
            this->overridable().OnLoaded();
        }
        else if (parent == nullptr && m_loaded)
        {
            m_loaded = false;
            m_unloaded = true;
            this->overridable().OnUnloaded();
        }
    }

    winrt::Windows::UI::Xaml::DependencyObject GetParent()
    {
        try
        {
            // element.Parent seems to throw E_FAIL at times
            if (auto parent = this->Parent())
            {
                return parent;
            }

            return winrt::Windows::UI::Xaml::Media::VisualTreeHelper::GetParent(*this);
        }
        catch (...)
        {
            return nullptr;
        }
    }
};
