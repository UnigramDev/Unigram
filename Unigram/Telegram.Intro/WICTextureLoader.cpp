//--------------------------------------------------------------------------------------
// File: WICTextureLoader.cpp
//
// Function for loading a WIC image and uploading its data into an ANGLE texture
//
// Based on WICTextureLoader written by Chuck Walbourn.
//
// Note: Assumes application has already called CoInitializeEx
//
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
//--------------------------------------------------------------------------------------

#include "pch.h"

#include <assert.h>
#include <memory>
#include <wincodec.h>
#include <wrl\client.h>

#include "WICTextureLoader.h"

using namespace Microsoft::WRL;

//-------------------------------------------------------------------------------------
// WIC Pixel Format Translation Data
//-------------------------------------------------------------------------------------
struct GLformattype
{
	GLenum format;
	GLenum type;
};

struct WICTranslate
{
	GUID         wic;
	GLformattype formattype;
};

static WICTranslate g_WICFormats[] =
{
	// More formats could be supported here
	{ GUID_WICPixelFormat128bppRGBAFloat,{ GL_RGBA,     GL_FLOAT } },
	{ GUID_WICPixelFormat64bppRGBAHalf,{ GL_RGBA,     GL_HALF_FLOAT_OES } },
	{ GUID_WICPixelFormat32bppRGBA,{ GL_RGBA,     GL_UNSIGNED_BYTE } },
	{ GUID_WICPixelFormat32bppBGRA,{ GL_BGRA_EXT, GL_UNSIGNED_BYTE } },
	{ GUID_WICPixelFormat24bppRGB,{ GL_RGB,      GL_UNSIGNED_BYTE } },
};

//-------------------------------------------------------------------------------------
// WIC Pixel Format nearest conversion table
//-------------------------------------------------------------------------------------

struct WICConvert
{
	GUID        source;
	GUID        target;
};

