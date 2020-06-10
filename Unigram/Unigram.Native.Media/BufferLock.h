// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <mfobjects.h>
#include <d2d1_1.h> 
#include <d2d1_2.h>
#include <dxgi.h> 
#include <dxgi1_2.h> 
#include <wrl.h>

using namespace Microsoft::WRL;

class BufferLock sealed
{
public:
	BufferLock(_In_ IMFSample* sample);
	BufferLock(_In_ IMFMediaBuffer* buffer);
	~BufferLock();

	inline IMFMediaBuffer* GetMediaBuffer() const
	{
		return m_mediaBuffer.Get();
	}

	inline bool IsValid() const
	{
		return m_buffer != nullptr;
	}

	inline byte* GetBuffer() const
	{
		return m_buffer;
	}

	inline DWORD GetLength() const
	{
		return m_length;
	}

private:
	ComPtr<IMFMediaBuffer> m_mediaBuffer;
	byte* m_buffer;
	DWORD m_length;
};

class BufferLock2D sealed
{
public:
	BufferLock2D(_In_ IMF2DBuffer* buffer);
	~BufferLock2D();

	inline IMF2DBuffer* GetMediaBuffer() const
	{
		return m_mediaBuffer.Get();
	}

	inline bool IsValid() const
	{
		return m_scanLine != nullptr;
	}

	inline byte* GetScanLine() const
	{
		return m_scanLine;
	}

	inline LONG GetPitch() const
	{
		return m_pitch;
	}

private:
	ComPtr<IMF2DBuffer> m_mediaBuffer;
	byte* m_scanLine;
	LONG m_pitch;
};

class BitmapLock sealed
{
public:
	BitmapLock(D2D1_MAP_OPTIONS options, _In_ ID2D1Bitmap1* bitmap);
	~BitmapLock();

	inline ID2D1Bitmap1* GetBitmap() const
	{
		return m_bitmap.Get();
	}

	inline bool IsValid() const
	{
		return m_mappedRectangle.bits != nullptr;
	}

	inline byte* GetScanLine() const
	{
		return m_mappedRectangle.bits;
	}

	inline LONG GetPitch() const
	{
		return m_mappedRectangle.pitch;
	}

private:
	ComPtr<ID2D1Bitmap1> m_bitmap;
	D2D1_MAPPED_RECT m_mappedRectangle;
};