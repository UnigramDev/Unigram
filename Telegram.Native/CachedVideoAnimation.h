#pragma once

#include "CachedVideoAnimation.g.h"

#include <stack>
#include <mutex>
#include <thread>
#include <condition_variable>
#include <memory>
#include <map>

#include <winrt/Windows.UI.Xaml.Media.Imaging.h>

#include "VideoAnimation.h"

using namespace winrt::Windows::Storage::Streams;
using namespace winrt::Windows::UI::Xaml::Media::Imaging;

#define CACHED_VERSION 7

namespace winrt::Telegram::Native::implementation
{
    class WorkQueue;

    struct CachedVideoAnimation : CachedVideoAnimationT<CachedVideoAnimation>
    {
        CachedVideoAnimation() = default;

        virtual ~CachedVideoAnimation()
        {
            Close();
        }

        void Close()
        {
            if (m_decompressBuffer)
            {
                delete[] m_decompressBuffer;
                m_decompressBuffer = nullptr;
            }

            if (m_animation)
            {
                m_animation->Close();
                m_animation = nullptr;
            }

            if (m_cacheHandle)
            {
                CloseHandle(m_cacheHandle);
                m_cacheHandle = INVALID_HANDLE_VALUE;
            }
        }

        static winrt::Telegram::Native::CachedVideoAnimation LoadFromFile(IVideoAnimationSource file, int32_t width, int32_t height, bool fit, bool precache, bool limitFps);

        void RenderSync(IBuffer bitmap, double& seconds, bool& completed);
        void Stop();
        void Cache();

        void Seek(double seconds);

        double FrameRate();

        int32_t TotalFrame();

        bool IsCaching();

        bool IsReadyToCache();

        int PixelWidth()
        {
            return m_pixelWidth;
        }

        int PixelHeight()
        {
            return m_pixelHeight;
        }

        int Rotation()
        {
            if (m_animation)
            {
                return m_animation->Rotation();
            }

            return 0;
        }

    private:
        bool Load(IVideoAnimationSource file, int32_t width, int32_t height, bool fit, bool limitFps);
        void RenderSync(uint8_t* pixels, double& seconds, bool& completed, bool* rendered);

        HANDLE GetCacheHandle();
        void CloseCacheHandle();

        bool ReadHeader(HANDLE precacheFile);

        static void CompressThreadProc();

        // Performance-optimized lock management
        static std::mutex& GetLockForKey(const std::string& key);

        static std::mutex s_init_mutex;
        static std::map<std::string, std::unique_ptr<std::mutex>> s_locks;

        static std::mutex s_compressLock;
        static bool s_compressStarted;
        static std::thread s_compressWorker;
        static WorkQueue s_compressQueue;

        bool m_caching = false;
        bool m_readyToCache = false;

        IVideoAnimationSource m_file{ nullptr };
        winrt::com_ptr<VideoAnimation> m_animation;
        size_t m_frameCount = 0;
        int32_t m_frameIndex = 0;
        int32_t m_fps = 30;
        int32_t m_pixelWidth = 0;
        int32_t m_pixelHeight = 0;
        bool m_precache = false;
        winrt::hstring m_path;
        std::wstring m_cacheFile;
        HANDLE m_cacheHandle = INVALID_HANDLE_VALUE;
        std::string m_data;
        std::string m_cacheKey;
        uint8_t* m_decompressBuffer = nullptr;  // Raw pointer for performance
        uint32_t m_maxFrameSize = 0;
        uint32_t m_imageSize = 0;
        std::vector<uint32_t> m_fileOffsets;
        std::vector<std::pair<std::uint32_t, std::uint32_t>> m_colors;
    };

    class WorkItem
    {
    public:
        winrt::weak_ref<CachedVideoAnimation> animation;
        size_t w;
        size_t h;

        WorkItem(winrt::weak_ref<CachedVideoAnimation> animation, size_t w, size_t h)
            : animation(animation)
            , w(w)
            , h(h)
        {
        }
    };

    class WorkQueue
    {
        std::condition_variable work_available;
        std::mutex work_mutex;
        std::stack<WorkItem> work;

    public:
        void push_work(WorkItem item)
        {
            std::unique_lock<std::mutex> lock(work_mutex);

            bool was_empty = work.empty();
            work.push(std::move(item));

            lock.unlock();

            if (was_empty)
            {
                work_available.notify_one();
            }
        }

        std::optional<WorkItem> wait_and_pop()
        {
            std::unique_lock<std::mutex> lock(work_mutex);
            while (work.empty())
            {
                const std::chrono::milliseconds timeout(3000);
                if (work_available.wait_for(lock, timeout) == std::cv_status::timeout)
                {
                    return std::nullopt;
                }
            }

            WorkItem tmp = std::move(work.top());
            work.pop();
            return std::make_optional<WorkItem>(std::move(tmp));
        }
    };

}

namespace winrt::Telegram::Native::factory_implementation
{
    struct CachedVideoAnimation : CachedVideoAnimationT<CachedVideoAnimation, implementation::CachedVideoAnimation>
    {
    };
}
