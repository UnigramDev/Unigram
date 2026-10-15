#pragma once

#include "Highlight/SyntaxLanguage.g.h"

#include <winrt/Windows.Foundation.Collections.h>

namespace winrt::Telegram::Native::Highlight::implementation
{
    struct SyntaxLanguage : SyntaxLanguageT<SyntaxLanguage>
    {
        SyntaxLanguage(hstring id, hstring name, winrt::Windows::Foundation::Collections::IVectorView<hstring> aliases);

        hstring Id();
        hstring Name();
        winrt::Windows::Foundation::Collections::IVectorView<hstring> Aliases();

    private:
        hstring m_id;
        hstring m_name;
        winrt::Windows::Foundation::Collections::IVectorView<hstring> m_aliases;
    };
}
