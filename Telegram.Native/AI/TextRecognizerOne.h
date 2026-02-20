#pragma once
#include <windows.h>
#include <string>
#include <winrt/Windows.Storage.h>

typedef struct
{
    __int32 t;
    __int32 col;
    __int32 row;
    __int32 _unk;
    __int64 step;
    __int64 data_ptr;
} ImageBuffer;

typedef struct
{
    float x1;
    float y1;
    float x2;
    float y2;
    float x3;
    float y3;
    float x4;
    float y4;
} BoundingBox;

typedef __int64(__cdecl* CreateOcrInitOptions_t)(__int64*);
typedef __int64(__cdecl* CreateOcrPipeline_t)(__int64, __int64, __int64, __int64*);
typedef __int64(__cdecl* CreateOcrProcessOptions_t)(__int64*);
typedef __int64(__cdecl* GetOcrLine_t)(__int64, __int64, __int64*);
typedef __int64(__cdecl* GetOcrLineBoundingBox_t)(__int64, BoundingBox**);
typedef __int64(__cdecl* GetOcrLineContent_t)(__int64, LPSTR*);
typedef __int64(__cdecl* GetOcrLineCount_t)(__int64, __int64*);
typedef __int64(__cdecl* GetOcrLineWordCount_t)(__int64, __int64*);
typedef __int64(__cdecl* GetOcrWord_t)(__int64, __int64, __int64*);
typedef __int64(__cdecl* GetOcrWordBoundingBox_t)(__int64, BoundingBox**);
typedef __int64(__cdecl* GetOcrWordContent_t)(__int64, LPSTR*);
typedef __int64(__cdecl* OcrInitOptionsSetUseModelDelayLoad_t)(__int64, char);
typedef __int64(__cdecl* OcrProcessOptionsSetMaxRecognitionLineCount_t)(__int64, __int64);
typedef __int64(__cdecl* RunOcrPipeline_t)(__int64, ImageBuffer*, __int64, __int64*);
typedef __int64(__cdecl* ReleaseOcrInitOptions_t)(__int64);
typedef __int64(__cdecl* ReleaseOcrPipeline_t)(__int64);
typedef __int64(__cdecl* ReleaseOcrProcessOptions_t)(__int64);
typedef __int64(__cdecl* ReleaseOcrResult_t)(__int64);

using namespace winrt;
using namespace winrt::Windows::Storage;

class TextRecognizerOne
{
public:
    static TextRecognizerOne& Instance()
    {
        static TextRecognizerOne instance;
        return instance;
    }

    bool IsLoaded() const { return m_fullyLoaded; }

    // Function pointers
    CreateOcrInitOptions_t CreateOcrInitOptions = nullptr;
    CreateOcrPipeline_t CreateOcrPipeline = nullptr;
    CreateOcrProcessOptions_t CreateOcrProcessOptions = nullptr;
    GetOcrLine_t GetOcrLine = nullptr;
    GetOcrLineBoundingBox_t GetOcrLineBoundingBox = nullptr;
    GetOcrLineContent_t GetOcrLineContent = nullptr;
    GetOcrLineCount_t GetOcrLineCount = nullptr;
    GetOcrLineWordCount_t GetOcrLineWordCount = nullptr;
    GetOcrWord_t GetOcrWord = nullptr;
    GetOcrWordBoundingBox_t GetOcrWordBoundingBox = nullptr;
    GetOcrWordContent_t GetOcrWordContent = nullptr;
    OcrInitOptionsSetUseModelDelayLoad_t OcrInitOptionsSetUseModelDelayLoad = nullptr;
    OcrProcessOptionsSetMaxRecognitionLineCount_t OcrProcessOptionsSetMaxRecognitionLineCount = nullptr;
    RunOcrPipeline_t RunOcrPipeline = nullptr;
    ReleaseOcrInitOptions_t ReleaseOcrInitOptions = nullptr;
    ReleaseOcrPipeline_t ReleaseOcrPipeline = nullptr;
    ReleaseOcrProcessOptions_t ReleaseOcrProcessOptions = nullptr;
    ReleaseOcrResult_t ReleaseOcrResult = nullptr;

private:
    HMODULE m_hOnnxRuntime = nullptr;
    HMODULE m_hOneOcr = nullptr;
    bool m_fullyLoaded = false;

