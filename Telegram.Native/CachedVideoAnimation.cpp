#include "pch.h"
#include "CachedVideoAnimation.h"
#if __has_include("CachedVideoAnimation.g.cpp")
#include "CachedVideoAnimation.g.cpp"
#endif

#define QOI_IMPLEMENTATION
#define QOI_NO_STDIO

#include <lz4.h>

#define RETURNFALSE(x) if (!x) return false;

namespace winrt::Telegram::Native::implementation
{
    std::map<std::string, std::mutex> CachedVideoAnimation::s_locks;

    std::mutex CachedVideoAnimation::s_compressLock;
    bool CachedVideoAnimation::s_compressStarted;
    std::thread CachedVideoAnimation::s_compressWorker;
    WorkQueue CachedVideoAnimation::s_compressQueue;

    inline bool ReadFileReturn(HANDLE hFile, LPVOID lpBuffer, DWORD nNumberOfBytesToRead, LPDWORD lpNumberOfBytesRead)
    {
        if (ReadFile(hFile, lpBuffer, nNumberOfBytesToRead, lpNumberOfBytesRead, NULL))
        {
            return nNumberOfBytesToRead == *lpNumberOfBytesRead;
        }

        return false;
    }

    bool CachedVideoAnimation::ReadHeader(HANDLE precacheFile)
    {
        DWORD read;
        uint8_t version = 0;
        RETURNFALSE(ReadFileReturn(precacheFile, &version, sizeof(uint8_t), &read));
        if (version == CACHED_VERSION)
        {
            uint32_t headerOffset;
            RETURNFALSE(ReadFileReturn(precacheFile, &headerOffset, sizeof(uint32_t), &read));
            if (headerOffset != 0)
            {
                SetFilePointer(precacheFile, headerOffset, NULL, FILE_BEGIN);
                RETURNFALSE(ReadFileReturn(precacheFile, &m_maxFrameSize, sizeof(uint32_t), &read));
                RETURNFALSE(ReadFileReturn(precacheFile, &m_imageSize, sizeof(uint32_t), &read));
                RETURNFALSE(ReadFileReturn(precacheFile, &m_pixelWidth, sizeof(int32_t), &read));
                RETURNFALSE(ReadFileReturn(precacheFile, &m_pixelHeight, sizeof(int32_t), &read));
                RETURNFALSE(ReadFileReturn(precacheFile, &m_fps, sizeof(int32_t), &read));
                RETURNFALSE(ReadFileReturn(precacheFile, &m_frameCount, sizeof(size_t), &read));
                m_fileOffsets = std::vector<uint32_t>(m_frameCount, 0);
                RETURNFALSE(ReadFileReturn(precacheFile, &m_fileOffsets[0], sizeof(uint32_t) * m_frameCount, &read));

                return true;
            }
        }

        return false;
    }

