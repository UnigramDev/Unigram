#pragma once

#include "WidgetView.g.h"

#include "SurfaceRenderer.h"
#include "WidgetTypes.h"
#include "twidget/widget_host.h"

namespace winrt::Telegram::Native::Widgets::implementation
{
    struct WidgetView : WidgetViewT<WidgetView>
    {
        WidgetView(UIComposition::Compositor const& compositor, array_view<uint8_t const> html, Telegram::Native::Widgets::WidgetTheme const& theme, Telegram::Native::Widgets::WidgetOptions const& options);
        ~WidgetView();

        static void LoadResources(hstring const& directory);

        UIComposition::Visual Visual() const { return m_visual; }
        double ContentHeight() const { return m_height; }
        bool KeepsBottom() const { return m_keepsBottom; }
        bool IsLoaded() const { return m_loaded; }
        bool IsDead() const { return m_dead; }
        hstring Error();
        hstring Summary();
        bool WatchesPublicData() const { return m_watchesPublicData; }

        void Start();
        void SetTheme(Telegram::Native::Widgets::WidgetTheme const& theme);
        void SetWidth(double width);
        void SetVisible(bool visible);
        void SetData(hstring const& chatData, hstring const& publicData);
        void SetContentSafeArea(Telegram::Native::Widgets::WidgetContentSafeArea const& area);
        int32_t Pointer(int32_t action, double x, double y, int32_t pointerId, bool primary, bool mouse, int32_t modifiers, int32_t button);
        void Hover(int32_t action, double x, double y, int32_t modifiers);
        hstring SelectedText();
        bool ClearSelection();
        void Close();

        event_token Changed(Windows::Foundation::TypedEventHandler<Telegram::Native::Widgets::WidgetView, Windows::Foundation::IInspectable> const& handler);
        void Changed(event_token const& token) noexcept;
        event_token ActionRequested(Windows::Foundation::TypedEventHandler<Telegram::Native::Widgets::WidgetView, Telegram::Native::Widgets::WidgetAction> const& handler);
        void ActionRequested(event_token const& token) noexcept;
        event_token StorageCommitted(Windows::Foundation::TypedEventHandler<Telegram::Native::Widgets::WidgetView, Telegram::Native::Widgets::WidgetStorageCommit> const& handler);
        void StorageCommitted(event_token const& token) noexcept;
        event_token RequestReceived(Windows::Foundation::TypedEventHandler<Telegram::Native::Widgets::WidgetView, Telegram::Native::Widgets::WidgetRequest> const& handler);
        void RequestReceived(event_token const& token) noexcept;
        event_token CopyTextRequested(Windows::Foundation::TypedEventHandler<Telegram::Native::Widgets::WidgetView, hstring> const& handler);
        void CopyTextRequested(event_token const& token) noexcept;

    private:
        // What the host is built from, held from the constructor to Start().
        struct HostArgs
        {
            std::string html;
            twidget::WidgetTheme theme;
            double width = 0;
            twidget::WidgetHostOptions options;
            std::shared_ptr<twidget::SandboxResources> resources;
            std::string stoppedLabel;
            UIComposition::CompositionSurfaceBrush brush{ nullptr };
        };

        // On m_creator: building a host loads the validator's runtime and bundle and starts its threads,
        // about 10 ms that would otherwise land on the caller's thread, which is the UI thread.
        void Create(HostArgs args);
        void OnHostChanged(const twidget::WidgetDiagnostics& diagnostics);
        void OnAction(const twidget::WidgetAction& action);
        void OnStorageCommit(const std::string& area, const std::map<std::string, std::string>& values);

        UIComposition::SpriteVisual m_visual{ nullptr };
        // The thread that made the view, where frames of a new height go up.
        UIDispatching::DispatcherQueue m_ui{ nullptr };
        double m_dpr;
        std::unique_ptr<HostArgs> m_args;
        std::thread m_creator;

        // m_host, m_renderer and what was asked of them before they existed. Every call into them holds it,
        // so Close cannot destroy them in the middle of one.
        std::mutex m_lock;
        std::unique_ptr<twidget::WidgetHost> m_host;
        // Owned by m_host, built by its factory.
        ::Widgets::SurfaceRenderer* m_renderer = nullptr;
        bool m_closing = false;
        bool m_visible = true;
        std::optional<double> m_pendingWidth;
        std::optional<twidget::WidgetTheme> m_pendingTheme;
        std::optional<twidget::WidgetData> m_pendingData;
        std::optional<twidget::WidgetContentSafeArea> m_pendingSafeArea;

        std::atomic<double> m_height{ 0 };
        std::atomic<bool> m_keepsBottom{ false };
        std::atomic<bool> m_loaded{ false };
        std::atomic<bool> m_dead{ false };
        std::atomic<bool> m_watchesPublicData{ false };
        std::mutex m_errorLock;
        std::string m_error;

        event<Windows::Foundation::TypedEventHandler<Telegram::Native::Widgets::WidgetView, Windows::Foundation::IInspectable>> m_changed;
        event<Windows::Foundation::TypedEventHandler<Telegram::Native::Widgets::WidgetView, Telegram::Native::Widgets::WidgetAction>> m_actionRequested;
        event<Windows::Foundation::TypedEventHandler<Telegram::Native::Widgets::WidgetView, Telegram::Native::Widgets::WidgetStorageCommit>> m_storageCommitted;
        event<Windows::Foundation::TypedEventHandler<Telegram::Native::Widgets::WidgetView, Telegram::Native::Widgets::WidgetRequest>> m_requestReceived;
        event<Windows::Foundation::TypedEventHandler<Telegram::Native::Widgets::WidgetView, hstring>> m_copyTextRequested;
    };
}

namespace winrt::Telegram::Native::Widgets::factory_implementation
{
    struct WidgetView : WidgetViewT<WidgetView, implementation::WidgetView>
    {
    };
}
