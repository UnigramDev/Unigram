#include "pch.h"
#include "H264Encoder.h"

#include <winrt/Telegram.Native.h>

#include <codecapi.h>
#include <icodecapi.h>
#include <mferror.h>

#include <algorithm>
#include <cstring>

#pragma comment(lib, "mfuuid.lib")

namespace winrt::Telegram::Native::Media::implementation
{
    namespace
    {
        // An encoder that does not support a setting keeps its own default.
        void SetCodecValue(ICodecAPI* codec, const GUID& key, uint32_t value)
        {
            VARIANT variant{};
            variant.vt = VT_UI4;
            variant.ulVal = value;

            codec->SetValue(&key, &variant);
        }

        winrt::com_ptr<IMFMediaType> CreateType(const GUID& subtype, uint32_t width, uint32_t height, uint32_t frameRate, bool hardware)
        {
            winrt::com_ptr<IMFMediaType> type;
            winrt::check_hresult(MFCreateMediaType(type.put()));

            winrt::check_hresult(type->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video));
            winrt::check_hresult(type->SetGUID(MF_MT_SUBTYPE, subtype));
            winrt::check_hresult(MFSetAttributeSize(type.get(), MF_MT_FRAME_SIZE, width, height));
            winrt::check_hresult(MFSetAttributeRatio(type.get(), MF_MT_FRAME_RATE, frameRate, 1));
            winrt::check_hresult(MFSetAttributeRatio(type.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1));
            winrt::check_hresult(type->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive));

            // What the frames were converted with: the GPU's video processor writes BT.709, libyuv
            // BT.601, both in studio range.
            type->SetUINT32(MF_MT_YUV_MATRIX, hardware ? MFVideoTransferMatrix_BT709 : MFVideoTransferMatrix_BT601);
            type->SetUINT32(MF_MT_VIDEO_NOMINAL_RANGE, MFNominalRange_16_235);

