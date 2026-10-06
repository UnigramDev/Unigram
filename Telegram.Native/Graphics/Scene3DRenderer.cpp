#include "pch.h"
#include "Scene3DRenderer.h"

#include <windows.ui.xaml.media.dxinterop.h>
#include <winrt/Telegram.Native.h>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <format>

using namespace winrt::Windows::UI::Xaml::Controls;

namespace Graphics3D
{
    // The LOGGER_ macros name both unqualified, and outside winrt::Telegram::Native nothing else
    // brings them into scope.
    using winrt::hstring;
    using winrt::Telegram::Native::NativeUtils;

    namespace
    {
        // The app being matched caps the frame delta before it advances anything, so that a
        // stall does not jump the animation a quarter turn.
        constexpr float MaximumStep = 0.1f;

        /// <summary>
        /// Compresses a tilt towards a limit it never reaches.
        /// </summary>
        /// <remarks>
        /// tanh, scaled so that small drags are very nearly one for one and the resistance only
        /// builds as the limit nears. At the limit itself the stone is looking straight up, so
        /// the top face can be brought fully into view - it just costs progressively more drag,
        /// which is what the phone feels like.
        ///
        /// **The curve is not from the beta.** Its accumulator is linear and its renderer takes
        /// the tilt raw - there is no clamp and no soft limit anywhere in the package - so the
        /// resistance is applied in a step between the two that the dex tooling could not follow
        /// across objects. This shape was chosen to match the behaviour as described: unbounded
        /// horizontally, resisted vertically, poles reachable with a long drag.
        /// </remarks>
        float SoftLimit(float degrees, float limit)
        {
            if (limit <= 0)
            {
                return degrees;
            }

            return limit * std::tanh(degrees / limit);
        }

        // The same angle measured from rest rather than from zero: 350 degrees is ten degrees
        // the other way, and a release should take the short way back.
        float Signed(float degrees)
        {
            degrees = std::fmod(degrees + 180.0f, 360.0f);

            if (degrees < 0)
            {
                degrees += 360.0f;
            }

            return degrees - 180.0f;
        }

        // Lets go with an OvershootInterpolator, whose default tension is 2.
        float Overshoot(float t)
        {
            constexpr float tension = 2.0f;
            t -= 1.0f;
            return t * t * ((tension + 1.0f) * t + tension) + 1.0f;
        }
    }

    Matrix4 Matrix4::RotationX(float radians)
    {
        Matrix4 r;
        const float c = std::cos(radians), s = std::sin(radians);
        r.at(1, 1) = c;  r.at(2, 1) = -s;
        r.at(1, 2) = s;  r.at(2, 2) = c;
        return r;
    }

    Matrix4 Matrix4::RotationY(float radians)
    {
        Matrix4 r;
        const float c = std::cos(radians), s = std::sin(radians);
        r.at(0, 0) = c;  r.at(2, 0) = s;
        r.at(0, 2) = -s; r.at(2, 2) = c;
        return r;
    }

    Matrix4 Matrix4::RotationZ(float radians)
    {
        Matrix4 r;
        const float c = std::cos(radians), s = std::sin(radians);
        r.at(0, 0) = c;  r.at(1, 0) = -s;
        r.at(0, 1) = s;  r.at(1, 1) = c;
        return r;
    }

    Matrix4 Matrix4::Transposed() const
    {
        Matrix4 r;
        for (int c = 0; c < 4; ++c)
        {
            for (int row = 0; row < 4; ++row)
            {
                r.at(c, row) = at(row, c);
            }
        }
        return r;
    }

    Matrix4 Matrix4::operator*(const Matrix4& other) const
    {
        Matrix4 r;
        for (int c = 0; c < 4; ++c)
        {
            for (int row = 0; row < 4; ++row)
            {
                float sum = 0;
                for (int k = 0; k < 4; ++k)
                {
                    sum += at(k, row) * other.at(c, k);
                }
                r.at(c, row) = sum;
            }
        }
        return r;
    }

