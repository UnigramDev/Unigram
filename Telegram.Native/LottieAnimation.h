#pragma once

#include "LottieAnimation.g.h"

#include <atomic>
#include <memory>
#include <string>
#include <vector>

#include "Cache/FrameCacheReader.h"
#include "Cache/FrameProducer.h"

using namespace winrt::Windows::Storage::Streams;
using namespace winrt::Windows::UI;

namespace winrt::Telegram::Native::implementation
{
    // A vector sticker, played from a cache file when there is one and rendered directly when there
    // is not. Building the cache belongs to Cache/FrameCacheService, shared with the video path, so
    // what is left here is a reader, a producer and a colour overlay.
    //
    // Gone with the move out of RLottie.UWP: a second copy of the compress queue and worker thread,
    // the LZ4 calls, the frame offset table, the per-instance decompression buffer, the per-key
    // lock, and the .tcache/.cache split. The two renderers now share one cache file, because they
    // produce the same premultiplied BGRA - see TlottieFrameProducer for the flag that makes that
    // true and must stay true.
    struct LottieAnimation : LottieAnimationT<LottieAnimation>
    {
        LottieAnimation() = default;

        virtual ~LottieAnimation()
        {
            Close();
        }

        void Close()
        {
            m_reader.Close();
            m_scratch.clear();
            m_scratch.shrink_to_fit();
            m_data.clear();
            m_data.shrink_to_fit();

            // Only this animation's reference: a build in flight holds its own, and the producer
            // goes when that ends rather than underneath it.
            m_producer = nullptr;
        }

        static Telegram::Native::LottieAnimation LoadFromFile(hstring filePath, int32_t pixelWidth, int32_t pixelHeight, bool precache, Windows::Foundation::Collections::IMapView<int32_t, int32_t> colorReplacement, Telegram::Native::FitzModifier modifier = Telegram::Native::FitzModifier::None);
        static Telegram::Native::LottieAnimation LoadFromData(hstring jsonData, int32_t pixelWidth, int32_t pixelHeight, hstring cacheKey, bool precache, Windows::Foundation::Collections::IMapView<int32_t, int32_t> colorReplacement, Telegram::Native::FitzModifier modifier = Telegram::Native::FitzModifier::None);

        static bool UseTLottie() noexcept;
        static void UseTLottie(bool value) noexcept;

        void SetColor(Color color)
        {
            m_color = color;
        }

        void RenderSync(IBuffer bitmap, int32_t frame) noexcept;
        void RenderSync(IBuffer bitmap, int32_t frame, bool clear) noexcept;

        bool IsCaching() noexcept;

        double FrameRate() noexcept;
        int32_t TotalFrame() noexcept;

        int32_t PixelWidth() noexcept { return m_pixelWidth; }
        int32_t PixelHeight() noexcept { return m_pixelHeight; }

    private:
        /// <summary>
        /// Parses the animation, which is deferred on purpose: an animation whose cache already
        /// exists never touches the JSON at all, and parsing is by far the most expensive part of
        /// loading one.
        /// </summary>
        bool EnsureProducer() noexcept;

        void ApplyColor(uint8_t* pixels) noexcept;

        /// <summary>Queues this animation's own build, once, after it has drawn a frame itself.</summary>
        void RequestCache() noexcept;

        std::shared_ptr<Cache::IFrameProducer> m_producer;
        Cache::FrameCacheReader m_reader;

        // Held until the producer is built, then released: it is only the source text.
        std::string m_data;
        std::vector<uint8_t> m_scratch;
        std::vector<std::pair<uint32_t, uint32_t>> m_colors;

        std::wstring m_cachePath;
        hstring m_path;

        int32_t m_pixelWidth{ 0 };
        int32_t m_pixelHeight{ 0 };
        Telegram::Native::FitzModifier m_modifier{ Telegram::Native::FitzModifier::None };

        // Latched at load, not read per frame: an animation keeps the renderer it was created with
        // even if the switch is flipped mid-playback.
        bool m_useTLottie{ false };
        bool m_precache{ false };

        // Set while this animation's build is queued or running, and cleared by the service when it
        // finishes or is dropped. An atomic load, because the alternative - asking the service - put
        // a global mutex and a path hash on the render worker, once per animation per frame.
        std::shared_ptr<std::atomic<bool>> m_building;
        bool m_failed{ false };

        Color m_color{};
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct LottieAnimation : LottieAnimationT<LottieAnimation, implementation::LottieAnimation>
    {
    };
}
