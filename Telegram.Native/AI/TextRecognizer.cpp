#include "pch.h"
#include "TextRecognizer.h"
#include "AI/TextRecognizer.g.cpp"

#include "TextRecognizerDefault.h"
#include "TextRecognizerOne.h"

#include <detours.h>

#include <winrt/Windows.Graphics.Imaging.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.System.Threading.h>
#include <thread>
#include <chrono>

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::System::Threading;
using namespace winrt::Windows::Graphics::Imaging;
using namespace winrt::Windows::Storage;
using namespace winrt::Windows::Storage::Streams;
using namespace std::chrono_literals;

namespace winrt::Telegram::Native::AI::implementation
{
    TextRecognizer::TextRecognizer(TextRecognizerOne& engine, hstring modelKey)
        : m_engine(engine)
        , m_modelKey(modelKey)
    {
    }

    ITextRecognizer TextRecognizer::GetDefault()
    {
        auto engine = winrt::Windows::Media::Ocr::OcrEngine::TryCreateFromUserProfileLanguages();
        if (engine)
        {
            return winrt::make_self<TextRecognizerDefault>(engine).as<ITextRecognizer>();
        }

        return nullptr;
    }

    ITextRecognizer TextRecognizer::GetOne(hstring modelKey)
    {
        auto& ocr = TextRecognizerOne::Instance();
        if (ocr.IsLoaded())
        {
            return winrt::make_self<TextRecognizer>(ocr, modelKey).as<ITextRecognizer>();
        }

        return nullptr;
    }

    std::wstring GetFilePathFromHandle(HANDLE hFile)
    {
        DWORD size = MAX_PATH;
        std::vector<wchar_t> buffer(size);

        // First call to get required size
        if (!GetFileInformationByHandleEx(hFile, FileNameInfo, buffer.data(), size * sizeof(wchar_t)))
        {
            if (GetLastError() == ERROR_MORE_DATA)
            {
                // Resize buffer and retry
                FILE_NAME_INFO* fni = reinterpret_cast<FILE_NAME_INFO*>(buffer.data());
                size = fni->FileNameLength / sizeof(wchar_t) + 2;
                buffer.resize(size);
                if (!GetFileInformationByHandleEx(hFile, FileNameInfo, buffer.data(), size * sizeof(wchar_t)))
                    return L"";
            }
            else
            {
                return L"";
            }
        }

        FILE_NAME_INFO* fni = reinterpret_cast<FILE_NAME_INFO*>(buffer.data());
        return std::wstring(fni->FileName, fni->FileNameLength / sizeof(wchar_t));
    }

    PFN_GetFinalPathNameByHandleW TextRecognizer::GetFinalPathNameByHandleWOriginal = nullptr;
    std::mutex TextRecognizer::m_hookLock;
    bool TextRecognizer::m_hookAttached = false;

    DWORD WINAPI TextRecognizer::GetFinalPathNameByHandleWHook(_In_ HANDLE hFile, _Out_writes_(cchFilePath) LPWSTR lpszFilePath, _In_ DWORD cchFilePath, _In_ DWORD dwFlags)
    {
        std::wstring test = GetFilePathFromHandle(hFile);
        if (test.ends_with(L"oneocr.onemodel"))
        {
            auto modelPath = L"\\\\?\\" + ApplicationData::Current().LocalFolder().Path() + L"\\Ocr\\oneocr.onemodel";
            wcsncpy_s(lpszFilePath, cchFilePath, modelPath.c_str(), modelPath.size());

            return modelPath.size();
        }

        return GetFinalPathNameByHandleWOriginal(hFile, lpszFilePath, cchFilePath, dwFlags);
    }

    IAsyncOperation<RecognizedText> TextRecognizer::RecognizeAsync(SoftwareBitmap bitmap)
    {
        co_await winrt::resume_background();

        if (bitmap.BitmapPixelFormat() != BitmapPixelFormat::Bgra8 ||
            bitmap.BitmapAlphaMode() != BitmapAlphaMode::Premultiplied)
        {
            bitmap = SoftwareBitmap::Convert(bitmap, BitmapPixelFormat::Bgra8, BitmapAlphaMode::Premultiplied);
        }

        BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode::Read);
        auto reference = buffer.CreateReference();
        auto data = reference.data();

        auto desc = buffer.GetPlaneDescription(0);
        int32_t cols = desc.Width;
        int32_t rows = desc.Height;
        int32_t step = desc.Stride;

        ImageBuffer image =
        {
            .t = 3,
            .col = cols,
            .row = rows,
            ._unk = 0,
            .step = (__int64)step,
            .data_ptr = (__int64)reinterpret_cast<char*>(data)
        };

        std::string modelPath = winrt::to_string(ApplicationData::Current().LocalFolder().Path() + L"\\Ocr\\oneocr.onemodel");
        std::string decryptionKey = winrt::to_string(m_modelKey);
        const char* model = modelPath.c_str();
        const char* key = decryptionKey.c_str();

        auto lines = winrt::single_threaded_vector<RecognizedLine>();

