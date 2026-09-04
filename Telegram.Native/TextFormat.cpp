#include "pch.h"
#include "TextFormat.h"
#if __has_include("TextFormat.g.cpp")
#include "TextFormat.g.cpp"
#endif

#include "Helpers\COMHelper.h"

namespace winrt::Telegram::Native::implementation
{
    TextFormat::TextFormat(winrt::com_ptr<IDWriteTextLayout> textLayout, uint32_t textLength, double fontSize, double maxWidth)
        : m_textLayout(textLayout)
        , m_textLength(textLength)
        , m_fontSize(fontSize)
        , m_maxWidth(maxWidth)
    {

    }

    // Every caller sets the whole configuration, as one layout serves callers that disagree on
    // all of it: a wrapping mode or reading direction left over from the previous one would
    // answer for a layout that nothing on screen matches.
    HRESULT TextFormat::Configure(double fontSize, double width, bool rtl, bool wrap)
    {
        HRESULT result;

        if (m_fontSize != fontSize)
        {
            ReturnIfFailed(result, m_textLayout->SetFontSize(fontSize, { 0, m_textLength }));
            m_fontSize = fontSize;
        }

        if (m_maxWidth != width)
        {
            ReturnIfFailed(result, m_textLayout->SetMaxWidth(width));
            m_maxWidth = width;
        }

        if (m_rtl != rtl)
        {
            ReturnIfFailed(result, m_textLayout->SetReadingDirection(rtl ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT));
            m_rtl = rtl;
        }

        if (m_wrap != wrap)
        {
            DWRITE_TRIMMING trimming = wrap
                ? DWRITE_TRIMMING{ DWRITE_TRIMMING_GRANULARITY_NONE, 0, 0 }
                : DWRITE_TRIMMING{ DWRITE_TRIMMING_GRANULARITY_CHARACTER, 0x2E, 3 };

            ReturnIfFailed(result, m_textLayout->SetTrimming(&trimming, nullptr));
            ReturnIfFailed(result, m_textLayout->SetWordWrapping(wrap ? DWRITE_WORD_WRAPPING_EMERGENCY_BREAK : DWRITE_WORD_WRAPPING_NO_WRAP));
            m_wrap = wrap;
        }

        return S_OK;
    }

    float2 TextFormat::ContentEnd(double fontSize, double width)
    {
        float2 offset;
        ContentEndImpl(fontSize, width, offset);
        return offset;
    }

    HRESULT TextFormat::ContentEndImpl(double fontSize, double width, float2& offset)
    {
        HRESULT result;
        offset = float2(0, 0);

        if (m_textLayout == nullptr || m_textLength == 0)
        {
            return S_OK;
        }

        ReturnIfFailed(result, Configure(fontSize, width, false, true));

        DWRITE_TEXT_METRICS metrics;
        ReturnIfFailed(result, m_textLayout->GetMetrics(&metrics));

        // The bottom right corner of the laid out text, not the last character: the two differ
        // wherever the last line ends in whitespace.
        BOOL isTrailingHit;
        BOOL isInside;
        DWRITE_HIT_TEST_METRICS hitTestMetrics;
        ReturnIfFailed(result, m_textLayout->HitTestPoint(metrics.width, metrics.height, &isTrailingHit, &isInside, &hitTestMetrics));

        offset = float2(hitTestMetrics.left + hitTestMetrics.width, hitTestMetrics.top + hitTestMetrics.height);
        return S_OK;
    }

    // X is the narrowest the text can be laid out without breaking a word, Y the widest line
    // it takes when nothing wraps it - the two numbers a table needs from a cell. Both come
    // from the one layout, so a caller measuring a table pays for one per cell.
    float2 TextFormat::ContentWidths(double fontSize, double width, bool rtl)
    {
        HRESULT result;

        if (m_textLayout == nullptr || m_textLength == 0)
        {
            return float2(0, 0);
        }

        ReturnDefaultIfFailed(result, Configure(fontSize, width, rtl, true));

        FLOAT minWidth;
        ReturnDefaultIfFailed(result, m_textLayout->DetermineMinWidth(&minWidth));

        DWRITE_TEXT_METRICS metrics;
        ReturnDefaultIfFailed(result, m_textLayout->GetMetrics(&metrics));

        return float2(minWidth, metrics.width);
    }

