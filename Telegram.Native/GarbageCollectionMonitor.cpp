#include "pch.h"
#include "GarbageCollectionMonitor.h"
#if __has_include("GarbageCollectionMonitor.g.cpp")
#include "GarbageCollectionMonitor.g.cpp"
#endif

#include <detours.h>

namespace winrt::Telegram::Native::implementation
{
    std::mutex GarbageCollectionMonitor::s_syncLock;
    std::unordered_map<void*, GarbageCollectionMonitor::WindowState> GarbageCollectionMonitor::s_windowStates;
    std::thread GarbageCollectionMonitor::s_monitorThread;
    std::atomic<bool> GarbageCollectionMonitor::s_xamlCollectionRequested{ false };
    std::atomic<int64_t> GarbageCollectionMonitor::s_lastCollectionTicks{ 0 };
    std::atomic<int32_t> GarbageCollectionMonitor::s_requested{ 0 };
    std::atomic<int32_t> GarbageCollectionMonitor::s_count{ 0 };
    CollectCallback GarbageCollectionMonitor::s_collectCallback;
    DispatcherQueue GarbageCollectionMonitor::s_dispatcher{ nullptr };

    GarbageCollectionMonitor::TimeSpan GarbageCollectionMonitor::InactivityTimeout{ 2000 };
    GarbageCollectionMonitor::TimeSpan GarbageCollectionMonitor::DebounceDelay{ 500 };
    GarbageCollectionMonitor::TimeSpan GarbageCollectionMonitor::CheckIntervalActive{ 250 };
    GarbageCollectionMonitor::TimeSpan GarbageCollectionMonitor::CheckIntervalInactive{ 1000 };

    void GarbageCollectionMonitor::Initialize(CollectCallback collectCallback, bool disableGcCollect, bool disablePressure)
    {
        if (s_collectCallback)
        {
            return;
        }

        s_collectCallback = std::move(collectCallback);
        s_dispatcher = DispatcherQueue::GetForCurrentThread();
        s_monitorThread = std::thread(MonitorThreadProc);

        auto mrt100 = GetModuleHandle(L"mrt100_app.dll");
        if (mrt100)
        {
            if (disableGcCollect)
            {
                s_RhCollect = reinterpret_cast<PFN_RhCollect>(GetProcAddress(mrt100, "RhCollect"));
            }

            if (disablePressure)
            {
                s_RhGetCurrentObjSize = reinterpret_cast<PFN_RhGetCurrentObjSize>(GetProcAddress(mrt100, "RhGetCurrentObjSize"));
            }

            if (s_RhCollect || s_RhGetCurrentObjSize)
            {
                DetourTransactionBegin();
                DetourUpdateThread(GetCurrentThread());

                if (s_RhGetCurrentObjSize)
                {
                    DetourAttach(reinterpret_cast<PVOID*>(&s_RhGetCurrentObjSize), GarbageCollectionMonitor::RhGetCurrentObjSize);
                }

                if (s_RhCollect)
                {
                    DetourAttach(reinterpret_cast<PVOID*>(&s_RhCollect), GarbageCollectionMonitor::RhCollect);
                }

                DetourTransactionCommit();

                if (s_RhCollect)
                {
                    s_suspending = Application::Current().Suspending(winrt::auto_revoke, &GarbageCollectionMonitor::OnSuspending);
                    s_resuming = Application::Current().Resuming(winrt::auto_revoke, &GarbageCollectionMonitor::OnResuming);
                }
            }
        }
    }

    void GarbageCollectionMonitor::MonitorThreadProc()
    {
        while (true)
        {
            // Determine check interval based on window activity
            bool anyWindowActive = IsAnyWindowActive();
            TimeSpan checkInterval = anyWindowActive ? CheckIntervalActive : CheckIntervalInactive;

            std::this_thread::sleep_for(checkInterval);

            TryTriggerCollection();
        }
    }

