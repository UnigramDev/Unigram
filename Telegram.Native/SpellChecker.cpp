#include "pch.h"
#include "SpellChecker.h"
#if __has_include("SpellChecker.g.cpp")
#include "SpellChecker.g.cpp"
#endif

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::implementation
{
    // Windows 10's checker throws std::regex_error on long text with some characters in it, @
    // among them, so long text is checked in pieces, cut after a space near the end of each.
    constexpr size_t kChunk = 5000;
    constexpr size_t kWord = 100;

    SpellChecker::SpellChecker(hstring languageTag, hstring scripts, winrt::com_ptr<::ISpellChecker> checker)
        : m_languageTag(languageTag)
        , m_scripts(scripts)
        , m_checker(checker)
    {
    }

    // A fresh factory each time: it is only asked for a checker per language change, and holding
    // one would tie it to the apartment of whichever thread asked first.
    winrt::com_ptr<ISpellCheckerFactory> SpellChecker::Factory()
    {
        winrt::com_ptr<ISpellCheckerFactory> factory;
        if (FAILED(CoCreateInstance(__uuidof(SpellCheckerFactory), nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(factory.put()))))
        {
            return nullptr;
        }

        return factory;
    }

    bool SpellChecker::IsSupported(hstring languageTag)
    {
        auto factory = Factory();
        BOOL supported = FALSE;

        return factory != nullptr && SUCCEEDED(factory->IsSupported(languageTag.c_str(), &supported)) && supported;
    }

    winrt::Telegram::Native::SpellChecker SpellChecker::Create(hstring languageTag)
    {
        auto factory = Factory();
        BOOL supported = FALSE;

        if (factory == nullptr || FAILED(factory->IsSupported(languageTag.c_str(), &supported)) || !supported)
        {
            return nullptr;
        }

        winrt::com_ptr<::ISpellChecker> checker;
        if (FAILED(factory->CreateSpellChecker(languageTag.c_str(), checker.put())))
        {
            return nullptr;
        }

        WCHAR scripts[64];
        if (GetLocaleInfoEx(languageTag.c_str(), LOCALE_SSCRIPTS, scripts, ARRAYSIZE(scripts)) == 0)
        {
            scripts[0] = L'\0';
        }

        return winrt::make<SpellChecker>(languageTag, hstring(scripts), checker);
    }

    hstring SpellChecker::LanguageTag()
    {
        return m_languageTag;
    }

    hstring SpellChecker::Scripts()
    {
        return m_scripts;
    }

    IVector<int32_t> SpellChecker::Check(hstring text)
    {
        auto result = winrt::single_threaded_vector<int32_t>();

        std::wstring_view view = text;
        if (view.size() <= kChunk && view.find(L'\x2029') == std::wstring_view::npos)
        {
            Check(text.c_str(), 0, result);
            return result;
        }

        // Runs of paragraph separators crash the checker; a space is the same to it.
        std::wstring copy(view);
        std::replace(copy.begin(), copy.end(), L'\x2029', L' ');

        size_t start = 0;

        while (start < copy.size())
        {
            auto end = std::min(start + kChunk, copy.size());
            if (end < copy.size())
            {
                for (auto cut = end; cut > end - kWord; cut--)
                {
                    if (iswspace(copy[cut - 1]))
                    {
                        end = cut;
                        break;
                    }
                }
            }

            // Cut in place, and the character at the cut put back once the piece is checked.
            auto saved = copy[end];
            copy[end] = L'\0';
            Check(copy.c_str() + start, (int32_t)start, result);
            copy[end] = saved;

            start = end;
        }

        return result;
    }

    void SpellChecker::Check(const wchar_t* text, int32_t offset, IVector<int32_t> const& result)
    {
        winrt::com_ptr<IEnumSpellingError> errors;
        if (text[0] == L'\0' || FAILED(m_checker->Check(text, errors.put())) || errors == nullptr)
        {
            return;
        }

        while (true)
        {
            winrt::com_ptr<ISpellingError> error;
            if (errors->Next(error.put()) != S_OK || error == nullptr)
            {
                break;
            }

            ULONG start = 0;
            ULONG length = 0;
            CORRECTIVE_ACTION action = CORRECTIVE_ACTION_NONE;

            if (FAILED(error->get_StartIndex(&start)) || FAILED(error->get_Length(&length)) || FAILED(error->get_CorrectiveAction(&action)))
            {
                continue;
            }

            // A word with suggestions, or one the checker would correct itself. Delete - a word
            // typed twice - has nothing to suggest, so there would be nothing to offer for it.
            if ((action == CORRECTIVE_ACTION_GET_SUGGESTIONS || action == CORRECTIVE_ACTION_REPLACE) && length > 0 && IsOwnScript(text + start, (int32_t)length))
            {
                result.Append(offset + (int32_t)start);
                result.Append((int32_t)length);
            }
        }
    }

    bool SpellChecker::IsOwnScript(const wchar_t* word, int32_t length)
    {
        // A language whose scripts the locale does not know judges nothing, rather than taking
        // every word in every script for a misspelling.
        if (m_scripts.empty())
        {
            return false;
        }

        WCHAR scripts[64];
        auto count = GetStringScripts(0, word, length, scripts, ARRAYSIZE(scripts));

        // No script at all is digits and punctuation, and the checker's word stands. Zero is a
        // word in more scripts than fit, which no dictionary has.
        if (count == 0)
        {
            return false;
        }

        return scripts[0] == L'\0' || VerifyScripts(0, m_scripts.c_str(), -1, scripts, -1);
    }

    IVector<hstring> SpellChecker::Suggest(hstring word)
    {
        auto result = winrt::single_threaded_vector<hstring>();

        winrt::com_ptr<IEnumString> suggestions;
        if (word.empty() || FAILED(m_checker->Suggest(word.c_str(), suggestions.put())) || suggestions == nullptr)
        {
            return result;
        }

        while (true)
        {
            LPOLESTR suggestion = nullptr;
            ULONG fetched = 0;

            if (suggestions->Next(1, &suggestion, &fetched) != S_OK || fetched == 0)
            {
                break;
            }

            result.Append(hstring(suggestion));
            CoTaskMemFree(suggestion);
        }

        return result;
    }

    void SpellChecker::Add(hstring word)
    {
        if (!word.empty())
        {
            m_checker->Add(word.c_str());
        }
    }

    void SpellChecker::Ignore(hstring word)
    {
        if (!word.empty())
        {
            m_checker->Ignore(word.c_str());
        }
    }
}