    winrt::Telegram::Native::CachedVideoAnimation CachedVideoAnimation::LoadFromFile(IVideoAnimationSource file, int32_t width, int32_t height, bool createCache)
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
                }

                info->m_cacheFile += L".cache";
                info->m_precache = true;

                std::lock_guard const guard(s_locks[info->m_cacheKey]);

                HANDLE precacheFile = CreateFile2(info->m_cacheFile.c_str(), GENERIC_READ, 0, OPEN_EXISTING, NULL);
                if (precacheFile != INVALID_HANDLE_VALUE)
                {
                    if (info->ReadHeader(precacheFile))
                    {
                        createCache = false;
                    }

                    CloseHandle(precacheFile);
                }

                if (createCache)
                {
                    if (!info->Load(file, width, height))
                    {
                        return nullptr;
                    }

                    info->m_precache = true;
                    precacheFile = CreateFile2(info->m_cacheFile.c_str(), GENERIC_WRITE, 0, CREATE_ALWAYS, NULL);
                    if (precacheFile != INVALID_HANDLE_VALUE)
                    {
                        DWORD write;
                        uint8_t version = CACHED_VERSION;
                        uint32_t offset = 0;
                        SetFilePointer(precacheFile, 0, NULL, FILE_BEGIN);
                        WriteFile(precacheFile, &version, sizeof(uint8_t), &write, NULL);
                        WriteFile(precacheFile, &offset, sizeof(uint32_t), &write, NULL);

                        CloseHandle(precacheFile);
                    }
                }
            }
        }
        else
        {
            if (!info->Load(file, width, height))
            {
                return nullptr;
            }
        }

        return info.as<winrt::Telegram::Native::CachedVideoAnimation>();
    }

    bool CachedVideoAnimation::Load(IVideoAnimationSource file, int32_t width, int32_t height)
    {
        m_animation = VideoAnimation::LoadFromFile(file, false, false, false).as<VideoAnimation>();
        if (m_animation == nullptr)
        {
            return false;
        }

        auto pixelWidth = m_animation->PixelWidth();
        auto pixelHeight = m_animation->PixelHeight();

        if (width <= pixelWidth && height <= pixelHeight && (width != 0 || height != 0))
        {
            double ratioX = (double)width / pixelWidth;
            double ratioY = (double)height / pixelHeight;
            double ratio = std::max(ratioX, ratioY);

            pixelWidth *= ratio;
            pixelHeight *= ratio;
        }

        auto widthalign = AV_INPUT_BUFFER_PADDING_SIZE / 4;
        auto neededWidth = pixelWidth + ((pixelWidth % widthalign) ? (widthalign - (pixelWidth % widthalign)) : 0);

        m_pixelWidth = neededWidth; //pixelWidth + (pixelWidth % 4);
        m_pixelHeight = (int)((double)neededWidth / pixelWidth * pixelHeight); //+(pixelHeight % 4);

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

    void CachedVideoAnimation::RenderSync(IBuffer bitmap, int32_t& seconds, bool& completed)
    {
        uint8_t* pixels = bitmap.data();
        bool rendered;
        RenderSync(pixels, seconds, completed, &rendered);
    }

    void CachedVideoAnimation::RenderSync(uint8_t* pixels, int32_t& seconds, bool& completed, bool* rendered)
    {
        bool loadedFromCache = false;
        if (rendered)
        {
            *rendered = false;
        }

        if (m_readyToCache)
        {
            return;
        }

        if (m_precache && m_imageSize == m_pixelWidth * m_pixelHeight * 4)
        {
            uint32_t offset = m_fileOffsets[m_frameIndex];
            if (offset > 0)
            {
                std::lock_guard const guard(s_locks[m_cacheKey]);

                HANDLE precacheFile = CreateFile2(m_cacheFile.c_str(), GENERIC_READ, 0, OPEN_EXISTING, NULL);
                if (precacheFile != INVALID_HANDLE_VALUE)
                {
                    SetFilePointer(precacheFile, offset, NULL, FILE_BEGIN);
                    if (m_decompressBuffer == nullptr)
                    {
                        m_decompressBuffer = new uint8_t[m_maxFrameSize];
                    }
                    DWORD read;
                    uint32_t frameSize;
                    auto completed = ReadFileReturn(precacheFile, &frameSize, sizeof(uint32_t), &read);
                    if (completed && frameSize <= m_maxFrameSize)
                    {
                        if (ReadFileReturn(precacheFile, m_decompressBuffer, sizeof(uint8_t) * frameSize, &read))
                        {
                            LZ4_decompress_safe((const char*)m_decompressBuffer, (char*)pixels, frameSize, m_pixelWidth * m_pixelHeight * 4);
                            loadedFromCache = true;

                            if (rendered)
                            {
                                *rendered = true;
                            }
                        }
                    }
                    CloseHandle(precacheFile);
                    int framesPerUpdate = /*limitFps ? fps < 60 ? 1 : 2 :*/ 1;
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

        if (!loadedFromCache && !m_caching)
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

    void CachedVideoAnimation::Cache()
    {
        if (m_animation == nullptr)
        {
            return;
        }

        if (m_precache)
        {
            m_readyToCache = false;

            m_caching = true;
            s_compressQueue.push_work(WorkItem(get_weak(), m_pixelWidth, m_pixelHeight));

            std::lock_guard const guard(s_compressLock);

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
    }

    void CachedVideoAnimation::CompressThreadProc()
    {
        while (s_compressStarted)
        {
            auto work = s_compressQueue.wait_and_pop();
            if (work == std::nullopt)
            {
                s_compressStarted = false;
                return;
            }

            auto oldW = 0;
            auto oldH = 0;

            int bound;
            uint8_t* compressBuffer = nullptr;
            uint8_t* pixels = nullptr;

            if (auto item{ work->animation.get() })
            {
                auto w = work->w;
                auto h = work->h;

                std::lock_guard const guard(s_locks[item->m_cacheKey]);

                HANDLE precacheFile = CreateFile2(item->m_cacheFile.c_str(), GENERIC_READ | GENERIC_WRITE, 0, OPEN_EXISTING, NULL);
                if (precacheFile != INVALID_HANDLE_VALUE)
                {
                    if (item->ReadHeader(precacheFile))
                    {
                        CloseHandle(precacheFile);
                        item->m_caching = false;
                        continue;
                    }

                    DWORD write;
                    size_t totalSize = SetFilePointer(precacheFile, sizeof(uint8_t) + sizeof(uint32_t), NULL, FILE_BEGIN);

                    if (w + h > oldW + oldH)
                    {
                        bound = LZ4_compressBound(w * h * 4);
                        compressBuffer = new uint8_t[bound];
                        pixels = new uint8_t[w * h * 4];
                    }

                    int32_t seconds = 0;
                    bool completed = false;
                    std::vector<uint32_t> offsets;

                    do
                    {
                        offsets.push_back(totalSize);

                        item->m_animation->RenderSync(pixels, item->m_pixelWidth, item->m_pixelHeight, false, seconds, completed);

                        uint32_t size = (uint32_t)LZ4_compress_default((const char*)pixels, (char*)compressBuffer, w * h * 4, bound);

                        if (size > item->m_maxFrameSize && item->m_decompressBuffer != nullptr)
                        {
                            delete[] item->m_decompressBuffer;
                            item->m_decompressBuffer = nullptr;
                        }

                        item->m_maxFrameSize = std::max(item->m_maxFrameSize, size);

                        WriteFile(precacheFile, &size, sizeof(uint32_t), &write, NULL);
                        WriteFile(precacheFile, compressBuffer, sizeof(uint8_t) * size, &write, NULL);
                        totalSize += size;
                        totalSize += 4;
                    } while (!completed);

                    SetFilePointer(precacheFile, 0, NULL, FILE_BEGIN);
                    uint8_t version = CACHED_VERSION;
                    item->m_fileOffsets = offsets;
                    item->m_frameCount = offsets.size();
                    item->m_imageSize = (uint32_t)w * h * 4;
                    WriteFile(precacheFile, &version, sizeof(uint8_t), &write, NULL);
                    WriteFile(precacheFile, &totalSize, sizeof(uint32_t), &write, NULL);
                    SetFilePointer(precacheFile, 0, NULL, FILE_END);
                    WriteFile(precacheFile, &item->m_maxFrameSize, sizeof(uint32_t), &write, NULL);
                    WriteFile(precacheFile, &item->m_imageSize, sizeof(uint32_t), &write, NULL);
                    WriteFile(precacheFile, &item->m_pixelWidth, sizeof(int32_t), &write, NULL);
                    WriteFile(precacheFile, &item->m_pixelHeight, sizeof(int32_t), &write, NULL);
                    WriteFile(precacheFile, &item->m_fps, sizeof(int32_t), &write, NULL);
                    WriteFile(precacheFile, &item->m_frameCount, sizeof(size_t), &write, NULL);
                    WriteFile(precacheFile, &item->m_fileOffsets[0], sizeof(uint32_t) * item->m_frameCount, &write, NULL);

                    CloseHandle(precacheFile);
                }

                item->m_caching = false;

                oldW = w;
                oldH = h;
            }

            if (compressBuffer)
            {
                delete[] compressBuffer;
            }

            if (pixels)
            {
                delete[] pixels;
            }
        }
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

        return m_frameCount;
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