static WICConvert g_WICConvert[] =
{
	// Note target GUID in this conversion table must be one of those directly supported formats (above).

	{ GUID_WICPixelFormatBlackWhite,           GUID_WICPixelFormat32bppRGBA },

	{ GUID_WICPixelFormat1bppIndexed,          GUID_WICPixelFormat32bppRGBA },
	{ GUID_WICPixelFormat2bppIndexed,          GUID_WICPixelFormat32bppRGBA },
	{ GUID_WICPixelFormat4bppIndexed,          GUID_WICPixelFormat32bppRGBA },
	{ GUID_WICPixelFormat8bppIndexed,          GUID_WICPixelFormat32bppRGBA },

	{ GUID_WICPixelFormat2bppGray,             GUID_WICPixelFormat32bppRGBA },
	{ GUID_WICPixelFormat4bppGray,             GUID_WICPixelFormat32bppRGBA },

	{ GUID_WICPixelFormat16bppGrayFixedPoint,  GUID_WICPixelFormat64bppRGBAHalf },
	{ GUID_WICPixelFormat32bppGrayFixedPoint,  GUID_WICPixelFormat64bppRGBAHalf },

	{ GUID_WICPixelFormat16bppBGR555,          GUID_WICPixelFormat32bppRGBA },
	{ GUID_WICPixelFormat16bppBGRA5551,        GUID_WICPixelFormat32bppRGBA },
	{ GUID_WICPixelFormat16bppBGR565,          GUID_WICPixelFormat32bppRGBA },

	{ GUID_WICPixelFormat32bppBGR101010,       GUID_WICPixelFormat128bppRGBAFloat },

	{ GUID_WICPixelFormat24bppBGR,             GUID_WICPixelFormat24bppRGB },
	{ GUID_WICPixelFormat32bppPBGRA,           GUID_WICPixelFormat32bppBGRA },
	{ GUID_WICPixelFormat32bppPRGBA,           GUID_WICPixelFormat32bppRGBA },

	{ GUID_WICPixelFormat48bppRGB,             GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat48bppBGR,             GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat64bppBGRA,            GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat64bppPRGBA,           GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat64bppPBGRA,           GUID_WICPixelFormat128bppRGBAFloat },

	{ GUID_WICPixelFormat48bppRGBFixedPoint,   GUID_WICPixelFormat64bppRGBAHalf },
	{ GUID_WICPixelFormat48bppBGRFixedPoint,   GUID_WICPixelFormat64bppRGBAHalf },
	{ GUID_WICPixelFormat64bppRGBAFixedPoint,  GUID_WICPixelFormat64bppRGBAHalf },
	{ GUID_WICPixelFormat64bppBGRAFixedPoint,  GUID_WICPixelFormat64bppRGBAHalf },
	{ GUID_WICPixelFormat64bppRGBFixedPoint,   GUID_WICPixelFormat64bppRGBAHalf },
	{ GUID_WICPixelFormat64bppRGBHalf,         GUID_WICPixelFormat64bppRGBAHalf },
	{ GUID_WICPixelFormat48bppRGBHalf,         GUID_WICPixelFormat64bppRGBAHalf },

	{ GUID_WICPixelFormat96bppRGBFixedPoint,   GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat128bppPRGBAFloat,     GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat128bppRGBFloat,       GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat128bppRGBAFixedPoint, GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat128bppRGBFixedPoint,  GUID_WICPixelFormat128bppRGBAFloat },

	{ GUID_WICPixelFormat32bppCMYK,            GUID_WICPixelFormat32bppRGBA },
	{ GUID_WICPixelFormat64bppCMYK,            GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat40bppCMYKAlpha,       GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat80bppCMYKAlpha,       GUID_WICPixelFormat128bppRGBAFloat },

#if (_WIN32_WINNT >= 0x0602 /*_WIN32_WINNT_WIN8*/)
	{ GUID_WICPixelFormat32bppRGB,             GUID_WICPixelFormat32bppRGBA },
	{ GUID_WICPixelFormat64bppRGB,             GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat64bppPRGBAHalf,       GUID_WICPixelFormat64bppRGBAHalf },
#endif

	{ GUID_WICPixelFormat64bppRGBA,            GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat32bppBGR,             GUID_WICPixelFormat32bppBGRA },

	{ GUID_WICPixelFormat32bppGrayFloat,       GUID_WICPixelFormat128bppRGBAFloat },
	{ GUID_WICPixelFormat16bppGrayHalf,        GUID_WICPixelFormat64bppRGBAHalf },
	{ GUID_WICPixelFormat16bppGray,            GUID_WICPixelFormat32bppRGBA },
	{ GUID_WICPixelFormat8bppGray,             GUID_WICPixelFormat32bppRGBA },

	{ GUID_WICPixelFormat8bppAlpha,            GUID_WICPixelFormat32bppRGBA },

#if (_WIN32_WINNT >= 0x0602 /*_WIN32_WINNT_WIN8*/)
	{ GUID_WICPixelFormat96bppRGBFloat,        GUID_WICPixelFormat128bppRGBAFloat },
#endif

	// We don't support n-channel formats
};

static bool g_WIC2 = false;

//--------------------------------------------------------------------------------------
IWICImagingFactory* _GetWIC()
{
	static IWICImagingFactory* s_Factory = nullptr;

	if (s_Factory)
		return s_Factory;

#if(_WIN32_WINNT >= _WIN32_WINNT_WIN8) || defined(_WIN7_PLATFORM_UPDATE)
	HRESULT hr = CoCreateInstance(
		CLSID_WICImagingFactory2,
		nullptr,
		CLSCTX_INPROC_SERVER,
		__uuidof(IWICImagingFactory2),
		(LPVOID*)&s_Factory
	);

	if (SUCCEEDED(hr))
	{
		// WIC2 is available on Windows 8 and Windows 7 SP1 with KB 2670838 installed
		g_WIC2 = true;
	}
	else
	{
		hr = CoCreateInstance(
			CLSID_WICImagingFactory1,
			nullptr,
			CLSCTX_INPROC_SERVER,
			__uuidof(IWICImagingFactory),
			(LPVOID*)&s_Factory
		);

		if (FAILED(hr))
		{
			s_Factory = nullptr;
			return nullptr;
		}
	}
#else
	HRESULT hr = CoCreateInstance(
		CLSID_WICImagingFactory,
		nullptr,
		CLSCTX_INPROC_SERVER,
		__uuidof(IWICImagingFactory),
		(LPVOID*)&s_Factory
	);

	if (FAILED(hr))
	{
		s_Factory = nullptr;
		return nullptr;
	}
#endif

	return s_Factory;
}

