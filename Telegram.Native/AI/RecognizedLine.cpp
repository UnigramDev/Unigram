#include "pch.h"
#include "RecognizedLine.h"
#if __has_include("AI/RecognizedLine.g.cpp")
#include "AI/RecognizedLine.g.cpp"
#endif

namespace winrt::Telegram::Native::AI::implementation
{
    RecognizedLine::RecognizedLine(hstring text, RecognizedTextBoundingBox boundingBox, IVector<RecognizedWord> words)
        : m_text(text)
        , m_boundingBox(boundingBox)
        , m_words(words)
    {

    }

    hstring RecognizedLine::Text()
    {
        return m_text;
    }

    RecognizedTextBoundingBox RecognizedLine::BoundingBox() const
    {
        return m_boundingBox;
    }

    IVector<RecognizedWord> RecognizedLine::Words()
    {
        return m_words;
    }

    hstring RecognizedLine::ToString()
    {
        return m_text;
    }
}
