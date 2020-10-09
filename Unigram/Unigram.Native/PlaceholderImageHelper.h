#pragma once
#include <ppl.h>
#include <wincodec.h>
#include <Dwrite_1.h>
#include <D2d1_3.h>
#include <map>
#include "Helpers\COMHelper.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using namespace Windows::UI;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Storage::Streams;
using namespace Platform;

namespace Unigram
{
	namespace Native
	{
		struct QrData {
			int size = 0;
			std::vector<bool> values; // size x size
		};

		public ref class PlaceholderImageHelper sealed
		{
		public:
			static PlaceholderImageHelper^ GetForCurrentView();

			static property PlaceholderImageHelper^ Current
			{
				PlaceholderImageHelper^ get() {
					auto lock = s_criticalSection.Lock();

					if (s_current == nullptr) {
						s_current = ref new PlaceholderImageHelper();
					}

					return s_current;
				}
			}

			Windows::Foundation::Size DrawSvg(_In_ String^ path, _In_ Color foreground, IRandomAccessStream^ randomAccessStream);
			void DrawQr(_In_ String^ data, _In_ Color foreground, _In_ Color background, IRandomAccessStream^ randomAccessStream);
			void DrawIdenticon(_In_ IVector<uint8>^ hash, _In_ int side, _In_ IRandomAccessStream^ randomAccessStream);
			void DrawGlyph(_In_ String^ glyph, _In_ Color clear, IRandomAccessStream^ randomAccessStream);
			void DrawSavedMessages(_In_ Color clear, IRandomAccessStream^ randomAccessStream);
			void DrawDeletedUser(_In_ Color clear, IRandomAccessStream^ randomAccessStream);
			void DrawProfilePlaceholder(_In_ Color clear, _In_ Platform::String^ text, _In_ IRandomAccessStream^ randomAccessStream);
			void DrawThumbnailPlaceholder(_In_ Platform::String^ fileName, float blurAmount, _In_ IRandomAccessStream^ randomAccessStream);

		internal:
			PlaceholderImageHelper();

		private:
			HRESULT InternalDrawSvg(_In_ String^ data, _In_ Color foreground, _In_ IRandomAccessStream^ randomAccessStream, _Out_ Windows::Foundation::Size& size);
			HRESULT InternalDrawQr(_In_ String^ data, _In_ Color foreground, _In_ Color background, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT InternalDrawIdenticon(_In_ IVector<uint8>^ hash, _In_ int side, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT InternalDrawGlyph(String^ glyph, Color clear, IRandomAccessStream^ randomAccessStream);
			HRESULT InternalDrawSavedMessages(Color clear, IRandomAccessStream^ randomAccessStream);
			HRESULT InternalDrawDeletedUser(Color clear, IRandomAccessStream^ randomAccessStream);
			HRESULT InternalDrawProfilePlaceholder(Color clear, _In_ Platform::String^ text, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT InternalDrawThumbnailPlaceholder(_In_ Platform::String^ fileName, float blurAmount, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT InternalDrawThumbnailPlaceholder(_In_ IWICBitmapSource* wicBitmapSource, float blurAmount, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT SaveImageToStream(_In_ ID2D1Image* image, _In_ REFGUID wicFormat, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT MeasureText(_In_ const wchar_t* text, _In_ IDWriteTextFormat* format, _Out_ DWRITE_TEXT_METRICS* textMetrics);
			HRESULT CreateDeviceIndependentResources();
			HRESULT CreateDeviceResources();

		private:
			static std::map<int, WeakReference> s_windowContext;

			static CriticalSection s_criticalSection;
			static PlaceholderImageHelper^ s_current;

			ComPtr<ID2D1Factory1> m_d2dFactory;
			ComPtr<ID2D1Device> m_d2dDevice;
			ComPtr<ID2D1DeviceContext2> m_d2dContext;
			D3D_FEATURE_LEVEL m_featureLevel;
			ComPtr<IWICImagingFactory2> m_wicFactory;
			ComPtr<IWICImageEncoder> m_imageEncoder;
			ComPtr<IDWriteFactory1> m_dwriteFactory;
			ComPtr<IDWriteFontCollectionLoader> m_customLoader;
			ComPtr<IDWriteFontCollection> m_fontCollection;
			ComPtr<IDWriteTextFormat> m_symbolFormat;
			ComPtr<IDWriteTextFormat> m_mdl2Format;
			ComPtr<IDWriteTextFormat> m_textFormat;
			ComPtr<ID2D1SolidColorBrush> m_textBrush;
			ComPtr<ID2D1SolidColorBrush> m_black;
			ComPtr<ID2D1SolidColorBrush> m_transparent;
			std::vector<ComPtr<ID2D1SolidColorBrush>> m_identiconBrushes;
			ComPtr<ID2D1Effect> m_gaussianBlurEffect;
			ComPtr<ID2D1Bitmap1> m_targetBitmap;
			CriticalSection m_criticalSection;
		};

	}
}