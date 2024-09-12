#pragma once

#include "PlaceholderImageHelper.g.h"

#include <ppl.h>
#include <wincodec.h>
#include <Dwrite_1.h>
#include <D2d1_3.h>
#include <map>

#include <SurfaceImage.h>
#include <TextFormat.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.Storage.Streams.h>

#include <winrt/Telegram.Td.Api.h>

using namespace concurrency;
using namespace winrt::Windows::UI;
using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::Storage::Streams;
using namespace winrt::Telegram::Td::Api;

namespace winrt::Telegram::Native::implementation
{
    struct PlaceholderImageHelper : PlaceholderImageHelperT<PlaceholderImageHelper>
    {
    public:
        PlaceholderImageHelper();

        HRESULT HandleDeviceLost()
        {
            if (FAILED(m_d3dDevice->GetDeviceRemovedReason()))
            {
                return CreateDeviceResources();
            }

            return S_OK;
        }

        static winrt::Telegram::Native::PlaceholderImageHelper Current()
        {
            std::lock_guard const guard(s_criticalSection);

            if (s_current == nullptr)
            {
                s_current = winrt::make_self<PlaceholderImageHelper>();
            }

            s_current->HandleDeviceLost();
            return s_current.as<winrt::Telegram::Native::PlaceholderImageHelper>();
        }

        static HRESULT WriteBytes(IVector<byte> hash, IRandomAccessStream randomAccessStream) noexcept;
        static IBuffer DrawWebP(hstring fileName, int32_t maxWidth, int32_t& pixelWidth, int32_t& pixelHeight) noexcept;

        HRESULT Encode(IBuffer source, IRandomAccessStream destination, int32_t width, int32_t height);

        winrt::Windows::Foundation::IAsyncAction DrawSvgAsync(hstring path, Color foreground, IRandomAccessStream randomAccessStream, double dpi);
        HRESULT DrawSvg(hstring path, Color foreground, IRandomAccessStream randomAccessStream, double dpi, Windows::Foundation::Size& size);

        HRESULT DrawThumbnailPlaceholder(hstring fileName, float blurAmount, IRandomAccessStream randomAccessStream);
        HRESULT DrawThumbnailPlaceholder(IVector<uint8_t> bytes, float blurAmount, IRandomAccessStream randomAccessStream);
        HRESULT DrawThumbnailPlaceholder(IVector<uint8_t> bytes, float blurAmount, IBuffer randomAccessStream);

        winrt::Telegram::Native::SurfaceImage Create(int32_t pixelWidth, int32_t pixelHeight);
        HRESULT Invalidate(winrt::Telegram::Native::SurfaceImage imageSource, IBuffer buffer);

        winrt::Telegram::Native::TextFormat CreateTextFormat2(hstring text, IVector<TextEntity> entities, double fontSize, double width);

        float2 ContentEnd(hstring text, IVector<TextEntity> entities, double fontSize, double width);
        IVector<Windows::Foundation::Rect> LineMetrics(hstring text, IVector<TextEntity> entities, double fontSize, double width, bool rtl);
        IVector<Windows::Foundation::Rect> RangeMetrics(hstring text, int32_t offset, int32_t length, IVector<TextEntity> entities, double fontSize, double width, bool rtl);
        int32_t TrimMetrics(hstring text, int32_t offset, int32_t length, IVector<TextEntity> entities, double fontSize, double width, double height, bool rtl);
        //IVector<Windows::Foundation::Rect> EntityMetrics(hstring text, IVector<TextEntity> entities, double fontSize, double width, bool rtl);

    private:
        HRESULT CreateDeviceIndependentResources();
        HRESULT CreateDeviceResources();
        HRESULT CreateTextFormat(double fontSize);

        HRESULT InternalDrawThumbnailPlaceholder(IWICBitmapSource* wicBitmapSource, float blurAmount, IRandomAccessStream randomAccessStream, bool minithumbnail);
        HRESULT InternalDrawThumbnailPlaceholder(IWICBitmapSource* wicBitmapSource, float blurAmount, IBuffer randomAccessStream, bool minithumbnail);
        HRESULT SaveImageToStream(ID2D1Image* image, REFGUID wicFormat, IRandomAccessStream randomAccessStream);

        HRESULT CreateTextFormatImpl(hstring text, IVector<TextEntity> entities, double fontSize, double width, winrt::com_ptr<TextFormat>& textFormat);
        HRESULT ContentEndImpl(hstring text, IVector<TextEntity> entities, double fontSize, double width, float2& offset);
        HRESULT RangeMetricsImpl(hstring text, int32_t offset, int32_t length, IVector<TextEntity> entities, double fontSize, double width, bool rtl, IVector<Windows::Foundation::Rect>& rects);
        HRESULT TrimMetricsImpl(hstring text, int32_t offset, int32_t length, IVector<TextEntity> entities, double fontSize, double width, double height, bool rtl, int32_t& output);


    public:
        static std::mutex s_criticalSection;
        static winrt::com_ptr<PlaceholderImageHelper> s_current;

        winrt::com_ptr<ID2D1Factory1> m_d2dFactory;
        winrt::com_ptr<ID2D1Device> m_d2dDevice;
        winrt::com_ptr<ID3D11Device> m_d3dDevice;
        winrt::com_ptr<ID2D1DeviceContext2> m_d2dContext;
        D3D_FEATURE_LEVEL m_featureLevel;
        winrt::com_ptr<IWICImagingFactory2> m_wicFactory;
        winrt::com_ptr<IWICImageEncoder> m_imageEncoder;
        winrt::com_ptr<IDWriteFactory1> m_dwriteFactory;
        winrt::com_ptr<IDWriteFontCollectionLoader> m_customLoader;
        winrt::com_ptr<IDWriteFontCollection> m_fontCollection;
        winrt::com_ptr<IDWriteFontCollection> m_systemCollection;
        winrt::com_ptr<IDWriteInlineObject> m_customEmoji;
        winrt::com_ptr<IDWriteTextFormat> m_appleFormat;
        winrt::com_ptr<ID2D1Effect> m_gaussianBlurEffect;
        std::mutex m_criticalSection;
    };
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
    struct PlaceholderImageHelper : PlaceholderImageHelperT<PlaceholderImageHelper, implementation::PlaceholderImageHelper>
    {
    };
} // namespace winrt::Telegram::Native::factory_implementation
