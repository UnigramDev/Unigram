#include "pch.h"
#include "WidgetView.h"
#if __has_include("WidgetView.g.cpp")
#include "WidgetView.g.cpp"
#endif

#include "GraphicsDevice.h"
#include "twidget/icu_intl.h"
#include "twidget/resources.h"

#include <format>

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::GraphicsDirectX;
using namespace winrt::UIComposition;

namespace
{
    std::mutex s_resourcesLock;

    // Hosts are built one at a time. A list realizes several widgets at once, and building them side by
    // side made each boot slow enough for the sandbox to kill it on its query and wall-clock limits.
    std::mutex s_createLock;
    std::shared_ptr<twidget::SandboxResources> s_resources;

    // A handler that throws must not take the host thread down with it.
    template <class Event, class... Args>
    void Raise(Event& event, Args&&... args)
    {
        try
        {
            event(std::forward<Args>(args)...);
        }
        catch (...)
        {
        }
    }
}

namespace winrt::Telegram::Native::Widgets::implementation
{
    void WidgetView::LoadResources(hstring const& directory)
    {
        auto resources = twidget::SandboxResources::fromDirectory(to_string(directory));
        std::lock_guard guard(s_resourcesLock);
        s_resources = std::move(resources);
    }

    WidgetView::WidgetView(Compositor const& compositor, array_view<uint8_t const> html, Telegram::Native::Widgets::WidgetTheme const& theme, Telegram::Native::Widgets::WidgetOptions const& options)
        : m_ui(UIDispatching::DispatcherQueue::GetForCurrentThread())
        , m_dpr(options.DevicePixelRatio())
    {
        std::shared_ptr<twidget::SandboxResources> resources;
        {
            std::lock_guard guard(s_resourcesLock);
            resources = s_resources;
        }

        if (!resources)
        {
            throw hresult_illegal_method_call(L"LoadResources first");
        }

        auto device = ::Widgets::GraphicsDevice::Current().CompositionDevice(compositor);
        auto surface = device.CreateDrawingSurface({ 1, 1 }, DirectXPixelFormat::B8G8R8A8UIntNormalized, DirectXAlphaMode::Premultiplied);

        // The surface is in physical px and grows with the document; the brush maps it back to DIPs
        // from the top left corner instead of stretching it into whatever size the visual has.
        auto brush = compositor.CreateSurfaceBrush(surface);
        brush.Stretch(CompositionStretch::None);
        brush.HorizontalAlignmentRatio(0);
        brush.VerticalAlignmentRatio(0);
        brush.Scale({ float(1 / m_dpr), float(1 / m_dpr) });

        m_visual = compositor.CreateSpriteVisual();
        m_visual.Brush(brush);
        m_visual.RelativeSizeAdjustment({ 1, 1 });
        // Content past the bubble's height is hard-clipped, compositor layers included (docs/sdk-v1.md §5.6).
        m_visual.Clip(compositor.CreateInsetClip());

        m_args = std::make_unique<HostArgs>();
        m_args->html.assign(reinterpret_cast<const char*>(html.data()), html.size());
        m_args->theme = get_self<WidgetTheme>(theme)->ToHost();
        m_args->width = options.Width();
        m_args->resources = std::move(resources);
        m_args->stoppedLabel = to_string(options.StoppedLabel());
        m_args->brush = brush;

        auto& hostOptions = m_args->options;
        hostOptions.devicePixelRatio = m_dpr;
        // What profile 1 sets on every platform (docs/integration.md, "Policy and features").
        hostOptions.features = { "webAnimations" };
        if (!options.Locale().empty()) hostOptions.locale = to_string(options.Locale());
        hostOptions.storage.local = ToStorageArea(options.LocalStorage());
        hostOptions.storage.session = ToStorageArea(options.SessionStorage());
        hostOptions.contentSafeArea = ToContentSafeArea(options.ContentSafeArea());

        auto self = get_self<WidgetOptions>(options);
        hostOptions.data = self->ToData();
        hostOptions.context = self->ToContext();
    }

    WidgetView::~WidgetView()
    {
        Close();
    }

