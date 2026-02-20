#include "pch.h"
#include "RecognizedText.h"
#if __has_include("AI/RecognizedText.g.cpp")
#include "AI/RecognizedText.g.cpp"
#endif

namespace winrt::Telegram::Native::AI::implementation
{
    RecognizedText::RecognizedText(IVector<RecognizedLine> lines, float textAngle)
        : m_lines(lines)
        , m_textAngle(textAngle)
    {

    }

    IVector<RecognizedLine> RecognizedText::Lines()
    {
        return m_lines;
    }

    float RecognizedText::TextAngle() const
    {
        return m_textAngle;
    }
}
