#pragma once

#include "SpellChecker.g.h"

#include <spellcheck.h>

#include <winrt/Windows.Foundation.Collections.h>

namespace winrt::Telegram::Native::implementation
{
    struct SpellChecker : SpellCheckerT<SpellChecker>
    {
        SpellChecker(hstring languageTag, hstring scripts, winrt::com_ptr<::ISpellChecker> checker);

        static bool IsSupported(hstring languageTag);
        static winrt::Telegram::Native::SpellChecker Create(hstring languageTag);

        hstring LanguageTag();
        hstring Scripts();

        winrt::Windows::Foundation::Collections::IVector<int32_t> Check(hstring text);
        winrt::Windows::Foundation::Collections::IVector<hstring> Suggest(hstring word);

        void Add(hstring word);
        void Ignore(hstring word);

    private:
        static winrt::com_ptr<ISpellCheckerFactory> Factory();

        void Check(const wchar_t* text, int32_t offset, winrt::Windows::Foundation::Collections::IVector<int32_t> const& result);
        bool IsOwnScript(const wchar_t* word, int32_t length);

        hstring m_languageTag;
        hstring m_scripts;
        winrt::com_ptr<::ISpellChecker> m_checker;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct SpellChecker : SpellCheckerT<SpellChecker, implementation::SpellChecker>
    {
    };
}
