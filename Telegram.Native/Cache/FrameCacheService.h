#pragma once

#include <atomic>
#include <condition_variable>
#include <deque>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>

#include "FrameProducer.h"

namespace winrt::Telegram::Native::Cache
{
    // The one place cache files get built, for every animation kind. Replaces the two independent
    // copies of this machinery that lived in CachedVideoAnimation.cpp and LottieAnimation.cpp.
    //
    // **Lifetime is the whole design.** An item holds a weak_ptr to its producer, so a build that
    // has not started yet is dropped when the animation behind it goes - that is the cancellation,
    // and it costs nothing because a queued item has rendered no frames. Once a build starts the
    // service holds a shared_ptr for its duration, which is the borrow rule: the owner can release
    // whenever it likes and the producer dies when the last borrow ends, never underneath a build.
    // A started build always finishes, deliberately: redoing it when the user scrolls back a few
    // seconds later costs more than letting it run.
    //
    // Scratch buffers belong to the service, not to the animations, so memory is bounded by
    // concurrency - one build at a time - rather than by how many builds are queued. They are
    // released when the queue drains.
    class FrameCacheService
    {
    public:
        static FrameCacheService& Instance() noexcept;

        /// <summary>
        /// Queues a build, and reports whether one is coming - which includes the case where
        /// another animation already queued this key, because from the caller's side that is the
        /// same answer. Only a refusal to build at all returns false.
        /// </summary>
        /// <remarks>
        /// The distinction matters: a caller that treats "already pending" as failure and keeps
        /// asking costs a dropped frame per retry, and one that treats it as failure and gives up
        /// never gets a cache at all. The same emoji is on screen in twenty places at once, so this
        /// is the common path, not the rare one.
        /// </remarks>
        /// <summary>
        /// The token an animation watches instead of asking. True while the build is queued or
        /// running, false once it has finished or been dropped; null when nothing was queued.
        /// </summary>
        /// <remarks>
        /// A token rather than a query because the question is asked once per animation per frame -
        /// a couple of hundred times a frame with a panel open. IsBuilding takes this service's
        /// mutex and hashes the whole path, which put a global lock on the render worker; an atomic
        /// load costs nothing and cannot contend with the build thread.
        /// </remarks>
        using BuildToken = std::shared_ptr<std::atomic<bool>>;

        BuildToken Enqueue(std::wstring path, std::weak_ptr<IFrameProducer> producer);

        bool IsBuilding(const std::wstring& path) const;

        void Shutdown();

    private:
        FrameCacheService() = default;
        ~FrameCacheService();

        FrameCacheService(const FrameCacheService&) = delete;
        FrameCacheService& operator=(const FrameCacheService&) = delete;

        struct Item
        {
            std::wstring path;
            std::weak_ptr<IFrameProducer> producer;
            BuildToken token;
        };

        void ThreadProc();
        void Build(const Item& item);
        void Trim();

        mutable std::mutex m_lock;
        std::condition_variable m_signal;

        std::deque<Item> m_queue;
        std::unordered_map<std::wstring, BuildToken> m_pending;

        std::thread m_thread;
        bool m_started{ false };
        bool m_stopping{ false };

        // Worker thread only.
        std::vector<uint8_t> m_pixels;
        std::vector<uint8_t> m_scratch;
    };
}
