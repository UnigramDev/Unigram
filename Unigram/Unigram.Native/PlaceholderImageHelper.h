#pragma once

#include "PlaceholderImageHelper.g.h"

#include <ppl.h>
#include <wincodec.h>
#include <Dwrite_1.h>
#include <D2d1_3.h>
#include <map>

#include <winrt/Windows.UI.h>
#include <winrt/Windows.Storage.Streams.h>

using namespace concurrency;
using namespace winrt::Windows::UI;
using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Storage::Streams;

namespace winrt::Unigram::Native::implementation
{
	struct QrData {
		int size = 0;
		std::vector<bool> values; // size x size
	};

	struct PlaceholderImageHelper : PlaceholderImageHelperT<PlaceholderImageHelper>
	{
	public:
		PlaceholderImageHelper();

		//static PlaceholderImageHelper GetForCurrentView();

		static winrt::Unigram::Native::PlaceholderImageHelper Current()
		{
			auto lock = critical_section::scoped_lock(s_criticalSection);

			if (s_current == nullptr) {
				s_current = winrt::make_self<PlaceholderImageHelper>();
			}

			return s_current.as<winrt::Unigram::Native::PlaceholderImageHelper>();
		}

		void DrawWebP(hstring fileName, IRandomAccessStream randomAccessStream);

		Windows::Foundation::Size DrawSvg(hstring path, _In_ Color foreground, IRandomAccessStream randomAccessStream);
		void DrawQr(hstring data, _In_ Color foreground, _In_ Color background, IRandomAccessStream randomAccessStream);
		void DrawIdenticon(_In_ IVector<uint8_t> hash, _In_ int side, _In_ IRandomAccessStream randomAccessStream);

		void DrawGlyph(hstring glyph, _In_ Color top, _In_ Color bottom, IRandomAccessStream randomAccessStream);
		void DrawSavedMessages(_In_ Color top, _In_ Color bottom, IRandomAccessStream randomAccessStream);
		void DrawDeletedUser(_In_ Color top, _In_ Color bottom, IRandomAccessStream randomAccessStream);
		void DrawProfilePlaceholder(hstring text, _In_ Color top, _In_ Color bottom, _In_ IRandomAccessStream randomAccessStream);
			
		void DrawThumbnailPlaceholder(hstring fileName, float blurAmount, _In_ IRandomAccessStream randomAccessStream);

	//internal:
	//	PlaceholderImageHelper();

	private:
		//PlaceholderImageHelper();

		HRESULT InternalDrawSvg(hstring data, _In_ Color foreground, _In_ IRandomAccessStream randomAccessStream, _Out_ Windows::Foundation::Size& size);
		HRESULT InternalDrawQr(hstring data, _In_ Color foreground, _In_ Color background, _In_ IRandomAccessStream randomAccessStream);
		HRESULT InternalDrawIdenticon(_In_ IVector<uint8_t> hash, _In_ int side, _In_ IRandomAccessStream randomAccessStream);
		HRESULT InternalDrawGlyph(hstring glyph, Color top, _In_ Color bottom, IRandomAccessStream randomAccessStream);
		HRESULT InternalDrawSavedMessages(Color top, _In_ Color bottom, IRandomAccessStream randomAccessStream);
		HRESULT InternalDrawDeletedUser(Color top, _In_ Color bottom, IRandomAccessStream randomAccessStream);
		HRESULT InternalDrawProfilePlaceholder(hstring text, Color top, _In_ Color bottom, _In_ IRandomAccessStream randomAccessStream);
		HRESULT InternalDrawThumbnailPlaceholder(hstring fileName, float blurAmount, _In_ IRandomAccessStream randomAccessStream);
		HRESULT InternalDrawThumbnailPlaceholder(_In_ IWICBitmapSource* wicBitmapSource, float blurAmount, _In_ IRandomAccessStream randomAccessStream);
		HRESULT SaveImageToStream(_In_ ID2D1Image* image, _In_ REFGUID wicFormat, _In_ IRandomAccessStream randomAccessStream);
		HRESULT MeasureText(_In_ const wchar_t* text, _In_ IDWriteTextFormat* format, _Out_ DWRITE_TEXT_METRICS* textMetrics);
		HRESULT CreateDeviceIndependentResources();
		HRESULT CreateDeviceResources();

	private:
		//static std::map<int, WeakReference> s_windowContext;

		static critical_section s_criticalSection;
		static winrt::com_ptr<PlaceholderImageHelper> s_current;

		winrt::com_ptr<ID2D1Factory1> m_d2dFactory;
		winrt::com_ptr<ID2D1Device> m_d2dDevice;
		winrt::com_ptr<ID2D1DeviceContext2> m_d2dContext;
		D3D_FEATURE_LEVEL m_featureLevel;
		winrt::com_ptr<IWICImagingFactory2> m_wicFactory;
		winrt::com_ptr<IWICImageEncoder> m_imageEncoder;
		winrt::com_ptr<IDWriteFactory1> m_dwriteFactory;
		winrt::com_ptr<IDWriteFontCollectionLoader> m_customLoader;
		winrt::com_ptr<IDWriteFontCollection> m_fontCollection;
		winrt::com_ptr<IDWriteTextFormat> m_symbolFormat;
		winrt::com_ptr<IDWriteTextFormat> m_mdl2Format;
		winrt::com_ptr<IDWriteTextFormat> m_textFormat;
		winrt::com_ptr<ID2D1SolidColorBrush> m_textBrush;
		winrt::com_ptr<ID2D1SolidColorBrush> m_black;
		winrt::com_ptr<ID2D1SolidColorBrush> m_transparent;
		std::vector<winrt::com_ptr<ID2D1SolidColorBrush>> m_identiconBrushes;
		winrt::com_ptr<ID2D1Effect> m_gaussianBlurEffect;
		winrt::com_ptr<ID2D1Bitmap1> m_targetBitmap;
		critical_section m_criticalSection;
	};
} // namespace winrt::Unigram::Native::implementation

namespace winrt::Unigram::Native::factory_implementation
{
	struct PlaceholderImageHelper : PlaceholderImageHelperT<PlaceholderImageHelper, implementation::PlaceholderImageHelper>
	{
	};
} // namespace winrt::Unigram::Native::factory_implementation
