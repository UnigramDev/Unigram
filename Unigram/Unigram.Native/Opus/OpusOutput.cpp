#include "pch.h"
#include "OpusOutput.h"
#if __has_include("Opus/OpusOutput.g.cpp")
#include "Opus/OpusOutput.g.cpp"
#endif

namespace winrt::Unigram::Native::Opus::implementation
{
    static int write_uint32(Packet* p, ogg_uint32_t val) {
        if (p->pos > p->maxlen - 4) {
            return 0;
        }
        p->data[p->pos] = (val) & 0xFF;
        p->data[p->pos + 1] = (val >> 8) & 0xFF;
        p->data[p->pos + 2] = (val >> 16) & 0xFF;
        p->data[p->pos + 3] = (val >> 24) & 0xFF;
        p->pos += 4;
        return 1;
    }

    static int write_uint16(Packet* p, ogg_uint16_t val) {
        if (p->pos > p->maxlen - 2) {
            return 0;
        }
        p->data[p->pos] = (val) & 0xFF;
        p->data[p->pos + 1] = (val >> 8) & 0xFF;
        p->pos += 2;
        return 1;
    }

    static int write_chars(Packet* p, const unsigned char* str, int nb_chars)
    {
        int i;
        if (p->pos > p->maxlen - nb_chars)
            return 0;
        for (i = 0; i < nb_chars; i++)
            p->data[p->pos++] = str[i];
        return 1;
    }

