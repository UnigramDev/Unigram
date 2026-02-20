#pragma once

#include "AI/RecognizedText.g.h"

#include <winrt/Windows.Foundation.Collections.h>

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::AI::implementation
{
    struct RecognizedText : RecognizedTextT<RecognizedText>
    {
        RecognizedText(IVector<RecognizedLine> lines, float textAngle);

        IVector<RecognizedLine> Lines();
        float TextAngle() const;

    private:
        IVector<RecognizedLine> m_lines;
        float m_textAngle;
    };
}

namespace winrt::Telegram::Native::AI::factory_implementation
{
    struct RecognizedText : RecognizedTextT<RecognizedText, implementation::RecognizedText>
    {
    };
}
