#include "pch.h"
#include "SurfaceRenderer.h"

#include "GraphicsDevice.h"
#include "twidget/json.h"

#include <format>
#include <set>

using twidget::Json;
using namespace winrt::UIComposition;

namespace Widgets
{
    namespace
    {
        constexpr double kTickSeconds = 1.0 / 60;
        // A compositor clock that stops ticking holds a tick no longer than the system timer's rounding would.
        constexpr DWORD kClockTimeoutMs = 17;
        constexpr uint32_t kSurfaceStep = 128;  // physical px
        constexpr uint32_t kSurfaceSlack = 384;
        // How long a frame of a new height waits for the UI thread before it goes up from the clock thread.
        constexpr auto kPresentTimeout = std::chrono::milliseconds(50);
    }

    double SurfaceRenderer::nowMs()
    {
        return std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now().time_since_epoch()).count();
    }

    SurfaceRenderer::SurfaceRenderer(const std::string& theme, double width, double dpr, CompositionSurfaceBrush brush, ContainerVisual root)
        : m_dpr(dpr)
        , m_backend(GraphicsDevice::Current().D2DDevice(), GraphicsDevice::Current().DWrite(), GraphicsDevice::Current().Wic())
        , m_brush(std::move(brush))
        , m_surface(m_brush.Surface().as<CompositionDrawingSurface>())
        , m_root(std::move(root))
    {
        m_interop = m_surface.as<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop>();
        m_core = std::make_unique<twlite::Renderer>(width, dpr, theme == "dark", m_backend);
        if (m_root)
        {
            // A layer moves on its own and must not leave the bubble.
            m_root.Clip(m_root.Compositor().CreateInsetClip());
            m_core->set_compositing(true);
        }
        m_clock = std::thread([this] { clockLoop(); });
    }

    SurfaceRenderer::~SurfaceRenderer()
    {
        destroy();
    }

    void SurfaceRenderer::load()
    {
        if (onMessage) onMessage("{\"type\":\"ready\"}");
        schedulePaint();
    }

    void SurfaceRenderer::send(const std::string& json)
    {
        if (json.rfind("{\"type\":\"requestFrame\"", 0) == 0)
        {
            requestFrame();
            return;
        }

        if (json.rfind("{\"type\":\"frozen\"", 0) == 0)
        {
            std::lock_guard l(m_clockMutex);
            m_frozen = true;
        }

        auto msg = Json::parse(json);
        if (!msg)
        {
            if (onDiag) onDiag("renderer-violation", "bad message");
            return;
        }

        run([&](std::vector<Json>& out)
        {
            try
            {
                out = m_core->receive(*msg);
            }
            catch (const std::exception& e)
            {
                if (onDiag) onDiag("renderer", std::string("receive: ") + e.what());
            }
            m_lastBuildMs = m_core->stats().parse_ms;
            m_builds = m_core->stats().builds;
            m_patched = m_core->stats().patched;
        });
    }

    template <class Call> void SurfaceRenderer::run(Call call)
    {
        std::vector<Json> out;
        bool dirty, ticking;
        double wake;
        std::optional<twlite::Renderer::EditorSpec> editor;
        bool editorChanged;
        {
            std::lock_guard l(m_mutex);
            if (m_destroyed) return;
            call(out);
            dirty = m_core->dirty();
            ticking = m_core->wants_tick();
            wake = m_core->next_tick_in();
            editorChanged = takeEditor(editor);
        }

        if (editorChanged && onEditor) onEditor(std::move(editor));
        dispatch(std::move(out));
        setWake(wake);
        setTicking(ticking);
        if (dirty) schedulePaint();
    }

    bool SurfaceRenderer::takeEditor(std::optional<twlite::Renderer::EditorSpec>& spec)
    {
        const twlite::Renderer::EditorSpec* current = m_core->editor();
        uint64_t generation = current ? current->generation : 0;
        if (generation == m_editorGeneration) return false;
        m_editorGeneration = generation;
        if (current)
        {
            spec = *current;
            spec->font_family = winrt::to_string(m_backend.resolve_family(current->font_family));
        }
        return true;
    }

    void SurfaceRenderer::setWake(double seconds)
    {
        {
            std::lock_guard l(m_clockMutex);
            if (!std::isfinite(seconds))
            {
                m_wakeAt.reset();
                return;
            }
            // At least a frame away: a wake-up that is already due waits for the next tick anyway.
            m_wakeAt = std::chrono::steady_clock::now() + std::chrono::duration_cast<std::chrono::steady_clock::duration>(std::chrono::duration<double>(std::max(seconds, kTickSeconds)));
        }
        m_clockCv.notify_all();
    }

    void SurfaceRenderer::setTheme(const std::string& theme)
    {
        run([&](std::vector<Json>& out) { m_core->set_theme(theme == "dark", out); });
    }

    void SurfaceRenderer::setWidth(double width)
    {
        run([&](std::vector<Json>& out) { m_core->set_width(width, out); });
    }

    int SurfaceRenderer::touch(int action, double x, double y, int pointer, bool primary, bool mouse, int modifiers, int button)
    {
        int claim = -1;
        run([&](std::vector<Json>& out)
        {
            out = m_core->touch(action, x, y, pointer, primary, mouse, modifiers, button);
            for (const Json& msg : out)
            {
                if (msg["type"].asString() == "gesture") claim = (msg["x"].asBool() ? 1 : 0) | (msg["y"].asBool() ? 2 : 0) | (msg["hold"].asBool() ? 4 : 0);
            }
        });

        // The core's arbiter drops the claim there too, but its gesture message reports the element's. The renderer gets
        // no `arbitrate` message, so its leading edge is the left one.
        if (claim > 0 && !mouse && x < twidget::gesture::EDGE_STRIP_PX)
        {
            claim = 0;
        }

        return claim;
    }

    std::string SurfaceRenderer::selectedText()
    {
        std::lock_guard l(m_mutex);
        return m_destroyed ? std::string() : m_core->selected_text();
    }

    bool SurfaceRenderer::clearSelection()
    {
        bool had = false;
        run([&](std::vector<Json>&) { had = m_core->clear_selection(); });
        return had;
    }

    void SurfaceRenderer::hover(int action, double x, double y, int modifiers)
    {
        run([&](std::vector<Json>& out) { out = m_core->hover(action, x, y, modifiers); });
    }

    void SurfaceRenderer::wheel(double x, double y, double dx, double dy, int mode, int modifiers)
    {
        run([&](std::vector<Json>& out) { out = m_core->wheel(x, y, dx, dy, mode, modifiers); });
    }

    void SurfaceRenderer::key(const std::string& type, const std::string& key, const std::string& code, bool repeat, int modifiers)
    {
        run([&](std::vector<Json>& out) { out = m_core->key(type, key, code, repeat, modifiers); });
    }

    void SurfaceRenderer::edit(uint64_t generation, const std::string& text)
    {
        run([&](std::vector<Json>& out) { out = m_core->edit(generation, text); });
    }

    void SurfaceRenderer::endEditing()
    {
        run([&](std::vector<Json>& out) { out = m_core->end_editing(); });
    }

    void SurfaceRenderer::setVisible(bool visible)
    {
        {
            std::lock_guard l(m_clockMutex);
            m_visible = visible;
        }
        m_clockCv.notify_all();
    }

    void SurfaceRenderer::setTicking(bool ticking)
    {
        {
            std::lock_guard l(m_clockMutex);
            if (m_ticking == ticking) return;
            m_ticking = ticking;
        }
        if (ticking) m_clockCv.notify_all();
    }

    void SurfaceRenderer::destroy()
    {
        {
            std::lock_guard l(m_clockMutex);
            if (m_stopClock) return;
            m_stopClock = true;
        }

        SetEvent(m_stopEvent.get());
        m_clockCv.notify_all();
        if (m_clock.joinable()) m_clock.join();

        std::lock_guard l(m_mutex);
        m_destroyed = true;
        m_core.reset();
        for (auto& [id, layer] : m_layers)
        {
            if (layer.clipVisual) m_root.Children().Remove(layer.clipVisual);
            else if (layer.visual) m_root.Children().Remove(layer.visual);
        }
        m_layers.clear();
    }

    void SurfaceRenderer::dispatch(std::vector<Json> messages)
    {
        if (!onMessage) return;
        for (const Json& msg : messages)
        {
            if (msg["type"].asString() == "violation" && onDiag) onDiag("renderer-violation", msg["detail"].asString());
            onMessage(msg.dump());
        }
    }

    void SurfaceRenderer::paint()
    {
        std::unique_lock l(m_mutex);
        if (m_destroyed) return;

        uint32_t w = m_core->px_width();
        uint32_t h = m_core->px_height();
        if (w == 0 || h == 0) return;

        double cssHeight = m_core->viewport_height();
        std::optional<Presentation> presentation;
        if (postToUi && cssHeight != m_presentedHeight)
        {
            presentation.emplace();
            presentation->cssHeight = cssHeight;
            presentation->keepsBottom = m_keepsBottom;
        }

        double t0 = nowMs();
        twlite::DisplayList list = m_core->paint();

        std::lock_guard draw(GraphicsDevice::Current().DrawLock());

        try
        {
            // Resizing a surface in place empties it, and Composition can show it empty before the
            // draw lands: a new size is drawn into a new surface, which the brush takes once complete.
            // The height grows in steps and shrinks only past a slack, so a height transition rarely
            // needs one; the brush draws from the top left and the visual clips the rest.
            CompositionDrawingSurface replacement{ nullptr };
            auto interop = m_interop;
            if (presentation)
            {
                // The surface on screen keeps the old frame until the UI thread swaps this one in.
                if (m_spare && w == m_spareWidth && h <= m_spareHeight && h + kSurfaceSlack >= m_spareHeight)
                {
                    presentation->surface = m_spare;
                    presentation->width = m_spareWidth;
                    presentation->height = m_spareHeight;
                }
                else
                {
                    uint32_t height = (h + kSurfaceStep - 1) / kSurfaceStep * kSurfaceStep;
                    presentation->surface = GraphicsDevice::Current().CompositionDevice(m_brush.Compositor()).CreateDrawingSurface({ float(w), float(height) },
                        winrt::GraphicsDirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized, winrt::GraphicsDirectX::DirectXAlphaMode::Premultiplied);
                    presentation->width = w;
                    presentation->height = height;
                }
                presentation->interop = presentation->surface.as<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop>();
                interop = presentation->interop;
            }
            else if (w != m_surfaceWidth || h > m_surfaceHeight || h + kSurfaceSlack < m_surfaceHeight)
            {
                uint32_t height = (h + kSurfaceStep - 1) / kSurfaceStep * kSurfaceStep;
                replacement = GraphicsDevice::Current().CompositionDevice(m_brush.Compositor()).CreateDrawingSurface({ float(w), float(height) },
                    winrt::GraphicsDirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized, winrt::GraphicsDirectX::DirectXAlphaMode::Premultiplied);
                interop = replacement.as<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop>();
                m_surfaceWidth = w;
                m_surfaceHeight = height;
            }

            // Layers that appear draw before the document drops their elements, and layers that go
            // after it draws them again: Composition may show any step on its own, and none of them
            // may leave a hole.
            const twlite::Composition& composition = m_core->composition();
            bool recompose = m_root && composition.version != m_compositionVersion;
            if (recompose) applyComposition(composition);

            winrt::com_ptr<ID2D1DeviceContext> ctx;
            POINT offset;
            winrt::check_hresult(interop->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), ctx.put_void(), &offset));

            // Transparent target: ClearType needs an opaque one.
            ctx->Clear(D2D1::ColorF(0, 0));
            ctx->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
            ctx->SetTransform(D2D1::Matrix3x2F::Scale(float(m_dpr), float(m_dpr)) * D2D1::Matrix3x2F::Translation(float(offset.x), float(offset.y)));

            m_backend.paint(list, ctx.get());

            winrt::check_hresult(interop->EndDraw());
            if (replacement)
            {
                m_brush.Surface(replacement);
                m_surface = replacement;
                m_interop = interop;
            }

            if (recompose)
            {
                pruneLayers(composition);
                m_compositionVersion = composition.version;
            }
        }
        catch (winrt::hresult_error const& e)
        {
            if (onDiag) onDiag("renderer", std::format("paint failed: 0x{:08X}", uint32_t(e.code().value)));
            return;
        }

        m_lastPaintMs = nowMs() - t0;
        m_paints++;

        if (presentation)
        {
            l.unlock();
            present(std::move(*presentation));
        }
    }

    // On the clock thread, outside m_mutex: the UI thread's install may call back into the renderer.
    void SurfaceRenderer::present(Presentation frame)
    {
        struct Handoff
        {
            std::mutex lock;
            std::condition_variable done;
            bool installed = false;
            bool cancelled = false;
        };
        auto handoff = std::make_shared<Handoff>();
        // Whichever runs first installs the frame: the UI thread, or this one once the wait is over. A
        // late UI install finds the handoff cancelled and touches nothing, `this` included.
        bool posted = postToUi([this, handoff, frame]
        {
            std::lock_guard guard(handoff->lock);
            if (handoff->cancelled) return;
            install(frame);
            if (onPresented) onPresented(frame.cssHeight, frame.keepsBottom);
            handoff->installed = true;
            handoff->done.notify_all();
        });

        {
            std::unique_lock guard(handoff->lock);
            if (posted && handoff->done.wait_for(guard, kPresentTimeout, [&] { return handoff->installed; }))
            {
                return;
            }
            handoff->cancelled = true;
            install(frame);
        }
        if (onPresented) onPresented(frame.cssHeight, frame.keepsBottom);
    }

    void SurfaceRenderer::install(const Presentation& frame)
    {
        m_brush.Surface(frame.surface);
        if (frame.surface != m_surface)
        {
            m_spare = m_surface;
            m_spareWidth = m_surfaceWidth;
            m_spareHeight = m_surfaceHeight;
        }
        m_surface = frame.surface;
        m_interop = frame.interop;
        m_surfaceWidth = frame.width;
        m_surfaceHeight = frame.height;
        m_presentedHeight = frame.cssHeight;
    }

    // Under m_mutex and the draw lock, on the clock thread: composition objects are agile.
    void SurfaceRenderer::applyComposition(const twlite::Composition& composition)
    {
        auto compositor = m_root.Compositor();
        for (const twlite::Layer& source : composition.layers)
        {
            LayerVisual& layer = m_layers[source.id];
            bool fresh = !layer.visual;
            if (fresh)
            {
                layer.visual = compositor.CreateSpriteVisual();
                layer.props = compositor.CreatePropertySet();
                layer.props.InsertVector4(L"M", { 1, 0, 0, 1 });
                layer.props.InsertVector2(L"T", { 0, 0 });
                auto matrix = compositor.CreateExpressionAnimation(L"Matrix4x4(p.M.X, p.M.Y, 0, 0, p.M.Z, p.M.W, 0, 0, 0, 0, 1, 0, p.T.X, p.T.Y, 0, 1)");
                matrix.SetReferenceParameter(L"p", layer.props);
                layer.visual.StartAnimation(L"TransformMatrix", matrix);
            }

            bool sameClip = !fresh && layer.hasClip == source.has_clip && std::memcmp(&layer.clip, &source.clip, sizeof(twlite::Rect)) == 0 &&
                            std::memcmp(&layer.clipRadii, &source.clip_radii, sizeof(twlite::Radii)) == 0;
            if (!sameClip)
            {
                layer.hasClip = source.has_clip;
                layer.clip = source.clip;
                layer.clipRadii = source.clip_radii;
                if (layer.clipVisual)
                {
                    layer.clipVisual.Children().RemoveAll();
                    m_root.Children().Remove(layer.clipVisual);
                    layer.clipVisual = nullptr;
                }
                else if (!fresh)
                {
                    m_root.Children().Remove(layer.visual);
                }
                if (source.has_clip)
                {
                    // Composition's rounded rectangle has one radius for every corner: uneven radii clip square.
                    const twlite::Radii& r = source.clip_radii;
                    bool even = r.tlx == r.trx && r.tlx == r.brx && r.tlx == r.blx && r.tly == r.try_ && r.tly == r.bry && r.tly == r.bly;
                    auto shape = compositor.CreateRoundedRectangleGeometry();
                    shape.Offset({ source.clip.x, source.clip.y });
                    shape.Size({ source.clip.w, source.clip.h });
                    shape.CornerRadius(even ? winrt::Windows::Foundation::Numerics::float2{ r.tlx, r.tly } : winrt::Windows::Foundation::Numerics::float2{ 0, 0 });
                    layer.clipVisual = compositor.CreateContainerVisual();
                    layer.clipVisual.Clip(compositor.CreateGeometricClip(shape));
                    layer.clipVisual.Children().InsertAtTop(layer.visual);
                }
            }

            bool sameRaster = !fresh && std::memcmp(&layer.bounds, &source.bounds, sizeof(twlite::Rect)) == 0 && layer.rasterScale == source.raster_scale;
            if (!sameRaster || layer.contentVersion != source.content_version)
            {
                if (!drawLayer(layer, source)) continue;
            }
            if (!sameRaster || layer.trackVersion != source.track_version)
            {
                startTrack(layer, source);
            }
        }

        // Paint order, bottom first, above the document's own surface (the root's brush).
        for (const twlite::Layer& source : composition.layers)
        {
            auto it = m_layers.find(source.id);
            if (it == m_layers.end()) continue;
            winrt::UIComposition::Visual top = it->second.clipVisual ? winrt::UIComposition::Visual(it->second.clipVisual) : it->second.visual;
            if (top.Parent()) m_root.Children().Remove(top);
            m_root.Children().InsertAtTop(top);
        }
    }

    void SurfaceRenderer::pruneLayers(const twlite::Composition& composition)
    {
        std::set<int64_t> alive;
        for (const twlite::Layer& source : composition.layers) alive.insert(source.id);
        for (auto it = m_layers.begin(); it != m_layers.end();)
        {
            if (alive.count(it->first))
            {
                ++it;
                continue;
            }
            if (it->second.clipVisual) m_root.Children().Remove(it->second.clipVisual);
            else if (it->second.visual) m_root.Children().Remove(it->second.visual);
            it = m_layers.erase(it);
        }
        m_layerCount = int(m_layers.size());
    }

    bool SurfaceRenderer::drawLayer(LayerVisual& layer, const twlite::Layer& source)
    {
        float scale = float(m_dpr) * source.raster_scale;
        LONG pw = LONG(std::ceil(source.bounds.w * scale)), ph = LONG(std::ceil(source.bounds.h * scale));
        if (pw <= 0 || ph <= 0) return false;

        // As for the document's surface: a new size is a new surface, swapped in once drawn.
        CompositionDrawingSurface target = layer.surface;
        if (!target || pw != layer.surfaceWidth || ph != layer.surfaceHeight)
        {
            target = GraphicsDevice::Current().CompositionDevice(m_root.Compositor()).CreateDrawingSurface({ float(pw), float(ph) },
                winrt::GraphicsDirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized, winrt::GraphicsDirectX::DirectXAlphaMode::Premultiplied);
        }
        auto interop = target.as<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop>();

        winrt::com_ptr<ID2D1DeviceContext> ctx;
        POINT offset;
        winrt::check_hresult(interop->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), ctx.put_void(), &offset));
        ctx->Clear(D2D1::ColorF(0, 0));
        ctx->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ctx->SetTransform(D2D1::Matrix3x2F::Translation(-source.bounds.x, -source.bounds.y) * D2D1::Matrix3x2F::Scale(scale, scale) *
                          D2D1::Matrix3x2F::Translation(float(offset.x), float(offset.y)));
        m_backend.paint(source.content, ctx.get());
        winrt::check_hresult(interop->EndDraw());

        if (target != layer.surface)
        {
            if (!layer.brush)
            {
                layer.brush = m_root.Compositor().CreateSurfaceBrush();
                layer.brush.Stretch(CompositionStretch::Fill);
                layer.visual.Brush(layer.brush);
            }
            layer.brush.Surface(target);
            layer.surface = target;
            layer.surfaceWidth = pw;
            layer.surfaceHeight = ph;
        }
        layer.visual.Size({ source.bounds.w, source.bounds.h });
        layer.bounds = source.bounds;
        layer.rasterScale = source.raster_scale;
        layer.contentVersion = source.content_version;
        return true;
    }

    void SurfaceRenderer::startTrack(LayerVisual& layer, const twlite::Layer& source)
    {
        auto compositor = m_root.Compositor();
        double duration = 0;
        auto keys = twlite::resume_track(source.track, twlite::Renderer::clock() - source.track.start, duration);
        if (keys.empty()) return;

        // The content is drawn from the bounds' top left: the key's matrix takes document
        // coordinates, the visual's own start at the bounds.
        auto local = [&](const twlite::LayerKey& k) { return k.matrix * twlite::Affine::translate(source.bounds.x, source.bounds.y); };
        layer.props.StopAnimation(L"M");
        layer.props.StopAnimation(L"T");
        layer.visual.StopAnimation(L"Opacity");
        if (keys.size() == 1 || duration <= 0)
        {
            twlite::Affine m = local(keys.back());
            layer.props.InsertVector4(L"M", { m.a, m.b, m.c, m.d });
            layer.props.InsertVector2(L"T", { m.e, m.f });
            layer.visual.Opacity(keys.back().opacity);
        }
        else
        {
            auto linear = compositor.CreateLinearEasingFunction();
            auto m = compositor.CreateVector4KeyFrameAnimation();
            auto t = compositor.CreateVector2KeyFrameAnimation();
            auto o = compositor.CreateScalarKeyFrameAnimation();
            for (const twlite::LayerKey& k : keys)
            {
                twlite::Affine a = local(k);
                m.InsertKeyFrame(k.t, { a.a, a.b, a.c, a.d }, linear);
                t.InsertKeyFrame(k.t, { a.e, a.f }, linear);
                o.InsertKeyFrame(k.t, k.opacity, linear);
            }
            auto span = std::chrono::duration_cast<winrt::Windows::Foundation::TimeSpan>(std::chrono::duration<double>(std::max(duration, 0.001)));
            for (KeyFrameAnimation animation : { KeyFrameAnimation(m), KeyFrameAnimation(t), KeyFrameAnimation(o) })
            {
                animation.Duration(span);
                animation.IterationBehavior(source.track.repeat ? AnimationIterationBehavior::Forever : AnimationIterationBehavior::Count);
                animation.IterationCount(1);
                animation.StopBehavior(AnimationStopBehavior::LeaveCurrentValue);
            }
            // Started in the same commit, the three share a start time and stay in step.
            layer.props.StartAnimation(L"M", m);
            layer.props.StartAnimation(L"T", t);
            layer.visual.StartAnimation(L"Opacity", o);
        }
        layer.trackVersion = source.track_version;
    }

    void SurfaceRenderer::requestFrame()
    {
        {
            std::lock_guard l(m_clockMutex);
            if (m_frozen || m_frameRequested) return;
            m_frameRequested = true;
        }
        m_clockCv.notify_all();
    }

    void SurfaceRenderer::schedulePaint()
    {
        {
            std::lock_guard l(m_clockMutex);
            if (m_paintScheduled || m_stopClock) return;
            m_paintScheduled = true;
        }
        m_clockCv.notify_all();
    }

    void SurfaceRenderer::clockLoop()
    {
        for (;;)
        {
            bool frame, paint, tick;
            {
                std::unique_lock l(m_clockMutex);
                auto ready = [&] { return m_stopClock || m_frameRequested || m_paintScheduled || (m_ticking && m_visible); };
                bool woken = false;
                while (!ready())
                {
                    if (m_wakeAt && m_visible)
                    {
                        auto at = *m_wakeAt;
                        if (!m_clockCv.wait_until(l, at, ready) && m_wakeAt == at)
                        {
                            woken = true;
                            m_wakeAt.reset();
                            break;
                        }
                    }
                    else
                    {
                        m_clockCv.wait(l);
                    }
                }
                if (m_stopClock) return;

                // One tick: like rAF before the browser's paint, the callback first, the raster after.
                l.unlock();
                waitForTick();
                l.lock();
                if (m_stopClock) return;

                frame = m_frameRequested;
                paint = m_paintScheduled;
                tick = (m_ticking && m_visible) || woken;
                m_frameRequested = false;
                m_paintScheduled = false;
            }

            if (frame && onMessage) onMessage("{\"type\":\"frame\",\"time\":" + Json::number(nowMs()) + "}");
            if (tick)
            {
                // Animations advance and coalesced moves go out once per frame, before the paint.
                std::vector<Json> out;
                bool dirty = false, ticking = false;
                double wake;
                std::optional<twlite::Renderer::EditorSpec> editor;
                bool editorChanged;
                {
                    std::lock_guard l(m_mutex);
                    if (m_destroyed) return;
                    out = m_core->tick();
                    dirty = m_core->dirty();
                    ticking = m_core->wants_tick();
                    wake = m_core->next_tick_in();
                    // An animation can move the control under the editor.
                    editorChanged = takeEditor(editor);
                }
                if (editorChanged && onEditor) onEditor(std::move(editor));
                dispatch(std::move(out));
                setWake(wake);
                {
                    std::lock_guard l(m_clockMutex);
                    m_ticking = ticking;
                }
                paint = paint || dirty;
            }
            if (paint) this->paint();
        }
    }

    // On the compositor's frames, so that ticks keep in step with the display: the first frame within half a
    // refresh of the interval. A timed wait rounds up to the system timer's 15.6 ms, so 1/60 s took two.
    void SurfaceRenderer::waitForTick()
    {
        using namespace std::chrono;
        const auto interval = duration_cast<steady_clock::duration>(duration<double>(kTickSeconds));
        HANDLE stop = m_stopEvent.get();

        steady_clock::time_point previous;
        for (;;)
        {
            DWORD result = GraphicsDevice::WaitForCompositorClock(stop, kClockTimeoutMs);
            if (result == WAIT_FAILED) break;
            if (result == WAIT_OBJECT_0) return;

            auto now = steady_clock::now();
            if (result == WAIT_OBJECT_0 + 1)
            {
                if (previous != steady_clock::time_point()) m_refresh = now - previous;
                previous = now;
            }

            if (now - m_lastTick + m_refresh / 2 >= interval)
            {
                m_lastTick = now;
                return;
            }
        }

        if (!m_timer) m_timer.attach(CreateWaitableTimerExW(nullptr, nullptr, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS));

        auto remaining = m_lastTick + interval - steady_clock::now();
        if (remaining > steady_clock::duration::zero())
        {
            LARGE_INTEGER due;
            due.QuadPart = -duration_cast<duration<LONGLONG, std::ratio<1, 10'000'000>>>(remaining).count();
            HANDLE handles[] = { stop, m_timer.get() };
            if (m_timer && SetWaitableTimer(m_timer.get(), &due, 0, nullptr, nullptr, FALSE))
            {
                WaitForMultipleObjects(2, handles, FALSE, INFINITE);
            }
            else
            {
                WaitForSingleObject(stop, DWORD(duration_cast<milliseconds>(remaining).count()) + 1);
            }
        }

        m_lastTick = steady_clock::now();
    }
}
