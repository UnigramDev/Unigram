#include "pch.h"
#include "MediaPlaylist.h"

#include <algorithm>
#include <charconv>

namespace winrt::Telegram::Native::Media::implementation
{
    namespace
    {
        bool StartsWith(std::string_view text, std::string_view prefix) noexcept
        {
            return text.size() >= prefix.size() && text.compare(0, prefix.size(), prefix) == 0;
        }

        std::string_view Trim(std::string_view text) noexcept
        {
            while (!text.empty() && (text.front() == ' ' || text.front() == '\t'))
            {
                text.remove_prefix(1);
            }

            while (!text.empty() && (text.back() == ' ' || text.back() == '\t' || text.back() == '\r'))
            {
                text.remove_suffix(1);
            }

            return text;
        }

        // "length[@offset]". The offset is optional in a segment's EXT-X-BYTERANGE, and then the
        // range follows the previous one.
        bool ParseRange(std::string_view text, int64_t& length, int64_t& offset, bool& hasOffset) noexcept
        {
            const auto at = text.find('@');
            const auto lengthText = text.substr(0, at);

            if (std::from_chars(lengthText.data(), lengthText.data() + lengthText.size(), length).ec != std::errc{} || length <= 0)
            {
                return false;
            }

            hasOffset = at != std::string_view::npos;

            if (hasOffset)
            {
                const auto offsetText = text.substr(at + 1);
                return std::from_chars(offsetText.data(), offsetText.data() + offsetText.size(), offset).ec == std::errc{} && offset >= 0;
            }

            return true;
        }

        bool ParseDuration(std::string_view text, double& duration) noexcept
        {
            // Up to the comma that separates the title, which Telegram leaves empty.
            const auto value = text.substr(0, text.find(','));
            return std::from_chars(value.data(), value.data() + value.size(), duration).ec == std::errc{} && duration > 0;
        }
    }

    double MediaPlaylist::Duration() const noexcept
    {
        return segments.empty() ? 0 : segments.back().start + segments.back().duration;
    }

    int32_t MediaPlaylist::SegmentAt(double time) const noexcept
    {
        if (segments.empty())
        {
            return -1;
        }

        const auto next = std::upper_bound(segments.begin(), segments.end(), time,
            [](double value, Segment const& segment) { return value < segment.start; });

        return next == segments.begin() ? 0 : static_cast<int32_t>(next - segments.begin()) - 1;
    }

    int32_t MediaPlaylist::SegmentFrom(double time) const noexcept
    {
        const auto first = std::lower_bound(segments.begin(), segments.end(), time - 0.001,
            [](Segment const& segment, double value) { return segment.start < value; });

        return first == segments.end() ? -1 : static_cast<int32_t>(first - segments.begin());
    }

    bool MediaPlaylist::Parse(std::string_view text, MediaPlaylist& playlist)
    {
        playlist = {};

        double duration = 0;
        bool hasDuration = false;

        int64_t length = 0;
        int64_t offset = 0;
        int64_t next = 0;
        bool hasRange = false;
        bool hasMap = false;
        double start = 0;

        while (!text.empty())
        {
            const auto end = text.find('\n');
            const auto line = Trim(text.substr(0, end));
            text.remove_prefix(end == std::string_view::npos ? text.size() : end + 1);

            if (StartsWith(line, "#EXT-X-MAP:"))
            {
                constexpr std::string_view attribute = "BYTERANGE=\"";
                const auto position = line.find(attribute);

                if (position == std::string_view::npos)
                {
                    return false;
                }

                auto value = line.substr(position + attribute.size());
                value = value.substr(0, value.find('"'));

                bool hasOffset = false;
                playlist.initOffset = 0;

                if (!ParseRange(value, playlist.initLength, playlist.initOffset, hasOffset))
                {
                    return false;
                }

                hasMap = true;
            }
            else if (StartsWith(line, "#EXTINF:"))
            {
                hasDuration = ParseDuration(line.substr(8), duration);
            }
            else if (StartsWith(line, "#EXT-X-BYTERANGE:"))
            {
                bool hasOffset = false;

                if (!ParseRange(line.substr(17), length, offset, hasOffset))
                {
                    return false;
                }

                if (!hasOffset)
                {
                    offset = next;
                }

                hasRange = true;
            }
            else if (!line.empty() && line.front() != '#')
            {
                // A segment's URI. Each has to come with its duration and its range, since all of
                // them are one file and only the range tells them apart.
                if (!hasDuration || !hasRange)
                {
                    return false;
                }

                playlist.segments.push_back({ start, duration, offset, length });

                start += duration;
                next = offset + length;
                hasDuration = false;
                hasRange = false;
            }
        }

        return hasMap && !playlist.segments.empty();
    }

    MediaSegmentMap::MediaSegmentMap(MediaPlaylist const& playlist, int32_t first)
    {
        m_ranges.push_back({ 0, playlist.initOffset, playlist.initLength, -1 });
        m_size = playlist.initLength;

        for (int32_t index = std::max(0, first); index < static_cast<int32_t>(playlist.segments.size()); index++)
        {
            auto const& segment = playlist.segments[index];

            m_ranges.push_back({ m_size, segment.offset, segment.length, index });
            m_size += segment.length;
        }
    }

    MediaSegmentMap::Range const* MediaSegmentMap::Find(int64_t position) const noexcept
    {
        if (position < 0 || position >= m_size)
        {
            return nullptr;
        }

        const auto next = std::upper_bound(m_ranges.begin(), m_ranges.end(), position,
            [](int64_t value, Range const& range) { return value < range.start; });

        return &*(next - 1);
    }

    bool MediaSegmentMap::Locate(int64_t position, int64_t& offset, int64_t& available) const noexcept
    {
        auto const* range = Find(position);

        if (range == nullptr)
        {
            return false;
        }

        offset = range->offset + (position - range->start);
        available = range->length - (position - range->start);

        return true;
    }

    int32_t MediaSegmentMap::SegmentOf(int64_t position) const noexcept
    {
        auto const* range = Find(position);
        return range == nullptr ? -1 : range->segment;
    }
}
