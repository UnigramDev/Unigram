#include "pch.h"
#include "SyntaxLanguage.h"
#if __has_include("Highlight/SyntaxLanguage.g.cpp")
#include "Highlight/SyntaxLanguage.g.cpp"
#endif

namespace winrt::Telegram::Native::Highlight::implementation
{
    SyntaxLanguage::SyntaxLanguage(hstring id, hstring name, winrt::Windows::Foundation::Collections::IVectorView<hstring> aliases)
        : m_id(id)
        , m_name(name)
        , m_aliases(aliases)
    {
    }

    hstring SyntaxLanguage::Id()
    {
        return m_id;
    }

    hstring SyntaxLanguage::Name()
    {
        return m_name;
    }

    winrt::Windows::Foundation::Collections::IVectorView<hstring> SyntaxLanguage::Aliases()
    {
        return m_aliases;
    }
}
