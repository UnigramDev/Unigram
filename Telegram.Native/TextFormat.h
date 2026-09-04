#pragma once

#include "TextFormat.g.h"

#include <ppl.h>
#include <wincodec.h>
#include <Dwrite_1.h>
#include <D2d1_3.h>
#include <map>
#include <vector>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>

using namespace concurrency;
using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Foundation::Numerics;

namespace winrt::Telegram::Native::implementation
{
    // A DirectWrite layout of one paragraph, kept alive by its caller across measures. Font
    // size, width, reading direction and wrapping are properties of the layout rather than of
    // a call, so each method sets the ones it needs and DirectWrite reflows lazily.
    struct TextFormat : TextFormatT<TextFormat>
    {
        TextFormat() = default;
        TextFormat(winrt::com_ptr<IDWriteTextLayout> textLayout, uint32_t textLength, double fontSize, double maxWidth);

        float2 ContentEnd(double fontSize, double width);
        float2 ContentWidths(double fontSize, double width, bool rtl);
        winrt::Telegram::Native::MaxLinesMetrics MaxLines(int32_t offset, int32_t length, double fontSize, double width, bool rtl, int32_t maxLines);
        IVector<Windows::Foundation::Rect> RangeMetrics(int32_t offset, int32_t length, double fontSize, double width, bool rtl, bool wrap);
        IVector<Windows::Foundation::Rect> LineMetrics(double fontSize, double width, bool rtl);

    private:
        winrt::com_ptr<IDWriteTextLayout> m_textLayout;
        uint32_t m_textLength{ 0 };
        double m_fontSize{ 0 };
        double m_maxWidth{ 0 };

        // The state CreateTextFormatImpl leaves the layout in.
        bool m_rtl{ false };
        bool m_wrap{ true };

        HRESULT Configure(double fontSize, double width, bool rtl, bool wrap);
        HRESULT ContentEndImpl(double fontSize, double width, float2& offset);
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct TextFormat : TextFormatT<TextFormat, implementation::TextFormat>
    {
    };
}
