#pragma once

#include "Media/VideoTranscoderLayer.g.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.Storage.Streams.h>

namespace winrt::Telegram::Native::Media::implementation
{
    struct VideoTranscoderLayer : VideoTranscoderLayerT<VideoTranscoderLayer>
    {
        VideoTranscoderLayer() = default;

        winrt::Windows::Storage::Streams::IBuffer Pixels() const { return m_pixels; }
        void Pixels(winrt::Windows::Storage::Streams::IBuffer const& value) { m_pixels = value; }

        winrt::Telegram::Native::LottieAnimation Lottie() const { return m_lottie; }
        void Lottie(winrt::Telegram::Native::LottieAnimation const& value) { m_lottie = value; }

        winrt::Telegram::Native::VideoAnimation Video() const { return m_video; }
        void Video(winrt::Telegram::Native::VideoAnimation const& value) { m_video = value; }

        int32_t PixelWidth() const { return m_pixelWidth; }
        void PixelWidth(int32_t value) { m_pixelWidth = value; }

        int32_t PixelHeight() const { return m_pixelHeight; }
        void PixelHeight(int32_t value) { m_pixelHeight = value; }

        winrt::Windows::Foundation::Rect Crop() const { return m_crop; }
        void Crop(winrt::Windows::Foundation::Rect const& value) { m_crop = value; }

        winrt::Windows::Foundation::Numerics::float3x2 Transform() const { return m_transform; }
        void Transform(winrt::Windows::Foundation::Numerics::float3x2 const& value) { m_transform = value; }

        bool White() const { return m_white; }
        void White(bool value) { m_white = value; }

    private:
        winrt::Windows::Storage::Streams::IBuffer m_pixels{ nullptr };
        winrt::Telegram::Native::LottieAnimation m_lottie{ nullptr };
        winrt::Telegram::Native::VideoAnimation m_video{ nullptr };
        int32_t m_pixelWidth{ 0 };
        int32_t m_pixelHeight{ 0 };
        winrt::Windows::Foundation::Rect m_crop{};
        winrt::Windows::Foundation::Numerics::float3x2 m_transform{ winrt::Windows::Foundation::Numerics::float3x2::identity() };
        bool m_white{ false };
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct VideoTranscoderLayer : VideoTranscoderLayerT<VideoTranscoderLayer, implementation::VideoTranscoderLayer>
    {
    };
}
