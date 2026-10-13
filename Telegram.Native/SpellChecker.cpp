#include "pch.h"
#include "SpellChecker.h"
#if __has_include("SpellChecker.g.cpp")
#include "SpellChecker.g.cpp"
#endif

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::implementation
{
    SpellChecker::SpellChecker(hstring languageTag, winrt::com_ptr<::ISpellChecker> checker)
        : m_languageTag(languageTag)
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

        return winrt::make<SpellChecker>(languageTag, checker);
    }

    hstring SpellChecker::LanguageTag()
    {
        return m_languageTag;
    }

    IVector<int32_t> SpellChecker::Check(hstring text)
    {
        auto result = winrt::single_threaded_vector<int32_t>();

        winrt::com_ptr<IEnumSpellingError> errors;
        if (text.empty() || FAILED(m_checker->Check(text.c_str(), errors.put())) || errors == nullptr)
        {
            return result;
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
            if ((action == CORRECTIVE_ACTION_GET_SUGGESTIONS || action == CORRECTIVE_ACTION_REPLACE) && length > 0)
            {
                result.Append((int32_t)start);
                result.Append((int32_t)length);
            }
        }

        return result;
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