    int opus_header_to_packet(const OpusHeader* h, unsigned char* packet, int len) {
        int i;
        Packet p;
        unsigned char ch;

        p.data = packet;
        p.maxlen = len;
        p.pos = 0;
        if (len < 19) {
            return 0;
        }
        if (!write_chars(&p, (const unsigned char*)"OpusHead", 8)) {
            return 0;
        }

        ch = 1;
        if (!write_chars(&p, &ch, 1)) {
            return 0;
        }

        ch = h->channels;
        if (!write_chars(&p, &ch, 1)) {
            return 0;
        }

        if (!write_uint16(&p, h->preskip)) {
            return 0;
        }

        if (!write_uint32(&p, h->input_sample_rate)) {
            return 0;
        }

        if (!write_uint16(&p, h->gain)) {
            return 0;
        }

        ch = h->channel_mapping;
        if (!write_chars(&p, &ch, 1)) {
            return 0;
        }

        if (h->channel_mapping != 0) {
            ch = h->nb_streams;
            if (!write_chars(&p, &ch, 1)) {
                return 0;
            }

            ch = h->nb_coupled;
            if (!write_chars(&p, &ch, 1)) {
                return 0;
            }

            /* Multi-stream support */
            for (i = 0; i < h->channels; i++) {
                if (!write_chars(&p, &h->stream_map[i], 1)) {
                    return 0;
                }
            }
        }

        return p.pos;
    }

#define writeint(buf, base, val) do { buf[base + 3] = ((val) >> 24) & 0xff; \
buf[base + 2]=((val) >> 16) & 0xff; \
buf[base + 1]=((val) >> 8) & 0xff; \
buf[base] = (val) & 0xff; \
} while(0)

    static void comment_init(char** comments, int* length, const char* vendor_string) {
        // The 'vendor' field should be the actual encoding library used
        int vendor_length = strlen(vendor_string);
        int user_comment_list_length = 0;
        int len = 8 + 4 + vendor_length + 4;
        char* p = (char*)malloc(len);
        memcpy(p, "OpusTags", 8);
        writeint(p, 8, vendor_length);
        memcpy(p + 12, vendor_string, vendor_length);
        writeint(p, 12 + vendor_length, user_comment_list_length);
        *length = len;
        *comments = p;
    }

    static void comment_pad(char** comments, int* length, int amount) {
        char* p;
        int newlen;
        int i;
        if (amount > 0) {
            p = *comments;
            // Make sure there is at least amount worth of padding free, and round up to the maximum that fits in the current ogg segments
            newlen = (*length + amount + 255) / 255 * 255 - 1;
            p = (char*)realloc(p, newlen);
            for (i = *length; i < newlen; i++) {
                p[i] = 0;
            }
            *comments = p;
            *length = newlen;
        }
    }

    static int writeOggPage(ogg_page* page, FILE* os) {
        int written = fwrite(page->header, sizeof(unsigned char), page->header_len, os);
        written += fwrite(page->body, sizeof(unsigned char), page->body_len, os);
        return written;
    }

    bool OpusOutput::Initialize(const wchar_t* path)
    {
        Dispose(false);

        if (!path) {
            return false;
        }

        _fileOs = _wfopen(path, L"wb");
        if (!_fileOs) {
            return false;
        }

        header.channels = TG_OPUS_CHANNELS;
        header.channel_mapping = 0;
        header.input_sample_rate = TG_OPUS_BITRATE;
        header.gain = 0;
        header.nb_streams = 1;

        int result = OPUS_OK;
        _encoder = opus_encoder_create(TG_OPUS_BITRATE, TG_OPUS_CHANNELS, OPUS_APPLICATION_AUDIO, &result);
        if (result != OPUS_OK) {
            return false;
        }

        min_bytes = max_frame_bytes = (1275 * 3 + 7) * header.nb_streams;
        _packet = (uint8_t*)malloc(max_frame_bytes);

        opus_int32 lookahead;
        ReturnIfFailed(result, opus_encoder_ctl(_encoder, OPUS_SET_BITRATE(TG_OPUS_BITRATE)));
        ReturnIfFailed(result, opus_encoder_ctl(_encoder, OPUS_SET_LSB_DEPTH(TG_OPUS_SAMPLE_SIZE)));
        ReturnIfFailed(result, opus_encoder_ctl(_encoder, OPUS_GET_LOOKAHEAD(&lookahead)));

        header.preskip = (int)(lookahead * (48000.0 / TG_OPUS_BITRATE));

        if (ogg_stream_init(&os, rand()) == -1) {
            return false;
        }

        unsigned char header_data[100];
        int packet_size = opus_header_to_packet(&header, header_data, 100);
        op.packet = header_data;
        op.bytes = packet_size;
        op.b_o_s = 1;
        op.e_o_s = 0;
        op.granulepos = 0;
        op.packetno = 0;
        ogg_stream_packetin(&os, &op);

        while ((result = ogg_stream_flush(&os, &og))) {
            if (result == 0) {
                break;
            }

            int pageBytesWritten = writeOggPage(&og, _fileOs);
            if (pageBytesWritten != og.header_len + og.body_len) {
                return false;
            }
            bytes_written += pageBytesWritten;
            pages_out++;
        }

        char* comments;
        int comments_length;
        comment_init(&comments, &comments_length, opus_get_version_string());
        comment_pad(&comments, &comments_length, 512);
        op.packet = (unsigned char*)comments;
        op.bytes = comments_length;
        op.b_o_s = 0;
        op.e_o_s = 0;
        op.granulepos = 0;
        op.packetno = 1;
        ogg_stream_packetin(&os, &op);

        while ((result = ogg_stream_flush(&os, &og))) {
            if (result == 0) {
                break;
            }

            int writtenPageBytes = writeOggPage(&og, _fileOs);
            if (writtenPageBytes != og.header_len + og.body_len) {
                return false;
            }

            bytes_written += writtenPageBytes;
            pages_out++;
        }

        free(comments);

        return true;
    }

    bool OpusOutput::WriteFrame(uint8_t* framePcmBytes, unsigned int frameByteCount)
    {
        int cur_frame_size = TG_OPUS_FRAME_SIZE;
        _packetId++;

        opus_int32 nb_samples = frameByteCount / 2;
        total_samples += nb_samples;
        if (nb_samples < TG_OPUS_FRAME_SIZE) {
            op.e_o_s = 1;
        }
        else {
            op.e_o_s = 0;
        }

        int nbBytes = 0;

        if (nb_samples != 0) {
            uint8_t* paddedFrameBytes = framePcmBytes;
            int freePaddedFrameBytes = 0;

            if (nb_samples < cur_frame_size) {
                paddedFrameBytes = (uint8_t*)malloc(cur_frame_size * 2);
                freePaddedFrameBytes = 1;
                memcpy(paddedFrameBytes, framePcmBytes, frameByteCount);
                memset(paddedFrameBytes + nb_samples * 2, 0, cur_frame_size * 2 - nb_samples * 2);
            }

            nbBytes = opus_encode(_encoder, (opus_int16*)paddedFrameBytes, cur_frame_size, _packet, max_frame_bytes / 10);
            if (freePaddedFrameBytes) {
                free(paddedFrameBytes);
                paddedFrameBytes = NULL;
            }

            if (nbBytes < 0) {
                return false;
            }

            enc_granulepos += cur_frame_size * 48000 / TG_OPUS_BITRATE;
            size_segments = (nbBytes + 255) / 255;
            min_bytes = std::min(nbBytes, min_bytes);
        }

        while ((((size_segments <= 255) && (last_segments + size_segments > 255)) || (enc_granulepos - last_granulepos > max_ogg_delay)) && ogg_stream_flush_fill(&os, &og, 255 * 255)) {
            if (ogg_page_packets(&og) != 0) {
                last_granulepos = ogg_page_granulepos(&og);
            }

            last_segments -= og.header[26];
            int writtenPageBytes = writeOggPage(&og, _fileOs);
            if (writtenPageBytes != og.header_len + og.body_len) {
                return false;
            }
            bytes_written += writtenPageBytes;
            pages_out++;
        }

        op.packet = (unsigned char*)_packet;
        op.bytes = nbBytes;
        op.b_o_s = 0;
        op.granulepos = enc_granulepos;
        if (op.e_o_s) {
            op.granulepos = ((total_samples * 48000 + TG_OPUS_BITRATE - 1) / TG_OPUS_BITRATE) + header.preskip;
        }
        op.packetno = 1 + _packetId;
        ogg_stream_packetin(&os, &op);
        last_segments += size_segments;

        while ((op.e_o_s || (enc_granulepos + (TG_OPUS_FRAME_SIZE * 48000 / TG_OPUS_BITRATE) - last_granulepos > max_ogg_delay) || (last_segments >= 255)) ? ogg_stream_flush_fill(&os, &og, 255 * 255) : ogg_stream_pageout_fill(&os, &og, 255 * 255)) {
            if (ogg_page_packets(&og) != 0) {
                last_granulepos = ogg_page_granulepos(&og);
            }
            last_segments -= og.header[26];
            int writtenPageBytes = writeOggPage(&og, _fileOs);
            if (writtenPageBytes != og.header_len + og.body_len) {
                return 0;
            }
            bytes_written += writtenPageBytes;
            pages_out++;
        }

        return true;
    }

    void OpusOutput::Dispose(bool disposing) {
        if (disposing && !disposed) {
            ogg_stream_flush(&os, &og);

            if (_encoder) {
                opus_encoder_destroy(_encoder);
                _encoder = 0;
            }

            ogg_stream_clear(&os);

            if (_packet) {
                free(_packet);
                _packet = 0;
            }

            if (_fileOs) {
                fclose(_fileOs);
                _fileOs = 0;
            }

            disposed = true;
        }

        _packetId = -1;
        bytes_written = 0;
        pages_out = 0;
        total_samples = 0;
        enc_granulepos = 0;
        size_segments = 0;
        last_segments = 0;
        last_granulepos = 0;
        memset(&os, 0, sizeof(ogg_stream_state));
        memset(&header, 0, sizeof(OpusHeader));
        memset(&op, 0, sizeof(ogg_packet));
        memset(&og, 0, sizeof(ogg_page));
    }
}
