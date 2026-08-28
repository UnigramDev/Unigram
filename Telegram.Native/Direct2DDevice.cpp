#include "pch.h"
#include "Direct2DDevice.h"
#if __has_include("Direct2DDevice.g.cpp")
#include "Direct2DDevice.g.cpp"
#endif

#include "SVG/nanosvg.h"
#include "StringUtils.h"
#include "Helpers\COMHelper.h"
#include "Helpers\BlurHelper.h"

#include <zlib.h>

#include <format>
#include <numbers>

#include <src\webp\decode.h>
#include <src\webp\demux.h>

#include <shcore.h>
#include <propkey.h>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Graphics.Effects.h>
#include <winrt/Windows.UI.Xaml.Media.Imaging.h>
#include <winrt/Windows.Security.Cryptography.h>
#include <windows.ui.xaml.media.dxinterop.h>

#include <BufferSurface.h>

using namespace D2D1;
using namespace winrt::Windows::ApplicationModel;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::UI::Xaml::Media::Imaging;

namespace winrt::Telegram::Native::implementation
{
    class CustomEmojiInlineObject
        : public winrt::implements<CustomEmojiInlineObject, IDWriteInlineObject>
    {
        IFACEMETHODIMP2 Draw(
            _In_opt_ void* clientDrawingContext,
            _In_ IDWriteTextRenderer* renderer,
            FLOAT originX,
            FLOAT originY,
            BOOL isSideways,
            BOOL isRightToLeft,
            _In_opt_ IUnknown* clientDrawingEffect
        ) override
        {
            return S_OK;
        }

        IFACEMETHODIMP2 GetMetrics(_Out_ DWRITE_INLINE_OBJECT_METRICS* metrics) override
        {
            DWRITE_INLINE_OBJECT_METRICS inlineMetrics = {};
            inlineMetrics.width = 20;
            inlineMetrics.height = 20;
            inlineMetrics.baseline = 20;
            *metrics = inlineMetrics;
            return S_OK;
        }

        IFACEMETHODIMP2 GetOverhangMetrics(_Out_ DWRITE_OVERHANG_METRICS* overhangs) override
        {
            DWRITE_OVERHANG_METRICS inlineOverhangs = {};
            inlineOverhangs.left = 0;
            inlineOverhangs.top = -2;
            inlineOverhangs.right = 0;
            inlineOverhangs.bottom = -6;
            *overhangs = inlineOverhangs;
            return S_OK;
        }

        IFACEMETHODIMP2 GetBreakConditions(_Out_ DWRITE_BREAK_CONDITION* breakConditionBefore, _Out_ DWRITE_BREAK_CONDITION* breakConditionAfter) override
        {
            *breakConditionBefore = DWRITE_BREAK_CONDITION_CAN_BREAK;
            *breakConditionAfter = DWRITE_BREAK_CONDITION_MAY_NOT_BREAK;
            return S_OK;
        }
    };

    class CustomFontFileEnumerator
        : public winrt::implements<CustomFontFileEnumerator, IDWriteFontFileEnumerator>
    {
        winrt::com_ptr<IDWriteFactory> m_factory;
        std::vector<const wchar_t*> m_filenames;
        int32_t m_index;
        winrt::com_ptr<IDWriteFontFile> m_theFile;

    public:
        CustomFontFileEnumerator(IDWriteFactory* factory, void const* collectionKey, uint32_t collectionKeySize)
            : m_factory()
            , m_index(0)
        {
            auto keys = static_cast<const wchar_t* const*>(collectionKey);
            auto count = collectionKeySize / sizeof(const wchar_t*);

            m_filenames = std::vector<const wchar_t*>(keys, keys + count);
            m_factory.copy_from(factory);
        }

        IFACEMETHODIMP2 MoveNext(BOOL* hasCurrentFile) override
        {
            if (m_index == m_filenames.size())
            {
                *hasCurrentFile = FALSE;
            }
            else if (SUCCEEDED(m_factory->CreateFontFileReference(m_filenames[m_index++], nullptr, m_theFile.put())))
            {
                *hasCurrentFile = TRUE;
            }
            else
            {
                *hasCurrentFile = FALSE;
            }

            return S_OK;
        }

        IFACEMETHODIMP2 GetCurrentFontFile(IDWriteFontFile** fontFile) override
        {
            m_theFile.copy_to(fontFile);
            return S_OK;
        }
    };



    class CustomFontLoader
        : public winrt::implements<CustomFontLoader, IDWriteFontCollectionLoader>
    {
        // DWrite keeps its own copy of the collection key and hands it back to
        // CreateEnumeratorFromKey whenever it needs to rebuild the collection, so the strings the
        // key points at have to live as long as the loader is registered, not as long as the call
        // that creates the collection.
        hstring m_paths[2];
        const wchar_t* m_key[2];

    public:
        CustomFontLoader(hstring const& fontPath, hstring const& emojiPath)
            : m_paths{ fontPath, emojiPath }
            , m_key{ m_paths[0].c_str(), m_paths[1].c_str() }
        {
        }

        void const* Key() const noexcept
        {
            return m_key;
        }

        // Byte count, as DWrite expects: it copies collectionKeySize bytes out of the key.
        uint32_t KeySize() const noexcept
        {
            return static_cast<uint32_t>(sizeof(m_key));
        }

        IFACEMETHODIMP2 CreateEnumeratorFromKey(
            IDWriteFactory* factory,
            void const* collectionKey,
            uint32_t collectionKeySize,
            IDWriteFontFileEnumerator** fontFileEnumerator) override
        {
            return ExceptionBoundary(
                [=]
                {
                    auto enumerator = winrt::make_self<CustomFontFileEnumerator>(factory, collectionKey, collectionKeySize);
                    enumerator.as<IDWriteFontFileEnumerator>().copy_to(fontFileEnumerator);
                });
        }
    };

    IBuffer Direct2DDevice::DrawWebP(hstring fileName, int32_t maxWidth, int32_t& pixelWidth, int32_t& pixelHeight) noexcept
    {
        pixelWidth = 0;
        pixelHeight = 0;

        DWORD desired_access = GENERIC_READ;

        // TODO: share mode
        DWORD share_mode = FILE_SHARE_READ | FILE_SHARE_DELETE | FILE_SHARE_WRITE;

        DWORD creation_disposition = OPEN_ALWAYS;

        DWORD native_flags = FILE_FLAG_BACKUP_SEMANTICS;
        //if (flags & Direct) {
        //	native_flags |= FILE_FLAG_WRITE_THROUGH | FILE_FLAG_NO_BUFFERING;
        //}
        //if (flags & WinStat) {
        //	native_flags |= FILE_FLAG_BACKUP_SEMANTICS;
        //}
        CREATEFILE2_EXTENDED_PARAMETERS extended_parameters;
        std::memset(&extended_parameters, 0, sizeof(extended_parameters));
        extended_parameters.dwSize = sizeof(extended_parameters);
        extended_parameters.dwFileAttributes = FILE_ATTRIBUTE_NORMAL;
        extended_parameters.dwFileFlags = native_flags;
        HANDLE handle = CreateFile2FromAppW(fileName.c_str(), desired_access, share_mode, creation_disposition, &extended_parameters);

        if (handle == INVALID_HANDLE_VALUE)
        {
            return nullptr;
        }

        LARGE_INTEGER pFileSize;
        if (!GetFileSizeEx(handle, &pFileSize))
        {
            CloseHandle(handle);
            return nullptr;
        }

        size_t length = static_cast<size_t>(pFileSize.QuadPart);
        char* buffer = (char*)malloc(length);

        DWORD numberOfBytesRead;
        if (!ReadFile(handle, buffer, length, &numberOfBytesRead, NULL))
        {
            CloseHandle(handle);
            return nullptr;
        }

        CloseHandle(handle);

        WebPData webPData;
        webPData.bytes = (uint8_t*)buffer;
        webPData.size = length;

        auto spDemuxer = std::unique_ptr<WebPDemuxer, decltype(&WebPDemuxDelete)>
        {
            WebPDemux(&webPData),
            WebPDemuxDelete
        };
        if (!spDemuxer)
        {
            //throw ref new InvalidArgumentException(ref new String(L"Failed to create demuxer"));
            free(buffer);
            return nullptr;
        }

        IBuffer surface;
        WebPIterator iter;
        if (WebPDemuxGetFrame(spDemuxer.get(), 1, &iter))
        {
            WebPDecoderConfig config;
            int ret = WebPInitDecoderConfig(&config);
            if (!ret)
            {
                //throw ref new FailureException(ref new String(L"WebPInitDecoderConfig failed"));
                free(buffer);
                return nullptr;
            }

            ret = (WebPGetFeatures(iter.fragment.bytes, iter.fragment.size, &config.input) == VP8_STATUS_OK);
            if (!ret)
            {
                //throw ref new FailureException(ref new String(L"WebPGetFeatures failed"));
                free(buffer);
                return nullptr;
            }

            int width = iter.width;
            int height = iter.height;

            if (iter.width > maxWidth || iter.height > maxWidth)
            {
                auto ratioX = (double)maxWidth / iter.width;
                auto ratioY = (double)maxWidth / iter.height;
                auto ratio = std::min(ratioX, ratioY);

                width = (int)(iter.width * ratio);
                height = (int)(iter.height * ratio);
            }

            pixelWidth = width;
            pixelHeight = height;

            surface = Telegram::Native::BufferSurface::Create(width * 4 * height);
            auto pixels = surface.data();
            //uint8_t* pixels = new uint8_t[(width * 4) * height];

            if (width != iter.width || height != iter.height)
            {
                config.options.scaled_width = width;
                config.options.scaled_height = height;
                config.options.use_scaling = 1;
                config.options.no_fancy_upsampling = 1;
            }

            config.output.colorspace = MODE_bgrA;
            config.output.is_external_memory = 1;
            config.output.u.RGBA.rgba = pixels;
            config.output.u.RGBA.stride = width * 4;
            config.output.u.RGBA.size = (width * 4) * height;

            ret = WebPDecode(iter.fragment.bytes, iter.fragment.size, &config);

            if (ret != VP8_STATUS_OK)
            {
                //throw ref new FailureException(ref new String(L"Failed to decode frame"));
                //delete[] pixels;

                free(buffer);
                return nullptr;
            }

            //delete[] pixels;

        }

        free(buffer);
        return surface;
    }

