#pragma once

#include "LanguageIdentification.g.h"

#include <ppl.h>

#include "lang_id/lang-id.h"

using namespace concurrency;
using namespace libtextclassifier3::mobile::lang_id;

namespace winrt::Telegram::Native::implementation
{
    struct LanguageIdentification : LanguageIdentificationT<LanguageIdentification>
    {
        static hstring IdentifyLanguage(hstring text);

    private:
        static critical_section s_criticalSection;
        static std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> s_langid;

        static LangId* Current();
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct LanguageIdentification : LanguageIdentificationT<LanguageIdentification, implementation::LanguageIdentification>
    {
    };
}
