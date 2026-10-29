#pragma once

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

    // Connected means in the live tree, and nothing less. Parent and VisualTreeHelper.GetParent
    // return null on every element of a subtree whose ancestor has left the live tree, not only on
    // the element that was removed: a template child of a pooled control, the content of a detached
    // frame, anything nested in them. Each of them gets Unloaded, the probe reads null, OnUnloaded
    // runs and IsConnected is false. Nothing under a detached ancestor is still connected because it
    // has a parent of its own.
    bool IsConnected() const noexcept { return m_loaded; }
    bool IsDisconnected() const noexcept { return m_unloaded; }

    virtual void OnLoaded() {}
    virtual void OnUnloaded() {}

private:
    bool m_loaded{ false };
    bool m_unloaded{ false };

    void HandleChanged(winrt::Windows::Foundation::IInspectable const&, winrt::UIXaml::RoutedEventArgs const&)
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

    winrt::UIXaml::DependencyObject GetParent()
    {
        try
        {
            // element.Parent seems to throw E_FAIL at times
            if (auto parent = this->Parent())
            {
                return parent;
            }

            return winrt::UIXaml::Media::VisualTreeHelper::GetParent(*this);
        }
        catch (...)
        {
            return nullptr;
        }
    }
};
