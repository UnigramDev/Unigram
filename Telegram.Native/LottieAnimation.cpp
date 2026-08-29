#include "pch.h"
#include "LottieAnimation.h"
#if __has_include("LottieAnimation.g.cpp")
#include "LottieAnimation.g.cpp"
#endif

#include <winrt/Windows.Storage.h>

#include "Cache/FrameCacheService.h"
#include "LottieFrameProducer.h"
#include "LottieStringUtils.h"
#include "TlottieFrameProducer.h"

namespace winrt::Telegram::Native::implementation
{
    namespace
    {
        // A malformed or hostile file should fail to load rather than allocate for an hour.
        constexpr int32_t MaxFrameRate = 120;
        constexpr uint32_t MaxFrameCount = 4096;

        std::atomic<bool> s_useTLottie{ false };

        long ColorHash(const Windows::Foundation::Collections::IMapView<int32_t, int32_t>& replacement,
            std::vector<std::pair<uint32_t, uint32_t>>& colors)
        {
            long hash = 0;

            if (replacement != nullptr)
            {
                for (auto&& elem : replacement)
                {
                    colors.push_back({ static_cast<uint32_t>(elem.Key()), static_cast<uint32_t>(elem.Value()) });

                    hash = ((hash * 20261) + 0x80000000L + elem.Key()) % 0x80000000L;
                    hash = ((hash * 20261) + 0x80000000L + elem.Value()) % 0x80000000L;
                }
            }

            return hash;
        }

        // Everything that changes the pixels, and nothing that does not. The renderer is absent on
        // purpose: rlottie and tlottie produce identical premultiplied BGRA, so they share a file
        // and toggling never forces a re-render.
        std::wstring BuildCachePath(const std::wstring& base, long hash, Telegram::Native::FitzModifier modifier, int32_t width, int32_t height)
        {
            auto result = base;

            if (hash != 0)
            {
                result += L"." + std::to_wstring(std::abs(hash));
            }

            if (modifier != Telegram::Native::FitzModifier::None)
            {
                result += L"." + std::to_wstring(static_cast<int>(modifier));
            }

            result += L"." + std::to_wstring(width) + L"x" + std::to_wstring(height);
            result += L".tgfc";

            return result;
        }
    }

    bool LottieAnimation::UseTLottie() noexcept
    {
#ifdef HAS_TLOTTIE
        return s_useTLottie.load(std::memory_order_relaxed);
#else
        return false;
#endif
    }

    void LottieAnimation::UseTLottie(bool value) noexcept
    {
        s_useTLottie.store(value, std::memory_order_relaxed);
    }

