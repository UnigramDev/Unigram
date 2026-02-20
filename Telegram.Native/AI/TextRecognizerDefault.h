#pragma once

#include "AI/TextRecognizer.g.h"

#include <winrt/Windows.Media.Ocr.h>
#include <winrt/Windows.Graphics.Imaging.h>
#include <winrt/Windows.Foundation.Collections.h>

using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Graphics::Imaging;

namespace winrt::Telegram::Native::AI::implementation
{
    struct TextRecognizerDefault : TextRecognizerT<TextRecognizerDefault>
    {
        TextRecognizerDefault(winrt::Windows::Media::Ocr::OcrEngine engine);

        Windows::Foundation::IAsyncOperation<RecognizedText> RecognizeAsync(SoftwareBitmap bitmap);

    private:
        winrt::Windows::Media::Ocr::OcrEngine m_engine;
    };
}