    bool Direct2DDevice::IsWebP(hstring fileName, int32_t& pixelWidth, int32_t& pixelHeight) noexcept
    {
        pixelWidth = 0;
        pixelHeight = 0;

        DWORD desired_access = GENERIC_READ;

        // TODO: share mode
        DWORD share_mode = FILE_SHARE_READ | FILE_SHARE_DELETE | FILE_SHARE_WRITE;

        DWORD creation_disposition = OPEN_ALWAYS;

        DWORD native_flags = FILE_FLAG_BACKUP_SEMANTICS;
        //if (flags & Direct) {
        //	native_flags |= FILE_FLAG_WRITE_THROUGH | FILE_FLAG_NO_BUFFERING;
        //}
        //if (flags & WinStat) {
        //	native_flags |= FILE_FLAG_BACKUP_SEMANTICS;
        //}
        CREATEFILE2_EXTENDED_PARAMETERS extended_parameters;
        std::memset(&extended_parameters, 0, sizeof(extended_parameters));
        extended_parameters.dwSize = sizeof(extended_parameters);
        extended_parameters.dwFileAttributes = FILE_ATTRIBUTE_NORMAL;
        extended_parameters.dwFileFlags = native_flags;
        HANDLE handle = CreateFile2FromAppW(fileName.c_str(), desired_access, share_mode, creation_disposition, &extended_parameters);

        if (handle == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        LARGE_INTEGER pFileSize;
        if (!GetFileSizeEx(handle, &pFileSize))
        {
            CloseHandle(handle);
            return false;
        }

        size_t length = static_cast<size_t>(pFileSize.QuadPart);
        char* buffer = (char*)malloc(length);

        DWORD numberOfBytesRead;
        if (!ReadFile(handle, buffer, length, &numberOfBytesRead, NULL))
        {
            CloseHandle(handle);
            return false;
        }

        CloseHandle(handle);

        WebPData webPData;
        webPData.bytes = (uint8_t*)buffer;
        webPData.size = length;

        auto spDemuxer = std::unique_ptr<WebPDemuxer, decltype(&WebPDemuxDelete)>
        {
            WebPDemux(&webPData),
            WebPDemuxDelete
        };
        if (!spDemuxer)
        {
            //throw ref new InvalidArgumentException(ref new String(L"Failed to create demuxer"));
            free(buffer);
            return false;
        }

        IBuffer surface;
        WebPIterator iter;
        if (WebPDemuxGetFrame(spDemuxer.get(), 1, &iter))
        {
            pixelWidth = iter.width;
            pixelHeight = iter.height;
        }

        free(buffer);
        return true;
    }

    winrt::Telegram::Native::SurfaceImage Direct2DDevice::Create(int32_t pixelWidth, int32_t pixelHeight)
    {
        std::lock_guard const guard(m_criticalSection);

        auto surface = winrt::make_self<SurfaceImage>(m_d2dDevice.get(), pixelWidth, pixelHeight);
        return surface.as<winrt::Telegram::Native::SurfaceImage>();
    }

    HRESULT Direct2DDevice::Invalidate(winrt::Telegram::Native::SurfaceImage imageSource, IBuffer buffer)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        com_ptr<SurfaceImage> source = imageSource.as<SurfaceImage>();
        int32_t pixelWidth = source->m_pixelWidth;
        int32_t pixelHeight = source->m_pixelHeight;
        winrt::com_ptr<ISurfaceImageSourceNativeWithD2D> native = source->m_native;

        D2D1_SIZE_U size{ pixelWidth, pixelHeight };
        D2D1_RECT_U rect{ 0, 0, pixelWidth, pixelHeight };
        RECT updateRect{ 0, 0, pixelWidth, pixelHeight };
        POINT offset{ 0, 0 };

        com_ptr<ID2D1DeviceContext> d2d1DeviceContext;
        result = native->BeginDraw(updateRect, __uuidof(ID2D1DeviceContext), d2d1DeviceContext.put_void(), &offset);

        if (result == DXGI_ERROR_DEVICE_REMOVED || result == DXGI_ERROR_DEVICE_RESET)
        {
            ReturnIfFailed(result, CreateDeviceResources());
            ReturnIfFailed(result, source->CreateDeviceResources(m_d2dDevice.get()));
            return Invalidate(imageSource, buffer);
        }

        com_ptr<ID2D1Bitmap1> bitmap;
        D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_NONE, 0 };
        CleanupIfFailed(result, d2d1DeviceContext->CreateBitmap(size, buffer.data(), pixelWidth * 4, &properties, bitmap.put()));

        d2d1DeviceContext->SetTransform(D2D1::Matrix3x2F::Translation(offset.x, offset.y));
        d2d1DeviceContext->Clear(D2D1::ColorF(0, 0, 0, 0));
        d2d1DeviceContext->DrawBitmap(bitmap.get());