    void GarbageCollectionMonitor::StartMonitoring(CoreWindow const& window)
    {
        void* windowKey = winrt::get_abi(window);
        WindowState* state = nullptr;
        bool isNewWindow = false;

        {
            std::lock_guard lock(s_syncLock);
            auto it = s_windowStates.find(windowKey);
            if (it != s_windowStates.end())
            {
                // Already monitoring this window
                return;
            }

            auto [insertIt, inserted] = s_windowStates.emplace(windowKey, WindowState(window));
            state = &insertIt->second;
            isNewWindow = inserted;
        }

        if (isNewWindow && state)
        {
            state->ActivatedToken = window.Activated([state](CoreWindow const&, WindowActivatedEventArgs const& args)
                {
                    state->IsWindowActive.store(args.WindowActivationState() != CoreWindowActivationState::Deactivated,
                        std::memory_order_relaxed);
                });

            state->ClosedToken = window.Closed([window](CoreWindow const&, CoreWindowEventArgs const&)
                {
                    StopMonitoring(window);
                });

            state->IsWindowActive.store(true, std::memory_order_relaxed);
        }
    }

    void GarbageCollectionMonitor::StopMonitoring(CoreWindow const& window)
    {
        void* windowKey = winrt::get_abi(window);
        WindowState state;
        bool found = false;

        {
            std::lock_guard lock(s_syncLock);
            auto it = s_windowStates.find(windowKey);
            if (it == s_windowStates.end())
            {
                return;
            }

            state = std::move(it->second);
            s_windowStates.erase(it);
            found = true;
        }

        if (found && state.Window)
        {
            if (state.ActivatedToken)
            {
                state.Window.Activated(state.ActivatedToken);
            }
            if (state.ClosedToken)
            {
                state.Window.Closed(state.ClosedToken);
            }
        }
    }

    void GarbageCollectionMonitor::DisconnectUnusedReferenceSources()
    {
        s_requested.fetch_add(1, std::memory_order_relaxed);

        auto usageLevel = MemoryManager::AppMemoryUsageLevel();
        if (usageLevel == AppMemoryUsageLevel::High || usageLevel == AppMemoryUsageLevel::OverLimit)
        {
            int64_t currentTicks = static_cast<int64_t>(GetTickCount64());
            s_lastCollectionTicks.store(currentTicks, std::memory_order_relaxed);

            int32_t count = s_count.fetch_add(1, std::memory_order_relaxed) + 1;
            LOGGER_INFO(L"{}/{}", count, s_requested.load(std::memory_order_relaxed));

            if (s_dispatcher)
            {
                s_dispatcher.TryEnqueue(Collect);
            }
        }
        else
        {
            s_xamlCollectionRequested.store(true, std::memory_order_relaxed);
        }
    }

    bool GarbageCollectionMonitor::IsAnyWindowActive()
    {
        std::lock_guard lock(s_syncLock);
        for (auto const& [key, state] : s_windowStates)
        {
            if (state.IsWindowActive.load(std::memory_order_relaxed))
            {
                return true;
            }
        }
        return false;
    }

    void GarbageCollectionMonitor::TryTriggerCollection()
    {
        if (!s_xamlCollectionRequested.load(std::memory_order_relaxed))
        {
            return;
        }

        int64_t currentTicks = static_cast<int64_t>(GetTickCount64());
        int64_t lastCollectionTicks = s_lastCollectionTicks.load(std::memory_order_relaxed);
        int64_t timeSinceLastCollection = currentTicks - lastCollectionTicks;

        if (timeSinceLastCollection < static_cast<int64_t>(DebounceDelay.count()))
        {
            return;
        }

        bool anyWindowActive = IsAnyWindowActive();
        if (anyWindowActive)
        {
            uint32_t lastInputTime = NativeUtils::GetLastInputTime();
            uint32_t timeSinceInput = currentTicks >= lastInputTime
                ? static_cast<uint32_t>(currentTicks - lastInputTime)
                : 0; // Handle wraparound conservatively

            if (timeSinceInput < static_cast<uint32_t>(InactivityTimeout.count()))
            {
                return; // User is still active
            }
        }
        else
        {
            if (timeSinceLastCollection < static_cast<int64_t>(InactivityTimeout.count()))
            {
                return;
            }
        }

        s_lastCollectionTicks.store(currentTicks, std::memory_order_relaxed);
        s_xamlCollectionRequested.store(false, std::memory_order_relaxed);

        int32_t count = s_count.fetch_add(1, std::memory_order_relaxed) + 1;
        LOGGER_INFO(L"{}/{}", count, s_requested.load(std::memory_order_relaxed));

        if (s_dispatcher)
        {
            s_dispatcher.TryEnqueue(Collect);
        }
    }