    Scene3DRenderer::Scene3DRenderer(std::unique_ptr<IScene3D> scene)
        : m_scene(std::move(scene))
    {
        m_motion = m_scene ? m_scene->Motion() : SceneMotion();
        m_spinning = m_motion.spinsByDefault;
    }

    Scene3DRenderer::~Scene3DRenderer()
    {
        // Not Detach: the panel's own destructor can bring this here on the finalizer thread,
        // where handing the panel a swap chain is not allowed. It has been detached by then.
        Stop();
        Release();
    }

    bool Scene3DRenderer::Attach(SwapChainPanel const& panel,
        std::function<void()> notify)
    {
        if (m_failed || m_scene == nullptr || m_device != nullptr)
        {
            return false;
        }

        m_device = Scene3DDevice::Acquire();

        if (m_device == nullptr)
        {
            m_failed = true;
            return false;
        }

        m_notify = std::move(notify);
        m_panel = panel;

        {
            std::scoped_lock lock(m_device->Lock());

            if (m_device->IsAbandoned())
            {
                m_failed = true;
            }
            else
            {
                m_device->Register(this);

                if (m_device->Device() == nullptr)
                {
                    // Lost, and a new one on its way. Registered, so the panel is told when it is.
                    return true;
                }

                if (!CreateSwapChain())
                {
                    if (FAILED(m_device->Device()->GetDeviceRemovedReason()))
                    {
                        m_device->MarkLost(m_device->Generation());
                        return true;
                    }

                    m_failed = true;
                }
                else
                {
                    m_generation = m_device->Generation();
                }
            }
        }

        if (!m_failed)
        {
            try
            {
                auto native = panel.as<ISwapChainPanelNative>();
                winrt::check_hresult(native->SetSwapChain(m_swapChain.get()));
                return true;
            }
            catch (...)
            {
                LogException(L"Scene3D: SetSwapChain");
                m_failed = true;
            }
        }

        m_panel = nullptr;
        Release();
        return false;
    }

    void Scene3DRenderer::Detach()
    {
        Stop();

        if (m_panel != nullptr)
        {
            try
            {
                // Before the swap chain goes, or the panel keeps presenting a dead one.
                auto native = m_panel.as<ISwapChainPanelNative>();
                native->SetSwapChain(nullptr);
            }
            catch (...)
            {
                // Teardown, and the panel may already be gone with its view.
            }

            m_panel = nullptr;
        }

        Release();
    }

    bool Scene3DRenderer::Rebind(SwapChainPanel const& panel, std::function<void()> notify)
    {
        if (m_device == nullptr || m_panel == nullptr)
        {
            return false;
        }

        winrt::com_ptr<IDXGISwapChain2> swapChain;

        {
            // Under the lock because the render thread calls m_notify under it.
            std::scoped_lock lock(m_device->Lock());

            m_notify = std::move(notify);
            swapChain = m_swapChain;
        }

        try
        {
            // Released by the old panel before the new one takes it.
            m_panel.as<ISwapChainPanelNative>()->SetSwapChain(nullptr);
        }
        catch (...)
        {
            // The old panel may already be gone with its view.
        }

        m_panel = panel;

        if (swapChain == nullptr)
        {
            // Attached while the device was lost: there is no swap chain to move, and the
            // replacement the new owner is about to be told of will bring one.
            return true;
        }

        try
        {
            winrt::check_hresult(panel.as<ISwapChainPanelNative>()->SetSwapChain(swapChain.get()));
            return true;
        }
        catch (...)
        {
            LogException(L"Scene3D: SetSwapChain on rebind");
            return false;
        }
    }

