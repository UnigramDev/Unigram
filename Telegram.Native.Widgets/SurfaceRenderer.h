// The Renderer contract of WidgetHost on the litehtml core (apps/renderer-litehtml/src/renderer.h),
// painted by Direct2D straight onto a CompositionDrawingSurface. It is the desktop core's
// LitehtmlRenderer (packages/desktop/renderer) without the C ABI and without the CPU raster: the
// display list goes to the GPU surface instead of into an RGBA buffer.
//
// Threading, as in LitehtmlRenderer: the core is not locked on its side, so every call into it
// goes through one mutex here. The host thread (messages), the clock thread (frames and paints)
// and the UI thread (pointers) take turns. Messages to the host are dispatched outside the lock.
#pragma once

#include "pch.h"

#include "d2d_backend.h"
#include "renderer.h"
#include "twidget/renderer.h"

#include <map>
#include <optional>

namespace Widgets
{
    class SurfaceRenderer final : public twidget::Renderer
    {
    public:
        // `root` shows `brush`, which holds a drawing surface; the core's compositor layers become its children. Null: the core
        // paints every frame into the surface instead.
        SurfaceRenderer(const std::string& theme, double width, double dpr, winrt::UIComposition::CompositionSurfaceBrush brush,
            winrt::UIComposition::ContainerVisual root);
        ~SurfaceRenderer() override;

        // Renderer (host thread)
        void load() override;
        void send(const std::string& json) override;
        void setTheme(const std::string& theme) override;
        void setWidth(double width) override;
        void destroy() override;

        // Input in CSS px, any thread. touch action: 0 down, 1 move, 2 up, 3 cancel; hover action: 0 move, 1 leave.
        // A primary press returns its gesture claim, bit0 horizontal drags, bit1 vertical, bit2 holds; anything else -1.
        int touch(int action, double x, double y, int pointer, bool primary, bool mouse, int modifiers, int button);
        void hover(int action, double x, double y, int modifiers);
        void wheel(double x, double y, double dx, double dy, int mode, int modifiers);
        void key(const std::string& type, const std::string& key, const std::string& code, bool repeat, int modifiers);
        // The text a mouse drag selected ("" without one), and dropping it; any thread.
        std::string selectedText();
        bool clearSelection();
        // Off screen nothing animates: the clock stops ticking until the widget is back.
        void setVisible(bool visible);
        // The app's editor changed the text of editor `generation`, or let go of the keyboard.
        void edit(uint64_t generation, const std::string& text);
        void endEditing();

        // The text control to cover with an editor (twlite::Renderer::editor()), or none; any thread,
        // outside the lock. Its font_family is the DirectWrite family the document draws it with.
        std::function<void(std::optional<twlite::Renderer::EditorSpec>)> onEditor;

        // A frame of a new height is installed on the UI thread, where the host resizes the bubble, so
        // that the two land in the same frame. Runs `install` there and returns true, or false when there
        // is no UI thread to post to. Set before load().
        std::function<bool(std::function<void()> install)> postToUi;
        // A frame of a new height is on screen: its CSS height, and whether its resize keeps the bottom
        // still. On the UI thread, or on the clock thread when the UI thread did not get to it in time.
        std::function<void(double height, bool keepsBottom)> onPresented;
        // The widget's resize anchor, which the frames of the heights that follow carry; any thread.
        void setKeepsBottom(bool keepsBottom) { m_keepsBottom = keepsBottom; }

        int paints() const { return m_paints; }
        double lastPaintMs() const { return m_lastPaintMs; }
        double lastBuildMs() const { return m_lastBuildMs; }
        int builds() const { return m_builds; }
        int patchedBuilds() const { return m_patched; }
        int layers() const { return m_layerCount; }

