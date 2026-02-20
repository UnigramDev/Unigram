#pragma once

#include "AI/TextRecognizer.g.h"
#include "TextRecognizerOne.h"

#include <winrt/Windows.Graphics.Imaging.h>
#include <winrt/Windows.Foundation.Collections.h>

using PFN_GetFinalPathNameByHandleW = DWORD(WINAPI*)(HANDLE, LPWSTR, DWORD, DWORD);

using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Graphics::Imaging;

namespace winrt::Telegram::Native::AI::implementation
{
    struct TextRecognizer : TextRecognizerT<TextRecognizer>
    {
        TextRecognizer(TextRecognizerOne& engine, hstring modelKey);

        static ITextRecognizer GetDefault();
        static ITextRecognizer GetOne(hstring modelKey);

        Windows::Foundation::IAsyncOperation<RecognizedText> RecognizeAsync(SoftwareBitmap bitmap);

    private:
        static DWORD WINAPI GetFinalPathNameByHandleWHook(_In_ HANDLE hFile, _Out_writes_(cchFilePath) LPWSTR lpszFilePath, _In_ DWORD cchFilePath, _In_ DWORD dwFlags);
        static PFN_GetFinalPathNameByHandleW GetFinalPathNameByHandleWOriginal;

        static void AttachHook();
        static void DetachHook();

        static std::mutex m_hookLock;
        static bool m_hookAttached;

        TextRecognizerOne& m_engine;
        hstring m_modelKey;
    };
}

namespace winrt::Telegram::Native::AI::factory_implementation
{
    struct TextRecognizer : TextRecognizerT<TextRecognizer, implementation::TextRecognizer>
    {
    };
}