    void Scene3DRenderer::Release()
    {
        if (m_device == nullptr)
        {
            return;
        }

        // Dropped after the lock is released rather than inside it: this may be the last
        // reference, and the device's destructor joins the thread that takes the lock.
        std::shared_ptr<Scene3DDevice> device;

        {
            std::scoped_lock lock(m_device->Lock());
            m_device->Unregister(this);

            // Under the lock, because releasing a swap chain drives the immediate context.
            m_backBufferView = nullptr;
            m_swapChain = nullptr;
            m_scene.reset();
            m_generation = 0;

            // Only once unregistered: until then a frame on the render thread may be reading it.
            device = std::move(m_device);
        }

        m_notify = nullptr;
    }

    void Scene3DRenderer::NotifyReplaced()
    {
        m_replaced = true;

        if (m_notify)
        {
            m_notify();
        }
    }

    void Scene3DRenderer::NotifyFailed()
    {
        m_failed = true;

        if (m_notify)
        {
            m_notify();
        }
    }

    bool Scene3DRenderer::CreateSwapChain()
    {
        try
        {
            // One pixel until the panel reports a size. Zero is not allowed and a large guess
            // would allocate a surface that is thrown away on the first layout pass.
            DXGI_SWAP_CHAIN_DESC1 desc = {};
            desc.Width = 1;
            desc.Height = 1;
            desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            desc.SampleDesc.Count = 1;
            desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
            desc.BufferCount = 2;
            desc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
            desc.Scaling = DXGI_SCALING_STRETCH;

            // The scene draws onto nothing and the panel is composed over the screen behind it,
            // so what leaves here is premultiplied with a real alpha channel.
            desc.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;

            winrt::com_ptr<IDXGISwapChain1> swapChain;
            winrt::check_hresult(m_device->Factory()->CreateSwapChainForComposition(
                m_device->Device(), &desc, nullptr, swapChain.put()));

            m_swapChain = swapChain.as<IDXGISwapChain2>();
            return true;
        }
        catch (...)
        {
            LogException(L"Scene3D: CreateSwapChainForComposition");
            return false;
        }
    }

    void Scene3DRenderer::SetSize(float width, float height, float scale)
    {
        if (width <= 0 || height <= 0 || scale <= 0)
        {
            return;
        }

        const auto physicalWidth = static_cast<std::uint32_t>(std::lround(width * scale));
        const auto physicalHeight = static_cast<std::uint32_t>(std::lround(height * scale));

        if (physicalWidth == 0 || physicalHeight == 0)
        {
            return;
        }

        {
            std::scoped_lock lock(m_pendingLock);
            m_pendingWidth = physicalWidth;
            m_pendingHeight = physicalHeight;
            m_pendingScale = scale;
        }

        m_pending = true;
    }

    void Scene3DRenderer::BeginDrag()
    {
        m_dragging = true;
    }

    void Scene3DRenderer::Drag(float horizontalPixels, float verticalPixels)
    {
        if (m_motion.dragYawPerPixel == 0 && m_motion.dragPitchPerPixel == 0)
        {
            return;
        }

        {
            // Accumulated rather than replaced: several pointer moves can land between two
            // frames, and dropping all but the last would make a fast drag travel less far.
            std::scoped_lock lock(m_pendingLock);
            m_pendingYaw -= horizontalPixels * m_motion.dragYawPerPixel;
            m_pendingPitch -= verticalPixels * m_motion.dragPitchPerPixel;
        }

        m_pending = true;
    }

    void Scene3DRenderer::EndDrag()
    {
        m_dragging = false;
    }

    void Scene3DRenderer::Kick(double degreesPerSecond)
    {
        if (m_motion.kickDecaySeconds <= 0)
        {
            return;
        }

        {
            // Accumulated, so a burst of them piles up rather than the last one replacing the
            // rest - which is what makes typing quickly spin it faster than typing slowly.
            std::scoped_lock lock(m_pendingLock);
            m_pendingKick += static_cast<float>(degreesPerSecond);
        }

        m_pending = true;
    }

    void Scene3DRenderer::SetSpinning(bool value)
    {
        m_spinning = value && m_motion.secondsPerTurn > 0;
    }

