#include "pch.h"
#include "CachedVideoAnimation.h"
#if __has_include("CachedVideoAnimation.g.cpp")
#include "CachedVideoAnimation.g.cpp"
#endif

#include <lz4.h>
#include <algorithm>

#define RETURNFALSE(x) if (!x) return false;

// Conservative limits to prevent obvious attacks while minimizing validation overhead
constexpr size_t MAX_FRAME_COUNT = 100000;
constexpr size_t MAX_FRAME_SIZE = 256 * 1024 * 1024; // 256MB

namespace winrt::Telegram::Native::implementation
{
    // Thread-safe initialization of locks map
    std::mutex CachedVideoAnimation::s_init_mutex;
    std::map<std::string, std::unique_ptr<std::mutex>> CachedVideoAnimation::s_locks;

    std::mutex CachedVideoAnimation::s_compressLock;
    bool CachedVideoAnimation::s_compressStarted = false;
    std::thread CachedVideoAnimation::s_compressWorker;
    WorkQueue CachedVideoAnimation::s_compressQueue;

    // Fast lock access after initialization
    std::mutex& CachedVideoAnimation::GetLockForKey(const std::string& key)
    {
        // Fast path - check if lock exists
        {
            std::lock_guard<std::mutex> guard(s_init_mutex);
            auto it = s_locks.find(key);
            if (it != s_locks.end())
            {
                return *it->second;
            }

            // Create new lock
            s_locks[key] = std::make_unique<std::mutex>();
            return *s_locks[key];
        }
    }

    inline bool ReadFileReturn(HANDLE hFile, LPVOID lpBuffer, DWORD nNumberOfBytesToRead, LPDWORD lpNumberOfBytesRead)
    {
        return ReadFile(hFile, lpBuffer, nNumberOfBytesToRead, lpNumberOfBytesRead, NULL) &&
            nNumberOfBytesToRead == *lpNumberOfBytesRead;
    }

    bool CachedVideoAnimation::ReadHeader(HANDLE precacheFile)
    {
        DWORD read;
        uint8_t version = 0;
        RETURNFALSE(ReadFileReturn(precacheFile, &version, sizeof(uint8_t), &read));
        if (version != CACHED_VERSION)
        {
            return false;
        }

        uint32_t headerOffset;
        RETURNFALSE(ReadFileReturn(precacheFile, &headerOffset, sizeof(uint32_t), &read));
        if (headerOffset == 0)
        {
            return false;
        }

        if (SetFilePointer(precacheFile, headerOffset, NULL, FILE_BEGIN) == INVALID_SET_FILE_POINTER)
        {
            return false;
        }

        RETURNFALSE(ReadFileReturn(precacheFile, &m_maxFrameSize, sizeof(uint32_t), &read));
        RETURNFALSE(ReadFileReturn(precacheFile, &m_imageSize, sizeof(uint32_t), &read));
        RETURNFALSE(ReadFileReturn(precacheFile, &m_pixelWidth, sizeof(int32_t), &read));
        RETURNFALSE(ReadFileReturn(precacheFile, &m_pixelHeight, sizeof(int32_t), &read));
        RETURNFALSE(ReadFileReturn(precacheFile, &m_fps, sizeof(int32_t), &read));
        RETURNFALSE(ReadFileReturn(precacheFile, &m_frameCount, sizeof(size_t), &read));

        // Basic validation - only check obvious corruption
        if (m_frameCount == 0 || m_frameCount > MAX_FRAME_COUNT ||
            m_pixelWidth <= 0 || m_pixelHeight <= 0 ||
            m_maxFrameSize > MAX_FRAME_SIZE)
        {
            return false;
        }

        m_fileOffsets.resize(m_frameCount);
        RETURNFALSE(ReadFileReturn(precacheFile, m_fileOffsets.data(),
            sizeof(uint32_t) * m_frameCount, &read));

        return true;
    }

    inline HANDLE CachedVideoAnimation::GetCacheHandle()
    {
        if (m_cacheHandle == INVALID_HANDLE_VALUE)
        {
            m_cacheHandle = CreateFile2(m_cacheFile.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, OPEN_EXISTING, NULL);
        }

        return m_cacheHandle;
    }

    inline void CachedVideoAnimation::CloseCacheHandle()
    {
        if (m_cacheHandle != INVALID_HANDLE_VALUE)
        {
            CloseHandle(m_cacheHandle);
            m_cacheHandle = INVALID_HANDLE_VALUE;
        }
    }

