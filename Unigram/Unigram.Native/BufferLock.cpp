#include "pch.h"
#include "Helpers\COMHelper.h"
#include "BufferLock.h"

BufferLock::BufferLock(IMFSample* sample)
{
	ThrowIfFailed(sample->ConvertToContiguousBuffer(&m_mediaBuffer));
	ThrowIfFailed(m_mediaBuffer->Lock(&m_buffer, nullptr, &m_length));
}

BufferLock::BufferLock(IMFMediaBuffer* buffer) :
	m_mediaBuffer(buffer)
{
	ThrowIfFailed(m_mediaBuffer->Lock(&m_buffer, nullptr, &m_length));
}

BufferLock::~BufferLock()
{
	ThrowIfFailed(m_mediaBuffer->Unlock());
}