    void Scene3DRenderer::SetVariant(int variant)
    {
        {
            std::scoped_lock lock(m_pendingLock);
            m_pendingVariant = variant;
            m_pendingVariantSet = true;
        }

        m_pending = true;
    }

    void Scene3DRenderer::Start()
    {
        if (m_failed || m_device == nullptr)
        {
            return;
        }

        std::scoped_lock lock(m_device->Lock());

        if (!m_active)
        {
            m_restart = true;
            m_device->SetActive(this, true);
        }
    }

    void Scene3DRenderer::Stop()
    {
        if (m_device == nullptr)
        {
            return;
        }

        std::scoped_lock lock(m_device->Lock());
        m_device->SetActive(this, false);
    }

    void Scene3DRenderer::ApplyScale()
    {
        if (m_swapChain == nullptr)
        {
            return;
        }

        // The swap chain holds physical pixels and the panel is laid out in logical ones, so the
        // surface is scaled back down by exactly what the size was scaled up by.
        DXGI_MATRIX_3X2_F matrix = {};
        matrix._11 = 1.0f / m_scale;
        matrix._22 = 1.0f / m_scale;

        m_swapChain->SetMatrixTransform(&matrix);
    }

    bool Scene3DRenderer::ApplyPending()
    {
        const auto ready = [this] { return m_width > 0 && m_backBufferView != nullptr; };

        if (!m_pending.exchange(false))
        {
            return ready();
        }

        std::uint32_t width, height;
        float scale, yaw, pitch, kick;
        bool variantSet;
        int variant;

        {
            std::scoped_lock lock(m_pendingLock);
            width = m_pendingWidth;
            height = m_pendingHeight;
            scale = m_pendingScale;
            yaw = m_pendingYaw;
            pitch = m_pendingPitch;
            kick = m_pendingKick;
            variantSet = m_pendingVariantSet;
            variant = m_pendingVariant;

            m_pendingYaw = 0;
            m_pendingPitch = 0;
            m_pendingKick = 0;
            m_pendingVariantSet = false;
        }

        if (variantSet)
        {
            m_scene->SetVariant(variant);
        }

        // Folded in by the caller through the pose, so the drag is consumed exactly once.
        m_dragYaw = yaw;
        m_dragPitch = pitch;
        m_kick += kick;

        if (width == 0 || height == 0)
        {
            return ready();
        }

        if (width != m_width || height != m_height)
        {
            m_width = width;
            m_height = height;

            // Everything holding a back buffer reference has to let go before ResizeBuffers.
            m_backBufferView = nullptr;
            m_device->Context()->OMSetRenderTargets(0, nullptr, nullptr);

            const HRESULT hr = m_swapChain->ResizeBuffers(0, width, height, DXGI_FORMAT_UNKNOWN, 0);
            if (FAILED(hr))
            {
                LOGGER_ERROR(L"Scene3D: ResizeBuffers to {}x{}, 0x{:08X}", width, height,
                    static_cast<std::uint32_t>(hr));
                m_failed = true;
                return false;
            }
        }

        if (scale != m_scale)
        {
            m_scale = scale;
            ApplyScale();
        }

        if (m_backBufferView == nullptr)
        {
            winrt::com_ptr<ID3D11Texture2D> backBuffer;
            HRESULT hr = m_swapChain->GetBuffer(0, winrt::guid_of<ID3D11Texture2D>(),
                backBuffer.put_void());

            if (SUCCEEDED(hr))
            {
                hr = m_device->Device()->CreateRenderTargetView(backBuffer.get(), nullptr,
                    m_backBufferView.put());
            }

            if (FAILED(hr))
            {
                LOGGER_ERROR(L"Scene3D: back buffer target, 0x{:08X}", static_cast<std::uint32_t>(hr));
                m_failed = true;
                return false;
            }

            m_scene->Resize(m_device->Device(), m_width, m_height);
        }

        return true;
    }

