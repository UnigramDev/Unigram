#include "pch.h"
#include "SoundPlayer.h"
#if __has_include("Media/SoundPlayer.g.cpp")
#include "Media/SoundPlayer.g.cpp"
#endif

#include <xaudio2.h>

#include <array>
#include <algorithm>
#include <chrono>
#include <condition_variable>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>

extern "C"
{
#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
}

namespace winrt::Telegram::Native::Media::implementation
{
    namespace
    {
        constexpr size_t CategoryCount = 4;

        // One engine per AUDIO_STREAM_CATEGORY: the category is a property of the mastering
        // voice, and the two the app needs behave differently. Communications is what makes
        // Windows duck other applications while the phone rings and route to the endpoint the
        // user picked for calls; alerts are ducked by it in turn.
        enum class Endpoint
        {
            Alerts,
            Communications
        };

        constexpr size_t EndpointCount = 2;

        constexpr Endpoint EndpointOf(size_t category) noexcept
        {
            return category == static_cast<size_t>(SoundCategory::Call)
                || category == static_cast<size_t>(SoundCategory::VideoChat)
                ? Endpoint::Communications
                : Endpoint::Alerts;
        }

        // A chat can go hours without making a sound, so the worker gives the engines up
        // shortly after the last one finishes rather than hold an audio endpoint open. Long
        // enough that a burst of notifications builds them once.
        constexpr auto IdleTimeout = std::chrono::seconds(5);

        constexpr size_t IoBufferSize = 32 * 1024;

        // A notification sound is a few seconds; both bounds are generous for one and keep a
        // malformed or mischosen file from being decoded into a buffer that matters.
        constexpr int64_t MaxFileSize = 8 * 1024 * 1024;
        constexpr int MaxSeconds = 30;

        // Decoded once into interleaved 16 bit PCM at the file's own rate: XAudio2 resamples
        // per voice, so there is nothing to match, and 16 bit halves what an effect costs to
        // hold on to.
        struct SoundBuffer
        {
            WAVEFORMATEX format{};
            std::vector<int16_t> samples;
        };

        struct MemoryStream
        {
            const uint8_t* data;
            int64_t size;
            int64_t offset;
        };

        int MemoryRead(void* opaque, uint8_t* buffer, int length)
        {
            auto stream = static_cast<MemoryStream*>(opaque);
            auto available = stream->size - stream->offset;

            if (available <= 0)
            {
                return AVERROR_EOF;
            }

            length = static_cast<int>(std::min<int64_t>(length, available));
            memcpy(buffer, stream->data + stream->offset, length);
            stream->offset += length;

            return length;
        }

        int64_t MemorySeek(void* opaque, int64_t offset, int whence)
        {
            auto stream = static_cast<MemoryStream*>(opaque);

            switch (whence)
            {
            case SEEK_SET:
                stream->offset = offset;
                break;
            case SEEK_CUR:
                stream->offset += offset;
                break;
            case SEEK_END:
                stream->offset = stream->size + offset;
                break;
            case AVSEEK_SIZE:
                return stream->size;
            default:
                return AVERROR(EINVAL);
            }

            stream->offset = std::clamp<int64_t>(stream->offset, 0, stream->size);
            return stream->offset;
        }

        std::vector<uint8_t> ReadAllBytes(const std::wstring& path)
        {
            // FromApp, because a custom notification sound is a downloaded file outside the
            // package and the plain CreateFile2 is denied there.
            auto file = CreateFile2FromAppW(path.c_str(), GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);
            if (file == INVALID_HANDLE_VALUE)
            {
                return {};
            }

            std::vector<uint8_t> bytes;
            LARGE_INTEGER size{};

            if (GetFileSizeEx(file, &size) && size.QuadPart > 0 && size.QuadPart <= MaxFileSize)
            {
                bytes.resize(static_cast<size_t>(size.QuadPart));

                DWORD read = 0;
                if (!ReadFile(file, bytes.data(), static_cast<DWORD>(bytes.size()), &read, nullptr)
                    || read != bytes.size())
                {
                    bytes.clear();
                }
            }

            CloseHandle(file);
            return bytes;
        }

