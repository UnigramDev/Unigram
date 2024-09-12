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

    float2 TextFormat::ContentEnd(double fontSize, double width)
    {
        float2 offset;
        ContentEndImpl(fontSize, width, offset);
        return offset;
    }

    HRESULT TextFormat::ContentEndImpl(double fontSize, double width, float2& offset)
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

        FLOAT x;
        FLOAT y;
        DWRITE_HIT_TEST_METRICS metrics;
        ReturnIfFailed(result, m_textLayout->HitTestTextPosition(m_textLength - 1, false, &x, &y, &metrics));

        offset = float2(metrics.left + metrics.width, metrics.top + metrics.height);
        return result;
    }
}