    void WidgetView::Create(HostArgs args)
    {
        auto brush = args.brush;
        auto factory = [this, brush](std::shared_ptr<twidget::SandboxResources>, const std::string& scheme, double width) -> std::unique_ptr<twidget::Renderer>
        {
            auto renderer = std::make_unique<::Widgets::SurfaceRenderer>(scheme, width, m_dpr, brush, ContainerVisual(m_visual));
            if (m_ui)
            {
                renderer->postToUi = [queue = m_ui](std::function<void()> install)
                {
                    return queue.TryEnqueue(UIDispatching::DispatcherQueuePriority::High, [install = std::move(install)] { install(); });
                };
            }
            renderer->onPresented = [this](double height, bool keepsBottom)
            {
                m_keepsBottom = keepsBottom;
                m_height = height;
                Raise(m_changed, *this, nullptr);
            };
            renderer->onSettled = [this]
            {
                m_loaded = true;
                Raise(m_changed, *this, nullptr);
            };
            m_renderer = renderer.get();
            return renderer;
        };

        std::unique_lock create(s_createLock);
        {
            // Closed while waiting for its turn: nothing to build.
            std::lock_guard guard(m_lock);
            if (m_closing)
            {
                return;
            }
        }

        std::unique_ptr<twidget::WidgetHost> host;
        try
        {
            // Intl from the system's ICU, in the chat's language when the widget names none.
            auto intl = std::make_shared<twidget::IcuIntl>(args.options.locale.empty() ? std::string("en-US") : args.options.locale);
            host = std::make_unique<twidget::WidgetHost>(std::move(args.html), std::move(args.theme), args.width, args.options, args.resources, factory, std::move(intl));
        }
        catch (const std::exception& e)
        {
            // Reported as a death: the bubble shows the stopped state, as for a widget that crashed.
            {
                std::lock_guard guard(m_errorLock);
                m_error = std::string("dead: ") + e.what();
            }
            m_dead = true;
            Raise(m_changed, *this, nullptr);
            return;
        }

        if (!args.stoppedLabel.empty())
        {
            host->stoppedLabel = args.stoppedLabel;
        }

        // The anchor is in the diagnostics before the size report of the same task's ops (WidgetDiagnostics::resizeAnchor),
        // so the renderer has it when it paints that height. The renderer lives as long as the host and its callbacks.
        host->onChange = [this, renderer = m_renderer](const twidget::WidgetDiagnostics& diagnostics)
        {
            renderer->setKeepsBottom(diagnostics.resizeAnchor == "bottom");
            if (diagnostics.rendered)
            {
                renderer->settle();
            }
            OnHostChanged(diagnostics);
        };
        host->onAction = [this](const twidget::WidgetAction& action) { OnAction(action); };
        host->onStorageCommit = [this](const std::string& area, const std::map<std::string, std::string>& values) { OnStorageCommit(area, values); };
        host->onCopyText = [this](const std::string& text) { Raise(m_copyTextRequested, *this, to_hstring(text)); };
        host->onWatchPublicData = [this](bool on)
        {
            m_watchesPublicData = on;
            Raise(m_changed, *this, nullptr);
        };

        // Left unset, the host refuses every request as unsupported, which is right for a view nobody answers for.
        if (m_requestReceived)
        {
            host->onRequest = [this](const twidget::WidgetRequest& request, std::function<void(const twidget::WidgetRequestOutcome&)> respond)
            {
                Raise(m_requestReceived, *this, make<WidgetRequest>(to_hstring(request.data), std::move(respond)));
            };
        }

        std::unique_lock lock(m_lock);
        if (m_closing)
        {
            lock.unlock();
            host->destroy();
            return;
        }

        if (!m_visible)
        {
            host->setVisible(false);
            m_renderer->setVisible(false);
        }

        if (m_pendingTheme)
        {
            host->setTheme(*m_pendingTheme);
        }

        if (m_pendingData)
        {
            host->setData(*m_pendingData);
        }

        if (m_pendingSafeArea)
        {
            host->setContentSafeArea(*m_pendingSafeArea);
        }

        if (m_pendingWidth)
        {
            host->onHostThread([this, width = *m_pendingWidth] { m_renderer->setWidth(width); });
        }

        m_host = std::move(host);
        m_host->start();
    }

    void WidgetView::Close()
    {
        {
            std::lock_guard guard(m_lock);
            m_closing = true;
        }

        // A host still being built is destroyed by Create itself once it sees m_closing.
        if (m_creator.joinable())
        {
            m_creator.join();
        }

        std::unique_ptr<twidget::WidgetHost> host;
        {
            std::lock_guard guard(m_lock);
            host = std::move(m_host);
            m_renderer = nullptr;
        }

        if (host)
        {
            // destroy() joins the host, engine and clock threads, so nothing calls back after it.
            host->destroy();
        }
    }