        __int64 initOptions = 0;
        __int64 pipeline = 0;
        __int64 processOptions = 0;
        __int64 result = 0;
        __int64 res = m_engine.CreateOcrInitOptions(&initOptions);
        if (res != 0)
        {
            goto Cleanup;
        }

        res = m_engine.OcrInitOptionsSetUseModelDelayLoad(initOptions, 0);
        if (res != 0)
        {
            goto Cleanup;
        }

        AttachHook();

        res = m_engine.CreateOcrPipeline((__int64)model, (__int64)key, initOptions, &pipeline);
        if (res != 0)
        {
            goto Cleanup;
        }

        res = m_engine.CreateOcrProcessOptions(&processOptions);
        if (res != 0)
        {
            goto Cleanup;
        }

        res = m_engine.OcrProcessOptionsSetMaxRecognitionLineCount(processOptions, 1000);
        if (res != 0)
        {
            goto Cleanup;
        }

        res = m_engine.RunOcrPipeline(pipeline, &image, processOptions, &result);
        if (res != 0)
        {
            goto Cleanup;
        }

        __int64 lineCount;
        res = m_engine.GetOcrLineCount(result, &lineCount);
        if (res != 0)
        {
            goto Cleanup;
        }

        for (__int64 i = 0; i < lineCount; i++)
        {
            __int64 line = 0;
            LPSTR lineContent;
            BoundingBox* lineBoundingBox;
            m_engine.GetOcrLine(result, i, &line);
            m_engine.GetOcrLineContent(line, &lineContent);
            m_engine.GetOcrLineBoundingBox(line, &lineBoundingBox);

            // TODO: GetOcrLineStyle, currently not used

            __int64 wordCount = 0;
            m_engine.GetOcrLineWordCount(line, &wordCount);

            auto words = winrt::single_threaded_vector<RecognizedWord>();

            for (__int64 j = 0; j < wordCount; j++)
            {
                __int64 word = 0;
                LPSTR wordContent;
                BoundingBox* wordBoundingBox;
                m_engine.GetOcrWord(line, j, &word);
                m_engine.GetOcrWordContent(word, &wordContent);
                m_engine.GetOcrWordBoundingBox(word, &wordBoundingBox);

                // TODO: GetOcrWordConfidence, currently not used

                words.Append(RecognizedWord(winrt::to_hstring(wordContent), RecognizedTextBoundingBox{
                    float2(wordBoundingBox->x1, wordBoundingBox->y1),
                    float2(wordBoundingBox->x2, wordBoundingBox->y2),
                    float2(wordBoundingBox->x3, wordBoundingBox->y3),
                    float2(wordBoundingBox->x4, wordBoundingBox->y4)
                    }));
            }

            lines.Append(RecognizedLine(winrt::to_hstring(lineContent), RecognizedTextBoundingBox{
                float2(lineBoundingBox->x1, lineBoundingBox->y1),
                float2(lineBoundingBox->x2, lineBoundingBox->y2),
                float2(lineBoundingBox->x3, lineBoundingBox->y3),
                float2(lineBoundingBox->x4, lineBoundingBox->y4)
                }, words));
        }

    Cleanup:
        m_engine.ReleaseOcrResult(result);
        m_engine.ReleaseOcrProcessOptions(processOptions);
        m_engine.ReleaseOcrPipeline(pipeline);
        m_engine.ReleaseOcrInitOptions(initOptions);

        DetachHook();

        // TODO: GetImageAngle, currently not used
        co_return RecognizedText(lines, 0);
    }

    void TextRecognizer::AttachHook()
    {
        std::lock_guard lock(m_hookLock);

        if (m_hookAttached)
        {
            return;
        }

        if (!GetFinalPathNameByHandleWOriginal)
        {
            HMODULE kernel32 = GetModuleHandle(L"Kernel32.dll");
            if (!kernel32) kernel32 = LoadLibrary(L"Kernel32.dll");

            GetFinalPathNameByHandleWOriginal =
                reinterpret_cast<PFN_GetFinalPathNameByHandleW>(GetProcAddress(kernel32, "GetFinalPathNameByHandleW"));
        }

        if (GetFinalPathNameByHandleWOriginal)
        {
            DetourTransactionBegin();
            DetourUpdateThread(GetCurrentThread());

            DetourAttach(reinterpret_cast<PVOID*>(&TextRecognizer::GetFinalPathNameByHandleWOriginal),
                TextRecognizer::GetFinalPathNameByHandleWHook);

            if (DetourTransactionCommit() == NO_ERROR)
            {
                m_hookAttached = true;
            }
        }
    }

    void TextRecognizer::DetachHook()
    {
        std::lock_guard lock(m_hookLock);

        if (!m_hookAttached || !GetFinalPathNameByHandleWOriginal)
        {
            return;
        }

        DetourTransactionBegin();
        DetourUpdateThread(GetCurrentThread());

        DetourDetach(reinterpret_cast<PVOID*>(&TextRecognizer::GetFinalPathNameByHandleWOriginal),
            TextRecognizer::GetFinalPathNameByHandleWHook);

        if (DetourTransactionCommit() == NO_ERROR)
        {
            m_hookAttached = false;
        }
    }
}
