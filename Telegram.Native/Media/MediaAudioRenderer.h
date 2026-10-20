#pragma once

#include "Media/MediaEngineCommon.h"

#include <xaudio2.h>

#include <atomic>
#include <deque>
#include <memory>
#include <mutex>
#include <vector>

extern "C"
{
#include <libavfilter/avfilter.h>
#include <libswresample/swresample.h>
}

namespace winrt::Telegram::Native::Media::implementation
{
    // Plays decoded audio through XAudio2 and tells the engine what time it is.
    //
    // Clock, Rate, Volume and RequestRebuild may be called from any thread; everything else runs on
    // the engine's audio thread.
    class MediaAudioRenderer
    {
    public:
        explicit MediaAudioRenderer(MediaWaiter& waiter);
        ~MediaAudioRenderer();

        MediaAudioRenderer(const MediaAudioRenderer&) = delete;
        MediaAudioRenderer& operator=(const MediaAudioRenderer&) = delete;

        bool Open(int32_t sampleRate, double rate, double volume, bool mute);

        // Samples before `from` are dropped, which is what makes a seek land where it was asked to
        // rather than on the packet before it.
        void Enqueue(AVFrame const* frame, double pts, double from);
        void EndOfStream();

        // Everything queued or playing is dropped, for a seek.
        void Flush();

        // Submits what is decoded while less than the lead is queued in XAudio2, and starts or stops
        // the voice. Called whenever the audio thread wakes.
        void Pump(bool running);

        // The media time being heard right now. False until something has been submitted since the
        // last flush, and whenever everything submitted has been heard: the engine's own clock
        // carries on from there, rather than stand still where the sound of a file runs out
        // before its picture.
        bool Clock(double& time);

        // Media seconds decoded and not yet heard, and the part of that not yet given to XAudio2.
        double Buffered();
        double Pending() const;

        bool Starved();
        bool Finished();

        // Everything up to the end of the stream has been given to XAudio2.
        bool EndSubmitted() const noexcept { return m_endSubmitted.load(); }

        // No output could be built, so this plays nothing, and the engine runs without audio.
        bool Failed() const noexcept { return m_failed.load(); }

        // Whether an output was built again after having failed, once: what was decoded meanwhile
        // was dropped, so the engine has to start the audio over from where playback is.
        bool TakeRecovered() noexcept { return m_recovered.exchange(false); }

        void Rate(double rate);
        void Volume(double volume, bool mute);

        // The output device went away, or the default one changed: the next Pump builds a new
        // output and carries on from what was being heard.
        void RequestRebuild();

    private:
        struct VoiceCallback : IXAudio2VoiceCallback
        {
            MediaWaiter* waiter{ nullptr };

            void __stdcall OnBufferEnd(void*) noexcept override
            {
                waiter->Signal();
            }

            void __stdcall OnVoiceProcessingPassStart(UINT32) noexcept override {}
            void __stdcall OnVoiceProcessingPassEnd() noexcept override {}
            void __stdcall OnStreamEnd() noexcept override {}
            void __stdcall OnBufferStart(void*) noexcept override {}
            void __stdcall OnLoopEnd(void*) noexcept override {}
            void __stdcall OnVoiceError(void*, HRESULT) noexcept override {}
        };

        // Only flags the audio thread: releasing the engine from inside its own critical error
        // deadlocks against XAudio2's thread.
        struct EngineCallback : IXAudio2EngineCallback
        {
            MediaAudioRenderer* owner{ nullptr };

            void __stdcall OnCriticalError(HRESULT) noexcept override
            {
                owner->RequestRebuild();
            }

            void __stdcall OnProcessingPassStart() noexcept override {}
            void __stdcall OnProcessingPassEnd() noexcept override {}
        };

        using Samples = std::shared_ptr<std::vector<float>>;

        // Decoded audio at the stream's speed, interleaved stereo.
        struct Chunk
        {
            Samples samples;
            double pts{ 0 };
        };

        // What XAudio2 was given, at the speed it plays at. XAudio2 reads straight out of the
        // samples, so they live until the buffer has been played.
        struct Buffer
        {
            Samples samples;
            uint64_t start{ 0 };
            uint32_t frames{ 0 };
            double media{ 0 };
            double step{ 0 };
        };

        bool CreateOutput();
        void DestroyOutput();
        bool CreateVoiceLocked();
        void DestroyVoiceLocked();

        bool EnsureResampler(AVFrame const* frame);
        void EnqueueSilence(double pts, double duration);

        bool BuildTempo(double tempo);
        void FreeTempo();

        void Requeue(bool rebuild);
        void Retire();
        void Submit(Chunk const& chunk);
        void SubmitBuffer(Samples samples, uint32_t frames, double media, double step);
        void DrainTempo();

        uint64_t PlayedLocked();
        double MediaAtLocked(double frames) const;
        void PullTempo();

        static uint32_t Frames(Chunk const& chunk) noexcept;

        MediaWaiter& m_waiter;
        VoiceCallback m_voiceCallback;
        EngineCallback m_engineCallback;

        // Guards the voice and the submitted buffers, which Clock reads from the presenter.
        mutable std::mutex m_lock;

        winrt::com_ptr<IXAudio2> m_xaudio;
        IXAudio2MasteringVoice* m_master{ nullptr };
        IXAudio2SourceVoice* m_voice{ nullptr };
        uint32_t m_masterRate{ 48000 };
        bool m_running{ false };

        int32_t m_sampleRate{ 0 };

        SwrContext* m_swr{ nullptr };
        int32_t m_swrFormat{ -1 };
        int32_t m_swrRate{ 0 };
        AVChannelLayout m_swrLayout{};

        AVFilterGraph* m_tempoGraph{ nullptr };
        AVFilterContext* m_tempoSource{ nullptr };
        AVFilterContext* m_tempoSink{ nullptr };
        AVFrame* m_tempoFrame{ nullptr };

        // What was asked for, and what is applied: one only, if the filter could not be built, and
        // then the audio plays at its own speed and the picture follows it.
        double m_requestedTempo{ 1 };
        double m_tempo{ 1 };

        double m_tempoStart{ 0 };
        int64_t m_tempoIn{ 0 };
        int64_t m_tempoOut{ 0 };
        bool m_tempoStarted{ false };

        std::deque<Chunk> m_pending;
        std::atomic<uint64_t> m_pendingFrames{ 0 };

        // Where the next decoded audio should start, NAN until the first since the last flush.
        double m_nextPts{ NAN };

        // Already given to XAudio2, and kept at the stream's speed so that a speed change or a new
        // device can start again from what was being heard.
        std::deque<Chunk> m_retained;

        std::deque<Buffer> m_submitted;
        uint64_t m_submittedFrames{ 0 };
        double m_lastMedia{ 0 };

        uint64_t m_lastPlayed{ 0 };
        double m_lastPlayedAt{ 0 };

        // SamplesPlayed is documented as counting "since the voice last started", which could mean
        // a Stop and Start resets it. Accumulated by hand, so either reading of that holds.
        uint64_t m_rawPlayed{ 0 };
        uint64_t m_rawBase{ 0 };

        bool m_endOfStream{ false };
        std::atomic<bool> m_endSubmitted{ false };
        std::atomic<bool> m_failed{ false };
        std::atomic<bool> m_recovered{ false };

        std::atomic<double> m_rate{ 1 };
        std::atomic<double> m_volume{ 1 };
        std::atomic<bool> m_mute{ false };
        std::atomic<bool> m_volumeChanged{ true };
        std::atomic<bool> m_rebuild{ false };
    };
}
