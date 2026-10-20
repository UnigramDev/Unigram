#include "pch.h"
#include "MediaAudioRenderer.h"

#include <winrt/Telegram.Native.h>

#include <cstdio>
#include <cstring>
#include <format>

extern "C"
{
#include <libavfilter/buffersink.h>
#include <libavfilter/buffersrc.h>
#include <libavutil/channel_layout.h>
}

namespace winrt::Telegram::Native::Media::implementation
{
    namespace
    {
        constexpr int32_t kChannels = 2;

        // How much XAudio2 is given ahead of what it plays. A pause stops the voice where it is,
        // but a speed change starts again from what is being heard, so this is how long the old
        // speed can still be heard; long enough to ride out the audio thread being descheduled.
        constexpr double kLead = 0.25;

        // Smaller gaps are timestamp rounding between packets, not missing audio. Larger ones than
        // the maximum are filled only that far, and the clock jumps the rest.
        constexpr double kMinGap = 0.02;
        constexpr double kMaxGap = 60;
    }

    MediaAudioRenderer::MediaAudioRenderer(MediaWaiter& waiter)
        : m_waiter(waiter)
    {
        m_voiceCallback.waiter = &waiter;
        m_engineCallback.owner = this;
    }

    MediaAudioRenderer::~MediaAudioRenderer()
    {
        DestroyOutput();
        FreeTempo();

        swr_free(&m_swr);
        av_channel_layout_uninit(&m_swrLayout);
    }

    bool MediaAudioRenderer::Open(int32_t sampleRate, double rate, double volume, bool mute)
    {
        m_sampleRate = sampleRate > 0 ? sampleRate : 48000;
        m_rate = rate;
        m_volume = volume;
        m_mute = mute;

        if (!CreateOutput())
        {
            m_failed = true;
            return false;
        }

        m_requestedTempo = rate;
        BuildTempo(rate);

        return true;
    }

    bool MediaAudioRenderer::CreateOutput()
    {
        winrt::com_ptr<IXAudio2> xaudio;
        HRESULT result = XAudio2Create(xaudio.put(), 0, XAUDIO2_DEFAULT_PROCESSOR);

        if (FAILED(result))
        {
            LOGGER_WARNING(L"XAudio2Create failed with 0x{:08x}", static_cast<uint32_t>(result));
            return false;
        }

        IXAudio2MasteringVoice* master = nullptr;
        result = xaudio->CreateMasteringVoice(&master, XAUDIO2_DEFAULT_CHANNELS, XAUDIO2_DEFAULT_SAMPLERATE,
            0, nullptr, nullptr, AudioCategory_Media);

        if (FAILED(result))
        {
            LOGGER_WARNING(L"CreateMasteringVoice failed with 0x{:08x}", static_cast<uint32_t>(result));
            return false;
        }

        XAUDIO2_VOICE_DETAILS details{};
        master->GetVoiceDetails(&details);

        xaudio->RegisterForCallbacks(&m_engineCallback);

        std::lock_guard<std::mutex> lock(m_lock);

        m_xaudio = xaudio;
        m_master = master;
        m_masterRate = details.InputSampleRate > 0 ? details.InputSampleRate : 48000;

        return CreateVoiceLocked();
    }

    void MediaAudioRenderer::DestroyOutput()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        DestroyVoiceLocked();

        if (m_master != nullptr)
        {
            m_master->DestroyVoice();
            m_master = nullptr;
        }

