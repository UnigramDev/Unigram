#pragma once

#include <mfidl.h>
#include <mftransform.h>

#include <cstdint>
#include <functional>
#include <string>

namespace winrt::Telegram::Native::Media::implementation
{
    // Windows' H.264 encoder, driven by hand rather than through ffmpeg or a sink writer, so that
    // we choose it, know which one we got, and can give a hardware one a device: a vendor's
    // transform is fed textures from our D3D11 device, which is how they expect to be driven, and
    // Microsoft's software one is fed memory when there is no hardware or it fails to set up.
    class H264Encoder
    {
    public:
        // The encoded bytes are Annex B; the time is in 100ns units, as given to Encode.
        using OutputHandler = std::function<void(const uint8_t* data, size_t size, int64_t time, bool key)>;

        H264Encoder() = default;
        ~H264Encoder();

        H264Encoder(const H264Encoder&) = delete;
        H264Encoder& operator=(const H264Encoder&) = delete;

        // A hardware encoder is tried first when allowed and the device can share its textures;
        // any failure while setting it up falls back to software. Throws when neither opens.
        void Open(ID3D11Device* device, bool allowHardware, uint32_t width, uint32_t height, uint32_t frameRate, uint32_t bitrate, OutputHandler output);

        bool IsHardware() const
        {
            return m_hardware;
        }

        const std::wstring& Name() const
        {
            return m_name;
        }

        // For a hardware encoder: an NV12 texture on the device given to Open.
        void EncodeTexture(ID3D11Texture2D* texture, int64_t time, int64_t duration);

        // For the software encoder: NV12 planes in memory.
        void EncodeMemory(const uint8_t* y, int yStride, const uint8_t* uv, int uvStride, int64_t time, int64_t duration);

        // Hands over every frame still inside the encoder.
        void Drain();

    private:
        void OpenTransform(ID3D11Device* device, bool hardware);
        void Close();

        void Submit(IMFSample* sample);
        void Pump();
        bool PullOne();
        void Deliver(IMFSample* sample);

        winrt::com_ptr<IMFActivate> m_activate;
        winrt::com_ptr<IMFTransform> m_transform;
        winrt::com_ptr<IMFMediaEventGenerator> m_events;
        winrt::com_ptr<IMFDXGIDeviceManager> m_manager;

        OutputHandler m_output;

        DWORD m_inputId{ 0 };
        DWORD m_outputId{ 0 };
        DWORD m_outputSize{ 0 };
        bool m_providesSamples{ false };

        uint32_t m_width{ 0 };
        uint32_t m_height{ 0 };
        uint32_t m_frameRate{ 0 };
        uint32_t m_bitrate{ 0 };

        bool m_started{ false };
        bool m_hardware{ false };
        bool m_drained{ false };
        uint32_t m_needInput{ 0 };

        std::wstring m_name;
    };
}
