#pragma once

#include "PlaceholderImageHelper.g.h"

#include <ppl.h>
#include <wincodec.h>
#include <Dwrite_1.h>
#include <D2d1_3.h>
#include <map>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.Storage.Streams.h>

using namespace concurrency;
using namespace winrt::Windows::UI;
using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::Storage::Streams;

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
            slim_lock_guard const guard(s_criticalSection);

            if (s_current == nullptr)
            {
                s_current = winrt::make_self<PlaceholderImageHelper>();
            }

            s_current->HandleDeviceLost();
            return s_current.as<winrt::Telegram::Native::PlaceholderImageHelper>();
        }

        static void WriteBytes(IVector<byte> hash, IRandomAccessStream randomAccessStream) noexcept;
        static IBuffer DrawWebP(hstring fileName, int32_t maxWidth, Windows::Foundation::Size& size) noexcept;

        HRESULT Encode(IBuffer source, IRandomAccessStream destination, int32_t width, int32_t height);

        winrt::Windows::Foundation::IAsyncAction DrawSvgAsync(hstring path, _In_ Color foreground, IRandomAccessStream randomAccessStream, double dpi);
        void DrawSvg(hstring path, _In_ Color foreground, IRandomAccessStream randomAccessStream, double dpi, Windows::Foundation::Size& size);

        void DrawThumbnailPlaceholder(hstring fileName, float blurAmount, _In_ IRandomAccessStream randomAccessStream);
        void DrawThumbnailPlaceholder(IVector<uint8_t> bytes, float blurAmount, _In_ IRandomAccessStream randomAccessStream);

        float2 ContentEnd(hstring text, IVector<PlaceholderEntity> entities, double fontSize, double width);
        IVector<Windows::Foundation::Rect> LineMetrics(hstring text, double fontSize, double width, bool rtl);
        //IVector<Windows::Foundation::Rect> EntityMetrics(hstring text, IVector<TextEntity> entities, double fontSize, double width, bool rtl);

    //internal:
    //	PlaceholderImageHelper();

    private:
        //PlaceholderImageHelper();

        HRESULT InternalDrawSvg(hstring data, _In_ Color foreground, _In_ IRandomAccessStream randomAccessStream, double dpi, _Out_ Windows::Foundation::Size& size);
        HRESULT InternalDrawThumbnailPlaceholder(hstring fileName, float blurAmount, _In_ IRandomAccessStream randomAccessStream);
        HRESULT InternalDrawThumbnailPlaceholder(IVector<uint8_t> bytes, float blurAmount, _In_ IRandomAccessStream randomAccessStream);
        HRESULT InternalDrawThumbnailPlaceholder(_In_ IWICBitmapSource* wicBitmapSource, float blurAmount, _In_ IRandomAccessStream randomAccessStream, bool minithumbnail);
        HRESULT SaveImageToStream(_In_ ID2D1Image* image, _In_ REFGUID wicFormat, _In_ IRandomAccessStream randomAccessStream);
        HRESULT MeasureText(_In_ const wchar_t* text, _In_ IDWriteTextFormat* format, _Out_ DWRITE_TEXT_METRICS* textMetrics);
        HRESULT CreateDeviceIndependentResources();
        HRESULT CreateDeviceResources();

    private:
        //static std::map<int, WeakReference> s_windowContext;

        static winrt::slim_mutex s_criticalSection;
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
        winrt::com_ptr<IDWriteTextFormat> m_appleFormat;
        winrt::com_ptr<ID2D1Effect> m_gaussianBlurEffect;
        winrt::slim_mutex m_criticalSection;
    };
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
    struct PlaceholderImageHelper : PlaceholderImageHelperT<PlaceholderImageHelper, implementation::PlaceholderImageHelper>
    {
    };
} // namespace winrt::Telegram::Native::factory_implementation
