#pragma once

#include "emoji_suggestions.h"

using namespace Platform;
using namespace Ui::Emoji;

namespace Unigram
{
	namespace Native
	{
		public ref class EmojiSuggestion sealed
		{
		public:
			static Array<EmojiSuggestion^>^ GetSuggestions(String^ query);

			property String^ Emoji
			{
				String^ get() { return m_emoji; }
			}

			property String^ Label
			{
				String^ get() { return m_label; }
			}

			property String^ Replacement
			{
				String^ get() { return m_replacement; }
			}


		private:
			EmojiSuggestion(Suggestion suggestion);

			String^ m_emoji;
			String^ m_label;
			String^ m_replacement;
		};
	}
}
