#include "pch.h"
#include "Scene3DDevice.h"
#include "Scene3DRenderer.h"

#include <winrt/Telegram.Native.h>

#include <algorithm>
#include <format>

namespace Graphics3D
{
    // The LOGGER_ macros name both unqualified, and outside winrt::Telegram::Native nothing else
    // brings them into scope.
    using winrt::hstring;
    using winrt::Telegram::Native::NativeUtils;

    void LogException(const wchar_t* where) noexcept
    {
        try
        {
            try
            {
                throw;
            }
            catch (winrt::hresult_error const& error)
            {
                LOGGER_ERROR(L"{}: 0x{:08X} {}", where,
                    static_cast<std::uint32_t>(error.code().value), error.message());
            }
            catch (std::exception const& error)
            {
                LOGGER_ERROR(L"{}: {}", where, winrt::to_hstring(error.what()));
            }
            catch (...)
            {
                LOGGER_ERROR(L"{}: unknown exception", where);
            }
        }
        catch (...)
        {
        }
    }

    namespace
    {
        // How many times a lost device is recreated before the panels are given their fallback.
        // With the backoff doubling from a quarter of a second to four, about twenty seconds.
        constexpr int MaximumRecoveryAttempts = 8;

        // The Microsoft Basic Render Driver, which is WARP behind a hardware driver type: what a
        // machine with no display driver, and many remote sessions, hand out. Not every build
        // flags it as software.
        constexpr UINT BasicRenderVendor = 0x1414;
        constexpr UINT BasicRenderDevice = 0x8C;

        winrt::com_ptr<IDXGIOutput> FindOutput(IDXGIAdapter1* adapter, IDXGIFactory2* factory)
        {
            winrt::com_ptr<IDXGIOutput> output;

            if (adapter != nullptr && SUCCEEDED(adapter->EnumOutputs(0, output.put())))
            {
                return output;
            }

            // A hybrid laptop renders on the discrete adapter and scans out from the integrated
            // one, so the adapter the device is on can have no output of its own.
            if (factory != nullptr)
            {
                winrt::com_ptr<IDXGIAdapter1> other;
                for (UINT i = 0; SUCCEEDED(factory->EnumAdapters1(i, other.put())); ++i)
                {
                    output = nullptr;
                    if (SUCCEEDED(other->EnumOutputs(0, output.put())))
                    {
                        return output;
                    }

                    other = nullptr;
                }
            }

            return nullptr;
        }
    }

    std::shared_ptr<Scene3DDevice> Scene3DDevice::Acquire() noexcept
    {
        static std::mutex s_lock;
        static std::weak_ptr<Scene3DDevice> s_instance;
        static bool s_unsupported = false;

        try
        {
            std::scoped_lock lock(s_lock);

            if (auto instance = s_instance.lock())
            {
                return instance;
            }

            if (s_unsupported)
            {
                return nullptr;
            }

            Resources resources;
            if (!Create(resources))
            {
                s_unsupported = true;
                return nullptr;
            }

            std::shared_ptr<Scene3DDevice> instance(new Scene3DDevice());

            {
                std::scoped_lock instanceLock(instance->m_lock);
                instance->Install(std::move(resources));
            }

            instance->m_thread = std::thread([raw = instance.get()] { raw->Run(); });

            s_instance = instance;
            return instance;
        }
        catch (...)
        {
            // Not remembered as unsupported: running out of memory or threads says nothing about
            // the adapter.
            LogException(L"Scene3D: Acquire");
            return nullptr;
        }
    }

    Scene3DDevice::~Scene3DDevice()
    {
        {
            std::scoped_lock lock(m_lock);
            m_stopping = true;
        }

        m_wake.notify_all();

        if (m_thread.joinable())
        {
            m_thread.join();
        }
    }

    bool Scene3DDevice::Create(Resources& resources)
    {
        UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
#if defined(_DEBUG)
        flags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

        // Eleven only: the shaders are compiled for shader model 5, which a 10.x device refuses
        // at CreatePixelShader - long after a device that looked usable had been handed out.
        const D3D_FEATURE_LEVEL levels[] =
        {
            D3D_FEATURE_LEVEL_11_1,
            D3D_FEATURE_LEVEL_11_0
        };

        winrt::com_ptr<ID3D11Device> device;
        winrt::com_ptr<ID3D11DeviceContext> context;

        HRESULT hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
            levels, ARRAYSIZE(levels), D3D11_SDK_VERSION,
            device.put(), nullptr, context.put());