    winrt::Telegram::Native::CachedVideoAnimation CachedVideoAnimation::LoadFromFile(IVideoAnimationSource file, int32_t width, int32_t height, bool fit, bool createCache, bool limitFps)
    {
        auto info = winrt::make_self<CachedVideoAnimation>();
        file.SeekCallback(0);

        if (createCache)
        {
            auto path = file.FilePath();
            if (path.size())
            {
                info->m_cacheFile = path;
                info->m_cacheKey = to_string(path);

                if (width != 0 && height != 0)
                {
                    info->m_cacheFile += L".";
                    info->m_cacheFile += std::to_wstring(width);
                    info->m_cacheFile += L"x";
                    info->m_cacheFile += std::to_wstring(height);

                    info->m_cacheKey += ".";
                    info->m_cacheKey += std::to_string(width);
                    info->m_cacheKey += "x";
                    info->m_cacheKey += std::to_string(height);

                    if (fit)
                    {
                        info->m_cacheFile += L".fit";
                        info->m_cacheKey += ".fit";
                    }
                }

                info->m_cacheFile += L".cache";
                info->m_precache = true;

                std::lock_guard<std::mutex> guard(GetLockForKey(info->m_cacheKey));

                HANDLE precacheFile = info->GetCacheHandle();
                if (precacheFile != INVALID_HANDLE_VALUE)
                {
                    bool headerValid = info->ReadHeader(precacheFile);
                    if (headerValid)
                    {
                        createCache = false;
                    }
                    else
                    {
                        info->CloseCacheHandle();
                    }
                }

                if (createCache)
                {
                    if (!info->Load(file, width, height, fit, limitFps))
                    {
                        return nullptr;
                    }
                }
            }
        }
        else
        {
            if (!info->Load(file, width, height, fit, limitFps))
            {
                return nullptr;
            }
        }

        return info.as<winrt::Telegram::Native::CachedVideoAnimation>();
    }

    bool CachedVideoAnimation::Load(IVideoAnimationSource file, int32_t width, int32_t height, bool fit, bool limitFps)
    {
        m_animation = VideoAnimation::LoadFromFile(file, false, limitFps, false).as<VideoAnimation>();
        if (m_animation == nullptr)
        {
            return false;
        }

        auto pixelWidth = m_animation->PixelWidth();
        auto pixelHeight = m_animation->PixelHeight();

        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            return false;
        }

        if (width > 0 && height > 0)
        {
            double ratioX = (double)width / pixelWidth;
            double ratioY = (double)height / pixelHeight;
            double ratio = fit ? std::min(ratioX, ratioY) : std::max(ratioX, ratioY);

            pixelWidth = (int)(pixelWidth * ratio);
            pixelHeight = (int)(pixelHeight * ratio);
        }

        auto widthalign = AV_INPUT_BUFFER_PADDING_SIZE / 4;
        auto neededWidth = pixelWidth + ((pixelWidth % widthalign) ? (widthalign - (pixelWidth % widthalign)) : 0);

        m_pixelWidth = neededWidth;
        m_pixelHeight = (int)((double)neededWidth / pixelWidth * pixelHeight);

