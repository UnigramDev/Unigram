#pragma once

#include "Highlight/SyntaxToken.g.h"
#include "SyntaxHighlighter.h"

#include <winrt/Windows.Foundation.Collections.h>

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Highlight::implementation
{
    struct SyntaxToken : SyntaxTokenT<SyntaxToken, winrt::Telegram::Native::Highlight::Token>
    {
        SyntaxToken(const Syntax& syntax);
        SyntaxToken(const std::string& type, const std::string& alias, TokenList&& children);

        hstring Type();
        hstring Alias();

        IVector<winrt::Telegram::Native::Highlight::Token> Children();

        hstring ToString();



        static IVector<hstring> Languages();

        static winrt::Telegram::Native::Highlight::SyntaxToken Tokenize(hstring language, hstring coddiri);
        static IAsyncOperation<winrt::Telegram::Native::Highlight::SyntaxToken> TokenizeAsync(hstring language, hstring coddiri);

        static IVector<winrt::Telegram::Native::Highlight::Token> Wrap(TokenList::ConstIterator begin, TokenList::ConstIterator end);
        static void Initialize();

    private:
        hstring m_type;
        hstring m_alias;
        IVector<winrt::Telegram::Native::Highlight::Token> m_children;

        static bool m_initialize;
        static std::mutex m_initializeLock;
        static std::shared_ptr<SyntaxHighlighter> m_highlighter;
    };
}

namespace winrt::Telegram::Native::Highlight::factory_implementation
{
    struct SyntaxToken : SyntaxTokenT<SyntaxToken, implementation::SyntaxToken>
    {
    };
}