        if (m_xaudio)
        {
            m_xaudio->UnregisterForCallbacks(&m_engineCallback);
            m_xaudio = nullptr;
        }
    }

    bool MediaAudioRenderer::CreateVoiceLocked()
    {
        WAVEFORMATEX format{};
        format.wFormatTag = WAVE_FORMAT_IEEE_FLOAT;
        format.nChannels = kChannels;
        format.nSamplesPerSec = m_sampleRate;
        format.wBitsPerSample = 32;
        format.nBlockAlign = kChannels * sizeof(float);
        format.nAvgBytesPerSec = m_sampleRate * format.nBlockAlign;

        HRESULT result = m_xaudio->CreateSourceVoice(&m_voice, &format, 0, XAUDIO2_DEFAULT_FREQ_RATIO, &m_voiceCallback);

        if (FAILED(result))
        {
            LOGGER_WARNING(L"CreateSourceVoice failed with 0x{:08x}", static_cast<uint32_t>(result));
            m_voice = nullptr;
            return false;
        }

        m_running = false;
        m_volumeChanged = true;

        m_submitted.clear();
        m_submittedFrames = 0;
        m_lastPlayed = 0;
        m_lastPlayedAt = 0;
        m_rawPlayed = 0;
        m_rawBase = 0;

        return true;
    }

    void MediaAudioRenderer::DestroyVoiceLocked()
    {
        if (m_voice != nullptr)
        {
            // Synchronous: once it returns, XAudio2 has stopped reading the submitted samples.
            m_voice->DestroyVoice();
            m_voice = nullptr;
        }

        m_running = false;
    }

    bool MediaAudioRenderer::EnsureResampler(AVFrame const* frame)
    {
        if (m_swr != nullptr
            && frame->format == m_swrFormat
            && frame->sample_rate == m_swrRate
            && av_channel_layout_compare(&frame->ch_layout, &m_swrLayout) == 0)
        {
            return true;
        }

        swr_free(&m_swr);
        av_channel_layout_uninit(&m_swrLayout);

        AVChannelLayout input{};

        if (frame->ch_layout.order == AV_CHANNEL_ORDER_UNSPEC || frame->ch_layout.nb_channels == 0)
        {
            av_channel_layout_default(&input, std::max(1, frame->ch_layout.nb_channels));
        }
        else
        {
            av_channel_layout_copy(&input, &frame->ch_layout);
        }

        AVChannelLayout output = AV_CHANNEL_LAYOUT_STEREO;

        int result = swr_alloc_set_opts2(&m_swr,
            &output, AV_SAMPLE_FMT_FLT, m_sampleRate,
            &input, static_cast<AVSampleFormat>(frame->format), frame->sample_rate,
            0, nullptr);

        if (result >= 0)
        {
            result = swr_init(m_swr);
        }

        av_channel_layout_uninit(&input);

        if (result < 0)
        {
            LOGGER_WARNING(L"swr_init failed with {}", result);
            swr_free(&m_swr);
            return false;
        }

        m_swrFormat = frame->format;
        m_swrRate = frame->sample_rate;
        av_channel_layout_copy(&m_swrLayout, &frame->ch_layout);

        return true;
    }

    uint32_t MediaAudioRenderer::Frames(Chunk const& chunk) noexcept
    {
        return static_cast<uint32_t>(chunk.samples->size() / kChannels);
    }

    void MediaAudioRenderer::Enqueue(AVFrame const* frame, double pts, double from)
    {
        if (m_failed || !EnsureResampler(frame))
        {
            return;
        }

        const int capacity = swr_get_out_samples(m_swr, frame->nb_samples);
        if (capacity <= 0)
        {
            return;
        }

        auto samples = std::make_shared<std::vector<float>>(static_cast<size_t>(capacity) * kChannels);
        uint8_t* output = reinterpret_cast<uint8_t*>(samples->data());

        const int converted = swr_convert(m_swr, &output, capacity,
            const_cast<const uint8_t**>(frame->extended_data), frame->nb_samples);

        if (converted <= 0)
        {
            return;
        }

        size_t skip = 0;

        if (pts < from)
        {
            skip = static_cast<size_t>(std::llround((from - pts) * m_sampleRate));

            if (skip >= static_cast<size_t>(converted))
            {
                return;
            }

            pts += static_cast<double>(skip) / m_sampleRate;
        }

        if (skip > 0)
        {
            samples->erase(samples->begin(), samples->begin() + skip * kChannels);
        }

        samples->resize((static_cast<size_t>(converted) - skip) * kChannels);

        // The clock counts samples played, so audio that starts after the video - or resumes
        // after a hole - would make it jump ahead by the gap, and the picture skip to catch up.
        // Silence keeps it counting real time until the sound begins.
        const double expected = std::isnan(m_nextPts) ? from : m_nextPts;

        if (pts - expected > kMinGap)
        {
            EnqueueSilence(expected, std::min(pts - expected, kMaxGap));
        }

        m_nextPts = pts + static_cast<double>(samples->size() / kChannels) / m_sampleRate;

        Chunk chunk{ std::move(samples), pts };
        m_pendingFrames += Frames(chunk);
        m_pending.push_back(std::move(chunk));
    }

    void MediaAudioRenderer::EnqueueSilence(double pts, double duration)
    {
        const int64_t total = std::llround(duration * m_sampleRate);
        const int64_t step = m_sampleRate / 10;

        // In short chunks, so that a speed change or a new device can cut into it like any audio.
        for (int64_t offset = 0; offset < total; offset += step)
        {
            const int64_t frames = std::min(step, total - offset);
            auto samples = std::make_shared<std::vector<float>>(static_cast<size_t>(frames) * kChannels, 0.0f);

            m_pendingFrames += static_cast<uint64_t>(frames);
            m_pending.push_back({ std::move(samples), pts + static_cast<double>(offset) / m_sampleRate });
        }
    }

    void MediaAudioRenderer::EndOfStream()
    {
        m_endOfStream = true;
    }

    void MediaAudioRenderer::Flush()
    {
        {
            std::lock_guard<std::mutex> lock(m_lock);

            // A new voice rather than FlushSourceBuffers: that completes asynchronously, and a new
            // voice counts its samples from zero.
            DestroyVoiceLocked();

            if (m_xaudio)
            {
                CreateVoiceLocked();
            }

            m_lastMedia = 0;
        }

        m_pending.clear();
        m_pendingFrames = 0;
        m_retained.clear();
        m_nextPts = NAN;

        m_endOfStream = false;
        m_endSubmitted = false;

        BuildTempo(m_requestedTempo);
    }

    void MediaAudioRenderer::Pump(bool running)
    {
        if (m_rebuild.exchange(false))
        {
            Requeue(true);
        }

        const double rate = m_rate.load();
        if (rate != m_requestedTempo)
        {
            m_requestedTempo = rate;
            Requeue(false);
        }

        {
            std::lock_guard<std::mutex> lock(m_lock);

            if (m_voice == nullptr)
            {
                return;
            }

            if (m_volumeChanged.exchange(false))
            {
                m_voice->SetVolume(m_mute ? 0.0f : static_cast<float>(m_volume.load()));
            }

            if (running != m_running)
            {
                if (running)
                {
                    m_voice->Start(0);
                }
                else
                {
                    m_voice->Stop(0);
                }

                m_running = running;
            }
        }

        Retire();

        const uint64_t lead = static_cast<uint64_t>(kLead * m_sampleRate);

        while (!m_pending.empty())
        {
            {
                std::lock_guard<std::mutex> lock(m_lock);

                if (m_submittedFrames - PlayedLocked() >= lead)
                {
                    break;
                }
            }

            Chunk chunk = std::move(m_pending.front());
            m_pending.pop_front();
            m_pendingFrames -= Frames(chunk);

            Submit(chunk);
            m_retained.push_back(std::move(chunk));
        }

        if (m_endOfStream && m_pending.empty() && !m_endSubmitted)
        {
            DrainTempo();
            m_endSubmitted = true;
        }
    }

    void MediaAudioRenderer::Requeue(bool rebuild)
    {
        double from = 0;
        bool known = false;

        {
            std::lock_guard<std::mutex> lock(m_lock);

            known = m_submittedFrames > 0;
            from = known ? MediaAtLocked(static_cast<double>(PlayedLocked())) : 0;
        }

        if (rebuild)
        {
            DestroyOutput();

            if (!CreateOutput())
            {
                LOGGER_WARNING(L"No audio output could be built, carrying on without audio");

                m_failed = true;
                m_pending.clear();
                m_pendingFrames = 0;
                m_retained.clear();
                return;
            }

            m_failed = false;
        }
        else
        {
            std::lock_guard<std::mutex> lock(m_lock);

            DestroyVoiceLocked();

            if (m_xaudio)
            {
                CreateVoiceLocked();
            }
        }

        std::deque<Chunk> chunks = std::move(m_retained);
        m_retained.clear();

        for (auto& chunk : m_pending)
        {
            chunks.push_back(std::move(chunk));
        }

        m_pending.clear();

        // Starts again from what was being heard: what went before it is gone, and the chunk it
        // falls in is cut at that sample.
        while (known && !chunks.empty())
        {
            auto& chunk = chunks.front();
            const uint32_t frames = Frames(chunk);

            if (chunk.pts + static_cast<double>(frames) / m_sampleRate <= from)
            {
                chunks.pop_front();
                continue;
            }

            if (chunk.pts < from)
            {
                const size_t skip = static_cast<size_t>(std::llround((from - chunk.pts) * m_sampleRate));

                if (skip > 0 && skip < frames)
                {
                    chunk.samples = std::make_shared<std::vector<float>>(chunk.samples->begin() + skip * kChannels, chunk.samples->end());
                    chunk.pts += static_cast<double>(skip) / m_sampleRate;
                }
            }

            break;
        }

        uint64_t pending = 0;

        for (auto const& chunk : chunks)
        {
            pending += Frames(chunk);
        }

        m_pending = std::move(chunks);
        m_pendingFrames = pending;
        m_endSubmitted = false;

        BuildTempo(m_requestedTempo);

        std::lock_guard<std::mutex> lock(m_lock);
        m_lastMedia = from;
    }

    void MediaAudioRenderer::Retire()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_voice == nullptr)
        {
            return;
        }

        const uint64_t played = PlayedLocked();

        while (!m_submitted.empty())
        {
            auto const& buffer = m_submitted.front();

            if (buffer.start + buffer.frames > played)
            {
                break;
            }

            m_lastMedia = buffer.media + buffer.frames * buffer.step;
            m_submitted.pop_front();
        }

        const double heard = MediaAtLocked(static_cast<double>(played));

        while (!m_retained.empty())
        {
            auto const& chunk = m_retained.front();

            if (chunk.pts + static_cast<double>(Frames(chunk)) / m_sampleRate >= heard)
            {
                break;
            }

            m_retained.pop_front();
        }
    }

    void MediaAudioRenderer::Submit(Chunk const& chunk)
    {
        const uint32_t frames = Frames(chunk);
        if (frames == 0)
        {
            return;
        }

        if (m_tempoGraph == nullptr)
        {
            SubmitBuffer(chunk.samples, frames, chunk.pts, 1.0 / m_sampleRate);
            return;
        }

        if (!m_tempoStarted)
        {
            m_tempoStarted = true;
            m_tempoStart = chunk.pts;
            m_tempoIn = 0;
            m_tempoOut = 0;
        }

        AVFrame* input = av_frame_alloc();
        if (input == nullptr)
        {
            return;
        }

        AVChannelLayout stereo = AV_CHANNEL_LAYOUT_STEREO;

        input->format = AV_SAMPLE_FMT_FLT;
        input->sample_rate = m_sampleRate;
        input->nb_samples = frames;
        input->pts = m_tempoIn;
        av_channel_layout_copy(&input->ch_layout, &stereo);

        if (av_frame_get_buffer(input, 0) >= 0)
        {
            std::memcpy(input->data[0], chunk.samples->data(), static_cast<size_t>(frames) * kChannels * sizeof(float));

            if (av_buffersrc_add_frame_flags(m_tempoSource, input, 0) >= 0)
            {
                m_tempoIn += frames;
                PullTempo();
            }
        }

        av_frame_free(&input);
    }

    void MediaAudioRenderer::DrainTempo()
    {
        if (m_tempoGraph != nullptr && m_tempoStarted && av_buffersrc_add_frame_flags(m_tempoSource, nullptr, 0) >= 0)
        {
            PullTempo();
        }
    }

    void MediaAudioRenderer::PullTempo()
    {
        while (av_buffersink_get_frame(m_tempoSink, m_tempoFrame) >= 0)
        {
            const int frames = m_tempoFrame->nb_samples;

            if (m_tempoFrame->format == AV_SAMPLE_FMT_FLT && m_tempoFrame->ch_layout.nb_channels == kChannels && frames > 0)
            {
                const float* data = reinterpret_cast<const float*>(m_tempoFrame->data[0]);
                auto samples = std::make_shared<std::vector<float>>(data, data + static_cast<size_t>(frames) * kChannels);

                // Output sample n of the filter is input sample n times the tempo, counted from the
                // first chunk fed into it since it was built.
                const double media = m_tempoStart + static_cast<double>(m_tempoOut) * m_tempo / m_sampleRate;
                SubmitBuffer(std::move(samples), frames, media, m_tempo / m_sampleRate);
            }

            m_tempoOut += frames;
            av_frame_unref(m_tempoFrame);
        }
    }

    void MediaAudioRenderer::SubmitBuffer(Samples samples, uint32_t frames, double media, double step)
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_voice == nullptr)
        {
            return;
        }

        XAUDIO2_BUFFER buffer{};
        buffer.AudioBytes = frames * kChannels * sizeof(float);
        buffer.pAudioData = reinterpret_cast<const BYTE*>(samples->data());

        if (FAILED(m_voice->SubmitSourceBuffer(&buffer)))
        {
            return;
        }

        m_submitted.push_back({ std::move(samples), m_submittedFrames, frames, media, step });
        m_submittedFrames += frames;
    }

    bool MediaAudioRenderer::BuildTempo(double tempo)
    {
        FreeTempo();

        if (std::abs(tempo - 1.0) < 0.001)
        {
            std::lock_guard<std::mutex> lock(m_lock);
            m_tempo = 1;
            return true;
        }

        const AVFilter* abuffer = avfilter_get_by_name("abuffer");
        const AVFilter* abuffersink = avfilter_get_by_name("abuffersink");
        const AVFilter* atempo = avfilter_get_by_name("atempo");

        bool built = false;

        if (abuffer != nullptr && abuffersink != nullptr && atempo != nullptr)
        {
            m_tempoGraph = avfilter_graph_alloc();
        }

        if (m_tempoGraph != nullptr)
        {
            char args[128];
            snprintf(args, sizeof(args), "time_base=1/%d:sample_rate=%d:sample_fmt=flt:channel_layout=stereo", m_sampleRate, m_sampleRate);

            built = avfilter_graph_create_filter(&m_tempoSource, abuffer, "in", args, nullptr, m_tempoGraph) >= 0
                && avfilter_graph_create_filter(&m_tempoSink, abuffersink, "out", nullptr, nullptr, m_tempoGraph) >= 0;

            AVFilterContext* last = m_tempoSource;
            double remaining = tempo;
            int index = 0;

            // One stage covers 0.5 to 100, so anything slower takes two.
            while (built)
            {
                const double stage = remaining < 0.5 ? 0.5 : remaining;
                remaining /= stage;

                char name[16];
                char options[32];
                snprintf(name, sizeof(name), "tempo%d", index++);
                snprintf(options, sizeof(options), "tempo=%.6f", stage);

                AVFilterContext* filter = nullptr;
                built = avfilter_graph_create_filter(&filter, atempo, name, options, nullptr, m_tempoGraph) >= 0
                    && avfilter_link(last, 0, filter, 0) >= 0;

                last = filter;

                if (std::abs(remaining - 1.0) < 0.001)
                {
                    break;
                }
            }

            built = built
                && avfilter_link(last, 0, m_tempoSink, 0) >= 0
                && avfilter_graph_config(m_tempoGraph, nullptr) >= 0;
        }

        if (built)
        {
            m_tempoFrame = av_frame_alloc();
            built = m_tempoFrame != nullptr;
        }

        if (!built)
        {
            LOGGER_WARNING(L"atempo could not be built for {}, playing audio at its own speed", tempo);
            FreeTempo();
        }

        std::lock_guard<std::mutex> lock(m_lock);
        m_tempo = built ? tempo : 1;

        return built;
    }

    void MediaAudioRenderer::FreeTempo()
    {
        avfilter_graph_free(&m_tempoGraph);
        m_tempoSource = nullptr;
        m_tempoSink = nullptr;

        av_frame_free(&m_tempoFrame);

        m_tempoStarted = false;
    }

    uint64_t MediaAudioRenderer::PlayedLocked()
    {
        if (m_voice == nullptr)
        {
            return 0;
        }

        XAUDIO2_VOICE_STATE state{};
        m_voice->GetState(&state, 0);

        if (state.SamplesPlayed < m_rawPlayed)
        {
            m_rawBase += m_rawPlayed;
        }

        m_rawPlayed = state.SamplesPlayed;
        return m_rawBase + m_rawPlayed;
    }

    double MediaAudioRenderer::MediaAtLocked(double frames) const
    {
        for (auto const& buffer : m_submitted)
        {
            if (frames < static_cast<double>(buffer.start))
            {
                return buffer.media;
            }

            if (frames < static_cast<double>(buffer.start + buffer.frames))
            {
                return buffer.media + (frames - static_cast<double>(buffer.start)) * buffer.step;
            }
        }

        if (!m_submitted.empty())
        {
            auto const& buffer = m_submitted.back();
            return buffer.media + buffer.frames * buffer.step;
        }

        return m_lastMedia;
    }

    bool MediaAudioRenderer::Clock(double& time)
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_voice == nullptr || m_submittedFrames == 0)
        {
            return false;
        }

        const uint64_t played = PlayedLocked();

        if (m_endSubmitted && played >= m_submittedFrames)
        {
            return false;
        }

        // SamplesPlayed moves once per XAudio2 pass, every 10 ms. Between passes the position is
        // carried forward on the wall clock, so the picture is not paced in 10 ms steps.
        const double now = MediaNow();

        if (played != m_lastPlayed)
        {
            m_lastPlayed = played;
            m_lastPlayedAt = now;
        }

        double frames = static_cast<double>(played);

        if (m_running)
        {
            frames += std::min(now - m_lastPlayedAt, 0.015) * m_sampleRate;
            frames = std::min(frames, static_cast<double>(m_submittedFrames));
        }

        double media = MediaAtLocked(frames);

        // What XAudio2 has read is still on its way to the speakers.
        XAUDIO2_PERFORMANCE_DATA performance{};
        m_xaudio->GetPerformanceData(&performance);

        media -= static_cast<double>(performance.CurrentLatencyInSamples) / m_masterRate * m_tempo;

        time = media;
        return true;
    }

    double MediaAudioRenderer::Buffered()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        const uint64_t played = PlayedLocked();
        const uint64_t queued = m_submittedFrames > played ? m_submittedFrames - played : 0;

        return static_cast<double>(queued) * m_tempo / m_sampleRate + static_cast<double>(m_pendingFrames.load()) / m_sampleRate;
    }

    double MediaAudioRenderer::Pending() const
    {
        return m_sampleRate > 0 ? static_cast<double>(m_pendingFrames.load()) / m_sampleRate : 0;
    }

    bool MediaAudioRenderer::Starved()
    {
        if (m_pendingFrames > 0)
        {
            return false;
        }

        std::lock_guard<std::mutex> lock(m_lock);

        if (m_voice == nullptr)
        {
            return true;
        }

        XAUDIO2_VOICE_STATE state{};
        m_voice->GetState(&state, XAUDIO2_VOICE_NOSAMPLESPLAYED);

        return state.BuffersQueued == 0;
    }

    bool MediaAudioRenderer::Finished()
    {
        return m_failed || (m_endSubmitted && Starved());
    }

    void MediaAudioRenderer::Rate(double rate)
    {
        m_rate = rate;
        m_waiter.Signal();
    }

    void MediaAudioRenderer::Volume(double volume, bool mute)
    {
        m_volume = volume;
        m_mute = mute;
        m_volumeChanged = true;
        m_waiter.Signal();
    }

    void MediaAudioRenderer::RequestRebuild()
    {
        m_rebuild = true;
        m_waiter.Signal();
    }
}