            return type;
        }
    }

    H264Encoder::~H264Encoder()
    {
        Close();

        if (m_started)
        {
            MFShutdown();
        }
    }

    void H264Encoder::Open(ID3D11Device* device, bool allowHardware, uint32_t width, uint32_t height, uint32_t frameRate, uint32_t bitrate, OutputHandler output)
    {
        m_width = width;
        m_height = height;
        m_frameRate = frameRate;
        m_bitrate = bitrate;
        m_output = std::move(output);

        // Counted per process: every MFStartup is matched by the MFShutdown in the destructor.
        winrt::check_hresult(MFStartup(MF_VERSION, MFSTARTUP_LITE));
        m_started = true;

        if (allowHardware && device != nullptr)
        {
            try
            {
                OpenTransform(device, true);
                m_hardware = true;
                return;
            }
            catch (winrt::hresult_error const& error)
            {
                LOGGER_WARNING(L"The hardware H.264 encoder could not be set up ({:#x}), encoding in software", static_cast<uint32_t>(error.code()));
                Close();
            }
        }

        OpenTransform(nullptr, false);
        m_hardware = false;
    }

    void H264Encoder::OpenTransform(ID3D11Device* device, bool hardware)
    {
        MFT_REGISTER_TYPE_INFO outputInfo{ MFMediaType_Video, MFVideoFormat_H264 };

        IMFActivate** activates = nullptr;
        UINT32 count = 0;

        const UINT32 flags = hardware
            ? MFT_ENUM_FLAG_HARDWARE | MFT_ENUM_FLAG_SORTANDFILTER
            : MFT_ENUM_FLAG_SYNCMFT | MFT_ENUM_FLAG_SORTANDFILTER;

        winrt::check_hresult(MFTEnumEx(MFT_CATEGORY_VIDEO_ENCODER, flags, nullptr, &outputInfo, &activates, &count));

        HRESULT result = MF_E_TOPO_CODEC_NOT_FOUND;

        for (UINT32 i = 0; i < count; i++)
        {
            if (!m_transform)
            {
                result = activates[i]->ActivateObject(IID_PPV_ARGS(m_transform.put()));

                if (SUCCEEDED(result))
                {
                    m_activate.copy_from(activates[i]);

                    LPWSTR name = nullptr;
                    UINT32 length = 0;

                    if (SUCCEEDED(activates[i]->GetAllocatedString(MFT_FRIENDLY_NAME_Attribute, &name, &length)))
                    {
                        m_name.assign(name, length);
                        CoTaskMemFree(name);
                    }
                }
            }

            activates[i]->Release();
        }

        CoTaskMemFree(activates);
        winrt::check_hresult(result);

        if (hardware)
        {
            // A hardware transform is asynchronous: it takes input and gives output when its events
            // say so, and has to be unlocked before anything else.
            winrt::com_ptr<IMFAttributes> attributes;
            winrt::check_hresult(m_transform->GetAttributes(attributes.put()));
            winrt::check_hresult(attributes->SetUINT32(MF_TRANSFORM_ASYNC_UNLOCK, TRUE));

            m_events = m_transform.as<IMFMediaEventGenerator>();

            UINT token = 0;
            winrt::check_hresult(MFCreateDXGIDeviceManager(&token, m_manager.put()));
            winrt::check_hresult(m_manager->ResetDevice(device, token));
            winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_SET_D3D_MANAGER, reinterpret_cast<ULONG_PTR>(m_manager.get())));
        }

        DWORD inputs = 0;
        DWORD outputs = 0;

        if (SUCCEEDED(m_transform->GetStreamCount(&inputs, &outputs)) && inputs == 1 && outputs == 1)
        {
            // Most transforms leave their stream ids at 0, and say so with E_NOTIMPL.
            DWORD inputId = 0;
            DWORD outputId = 0;

            if (SUCCEEDED(m_transform->GetStreamIDs(1, &inputId, 1, &outputId)))
            {
                m_inputId = inputId;
                m_outputId = outputId;
            }
        }

        if (auto codec = m_transform.try_as<ICodecAPI>())
        {
            SetCodecValue(codec.get(), CODECAPI_AVEncCommonRateControlMode, eAVEncCommonRateControlMode_UnconstrainedVBR);
            SetCodecValue(codec.get(), CODECAPI_AVEncCommonMeanBitRate, m_bitrate);
            SetCodecValue(codec.get(), CODECAPI_AVEncMPVGOPSize, m_frameRate);
            SetCodecValue(codec.get(), CODECAPI_AVEncMPVDefaultBPictureCount, 0);
        }

        // The output type goes first: an encoder offers its input types only once it knows what it
        // is making.
        auto output = CreateType(MFVideoFormat_H264, m_width, m_height, m_frameRate, hardware);
        winrt::check_hresult(output->SetUINT32(MF_MT_AVG_BITRATE, m_bitrate));
        winrt::check_hresult(output->SetUINT32(MF_MT_MPEG2_PROFILE, eAVEncH264VProfile_Main));
        winrt::check_hresult(m_transform->SetOutputType(m_outputId, output.get(), 0));

        auto input = CreateType(MFVideoFormat_NV12, m_width, m_height, m_frameRate, hardware);
        winrt::check_hresult(m_transform->SetInputType(m_inputId, input.get(), 0));

        MFT_OUTPUT_STREAM_INFO info{};
        winrt::check_hresult(m_transform->GetOutputStreamInfo(m_outputId, &info));

        m_providesSamples = (info.dwFlags & (MFT_OUTPUT_STREAM_PROVIDES_SAMPLES | MFT_OUTPUT_STREAM_CAN_PROVIDE_SAMPLES)) != 0;
        m_outputSize = info.cbSize;

        winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, 0));
        winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_NOTIFY_START_OF_STREAM, 0));
    }

    void H264Encoder::Close()
    {
        if (m_transform)
        {
            m_transform->ProcessMessage(MFT_MESSAGE_NOTIFY_END_STREAMING, 0);
        }

        m_events = nullptr;
        m_transform = nullptr;

        // An activated hardware transform keeps its resources until its activation shuts it down.
        if (m_activate)
        {
            m_activate->ShutdownObject();
            m_activate = nullptr;
        }

        m_manager = nullptr;
        m_needInput = 0;
        m_name.clear();
    }

    void H264Encoder::EncodeTexture(ID3D11Texture2D* texture, int64_t time, int64_t duration)
    {
        winrt::com_ptr<IMFMediaBuffer> buffer;
        winrt::check_hresult(MFCreateDXGISurfaceBuffer(__uuidof(ID3D11Texture2D), texture, 0, FALSE, buffer.put()));

        if (auto buffer2D = buffer.try_as<IMF2DBuffer>())
        {
            DWORD length = 0;
            if (SUCCEEDED(buffer2D->GetContiguousLength(&length)))
            {
                buffer->SetCurrentLength(length);
            }
        }

        winrt::com_ptr<IMFSample> sample;
        winrt::check_hresult(MFCreateSample(sample.put()));
        winrt::check_hresult(sample->AddBuffer(buffer.get()));
        winrt::check_hresult(sample->SetSampleTime(time));
        winrt::check_hresult(sample->SetSampleDuration(duration));

        Submit(sample.get());
    }

    void H264Encoder::EncodeMemory(const uint8_t* y, int yStride, const uint8_t* uv, int uvStride, int64_t time, int64_t duration)
    {
        const DWORD size = m_width * m_height * 3 / 2;

        winrt::com_ptr<IMFMediaBuffer> buffer;
        winrt::check_hresult(MFCreateMemoryBuffer(size, buffer.put()));

        BYTE* data = nullptr;
        winrt::check_hresult(buffer->Lock(&data, nullptr, nullptr));

        // Packed: the software encoder takes rows exactly a frame wide.
        for (uint32_t row = 0; row < m_height; row++)
        {
            std::memcpy(data + row * m_width, y + static_cast<size_t>(row) * yStride, m_width);
        }

        auto chroma = data + m_width * m_height;

        for (uint32_t row = 0; row < m_height / 2; row++)
        {
            std::memcpy(chroma + row * m_width, uv + static_cast<size_t>(row) * uvStride, m_width);
        }

        buffer->Unlock();
        winrt::check_hresult(buffer->SetCurrentLength(size));

        winrt::com_ptr<IMFSample> sample;
        winrt::check_hresult(MFCreateSample(sample.put()));
        winrt::check_hresult(sample->AddBuffer(buffer.get()));
        winrt::check_hresult(sample->SetSampleTime(time));
        winrt::check_hresult(sample->SetSampleDuration(duration));

        Submit(sample.get());
    }

    void H264Encoder::Submit(IMFSample* sample)
    {
        if (m_events)
        {
            // Output is taken as it comes while waiting to be asked for input.
            while (m_needInput == 0)
            {
                Pump();
            }

            m_needInput--;
            winrt::check_hresult(m_transform->ProcessInput(m_inputId, sample, 0));
            return;
        }

        while (true)
        {
            const HRESULT result = m_transform->ProcessInput(m_inputId, sample, 0);
            if (result != MF_E_NOTACCEPTING)
            {
                winrt::check_hresult(result);
                break;
            }

            // Full: what it has made has to be taken before it takes more.
            while (PullOne())
            {
            }
        }

        while (PullOne())
        {
        }
    }

    void H264Encoder::Pump()
    {
        winrt::com_ptr<IMFMediaEvent> event;
        winrt::check_hresult(m_events->GetEvent(0, event.put()));

        MediaEventType type = MEUnknown;
        winrt::check_hresult(event->GetType(&type));

        HRESULT status = S_OK;
        winrt::check_hresult(event->GetStatus(&status));
        winrt::check_hresult(status);

        switch (type)
        {
        case METransformNeedInput:
            m_needInput++;
            break;
        case METransformHaveOutput:
            PullOne();
            break;
        case METransformDrainComplete:
            m_drained = true;
            break;
        default:
            break;
        }
    }

    // False when the encoder has nothing to give yet.
    bool H264Encoder::PullOne()
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            MFT_OUTPUT_DATA_BUFFER buffer{};
            buffer.dwStreamID = m_outputId;

            winrt::com_ptr<IMFSample> allocated;

            if (!m_providesSamples)
            {
                winrt::com_ptr<IMFMediaBuffer> memory;
                winrt::check_hresult(MFCreateSample(allocated.put()));
                winrt::check_hresult(MFCreateMemoryBuffer(std::max<DWORD>(m_outputSize, m_width * m_height * 3 / 2), memory.put()));
                winrt::check_hresult(allocated->AddBuffer(memory.get()));

                buffer.pSample = allocated.get();
            }

            DWORD status = 0;
            const HRESULT result = m_transform->ProcessOutput(0, 1, &buffer, &status);

            if (buffer.pEvents != nullptr)
            {
                buffer.pEvents->Release();
            }

            if (result == MF_E_TRANSFORM_NEED_MORE_INPUT)
            {
                return false;
            }

            if (result == MF_E_TRANSFORM_STREAM_CHANGE)
            {
                // The encoder settled its output differently: take what it offers, then ask again.
                winrt::com_ptr<IMFMediaType> type;
                winrt::check_hresult(m_transform->GetOutputAvailableType(m_outputId, 0, type.put()));
                winrt::check_hresult(m_transform->SetOutputType(m_outputId, type.get(), 0));

                MFT_OUTPUT_STREAM_INFO info{};
                winrt::check_hresult(m_transform->GetOutputStreamInfo(m_outputId, &info));

                m_providesSamples = (info.dwFlags & (MFT_OUTPUT_STREAM_PROVIDES_SAMPLES | MFT_OUTPUT_STREAM_CAN_PROVIDE_SAMPLES)) != 0;
                m_outputSize = info.cbSize;

                // An asynchronous transform says when the frame is ready with another event.
                if (m_events)
                {
                    return true;
                }

                continue;
            }

            winrt::check_hresult(result);

            if (buffer.pSample != nullptr)
            {
                Deliver(buffer.pSample);

                // A sample the encoder provided is ours to release; one we allocated is held above.
                if (m_providesSamples)
                {
                    buffer.pSample->Release();
                }
            }

            return true;
        }

        return false;
    }

    void H264Encoder::Deliver(IMFSample* sample)
    {
        winrt::com_ptr<IMFMediaBuffer> buffer;
        winrt::check_hresult(sample->ConvertToContiguousBuffer(buffer.put()));

        LONGLONG time = 0;
        sample->GetSampleTime(&time);

        const bool key = MFGetAttributeUINT32(sample, MFSampleExtension_CleanPoint, FALSE) != FALSE;

        BYTE* data = nullptr;
        DWORD length = 0;
        winrt::check_hresult(buffer->Lock(&data, nullptr, &length));

        try
        {
            if (length > 0)
            {
                m_output(data, length, time, key);
            }
        }
        catch (...)
        {
            buffer->Unlock();
            throw;
        }

        buffer->Unlock();
    }

    void H264Encoder::Drain()
    {
        if (!m_transform)
        {
            return;
        }

        winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0));
        winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_COMMAND_DRAIN, 0));

        if (m_events)
        {
            m_drained = false;

            while (!m_drained)
            {
                Pump();
            }
        }
        else
        {
            while (PullOne())
            {
            }
        }
    }
}
