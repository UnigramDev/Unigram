// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "ReadFramesAsyncOperation.h"
#include "Helpers\COMHelper.h"

using namespace Unigram::Native;

HRESULT ReadFramesAsyncOperation::RuntimeClassInitialize(const D2D1_SIZE_U& maximumFrameSize, Windows::Foundation::Uri^ uri)
{
	HRESULT result;
	ComPtr<IMFAttributes> attributes;
	ReturnIfFailed(result, CreateSourceReaderAttributes(&attributes));
	ReturnIfFailed(result, MFCreateSourceReaderFromURL(uri->RawUri->Data(), attributes.Get(), &m_sourceReader));

	return RuntimeClassInitialize(maximumFrameSize);
}

HRESULT ReadFramesAsyncOperation::RuntimeClassInitialize(const D2D1_SIZE_U& maximumFrameSize, Windows::Storage::Streams::IRandomAccessStream^ stream)
{
	HRESULT result;
	ComPtr<IMFByteStream> spMFByteStream;
	ReturnIfFailed(result, MFCreateMFByteStreamOnStreamEx(reinterpret_cast<IUnknown*>(stream), &spMFByteStream));

	ComPtr<IMFAttributes> attributes;
	ReturnIfFailed(result, CreateSourceReaderAttributes(&attributes));
	ReturnIfFailed(result, MFCreateSourceReaderFromByteStream(spMFByteStream.Get(), attributes.Get(), &m_sourceReader));

	return RuntimeClassInitialize(maximumFrameSize);
}

HRESULT ReadFramesAsyncOperation::RuntimeClassInitialize(const D2D1_SIZE_U& maximumFrameSize, Windows::Media::Core::IMediaSource^ mediaSource)
{
	HRESULT result;
	ComPtr<IMFMediaSource> mfMediaSource;
	ReturnIfFailed(result, MFGetService(reinterpret_cast<IUnknown*>(mediaSource), MF_MEDIASOURCE_SERVICE, IID_PPV_ARGS(&mfMediaSource)));

	ComPtr<IMFAttributes> attributes;
	ReturnIfFailed(result, CreateSourceReaderAttributes(&attributes));
	ReturnIfFailed(result, MFCreateSourceReaderFromMediaSource(mfMediaSource.Get(), attributes.Get(), &m_sourceReader));

	return RuntimeClassInitialize(maximumFrameSize);
}

HRESULT ReadFramesAsyncOperation::CreateSourceReaderAttributes(IMFAttributes** ppAttributes)
{
	HRESULT result;
	ComPtr<IMFAttributes> attributes;
	ReturnIfFailed(result, MFCreateAttributes(&attributes, 2));
	ReturnIfFailed(result, attributes->SetUINT32(MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING, TRUE));
	ReturnIfFailed(result, attributes->SetUnknown(MF_SOURCE_READER_ASYNC_CALLBACK, CastToUnknown()));

	*ppAttributes = attributes.Detach();
	return S_OK;
}

HRESULT ReadFramesAsyncOperation::RuntimeClassInitialize(const D2D1_SIZE_U& maximumFrameSize)
{
	m_frameSize = maximumFrameSize;

	HRESULT result;
	ReturnIfFailed(result, m_sourceReader->SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, FALSE));

	ComPtr<IMFMediaType> nativeMediaType;
	ReturnIfFailed(result, m_sourceReader->GetNativeMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, &nativeMediaType));

	ComPtr<IMFMediaType> mediaType;
	ReturnIfFailed(result, CreateUncompressedMediaType(nativeMediaType.Get(), MFVideoFormat_ARGB32, &m_frameSize, &mediaType));
	ReturnIfFailed(result, m_sourceReader->SetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, nullptr, mediaType.Get()));
	ReturnIfFailed(result, m_sourceReader->SetStreamSelection(MF_SOURCE_READER_FIRST_VIDEO_STREAM, TRUE));

	return MakeAndInitialize<FramesCacheStore>(&m_framesCacheStore);
}

task<ComPtr<FramesCacheStore>> ReadFramesAsyncOperation::Start(cancellation_token& ct)
{
	auto task = create_task(m_taskCompletionEvent, task_options(ct, task_continuation_context::use_arbitrary()));

	HRESULT result;
	if (FAILED(result = m_sourceReader->ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, nullptr, nullptr, nullptr, nullptr)))
	{
		m_taskCompletionEvent.set_exception(Exception::CreateException(result));
	}

	auto cancellationTokenRegistration = ct.register_callback([this]
	{
		auto lock = m_criticalSection.Lock();

		if (m_sourceReader != nullptr)
		{
			m_sourceReader->Flush(MF_SOURCE_READER_FIRST_VIDEO_STREAM);
		}
	});

	return task.then([ct, cancellationTokenRegistration](auto& result)
	{
		ct.deregister_callback(cancellationTokenRegistration);
		return result;
	});
}

