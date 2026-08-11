#pragma once

#include <audioclient.h>
#include <mmdeviceapi.h>
#include <mfapi.h>
#include <mfidl.h>

#include <functional>
#include <vector>

namespace winrt::Telegram::Native::Calls::implementation
{
    // Captures what another process is rendering, so a screencast can carry its audio.
    // Derived from the Microsoft ApplicationLoopbackAudio sample, rewritten onto
    // C++/WinRT and reduced to the one work item that actually needs the MMCSS queue.
    //
    // Ref-counted because the queued MF work item holds a reference of its own: hold it
    // through the com_ptr that make_self returns, never by value.
    struct VoipLoopbackCapture : implements<VoipLoopbackCapture,
        IMFAsyncCallback,
        IActivateAudioInterfaceCompletionHandler>
    {
        // Called on the capture queue with 16-bit mono samples at 48kHz. That format is
        // not ours to pick: it is what tgcalls' ExternalAudioRecorder reads back out of
        // addExternalAudioSamples, ten milliseconds at a time.
        using SamplesHandler = std::function<void(std::vector<uint8_t>&&)>;

        explicit VoipLoopbackCapture(SamplesHandler samples);
        ~VoipLoopbackCapture();

        // processId is the process to capture; includeProcessTree captures it and its
        // children, otherwise everything except it. Synchronous: it waits for the audio
        // interface to activate, which is why it returns the failure rather than throwing.
        hresult Start(uint32_t processId, bool includeProcessTree) noexcept;

        // Blocks only for as long as a sample callback is already inside the handler.
        void Stop() noexcept;

        HRESULT __stdcall GetParameters(DWORD* flags, DWORD* queue) noexcept override;
        HRESULT __stdcall Invoke(IMFAsyncResult*) noexcept override;
        HRESULT __stdcall ActivateCompleted(IActivateAudioInterfaceAsyncOperation* operation) noexcept override;

    private:
        hresult Activate(uint32_t processId, bool includeProcessTree) noexcept;
        hresult OnActivated(IActivateAudioInterfaceAsyncOperation* operation) noexcept;
        hresult ReadPackets();

        // Guards everything the capture queue touches against Stop. Held while the
        // handler runs, so Stop cannot pull the capture client out from under it.
        slim_mutex m_lock;
        bool m_capturing{ false };

        const SamplesHandler m_samples;

        com_ptr<IAudioClient> m_audioClient;
        com_ptr<IAudioCaptureClient> m_captureClient;
        com_ptr<IMFAsyncResult> m_sampleReadyResult;

        handle m_sampleReady;
        handle m_activated;
        hresult m_activateResult{ E_UNEXPECTED };

        WAVEFORMATEX m_format{};
        MFWORKITEM_KEY m_sampleReadyKey{ 0 };
        DWORD m_queueId{ 0 };
        bool m_mediaFoundation{ false };
    };
}