    void GarbageCollectionMonitor::Collect()
    {
        if (s_collectCallback)
        {
            Detour(true);
            try
            {
                s_collectCallback();
            }
            catch (...)
            {
                // Swallow exceptions to prevent crashes
            }
            Detour(false);
        }
    }

    hstring GarbageCollectionMonitor::Debug()
    {
        bool xamlRequested = s_xamlCollectionRequested.load(std::memory_order_relaxed);
        int32_t count = s_count.load(std::memory_order_relaxed);
        int32_t requested = s_requested.load(std::memory_order_relaxed);

        return hstring(std::format(L" {}/{}{}", count, requested, xamlRequested ? L"*" : L""));
    }

    PFN_RhGetCurrentObjSize GarbageCollectionMonitor::s_RhGetCurrentObjSize;
    PFN_RhCollect GarbageCollectionMonitor::s_RhCollect;

    Application::Suspending_revoker GarbageCollectionMonitor::s_suspending = {};
    Application::Resuming_revoker GarbageCollectionMonitor::s_resuming = {};

    std::mutex GarbageCollectionMonitor::s_collectLock;
    bool GarbageCollectionMonitor::s_collect = false;
    bool GarbageCollectionMonitor::s_suspended = false;

    void GarbageCollectionMonitor::Detour(bool value)
    {
        std::lock_guard const guard(s_collectLock);

        if (value == s_collect || s_suspended || !s_RhCollect)
        {
            return;
        }

        s_collect = value;

        auto mrt100 = GetModuleHandle(L"mrt100_app.dll");
        if (mrt100 && s_RhCollect)
        {
            DetourTransactionBegin();
            DetourUpdateThread(GetCurrentThread());

            if (value)
            {
                DetourDetach(reinterpret_cast<PVOID*>(&s_RhCollect), GarbageCollectionMonitor::RhCollect);
            }
            else
            {
                DetourAttach(reinterpret_cast<PVOID*>(&s_RhCollect), GarbageCollectionMonitor::RhCollect);
            }

            DetourTransactionCommit();
        }
    }

    void GarbageCollectionMonitor::OnSuspending(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::ApplicationModel::SuspendingEventArgs const& e)
    {
        std::lock_guard const guard(s_collectLock);

        if (s_suspended || !s_RhCollect)
        {
            return;
        }

        s_suspended = true;

        auto mrt100 = GetModuleHandle(L"mrt100_app.dll");
        if (mrt100 && s_RhCollect)
        {
            DetourTransactionBegin();
            DetourUpdateThread(GetCurrentThread());
            DetourDetach(reinterpret_cast<PVOID*>(&s_RhCollect), GarbageCollectionMonitor::RhCollect);
            DetourTransactionCommit();
        }
    }

    void GarbageCollectionMonitor::OnResuming(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::Foundation::IInspectable const& e)
    {
        std::lock_guard const guard(s_collectLock);

        if (!s_suspended || !s_RhCollect)
        {
            return;
        }

        s_suspended = false;

        auto mrt100 = GetModuleHandle(L"mrt100_app.dll");
        if (mrt100 && s_RhCollect)
        {
            DetourTransactionBegin();
            DetourUpdateThread(GetCurrentThread());
            DetourAttach(reinterpret_cast<PVOID*>(&s_RhCollect), GarbageCollectionMonitor::RhCollect);
            DetourTransactionCommit();
        }
    }
}
