#include "pch.h"
#include "TextToken.h"
#if __has_include("TextToken.g.cpp")
#include "TextToken.g.cpp"
#endif

#include "TokenList.h"
namespace winrt::Telegram::Native::Highlight::implementation
{
    TextToken::TextToken(const Text& text)
        : m_value(winrt::to_hstring(text.value()))
    {

    }

    hstring TextToken::Value()
    {
        return m_value;
    }

    hstring TextToken::ToString()
    {
        return L"TextToken \"" + m_value + L"\"";
    }
}