    void Scene3DRenderer::Advance(ScenePose& pose, float delta, bool settling)
    {
        const float step = std::clamp(delta, 0.0f, MaximumStep);

        pose.time += step;
        pose.step = step;

        // Accumulated apart from the yaw, so that the rate below is the amount actually turned
        // rather than the difference between two wrapped angles - which reads as a third of a
        // turn in one frame, every time the wrap comes round.
        float turned = 0;

        // Held still for the first moment it is on screen. pose.time is the right clock for it:
        // it counts only while the scene is visible and survives a scroll, so the hold happens
        // once when it appears rather than again every time it comes back into view.
        const bool held = pose.time < m_motion.spinStartDelaySeconds;

        // Not while settling either: the release owns both angles until it is done, and a spin
        // added underneath it would fight the spring. The app being matched cancels its spin
        // animator for the same reason and restarts it when the settle ends.
        if (m_spinning && !m_dragging && !settling && !held && m_motion.secondsPerTurn > 0)
        {
            // Added, which is what the beta's own animator does - it runs the yaw from d to
            // d + 360. It turns the scene left to right because beta 11 stopped negating the
            // yaw in its model matrix; under beta 8's convention the same sign went the other
            // way, which is why this needed a hand-held minus before the re-port.
            const float speed = m_spinSpeed;
            turned += (speed > 0 ? speed : 360.0f / m_motion.secondsPerTurn) * step;
        }

        turned += m_dragYaw;
        m_dragYaw = 0;

        // Whatever is left of a kick, spent over this frame and then run down. Independent of the
        // idle spin, so a scene that never turns on its own can still be nudged - and suppressed
        // while dragging, because the pointer owns the yaw outright for as long as it is down.
        if (m_kick != 0 && !m_dragging)
        {
            turned += m_kick * step;

            m_kick *= std::exp(-step / m_motion.kickDecaySeconds);

            // Below half a degree a second there is nothing left to see, and stopping here keeps
            // the pose still rather than creeping for ever on a vanishing remainder.
            if (std::fabs(m_kick) < 0.5f)
            {
                m_kick = 0;
            }
        }

        pose.yaw += turned;

        // Accumulated unclamped and compressed only where it is read, so that the resistance is
        // in what the stone does rather than in what the pointer is allowed to say. Dragging back
        // then undoes exactly what dragging out did, which a clamp would not.
        m_rawPitch += m_dragPitch;
        m_dragPitch = 0;

        pose.pitch = SoftLimit(m_rawPitch, m_motion.pitchSoftLimitDegrees);

        // Kept in one turn so that neither the yaw nor anything splined off it drifts out of the
        // range its table covers after a few minutes of spinning.
        pose.yaw = std::fmod(pose.yaw, 360.0f);
        if (pose.yaw < 0)
        {
            pose.yaw += 360.0f;
        }

        pose.yawPerSecond = step > 0 ? turned / step : 0;
        pose.width = m_width;
        pose.height = m_height;
    }