    Cleanup:
        return native->EndDraw();
    }

    winrt::Windows::Foundation::IAsyncOperation<ChatBackgroundPattern> Direct2DDevice::DrawSvgAsync(Compositor compositor, hstring path, float intensity, bool negative, double rasterizationScale)
    {
        winrt::apartment_context ui_thread;
        co_await winrt::resume_background();

        ChatBackgroundPattern pattern{ nullptr };
        try
        {
            pattern = DrawSvg(compositor, path, intensity, negative, rasterizationScale);
        }
        catch (...)
        {
            pattern = nullptr;
        }

        co_await ui_thread;
        co_return pattern;
    }

    constexpr float PI = 3.14159265358979323846f;

    inline static ChatBackgroundSymbol ParseGiftPattern(float topLeftX, float topLeftY, float topRightX, float topRightY, float bottomRightX, float bottomRightY, float bottomLeftX, float bottomLeftY)
    {
        ChatBackgroundSymbol pattern;
        pattern.Offset = float2(topLeftX, topLeftY);

        float dx_top = topRightX - topLeftX;
        float dy_top = topRightY - topLeftY;
        pattern.RotationAngle = atan2(dy_top, dx_top);

        float dx_left = bottomLeftX - topLeftX;
        float dy_left = bottomLeftY - topLeftY;
        float width = sqrt(dx_top * dx_top + dy_top * dy_top);
        float height = sqrt(dx_left * dx_left + dy_left * dy_left);
        pattern.Size = float2(width, height);

        return pattern;
    }

    inline static bool IsGzipCompressed(const char* data, size_t length)
    {
        if (length < 10) return false;
        return (static_cast<unsigned char>(data[0]) == 0x1f &&
            static_cast<unsigned char>(data[1]) == 0x8b);
    }

    inline static std::string DecompressFromFile(hstring path)
    {
        FILE* file;
        _wfopen_s(&file, path.c_str(), L"rb");
        if (file == NULL)
        {
            return "";
        }

        fseek(file, 0, SEEK_END);
        size_t length = ftell(file);
        fseek(file, 0, SEEK_SET);

        if (length == 0)
        {
            fclose(file);
            return "";
        }

        char* buffer = (char*)malloc(length);
        if (!buffer)
        {
            fclose(file);
            return "";
        }

        fread(buffer, 1, length, file);
        fclose(file);

        if (!IsGzipCompressed(buffer, length))
        {
            // Construct the string *before* freeing the buffer; the previous order read freed memory.
            std::string raw(buffer, length);
            free(buffer);
            return raw;
        }

        z_stream stream = {};

        if (inflateInit2(&stream, 15 + 16) != Z_OK)
        {
            free(buffer);
            return "";
        }

        stream.next_in = reinterpret_cast<Bytef*>(buffer);
        stream.avail_in = static_cast<uInt>(length);

        std::string decompressed;
        // Pre-size to avoid the append/realloc cascade that fragments the heap (gzip on these is ~3-5x).
        decompressed.reserve(length * 4);

        // One reusable stack buffer instead of allocating a vector on every inflate iteration.
        char chunk[32768];

        int ret;
        do
        {
            stream.next_out = reinterpret_cast<Bytef*>(chunk);
            stream.avail_out = static_cast<uInt>(sizeof(chunk));

            ret = inflate(&stream, Z_NO_FLUSH);

            if (ret != Z_OK && ret != Z_STREAM_END)
            {
                inflateEnd(&stream);
                free(buffer);
                return "";
            }

            decompressed.append(chunk, sizeof(chunk) - stream.avail_out);

        } while (ret != Z_STREAM_END);

        inflateEnd(&stream);
        free(buffer);
        return decompressed;
    }

    // Returns decompressed SVG bytes for the given file, caching results in a small LRU so repeated
    // background re-renders don't re-read + gunzip the same file (which churns/fragments the heap).
    // Must be called while holding m_criticalSection. nsvgParse mutates its input in place, so callers
    // must copy the returned bytes into a local buffer before parsing.
    const std::string& Direct2DDevice::GetDecompressedSvg(hstring const& path)
    {
        std::wstring key(path.c_str());

        auto found = m_svgCacheIndex.find(key);
        if (found != m_svgCacheIndex.end())
        {
            // Promote to most-recently-used.
            m_svgCacheList.splice(m_svgCacheList.begin(), m_svgCacheList, found->second);
            return found->second->second;
        }

        auto decompressed = DecompressFromFile(path);
        if (decompressed.empty())
        {
            // Don't cache failures (e.g. file not yet downloaded) so a later retry can re-read.
            static const std::string empty;
            return empty;
        }

        m_svgCacheList.emplace_front(key, std::move(decompressed));
        m_svgCacheIndex[key] = m_svgCacheList.begin();

        if (m_svgCacheList.size() > kSvgCacheCapacity)
        {
            m_svgCacheIndex.erase(m_svgCacheList.back().first);
            m_svgCacheList.pop_back();
        }

        return m_svgCacheList.front().second;
    }

    ChatBackgroundPattern Direct2DDevice::DrawSvg(Compositor compositor, hstring path, float intensity, bool negative, double rasterizationScale)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        if (rasterizationScale < 1)
        {
            rasterizationScale = 1;
        }
        else if (rasterizationScale > 4)
        {
            rasterizationScale = 4;
        }

        auto scale = (int)(rasterizationScale * 100);
        float rasterScale = (float)rasterizationScale;
        float dpi = 0.25f * rasterScale;

        // nsvgParse mutates its input buffer in place, so parse a copy of the cached bytes.
        std::string data(GetDecompressedSvg(path));
        auto patterns = winrt::single_threaded_vector<ChatBackgroundSymbol>();

        struct NSVGimage* image;
        image = nsvgParse((char*)data.c_str(), "px", 96);

        auto unique = std::shared_ptr<NSVGimage>(image, [](NSVGimage* p)
            {
                nsvgDelete(p);
            });

        auto imageWidth = image->width;
        auto imageHeight = image->height;

        winrt::com_ptr<ID2D1SolidColorBrush> blackBrush;

        winrt::com_ptr<abi::ICompositionGraphicsDevice> deviceInterop;
        CompositionGraphicsDevice device{ nullptr };
        CompositionDrawingSurface surface{ nullptr };
        winrt::com_ptr<abi::ICompositionDrawingSurfaceInterop> surfaceInterop;
        winrt::Windows::Foundation::Size imageSize(imageWidth * dpi, imageHeight * dpi);

        winrt::com_ptr<ID2D1DeviceContext> d2dContext;
        POINT offset;

        auto compositorInterop = compositor.as<abi::ICompositorInterop>();
        CleanupIfFailed(result, compositorInterop->CreateGraphicsDevice(m_d2dDevice.get(), deviceInterop.put()));

        device = deviceInterop.as<CompositionGraphicsDevice>();
        surface = device.CreateDrawingSurface(imageSize, DirectXPixelFormat::B8G8R8A8UIntNormalized, DirectXAlphaMode::Premultiplied);
        surfaceInterop = surface.as<abi::ICompositionDrawingSurfaceInterop>();

        // TODO: BeginDraw can return DXGI_ERROR_DEVICE_REMOVED, but it shouldn't be possible
        // Because we always create a new composition graphics device (not great ndr, but we must use background instance not to block messages measure)
        // And we handle device loss right before this method is invoked.
        CleanupIfFailed(result, surfaceInterop->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), d2dContext.put_void(), &offset));

        if (negative)
        {
            d2dContext->Clear(D2D1::ColorF(0, 0, 0, 1));
            d2dContext->SetPrimitiveBlend(D2D1_PRIMITIVE_BLEND_COPY);

            CleanupIfFailed(result, d2dContext->CreateSolidColorBrush(D2D1::ColorF(0, 0, 0, 1 - intensity), blackBrush.put()));
        }
        else
        {
            d2dContext->Clear(D2D1::ColorF(0, 0, 0, 0));

            CleanupIfFailed(result, d2dContext->CreateSolidColorBrush(D2D1::ColorF(0, 0, 0, intensity), blackBrush.put()));
        }

        d2dContext->SetTransform(D2D1::Matrix3x2F::Scale(1 * dpi, 1 * dpi));

        for (auto shape = image->shapes; shape != NULL; shape = shape->next)
        {
            if (!(shape->flags & NSVG_FLAGS_VISIBLE) || (shape->fill.type == NSVG_PAINT_NONE && shape->stroke.type == NSVG_PAINT_NONE))
            {
                continue;
            }

            if (strcmp(shape->id, "GiftPatterns") == 0)
            {
                if (shape->paths && shape->paths->npts == 13)
                {
                    auto topLeftX = shape->paths->pts[0] * (1 * dpi);
                    auto topLeftY = shape->paths->pts[1] * (1 * dpi);
                    auto topRightX = shape->paths->pts[6] * (1 * dpi);
                    auto topRightY = shape->paths->pts[7] * (1 * dpi);
                    auto bottomRightX = shape->paths->pts[12] * (1 * dpi);
                    auto bottomRightY = shape->paths->pts[13] * (1 * dpi);
                    auto bottomLeftX = shape->paths->pts[18] * (1 * dpi);
                    auto bottomLeftY = shape->paths->pts[19] * (1 * dpi);

                    patterns.Append(ParseGiftPattern(topLeftX, topLeftY, topRightX, topRightY, bottomRightX, bottomRightY, bottomLeftX, bottomLeftY));
                }

                continue;
            }

            blackBrush->SetOpacity(shape->opacity);

            winrt::com_ptr<ID2D1PathGeometry1> geometry;
            CleanupIfFailed(result, m_d2dFactory->CreatePathGeometry(geometry.put()));

            winrt::com_ptr<ID2D1GeometrySink> sink;
            CleanupIfFailed(result, geometry->Open(sink.put()));

            for (NSVGpath* path = shape->paths; path != NULL; path = path->next)
            {
                sink->BeginFigure({ path->pts[0], path->pts[1] }, D2D1_FIGURE_BEGIN_FILLED);

                for (int i = 0; i < path->npts - 1; i += 3)
                {
                    float* p = &path->pts[i * 2];
                    sink->AddBezier({ { p[2], p[3] }, { p[4], p[5] }, { p[6], p[7] } });
                }

                sink->EndFigure(path->closed ? D2D1_FIGURE_END_CLOSED : D2D1_FIGURE_END_OPEN);
            }

            CleanupIfFailed(result, sink->Close());

            if (shape->fill.type != NSVG_PAINT_NONE)
            {
                switch (shape->fillRule)
                {
                case NSVG_FILLRULE_EVENODD:
                    sink->SetFillMode(D2D1_FILL_MODE_ALTERNATE);
                    break;
                default:
                    sink->SetFillMode(D2D1_FILL_MODE_WINDING);
                    break;
                }

                winrt::com_ptr<ID2D1PathGeometry1> widenGeometry;
                CleanupIfFailed(result, m_d2dFactory->CreatePathGeometry(widenGeometry.put()));

                winrt::com_ptr<ID2D1GeometrySink> widenSink;
                CleanupIfFailed(result, widenGeometry->Open(widenSink.put()));

                geometry->Widen(0.25f * rasterizationScale / dpi, NULL, NULL, widenSink.get());
                widenSink->Close();

                d2dContext->FillGeometry(widenGeometry.get(), blackBrush.get());
                d2dContext->FillGeometry(geometry.get(), blackBrush.get());
            }

            if (shape->stroke.type != NSVG_PAINT_NONE)
            {
                D2D1_STROKE_STYLE_PROPERTIES1 strokeProperties{};
                strokeProperties.miterLimit = shape->miterLimit;

                switch (shape->strokeLineCap)
                {
                case NSVG_CAP_BUTT:
                    strokeProperties.startCap = strokeProperties.endCap = D2D1_CAP_STYLE_FLAT;
                    break;
                case NSVG_CAP_ROUND:
                    strokeProperties.startCap = strokeProperties.endCap = D2D1_CAP_STYLE_ROUND;
                    break;
                case NSVG_CAP_SQUARE:
                    strokeProperties.startCap = strokeProperties.endCap = D2D1_CAP_STYLE_SQUARE;
                    break;
                default:
                    break;
                }

                switch (shape->strokeLineJoin)
                {
                case NSVG_JOIN_BEVEL:
                    strokeProperties.lineJoin = D2D1_LINE_JOIN_BEVEL;
                    break;
                case NSVG_JOIN_MITER:
                    strokeProperties.lineJoin = D2D1_LINE_JOIN_MITER;
                    break;
                case NSVG_JOIN_ROUND:
                    strokeProperties.lineJoin = D2D1_LINE_JOIN_ROUND;
                    break;
                default:
                    break;
                }

                winrt::com_ptr<ID2D1StrokeStyle1> strokeStyle;
                CleanupIfFailed(result, m_d2dFactory->CreateStrokeStyle(strokeProperties, NULL, 0, strokeStyle.put()));

                auto strokeWidth = std::max(1 * rasterScale / dpi, shape->strokeWidth);

                d2dContext->DrawGeometry(geometry.get(), blackBrush.get(), strokeWidth, strokeStyle.get());
            }
        }

        d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());

        CleanupIfFailed(result, surfaceInterop->EndDraw());

        return ChatBackgroundPattern(surface, imageWidth, imageHeight, rasterizationScale, patterns);

    Cleanup:
        return nullptr;
    }

    SoftwareBitmap Direct2DDevice::DrawBlurred(hstring fileName, float blurAmount)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        HANDLE file = CreateFile2FromAppW(fileName.data(), GENERIC_READ, FILE_SHARE_READ, OPEN_EXISTING, nullptr);

        if (file == INVALID_HANDLE_VALUE)
        {
            return nullptr;
        }

        winrt::com_ptr<IWICBitmapDecoder> wicBitmapDecoder;
        winrt::com_ptr<IWICBitmapFrameDecode> wicFrameDecode;
        winrt::com_ptr<IWICFormatConverter> wicFormatConverter;
        SoftwareBitmap bitmap{ nullptr };

        CleanupIfFailed(result, m_wicFactory->CreateDecoderFromFileHandle(reinterpret_cast<ULONG_PTR>(file), nullptr, WICDecodeMetadataCacheOnDemand, wicBitmapDecoder.put()));

        CleanupIfFailed(result, wicBitmapDecoder->GetFrame(0, wicFrameDecode.put()));

        CleanupIfFailed(result, m_wicFactory->CreateFormatConverter(wicFormatConverter.put()));
        CleanupIfFailed(result, wicFormatConverter->Initialize(wicFrameDecode.get(), GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, nullptr, 0.f, WICBitmapPaletteTypeCustom));

        CleanupIfFailed(result, DrawBlurredImpl(wicFormatConverter.get(), blurAmount, bitmap, false));

    Cleanup:
        CloseHandle(file);

        return bitmap;
    }

    SoftwareBitmap Direct2DDevice::DrawBlurred(array_view<uint8_t const> bytes, float blurAmount)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        winrt::com_ptr<IStream> stream;
        auto bytesView = std::vector<byte>(bytes.begin(), bytes.end());

        winrt::com_ptr<IWICBitmapDecoder> wicBitmapDecoder;
        winrt::com_ptr<IWICBitmapFrameDecode> wicFrameDecode;
        winrt::com_ptr<IWICFormatConverter> wicFormatConverter;
        SoftwareBitmap bitmap{ nullptr };

        CleanupIfFailed(result, CreateStreamOnHGlobal(nullptr, TRUE, stream.put()));

        CleanupIfFailed(result, stream->Write(bytesView.data(), bytesView.size(), nullptr));
        CleanupIfFailed(result, stream->Seek({ 0 }, STREAM_SEEK_SET, nullptr));

        CleanupIfFailed(result, m_wicFactory->CreateDecoderFromStream(stream.get(), nullptr, WICDecodeMetadataCacheOnDemand, wicBitmapDecoder.put()));

        CleanupIfFailed(result, wicBitmapDecoder->GetFrame(0, wicFrameDecode.put()));

        CleanupIfFailed(result, m_wicFactory->CreateFormatConverter(wicFormatConverter.put()));
        CleanupIfFailed(result, wicFormatConverter->Initialize(wicFrameDecode.get(), GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, nullptr, 0.f, WICBitmapPaletteTypeCustom));

        CleanupIfFailed(result, DrawBlurredImpl(wicFormatConverter.get(), blurAmount, bitmap, true));

    Cleanup:
        return bitmap;
    }

    HRESULT Direct2DDevice::DrawBlurredImpl(IWICBitmapSource* wicBitmapSource, float blurAmount, SoftwareBitmap& bitmap, bool minithumbnail)
    {
        HRESULT result;
        winrt::com_ptr<ID2D1ImageSourceFromWic> imageSource;
        ReturnIfFailed(result, m_d2dContext->CreateImageSourceFromWic(wicBitmapSource, imageSource.put()));

        D2D1_SIZE_U size;
        ReturnIfFailed(result, wicBitmapSource->GetSize(&size.width, &size.height));

        uint32_t totalPixels = size.width * size.height;
        // Disabled for now
        if (false && ((totalPixels <= 400 * 400 && blurAmount == 3) || (totalPixels <= 150 * 150 && blurAmount == 15)))
        {
            UINT bytesPerPixel = 4;
            UINT stride = size.width * bytesPerPixel;
            UINT bufferSize = stride * size.height;

            bitmap = SoftwareBitmap(BitmapPixelFormat::Bgra8, size.width, size.height, BitmapAlphaMode::Premultiplied);
            auto buffer = bitmap.LockBuffer(BitmapBufferAccessMode::Write);
            auto reference = buffer.CreateReference();
            auto pixels = reference.data();

            WICRect rect = { 0, 0, static_cast<INT>(size.width), static_cast<INT>(size.height) };
            ReturnIfFailed(result, wicBitmapSource->CopyPixels(&rect, stride, bufferSize, pixels));

            if (blurAmount == 3)
            {
                if (totalPixels <= 100 * 100)
                {
                    FixedRadius3Blur::ApplyBlur(pixels, size.width, size.height);
                }
                else
                {
                    FixedRadius3BoxBlur::ApplyFastBlur(pixels, size.width, size.height);
                }
            }
            else if (totalPixels <= 50 * 50)
            {
                FixedRadius15Blur::ApplyBlur(pixels, size.width, size.height);
            }
            else
            {
                FixedRadius15BoxBlur::ApplyFastBlur(pixels, size.width, size.height);
            }

            return S_OK;
        }

        winrt::com_ptr<ID2D1Bitmap1> targetBitmap;
        D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
        ReturnIfFailed(result, m_d2dContext->CreateBitmap(size, nullptr, 0, &properties, targetBitmap.put()));

        ReturnIfFailed(result, m_gaussianBlurEffect->SetValue(D2D1_GAUSSIANBLUR_PROP_STANDARD_DEVIATION, blurAmount));

        m_gaussianBlurEffect->SetInput(0, imageSource.get());

        m_d2dContext->SetTarget(targetBitmap.get());
        m_d2dContext->BeginDraw();
        //m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
        m_d2dContext->Clear(D2D1::ColorF(ColorF::Black, 0.0f));
        m_d2dContext->DrawImage(m_gaussianBlurEffect.get());

        if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
        {
            ReturnIfFailed(result, CreateDeviceResources());
            return DrawBlurredImpl(wicBitmapSource, blurAmount, bitmap, minithumbnail);
        }

        //winrt::com_ptr<IDXGISurface> surface;
        //ReturnIfFailed(result, targetBitmap->GetSurface(surface.put()));

        //winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface direct3DSurface{ nullptr };
        //ReturnIfFailed(result, CreateDirect3D11SurfaceFromDXGISurface(surface.get(), reinterpret_cast<::IInspectable**>(winrt::put_abi(direct3DSurface))));

        //bitmap = SoftwareBitmap::CreateCopyFromSurfaceAsync(direct3DSurface, BitmapAlphaMode::Premultiplied).get();
        //return result;

        winrt::com_ptr<ID2D1Bitmap1> readBitmap;
        D2D1_BITMAP_PROPERTIES1 properties2 = { { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_CPU_READ | D2D1_BITMAP_OPTIONS_CANNOT_DRAW, 0 };
        ReturnIfFailed(result, m_d2dContext->CreateBitmap(size, nullptr, 0, &properties2, readBitmap.put()));

        D2D1_POINT_2U origin{ 0, 0 };
        D2D1_RECT_U source{ 0, 0, size.width, size.height };
        D2D1_MAPPED_RECT map;
        ReturnIfFailed(result, readBitmap->CopyFromBitmap(&origin, targetBitmap.get(), &source));
        ReturnIfFailed(result, readBitmap->Map(D2D1_MAP_OPTIONS_READ, &map));

        // Fast path
        uint32_t rowSizeBytes = size.width * 4;
        if (map.pitch == rowSizeBytes)
        {
            uint32_t bufferSize = map.pitch * size.height;
            winrt::array_view<const uint8_t> pixelData(
                static_cast<const uint8_t*>(map.bits),
                static_cast<const uint8_t*>(map.bits) + bufferSize
            );

            // BufferSurface here also works
            auto buffer = winrt::Windows::Security::Cryptography::CryptographicBuffer::CreateFromByteArray(pixelData);
            bitmap = SoftwareBitmap::CreateCopyFromBuffer(buffer, BitmapPixelFormat::Bgra8, size.width, size.height, BitmapAlphaMode::Premultiplied);
        }
        else
        {
            bitmap = SoftwareBitmap(BitmapPixelFormat::Bgra8, size.width, size.height, BitmapAlphaMode::Premultiplied);
            auto buffer = bitmap.LockBuffer(BitmapBufferAccessMode::Write);
            auto reference = buffer.CreateReference();

            const uint8_t* srcRow = static_cast<const uint8_t*>(map.bits);
            uint8_t* dstRow = reference.data();

            for (uint32_t y = 0; y < size.height; ++y)
            {
                memcpy(dstRow, srcRow, rowSizeBytes);
                srcRow += map.pitch;
                dstRow += rowSizeBytes;
            }
        }

        return readBitmap->Unmap();
    }

    // The compositor is per thread, and null for the background instance, which draws into
    // software bitmaps and composes nothing.
    Direct2DDevice::Direct2DDevice(Compositor compositor)
        : m_compositor(compositor)
        , m_compositionDevice(nullptr)
        , m_alphaMaskFactory(nullptr)
    {
        winrt::check_hresult(CreateDeviceIndependentResources());
        winrt::check_hresult(CreateDeviceResources());
    }

    HRESULT Direct2DDevice::CreateDeviceIndependentResources()
    {
        if (m_compositor)
        {
            auto alphaMask = winrt::make_self<CompositionAlphaMaskEffect>();
            alphaMask->Name(L"AlphaMask");
            alphaMask->Source(CompositionEffectSourceParameter(L"source"));
            alphaMask->AlphaMask(CompositionEffectSourceParameter(L"mask"));

            m_alphaMaskFactory = m_compositor.CreateEffectFactory(alphaMask.as<IGraphicsEffect>());
        }

        HRESULT result;
        D2D1_FACTORY_OPTIONS options = {};
        ReturnIfFailed(result, D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, __uuidof(ID2D1Factory1), &options, m_d2dFactory.put_void()));
        ReturnIfFailed(result, CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&m_wicFactory)));
        ReturnIfFailed(result, DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), (IUnknown**)m_dwriteFactory.put()));

        m_customEmoji = winrt::make_self<CustomEmojiInlineObject>();

        hstring path = Package::Current().InstalledLocation().Path();

        auto loader = winrt::make_self<CustomFontLoader>(path + L"\\Assets\\Fonts\\Telegram.ttf", path + L"\\Assets\\Emoji\\apple.ttf");
        m_customLoader = loader.as<IDWriteFontCollectionLoader>();

        ReturnIfFailed(result, m_dwriteFactory->RegisterFontCollectionLoader(m_customLoader.get()));
        ReturnIfFailed(result, m_dwriteFactory->CreateCustomFontCollection(m_customLoader.get(), loader->Key(), loader->KeySize(), m_fontCollection.put()));
        ReturnIfFailed(result, m_dwriteFactory->GetSystemFontCollection(m_systemCollection.put()));

        for (auto familyName : { L"Cascadia Mono", L"Consolas" })
        {
            UINT32 index;
            BOOL exists = FALSE;
            if (SUCCEEDED(m_systemCollection->FindFamilyName(familyName, &index, &exists)) && exists)
            {
                m_monospaceFamily = familyName;
                break;
            }
        }

        return S_OK;
    }

    HRESULT Direct2DDevice::CreateDeviceResources()
    {
        HRESULT result;
        UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

        // The removed device has to go before a new one is created: com_ptr::put() overwrites the
        // pointer without releasing it, so recreating over a live device strands it and everything
        // the display driver holds behind it.
        m_d3dDevice = nullptr;

        winrt::com_ptr<ID3D11DeviceContext> context;
        if (FAILED(D3D11CreateDevice(nullptr,               // specify null to use the default adapter
            D3D_DRIVER_TYPE_HARDWARE, 0,
            creationFlags,									// optionally set debug and Direct2D compatibility flags
            NULL,											// list of feature levels this app can support
            0,												// number of possible feature levels
            D3D11_SDK_VERSION,
            m_d3dDevice.put(),								// returns the Direct3D device created
            &m_featureLevel,								// returns feature level of device created
            context.put()									// returns the device immediate context
        )))
        {
            // A failed D3D11CreateDevice can still have written the out parameters, and put()
            // would overwrite them.
            m_d3dDevice = nullptr;
            context = nullptr;

            // Try again using WARP (software rendering)
            ReturnIfFailed(result, D3D11CreateDevice(nullptr,
                D3D_DRIVER_TYPE_WARP, 0,
                creationFlags,
                NULL,
                0,
                D3D11_SDK_VERSION,
                m_d3dDevice.put(),
                &m_featureLevel,
                context.put()
            ));
        }

        winrt::com_ptr<IDXGIDevice> dxgiDevice = m_d3dDevice.as<IDXGIDevice>();
        ReturnIfFailed(result, m_d2dFactory->CreateDevice(dxgiDevice.get(), m_d2dDevice.put()));

        winrt::com_ptr<ID2D1DeviceContext> d2dContext;
        ReturnIfFailed(result, m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, d2dContext.put()));
        m_d2dContext = d2dContext.as<ID2D1DeviceContext2>();

        ReturnIfFailed(result, m_d2dContext->CreateEffect(CLSID_D2D1GaussianBlur, m_gaussianBlurEffect.put()));
        ReturnIfFailed(result, m_gaussianBlurEffect->SetValue(D2D1_GAUSSIANBLUR_PROP_BORDER_MODE, D2D1_BORDER_MODE_HARD));

        m_d2dContext->SetAntialiasMode(D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);

        ReturnIfFailed(result, m_wicFactory->CreateImageEncoder(m_d2dDevice.get(), m_imageEncoder.put()));

        if (m_compositor)
        {
            // If the composition device already exists, invalidate the rendering device
            if (m_compositionDevice)
            {
                winrt::com_ptr<abi::ICompositionGraphicsDeviceInterop> compositionGraphicsDeviceInterop{ m_compositionDevice.as<abi::ICompositionGraphicsDeviceInterop>() };
                result = compositionGraphicsDeviceInterop->SetRenderingDevice(m_d2dDevice.get());
            }
            else
            {
                auto compositorInterop = m_compositor.as<abi::ICompositorInterop>();
                winrt::com_ptr<abi::ICompositionGraphicsDevice> deviceInterop;
                ReturnIfFailed(result, compositorInterop->CreateGraphicsDevice(m_d2dDevice.get(), deviceInterop.put()));

                m_compositionDevice = deviceInterop.as<CompositionGraphicsDevice>();
            }
        }

        m_deviceLostHelper.WatchDevice(dxgiDevice);
        m_deviceLostHelper.DeviceLost({ this, &Direct2DDevice::OnDirect3DDeviceLost });

        return S_OK;
    }

    void Direct2DDevice::OnDirect3DDeviceLost(DeviceLostHelper const* /* sender */, DeviceLostEventArgs const& /* args */)
    {
        // Deliberately does not recreate the device here. This runs on the threadpool callback for
        // the device-removed event, so the display driver is still tearing itself down, and calling
        // D3D11CreateDevice at that moment faults inside the vendor user-mode driver.
        //
        // Direct2D asks HandleDeviceLost() on every access to the singleton, so the device
        // is rebuilt from the UI thread the next time it is actually needed.
        LOGGER_ERROR(L"Direct3D device lost");
    }

    HRESULT Direct2DDevice::CreateTextFormat(double fontSize)
    {
        if (m_appleFormat != nullptr && fontSize == m_appleFormat->GetFontSize())
        {
            return S_OK;
        }

        HRESULT result;
        ReturnIfFailed(result, m_dwriteFactory->CreateTextFormat(
            L"Segoe UI Emoji",						// font family name
            m_fontCollection.get(),			        // system font collection
            DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
            DWRITE_FONT_STYLE_NORMAL,				// font style
            DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
            fontSize,								// font size
            L"",									// locale name
            m_appleFormat.put()
        ));
        ReturnIfFailed(result, m_appleFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
        ReturnIfFailed(result, m_appleFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
        return result;
    }

    winrt::Telegram::Native::TextFormat Direct2DDevice::CreateTextFormat2(hstring text, IVector<TextStylePart> entities, double fontSize, double width)
    {
        winrt::com_ptr<TextFormat> textFormat;
        CreateTextFormatImpl(text, entities, fontSize, width, textFormat);
        return textFormat.as<winrt::Telegram::Native::TextFormat>();
    }

    HRESULT Direct2DDevice::CreateTextFormatImpl(hstring text, IVector<TextStylePart> entities, double fontSize, double width, winrt::com_ptr<TextFormat>& textFormat2)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        //ReturnIfFailed(result, CreateTextFormat(fontSize));

        winrt::com_ptr<IDWriteTextFormat> textFormat;
        ReturnIfFailed(result, m_dwriteFactory->CreateTextFormat(
            L"Segoe UI Emoji",						// font family name
            m_fontCollection.get(),			        // system font collection
            DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
            DWRITE_FONT_STYLE_NORMAL,				// font style
            DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
            fontSize,								// font size
            L"",									// locale name
            textFormat.put()
        ));
        ReturnIfFailed(result, textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
        ReturnIfFailed(result, textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));

        winrt::com_ptr<IDWriteTextLayout> textLayout;
        ReturnIfFailed(result, m_dwriteFactory->CreateTextLayout(
            text.data(),					// The string to be laid out and formatted.
            text.size(),        			// The length of the string.
            textFormat.get(),			    // The text format to apply to the string (contains font information, etc).
            width,							// The width of the layout box.
            INFINITY,						// The height of the layout box.
            textLayout.put()				// The IDWriteTextLayout interface pointer.
        ));

        for (const TextStylePart& entity : entities)
        {
            UINT32 startPosition = entity.Offset;
            UINT32 length = entity.Length;

            if (entity.Type == TextStyle::Bold)
            {
                ReturnIfFailed(result, textLayout->SetFontWeight(DWRITE_FONT_WEIGHT_SEMI_BOLD, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Italic)
            {
                ReturnIfFailed(result, textLayout->SetFontStyle(DWRITE_FONT_STYLE_ITALIC, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Strikethrough)
            {
                ReturnIfFailed(result, textLayout->SetStrikethrough(TRUE, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Underline)
            {
                ReturnIfFailed(result, textLayout->SetUnderline(TRUE, { startPosition, length }));
            }
            //else if (name == winrt::name_of<TextStylePartTypeCustomEmoji>())
            //{
            //    textLayout->SetInlineObject(m_customEmoji.get(), { startPosition, length });
            //}
            else if (entity.Type == TextStyle::Monospace)
            {
                ReturnIfFailed(result, textLayout->SetFontCollection(m_systemCollection.get(), { startPosition, length }));
                ReturnIfFailed(result, textLayout->SetFontFamilyName(m_monospaceFamily, { startPosition, length }));
            }
        }

        textFormat2 = winrt::make_self<TextFormat>(textLayout, text.size(), fontSize, width);
        return result;
    }

    float2 Direct2DDevice::ContentEnd(hstring text, IVector<TextStylePart> entities, double fontSize, double width)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        //ReturnIfFailed(result, CreateTextFormat(fontSize));

        winrt::com_ptr<IDWriteTextFormat> textFormat;
        ReturnDefaultIfFailed(result, m_dwriteFactory->CreateTextFormat(
            L"Segoe UI Emoji",						// font family name
            m_fontCollection.get(),			        // system font collection
            DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
            DWRITE_FONT_STYLE_NORMAL,				// font style
            DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
            fontSize,								// font size
            L"",									// locale name
            textFormat.put()
        ));
        ReturnDefaultIfFailed(result, textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
        ReturnDefaultIfFailed(result, textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
        ReturnDefaultIfFailed(result, textFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_EMERGENCY_BREAK));

        winrt::com_ptr<IDWriteTextLayout> textLayout;
        ReturnDefaultIfFailed(result, m_dwriteFactory->CreateTextLayout(
            text.data(),					// The string to be laid out and formatted.
            text.size(),        			// The length of the string.
            textFormat.get(),			    // The text format to apply to the string (contains font information, etc).
            width,							// The width of the layout box.
            INFINITY,						// The height of the layout box.
            textLayout.put()				// The IDWriteTextLayout interface pointer.
        ));

        for (const TextStylePart& entity : entities)
        {
            UINT32 startPosition = entity.Offset;
            UINT32 length = entity.Length;

            if (entity.Type == TextStyle::Bold)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontWeight(DWRITE_FONT_WEIGHT_SEMI_BOLD, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Italic)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontStyle(DWRITE_FONT_STYLE_ITALIC, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Strikethrough)
            {
                ReturnDefaultIfFailed(result, textLayout->SetStrikethrough(TRUE, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Underline)
            {
                ReturnDefaultIfFailed(result, textLayout->SetUnderline(TRUE, { startPosition, length }));
            }
            //else if (name == winrt::name_of<TextStylePartTypeCustomEmoji>())
            //{
            //    textLayout->SetInlineObject(m_customEmoji.get(), { startPosition, length });
            //}
            else if (entity.Type == TextStyle::Monospace)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontCollection(m_systemCollection.get(), { startPosition, length }));
                ReturnDefaultIfFailed(result, textLayout->SetFontFamilyName(m_monospaceFamily, { startPosition, length }));
            }
        }

        DWRITE_TEXT_METRICS metrics;
        ReturnDefaultIfFailed(result, textLayout->GetMetrics(&metrics));

        BOOL isTrailingHit;
        BOOL isInside;
        DWRITE_HIT_TEST_METRICS hitTestMetrics;
        ReturnDefaultIfFailed(result, textLayout->HitTestPoint(metrics.width, metrics.height, &isTrailingHit, &isInside, &hitTestMetrics));

        return float2(hitTestMetrics.left + hitTestMetrics.width, hitTestMetrics.top + hitTestMetrics.height);
    }

    IVector<Windows::Foundation::Rect> Direct2DDevice::LineMetrics(hstring text, IVector<TextStylePart> entities, double fontSize, double width, bool rtl)
    {
        return RangeMetrics(text, 0, text.size(), entities, fontSize, width, rtl, true);
    }

    IVector<Windows::Foundation::Rect> Direct2DDevice::RangeMetrics(hstring text, int32_t offset, int32_t length, IVector<TextStylePart> entities, double fontSize, double width, bool rtl, bool wrap)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        //ReturnIfFailed(result, CreateTextFormat(fontSize));
        //ReturnIfFailed(result, m_appleFormat->SetReadingDirection(rtl ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT));

        winrt::com_ptr<IDWriteTextFormat> textFormat;
        ReturnDefaultIfFailed(result, m_dwriteFactory->CreateTextFormat(
            L"Segoe UI Emoji",						// font family name
            m_fontCollection.get(),			        // system font collection
            DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
            DWRITE_FONT_STYLE_NORMAL,				// font style
            DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
            fontSize,								// font size
            L"",									// locale name
            textFormat.put()
        ));
        ReturnDefaultIfFailed(result, textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
        ReturnDefaultIfFailed(result, textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
        ReturnDefaultIfFailed(result, textFormat->SetReadingDirection(rtl ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT));
        ReturnDefaultIfFailed(result, textFormat->SetWordWrapping(wrap ? DWRITE_WORD_WRAPPING_EMERGENCY_BREAK : DWRITE_WORD_WRAPPING_NO_WRAP));

        if (wrap)
        {
            ReturnDefaultIfFailed(result, textFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_EMERGENCY_BREAK));
        }
        else
        {
            DWRITE_TRIMMING trimming = { DWRITE_TRIMMING_GRANULARITY_CHARACTER, '.', 3 };
            ReturnDefaultIfFailed(result, textFormat->SetTrimming(&trimming, nullptr));
            ReturnDefaultIfFailed(result, textFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP));
        }

        winrt::com_ptr<IDWriteTextLayout> textLayout;
        ReturnDefaultIfFailed(result, m_dwriteFactory->CreateTextLayout(
            text.data(),					// The string to be laid out and formatted.
            text.size(),        			// The length of the string.
            textFormat.get(),			    // The text format to apply to the string (contains font information, etc).
            width,							// The width of the layout box.
            INFINITY,						// The height of the layout box.
            textLayout.put()				// The IDWriteTextLayout interface pointer.
        ));

        for (const TextStylePart& entity : entities)
        {
            UINT32 startPosition = entity.Offset;
            UINT32 length = entity.Length;

            if (entity.Type == TextStyle::Bold)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontWeight(DWRITE_FONT_WEIGHT_SEMI_BOLD, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Italic)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontStyle(DWRITE_FONT_STYLE_ITALIC, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Strikethrough)
            {
                ReturnDefaultIfFailed(result, textLayout->SetStrikethrough(TRUE, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Underline)
            {
                ReturnDefaultIfFailed(result, textLayout->SetUnderline(TRUE, { startPosition, length }));
            }
            //else if (name == winrt::name_of<TextStylePartTypeCustomEmoji>())
            //{
            //    textLayout->SetInlineObject(m_customEmoji.get(), { startPosition, length });
            //}
            else if (entity.Type == TextStyle::Monospace)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontCollection(m_systemCollection.get(), { startPosition, length }));
                ReturnDefaultIfFailed(result, textLayout->SetFontFamilyName(m_monospaceFamily, { startPosition, length }));
            }
        }

        DWRITE_TEXT_METRICS metrics;
        ReturnDefaultIfFailed(result, textLayout->GetMetrics(&metrics));

        UINT32 maxHitTestMetricsCount = metrics.lineCount * metrics.maxBidiReorderingDepth;
        UINT32 actualTestsCount;
        DWRITE_HIT_TEST_METRICS* ranges = new DWRITE_HIT_TEST_METRICS[maxHitTestMetricsCount];
        result = textLayout->HitTestTextRange(offset, length, 0, 0, ranges, maxHitTestMetricsCount, &actualTestsCount);

        if (result == E_NOT_SUFFICIENT_BUFFER)
        {
            delete[] ranges;

            ranges = new DWRITE_HIT_TEST_METRICS[actualTestsCount];
            result = textLayout->HitTestTextRange(offset, length, 0, 0, ranges, actualTestsCount, &actualTestsCount);
        }

        if (FAILED(result))
        {
            delete[] ranges;
            return winrt::single_threaded_vector<Windows::Foundation::Rect>();
        }

        std::vector<Windows::Foundation::Rect> vector;

        for (int i = 0; i < actualTestsCount; i++)
        {
            if (ranges[i].isTrimmed)
            {
                break;
            }

            float left = ranges[i].left;
            float top = ranges[i].top;
            float right = ranges[i].left + ranges[i].width;
            float bottom = ranges[i].top + ranges[i].height;

            vector.push_back({ left, top, right - left, bottom - top });
        }

        delete[] ranges;
        return winrt::single_threaded_vector<Windows::Foundation::Rect>(std::move(vector));
    }

    Windows::Foundation::Rect Direct2DDevice::LayoutMetrics(hstring text, int32_t offset, int32_t length, IVector<TextStylePart> entities, double fontSize, double width, bool rtl)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        //ReturnIfFailed(result, CreateTextFormat(fontSize));
        //ReturnIfFailed(result, m_appleFormat->SetReadingDirection(rtl ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT));

        winrt::com_ptr<IDWriteTextFormat> textFormat;
        ReturnDefaultIfFailed(result, m_dwriteFactory->CreateTextFormat(
            L"Segoe UI Emoji",						// font family name
            m_fontCollection.get(),			        // system font collection
            DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
            DWRITE_FONT_STYLE_NORMAL,				// font style
            DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
            fontSize,								// font size
            L"",									// locale name
            textFormat.put()
        ));
        ReturnDefaultIfFailed(result, textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
        ReturnDefaultIfFailed(result, textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
        ReturnDefaultIfFailed(result, textFormat->SetReadingDirection(rtl ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT));
        ReturnDefaultIfFailed(result, textFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_EMERGENCY_BREAK));

        winrt::com_ptr<IDWriteTextLayout> textLayout;
        ReturnDefaultIfFailed(result, m_dwriteFactory->CreateTextLayout(
            text.data(),					// The string to be laid out and formatted.
            text.size(),        			// The length of the string.
            textFormat.get(),			    // The text format to apply to the string (contains font information, etc).
            width,							// The width of the layout box.
            INFINITY,						// The height of the layout box.
            textLayout.put()				// The IDWriteTextLayout interface pointer.
        ));

        for (const TextStylePart& entity : entities)
        {
            UINT32 startPosition = entity.Offset;
            UINT32 length = entity.Length;

            if (entity.Type == TextStyle::Bold)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontWeight(DWRITE_FONT_WEIGHT_SEMI_BOLD, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Italic)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontStyle(DWRITE_FONT_STYLE_ITALIC, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Strikethrough)
            {
                ReturnDefaultIfFailed(result, textLayout->SetStrikethrough(TRUE, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Underline)
            {
                ReturnDefaultIfFailed(result, textLayout->SetUnderline(TRUE, { startPosition, length }));
            }
            //else if (name == winrt::name_of<TextStylePartTypeCustomEmoji>())
            //{
            //    textLayout->SetInlineObject(m_customEmoji.get(), { startPosition, length });
            //}
            else if (entity.Type == TextStyle::Monospace)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontCollection(m_systemCollection.get(), { startPosition, length }));
                ReturnDefaultIfFailed(result, textLayout->SetFontFamilyName(m_monospaceFamily, { startPosition, length }));
            }
        }

        DWRITE_TEXT_METRICS metrics;
        ReturnDefaultIfFailed(result, textLayout->GetMetrics(&metrics));

        return { metrics.left, metrics.top, metrics.width, metrics.height };
    }

    MaxLinesMetrics Direct2DDevice::MaxLines(hstring text, int32_t offset, int32_t length, IVector<TextStylePart> entities, double fontSize, double width, bool rtl, int32_t maxLines)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        //ReturnIfFailed(result, CreateTextFormat(fontSize));
        //ReturnIfFailed(result, m_appleFormat->SetReadingDirection(rtl ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT));

        winrt::com_ptr<IDWriteTextFormat> textFormat;
        ReturnDefaultIfFailed(result, m_dwriteFactory->CreateTextFormat(
            L"Segoe UI Emoji",						// font family name
            m_fontCollection.get(),			        // system font collection
            DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
            DWRITE_FONT_STYLE_NORMAL,				// font style
            DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
            fontSize,								// font size
            L"",									// locale name
            textFormat.put()
        ));
        ReturnDefaultIfFailed(result, textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
        ReturnDefaultIfFailed(result, textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
        ReturnDefaultIfFailed(result, textFormat->SetReadingDirection(rtl ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT));
        ReturnDefaultIfFailed(result, textFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_EMERGENCY_BREAK));

        winrt::com_ptr<IDWriteTextLayout> textLayout;
        ReturnDefaultIfFailed(result, m_dwriteFactory->CreateTextLayout(
            text.data(),					// The string to be laid out and formatted.
            text.size(),        			// The length of the string.
            textFormat.get(),			    // The text format to apply to the string (contains font information, etc).
            width,							// The width of the layout box.
            INFINITY,						// The height of the layout box.
            textLayout.put()				// The IDWriteTextLayout interface pointer.
        ));

        for (const TextStylePart& entity : entities)
        {
            UINT32 startPosition = entity.Offset;
            UINT32 length = entity.Length;

            if (entity.Type == TextStyle::Bold)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontWeight(DWRITE_FONT_WEIGHT_SEMI_BOLD, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Italic)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontStyle(DWRITE_FONT_STYLE_ITALIC, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Strikethrough)
            {
                ReturnDefaultIfFailed(result, textLayout->SetStrikethrough(TRUE, { startPosition, length }));
            }
            else if (entity.Type == TextStyle::Underline)
            {
                ReturnDefaultIfFailed(result, textLayout->SetUnderline(TRUE, { startPosition, length }));
            }
            //else if (name == winrt::name_of<TextStylePartTypeCustomEmoji>())
            //{
            //    textLayout->SetInlineObject(m_customEmoji.get(), { startPosition, length });
            //}
            else if (entity.Type == TextStyle::Monospace)
            {
                ReturnDefaultIfFailed(result, textLayout->SetFontCollection(m_systemCollection.get(), { startPosition, length }));
                ReturnDefaultIfFailed(result, textLayout->SetFontFamilyName(m_monospaceFamily, { startPosition, length }));
            }
        }

        DWRITE_TEXT_METRICS metrics;
        ReturnDefaultIfFailed(result, textLayout->GetMetrics(&metrics));

        if (maxLines == 0)
        {
            return { metrics.left, metrics.top, metrics.width, metrics.height, metrics.height, length };
        }

        UINT32 actualLineCount;
        DWRITE_LINE_METRICS* ranges = new DWRITE_LINE_METRICS[metrics.lineCount];
        result = textLayout->GetLineMetrics(ranges, metrics.lineCount, &actualLineCount);

        if (result == E_NOT_SUFFICIENT_BUFFER)
        {
            delete[] ranges;

            ranges = new DWRITE_LINE_METRICS[actualLineCount];
            result = textLayout->GetLineMetrics(ranges, actualLineCount, &actualLineCount);
        }

        if (FAILED(result))
        {
            delete[] ranges;
            return {};
        }

        float truncateHeight = 0;
        int32_t truncatePosition = 0;

        // Calculate position where to truncate
        for (UINT32 i = 0; i < maxLines && i < actualLineCount; ++i)
        {
            truncateHeight += ranges[i].height;
            truncatePosition += ranges[i].length;
        }

        // Remove trailing whitespace from last included line
        if (maxLines <= actualLineCount)
        {
            //truncateHeight += ranges[maxLines - 1].height;
            truncatePosition -= ranges[maxLines - 1].trailingWhitespaceLength;
            truncatePosition -= ranges[maxLines - 1].newlineLength;
        }

        delete[] ranges;
        return { metrics.left, metrics.top, metrics.width, metrics.height, truncateHeight, truncatePosition };
    }

    HRESULT Direct2DDevice::WriteBytes(array_view<uint8_t const> hash, IRandomAccessStream randomAccessStream) noexcept
    {
        HRESULT result;
        winrt::com_ptr<IStream> stream;
        ReturnIfFailed(result, CreateStreamOverRandomAccessStream(winrt::get_unknown(randomAccessStream), IID_PPV_ARGS(&stream)));

        ReturnIfFailed(result, stream->Write(hash.data(), hash.size(), nullptr));
        ReturnIfFailed(result, stream->Seek({ 0 }, STREAM_SEEK_SET, nullptr));

        return S_OK;
    }

    inline static HRESULT GetTextFromLocalizedStrings(UINT32 stringIndex, winrt::com_ptr<IDWriteLocalizedStrings> const& localizedStrings, hstring* nameIfFound)
    {
        HRESULT result;

        UINT32 attributeLength;
        ReturnIfFailed(result, localizedStrings->GetStringLength(stringIndex, &attributeLength));

        std::wstring buffer(attributeLength + 1, L'\0');
        ReturnIfFailed(result, localizedStrings->GetString(stringIndex, buffer.data(), attributeLength + 1));

        buffer.resize(attributeLength);
        *nameIfFound = hstring(buffer);

        return S_OK;
    }

    inline static HRESULT TryGetLocalizedName(wchar_t const* locale, winrt::com_ptr<IDWriteLocalizedStrings> const& familyNames, hstring* nameIfFound)
    {
        HRESULT result;

        UINT32 index;
        BOOL found;
        ReturnIfFailed(result, familyNames->FindLocaleName(locale, &index, &found));
        if (found)
        {
            ReturnIfFailed(result, GetTextFromLocalizedStrings(index, familyNames, nameIfFound));
            return S_OK;
        }

        return E_FAIL;
    }


    inline static HRESULT TryGetLocalizedNameUsingLocaleList(IVector<hstring> const& localeList, winrt::com_ptr<IDWriteLocalizedStrings> const& familyNames, hstring* nameIfFound)
    {
        for (auto const& locale : localeList)
        {
            HRESULT result = TryGetLocalizedName(locale.c_str(), familyNames, nameIfFound);

            if (SUCCEEDED(result))
            {
                return S_OK;
            }
        }

        return E_FAIL;
    }

    IVector<hstring> Direct2DDevice::GetSystemFontFamilies(IVector<hstring> localeNames)
    {
        auto families = winrt::single_threaded_vector<hstring>();

        HRESULT result;
        UINT32 count = m_systemCollection->GetFontFamilyCount();

        for (UINT32 i = 0; i < count; i++)
        {
            winrt::com_ptr<IDWriteFontFamily> fontFamily;
            ContinueIfFailed(result, m_systemCollection->GetFontFamily(i, fontFamily.put()));

            winrt::com_ptr<IDWriteLocalizedStrings> familyNames;
            ContinueIfFailed(result, fontFamily->GetFamilyNames(familyNames.put()));

            hstring name;
            result = TryGetLocalizedNameUsingLocaleList(localeNames, familyNames, &name);

            if (FAILED(result))
            {
                result = TryGetLocalizedName(L"en-us", familyNames, &name);
            }

            if (FAILED(result))
            {
                result = GetTextFromLocalizedStrings(0, familyNames, &name);
            }

            if (SUCCEEDED(result))
            {
                families.Append(name);
            }
        }

        return families;
    }

    winrt::Telegram::Native::FreeformGradientSurface Direct2DDevice::CreateFreeformGradient(IVectorView<int32_t> colors)
    {
        auto surface = CreateDrawingSurface({ 50, 50 });
        if (surface)
        {
            auto gradient = winrt::make_self<FreeformGradientSurface>(m_compositionDevice, m_d2dFactory, m_compositor, surface, colors);
            return gradient.as<winrt::Telegram::Native::FreeformGradientSurface>();
        }

        return nullptr;
    }

    // Nothing reports that a XamlRoot has gone, so expired buckets are dropped whenever a window is
    // seen for the first time. Pruning on lookup instead would put a weak resolve on the path every
    // bubble takes, to reclaim a handful of small surfaces sooner.
    void Direct2DDevice::PruneNineGridCache()
    {
        for (auto it = m_nineGridCache.begin(); it != m_nineGridCache.end();)
        {
            if (it->second.Root.get())
            {
                ++it;
            }
            else
            {
                it = m_nineGridCache.erase(it);
            }
        }
    }

    winrt::com_ptr<MessageBubbleNineGrid> Direct2DDevice::GetNineGrid(XamlRoot const& xamlRoot, int topLeftRadius, int topRightRadius, int bottomRightRadius, int bottomLeftRadius)
    {
        // Bubbles are realized before the window has content, so a missing XamlRoot is expected.
        if (xamlRoot == nullptr || m_compositionDevice == nullptr)
        {
            return nullptr;
        }

        // Pack 4 radius values into one int
        // Each value needs only 5 bits (0-31 range), so 4 values fit in 20 bits
        int key = (topLeftRadius << 15) | (topRightRadius << 10) | (bottomRightRadius << 5) | bottomLeftRadius;

        // The projection always holds the same interface pointer for a given XamlRoot, so its ABI
        // pointer identifies the window without keeping it alive. Reuse of a freed address is not a
        // hazard because a bucket is only ever added after pruning.
        auto owner = winrt::get_abi(xamlRoot);

        auto bucket = m_nineGridCache.find(owner);
        if (bucket == m_nineGridCache.end())
        {
            PruneNineGridCache();
            bucket = m_nineGridCache.emplace(owner, NineGridBucket{ winrt::make_weak(xamlRoot), {} }).first;
        }
        else
        {
            auto it = bucket->second.Grids.find(key);
            if (it != bucket->second.Grids.end())
            {
                return it->second;
            }
        }

        double rasterizationScale = xamlRoot.RasterizationScale();
        SizeInt32 imageSize(std::ceil(MessageBubbleNineGrid::s_width * rasterizationScale), std::ceil(MessageBubbleNineGrid::s_height * rasterizationScale));

        auto surface = CreateDrawingSurface(imageSize);
        if (surface)
        {
            auto effect = m_alphaMaskFactory.CreateBrush();
            auto nineGrid = winrt::make_self<MessageBubbleNineGrid>(m_compositionDevice, m_d2dFactory, m_compositor, xamlRoot, surface, effect, topLeftRadius, topRightRadius, bottomRightRadius, bottomLeftRadius);
            bucket->second.Grids[key] = nineGrid;
            return nineGrid;
        }

        return nullptr;
    }

    CompositionEffectBrush Direct2DDevice::GetTail(XamlRoot xamlRoot, int topLeftRadius, int topRightRadius, int bottomRightRadius, int bottomLeftRadius)
    {
        auto nineGrid = GetNineGrid(xamlRoot, topLeftRadius, topRightRadius, bottomRightRadius, bottomLeftRadius);
        return nineGrid ? nineGrid->Effect() : nullptr;
    }

    // The alpha mask of GetTail, for callers that paint the silhouette rather than mask a layer:
    // a CompositionMaskBrush over this costs no offscreen intermediate, a LayerVisual effect does.
    CompositionNineGridBrush Direct2DDevice::GetTailMask(XamlRoot xamlRoot, int topLeftRadius, int topRightRadius, int bottomRightRadius, int bottomLeftRadius)
    {
        auto nineGrid = GetNineGrid(xamlRoot, topLeftRadius, topRightRadius, bottomRightRadius, bottomLeftRadius);
        return nineGrid ? nineGrid->Mask() : nullptr;
    }

    CompositionDrawingSurface Direct2DDevice::CreateDrawingSurface(SizeInt32 size)
    {
        try
        {
            return m_compositionDevice.CreateDrawingSurface2(size, DirectXPixelFormat::B8G8R8A8UIntNormalized, DirectXAlphaMode::Premultiplied);
        }
        catch (...)
        {
            // TODO: handle device lost, for now we return null
            return nullptr;
        }
    }

    //CompositionPath Direct2DDevice::GetOutline(IVector<ClosedVectorPath> contours)
    //{
    //    std::lock_guard const guard(m_criticalSection);
    //    HRESULT result;

    //    winrt::com_ptr<ID2D1GeometrySink> d2dGeometrySink;
    //    winrt::com_ptr<ID2D1PathGeometry1> d2dPathGeometry;

    //    ReturnNullIfFailed(result, m_d2dFactory->CreatePathGeometry(d2dPathGeometry.put()));
    //    ReturnNullIfFailed(result, d2dPathGeometry->Open(d2dGeometrySink.put()));

    //    for (const ClosedVectorPath& path : contours)
    //    {
    //        bool open = true;
    //        VectorPathCommandCubicBezierCurve endCurve{ nullptr };

    //        for (const VectorPathCommand& command : path.Commands())
    //        {
    //            if (auto line = command.try_as<VectorPathCommandLine>())
    //            {
    //                auto endPoint = line.EndPoint();
    //                if (open)
    //                {
    //                    open = false;
    //                    d2dGeometrySink->BeginFigure({ (float)endPoint.X(), (float)endPoint.Y() }, D2D1_FIGURE_BEGIN_FILLED);
    //                }
    //                else
    //                {
    //                    d2dGeometrySink->AddLine({ (float)endPoint.X(), (float)endPoint.Y() });
    //                }
    //            }
    //            else if (auto cubicBezierCurve = command.try_as<VectorPathCommandCubicBezierCurve>())
    //            {
    //                auto endPoint = cubicBezierCurve.EndPoint();

    //                if (open)
    //                {
    //                    open = false;
    //                    d2dGeometrySink->BeginFigure({ (float)endPoint.X(), (float)endPoint.Y() }, D2D1_FIGURE_BEGIN_FILLED);
    //                    endCurve = cubicBezierCurve;
    //                }
    //                else
    //                {
    //                    auto controlPoint1 = cubicBezierCurve.StartControlPoint();
    //                    auto controlPoint2 = cubicBezierCurve.EndControlPoint();

    //                    d2dGeometrySink->AddBezier({
    //                        { (float)controlPoint1.X(), (float)controlPoint1.Y() },
    //                        { (float)controlPoint2.X(), (float)controlPoint2.Y() },
    //                        { (float)endPoint.X(), (float)endPoint.Y() }
    //                        });
    //                }
    //            }
    //        }

    //        if (endCurve)
    //        {
    //            auto endPoint = endCurve.EndPoint();
    //            auto controlPoint1 = endCurve.StartControlPoint();
    //            auto controlPoint2 = endCurve.EndControlPoint();

    //            d2dGeometrySink->AddBezier({
    //                { (float)controlPoint1.X(), (float)controlPoint1.Y() },
    //                { (float)controlPoint2.X(), (float)controlPoint2.Y() },
    //                { (float)endPoint.X(), (float)endPoint.Y() }
    //                });
    //        }

    //        d2dGeometrySink->EndFigure(D2D1_FIGURE_END_CLOSED);
    //    }

    //    ReturnNullIfFailed(result, d2dGeometrySink->Close());

    //    auto geometry = winrt::make_self<CompositionPathSource>(d2dPathGeometry);
    //    return CompositionPath(geometry.as<winrt::Windows::Graphics::IGeometrySource2D>());
    //}

    CompositionPath Direct2DDevice::GetEllipticalClip(float width, float height, float radius, float x, float y)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        winrt::com_ptr<ID2D1GeometrySink> d2dGeometrySink;
        winrt::com_ptr<ID2D1PathGeometry1> d2dPathGeometry;

        ReturnNullIfFailed(result, m_d2dFactory->CreatePathGeometry(d2dPathGeometry.put()));
        ReturnNullIfFailed(result, d2dPathGeometry->Open(d2dGeometrySink.put()));

        d2dGeometrySink->SetFillMode(D2D1_FILL_MODE_ALTERNATE);
        d2dGeometrySink->BeginFigure({ 0, 0 }, D2D1_FIGURE_BEGIN_FILLED);
        d2dGeometrySink->AddLine({ width, 0 });
        d2dGeometrySink->AddLine({ width, height });
        d2dGeometrySink->AddLine({ 0, height });
        d2dGeometrySink->EndFigure(D2D1_FIGURE_END_CLOSED);

        D2D1_POINT_2F startPoint = D2D1::Point2F(x + radius, y);
        D2D1_SIZE_F radii = D2D1::SizeF(radius, radius);

        d2dGeometrySink->BeginFigure(startPoint, D2D1_FIGURE_BEGIN_FILLED);
        d2dGeometrySink->AddArc(D2D1::ArcSegment(
            D2D1::Point2F(x - radius, y),
            radii,
            0.0f,
            D2D1_SWEEP_DIRECTION_CLOCKWISE,
            D2D1_ARC_SIZE_SMALL
        ));
        d2dGeometrySink->AddArc(D2D1::ArcSegment(
            startPoint,
            radii,
            0.0f,
            D2D1_SWEEP_DIRECTION_CLOCKWISE,
            D2D1_ARC_SIZE_SMALL
        ));
        d2dGeometrySink->EndFigure(D2D1_FIGURE_END_CLOSED);

        ReturnNullIfFailed(result, d2dGeometrySink->Close());

        auto geometry = winrt::make_self<CompositionPathSource>(d2dPathGeometry);
        return CompositionPath(geometry.as<winrt::Windows::Graphics::IGeometrySource2D>());
    }

    inline void AppendButton(winrt::com_ptr<ID2D1GeometrySink> d2dGeometrySink, float x, float y, float width, float height, float topLeftRadius, float topRightRadius, float bottomRightRadius, float bottomLeftRadius)
    {
        d2dGeometrySink->BeginFigure({ x + topLeftRadius, y }, D2D1_FIGURE_BEGIN_FILLED);

        // Top edge
        d2dGeometrySink->AddLine({ x + width - topRightRadius, y });

        // Top-right corner
        if (topRightRadius > 0)
            d2dGeometrySink->AddArc({ { x + width, y + topRightRadius }, { topRightRadius, topRightRadius }, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL });

        // Right edge
        d2dGeometrySink->AddLine({ x + width, y + height - bottomRightRadius });

        // Bottom-right corner
        if (bottomRightRadius > 0)
            d2dGeometrySink->AddArc({ { x + width - bottomRightRadius, y + height }, { bottomRightRadius, bottomRightRadius }, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL });

        // Bottom edge
        d2dGeometrySink->AddLine({ x + bottomLeftRadius, y + height });

        // Bottom-left corner
        if (bottomLeftRadius > 0)
            d2dGeometrySink->AddArc({ { x, y + height - bottomLeftRadius }, { bottomLeftRadius, bottomLeftRadius }, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL });

        // Left edge
        d2dGeometrySink->AddLine({ x, y + topLeftRadius });

        // Top-left corner
        if (topLeftRadius > 0)
            d2dGeometrySink->AddArc({ { x + topLeftRadius, y }, { topLeftRadius, topLeftRadius }, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL });

        d2dGeometrySink->EndFigure(D2D1_FIGURE_END_CLOSED);
    }

    CompositionPath Direct2DDevice::GetReplyMarkupClip(IVector<IVector<Windows::Foundation::Rect>> rows, float bottomRightRadius, float bottomLeftRadius)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        winrt::com_ptr<ID2D1GeometrySink> d2dGeometrySink;
        winrt::com_ptr<ID2D1PathGeometry1> d2dPathGeometry;

        ReturnNullIfFailed(result, m_d2dFactory->CreatePathGeometry(d2dPathGeometry.put()));
        ReturnNullIfFailed(result, d2dPathGeometry->Open(d2dGeometrySink.put()));

        auto padding = 2;
        auto x = 0.f;
        auto y = 0.f;

        auto j = 0;

        for (const IVector<Windows::Foundation::Rect>& row : rows)
        {
            auto i = 0;

            for (const Windows::Foundation::Rect& button : row)
            {
                auto bottomRight = 4.f;
                auto bottomLeft = 4.f;

                if (j == rows.Size() - 1)
                {
                    if (i == 0)
                    {
                        bottomLeft = bottomLeftRadius;
                    }

                    if (i == row.Size() - 1)
                    {
                        bottomRight = bottomRightRadius;
                    }
                }

                AppendButton(d2dGeometrySink, button.X, button.Y, button.Width, button.Height, 4, 4, bottomRight, bottomLeft);

                i++;
            }

            j++;
        }

        ReturnNullIfFailed(result, d2dGeometrySink->Close());

        auto geometry = winrt::make_self<CompositionPathSource>(d2dPathGeometry);
        return CompositionPath(geometry.as<winrt::Windows::Graphics::IGeometrySource2D>());
    }

    CompositionPath Direct2DDevice::GetVoiceNoteClip(array_view<uint8_t const> waveform, double waveformWidth)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        winrt::com_ptr<ID2D1GeometrySink> d2dGeometrySink;
        winrt::com_ptr<ID2D1PathGeometry1> d2dPathGeometry;

        ReturnNullIfFailed(result, m_d2dFactory->CreatePathGeometry(d2dPathGeometry.put()));
        ReturnNullIfFailed(result, d2dPathGeometry->Open(d2dGeometrySink.put()));

        auto lines = waveform.size() * 8 / 5;
        auto bytes = new double[lines];

        for (int i = 0; i < lines; i++)
        {
            int j = (i * 5) / 8, shift = (i * 5) % 8;
            bytes[i] = ((waveform.at(j) | ((j + 1 < waveform.size() ? waveform.at(j + 1) : 0) << 8)) >> shift & 0x1F) / 31.0;
        }

        auto imageWidth = waveformWidth; // 142d; // double.IsNaN(ActualWidth) ? 142 : ActualWidth;
        auto imageHeight = 20;

        auto space = 1.0;
        auto lineWidth = 2.0;
        auto maxLines = (imageWidth - space) / (lineWidth + space);
        auto maxWidth = lines / maxLines;

        for (int index = 0; index < maxLines; index++)
        {
            auto lineIndex = (int)(index * maxWidth);
            auto lineHeight = bytes[lineIndex] * (double)(imageHeight - 2.0) + 2.0;

            float x1 = (int)(index * (lineWidth + space));
            float y1 = (imageHeight - (int)lineHeight) / 2;
            float x2 = (int)(index * (lineWidth + space) + lineWidth);
            float y2 = imageHeight - y1;

            //d2dGeometrySink->BeginFigure({ x1, y1 }, D2D1_FIGURE_BEGIN_FILLED);
            //d2dGeometrySink->AddLine({ x2, y1 });
            //d2dGeometrySink->AddLine({ x2, y2 });
            //d2dGeometrySink->AddLine({ x1, y2 });
            //d2dGeometrySink->EndFigure(D2D1_FIGURE_END_CLOSED);

            if (lineHeight > 2)
            {
                d2dGeometrySink->BeginFigure({ x1, y1 + 1 }, D2D1_FIGURE_BEGIN_FILLED);
                d2dGeometrySink->AddArc(D2D1::ArcSegment(
                    D2D1::Point2F(x2, y1 + 1),
                    D2D1::SizeF(1, 1),
                    0.0f,
                    D2D1_SWEEP_DIRECTION_CLOCKWISE,
                    D2D1_ARC_SIZE_SMALL
                ));
                d2dGeometrySink->AddLine({ x2, y2 - 1 });
                d2dGeometrySink->AddArc(D2D1::ArcSegment(
                    { x1, y2 - 1 },
                    D2D1::SizeF(1, 1),
                    0.0f,
                    D2D1_SWEEP_DIRECTION_CLOCKWISE,
                    D2D1_ARC_SIZE_SMALL
                ));
            }
            else
            {
                d2dGeometrySink->BeginFigure({ x1, 10 }, D2D1_FIGURE_BEGIN_FILLED);
                d2dGeometrySink->AddArc(D2D1::ArcSegment(
                    D2D1::Point2F(x2, 10),
                    D2D1::SizeF(1, 1),
                    0.0f,
                    D2D1_SWEEP_DIRECTION_CLOCKWISE,
                    D2D1_ARC_SIZE_SMALL
                ));
                d2dGeometrySink->AddArc(D2D1::ArcSegment(
                    { x1, 10 },
                    D2D1::SizeF(1, 1),
                    0.0f,
                    D2D1_SWEEP_DIRECTION_CLOCKWISE,
                    D2D1_ARC_SIZE_SMALL
                ));
            }

            d2dGeometrySink->EndFigure(D2D1_FIGURE_END_CLOSED);
        }

        delete[] bytes;

        ReturnNullIfFailed(result, d2dGeometrySink->Close());

        auto geometry = winrt::make_self<CompositionPathSource>(d2dPathGeometry);
        return CompositionPath(geometry.as<winrt::Windows::Graphics::IGeometrySource2D>());
    }

    CompositionPath Direct2DDevice::GetRoundedPolygon(IVector<IVector<Windows::Foundation::Rect>> shapes)
    {
        std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        winrt::com_ptr<ID2D1GeometrySink> d2dGeometrySink;
        winrt::com_ptr<ID2D1PathGeometry1> d2dPathGeometry;

        ReturnNullIfFailed(result, m_d2dFactory->CreatePathGeometry(d2dPathGeometry.put()));
        ReturnNullIfFailed(result, d2dPathGeometry->Open(d2dGeometrySink.put()));

        auto angle = -90 * std::numbers::pi_v<float> / 180.0f; // MathFEx.ToRadians(-90);
        static auto rightAt = [](const winrt::Windows::Foundation::Rect& rect)
            {
                return rect.X + rect.Width;
            };

        for (int j = 0; j < shapes.Size(); j++)
        {
            const auto& rectangles = shapes.GetAt(j);

            for (int i = 0; i < rectangles.Size(); i++)
            {
                const auto& rect = rectangles.GetAt(i);
                const auto right = rect.X + rect.Width;
                const auto bottom = rect.Y + rect.Height;

                if (i == 0)
                {
                    d2dGeometrySink->BeginFigure({ right - 4, rect.Y }, D2D1_FIGURE_BEGIN_FILLED);
                    d2dGeometrySink->AddArc(D2D1::ArcSegment({ right, rect.Y + 4 }, { 4, 4 }, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL));
                }
                else
                {
                    auto y1diff = i > 0 ? right - rightAt(rectangles.GetAt(i - 1)) : 4;
                    auto y1radius = fminf(4, fabsf(y1diff));

                    if (y1diff < 0)
                    {
                        d2dGeometrySink->AddLine({ right + y1radius, rect.Y });
                        d2dGeometrySink->AddArc(D2D1::ArcSegment({ right, rect.Y + y1radius }, { y1radius, y1radius }, 0, D2D1_SWEEP_DIRECTION_COUNTER_CLOCKWISE, D2D1_ARC_SIZE_SMALL));
                    }
                    else if (y1diff > 0)
                    {
                        d2dGeometrySink->AddLine({ right - y1radius, rect.Y });
                        d2dGeometrySink->AddArc(D2D1::ArcSegment({ right, rect.Y + y1radius }, { y1radius, y1radius }, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL));
                    }
                }

                auto y2diff = i < rectangles.Size() - 1 ? right - rightAt(rectangles.GetAt(i + 1)) : 4;
                auto y2radius = fminf(4, fabsf(y2diff));

                d2dGeometrySink->AddLine({ right, bottom - y2radius });

                if (y2diff < 0)
                {
                    d2dGeometrySink->AddArc(D2D1::ArcSegment({ right + y2radius, rectangles.GetAt(i + 1).Y }, { y2radius, y2radius }, 0, D2D1_SWEEP_DIRECTION_COUNTER_CLOCKWISE, D2D1_ARC_SIZE_SMALL));
                }
                else if (y2diff > 0)
                {
                    d2dGeometrySink->AddArc(D2D1::ArcSegment({ right - y2radius, bottom }, { y2radius, y2radius }, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL));
                }
            }

            for (int i = rectangles.Size() - 1; i >= 0; i--)
            {
                const auto& rect = rectangles.GetAt(i);
                const auto right = rect.X + rect.Width;
                const auto bottom = rect.Y + rect.Height;

                auto y1diff = i < rectangles.Size() - 1 ? rect.X - rectangles.GetAt(i + 1).X : -4;
                auto y1radius = fminf(4, fabsf(y1diff));

                if (y1diff > 0)
                {
                    d2dGeometrySink->AddLine({ rect.X - y1radius, bottom });
                    d2dGeometrySink->AddArc(D2D1::ArcSegment({ rect.X, bottom - y1radius }, { y1radius, y1radius }, 0, D2D1_SWEEP_DIRECTION_COUNTER_CLOCKWISE, D2D1_ARC_SIZE_SMALL));
                }
                else if (y1diff < 0)
                {
                    d2dGeometrySink->AddLine({ rect.X + y1radius, bottom });
                    d2dGeometrySink->AddArc(D2D1::ArcSegment({ rect.X, bottom - y1radius }, { y1radius, y1radius }, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL));
                }

                auto y2diff = i > 0 ? rect.X - rectangles.GetAt(i - 1).X : -4;
                auto y2radius = fminf(4, fabsf(y2diff));

                d2dGeometrySink->AddLine({ rect.X, rect.Y + y2radius });

                if (y2diff > 0)
                {
                    d2dGeometrySink->AddArc(D2D1::ArcSegment({ rect.X - y2radius, rect.Y }, { y2radius, y2radius }, angle, D2D1_SWEEP_DIRECTION_COUNTER_CLOCKWISE, D2D1_ARC_SIZE_SMALL));
                }
                else if (y2diff < 0)
                {
                    d2dGeometrySink->AddArc(D2D1::ArcSegment({ rect.X + y2radius, rect.Y }, { y2radius, y2radius }, angle, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL));
                }
            }

            d2dGeometrySink->EndFigure(D2D1_FIGURE_END_CLOSED);
        }

        ReturnNullIfFailed(result, d2dGeometrySink->Close());

        auto geometry = winrt::make_self<CompositionPathSource>(d2dPathGeometry);
        return CompositionPath(geometry.as<winrt::Windows::Graphics::IGeometrySource2D>());
    }

    HRESULT Direct2DDevice::Encode(IBuffer source, IRandomAccessStream destination, int32_t width, int32_t height, int32_t rotation)
    {
        HRESULT result;
        winrt::com_ptr<IStream> stream;
        ReturnIfFailed(result, CreateStreamOverRandomAccessStream(winrt::get_unknown(destination), IID_PPV_ARGS(&stream)));

        if (destination.Size())
        {
            stream->SetSize({ 0 });
        }

        winrt::com_ptr<IWICBitmapEncoder> wicBitmapEncoder;
        ReturnIfFailed(result, m_wicFactory->CreateEncoder(GUID_ContainerFormatPng, nullptr, wicBitmapEncoder.put()));
        ReturnIfFailed(result, wicBitmapEncoder->Initialize(stream.get(), WICBitmapEncoderNoCache));

        winrt::com_ptr<IWICBitmapFrameEncode> wicFrameEncode;
        ReturnIfFailed(result, wicBitmapEncoder->CreateNewFrame(wicFrameEncode.put(), nullptr));
        ReturnIfFailed(result, wicFrameEncode->Initialize(nullptr));

        WICPixelFormatGUID pixelFormat = GUID_WICPixelFormat32bppBGRA;
        ReturnIfFailed(result, wicFrameEncode->SetSize(width, height));
        ReturnIfFailed(result, wicFrameEncode->SetPixelFormat(&pixelFormat));

        if (rotation)
        {
            winrt::com_ptr<IWICMetadataQueryWriter> pMetadataWriter;
            ReturnIfFailed(result, wicFrameEncode->GetMetadataQueryWriter(pMetadataWriter.put()));

            PROPVARIANT propValue;
            PropVariantInit(&propValue);
            propValue.vt = VT_UI2;

            switch (rotation)
            {
            case 90:
                propValue.uiVal = PHOTO_ORIENTATION_ROTATE270;
                break;
            case 180:
                propValue.uiVal = PHOTO_ORIENTATION_ROTATE180;
                break;
            case 270:
                propValue.uiVal = PHOTO_ORIENTATION_ROTATE90;
                break;
            default:
                propValue.uiVal = PHOTO_ORIENTATION_NORMAL;
                break;
            }

            ReturnIfFailed(result, pMetadataWriter->SetMetadataByName(L"System.Photo.Orientation", &propValue));
            PropVariantClear(&propValue);
        }

        ReturnIfFailed(result, wicFrameEncode->WritePixels(height, width * 4, width * height * 4, source.data()));
        ReturnIfFailed(result, wicFrameEncode->Commit());
        ReturnIfFailed(result, wicBitmapEncoder->Commit());

        ReturnIfFailed(result, stream->Commit(STGC_DEFAULT));

        return stream->Seek({ 0 }, STREAM_SEEK_SET, nullptr);
    }

    HRESULT Direct2DDevice::SaveImageToStream(ID2D1Image* image, REFGUID wicFormat, IRandomAccessStream randomAccessStream)
    {
        HRESULT result;
        winrt::com_ptr<IStream> stream;
        ReturnIfFailed(result, CreateStreamOverRandomAccessStream(winrt::get_unknown(randomAccessStream), IID_PPV_ARGS(&stream)));

        if (randomAccessStream.Size())
        {
            stream->SetSize({ 0 });
        }

        winrt::com_ptr<IWICBitmapEncoder> wicBitmapEncoder;
        ReturnIfFailed(result, m_wicFactory->CreateEncoder(wicFormat, nullptr, wicBitmapEncoder.put()));
        ReturnIfFailed(result, wicBitmapEncoder->Initialize(stream.get(), WICBitmapEncoderNoCache));

        winrt::com_ptr<IWICBitmapFrameEncode> wicFrameEncode;
        ReturnIfFailed(result, wicBitmapEncoder->CreateNewFrame(wicFrameEncode.put(), nullptr));
        ReturnIfFailed(result, wicFrameEncode->Initialize(nullptr));

        ReturnIfFailed(result, m_imageEncoder->WriteFrame(image, wicFrameEncode.get(), nullptr));
        ReturnIfFailed(result, wicFrameEncode->Commit());
        ReturnIfFailed(result, wicBitmapEncoder->Commit());

        ReturnIfFailed(result, stream->Commit(STGC_DEFAULT));

        return stream->Seek({ 0 }, STREAM_SEEK_SET, nullptr);
    }
} // namespace winrt::Telegram::Native::implementation