        constexpr int16_t ToInt16(float value) noexcept
        {
            return static_cast<int16_t>(std::clamp(value, -1.0f, 1.0f) * 32767.0f);
        }

        // ffmpeg hands back whatever the decoder produces - mp3float and vorbis are planar
        // float, opus can be packed 16 bit - so the interleaving XAudio2 wants is done here
        // instead of linking swresample and shipping a second DLL for it.
        bool AppendSamples(const AVFrame* frame, std::vector<int16_t>& samples)
        {
            const auto format = static_cast<AVSampleFormat>(frame->format);
            const auto packed = av_get_packed_sample_fmt(format);
            const auto planar = av_sample_fmt_is_planar(format) != 0;

            const auto channels = frame->ch_layout.nb_channels;
            const auto count = frame->nb_samples;

            const auto offset = samples.size();
            samples.resize(offset + static_cast<size_t>(count) * channels);

            for (int index = 0; index < count; index++)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    const auto data = frame->extended_data[planar ? channel : 0];
                    const auto position = planar ? index : index * channels + channel;

                    int16_t value;

                    switch (packed)
                    {
                    case AV_SAMPLE_FMT_U8:
                        value = static_cast<int16_t>((reinterpret_cast<const uint8_t*>(data)[position] - 128) * 256);
                        break;
                    case AV_SAMPLE_FMT_S16:
                        value = reinterpret_cast<const int16_t*>(data)[position];
                        break;
                    case AV_SAMPLE_FMT_S32:
                        value = static_cast<int16_t>(reinterpret_cast<const int32_t*>(data)[position] >> 16);
                        break;
                    case AV_SAMPLE_FMT_FLT:
                        value = ToInt16(reinterpret_cast<const float*>(data)[position]);
                        break;
                    case AV_SAMPLE_FMT_DBL:
                        value = ToInt16(static_cast<float>(reinterpret_cast<const double*>(data)[position]));
                        break;
                    default:
                        samples.resize(offset);
                        return false;
                    }

                    samples[offset + static_cast<size_t>(index) * channels + channel] = value;
                }
            }