//---------------------------------------------------------------------------------
static GLformattype _WICToGL(const GUID& guid)
{
	for (size_t i = 0; i < _countof(g_WICFormats); ++i)
	{
		if (memcmp(&g_WICFormats[i].wic, &guid, sizeof(GUID)) == 0)
			return g_WICFormats[i].formattype;
	}

	return{ GL_NONE, GL_UNSIGNED_BYTE };
}

//---------------------------------------------------------------------------------
static size_t _WICBitsPerPixel(REFGUID targetGuid)
{
	IWICImagingFactory* pWIC = _GetWIC();
	if (!pWIC)
		return 0;

	ComPtr<IWICComponentInfo> cinfo;
	if (FAILED(pWIC->CreateComponentInfo(targetGuid, &cinfo)))
		return 0;

	WICComponentType type;
	if (FAILED(cinfo->GetComponentType(&type)))
		return 0;

	if (type != WICPixelFormat)
		return 0;

	ComPtr<IWICPixelFormatInfo> pfinfo;
	if (FAILED(cinfo.As(&pfinfo)))
		return 0;

	UINT bpp;
	if (FAILED(pfinfo->GetBitsPerPixel(&bpp)))
		return 0;

	return bpp;
}

static HRESULT CopyPixelsHelper(IWICBitmapSource* source, const WICRect *prc, UINT cbStride, UINT cbBufferSize, BYTE *pbBuffer)
{
	HRESULT hr = S_OK;

	IWICImagingFactory* pWIC = _GetWIC();
	if (!pWIC)
		return E_NOINTERFACE;

	hr = source->CopyPixels(0, cbStride, cbBufferSize, pbBuffer);
	if (FAILED(hr))
		return hr;

	return hr;
}

