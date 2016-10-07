#pragma once
#include <mfobjects.h>
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

