#pragma once

#include "GarbageCollectionMonitor.g.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.System.h>

#include <thread>
#include <mutex>
#include <atomic>
#include <unordered_map>
#include <chrono>
#include <functional>

using PFN_RhGetCurrentObjSize = INT64(__fastcall*)();
using PFN_RhCollect = void(__fastcall*)(int generation, int mode);

using namespace winrt::Windows::UI::Xaml;
using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::UI::Core;
using namespace winrt::Windows::System;

template<typename Func>
inline void post_to_threadpool(Func&& func)
{
    auto* heapFunc = new std::decay_t<Func>(std::forward<Func>(func));

    PTP_WORK work = CreateThreadpoolWork(
        [](PTP_CALLBACK_INSTANCE, PVOID context, PTP_WORK) {
            std::unique_ptr<std::decay_t<Func>> funcPtr(
                static_cast<std::decay_t<Func>*>(context)
            );
            (*funcPtr)();
        },
        heapFunc,
        nullptr
    );

    SubmitThreadpoolWork(work);
    CloseThreadpoolWork(work);
}

namespace winrt::Telegram::Native::implementation
{
    struct GarbageCollectionMonitor : GarbageCollectionMonitorT<GarbageCollectionMonitor>
    {
        using TimeSpan = std::chrono::milliseconds;
        static TimeSpan InactivityTimeout;
        static TimeSpan DebounceDelay;
        static TimeSpan CheckIntervalActive;
        static TimeSpan CheckIntervalInactive;

        static void Initialize(CollectCallback collectCallback, bool disableGcCollect, bool disablePressure);
        static void StartMonitoring(CoreWindow const& window);
        static void StopMonitoring(CoreWindow const& window);
        static void DisconnectUnusedReferenceSources();
        static hstring Debug();

    private:
        struct WindowState
        {
            CoreWindow Window{ nullptr };
            std::atomic<bool> IsWindowActive{ false };
            event_token ActivatedToken{};
            event_token ClosedToken{};

            WindowState() = default;
            explicit WindowState(CoreWindow const& window)
                : Window(window), IsWindowActive(false)
            {
            }

            WindowState(WindowState&& other) noexcept
                : Window(std::move(other.Window))
                , IsWindowActive(other.IsWindowActive.load(std::memory_order_relaxed))
                , ActivatedToken(other.ActivatedToken)
                , ClosedToken(other.ClosedToken)
            {
                other.Window = nullptr;
                other.ActivatedToken = {};
                other.ClosedToken = {};
            }

            WindowState& operator=(WindowState&& other) noexcept
            {
                if (this != &other)
                {
                    Window = std::move(other.Window);
                    IsWindowActive.store(other.IsWindowActive.load(std::memory_order_relaxed), std::memory_order_relaxed);
                    ActivatedToken = other.ActivatedToken;
                    ClosedToken = other.ClosedToken;

                    other.Window = nullptr;
                    other.ActivatedToken = {};
                    other.ClosedToken = {};
                }
                return *this;
            }

            WindowState(const WindowState&) = delete;
            WindowState& operator=(const WindowState&) = delete;
        };

        static void MonitorThreadProc();
        static bool IsAnyWindowActive();
        static void TryTriggerCollection();
        static void Collect();
        static void Detour(bool value);

        static std::mutex s_syncLock;
        static std::unordered_map<void*, WindowState> s_windowStates;
        static std::thread s_monitorThread;
        static std::atomic<bool> s_xamlCollectionRequested;
        static std::atomic<int64_t> s_lastCollectionTicks;
        static std::atomic<int32_t> s_requested;
        static std::atomic<int32_t> s_count;
        static CollectCallback s_collectCallback;
        static DispatcherQueue s_dispatcher;

        static Application::Suspending_revoker s_suspending;
        static Application::Resuming_revoker s_resuming;

        static PFN_RhGetCurrentObjSize s_RhGetCurrentObjSize;
        static PFN_RhCollect s_RhCollect;

        static std::mutex s_collectLock;
        static bool s_collect;
        static bool s_suspended;

        static INT64 RhGetCurrentObjSize()
        {
            return 0x7FFFFFFFFFFFFFFF;
        }

        static void RhCollect(int generation, int mode)
        {
            post_to_threadpool([&]() { DisconnectUnusedReferenceSources(); });
        }

        static void OnSuspending(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::ApplicationModel::SuspendingEventArgs const& e);
        static void OnResuming(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::Foundation::IInspectable const& e);
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct GarbageCollectionMonitor : GarbageCollectionMonitorT<GarbageCollectionMonitor, implementation::GarbageCollectionMonitor>
    {
    };
}