//---------------------------------------------------------------------------------
static HRESULT TexImage2DFromWIC(GLenum target, GLint level, IWICBitmapFrameDecode *frame)
{
	UINT width, height;
	HRESULT hr = frame->GetSize(&width, &height);
	if (FAILED(hr))
		return hr;

	assert(width > 0 && height > 0);

	GLint imaxsize;
	glGetIntegerv(GL_MAX_TEXTURE_SIZE, &imaxsize);
	assert(imaxsize > 0);
	UINT maxsize = static_cast<UINT>(imaxsize);

	UINT twidth, theight;
	if (width > maxsize || height > maxsize)
	{
		float ar = static_cast<float>(height) / static_cast<float>(width);
		if (width > height)
		{
			twidth = static_cast<UINT>(maxsize);
			theight = static_cast<UINT>(static_cast<float>(maxsize) * ar);
		}
		else
		{
			theight = static_cast<UINT>(maxsize);
			twidth = static_cast<UINT>(static_cast<float>(maxsize) / ar);
		}
		assert(twidth <= maxsize && theight <= maxsize);
	}
	else
	{
		twidth = width;
		theight = height;
	}

	// Determine format
	WICPixelFormatGUID pixelFormat;
	hr = frame->GetPixelFormat(&pixelFormat);
	if (FAILED(hr))
		return hr;


	WICPixelFormatGUID convertGUID;
	memcpy(&convertGUID, &pixelFormat, sizeof(WICPixelFormatGUID));

	convertGUID = GUID_WICPixelFormat32bppPBGRA;

	size_t bpp = 0;

	GLformattype formattype = _WICToGL(pixelFormat);
	if (formattype.format == GL_NONE)
	{
		for (size_t i = 0; i < _countof(g_WICConvert); ++i)
		{
			if (memcmp(&g_WICConvert[i].source, &pixelFormat, sizeof(WICPixelFormatGUID)) == 0)
			{
				memcpy(&convertGUID, &g_WICConvert[i].target, sizeof(WICPixelFormatGUID));

				formattype = _WICToGL(g_WICConvert[i].target);
				assert(formattype.format != GL_NONE);
				bpp = _WICBitsPerPixel(convertGUID);
				break;
			}
		}

		if (formattype.format == GL_NONE)
			return HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED);
	}
	else
	{
		bpp = _WICBitsPerPixel(pixelFormat);
	}

	if (!bpp)
		return E_FAIL;

	// Allocate temporary memory for image
	size_t rowPitch = (twidth * bpp + 7) / 8;
	size_t imageSize = rowPitch * theight;

	std::unique_ptr<uint8_t[]> temp(new uint8_t[imageSize]);

	// Load image data
	if (memcmp(&convertGUID, &pixelFormat, sizeof(GUID)) == 0
		&& twidth == width
		&& theight == height)
	{
		// No format conversion or resize needed
		hr = CopyPixelsHelper(frame, 0, static_cast<UINT>(rowPitch), static_cast<UINT>(imageSize), temp.get());
		if (FAILED(hr))
			return hr;
	}
	else if (twidth != width || theight != height)
	{
		// Resize
		IWICImagingFactory* pWIC = _GetWIC();
		if (!pWIC)
			return E_NOINTERFACE;

		ComPtr<IWICBitmapScaler> scaler;
		hr = pWIC->CreateBitmapScaler(&scaler);
		if (FAILED(hr))
			return hr;

		hr = scaler->Initialize(frame, twidth, theight, WICBitmapInterpolationModeFant);
		if (FAILED(hr))
			return hr;

		WICPixelFormatGUID pfScaler;
		hr = scaler->GetPixelFormat(&pfScaler);
		if (FAILED(hr))
			return hr;

		if (memcmp(&convertGUID, &pfScaler, sizeof(GUID)) == 0)
		{
			hr = CopyPixelsHelper(scaler.Get(), 0, static_cast<UINT>(rowPitch), static_cast<UINT>(imageSize), temp.get());
			if (FAILED(hr))
				return hr;
		}
		else
		{
			ComPtr<IWICFormatConverter> FC;
			hr = pWIC->CreateFormatConverter(&FC);
			if (FAILED(hr))
				return hr;

			BOOL canConvert = FALSE;
			hr = FC->CanConvert(pfScaler, convertGUID, &canConvert);
			if (FAILED(hr) || !canConvert)
			{
				return E_UNEXPECTED;
			}

			hr = FC->Initialize(scaler.Get(), convertGUID, WICBitmapDitherTypeErrorDiffusion, 0, 0, WICBitmapPaletteTypeCustom);
			if (FAILED(hr))
				return hr;

			hr = CopyPixelsHelper(FC.Get(), 0, static_cast<UINT>(rowPitch), static_cast<UINT>(imageSize), temp.get());
			if (FAILED(hr))
				return hr;
		}
	}
	else
	{
		// Format conversion but no resize
		IWICImagingFactory* pWIC = _GetWIC();
		if (!pWIC)
			return E_NOINTERFACE;

		ComPtr<IWICFormatConverter> FC;
		hr = pWIC->CreateFormatConverter(&FC);
		if (FAILED(hr))
			return hr;

		BOOL canConvert = FALSE;
		hr = FC->CanConvert(pixelFormat, convertGUID, &canConvert);
		if (FAILED(hr) || !canConvert)
		{
			return E_UNEXPECTED;
		}

		hr = FC->Initialize(frame, convertGUID, WICBitmapDitherTypeErrorDiffusion, 0, 0, WICBitmapPaletteTypeCustom);
		if (FAILED(hr))
			return hr;

		hr = CopyPixelsHelper(FC.Get(), 0, static_cast<UINT>(rowPitch), static_cast<UINT>(imageSize), temp.get());
		if (FAILED(hr))
			return hr;
	}

	glTexImage2D(target, level, formattype.format, twidth, theight, 0, formattype.format, formattype.type, temp.get());
	if (glGetError() != GL_NO_ERROR)
	{
		return E_FAIL;
	}

	return hr;
}

//---------------------------------------------------------------------------------
HRESULT WICTexImage2DFromFile(GLenum target, GLint level, const wchar_t* fileName)
{
	if (!fileName)
	{
		return E_INVALIDARG;
	}

	IWICImagingFactory* pWIC = _GetWIC();
	if (!pWIC)
		return E_NOINTERFACE;

	// Initialize WIC
	ComPtr<IWICBitmapDecoder> decoder;
	HRESULT hr = pWIC->CreateDecoderFromFilename(fileName, 0, GENERIC_READ, WICDecodeMetadataCacheOnDemand, &decoder);
	if (FAILED(hr))
		return hr;

	ComPtr<IWICBitmapFrameDecode> frame;
	hr = decoder->GetFrame(0, &frame);
	if (FAILED(hr))
		return hr;

	return TexImage2DFromWIC(target, level, frame.Get());
}