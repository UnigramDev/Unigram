#pragma once

#include <cstdint>
#include <string_view>
#include <vector>

namespace winrt::Telegram::Native::Media::implementation
{
    // One quality of a video, as Telegram's HLS playlists describe it: a fragmented MP4 whose init
    // section and segments are byte ranges of a single file. The qualities of one video share their
    // segment boundaries, which is what makes switching between them at a boundary seamless.
    struct MediaPlaylist
    {
        struct Segment
        {
            double start{ 0 };
            double duration{ 0 };
            int64_t offset{ 0 };
            int64_t length{ 0 };
        };

        int64_t initOffset{ 0 };
        int64_t initLength{ 0 };
        std::vector<Segment> segments;

        double Duration() const noexcept;

        // The segment playing at the time, clamped to the first and the last.
        int32_t SegmentAt(double time) const noexcept;

        // The first segment starting at or after the time, or -1 when there is none.
        int32_t SegmentFrom(double time) const noexcept;

        static bool Parse(std::string_view text, MediaPlaylist& playlist);
    };

    // The bytes a demuxer reads to start a quality at one segment: the init section, then every
    // segment from there to the end, laid end to end as if they were one file.
    class MediaSegmentMap
    {
    public:
        MediaSegmentMap(MediaPlaylist const& playlist, int32_t first);

        int64_t Size() const noexcept
        {
            return m_size;
        }

        // Where a position of the virtual file is in the real one, and how many bytes from there
        // belong to the same range.
        bool Locate(int64_t position, int64_t& offset, int64_t& available) const noexcept;

        // The segment a position of the virtual file falls in, -1 inside the init section.
        int32_t SegmentOf(int64_t position) const noexcept;

    private:
        struct Range
        {
            int64_t start;
            int64_t offset;
            int64_t length;
            int32_t segment;
        };

        Range const* Find(int64_t position) const noexcept;

        std::vector<Range> m_ranges;
        int64_t m_size{ 0 };
    };
}
