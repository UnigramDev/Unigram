#include "pch.h"
#include "SyntaxToken.h"
#include "TextToken.h"
#if __has_include("Highlight/SyntaxToken.g.cpp")
#include "Highlight/SyntaxToken.g.cpp"
#endif

#include <sstream>
#include <fstream>

#include "TokenList.h"

namespace winrt::Telegram::Native::Highlight::implementation
{
    SyntaxToken::SyntaxToken(const Syntax& syntax)
        : m_type(winrt::to_hstring(syntax.type()))
        , m_alias(winrt::to_hstring(syntax.alias()))
        , m_children(Wrap(syntax.begin(), syntax.end()))
    {
    }

    SyntaxToken::SyntaxToken(const std::string& type, const std::string& alias, TokenList&& children)
        : m_type(winrt::to_hstring(type))
        , m_alias(winrt::to_hstring(alias))
        , m_children(Wrap(children.begin(), children.end()))
    {

    }

    hstring SyntaxToken::Type()
    {
        return m_type;
    }

    hstring SyntaxToken::Alias()
    {
        return m_alias;
    }

    IVector<winrt::Telegram::Native::Highlight::Token> SyntaxToken::Children()
    {
        return m_children;
    }

    hstring SyntaxToken::ToString()
    {
        return L"SyntaxToken { " + m_type + L" }";
    }



    IAsyncOperation<winrt::Telegram::Native::Highlight::SyntaxToken> SyntaxToken::TokenizeAsync(hstring language, hstring coddiri)
    {
        winrt::apartment_context ui_thread;
        co_await winrt::resume_background();

        auto tokens = Tokenize(language, coddiri);

        co_await ui_thread;
        co_return tokens;
    }

    bool SyntaxToken::m_initialize = true;
    winrt::slim_mutex SyntaxToken::m_initializeLock;
    std::shared_ptr<SyntaxHighlighter> SyntaxToken::m_highlighter;

    void SyntaxToken::Initialize()
    {
        slim_lock_guard const guard(m_initializeLock);

        if (m_initialize)
        {
            m_initialize = false;
            m_highlighter = std::make_shared<SyntaxHighlighter>("Assets\\grammars.dat");
        }
    }

    IVector<hstring> SyntaxToken::Languages()
    {
        Initialize();

        auto languages = winrt::single_threaded_vector<hstring>();

        for (const auto& lang : m_highlighter->languages())
        {
            languages.Append(winrt::to_hstring(lang));
        }

        return languages;
    }

    winrt::Telegram::Native::Highlight::SyntaxToken SyntaxToken::Tokenize(hstring language, hstring coddiri)
    {
        Initialize();

        std::string hcode = winrt::to_string(coddiri);
        std::string hlanguage = winrt::to_string(language);

        auto tokens = m_highlighter->tokenize(hcode, hlanguage);

        auto wrapped = winrt::make_self<SyntaxToken>(hlanguage, "alias", std::move(tokens));
        return wrapped.as<winrt::Telegram::Native::Highlight::SyntaxToken>();
    }

    IVector<winrt::Telegram::Native::Highlight::Token> SyntaxToken::Wrap(TokenList::ConstIterator begin, TokenList::ConstIterator end)
    {
        auto children = winrt::single_threaded_vector<winrt::Telegram::Native::Highlight::Token>();

        for (auto it = begin; it != end; ++it)
        {
            auto& node = *it;
            if (node.isSyntax())
            {
                const auto& child = dynamic_cast<const Syntax&>(node);
                auto wrapped = winrt::make_self<SyntaxToken>(child);
                children.Append(wrapped.as<winrt::Telegram::Native::Highlight::SyntaxToken>());
            }
            else
            {
                const auto& child = dynamic_cast<const Text&>(node);
                auto wrapped = winrt::make_self<winrt::Telegram::Native::Highlight::implementation::TextToken>(child);
                children.Append(wrapped.as<winrt::Telegram::Native::Highlight::TextToken>());
            }
        }

        return children;
    }
}