        if (FAILED(hr))
        {
            // The debug layer is absent unless the graphics tools feature is installed.
            device = nullptr;
            context = nullptr;

            flags &= ~static_cast<UINT>(D3D11_CREATE_DEVICE_DEBUG);
            hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
                levels, ARRAYSIZE(levels), D3D11_SDK_VERSION,
                device.put(), nullptr, context.put());
        }

        if (FAILED(hr))
        {
            LOGGER_WARNING(L"Scene3D: no feature level 11 hardware device, 0x{:08X}",
                static_cast<std::uint32_t>(hr));
            return false;
        }

        try
        {
            winrt::com_ptr<IDXGIAdapter> adapter;
            winrt::check_hresult(device.as<IDXGIDevice>()->GetAdapter(adapter.put()));

            auto adapter1 = adapter.as<IDXGIAdapter1>();

            DXGI_ADAPTER_DESC1 desc = {};
            winrt::check_hresult(adapter1->GetDesc1(&desc));

            // A pixel shader of thirteen hundred instruction slots run on the CPU, at vsync, for
            // every panel on screen. Better blank than that.
            if ((desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE)
                || (desc.VendorId == BasicRenderVendor && desc.DeviceId == BasicRenderDevice))
            {
                LOGGER_WARNING(L"Scene3D: refusing software adapter {}", desc.Description);
                return false;
            }

            winrt::com_ptr<IDXGIFactory2> factory;
            winrt::check_hresult(adapter1->GetParent(winrt::guid_of<IDXGIFactory2>(),
                factory.put_void()));

            resources.device = std::move(device);
            resources.context = std::move(context);
            resources.adapter = std::move(adapter1);
            resources.factory = std::move(factory);
            return true;
        }
        catch (...)
        {
            LogException(L"Scene3D: no adapter behind the device");
            return false;
        }
    }

    void Scene3DDevice::Install(Resources&& resources)
    {
        m_device = std::move(resources.device);
        m_context = std::move(resources.context);
        m_adapter = std::move(resources.adapter);
        m_factory = std::move(resources.factory);

        ++m_generation;
        m_lost = false;

        // Found again by the thread, which is the only one that waits on it.
        m_output = nullptr;
        m_nextOutputSearch = {};
    }

    void Scene3DDevice::Register(Scene3DRenderer* renderer)
    {
        if (std::find(m_renderers.begin(), m_renderers.end(), renderer) == m_renderers.end())
        {
            m_renderers.push_back(renderer);
        }
    }

    void Scene3DDevice::Unregister(Scene3DRenderer* renderer)
    {
        SetActive(renderer, false);

        const auto it = std::find(m_renderers.begin(), m_renderers.end(), renderer);
        if (it != m_renderers.end())
        {
            m_renderers.erase(it);
        }
    }

    void Scene3DDevice::SetActive(Scene3DRenderer* renderer, bool active)
    {
        if (renderer->m_active == active)
        {
            return;
        }

        renderer->m_active = active;

        if (active)
        {
            ++m_active;
            m_wake.notify_all();
        }
        else
        {
            --m_active;
        }
    }

    void Scene3DDevice::MarkLost(std::uint64_t generation)
    {
        if (m_lost || generation != m_generation)
        {
            return;
        }

        LOGGER_ERROR(L"Scene3D: device lost, 0x{:08X}", static_cast<std::uint32_t>(
            m_device != nullptr ? m_device->GetDeviceRemovedReason() : E_FAIL));

        m_lost = true;
        m_wake.notify_all();

        // The panels are not told yet. A panel rebuilding now could drop the last reference to
        // this object, and the next Acquire would create a device straight away on the UI thread,
        // inside the reset the backoff in Recover exists to wait out.
    }

    void Scene3DDevice::NotifyReplaced()
    {
        for (auto renderer : m_renderers)
        {
            renderer->NotifyReplaced();
        }
    }

    void Scene3DDevice::Abandon()
    {
        m_abandoned = true;

        for (auto renderer : m_renderers)
        {
            SetActive(renderer, false);
            renderer->NotifyFailed();
        }
    }

    void Scene3DDevice::Run()
    {
        std::unique_lock lock(m_lock);

        while (true)
        {
            try
            {
                m_wake.wait(lock, [this]
                {
                    return m_stopping || (!m_abandoned && (m_lost || m_active > 0));
                });

                if (m_stopping)
                {
                    break;
                }

                if (m_lost)
                {
                    Recover(lock);
                    continue;
                }

                const auto now = std::chrono::steady_clock::now();

                bool presented = false;
                bool visible = false;

                for (const auto renderer : m_renderers)
                {
                    // Once one frame has found the device gone, nothing else may draw on it.
                    if (m_lost)
                    {
                        break;
                    }

                    if (!renderer->m_active || renderer->Generation() != m_generation)
                    {
                        continue;
                    }

                    Scene3DRenderer::FrameResult result;

                    try
                    {
                        result = renderer->Frame(now);
                    }
                    catch (...)
                    {
                        LogException(L"Scene3D: frame");
                        renderer->m_failed = true;
                        result = Scene3DRenderer::FrameResult::Failed;
                    }

                    switch (result)
                    {
                    case Scene3DRenderer::FrameResult::Presented:
                        presented = true;
                        visible = true;
                        break;
                    case Scene3DRenderer::FrameResult::Occluded:
                        presented = true;
                        break;
                    case Scene3DRenderer::FrameResult::Failed:
                        SetActive(renderer, false);
                        renderer->NotifyFailed();
                        break;
                    case Scene3DRenderer::FrameResult::Lost:
                        MarkLost(m_generation);
                        break;
                    case Scene3DRenderer::FrameResult::Idle:
                        break;
                    }
                }

                if (m_lost)
                {
                    continue;
                }

                lock.unlock();

                if (visible)
                {
                    WaitForVerticalBlank();
                }
                else if (presented)
                {
                    // Nothing is on screen, so nothing is worth a frame a refresh.
                    std::this_thread::sleep_for(std::chrono::milliseconds(100));
                }
                else
                {
                    // No size yet, or a target that could not be made: nothing to present, and
                    // looping on it would burn a core.
                    std::this_thread::sleep_for(std::chrono::milliseconds(16));
                }

                lock.lock();
            }
            catch (...)
            {
                // Leaving this thread with an exception ends the process. A renderer that throws
                // is failed above; this is for the loop itself, which carries on after a pause
                // rather than spinning on whatever threw.
                LogException(L"Scene3D: render loop");

                if (lock.owns_lock())
                {
                    lock.unlock();
                }

                std::this_thread::sleep_for(std::chrono::milliseconds(100));
                lock.lock();
            }
        }
    }

    void Scene3DDevice::Recover(std::unique_lock<std::mutex>& lock)
    {
        if (m_context != nullptr)
        {
            m_context->ClearState();
            m_context->Flush();
        }

        m_cache.Clear();
        m_context = nullptr;
        m_device = nullptr;
        m_adapter = nullptr;
        m_factory = nullptr;
        m_output = nullptr;

        auto backoff = std::chrono::milliseconds(250);

        for (int attempt = 1; !m_stopping; ++attempt)
        {
            // Not at once: the driver may still be resetting, and creating a device inside that
            // window faults in the vendor's user-mode driver - see
            // Direct2DDevice::OnDirect3DDeviceLost.
            if (m_wake.wait_for(lock, backoff, [this] { return m_stopping; }))
            {
                return;
            }

            Resources resources;

            lock.unlock();
            const bool created = Create(resources);
            lock.lock();

            if (m_stopping)
            {
                return;
            }

            if (created)
            {
                LOGGER_WARNING(L"Scene3D: device recovered after {} attempts", attempt);

                Install(std::move(resources));
                NotifyReplaced();
                return;
            }

            if (attempt >= MaximumRecoveryAttempts)
            {
                LOGGER_ERROR(L"Scene3D: device not recovered after {} attempts", attempt);

                Abandon();
                return;
            }

            backoff = std::min(backoff * 2, std::chrono::milliseconds(4000));
        }
    }

    /// <summary>
    /// Waits for the next refresh of the display, which is what paces every panel at once.
    /// </summary>
    /// <remarks>
    /// Not a vsynced Present, which is what one panel with its own thread could use: here each
    /// Present(1) would wait for a refresh of its own, so N panels would run at a refresh rate
    /// divided by N. Every swap chain presents with an interval of zero instead - which on a flip
    /// model swap chain still never tears, and lets a newer frame replace one not yet shown - and
    /// the loop waits once.
    ///
    /// For the same reason the device keeps its default frame latency. One frame, which is what
    /// stops a dragged stone trailing the pointer, is what this pacing gives anyway, while
    /// SetMaximumFrameLatency(1) would make each Present wait for the previous panel's frame to
    /// finish on the GPU.
    ///
    /// The output is the primary one, so a panel on a second monitor at a different refresh rate
    /// runs at the primary's.
    /// </remarks>
    void Scene3DDevice::WaitForVerticalBlank()
    {
        const auto now = std::chrono::steady_clock::now();

        if (m_output == nullptr && now >= m_nextOutputSearch)
        {
            m_nextOutputSearch = now + std::chrono::seconds(1);

            std::scoped_lock lock(m_lock);
            m_output = FindOutput(m_adapter.get(), m_factory.get());
        }

        if (m_output != nullptr)
        {
            if (FAILED(m_output->WaitForVBlank()))
            {
                // Usually an output that has gone away. Looked for again in a second.
                m_output = nullptr;
            }
            else
            {
                const auto previous = m_lastVerticalBlank;
                m_lastVerticalBlank = std::chrono::steady_clock::now();

                // A display that is asleep stops blocking, and two blanks this close together are
                // not a refresh rate anybody has.
                if (m_lastVerticalBlank - previous >= std::chrono::milliseconds(2))
                {
                    return;
                }
            }
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(16));
    }
}
