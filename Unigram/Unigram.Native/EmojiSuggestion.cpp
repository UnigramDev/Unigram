#include "pch.h"
#include "EmojiSuggestion.h"

using namespace Platform;
using namespace Ui::Emoji;
using namespace Unigram::Native;

EmojiSuggestion::EmojiSuggestion(Suggestion suggestion)
{
	auto emoji = reinterpret_cast<const wchar_t*>(suggestion.emoji().data());
	m_emoji = ref new String(emoji, suggestion.emoji().size());

	auto label = reinterpret_cast<const wchar_t*>(suggestion.label().data());
	m_label = ref new String(label, suggestion.label().size());

	auto replacement = reinterpret_cast<const wchar_t*>(suggestion.replacement().data());
	m_replacement = ref new String(replacement, suggestion.replacement().size());
}

Array<EmojiSuggestion^>^ EmojiSuggestion::GetSuggestions(String^ query)
{
	auto data = reinterpret_cast<const utf16char*>(query->Data());
	auto results = Ui::Emoji::GetSuggestions(utf16string(data, query->Length()));

	std::vector<EmojiSuggestion^> suggestions;

	for (auto &item : results)
	{
		suggestions.push_back(ref new EmojiSuggestion(item));
	}

	return ref new Array<EmojiSuggestion^>(suggestions.data(), suggestions.size());
}