    TextRecognizerOne()
    {
        hstring onnxruntime = ApplicationData::Current().LocalFolder().Path() + L"\\Ocr\\onnxruntime.dll";
        hstring oneocr = ApplicationData::Current().LocalFolder().Path() + L"\\Ocr\\oneocr.dll";
        m_hOnnxRuntime = LoadLibraryW(onnxruntime.c_str());
        m_hOneOcr = LoadLibraryW(oneocr.c_str());
        if (!m_hOnnxRuntime || !m_hOneOcr) return;

        bool ok = true;
        ok &= (CreateOcrInitOptions = load<CreateOcrInitOptions_t>("CreateOcrInitOptions")) != nullptr;
        ok &= (CreateOcrPipeline = load<CreateOcrPipeline_t>("CreateOcrPipeline")) != nullptr;
        ok &= (CreateOcrProcessOptions = load<CreateOcrProcessOptions_t>("CreateOcrProcessOptions")) != nullptr;
        ok &= (GetOcrLine = load<GetOcrLine_t>("GetOcrLine")) != nullptr;
        ok &= (GetOcrLineBoundingBox = load<GetOcrLineBoundingBox_t>("GetOcrLineBoundingBox")) != nullptr;
        ok &= (GetOcrLineContent = load<GetOcrLineContent_t>("GetOcrLineContent")) != nullptr;
        ok &= (GetOcrLineCount = load<GetOcrLineCount_t>("GetOcrLineCount")) != nullptr;
        ok &= (GetOcrLineWordCount = load<GetOcrLineWordCount_t>("GetOcrLineWordCount")) != nullptr;
        ok &= (GetOcrWord = load<GetOcrWord_t>("GetOcrWord")) != nullptr;
        ok &= (GetOcrWordBoundingBox = load<GetOcrWordBoundingBox_t>("GetOcrWordBoundingBox")) != nullptr;
        ok &= (GetOcrWordContent = load<GetOcrWordContent_t>("GetOcrWordContent")) != nullptr;
        ok &= (OcrInitOptionsSetUseModelDelayLoad = load<OcrInitOptionsSetUseModelDelayLoad_t>("OcrInitOptionsSetUseModelDelayLoad")) != nullptr;
        ok &= (OcrProcessOptionsSetMaxRecognitionLineCount = load<OcrProcessOptionsSetMaxRecognitionLineCount_t>("OcrProcessOptionsSetMaxRecognitionLineCount")) != nullptr;
        ok &= (RunOcrPipeline = load<RunOcrPipeline_t>("RunOcrPipeline")) != nullptr;
        ok &= (ReleaseOcrInitOptions = load<ReleaseOcrInitOptions_t>("ReleaseOcrInitOptions")) != nullptr;
        ok &= (ReleaseOcrPipeline = load<ReleaseOcrPipeline_t>("ReleaseOcrPipeline")) != nullptr;
        ok &= (ReleaseOcrProcessOptions = load<ReleaseOcrProcessOptions_t>("ReleaseOcrProcessOptions")) != nullptr;
        ok &= (ReleaseOcrResult = load<ReleaseOcrResult_t>("ReleaseOcrResult")) != nullptr;

        m_fullyLoaded = ok;
        if (!ok)
        {
            FreeLibrary(m_hOneOcr);
            FreeLibrary(m_hOnnxRuntime);
            m_hOneOcr = nullptr;
            m_hOnnxRuntime = nullptr;
        }
    }

    ~TextRecognizerOne()
    {
        if (m_hOneOcr)
        {
            FreeLibrary(m_hOneOcr);
        }

        if (m_hOnnxRuntime)
        {
            FreeLibrary(m_hOnnxRuntime);
        }
    }

    template<typename T>
    T load(const char* name)
    {
        return reinterpret_cast<T>(GetProcAddress(m_hOneOcr, name));
    }

    TextRecognizerOne(const TextRecognizerOne&) = delete;
    TextRecognizerOne& operator=(const TextRecognizerOne&) = delete;
};