    private:
        // One compositor layer of the core (compositor.h): a sprite showing the layer's content,
        // under a container that clips it when an ancestor clips. Its transform is an expression
        // over `props`, whose M (a, b, c, d) and T (e, f) keyframes interpolate linearly, component
        // by component, as the core baked them.
        struct LayerVisual
        {
            uint64_t contentVersion = 0;
            uint64_t trackVersion = 0;
            twlite::Rect bounds;
            float rasterScale = 0;
            bool hasClip = false;
            twlite::Rect clip;
            twlite::Radii clipRadii;
            winrt::UIComposition::SpriteVisual visual{ nullptr };
            winrt::UIComposition::ContainerVisual clipVisual{ nullptr };
            winrt::UIComposition::CompositionDrawingSurface surface{ nullptr };
            winrt::UIComposition::CompositionSurfaceBrush brush{ nullptr };
            LONG surfaceWidth = 0;
            LONG surfaceHeight = 0;
            winrt::UIComposition::CompositionPropertySet props{ nullptr };
        };
        // Adds and updates layers; pruneLayers drops the ones `composition` no longer has.
        void applyComposition(const twlite::Composition& composition);
        void pruneLayers(const twlite::Composition& composition);
        bool drawLayer(LayerVisual& layer, const twlite::Layer& source);
        void startTrack(LayerVisual& layer, const twlite::Layer& source);
        void setWake(double seconds);
        // Under m_mutex: true when the editor changed since it was last handed out, with the new one in `spec`.
        bool takeEditor(std::optional<twlite::Renderer::EditorSpec>& spec);

        void dispatch(std::vector<twidget::Json> messages);
        // Runs `call` on the core under the lock, then sends what it returned, repaints and keeps ticking as it asks.
        template <class Call> void run(Call call);
        void setTicking(bool ticking);
        void schedulePaint();
        void requestFrame();
        void clockLoop();
        void paint();
        // A frame drawn for a new height, waiting for the UI thread to show it.
        struct Presentation
        {
            winrt::UIComposition::CompositionDrawingSurface surface{ nullptr };
            winrt::com_ptr<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop> interop;
            uint32_t width = 0, height = 0;  // the surface's, physical px
            double cssHeight = 0;
            bool keepsBottom = false;
        };
        void present(Presentation frame);
        void install(const Presentation& frame);
        static double nowMs();

        double m_dpr;
        twlite::D2DBackend m_backend;
        std::unique_ptr<twlite::Renderer> m_core;
        winrt::UIComposition::CompositionSurfaceBrush m_brush;
        winrt::UIComposition::CompositionDrawingSurface m_surface;
        winrt::com_ptr<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop> m_interop;
        uint32_t m_surfaceWidth = 0;
        uint32_t m_surfaceHeight = 0;
        // The surface the brush showed before the last presentation: a new height is drawn into it, not
        // into the one on screen. The clock thread's, and the install's while the clock thread waits on it.
        winrt::UIComposition::CompositionDrawingSurface m_spare{ nullptr };
        uint32_t m_spareWidth = 0;
        uint32_t m_spareHeight = 0;
        double m_presentedHeight = -1;
        std::atomic<bool> m_keepsBottom{ false };
        std::mutex m_mutex; // every call into m_core, the surfaces and the layer visuals
        winrt::UIComposition::ContainerVisual m_root{ nullptr };
        std::map<int64_t, LayerVisual> m_layers;
        uint64_t m_compositionVersion = 0;
        std::atomic<int> m_layerCount{ 0 };
        uint64_t m_editorGeneration = 0; // of the spec last handed to onEditor, 0 for none

        // One tick per display refresh at most, like CADisplayLink / Choreographer in the demos.
        std::mutex m_clockMutex;
        std::condition_variable m_clockCv;
        std::thread m_clock;
        bool m_frameRequested = false;
        bool m_ticking = false; // the core animates or holds coalesced moves
        // Not ticking, the core still needs a tick here: an animation event, or a layer's track ending.
        std::optional<std::chrono::steady_clock::time_point> m_wakeAt;
        bool m_visible = true;
        bool m_paintScheduled = false;
        bool m_stopClock = false;
        bool m_frozen = false;
        bool m_destroyed = false;

        std::atomic<int> m_paints{ 0 };
        std::atomic<double> m_lastPaintMs{ 0 };
        std::atomic<double> m_lastBuildMs{ 0 };
        std::atomic<int> m_builds{ 0 };
        std::atomic<int> m_patched{ 0 };
    };
}
