#include "pch.h"
#include "LanguageIdentification.h"
#if __has_include("LanguageIdentification.g.cpp")
#include "LanguageIdentification.g.cpp"
#endif

#include "StringUtils.h"

#include "lang_id/fb_model/lang-id-from-fb.h"
#include "lang_id/lang-id.h"

namespace winrt::Unigram::Native::implementation
{
	critical_section LanguageIdentification::s_criticalSection;
	std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> LanguageIdentification::s_langid{ nullptr };

	LangId* LanguageIdentification::Current()
	{
		auto lock = critical_section::scoped_lock(s_criticalSection);

		if (s_langid == nullptr) {
			s_langid = libtextclassifier3::mobile::lang_id::GetLangIdFromFlatbufferFile("Assets\\langid_model.smfb.jpg");
		}

		return s_langid.get();
	}

	hstring LanguageIdentification::IdentifyLanguage(hstring text) {
		auto unmanaged = string_to_unmanaged(text);
		auto language = Current()->FindLanguage(unmanaged);
		return string_from_unmanaged(language);
	}
}
