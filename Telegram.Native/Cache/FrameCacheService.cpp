#include "pch.h"

#include "FrameCacheService.h"
#include "FrameCacheReader.h"
#include "FrameCacheWriter.h"

namespace winrt::Telegram::Native::Cache
{
    FrameCacheService& FrameCacheService::Instance() noexcept
    {
        static FrameCacheService instance;
        return instance;
    }

    FrameCacheService::~FrameCacheService()
    {
        Shutdown();
    }

    FrameCacheService::BuildToken FrameCacheService::Enqueue(std::wstring path, std::weak_ptr<IFrameProducer> producer)
    {
        if (path.empty() || producer.expired())
        {
            return nullptr;
        }

        std::unique_lock lock(m_lock);

        if (m_stopping)
        {
            return nullptr;
        }

        // Someone else already building this key hands back their token: a build is coming, which
        // is what the caller is really asking, and both animations then watch the same flag.
        auto existing = m_pending.find(path);
        if (existing != m_pending.end())
        {
            return existing->second;
        }

        auto token = std::make_shared<std::atomic<bool>>(true);
        m_pending.emplace(path, token);

        m_queue.push_back({ std::move(path), std::move(producer), token });

        if (!m_started)
        {
            m_started = true;
            m_thread = std::thread(&FrameCacheService::ThreadProc, this);
        }

        lock.unlock();
        m_signal.notify_one();

        return token;
    }

    bool FrameCacheService::IsBuilding(const std::wstring& path) const
    {
        std::lock_guard lock(m_lock);
        return m_pending.find(path) != m_pending.end();
    }

    void FrameCacheService::Shutdown()
    {
        {
            std::lock_guard lock(m_lock);
            if (!m_started)
            {
                return;
            }

            m_stopping = true;
            m_queue.clear();

            // Nothing more will be built, so nobody should still be waiting on a token.
            for (auto& pending : m_pending)
            {
                pending.second->store(false, std::memory_order_relaxed);
            }

            m_pending.clear();
        }

        m_signal.notify_all();

        if (m_thread.joinable())
        {
            m_thread.join();
        }

        m_started = false;
        m_stopping = false;
    }

    void FrameCacheService::ThreadProc()
    {
        for (;;)
        {
            Item item;

            {
                std::unique_lock lock(m_lock);
                m_signal.wait(lock, [this] { return m_stopping || !m_queue.empty(); });

                if (m_stopping)
                {
                    return;
                }

                // Newest first. A scroll queues a build per sticker it passes, and the ones the
                // user has already gone by are worth nothing: taking from the back means the queue
                // follows the scroll instead of trailing it, and the sticker on screen is not stuck
                // behind a hundred it has left. The original s_compressQueue was a std::stack for
                // this reason, and AnimatedImageLoader's own work queue is LIFO for the same one.
                //
                // The backlog is not lost, only deferred: it drains oldest-last once the scrolling
                // stops and nothing new is being queued.
                item = std::move(m_queue.back());
                m_queue.pop_back();
            }

            Build(item);

            if (item.token)
            {
                item.token->store(false, std::memory_order_relaxed);
            }

            {
                std::unique_lock lock(m_lock);
                m_pending.erase(item.path);

                if (m_queue.empty())
                {
                    lock.unlock();

                    // Nothing left to build, so the scratch is dead weight until the next one.
                    Trim();
                }
            }
        }
    }

    void FrameCacheService::Build(const Item& item)
    {
        // The cancellation. A queued build whose animation has gone has rendered nothing, so
        // dropping it costs nothing - which is why cancellation only ever happens here and never
        // part-way through.
        auto producer = item.producer.lock();
        if (producer == nullptr)
        {
            return;
        }

        auto width = producer->PixelWidth();
        auto height = producer->PixelHeight();

        if (width == 0 || height == 0)
        {
            return;
        }

        // Someone else - another process, or a previous session - may have finished it since this
        // was queued. Opening is the cheapest way to ask, and it is the same check the reader does.
        {
            FrameCacheReader existing;
            if (existing.Open(item.path, width, height))
            {
                return;
            }
        }

        if (!producer->Prepare())
        {
            return;
        }

        auto pixels = static_cast<size_t>(width) * height * 4;
        auto scratch = FrameCacheWriter::ScratchSize(width, height);

        if (scratch == 0)
        {
            producer->Release();
            return;
        }

        if (m_pixels.size() < pixels)
        {
            m_pixels.resize(pixels);
        }

        if (m_scratch.size() < scratch)
        {
            m_scratch.resize(scratch);
        }

        FrameCacheWriter writer;
        if (!writer.Begin(item.path, width, height, producer->FrameRate(), producer->Rotation()))
        {
            producer->Release();
            return;
        }

        producer->Reset();

        auto ok = true;
        float timestamp = 0;

        while (producer->NextFrame(m_pixels.data(), pixels, timestamp))
        {
            if (!writer.Write(m_pixels.data(), pixels, timestamp, m_scratch.data(), m_scratch.size()))
            {
                ok = false;
                break;
            }
        }

        producer->Release();

        if (ok)
        {
            // Commit renames into place, and fails harmlessly if another builder got there first.
            writer.Commit();
        }
        else
        {
            writer.Cancel();
        }
    }

    void FrameCacheService::Trim()
    {
        m_pixels.clear();
        m_pixels.shrink_to_fit();

        m_scratch.clear();
        m_scratch.shrink_to_fit();
    }
}
