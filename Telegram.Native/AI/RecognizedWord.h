#pragma once

#include "AI/RecognizedWord.g.h"

namespace winrt::Telegram::Native::AI::implementation
{
    struct RecognizedWord : RecognizedWordT<RecognizedWord>
    {
        RecognizedWord(hstring text, RecognizedTextBoundingBox boundingBox);

        hstring Text();
        RecognizedTextBoundingBox BoundingBox() const;

        hstring ToString();

    private:
        hstring m_text;
        RecognizedTextBoundingBox m_boundingBox;
    };
}

namespace winrt::Telegram::Native::AI::factory_implementation
{
    struct RecognizedWord : RecognizedWordT<RecognizedWord, implementation::RecognizedWord>
    {
    };
}