HRESULT ReadFramesAsyncOperation::OnReadSample(HRESULT result, DWORD dwStreamIndex, DWORD dwStreamFlags,
	LONGLONG llTimestamp, IMFSample* pSample)
{
	auto lock = m_criticalSection.Lock();

	do
	{
		if (!(dwStreamFlags &  MF_SOURCE_READERF_STREAMTICK) && pSample != nullptr)
		{
			BufferLock bufferLock(pSample);
			if (!bufferLock.IsValid())
			{
				result = MF_E_NO_VIDEO_SAMPLE_AVAILABLE;
				break;
			}

			LONGLONG delay;
			if (FAILED(pSample->GetSampleDuration(&delay)))
			{
				delay = 10000000;
			}

			BreakIfFailed(result, m_framesCacheStore->WriteBitmapEntry(bufferLock.GetBuffer(),
				bufferLock.GetLength(), m_frameSize.width * sizeof(DWORD), delay));
		}

		if (dwStreamFlags & MF_SOURCE_READERF_ENDOFSTREAM)
		{
			BreakIfFailed(result, m_framesCacheStore->Lock());

			m_sourceReader.Reset();
			m_taskCompletionEvent.set(m_framesCacheStore);
			return S_OK;
		}

		if (!(dwStreamFlags &  MF_SOURCE_READERF_ERROR))
		{
			BreakIfFailed(result, m_sourceReader->ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM,
				0, nullptr, nullptr, nullptr, nullptr));
		}
	} while (false);

	if (FAILED(result))
	{
		m_sourceReader->Flush(MF_SOURCE_READER_FIRST_VIDEO_STREAM);
		m_taskCompletionEvent.set_exception(Exception::CreateException(result));
	}

	return S_OK;
}

HRESULT ReadFramesAsyncOperation::OnFlush(DWORD dwStreamIndex)
{
	auto lock = m_criticalSection.Lock();

	m_sourceReader.Reset();
	m_taskCompletionEvent.set(nullptr);
	return S_OK;
}

HRESULT ReadFramesAsyncOperation::OnEvent(DWORD dwStreamIndex, IMFMediaEvent* pEvent)
{
	return S_OK;
}

HRESULT ReadFramesAsyncOperation::CreateUncompressedMediaType(IMFMediaType* pType, const GUID& subtype,
	D2D1_SIZE_U* frameSize, IMFMediaType** ppType)
{
	HRESULT result;
	GUID majorType;
	ReturnIfFailed(result, pType->GetMajorType(&majorType));
	if (majorType != MFMediaType_Video)
	{
		return MF_E_INVALIDMEDIATYPE;
	}

	ComPtr<IMFMediaType> uncompressedType;
	ReturnIfFailed(result, MFCreateMediaType(&uncompressedType));
	ReturnIfFailed(result, pType->CopyAllItems(uncompressedType.Get()));
	ReturnIfFailed(result, uncompressedType->SetGUID(MF_MT_SUBTYPE, subtype));
	ReturnIfFailed(result, uncompressedType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE));

	UINT32 width;
	UINT32 height;
	ReturnIfFailed(result, MFGetAttributeSize(pType, MF_MT_FRAME_SIZE, &width, &height));
	if (width > frameSize->width || height > frameSize->height)
	{
		if (width > height)
		{
			frameSize->height = static_cast<UINT32>(static_cast<float>(frameSize->width  * height) / static_cast<float>(width));
		}
		else
		{
			frameSize->width = static_cast<UINT32>(static_cast<float>(frameSize->height * width) / static_cast<float>(height));
		}
	}
	else
	{
		frameSize->width = width;
		frameSize->height = height;
	}

	ReturnIfFailed(result, MFSetAttributeSize(uncompressedType.Get(), MF_MT_FRAME_SIZE, frameSize->width, frameSize->height));

	if (FAILED(MFGetAttributeRatio(uncompressedType.Get(), MF_MT_PIXEL_ASPECT_RATIO, &width, &height)))
	{
		ReturnIfFailed(result, MFSetAttributeRatio(uncompressedType.Get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1));
	}

	*ppType = uncompressedType.Detach();
	return S_OK;
}