    Telegram::Native::LottieAnimation LottieAnimation::LoadFromFile(hstring filePath, int32_t pixelWidth, int32_t pixelHeight, bool precache,
        Windows::Foundation::Collections::IMapView<int32_t, int32_t> colorReplacement, Telegram::Native::FitzModifier modifier)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 || filePath.empty())
        {
            return nullptr;
        }

        auto info = winrt::make_self<LottieAnimation>();
        info->m_path = filePath;
        info->m_pixelWidth = pixelWidth;
        info->m_pixelHeight = pixelHeight;
        info->m_modifier = modifier;
        info->m_useTLottie = UseTLottie();
        info->m_precache = precache;

        auto hash = ColorHash(colorReplacement, info->m_colors);

        if (precache)
        {
            info->m_cachePath = BuildCachePath(std::wstring(filePath), hash, modifier, pixelWidth, pixelHeight);
            info->m_reader.Open(info->m_cachePath, pixelWidth, pixelHeight);
        }

        return info.as<Telegram::Native::LottieAnimation>();
    }

    Telegram::Native::LottieAnimation LottieAnimation::LoadFromData(hstring jsonData, int32_t pixelWidth, int32_t pixelHeight, hstring cacheKey, bool precache,
        Windows::Foundation::Collections::IMapView<int32_t, int32_t> colorReplacement, Telegram::Native::FitzModifier modifier)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 || jsonData.empty())
        {
            return nullptr;
        }

        auto info = winrt::make_self<LottieAnimation>();
        info->m_data = winrt::to_string(jsonData);
        info->m_pixelWidth = pixelWidth;
        info->m_pixelHeight = pixelHeight;
        info->m_modifier = modifier;
        info->m_useTLottie = UseTLottie();
        info->m_precache = precache && !cacheKey.empty();

        auto hash = ColorHash(colorReplacement, info->m_colors);

        if (info->m_precache)
        {
            // Data has no file of its own to sit beside, so its cache needs a home of its own.
            // TemporaryState rather than LocalState, where these used to land: the file is pure
            // derived data, and the reader treats a missing one as no cache and rebuilds - so
            // letting the OS reclaim it under pressure costs nothing but the rebuild. It also
            // keeps them out of the sweep, which only walks the per-session sticker directory.
            auto folder = Windows::Storage::ApplicationData::Current().TemporaryFolder().Path();
            auto base = std::wstring(folder) + L"\\" + std::wstring(cacheKey);

            info->m_cachePath = BuildCachePath(base, hash, modifier, pixelWidth, pixelHeight);
            info->m_reader.Open(info->m_cachePath, pixelWidth, pixelHeight);
        }

        return info.as<Telegram::Native::LottieAnimation>();
    }

    bool LottieAnimation::EnsureProducer() noexcept
    {
        if (m_producer != nullptr)
        {
            return true;
        }

        if (m_failed)
        {
            return false;
        }

        // One shot: a file that will not parse is not going to parse on the next frame either, and
        // retrying per frame would turn a broken sticker into a busy loop.
        m_failed = true;

        try
        {
            if (m_data.empty() && !m_path.empty())
            {
                m_data = DecompressFromFile(m_path);
            }

            if (m_data.empty())
            {
                return false;
            }

            std::shared_ptr<Cache::IFrameProducer> producer;

#ifdef HAS_TLOTTIE
            if (m_useTLottie)
            {
                std::vector<TLottieColorReplacement> replacements;
                replacements.reserve(m_colors.size());

                for (auto const& pair : m_colors)
                {
                    replacements.push_back({ pair.first, pair.second });
                }

                // TLOTTIE_CHANNEL_BGRA, always. The default is RGBA, and the two renderers share a
                // cache file - see TlottieFrameProducer.
                auto instance = tlottie_new_with_options(
                    reinterpret_cast<const uint8_t*>(m_data.data()), m_data.size(),
                    static_cast<uint32_t>(m_modifier),
                    nullptr, 0,
                    replacements.empty() ? nullptr : replacements.data(), replacements.size(),
                    TLOTTIE_CHANNEL_BGRA);

                if (instance == nullptr)
                {
                    return false;
                }

                producer = std::make_shared<TlottieFrameProducer>(instance, m_pixelWidth, m_pixelHeight);
            }
            else
#endif
            {
                auto animation = rlottie::Animation::loadFromData(
                    m_data, std::string(), std::string(), false, m_colors,
                    static_cast<rlottie::FitzModifier>(m_modifier));

                if (animation == nullptr)
                {
                    return false;
                }

                producer = std::make_shared<LottieFrameProducer>(std::move(animation), m_pixelWidth, m_pixelHeight);
            }

            auto frames = producer->FrameCount();
            if (frames == 0 || frames > MaxFrameCount || producer->FrameRate() > MaxFrameRate)
            {
                return false;
            }

            m_producer = std::move(producer);
            m_failed = false;

            // The source text is the producer's now.
            m_data.clear();
            m_data.shrink_to_fit();

            return true;
        }
        catch (...)
        {
            return false;
        }
    }

    void LottieAnimation::RenderSync(IBuffer bitmap, int32_t frame) noexcept
    {
        auto pixels = bitmap.data();
        if (pixels == nullptr || frame < 0)
        {
            return;
        }

        auto size = static_cast<size_t>(m_pixelWidth) * m_pixelHeight * 4;

        // The build this animation asked for may have finished. Adopting it here is what stops
        // the instance that did the caching from rendering every remaining frame by hand.
        if (m_building && !m_building->load(std::memory_order_relaxed) && !m_reader.IsOpen())
        {
            m_building = nullptr;
            m_reader.Open(m_cachePath, m_pixelWidth, m_pixelHeight);
        }

        if (m_reader.IsOpen())
        {
            if (m_scratch.size() < m_reader.MaxCompressedSize())
            {
                m_scratch.resize(m_reader.MaxCompressedSize());
            }

            if (m_reader.ReadFrame(frame, pixels, size, m_scratch.data(), m_scratch.size()))
            {
                ApplyColor(pixels);
                return;
            }

            // A cache that stops reading is one we should not be holding: fall through to the
            // renderer rather than freezing the sticker.
            m_reader.Close();
        }

        if (!EnsureProducer())
        {
            return;
        }

        if (m_producer->RenderFrame(static_cast<uint32_t>(frame), pixels, size))
        {
            ApplyColor(pixels);

            // Queued here rather than reported to the caller and queued back: the frame it needed
            // to draw first is already in the buffer, and the animation carries on rendering live
            // until the build lands, so nothing ever waits on it.
            RequestCache();
        }
    }

    void LottieAnimation::ApplyColor(uint8_t* pixels) noexcept
    {
        if (m_color.A == 0x00)
        {
            return;
        }

        auto count = static_cast<size_t>(m_pixelWidth) * m_pixelHeight;

        for (size_t i = 0; i < count; i++)
        {
            auto index = i * 4;
            auto alpha = pixels[index + 3];

            if (alpha != 0x00)
            {
                pixels[index + 0] = static_cast<uint8_t>((m_color.B * alpha + 127) / 255);
                pixels[index + 1] = static_cast<uint8_t>((m_color.G * alpha + 127) / 255);
                pixels[index + 2] = static_cast<uint8_t>((m_color.R * alpha + 127) / 255);
            }
        }
    }

    bool LottieAnimation::IsCaching() noexcept
    {
        return m_building && m_building->load(std::memory_order_relaxed);
    }

    void LottieAnimation::RequestCache() noexcept
    {
        // Once. A still has nothing to cache that rendering it again would not do just as fast.
        if (!m_precache || m_cachePath.empty() || m_building || m_producer == nullptr
            || m_producer->FrameCount() <= 1)
        {
            return;
        }

        // Null when the service refuses outright; the token of whoever got there first when this
        // key is already queued, so both animations watch the same flag.
        m_building = Cache::FrameCacheService::Instance().Enqueue(
            m_cachePath, std::weak_ptr<Cache::IFrameProducer>(m_producer));
    }

    double LottieAnimation::FrameRate() noexcept
    {
        if (m_reader.IsOpen())
        {
            return m_reader.FrameRate();
        }

        return EnsureProducer() ? m_producer->FrameRate() : 60;
    }

    int32_t LottieAnimation::TotalFrame() noexcept
    {
        if (m_reader.IsOpen())
        {
            return static_cast<int32_t>(m_reader.FrameCount());
        }

        return EnsureProducer() ? static_cast<int32_t>(m_producer->FrameCount()) : 0;
    }
}
