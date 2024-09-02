#pragma once

#include "TextFormat.g.h"

#include <ppl.h>
#include <wincodec.h>
#include <Dwrite_1.h>
#include <D2d1_3.h>
#include <map>

#include <winrt/Windows.Foundation.h>

using namespace concurrency;
using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Foundation::Numerics;

namespace winrt::Telegram::Native::implementation
{
    struct TextFormat : TextFormatT<TextFormat>
    {
        TextFormat() = default;
        TextFormat(winrt::com_ptr<IDWriteTextLayout> textLayout, uint32_t textLength, double fontSize, double maxWidth);

        float2 ContentEnd(double fontSize, double width);

    private:
        winrt::com_ptr<IDWriteTextLayout> m_textLayout;
        uint32_t m_textLength;
        double m_fontSize;
        double m_maxWidth;

        HRESULT ContentEndImpl(double fontSize, double width, float2& offset);
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct TextFormat : TextFormatT<TextFormat, implementation::TextFormat>
    {
    };
}