        m_fps = m_animation->FrameRate();
        return true;
    }

    void CachedVideoAnimation::Stop()
    {
        if (m_animation != nullptr)
        {
            m_animation->SeekToMilliseconds(0, false);
        }
        m_frameIndex = 0;
    }

    void CachedVideoAnimation::RenderSync(IBuffer bitmap, double& seconds, bool& completed)
    {
        uint8_t* pixels = bitmap.data();
        bool rendered;
        RenderSync(pixels, seconds, completed, &rendered);
    }

    void CachedVideoAnimation::RenderSync(uint8_t* pixels, double& seconds, bool& completed, bool* rendered)
    {
        if (rendered)
        {
            *rendered = false;
        }

        // Fast early exit
        if (m_readyToCache || !pixels)
        {
            return;
        }

        bool loadedFromCache = false;

        // Optimized cache path - minimize branches in hot path
        if (m_precache && m_imageSize == static_cast<uint32_t>(m_pixelWidth) * m_pixelHeight * 4) [[likely]]
        {
            if (m_frameIndex < m_fileOffsets.size()) [[likely]]
            {
                uint32_t offset = m_fileOffsets[m_frameIndex];
                if (offset > 0) [[likely]]
                {
                    std::lock_guard<std::mutex> guard(GetLockForKey(m_cacheKey));

                    HANDLE precacheFile = GetCacheHandle();
                    if (precacheFile != INVALID_HANDLE_VALUE)
                    {
                        if (SetFilePointer(precacheFile, offset, NULL, FILE_BEGIN) != INVALID_SET_FILE_POINTER)
                        {
                            // Lazy decompression buffer allocation
                            if (m_decompressBuffer == nullptr && m_maxFrameSize > 0)
                            {
                                m_decompressBuffer = new uint8_t[m_maxFrameSize];
                            }

                            if (m_decompressBuffer)
                            {
                                DWORD read;
                                uint32_t frameSize;
                                if (ReadFileReturn(precacheFile, &frameSize, sizeof(uint32_t), &read) &&
                                    frameSize <= m_maxFrameSize && frameSize > 0)
                                {
                                    if (ReadFileReturn(precacheFile, m_decompressBuffer, frameSize, &read))
                                    {
                                        // CRITICAL: Validate LZ4 result
                                        int result = LZ4_decompress_safe(
                                            (const char*)m_decompressBuffer,
                                            (char*)pixels,
                                            frameSize,
                                            m_pixelWidth * m_pixelHeight * 4);

                                        if (result > 0) [[likely]]
                                        {
                                            loadedFromCache = true;
                                            if (rendered)
                                            {
                                                *rendered = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (loadedFromCache)
                        {
                            constexpr int framesPerUpdate = 1;
                            if (m_frameIndex + framesPerUpdate >= m_frameCount)
                            {
                                m_frameIndex = 0;
                                completed = true;
                            }
                            else
                            {
                                m_frameIndex += framesPerUpdate;
                                completed = false;
                            }
                        }
                    }
                }
            }
        }

        // Direct rendering fallback
        if (!loadedFromCache && !m_caching) [[likely]]
        {
            if (m_animation == nullptr)
            {
                return;
            }

            auto result = m_animation->RenderSync(pixels, m_pixelWidth, m_pixelHeight, false, seconds, completed);

            if (result && rendered)
            {
                *rendered = true;
            }

            if (m_precache)
            {
                m_readyToCache = true;
            }
        }
    }

    void CachedVideoAnimation::Seek(double seconds)
    {
        if (m_animation)
        {
            m_animation->SeekToMilliseconds((int64_t)(seconds * 1000), true);
        }
    }

    void CachedVideoAnimation::Cache()
    {
        if (m_animation == nullptr || !m_precache)
        {
            return;
        }

        m_readyToCache = false;
        m_caching = true;
        s_compressQueue.push_work(WorkItem(get_weak(), m_pixelWidth, m_pixelHeight));

        std::lock_guard<std::mutex> guard(s_compressLock);

        if (!s_compressStarted)
        {
            if (s_compressWorker.joinable())
            {
                s_compressWorker.join();
            }

            s_compressStarted = true;
            s_compressWorker = std::thread(&CachedVideoAnimation::CompressThreadProc);
        }
    }

    void CachedVideoAnimation::CompressThreadProc()
    {
        // CRITICAL FIX: Thread-local static buffers to prevent memory leaks
        static thread_local uint8_t* compressBuffer = nullptr;
        static thread_local uint8_t* pixels = nullptr;
        static thread_local size_t currentCompressBound = 0;
        static thread_local size_t currentPixelSize = 0;

        while (s_compressStarted)
        {
            auto work = s_compressQueue.wait_and_pop();
            if (work == std::nullopt)
            {
                s_compressStarted = false;
                return;
            }

            if (auto item{ work->animation.get() })
            {
                auto w = work->w;
                auto h = work->h;

                // Basic overflow check
                if (w <= 0 || h <= 0 || static_cast<uint64_t>(w) * h > MAX_FRAME_SIZE / 4)
                {
                    item->m_caching = false;
                    continue;
                }

                size_t imageSize = static_cast<size_t>(w) * h * 4;
                size_t neededBound = LZ4_compressBound(static_cast<int>(imageSize));

                // Efficient buffer reallocation
                if (neededBound > currentCompressBound)
                {
                    delete[] compressBuffer;
                    compressBuffer = new uint8_t[neededBound];
                    currentCompressBound = neededBound;
                }

                if (imageSize > currentPixelSize)
                {
                    delete[] pixels;
                    pixels = new uint8_t[imageSize];
                    currentPixelSize = imageSize;
                }

                std::lock_guard<std::mutex> guard(GetLockForKey(item->m_cacheKey));

                HANDLE precacheFile = CreateFile2(item->m_cacheFile.c_str(), GENERIC_READ | GENERIC_WRITE, 0, OPEN_ALWAYS, NULL);
                if (precacheFile != INVALID_HANDLE_VALUE)
                {
                    // Quick header check
                    if (item->ReadHeader(precacheFile))
                    {
                        CloseHandle(precacheFile);
                        item->m_caching = false;
                        continue;
                    }

                    DWORD write;
                    uint8_t version = CACHED_VERSION;
                    uint32_t offset = 0;
                    SetFilePointer(precacheFile, 0, NULL, FILE_BEGIN);
                    WriteFile(precacheFile, &version, sizeof(uint8_t), &write, NULL);
                    WriteFile(precacheFile, &offset, sizeof(uint32_t), &write, NULL);

                    DWORD totalSize = sizeof(uint8_t) + sizeof(uint32_t);

                    double seconds = 0;
                    bool completed = false;
                    std::vector<uint32_t> offsets;
                    uint32_t maxFrameSize = 0;

                    do
                    {
                        offsets.push_back(totalSize);

                        if (!item->m_animation->RenderSync(pixels, item->m_pixelWidth, item->m_pixelHeight, false, seconds, completed))
                        {
                            break;
                        }

                        int compressedSize = LZ4_compress_default(
                            (const char*)pixels,
                            (char*)compressBuffer,
                            static_cast<int>(imageSize),
                            static_cast<int>(neededBound));

                        if (compressedSize <= 0)
                        {
                            break;
                        }

                        uint32_t frameSize = static_cast<uint32_t>(compressedSize);
                        maxFrameSize = std::max(maxFrameSize, frameSize);

                        if (!WriteFile(precacheFile, &frameSize, sizeof(uint32_t), &write, NULL) ||
                            !WriteFile(precacheFile, compressBuffer, frameSize, &write, NULL))
                        {
                            break;
                        }

                        totalSize += frameSize + sizeof(uint32_t);

                        // Safety check for runaway animations
                        if (offsets.size() > MAX_FRAME_COUNT)
                        {
                            break;
                        }

                    } while (!completed);

                    // Update header
                    if (SetFilePointer(precacheFile, 0, NULL, FILE_BEGIN) != INVALID_SET_FILE_POINTER)
                    {
                        uint8_t version = CACHED_VERSION;
                        item->m_fileOffsets = std::move(offsets);
                        item->m_frameCount = item->m_fileOffsets.size();
                        item->m_imageSize = static_cast<uint32_t>(imageSize);
                        item->m_maxFrameSize = maxFrameSize;

                        WriteFile(precacheFile, &version, sizeof(uint8_t), &write, NULL);
                        WriteFile(precacheFile, &totalSize, sizeof(uint32_t), &write, NULL);

                        SetFilePointer(precacheFile, totalSize, NULL, FILE_BEGIN);
                        WriteFile(precacheFile, &item->m_maxFrameSize, sizeof(uint32_t), &write, NULL);
                        WriteFile(precacheFile, &item->m_imageSize, sizeof(uint32_t), &write, NULL);
                        WriteFile(precacheFile, &item->m_pixelWidth, sizeof(int32_t), &write, NULL);
                        WriteFile(precacheFile, &item->m_pixelHeight, sizeof(int32_t), &write, NULL);
                        WriteFile(precacheFile, &item->m_fps, sizeof(int32_t), &write, NULL);
                        WriteFile(precacheFile, &item->m_frameCount, sizeof(size_t), &write, NULL);
                        if (!item->m_fileOffsets.empty())
                        {
                            WriteFile(precacheFile, item->m_fileOffsets.data(), sizeof(uint32_t) * item->m_frameCount, &write, NULL);
                        }

                        SetEndOfFile(precacheFile);
                    }

                    CloseHandle(precacheFile);
                }

                item->m_caching = false;
            }
        }

        // Cleanup on thread exit
        delete[] compressBuffer;
        delete[] pixels;
        compressBuffer = nullptr;
        pixels = nullptr;
        currentCompressBound = 0;
        currentPixelSize = 0;
    }

#pragma region Properties

    double CachedVideoAnimation::FrameRate()
    {
        if (m_animation)
        {
            return m_animation->FrameRate();
        }
        return m_fps;
    }

    int32_t CachedVideoAnimation::TotalFrame()
    {
        if (m_animation)
        {
            return INT_MAX;
        }
        return static_cast<int32_t>(m_frameCount);
    }

    bool CachedVideoAnimation::IsCaching()
    {
        return m_caching;
    }

    bool CachedVideoAnimation::IsReadyToCache()
    {
        return m_readyToCache;
    }

#pragma endregion

}
