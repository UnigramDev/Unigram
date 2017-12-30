// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "Helpers\COMHelper.h"
#include "BufferLock.h"

BufferLock::BufferLock(IMFSample* sample) :
	m_buffer(nullptr),
	m_length(0)
{
	if (SUCCEEDED(sample->ConvertToContiguousBuffer(&m_mediaBuffer)))
	{
		m_mediaBuffer->Lock(&m_buffer, nullptr, &m_length);
	}
}

BufferLock::BufferLock(IMFMediaBuffer* buffer) :
	m_mediaBuffer(buffer),
	m_buffer(nullptr),
	m_length(0)
{
	m_mediaBuffer->Lock(&m_buffer, nullptr, &m_length);
}

BufferLock::~BufferLock()
{
	m_mediaBuffer->Unlock();
}


BufferLock2D::BufferLock2D(IMF2DBuffer* buffer) :
	m_mediaBuffer(buffer),
	m_scanLine(nullptr),
	m_pitch(0)
{
	m_mediaBuffer->Lock2D(&m_scanLine, &m_pitch);
}

BufferLock2D::~BufferLock2D()
{
	m_mediaBuffer->Unlock2D();
}


BitmapLock::BitmapLock(D2D1_MAP_OPTIONS options, ID2D1Bitmap1* bitmap) :
	m_bitmap(bitmap),
	m_mappedRectangle({})
{
	m_bitmap->Map(options, &m_mappedRectangle);
}

BitmapLock::~BitmapLock()
{
	m_bitmap->Unmap();
}