#pragma once

#include <atomic>
#include <chrono>
#include <cmath>
#include <condition_variable>
#include <deque>
#include <limits>
#include <memory>
#include <mutex>

extern "C"
{
#include <libavcodec/avcodec.h>
#include <libavutil/frame.h>
#include <libavutil/rational.h>
}

#ifndef CREATE_WAITABLE_TIMER_HIGH_RESOLUTION
#define CREATE_WAITABLE_TIMER_HIGH_RESOLUTION 0x00000002
#endif

namespace winrt::Telegram::Native::Media::implementation
{
    struct MediaVideoFormat
    {
        // Clockwise, in degrees: 0, 90, 180 or 270.
        int32_t rotation{ 0 };

        // Zero when the pixels are square.
        AVRational aspect{ 0, 1 };
    };

    // What a decoder needs to decode the packets of one stream of one open file. A new one comes
    // with every file opened, so a decoder knows to start over when the quality changes.
    struct MediaStreamSetup;

    inline double MediaNow() noexcept
    {
        using namespace std::chrono;
        return duration<double>(steady_clock::now().time_since_epoch()).count();
    }

    // What the engine's threads sleep on. Not a condition variable: those waits are rounded to the
    // system tick, 15.6 ms unless something in the process raised the timer resolution, which is
    // a whole frame at 60 fps. Auto-reset, so a signal raised while nobody waits is kept for the
    // next wait instead of lost.
    class MediaWaiter
    {
    public:
        MediaWaiter() noexcept
        {
            m_event = CreateEventExW(nullptr, nullptr, 0, EVENT_ALL_ACCESS);
            m_timer = CreateWaitableTimerExW(nullptr, nullptr, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);
        }

        ~MediaWaiter() noexcept
        {
            if (m_timer != nullptr)
            {
                CloseHandle(m_timer);
            }

            if (m_event != nullptr)
            {
                CloseHandle(m_event);
            }
        }

        MediaWaiter(const MediaWaiter&) = delete;
        MediaWaiter& operator=(const MediaWaiter&) = delete;

        void Signal() noexcept
        {
            SetEvent(m_event);
        }

        // Returns when signalled, or after the timeout. A negative timeout waits for the signal.
        void Wait(double seconds) noexcept
        {
            if (seconds < 0)
            {
                WaitForSingleObjectEx(m_event, INFINITE, FALSE);
                return;
            }

            LARGE_INTEGER due;
            due.QuadPart = -std::max<LONGLONG>(1, static_cast<LONGLONG>(seconds * 10'000'000));

            if (m_timer == nullptr || !SetWaitableTimerEx(m_timer, &due, 0, nullptr, nullptr, nullptr, 0))
            {
                WaitForSingleObjectEx(m_event, static_cast<DWORD>(std::ceil(seconds * 1000)), FALSE);
                return;
            }

            HANDLE handles[] = { m_event, m_timer };
            WaitForMultipleObjectsEx(2, handles, FALSE, INFINITE, FALSE);

            CancelWaitableTimer(m_timer);
        }

    private:
        HANDLE m_event{ nullptr };
        HANDLE m_timer{ nullptr };
    };

    // Media time that advances with the wall clock while running. With audio, the audio renderer
    // decides what time it is and this is kept in step with it, so that audio ending before the
    // video does not make the picture jump.
    class MediaClock
    {
    public:
        double Now() const
        {
            std::lock_guard<std::mutex> lock(m_lock);
            return NowLocked();
        }

        void Set(double time)
        {
            std::lock_guard<std::mutex> lock(m_lock);
            m_base = time;
            m_wall = MediaNow();
        }

        void Run(bool running)
        {
            std::lock_guard<std::mutex> lock(m_lock);

            if (m_running != running)
            {
                m_base = NowLocked();
                m_wall = MediaNow();
                m_running = running;
            }
        }

        void Rate(double rate)
        {
            std::lock_guard<std::mutex> lock(m_lock);
            m_base = NowLocked();
            m_wall = MediaNow();
            m_rate = rate;
        }

        bool Running() const
        {
            std::lock_guard<std::mutex> lock(m_lock);
            return m_running;
        }

    private:
        double NowLocked() const
        {
            return m_running ? m_base + (MediaNow() - m_wall) * m_rate : m_base;
        }

