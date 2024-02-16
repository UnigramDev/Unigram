#pragma once

#include "Highlight/TextToken.g.h"
#include "SyntaxHighlighter.h"

namespace winrt::Telegram::Native::Highlight::implementation
{
    struct TextToken : TextTokenT<TextToken, winrt::Telegram::Native::Highlight::Token>
    {
        TextToken(const Text& text);

        hstring Value();

        hstring ToString();

    private:
        hstring m_value;
    };
}