    winrt::Telegram::Native::MaxLinesMetrics TextFormat::MaxLines(int32_t offset, int32_t length, double fontSize, double width, bool rtl, int32_t maxLines)
    {
        HRESULT result;

        if (m_textLayout == nullptr)
        {
            return {};
        }

        ReturnDefaultIfFailed(result, Configure(fontSize, width, rtl, true));

        DWRITE_TEXT_METRICS metrics;
        ReturnDefaultIfFailed(result, m_textLayout->GetMetrics(&metrics));

        if (maxLines == 0)
        {
            return { metrics.left, metrics.top, metrics.width, metrics.height, metrics.height, length };
        }

        UINT32 actualLineCount;
        DWRITE_LINE_METRICS* ranges = new DWRITE_LINE_METRICS[metrics.lineCount];
        result = m_textLayout->GetLineMetrics(ranges, metrics.lineCount, &actualLineCount);

        if (result == E_NOT_SUFFICIENT_BUFFER)
        {
            delete[] ranges;

            ranges = new DWRITE_LINE_METRICS[actualLineCount];
            result = m_textLayout->GetLineMetrics(ranges, actualLineCount, &actualLineCount);
        }

        if (FAILED(result))
        {
            delete[] ranges;
            return {};
        }

        float truncateHeight = 0;
        int32_t truncatePosition = 0;

        // Calculate position where to truncate
        for (UINT32 i = 0; i < maxLines && i < actualLineCount; ++i)
        {
            truncateHeight += ranges[i].height;
            truncatePosition += ranges[i].length;
        }

        // Remove trailing whitespace from last included line
        if (maxLines <= actualLineCount)
        {
            //truncateHeight += ranges[maxLines - 1].height;
            truncatePosition -= ranges[maxLines - 1].trailingWhitespaceLength;
            truncatePosition -= ranges[maxLines - 1].newlineLength;
        }

        delete[] ranges;
        return { metrics.left, metrics.top, metrics.width, metrics.height, truncateHeight, truncatePosition };
    }

    IVector<Windows::Foundation::Rect> TextFormat::LineMetrics(double fontSize, double width, bool rtl)
    {
        return RangeMetrics(0, m_textLength, fontSize, width, rtl, true);
    }

    IVector<Windows::Foundation::Rect> TextFormat::RangeMetrics(int32_t offset, int32_t length, double fontSize, double width, bool rtl, bool wrap)
    {
        HRESULT result;

        if (m_textLayout == nullptr)
        {
            return winrt::single_threaded_vector<Windows::Foundation::Rect>();
        }

        ReturnDefaultIfFailed(result, Configure(fontSize, width, rtl, wrap));

        DWRITE_TEXT_METRICS metrics;
        ReturnDefaultIfFailed(result, m_textLayout->GetMetrics(&metrics));

        UINT32 maxHitTestMetricsCount = metrics.lineCount * metrics.maxBidiReorderingDepth;
        UINT32 actualTestsCount;
        DWRITE_HIT_TEST_METRICS* ranges = new DWRITE_HIT_TEST_METRICS[maxHitTestMetricsCount];
        result = m_textLayout->HitTestTextRange(offset, length, 0, 0, ranges, maxHitTestMetricsCount, &actualTestsCount);

        if (result == E_NOT_SUFFICIENT_BUFFER)
        {
            delete[] ranges;

            ranges = new DWRITE_HIT_TEST_METRICS[actualTestsCount];
            result = m_textLayout->HitTestTextRange(offset, length, 0, 0, ranges, actualTestsCount, &actualTestsCount);
        }

        if (FAILED(result))
        {
            delete[] ranges;
            return winrt::single_threaded_vector<Windows::Foundation::Rect>();
        }

        std::vector<Windows::Foundation::Rect> vector;

        for (UINT32 i = 0; i < actualTestsCount; i++)
        {
            if (ranges[i].isTrimmed)
            {
                break;
            }

            float left = ranges[i].left;
            float top = ranges[i].top;
            float right = ranges[i].left + ranges[i].width;
            float bottom = ranges[i].top + ranges[i].height;

            vector.push_back({ left, top, right - left, bottom - top });
        }

        delete[] ranges;
        return winrt::single_threaded_vector<Windows::Foundation::Rect>(std::move(vector));
    }
}