        mutable std::mutex m_lock;
        double m_base{ 0 };
        double m_wall{ 0 };
        double m_rate{ 1 };
        bool m_running{ false };
    };

    struct MediaPacket
    {
        enum class Kind
        {
            Data,
            // Where a seek lands: decoders flush, and drop what they decode before the target.
            Flush,
            // The end of the stream: decoders drain what they still hold.
            Drain,
            // Another quality takes over from here: decoders drain what they hold of the previous
            // one, which all plays before this point, and go on with the setup this carries.
            Switch
        };

        Kind kind{ Kind::Data };
        AVPacket* packet{ nullptr };
        int32_t serial{ 0 };
        double target{ 0 };

        // The segment of the playlist the packet was read from, -1 for a file without one.
        int32_t segment{ -1 };

        // When the packet stops playing, in seconds, NAN if unknown.
        double end{ NAN };

        std::shared_ptr<MediaStreamSetup> setup;
    };

    class PacketQueue
    {
    public:
        explicit PacketQueue(MediaWaiter* listener = nullptr) noexcept
            : m_listener(listener)
        {
        }

        ~PacketQueue()
        {
            Flush();
        }

        // Takes the packet.
        void Push(MediaPacket&& item)
        {
            {
                std::lock_guard<std::mutex> lock(m_lock);

                if (m_aborted)
                {
                    av_packet_free(&item.packet);
                    return;
                }

                if (item.packet != nullptr)
                {
                    m_bytes += item.packet->size;
                }

                if (item.kind == MediaPacket::Kind::Flush)
                {
                    m_end = item.target;
                }
                else if (!std::isnan(item.end))
                {
                    m_end = std::max(m_end, item.end);
                }

                m_items.push_back(std::move(item));
            }

            m_condition.notify_one();

            if (m_listener != nullptr)
            {
                m_listener->Signal();
            }
        }

        bool TryPop(MediaPacket& item)
        {
            std::lock_guard<std::mutex> lock(m_lock);
            return PopLocked(item);
        }

        bool Pop(MediaPacket& item, std::chrono::milliseconds timeout)
        {
            std::unique_lock<std::mutex> lock(m_lock);
            m_condition.wait_for(lock, timeout, [this] { return m_aborted || !m_items.empty(); });

            return PopLocked(item);
        }

        void Flush()
        {
            std::lock_guard<std::mutex> lock(m_lock);

            for (auto& item : m_items)
            {
                av_packet_free(&item.packet);
            }

            m_items.clear();
            m_bytes = 0;
            m_end = -std::numeric_limits<double>::infinity();
            m_popped = -1;
        }

        // Drops what was read from the segment on, from both queues at once, so that another
        // quality can be read from there instead. Refused when a decoder has already taken a packet
        // of that segment: what it holds could then no longer be followed seamlessly.
        static bool TrimFrom(PacketQueue& first, PacketQueue& second, int32_t segment)
        {
            std::scoped_lock lock(first.m_lock, second.m_lock);

            if (first.m_popped >= segment || second.m_popped >= segment)
            {
                return false;
            }

            first.TrimLocked(segment);
            second.TrimLocked(segment);

            return true;
        }

        void Abort()
        {
            {
                std::lock_guard<std::mutex> lock(m_lock);
                m_aborted = true;
            }

            m_condition.notify_all();
            Flush();
        }

        int64_t Bytes() const
        {
            std::lock_guard<std::mutex> lock(m_lock);
            return m_bytes;
        }

        // When the last packet queued stops playing, or minus infinity when nothing is known.
        double End() const
        {
            std::lock_guard<std::mutex> lock(m_lock);
            return m_end;
        }

        bool Empty() const
        {
            std::lock_guard<std::mutex> lock(m_lock);
            return m_items.empty();
        }

    private:
        bool PopLocked(MediaPacket& item)
        {
            if (m_aborted || m_items.empty())
            {
                return false;
            }

            item = std::move(m_items.front());
            m_items.pop_front();

            if (item.packet != nullptr)
            {
                m_bytes -= item.packet->size;
            }

            if (item.kind == MediaPacket::Kind::Data)
            {
                m_popped = std::max(m_popped, item.segment);
            }
            else if (item.kind != MediaPacket::Kind::Drain)
            {
                m_popped = -1;
            }

            return true;
        }

