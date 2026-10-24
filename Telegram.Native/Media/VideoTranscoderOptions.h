#pragma once

#include "Media/VideoTranscoderOptions.g.h"

#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.UI.h>

#include <vector>

namespace winrt::Telegram::Native::Media::implementation
{
    struct VideoTranscoderOptions : VideoTranscoderOptionsT<VideoTranscoderOptions>
    {
        VideoTranscoderOptions() = default;

        hstring InputPath() const { return m_inputPath; }
        void InputPath(hstring const& value) { m_inputPath = value; }

        hstring OutputPath() const { return m_outputPath; }
        void OutputPath(hstring const& value) { m_outputPath = value; }

        int64_t StartTime() const { return m_startTime; }
        void StartTime(int64_t value) { m_startTime = value; }

        int64_t StopTime() const { return m_stopTime; }
        void StopTime(int64_t value) { m_stopTime = value; }

        bool Mute() const { return m_mute; }
        void Mute(bool value) { m_mute = value; }

        int32_t OutputWidth() const { return m_outputWidth; }
        void OutputWidth(int32_t value) { m_outputWidth = value; }

        int32_t OutputHeight() const { return m_outputHeight; }
        void OutputHeight(int32_t value) { m_outputHeight = value; }

        winrt::Windows::Foundation::Numerics::float3x2 Transform() const { return m_transform; }
        void Transform(winrt::Windows::Foundation::Numerics::float3x2 const& value) { m_transform = value; }

        winrt::Windows::UI::Color BackgroundTop() const { return m_backgroundTop; }
        void BackgroundTop(winrt::Windows::UI::Color const& value) { m_backgroundTop = value; }

        winrt::Windows::UI::Color BackgroundBottom() const { return m_backgroundBottom; }
        void BackgroundBottom(winrt::Windows::UI::Color const& value) { m_backgroundBottom = value; }

        int32_t VideoBitrate() const { return m_videoBitrate; }
        void VideoBitrate(int32_t value) { m_videoBitrate = value; }

        int32_t AudioBitrate() const { return m_audioBitrate; }
        void AudioBitrate(int32_t value) { m_audioBitrate = value; }

        bool MonoAudio() const { return m_monoAudio; }
        void MonoAudio(bool value) { m_monoAudio = value; }

        int32_t MaxFrameRate() const { return m_maxFrameRate; }
        void MaxFrameRate(int32_t value) { m_maxFrameRate = value; }

        bool HardwareEncoding() const { return m_hardwareEncoding; }
        void HardwareEncoding(bool value) { m_hardwareEncoding = value; }

        bool RoundBlur() const { return m_roundBlur; }
        void RoundBlur(bool value) { m_roundBlur = value; }

        winrt::Windows::Storage::Streams::IBuffer StillPixels() const { return m_stillPixels; }
        void StillPixels(winrt::Windows::Storage::Streams::IBuffer const& value) { m_stillPixels = value; }

        int32_t StillWidth() const { return m_stillWidth; }
        void StillWidth(int32_t value) { m_stillWidth = value; }

        int32_t StillHeight() const { return m_stillHeight; }
        void StillHeight(int32_t value) { m_stillHeight = value; }

        int64_t StillDuration() const { return m_stillDuration; }
        void StillDuration(int64_t value) { m_stillDuration = value; }

        void AddLayer(winrt::Telegram::Native::Media::VideoTranscoderLayer const& layer) { m_layers.push_back(layer); }
        const std::vector<winrt::Telegram::Native::Media::VideoTranscoderLayer>& Layers() const { return m_layers; }

    private:
        hstring m_inputPath;
        hstring m_outputPath;
        int64_t m_startTime{ 0 };
        int64_t m_stopTime{ 0 };
        bool m_mute{ false };
        int32_t m_outputWidth{ 0 };
        int32_t m_outputHeight{ 0 };
        winrt::Windows::Foundation::Numerics::float3x2 m_transform{ winrt::Windows::Foundation::Numerics::float3x2::identity() };
        winrt::Windows::UI::Color m_backgroundTop{ 255, 0, 0, 0 };
        winrt::Windows::UI::Color m_backgroundBottom{ 255, 0, 0, 0 };
        int32_t m_videoBitrate{ 0 };
        int32_t m_audioBitrate{ 0 };
        bool m_monoAudio{ false };
        int32_t m_maxFrameRate{ 0 };
        bool m_hardwareEncoding{ true };
        bool m_roundBlur{ false };
        winrt::Windows::Storage::Streams::IBuffer m_stillPixels{ nullptr };
        int32_t m_stillWidth{ 0 };
        int32_t m_stillHeight{ 0 };
        int64_t m_stillDuration{ 0 };
        std::vector<winrt::Telegram::Native::Media::VideoTranscoderLayer> m_layers;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct VideoTranscoderOptions : VideoTranscoderOptionsT<VideoTranscoderOptions, implementation::VideoTranscoderOptions>
    {
    };
}