    Scene3DRenderer::FrameResult Scene3DRenderer::Frame(std::chrono::steady_clock::time_point now)
    {
        if (m_restart)
        {
            m_restart = false;
            m_previous = now;
        }

        if (!ApplyPending())
        {
            m_previous = now;

            // A target that could not be made is as often the device going as anything else, and
            // that is recoverable where a failure of the target itself is not.
            if (FAILED(m_device->Device()->GetDeviceRemovedReason()))
            {
                return FrameResult::Lost;
            }

            return m_failed ? FrameResult::Failed : FrameResult::Idle;
        }

        if (!m_loaded)
        {
            m_loaded = true;

            std::wstring error;
            if (!m_scene->Load(m_device->Device(), m_device->Cache(), error))
            {
                LOGGER_ERROR(L"Scene3D: {}", error);
                m_failed = true;
                return FrameResult::Failed;
            }

            // Loading is not time the scene has spent on screen, and the clock was started
            // before it. The driver compiling the diamond's pixel shader and nine files
            // coming off disk run to a tenth of a second between them, which is most of the
            // entry fade and most of the pause before the spin - so without this the first
            // frame shown is already half faded in and reads as a pop.
            now = std::chrono::steady_clock::now();
            m_previous = now;
        }

        const float delta = std::chrono::duration<float>(now - m_previous).count();
        m_previous = now;

        const bool dragging = m_dragging;
        if (!m_wasDragging && dragging && m_settle < 0)
        {
            // Only when nothing is already springing back. A press that interrupts a release
            // inherits the target that release was heading for, because the pose under it is
            // part way home and is not where rest is - grabbing the stone repeatedly while it
            // settles would otherwise leave it a little further off square every time, which
            // is exactly the drift that looked unreproducible.
            m_pressedYaw = m_pose.yaw;
            m_pressedPitch = m_rawPitch;
        }
        else if (m_wasDragging && !dragging && m_motion.settleSeconds > 0)
        {
            // Releasing springs the scene back to where it stood when the press began, not
            // to rest. The beta blends `yaw = M * (1 - t) + E`, with E the pose at the press
            // and M the amount dragged since, so a stone that was already turned returns to
            // being turned. The two coincide only from rest, which is why this looked like a
            // return to zero.
            m_settleYaw = Signed(m_pose.yaw - m_pressedYaw);
            m_settlePitch = m_rawPitch - m_pressedPitch;
            m_settle = 0;
        }

        if (dragging)
        {
            m_settle = -1;
        }

        m_wasDragging = dragging;

        Advance(m_pose, delta, m_settle >= 0);

        if (m_settle >= 0)
        {
            const float before = m_pose.yaw;

            m_settle += delta / m_motion.settleSeconds;

            // Both scaled by the same remainder, so the stone travels back along the arc it
            // was dragged out on rather than unwinding one axis ahead of the other.
            const float remaining = m_settle >= 1 ? 0 : 1.0f - Overshoot(m_settle);

            m_pose.yaw = m_pressedYaw + m_settleYaw * remaining;

            m_rawPitch = m_pressedPitch + m_settlePitch * remaining;
            m_pose.pitch = SoftLimit(m_rawPitch, m_motion.pitchSoftLimitDegrees);

            // The spring is movement like any other, and what reads the turn rate - the
            // anchored sparkle, which only shows on a near-still stone - has to see it.
            m_pose.yawPerSecond = m_pose.step > 0
                ? Signed(m_pose.yaw - before) / m_pose.step : 0;

            if (m_settle >= 1)
            {
                m_settle = -1;
            }
        }

        const float transparent[4] = { 0, 0, 0, 0 };
        m_device->Context()->ClearRenderTargetView(m_backBufferView.get(), transparent);

        SceneTarget target = {};
        target.context = m_device->Context();
        target.view = m_backBufferView.get();
        target.width = m_width;
        target.height = m_height;

        m_scene->Render(target, m_pose);

        // Interval zero, because one refresh is waited for once for every panel - see
        // Scene3DDevice::WaitForVerticalBlank.
        const HRESULT hr = m_swapChain->Present(0, 0);

        if (hr == DXGI_STATUS_OCCLUDED)
        {
            return FrameResult::Occluded;
        }

        if (hr == DXGI_ERROR_DEVICE_REMOVED || hr == DXGI_ERROR_DEVICE_RESET
            || (FAILED(hr) && FAILED(m_device->Device()->GetDeviceRemovedReason())))
        {
            return FrameResult::Lost;
        }

        if (FAILED(hr))
        {
            LOGGER_ERROR(L"Scene3D: Present, 0x{:08X}", static_cast<std::uint32_t>(hr));
            m_failed = true;
            return FrameResult::Failed;
        }

        return FrameResult::Presented;
    }
}