            return true;
        }

        struct DecodeContext
        {
            AVFormatContext* format{ nullptr };
            AVIOContext* io{ nullptr };
            AVCodecContext* codec{ nullptr };
            AVPacket* packet{ nullptr };
            AVFrame* frame{ nullptr };

            ~DecodeContext()
            {
                av_frame_free(&frame);
                av_packet_free(&packet);
                avcodec_free_context(&codec);

                if (format != nullptr)
                {
                    // avformat_open_input sets AVFMT_FLAG_CUSTOM_IO when pb was assigned before
                    // the call, so this leaves the context below alone for us to free.
                    avformat_close_input(&format);
                }

                if (io != nullptr)
                {
                    av_freep(&io->buffer);
                    avio_context_free(&io);
                }
            }
        };

        std::shared_ptr<SoundBuffer> Decode(const std::wstring& path)
        {
            auto bytes = ReadAllBytes(path);
            if (bytes.empty())
            {
                LOGGER_WARNING(L"{} is missing, empty or too large to be a sound", path);
                return nullptr;
            }

            MemoryStream stream{ bytes.data(), static_cast<int64_t>(bytes.size()), 0 };
            DecodeContext context;

            // Read from the bytes already in hand rather than through ffmpeg's file protocol,
            // which nothing else in the app exercises.
            auto buffer = static_cast<uint8_t*>(av_malloc(IoBufferSize));
            if (buffer == nullptr)
            {
                return nullptr;
            }

            context.io = avio_alloc_context(buffer, IoBufferSize, 0, &stream, MemoryRead, nullptr, MemorySeek);
            if (context.io == nullptr)
            {
                av_free(buffer);
                return nullptr;
            }

            context.format = avformat_alloc_context();
            if (context.format == nullptr)
            {
                return nullptr;
            }

            context.format->pb = context.io;

            // The name is never opened - pb answers every read - but the demuxer probe takes
            // the extension as a hint.
            auto name = winrt::to_string(path);

            if (avformat_open_input(&context.format, name.c_str(), nullptr, nullptr) < 0
                || avformat_find_stream_info(context.format, nullptr) < 0)
            {
                LOGGER_WARNING(L"{} could not be demuxed", path);
                return nullptr;
            }

            auto index = av_find_best_stream(context.format, AVMEDIA_TYPE_AUDIO, -1, -1, nullptr, 0);
            if (index < 0)
            {
                LOGGER_WARNING(L"{} has no audio stream", path);
                return nullptr;
            }

            auto parameters = context.format->streams[index]->codecpar;
            auto decoder = avcodec_find_decoder(parameters->codec_id);
            if (decoder == nullptr)
            {
                LOGGER_WARNING(L"{} needs a decoder this build does not have", path);
                return nullptr;
            }

            context.codec = avcodec_alloc_context3(decoder);
            if (context.codec == nullptr
                || avcodec_parameters_to_context(context.codec, parameters) < 0
                || avcodec_open2(context.codec, decoder, nullptr) < 0)
            {
                return nullptr;
            }

            context.packet = av_packet_alloc();
            context.frame = av_frame_alloc();

            if (context.packet == nullptr || context.frame == nullptr)
            {
                return nullptr;
            }

            auto sound = std::make_shared<SoundBuffer>();
            size_t limit = 0;

            auto receive = [&]
            {
                while (avcodec_receive_frame(context.codec, context.frame) >= 0)
                {
                    auto channels = context.frame->ch_layout.nb_channels;
                    auto rate = context.frame->sample_rate;

                    if (channels <= 0 || channels > XAUDIO2_MAX_AUDIO_CHANNELS || rate <= 0)
                    {
                        return false;
                    }

                    if (sound->format.nChannels == 0)
                    {
                        sound->format.wFormatTag = WAVE_FORMAT_PCM;
                        sound->format.nChannels = static_cast<WORD>(channels);
                        sound->format.nSamplesPerSec = static_cast<DWORD>(rate);
                        sound->format.wBitsPerSample = 16;
                        sound->format.nBlockAlign = static_cast<WORD>(channels * sizeof(int16_t));
                        sound->format.nAvgBytesPerSec = sound->format.nSamplesPerSec * sound->format.nBlockAlign;

                        limit = static_cast<size_t>(rate) * channels * MaxSeconds;
                    }
                    else if (sound->format.nChannels != channels
                        || sound->format.nSamplesPerSec != static_cast<DWORD>(rate))
                    {
                        // One buffer is submitted for the whole sound, so a file that changes
                        // shape halfway through is played up to the point where it does.
                        return false;
                    }

                    auto appended = AppendSamples(context.frame, sound->samples);
                    av_frame_unref(context.frame);

                    if (!appended || sound->samples.size() >= limit)
                    {
                        return false;
                    }
                }

                return true;
            };

            auto reading = true;

            while (reading && av_read_frame(context.format, context.packet) >= 0)
            {
                if (context.packet->stream_index == index
                    && avcodec_send_packet(context.codec, context.packet) >= 0)
                {
                    reading = receive();
                }

                av_packet_unref(context.packet);
            }

            if (reading && avcodec_send_packet(context.codec, nullptr) >= 0)
            {
                receive();
            }

            if (sound->samples.empty())
            {
                LOGGER_WARNING(L"{} decoded to nothing", path);
                return nullptr;
            }

            return sound;
        }

        class SoundEngine;

        struct VoiceCallback : IXAudio2VoiceCallback
        {
            size_t category{ 0 };

            void __stdcall OnBufferEnd(void*) noexcept override;

            void __stdcall OnVoiceProcessingPassStart(UINT32) noexcept override {}
            void __stdcall OnVoiceProcessingPassEnd() noexcept override {}
            void __stdcall OnStreamEnd() noexcept override {}
            void __stdcall OnBufferStart(void*) noexcept override {}
            void __stdcall OnLoopEnd(void*) noexcept override {}
            void __stdcall OnVoiceError(void*, HRESULT) noexcept override {}
        };

        struct EngineCallback : IXAudio2EngineCallback
        {
            size_t endpoint{ 0 };

            void __stdcall OnCriticalError(HRESULT) noexcept override;

            void __stdcall OnProcessingPassStart() noexcept override {}
            void __stdcall OnProcessingPassEnd() noexcept override {}
        };

        struct Engine
        {
            com_ptr<IXAudio2> xaudio{ nullptr };
            IXAudio2MasteringVoice* mastering{ nullptr };
            EngineCallback callback;
        };

        struct Voice
        {
            IXAudio2SourceVoice* source{ nullptr };
            std::shared_ptr<SoundBuffer> sound;
            VoiceCallback callback;
        };

        // Everything XAudio2 owns, and everything decoded for it, lives here - on the worker's
        // stack, for as long as the worker runs. Keeping it out of the singleton is what lets
        // the worker exit without coordinating with whoever starts the next one: two of them
        // briefly overlapping is two independent engines, not a race over one.
        class Session
        {
        public:
            Session();
            ~Session();

            void Play(const std::wstring& path, int32_t loopCount, size_t category);
            void Stop(size_t category);
            void StopAll();

            void Reset(size_t endpoint);

            bool Playing() const;

        private:
            bool Ensure(Engine& engine, AUDIO_STREAM_CATEGORY category);

            std::array<Engine, EndpointCount> _engines;
            std::array<Voice, CategoryCount> _voices;
            std::unordered_map<std::wstring, std::shared_ptr<SoundBuffer>> _decoded;
        };

        struct Command
        {
            enum class Kind
            {
                Play,
                StopAll
            };

            Kind kind{ Kind::Play };
            size_t category{ 0 };
            int32_t loopCount{ 0 };
            std::wstring path;
        };

        class SoundEngine
        {
        public:
            static SoundEngine& Current();

            void Post(Command&& command);

            // Both are called from XAudio2's audio thread, and both do no more than flag the
            // worker: destroying a voice from inside a callback deadlocks against that thread,
            // and so does releasing the engine from its own critical error. Which is also why
            // the lock below is never held across a call into XAudio2 - the audio thread can be
            // waiting for it inside the very call that is being waited on.
            void Finished(size_t category);
            void Faulted(size_t endpoint);

        private:
            void Run() noexcept;
            void Work(Session& session);

            std::mutex _mutex;
            std::condition_variable _signal;

            std::vector<Command> _queue;
            uint32_t _finished{ 0 };
            uint32_t _faulted{ 0 };
            bool _running{ false };
        };

        void VoiceCallback::OnBufferEnd(void*) noexcept
        {
            SoundEngine::Current().Finished(category);
        }

        void EngineCallback::OnCriticalError(HRESULT) noexcept
        {
            SoundEngine::Current().Faulted(endpoint);
        }

        Session::Session()
        {
            for (size_t index = 0; index < _voices.size(); index++)
            {
                _voices[index].callback.category = index;
            }

            for (size_t index = 0; index < _engines.size(); index++)
            {
                _engines[index].callback.endpoint = index;
            }
        }

        Session::~Session()
        {
            StopAll();

            for (size_t index = 0; index < _engines.size(); index++)
            {
                Reset(index);
            }
        }

        bool Session::Ensure(Engine& engine, AUDIO_STREAM_CATEGORY category)
        {
            if (engine.xaudio != nullptr)
            {
                return true;
            }

            com_ptr<IXAudio2> xaudio;
            auto result = XAudio2Create(xaudio.put());

            if (FAILED(result))
            {
                LOGGER_WARNING(L"XAudio2Create failed with 0x{:08x}", static_cast<uint32_t>(result));
                return false;
            }

            IXAudio2MasteringVoice* mastering = nullptr;
            result = xaudio->CreateMasteringVoice(&mastering, XAUDIO2_DEFAULT_CHANNELS,
                XAUDIO2_DEFAULT_SAMPLERATE, 0, nullptr, nullptr, category);

            if (FAILED(result))
            {
                LOGGER_WARNING(L"CreateMasteringVoice failed with 0x{:08x}", static_cast<uint32_t>(result));
                return false;
            }

            xaudio->RegisterForCallbacks(&engine.callback);

            engine.xaudio = xaudio;
            engine.mastering = mastering;

            return true;
        }

        void Session::Play(const std::wstring& path, int32_t loopCount, size_t category)
        {
            auto cached = _decoded.find(path);
            auto sound = cached != _decoded.end() ? cached->second : Decode(path);

            if (sound == nullptr)
            {
                return;
            }

            // The two notification sounds are played over and over, so they are worth keeping
            // decoded for as long as the worker lives. A ringtone is both long and rare, and
            // the cache is bounded by leaving it out.
            if (cached == _decoded.end() && sound->samples.size() * sizeof(int16_t) <= 512 * 1024)
            {
                _decoded.emplace(path, sound);
            }

            auto endpoint = EndpointOf(category);
            auto& engine = _engines[static_cast<size_t>(endpoint)];

            if (!Ensure(engine, endpoint == Endpoint::Communications
                ? AudioCategory_Communications
                : AudioCategory_Alerts))
            {
                return;
            }

            Stop(category);

            auto& voice = _voices[category];
            auto result = engine.xaudio->CreateSourceVoice(&voice.source, &sound->format,
                0, XAUDIO2_DEFAULT_FREQ_RATIO, &voice.callback);

            if (FAILED(result))
            {
                LOGGER_WARNING(L"CreateSourceVoice failed with 0x{:08x}", static_cast<uint32_t>(result));
                voice.source = nullptr;
                return;
            }

            XAUDIO2_BUFFER buffer{};
            buffer.Flags = XAUDIO2_END_OF_STREAM;
            buffer.AudioBytes = static_cast<UINT32>(sound->samples.size() * sizeof(int16_t));
            buffer.pAudioData = reinterpret_cast<const BYTE*>(sound->samples.data());
            buffer.LoopCount = loopCount < 0
                ? XAUDIO2_LOOP_INFINITE
                : static_cast<UINT32>(std::min<int32_t>(loopCount, XAUDIO2_MAX_LOOP_COUNT));

            // XAudio2 does not copy the samples: it reads out of this buffer until the voice is
            // destroyed, so the voice holds the sound alive.
            voice.sound = sound;

            if (FAILED(voice.source->SubmitSourceBuffer(&buffer)) || FAILED(voice.source->Start()))
            {
                Stop(category);
            }
        }

        void Session::Stop(size_t category)
        {
            auto& voice = _voices[category];

            if (voice.source == nullptr)
            {
                return;
            }

            voice.source->Stop();
            voice.source->FlushSourceBuffers();

            // Returns once the audio thread has let go of the voice, so no callback of it can
            // arrive after this line.
            voice.source->DestroyVoice();

            voice.source = nullptr;
            voice.sound = nullptr;
        }

        void Session::StopAll()
        {
            for (size_t index = 0; index < _voices.size(); index++)
            {
                Stop(index);
            }
        }

        void Session::Reset(size_t endpoint)
        {
            auto& engine = _engines[endpoint];

            if (engine.xaudio == nullptr)
            {
                return;
            }

            for (size_t index = 0; index < _voices.size(); index++)
            {
                // A source voice belongs to the engine that created it, so it cannot outlive
                // one being dropped.
                if (EndpointOf(index) == static_cast<Endpoint>(endpoint))
                {
                    Stop(index);
                }
            }

            engine.xaudio->UnregisterForCallbacks(&engine.callback);

            if (engine.mastering != nullptr)
            {
                engine.mastering->DestroyVoice();
                engine.mastering = nullptr;
            }

            engine.xaudio = nullptr;
        }

        bool Session::Playing() const
        {
            for (auto& voice : _voices)
            {
                if (voice.source != nullptr)
                {
                    return true;
                }
            }

            return false;
        }

        SoundEngine& SoundEngine::Current()
        {
            // Leaked on purpose: a static destructor would run at process exit, with a worker
            // possibly still holding XAudio2, and there is nothing to release that the process
            // ending does not.
            static auto instance = new SoundEngine();
            return *instance;
        }

        void SoundEngine::Post(Command&& command)
        {
            std::lock_guard lock(_mutex);
            _queue.push_back(std::move(command));

            if (!_running)
            {
                // Detached: the worker owns everything it creates and gives it up on the way
                // out, so there is nothing to join - and joining here would park the caller,
                // usually the UI thread, behind an engine teardown.
                std::thread(&SoundEngine::Run, this).detach();
                _running = true;
            }

            _signal.notify_one();
        }

        void SoundEngine::Finished(size_t category)
        {
            {
                std::lock_guard lock(_mutex);
                _finished |= 1u << category;
            }

            _signal.notify_one();
        }

        void SoundEngine::Faulted(size_t endpoint)
        {
            {
                std::lock_guard lock(_mutex);
                _faulted |= 1u << endpoint;
            }

            _signal.notify_one();
        }

        void SoundEngine::Run() noexcept
        {
            // A notification sound is not worth taking the process down for, and this thread is
            // the one place where a failed allocation would go unhandled.
            try
            {
                // XAudio2 is COM, and every call into it is made from this thread. The session
                // is scoped so that its voices and engines are released inside the apartment
                // they were created in.
                winrt::init_apartment(winrt::apartment_type::multi_threaded);

                {
                    Session session;
                    Work(session);
                }

                winrt::uninit_apartment();
            }
            catch (...)
            {
            }

            // Already done on the way out of Work, and done again here because the next sound
            // has to be able to start another worker however this one ended.
            std::lock_guard lock(_mutex);
            _running = false;
        }

        void SoundEngine::Work(Session& session)
        {
            for (;;)
            {
                std::vector<Command> commands;
                uint32_t finished;
                uint32_t faulted;

                {
                    std::unique_lock lock(_mutex);

                    if (_queue.empty() && _finished == 0 && _faulted == 0)
                    {
                        _signal.wait_for(lock, IdleTimeout, [this]
                            {
                                return !_queue.empty() || _finished != 0 || _faulted != 0;
                            });

                        if (_queue.empty() && _finished == 0 && _faulted == 0 && !session.Playing())
                        {
                            _running = false;
                            break;
                        }
                    }

                    commands.swap(_queue);
                    finished = _finished;
                    faulted = _faulted;

                    _finished = 0;
                    _faulted = 0;
                }

                for (size_t index = 0; index < EndpointCount; index++)
                {
                    if (faulted & (1u << index))
                    {
                        LOGGER_WARNING(L"audio endpoint {} was lost, dropping the engine", index);
                        session.Reset(index);
                    }
                }

                for (size_t index = 0; index < CategoryCount; index++)
                {
                    if (finished & (1u << index))
                    {
                        session.Stop(index);
                    }
                }

                for (auto& command : commands)
                {
                    switch (command.kind)
                    {
                    case Command::Kind::Play:
                        session.Play(command.path, command.loopCount, command.category);

                        {
                            // A voice that finished before this command was read has left its
                            // flag behind; clearing it here keeps the sound just started from
                            // being taken for the one that ended.
                            std::lock_guard lock(_mutex);
                            _finished &= ~(1u << command.category);
                        }
                        break;
                    case Command::Kind::StopAll:
                        session.StopAll();
                        break;
                    }
                }
            }
        }
    }

    // Both swallow what they cannot do anything about: queueing allocates, and a sound that
    // cannot be queued is not a reason to hand the caller an exception it has no answer for.
    void SoundPlayer::Play(hstring path, int32_t loopCount, SoundCategory category)
    {
        try
        {
            Command command;
            command.kind = Command::Kind::Play;
            command.category = static_cast<size_t>(category);
            command.loopCount = loopCount;
            command.path = path.c_str();

            SoundEngine::Current().Post(std::move(command));
        }
        catch (...)
        {
        }
    }

    void SoundPlayer::StopAll()
    {
        try
        {
            Command command;
            command.kind = Command::Kind::StopAll;

            SoundEngine::Current().Post(std::move(command));
        }
        catch (...)
        {
        }
    }
}
