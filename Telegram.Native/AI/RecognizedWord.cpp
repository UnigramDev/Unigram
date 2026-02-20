#include "pch.h"
#include "RecognizedWord.h"
#if __has_include("AI/RecognizedWord.g.cpp")
#include "AI/RecognizedWord.g.cpp"
#endif

namespace winrt::Telegram::Native::AI::implementation
{
    RecognizedWord::RecognizedWord(hstring content, RecognizedTextBoundingBox boundingBox)
        : m_text(content)
        , m_boundingBox(boundingBox)
    {

    }

    hstring RecognizedWord::Text()
    {
        return m_text;
    }

    RecognizedTextBoundingBox RecognizedWord::BoundingBox() const
    {
        return m_boundingBox;
    }

    hstring RecognizedWord::ToString()
    {
        return m_text;
    }
}
