#include "pch.h"
#include "VoipLoopbackCapture.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    namespace
    {
        // Virtual device that loopback-captures by process id rather than by endpoint.
        constexpr auto ProcessLoopbackDevice = L"VAD\\Process_Loopback";

        // Declared in the Windows SDK only from a later header set than we compile
        // against, so they are reproduced here exactly as the SDK defines them.
        enum ProcessLoopbackMode
        {
            IncludeTargetProcessTree = 0,
            ExcludeTargetProcessTree = 1
        };

        struct ProcessLoopbackParams
        {
            DWORD TargetProcessId;
            ProcessLoopbackMode ProcessLoopbackMode;
        };

        enum ActivationType
        {
            ActivationTypeDefault = 0,
            ActivationTypeProcessLoopback = 1
        };

        struct ActivationParams
        {
            ActivationType ActivationType;
            union
            {
                ProcessLoopbackParams ProcessLoopbackParams;
            };
        };
    }

    VoipLoopbackCapture::VoipLoopbackCapture(SamplesHandler samples)
        : m_samples(std::move(samples))
    {
    }

    VoipLoopbackCapture::~VoipLoopbackCapture()
    {
        Stop();
    }

    hresult VoipLoopbackCapture::Start(uint32_t processId, bool includeProcessTree) noexcept
    {
        m_sampleReady.attach(CreateEventW(nullptr, false, false, nullptr));
        m_activated.attach(CreateEventW(nullptr, false, false, nullptr));

        if (!m_sampleReady || !m_activated)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        if (auto result = MFStartup(MF_VERSION, MFSTARTUP_LITE); FAILED(result))
        {
            return result;
        }

        // Paired in Stop, which the destructor calls even when we fail below.
        m_mediaFoundation = true;

        // Capture has to run at MMCSS priority or it drops packets under load, and this
        // is the one way to ask for that from an app container.
        DWORD taskId = 0;
        if (auto result = MFLockSharedWorkQueue(L"Capture", 0, &taskId, &m_queueId); FAILED(result))
        {
            return result;
        }

        if (auto result = Activate(processId, includeProcessTree); FAILED(result))
        {
            return result;
        }

        if (auto result = m_audioClient->Start(); FAILED(result))
        {
            return result;
        }

        slim_lock_guard const guard(m_lock);
        m_capturing = true;

        return MFPutWaitingWorkItem(m_sampleReady.get(), 0, m_sampleReadyResult.get(), &m_sampleReadyKey);
    }

    hresult VoipLoopbackCapture::Activate(uint32_t processId, bool includeProcessTree) noexcept
    {
        ActivationParams activationParams{};
        activationParams.ActivationType = ActivationTypeProcessLoopback;
        activationParams.ProcessLoopbackParams.TargetProcessId = processId;
        activationParams.ProcessLoopbackParams.ProcessLoopbackMode = includeProcessTree
            ? IncludeTargetProcessTree
            : ExcludeTargetProcessTree;

        PROPVARIANT activateParams{};
        activateParams.vt = VT_BLOB;
        activateParams.blob.cbSize = sizeof(activationParams);
        activateParams.blob.pBlobData = reinterpret_cast<BYTE*>(&activationParams);

        com_ptr<IActivateAudioInterfaceAsyncOperation> operation;
        auto result = ActivateAudioInterfaceAsync(ProcessLoopbackDevice, __uuidof(IAudioClient), &activateParams,
            static_cast<IActivateAudioInterfaceCompletionHandler*>(this), operation.put());

        if (FAILED(result))
        {
            return result;
        }

        // ActivateCompleted signals this; activationParams has to outlive the wait.
        WaitForSingleObject(m_activated.get(), INFINITE);
        return m_activateResult;
    }

    HRESULT __stdcall VoipLoopbackCapture::ActivateCompleted(IActivateAudioInterfaceAsyncOperation* operation) noexcept
    {
        m_activateResult = OnActivated(operation);
        SetEvent(m_activated.get());

        // The activation itself succeeded even when what we did with it did not; the
        // failure travels back through m_activateResult.
        return S_OK;
    }

    hresult VoipLoopbackCapture::OnActivated(IActivateAudioInterfaceAsyncOperation* operation) noexcept
    {
        HRESULT activateResult = E_UNEXPECTED;
        com_ptr<IUnknown> audioInterface;

        if (auto result = operation->GetActivateResult(&activateResult, audioInterface.put()); FAILED(result))
        {
            return result;
        }

        if (FAILED(activateResult))
        {
            return activateResult;
        }

        m_audioClient = audioInterface.try_as<IAudioClient>();
        if (m_audioClient == nullptr)
        {
            return E_NOINTERFACE;
        }

        m_format.wFormatTag = WAVE_FORMAT_PCM;
        m_format.nChannels = 1;
        m_format.nSamplesPerSec = 48000;
        m_format.wBitsPerSample = 16;
        m_format.nBlockAlign = m_format.nChannels * m_format.wBitsPerSample / 8;
        m_format.nAvgBytesPerSec = m_format.nSamplesPerSec * m_format.nBlockAlign;

        // AUTOCONVERTPCM sits in the periodicity argument rather than in the stream
        // flags, which is where the Microsoft sample put it and how this has always
        // shipped. Left alone deliberately: moving it changes what the audio engine is
        // asked for, and that wants testing against a real capture, not reasoning.
        if (auto result = m_audioClient->Initialize(AUDCLNT_SHAREMODE_SHARED,
            AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_EVENTCALLBACK,
            200000,
            AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM,
            &m_format,
            nullptr); FAILED(result))
        {
            return result;
        }

        if (auto result = m_audioClient->GetService(IID_PPV_ARGS(m_captureClient.put())); FAILED(result))
        {
            return result;
        }

        if (auto result = MFCreateAsyncResult(nullptr, static_cast<IMFAsyncCallback*>(this), nullptr,
            m_sampleReadyResult.put()); FAILED(result))
        {
            return result;
        }

        return m_audioClient->SetEventHandle(m_sampleReady.get());
    }

    void VoipLoopbackCapture::Stop() noexcept
    {
        {
            slim_lock_guard const guard(m_lock);

            if (m_capturing)
            {
                m_capturing = false;

                if (m_sampleReadyKey != 0)
                {
                    MFCancelWorkItem(m_sampleReadyKey);
                    m_sampleReadyKey = 0;
                }
            }

            if (m_audioClient)
            {
                m_audioClient->Stop();
            }

            m_sampleReadyResult = nullptr;
            m_captureClient = nullptr;
            m_audioClient = nullptr;
        }

        if (m_queueId != 0)
        {
            MFUnlockWorkQueue(m_queueId);
            m_queueId = 0;
        }

        if (m_mediaFoundation)
        {
            m_mediaFoundation = false;
            MFShutdown();
        }
    }

    HRESULT __stdcall VoipLoopbackCapture::GetParameters(DWORD* flags, DWORD* queue) noexcept
    {
        *flags = 0;
        *queue = m_queueId;

        return S_OK;
    }

    HRESULT __stdcall VoipLoopbackCapture::Invoke(IMFAsyncResult*) noexcept
    {
        slim_lock_guard const guard(m_lock);

        if (!m_capturing)
        {
            return S_OK;
        }

        if (FAILED(ReadPackets()))
        {
            m_capturing = false;
            return S_OK;
        }

        return MFPutWaitingWorkItem(m_sampleReady.get(), 0, m_sampleReadyResult.get(), &m_sampleReadyKey);
    }

    hresult VoipLoopbackCapture::ReadPackets()
    {
        // The engine is free to split what it accumulated across several packets, and
        // it does not run us once per packet, so drain everything that is ready.
        for (UINT32 packetFrames = 0;
            SUCCEEDED(m_captureClient->GetNextPacketSize(&packetFrames)) && packetFrames > 0;)
        {
            BYTE* data = nullptr;
            UINT32 frames = 0;
            DWORD flags = 0;

            if (auto result = m_captureClient->GetBuffer(&data, &frames, &flags, nullptr, nullptr); FAILED(result))
            {
                return result;
            }

            // Size from what GetBuffer actually handed back, not from the packet size we
            // asked about: they are allowed to differ.
            auto samples = std::vector<uint8_t>(size_t(frames) * m_format.nBlockAlign);

            // A silent buffer's contents are undefined, so it has to be zeroed rather
            // than copied. Still pushed, to keep the stream continuous.
            if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) == 0)
            {
                memcpy(samples.data(), data, samples.size());
            }

            m_captureClient->ReleaseBuffer(frames);

            if (m_samples && samples.size() > 0)
            {
                m_samples(std::move(samples));
            }
        }

        return S_OK;
    }
}
