#pragma once

#include "SpellChecker.g.h"

#include <spellcheck.h>

#include <winrt/Windows.Foundation.Collections.h>

namespace winrt::Telegram::Native::implementation
{
    struct SpellChecker : SpellCheckerT<SpellChecker>
    {
        SpellChecker(hstring languageTag, winrt::com_ptr<::ISpellChecker> checker);

        static bool IsSupported(hstring languageTag);
        static winrt::Telegram::Native::SpellChecker Create(hstring languageTag);

        hstring LanguageTag();

        winrt::Windows::Foundation::Collections::IVector<int32_t> Check(hstring text);
        winrt::Windows::Foundation::Collections::IVector<hstring> Suggest(hstring word);

        void Add(hstring word);
        void Ignore(hstring word);

    private:
        static winrt::com_ptr<ISpellCheckerFactory> Factory();

        hstring m_languageTag;
        winrt::com_ptr<::ISpellChecker> m_checker;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct SpellChecker : SpellCheckerT<SpellChecker, implementation::SpellChecker>
    {
    };
}