    // The height is the renderer's to report, once its frame is up (onPresented).
    void WidgetView::OnHostChanged(const twidget::WidgetDiagnostics& d)
    {
        bool changed = false;
        if (d.dead.has_value() != m_dead)
        {
            m_dead = d.dead.has_value();
            changed = true;
        }

        std::string error = d.dead ? "dead: " + *d.dead : d.lastError.value_or("");
        {
            std::lock_guard guard(m_errorLock);
            if (error != m_error)
            {
                m_error = std::move(error);
                changed = true;
            }
        }

        if (changed)
        {
            Raise(m_changed, *this, nullptr);
        }
    }

    void WidgetView::OnAction(const twidget::WidgetAction& action)
    {
        Telegram::Native::Widgets::WidgetAction args{};
        switch (action.kind)
        {
        case twidget::WidgetAction::OpenLink: args.Kind = WidgetActionKind::OpenLink; break;
        case twidget::WidgetAction::OpenTelegramLink: args.Kind = WidgetActionKind::OpenTelegramLink; break;
        case twidget::WidgetAction::Haptic: args.Kind = WidgetActionKind::Haptic; break;
        default: args.Kind = WidgetActionKind::Ready; break;
        }

        args.Url = to_hstring(action.url);
        args.HapticKind = to_hstring(action.hapticKind);
        args.HapticValue = to_hstring(action.hapticValue);
        Raise(m_actionRequested, *this, args);
    }

    void WidgetView::OnStorageCommit(const std::string& area, const std::map<std::string, std::string>& values)
    {
        // A lone surrogate arrives in its WTF-8 form, which UTF-16 cannot carry: it comes back as U+FFFD.
        auto map = single_threaded_map<hstring, hstring>();
        for (auto const& [key, value] : values)
        {
            map.Insert(to_hstring(key), to_hstring(value));
        }

        Raise(m_storageCommitted, *this, make<WidgetStorageCommit>(to_hstring(area), map.GetView()));
    }

    hstring WidgetView::Error()
    {
        std::lock_guard guard(m_errorLock);
        return to_hstring(m_error);
    }

    hstring WidgetView::Summary()
    {
        std::lock_guard guard(m_lock);
        if (!m_host)
        {
            return m_closing ? L"closed" : L"starting";
        }

        auto d = m_host->diagnostics();
        auto ms = [](const std::optional<double>& v) { return v ? std::format("{:.0f} ms", *v) : std::string("-"); };

        std::string text = std::format("loaded {} | content {} | h {:.0f} | js {:.1f} ms, {} tasks | ops {} | queries {} | mem {:.1f} MB",
            ms(d.loadedMs), ms(d.firstContentMs), d.height, d.stats.jsMs, d.stats.tasks, d.stats.ops, d.stats.queries, d.stats.memoryBytes / 1048576.0);

        if (auto renderer = m_renderer)
        {
            text += std::format(" | paints {} ({:.1f} ms) | build {:.1f} ms, patched {}/{} | layers {}", renderer->paints(), renderer->lastPaintMs(),
                renderer->lastBuildMs(), renderer->patchedBuilds(), renderer->builds(), renderer->layers());
        }

        if (d.stats.throttledTasks > 0)
        {
            text += std::format(" | throttled {}", d.stats.throttledTasks);
        }

        int errors = 0;
        for (auto const& log : d.logs)
        {
            if (log.first == "error") errors++;
        }

        if (errors > 0)
        {
            text += std::format(" | errors {}", errors);
        }

        for (const char* kind : { "unsupported", "url-blocked", "renderer-violation", "renderer", "bridge-rejected", "activation-blocked", "storage-rejected", "data-rejected", "request-blocked" })
        {
            auto it = d.diags.find(kind);
            if (it != d.diags.end())
            {
                auto const& [count, details] = it->second;
                text += std::format(" | {} {}: {}", kind, count, details.empty() ? "" : details.front().substr(0, 120));
            }
        }

        return to_hstring(text);
    }

    void WidgetView::Start()
    {
        if (m_args && !m_creator.joinable())
        {
            m_creator = std::thread([this, args = std::move(m_args)]() mutable { Create(std::move(*args)); });
        }
    }

