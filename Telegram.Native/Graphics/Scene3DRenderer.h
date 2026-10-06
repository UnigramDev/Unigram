#pragma once

#include "Scene3D.h"
#include "Scene3DDevice.h"

#include <winrt/Windows.UI.Xaml.Controls.h>

#include <d3d11_1.h>
#include <dxgi1_3.h>

#include <atomic>
#include <chrono>
#include <functional>
#include <memory>
#include <mutex>

namespace Graphics3D
{
    /// <summary>
    /// A swap chain sized to a <c>SwapChainPanel</c>, a frame clock and a pointer that turns
    /// whatever is in it. Everything here is the same for any scene.
    /// </summary>
    /// <remarks>
    /// Frames come off <see cref="Scene3DDevice"/>'s thread, shared with every other panel, rather
    /// than off a timer on the UI thread: that is then not woken per frame, and nothing is queued
    /// on it for the compositor to collect. <c>CompositionTarget.Rendering</c> would do neither -
    /// it allocates an event args peer per frame that only suspending drains.
    ///
    /// The division of labour is strict: the render thread is the only one that draws, and only
    /// between <see cref="Start"/> and <see cref="Stop"/>, both of which take the device lock and
    /// so cannot return while a frame of this renderer is under way. The UI thread leaves
    /// *requests* - a new size, a drag, a kick - which the thread picks up at the top of a frame.
    /// </remarks>
    class Scene3DRenderer
    {
    public:
        Scene3DRenderer(std::unique_ptr<IScene3D> scene);
        ~Scene3DRenderer();

        Scene3DRenderer(const Scene3DRenderer&) = delete;
        Scene3DRenderer& operator=(const Scene3DRenderer&) = delete;

        // UI thread, and once: a renderer is not attached again after Detach. Hands a swap chain
        // to the panel. False means this machine cannot draw a scene, and never will.
        //
        // notify is called on the render thread, under the device lock, when the renderer has
        // failed for good or its device has been replaced - HasFailed and WasReplaced say which.
        // Either way the panel has to drop this renderer, and must not do so from inside the call.
        bool Attach(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& panel,
            std::function<void()> notify);
        void Detach();

        // UI thread. Moves the swap chain to another panel and sends notifications to its owner
        // from now on; the scene, the pose and the clock carry on untouched. False if the new
        // panel refused the swap chain, which leaves the renderer to be detached from it.
        bool Rebind(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& panel,
            std::function<void()> notify);

        // Any thread.
        bool HasFailed() const noexcept { return m_failed; }
        bool WasReplaced() const noexcept { return m_replaced; }

        // UI thread, and both are safe to call repeatedly.
        void Start();
        void Stop();

        // UI thread. Panel size in logical pixels and its rasterization scale; the swap chain is
        // sized in physical ones and scaled back down so one texel is one physical pixel.
        void SetSize(float width, float height, float scale);

        // UI thread. Degrees already, so that the two hosts being matched can be compared against
        // this line for line.
        void BeginDrag();
        void Drag(float horizontalPixels, float verticalPixels);
        void EndDrag();

        // UI thread. An angular impulse in degrees per second, added to whatever the scene is
        // already doing and running down from there. Positive hurries the idle spin along.
        void Kick(double degreesPerSecond);

        void SetSpinning(bool value);
        bool IsSpinning() const noexcept { return m_spinning; }

        // UI thread. Degrees per second, or zero for the scene's own rate.
        void SetSpinSpeed(float degreesPerSecond) noexcept { m_spinSpeed = degreesPerSecond; }

        // Which look the scene should wear, for one authored with more than one.
        void SetVariant(int variant);

    private:
        friend class Scene3DDevice;

        enum class FrameResult
        {
            Presented,
            Occluded,
            Idle,       // nothing to present yet
            Failed,     // for good; the renderer is taken off the loop
            Lost
        };

        // Render thread, under the device lock.
        FrameResult Frame(std::chrono::steady_clock::time_point now);
        std::uint64_t Generation() const noexcept { return m_generation; }
        void NotifyReplaced();
        void NotifyFailed();

        bool CreateSwapChain();
        void Release();
        bool ApplyPending();
        void ApplyScale();
        void Advance(ScenePose& pose, float delta, bool settling);

        std::unique_ptr<IScene3D> m_scene;
        SceneMotion m_motion;

        winrt::Windows::UI::Xaml::Controls::SwapChainPanel m_panel{ nullptr };

        std::shared_ptr<Scene3DDevice> m_device;
        std::function<void()> m_notify;
        std::atomic<bool> m_replaced{ false };

        // Under the device lock, both of them. The generation is the device the swap chain was
        // made on, and zero when there is none.
        std::uint64_t m_generation = 0;
        bool m_active = false;

        winrt::com_ptr<IDXGISwapChain2> m_swapChain;
        winrt::com_ptr<ID3D11RenderTargetView> m_backBufferView;

        // Where the scene stands, and the release spring that may be part way through. Members
        // rather than locals in Run, because stopping for a scroll and starting again must not
        // lose the angle it was left at or restart the baked animation from zero.
        ScenePose m_pose;
        bool m_wasDragging = false;

        // Where the pose stood when the press began, which is what a release returns to.
        float m_pressedYaw = 0;
        float m_pressedPitch = 0;

        float m_settleYaw = 0;
        float m_settlePitch = 0;
        float m_settle = -1;        // negative while no settle is running

        // When the last frame was drawn, and whether the clock has to start over because the
        // renderer has just been started. Render thread only, apart from Start setting the flag
        // under the device lock.
        std::chrono::steady_clock::time_point m_previous{};
        bool m_restart = true;

        // True once Load has been tried. A scene that cannot load is not retried: the only way it
        // fails is a missing or malformed asset, and that does not get better by asking again.
        // Set on the render thread and read on the UI one, which is why it is atomic.
        bool m_loaded = false;
        std::atomic<bool> m_failed{ false };

        // What the UI thread has left for the render thread. The flag is what is read on the hot
        // path, so an untouched frame takes no lock at all.
        std::mutex m_pendingLock;
        std::atomic<bool> m_pending{ false };

        std::uint32_t m_pendingWidth = 0;
        std::uint32_t m_pendingHeight = 0;
        float m_pendingScale = 1;
        float m_pendingYaw = 0;
        float m_pendingPitch = 0;
        float m_pendingKick = 0;
        bool m_pendingVariantSet = false;
        int m_pendingVariant = 0;

        std::uint32_t m_width = 0;
        std::uint32_t m_height = 0;
        float m_scale = 1;

        // The drag taken off the pending block, waiting to be folded into the pose. Render thread
        // only, which is what lets Advance consume it without a lock.
        float m_dragYaw = 0;
        float m_dragPitch = 0;

        // The tilt as dragged, before the soft limit. The pose carries the compressed angle; this
        // is the one the pointer and the release spring act on.
        float m_rawPitch = 0;

        // What is left of the kicks it has been given, in degrees per second.
        float m_kick = 0;

        std::atomic<bool> m_dragging{ false };
        std::atomic<bool> m_spinning{ false };
        std::atomic<float> m_spinSpeed{ 0 };
    };
}