        void TrimLocked(int32_t segment)
        {
            std::deque<MediaPacket> kept;

            m_bytes = 0;
            m_end = -std::numeric_limits<double>::infinity();

            for (auto& item : m_items)
            {
                // The end of the stream goes too: the next quality is read to its own end.
                if ((item.kind == MediaPacket::Kind::Data && item.segment >= segment) || item.kind == MediaPacket::Kind::Drain)
                {
                    av_packet_free(&item.packet);
                    continue;
                }

                if (item.packet != nullptr)
                {
                    m_bytes += item.packet->size;
                }

                if (item.kind == MediaPacket::Kind::Flush)
                {
                    m_end = item.target;
                }
                else if (!std::isnan(item.end))
                {
                    m_end = std::max(m_end, item.end);
                }

                kept.push_back(std::move(item));
            }

            m_items = std::move(kept);
        }

        mutable std::mutex m_lock;
        std::condition_variable m_condition;
        std::deque<MediaPacket> m_items;
        MediaWaiter* m_listener;

        int64_t m_bytes{ 0 };
        double m_end{ -std::numeric_limits<double>::infinity() };
        bool m_aborted{ false };

        // The latest segment a decoder has taken a packet of.
        int32_t m_popped{ -1 };
    };

    struct VideoFrame
    {
        // Null marks the end of the stream for this serial.
        AVFrame* frame{ nullptr };
        double pts{ 0 };
        double duration{ 0 };
        int32_t serial{ 0 };
        MediaVideoFormat format;
    };

    // Decoded pictures waiting for their time. One producer, the decoder, and one consumer, the
    // presenter: only the presenter removes, so a pointer it got from Front stays valid while the
    // decoder appends behind it.
    class FrameQueue
    {
    public:
        FrameQueue(size_t capacity, MediaWaiter* listener) noexcept
            : m_capacity(capacity)
            , m_listener(listener)
        {
        }

        ~FrameQueue()
        {
            Clear();
        }

        // Blocks while the queue is full. Gives up, freeing the frame, when the queue is aborted or
        // a seek has made the frame stale.
        bool Push(VideoFrame&& item, std::atomic<int32_t> const& serial)
        {
            {
                std::unique_lock<std::mutex> lock(m_lock);
                m_condition.wait(lock, [&] { return m_aborted || m_items.size() < m_capacity || item.serial != serial.load(); });

                if (m_aborted || item.serial != serial.load())
                {
                    av_frame_free(&item.frame);
                    return false;
                }

                m_items.push_back(std::move(item));
            }

            if (m_listener != nullptr)
            {
                m_listener->Signal();
            }

            return true;
        }

        VideoFrame* Front()
        {
            std::lock_guard<std::mutex> lock(m_lock);
            return m_items.empty() ? nullptr : &m_items[0];
        }

        VideoFrame* Next()
        {
            std::lock_guard<std::mutex> lock(m_lock);
            return m_items.size() < 2 ? nullptr : &m_items[1];
        }

        void Pop()
        {
            {
                std::lock_guard<std::mutex> lock(m_lock);

                if (m_items.empty())
                {
                    return;
                }

                av_frame_free(&m_items.front().frame);
                m_items.pop_front();
            }

            m_condition.notify_all();
        }

        // Wakes a decoder blocked on a full queue, so that it notices a seek made its frame stale.
        void Wake()
        {
            {
                // Empty, but not removable: a decoder that tested the serial and is about to sleep
                // holds this lock, so taking it orders the notify after that sleep begins.
                std::lock_guard<std::mutex> lock(m_lock);
            }

            m_condition.notify_all();
        }

        void Abort()
        {
            {
                std::lock_guard<std::mutex> lock(m_lock);
                m_aborted = true;
            }

            m_condition.notify_all();
        }

        // Only once neither thread can touch the queue any more.
        void Clear()
        {
            std::lock_guard<std::mutex> lock(m_lock);

            for (auto& item : m_items)
            {
                av_frame_free(&item.frame);
            }

            m_items.clear();
        }

    private:
        std::mutex m_lock;
        std::condition_variable m_condition;
        std::deque<VideoFrame> m_items;
        size_t m_capacity;
        MediaWaiter* m_listener;
        bool m_aborted{ false };
    };
}