    void WidgetView::SetTheme(Telegram::Native::Widgets::WidgetTheme const& theme)
    {
        auto value = get_self<WidgetTheme>(theme)->ToHost();

        std::lock_guard guard(m_lock);
        if (m_host) m_host->setTheme(value);
        else m_pendingTheme = std::move(value);
    }

    void WidgetView::SetWidth(double width)
    {
        std::lock_guard guard(m_lock);
        // WidgetHost has no width of its own: the renderer reports the new size to it as it would after a batch.
        if (m_host && m_renderer) m_host->onHostThread([this, width] { m_renderer->setWidth(width); });
        else m_pendingWidth = width;
    }

    void WidgetView::SetVisible(bool visible)
    {
        std::lock_guard guard(m_lock);
        m_visible = visible;
        if (m_host)
        {
            m_host->setVisible(visible);
            m_renderer->setVisible(visible);
        }
    }

    void WidgetView::SetData(hstring const& chatData, hstring const& publicData)
    {
        auto data = ToData(chatData, publicData);

        std::lock_guard guard(m_lock);
        if (m_host) m_host->setData(data);
        else m_pendingData = std::move(data);
    }

    void WidgetView::SetContentSafeArea(Telegram::Native::Widgets::WidgetContentSafeArea const& area)
    {
        auto value = ToContentSafeArea(area);

        std::lock_guard guard(m_lock);
        if (m_host) m_host->setContentSafeArea(value);
        else m_pendingSafeArea = value;
    }

    int32_t WidgetView::Pointer(int32_t action, double x, double y, int32_t pointerId, bool primary, bool mouse, int32_t modifiers, int32_t button)
    {
        std::lock_guard guard(m_lock);
        // The renderer decides the claim inside touch(); WidgetHost::onGestureClaim reports the same one later, on the host thread.
        return m_host && m_renderer ? m_renderer->touch(action, x, y, pointerId, primary, mouse, modifiers, button) : -1;
    }

    void WidgetView::Hover(int32_t action, double x, double y, int32_t modifiers)
    {
        std::lock_guard guard(m_lock);
        if (m_host && m_renderer) m_renderer->hover(action, x, y, modifiers);
    }

    hstring WidgetView::SelectedText()
    {
        std::lock_guard guard(m_lock);
        return m_renderer ? to_hstring(m_renderer->selectedText()) : hstring();
    }

    bool WidgetView::ClearSelection()
    {
        std::lock_guard guard(m_lock);
        return m_renderer && m_renderer->clearSelection();
    }

    event_token WidgetView::Changed(TypedEventHandler<Telegram::Native::Widgets::WidgetView, IInspectable> const& handler)
    {
        return m_changed.add(handler);
    }

    void WidgetView::Changed(event_token const& token) noexcept
    {
        m_changed.remove(token);
    }

    event_token WidgetView::ActionRequested(TypedEventHandler<Telegram::Native::Widgets::WidgetView, Telegram::Native::Widgets::WidgetAction> const& handler)
    {
        return m_actionRequested.add(handler);
    }

    void WidgetView::ActionRequested(event_token const& token) noexcept
    {
        m_actionRequested.remove(token);
    }

    event_token WidgetView::StorageCommitted(TypedEventHandler<Telegram::Native::Widgets::WidgetView, Telegram::Native::Widgets::WidgetStorageCommit> const& handler)
    {
        return m_storageCommitted.add(handler);
    }

    void WidgetView::StorageCommitted(event_token const& token) noexcept
    {
        m_storageCommitted.remove(token);
    }

    event_token WidgetView::RequestReceived(TypedEventHandler<Telegram::Native::Widgets::WidgetView, Telegram::Native::Widgets::WidgetRequest> const& handler)
    {
        return m_requestReceived.add(handler);
    }

    void WidgetView::RequestReceived(event_token const& token) noexcept
    {
        m_requestReceived.remove(token);
    }

    event_token WidgetView::CopyTextRequested(TypedEventHandler<Telegram::Native::Widgets::WidgetView, hstring> const& handler)
    {
        return m_copyTextRequested.add(handler);
    }

    void WidgetView::CopyTextRequested(event_token const& token) noexcept
    {
        m_copyTextRequested.remove(token);
    }
}
