#pragma once

#include "Opus/OpusOutput.g.h"

#include <opus/opus_defines.h>
#include <opus/opus_types.h>

#include <ogg/ogg.h>
#include <opus/opus.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Media.h>

#define TG_OPUS_SAMPLE_SIZE 16
#define TG_OPUS_CHANNELS 1
#define TG_OPUS_BITRATE 48000
#define TG_OPUS_FRAME_SIZE 960

#define WAV_HEADER_FIXED 44

#define ReturnIfFailed(result, method) \
	if((result = method) != OPUS_OK) \
	{ \
		return result; \
	}

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Media;

namespace winrt::Unigram::Native::Opus::implementation
{
    typedef struct {
        int version;
        int channels; /* Number of channels: 1..255 */
        int preskip;
        ogg_uint32_t input_sample_rate;
        int gain; /* in dB S7.8 should be zero whenever possible */
        int channel_mapping;
        /* The rest is only used if channel_mapping != 0 */
        int nb_streams;
        int nb_coupled;
        unsigned char stream_map[255];
    } OpusHeader;

    typedef struct {
        unsigned char* data;
        int maxlen;
        int pos;
    } Packet;

    typedef struct {
        const unsigned char* data;
        int maxlen;
        int pos;
    } ROPacket;

    struct OpusOutput : OpusOutputT<OpusOutput>
    {
        OpusOutput(hstring fileName) {
            Initialize(fileName.data());
        }

        ~OpusOutput() {
            Dispose(true);
        }

        void Close() {
            Dispose(true);
        }

        void Transcode(hstring fileName) {
            FILE* file = _wfopen(fileName.data(), L"rb");
            if (file == NULL) {
                return;
            }

            fseek(file, 0, SEEK_END);
            size_t length = ftell(file);
            size_t dataLength = length - WAV_HEADER_FIXED;
            fseek(file, WAV_HEADER_FIXED, SEEK_SET);

            const int frameLength = TG_OPUS_FRAME_SIZE * sizeof(int16_t);
            uint32_t partsCount = dataLength / frameLength;
            uint8_t* buffer = (uint8_t*)malloc(frameLength);

            for (int i = 0; i < partsCount; i++)
            {
                unsigned int count = frameLength * (i + 1) > dataLength ? dataLength - frameLength * i : frameLength;
                fread(buffer, 1, count, file);
                WriteFrame(buffer, count);
            }

            fclose(file);
        }

        void WriteFrame(AudioFrame frame) {
            // TODO: this method doesn't really work,
            // and I'm too lazy to really figure out why
            AudioBuffer audioBuffer = frame.LockBuffer(AudioBufferAccessMode::Read);
            IMemoryBufferReference bufferReference = audioBuffer.CreateReference();

            const int frameLength = TG_OPUS_FRAME_SIZE * sizeof(int16_t);
            size_t dataLength = audioBuffer.Length();
            size_t partsCount = dataLength / frameLength;

            auto buffer = bufferReference.data();

            for (int i = 0; i < partsCount; i++)
            {
                unsigned int count = frameLength * (i + 1) > dataLength ? dataLength - frameLength * i : frameLength;
                WriteFrame(buffer + frameLength * i, count);
            }

            bufferReference.Close();
            audioBuffer.Close();
        }

    private:
        bool Initialize(const wchar_t* path);
        bool WriteFrame(uint8_t* framePcmBytes, unsigned int frameByteCount);
        void Dispose(bool disposing);

        const int with_cvbr = 1;
        const int max_ogg_delay = 0;

        bool disposed = false;
        ogg_int32_t _packetId;
        OpusEncoder* _encoder = 0;
        uint8_t* _packet = 0;
        ogg_stream_state os;
        FILE* _fileOs = 0;
        OpusHeader header;
        opus_int32 min_bytes;
        int max_frame_bytes;
        ogg_packet op;
        ogg_page og;
        opus_int64 bytes_written;
        opus_int64 pages_out;
        opus_int64 total_samples;
        ogg_int64_t enc_granulepos;
        ogg_int64_t last_granulepos;
        int size_segments;
        int last_segments;
    };
}

namespace winrt::Unigram::Native::Opus::factory_implementation
{
    struct OpusOutput : OpusOutputT<OpusOutput, implementation::OpusOutput>
    {
    };
}
