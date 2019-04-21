#pragma once

#include <iostream>  
#include <iomanip>
#include <sstream>
#include <vector>
#include <windows.h>
#include "Shlwapi.h"
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

		public ref class MrzRecognizer sealed
		{
		public:
			static Array<int>^ FindCornerPoints(IRandomAccessStream^ bitmap);
			static IVector<IVector<Windows::Foundation::Rect>^>^ BinarizeAndFindCharacters(IRandomAccessStream^ inBmp, String^* mrz);

		private:
			static HRESULT FindCornerPointsInternal(IRandomAccessStream^ bitmap, Array<int>^* points);
			static HRESULT BinarizeAndFindCharactersInternal(IRandomAccessStream^ inBmp, String^* mrz, IVector<IVector<Windows::Foundation::Rect>^>^* outCharRects);
			static HRESULT PerformRecognition(IVector<IVector<Windows::Foundation::Rect>^>^ characters, ComPtr<IWICBitmap> bitmap, String^* mrz);

			static HRESULT CreateBitmapFromRandomAccessStream(IRandomAccessStream^ bitmap, WICPixelFormatGUID desired, IWICBitmap** outBitmap);
			static HRESULT CreateBitmap(UINT width, UINT height, WICPixelFormatGUID desired, IWICBitmap** outBitmap);
		};

	}
}