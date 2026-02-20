#pragma once

#include "AI/RecognizedLine.g.h"

#include <winrt/Windows.Foundation.Collections.h>

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::AI::implementation
{
    struct RecognizedLine : RecognizedLineT<RecognizedLine>
    {
        RecognizedLine(hstring text, RecognizedTextBoundingBox boundingBox, IVector<RecognizedWord> words);

        hstring Text();
        RecognizedTextBoundingBox BoundingBox() const;
        IVector<RecognizedWord> Words();

        hstring ToString();

    private:
        hstring m_text;
        RecognizedTextBoundingBox m_boundingBox;
        IVector<RecognizedWord> m_words;
    };
}

namespace winrt::Telegram::Native::AI::factory_implementation
{
    struct RecognizedLine : RecognizedLineT<RecognizedLine, implementation::RecognizedLine>
    {
    };
}
