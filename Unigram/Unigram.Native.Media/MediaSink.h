// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <queue>
#include <memory>
#include <windows.media.core.h>
#include <windows.media.h>
#include <windows.foundation.h>
#include <Wincodec.h>
#include <mfobjects.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <Mferror.h>
#include <mfapi.h>
#include <wrl.h>
#include <wrl\wrappers\corewrappers.h>
#include "MediaFoundationExtensions.h"

using namespace Microsoft::WRL;

namespace Unigram
{
	namespace Native
	{

		class MediaSink abstract : public Implements<RuntimeClassFlags<RuntimeClassType::WinRtClassicComMix>,
			ABI::Windows::Media::IMediaExtension, ABI::Windows::Foundation::IClosable, IMFMediaSink, IMFClockStateSink, IMFFinalizableMediaSink>
		{
			friend class StreamSink;

		public:
			MediaSink();
			virtual ~MediaSink();

			IFACEMETHODIMP AddStreamSink(DWORD dwStreamSinkIdentifier, IMFMediaType* pMediaType, IMFStreamSink** ppStreamSink);
			IFACEMETHODIMP GetCharacteristics(DWORD* pdwCharacteristics);
			IFACEMETHODIMP GetPresentationClock(IMFPresentationClock** ppPresentationClock);
			IFACEMETHODIMP GetStreamSinkById(DWORD dwIdentifier, IMFStreamSink** ppStreamSink);
			IFACEMETHODIMP GetStreamSinkByIndex(DWORD dwIndex, IMFStreamSink** ppStreamSink);
			IFACEMETHODIMP GetStreamSinkCount(DWORD* pcStreamSinkCount);
			IFACEMETHODIMP RemoveStreamSink(DWORD dwStreamSinkIdentifier);
			IFACEMETHODIMP SetPresentationClock(IMFPresentationClock* pPresentationClock);
			IFACEMETHODIMP Shutdown();
			IFACEMETHODIMP OnClockPause(MFTIME hnsSystemTime);
			IFACEMETHODIMP OnClockRestart(MFTIME hnsSystemTime);
			IFACEMETHODIMP OnClockSetRate(MFTIME hnsSystemTime, float flRate);
			IFACEMETHODIMP OnClockStart(MFTIME hnsSystemTime, LONGLONG llClockStartOffset);
			IFACEMETHODIMP OnClockStop(MFTIME hnsSystemTime);
			IFACEMETHODIMP BeginFinalize(IMFAsyncCallback* pCallback, IUnknown* punkState);
			IFACEMETHODIMP EndFinalize(IMFAsyncResult* pResult);

		protected:
			enum class MediaSinkState
			{
				Shutdown = -1,
				None,
				Stopped,
				Paused,
				Started,
				Finalized
			};

			inline CriticalSection& GetCriticalSection()
			{
				return m_criticalSection;
			}

			inline MediaSinkState GetState() const
			{
				return m_state;
			}

			HRESULT RuntimeClassInitialize();
			virtual DWORD GetCharacteristics() noexcept;
			virtual HRESULT OnAddStream(DWORD streamSinkIdentifier, _In_ IMFMediaType* mediaType, _Out_ StreamSink** streamSink);
			virtual HRESULT OnRemoveStream(DWORD streamSinkIdentifier);
			virtual DWORD GetStreamSinkCount() noexcept = 0;
			virtual StreamSink* GetStreamSinkByIndex(DWORD streamIndex) noexcept = 0;
			virtual StreamSink* GetStreamSinkById(DWORD streamId) noexcept = 0;
			virtual HRESULT OnStart() = 0;
			virtual HRESULT OnPause() = 0;
			virtual HRESULT OnStop() = 0;
			virtual HRESULT OnShutdown() = 0;
			virtual HRESULT OnSetProperties(_In_ ABI::Windows::Foundation::Collections::IPropertySet* configuration) = 0;

		private:
			IFACEMETHODIMP SetProperties(ABI::Windows::Foundation::Collections::IPropertySet* pConfiguration);
			IFACEMETHODIMP Close();

			CriticalSection m_criticalSection;
			MediaSinkState m_state;
			ComPtr<IMFPresentationClock> m_clock;
		};

		class StreamSink abstract : public Implements<RuntimeClassFlags<RuntimeClassType::ClassicCom>,
			IMFStreamSink, IMFMediaEventGenerator, IMFMediaTypeHandler, IMFAsyncCallback>
		{
			friend class MediaSink;

		public:
			StreamSink();
			virtual ~StreamSink();

			IFACEMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			IFACEMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, PROPVARIANT const* pvValue);
			IFACEMETHODIMP GetMediaSink(IMFMediaSink** ppMediaSink);
			IFACEMETHODIMP GetIdentifier(DWORD* pdwIdentifier);
			IFACEMETHODIMP GetMediaTypeHandler(IMFMediaTypeHandler** ppHandler);
			IFACEMETHODIMP ProcessSample(IMFSample* pSample);
			IFACEMETHODIMP PlaceMarker(MFSTREAMSINK_MARKER_TYPE eMarkerType, PROPVARIANT const* pvarMarkerValue, PROPVARIANT const* pvarContextValue);
			IFACEMETHODIMP Flush();
			IFACEMETHODIMP IsMediaTypeSupported(IMFMediaType* pMediaType, IMFMediaType** ppMediaType);
			IFACEMETHODIMP GetMediaTypeCount(DWORD* pdwTypeCount);
			IFACEMETHODIMP GetMediaTypeByIndex(DWORD dwIndex, IMFMediaType** ppType);
			IFACEMETHODIMP SetCurrentMediaType(IMFMediaType* pMediaType);
			IFACEMETHODIMP GetCurrentMediaType(IMFMediaType** ppMediaType);
			IFACEMETHODIMP GetMajorType(GUID* pguidMajorType);
			IFACEMETHODIMP Shutdown();

		protected:
			enum class StreamSinkState
			{
				Shutdown = -1,
				None,
				Stopped,
				Paused,
				Started,
				Finalized
			};

			inline CriticalSection& GetCriticalSection()
			{
				return m_criticalSection;
			}

			inline StreamSinkState GetState() const
			{
				return m_state;
			}

			inline MediaSink* GetMediaSink() const
			{
				return m_mediaSink;
			}

			inline IMFMediaType* GetMediaType() const
			{
				return m_mediaType.Get();
			}

			inline HRESULT RuntimeClassInitialize(_In_ MediaSink* mediaSink)
			{
				return RuntimeClassInitialize(mediaSink, nullptr);
			}

			HRESULT RuntimeClassInitialize(_In_ MediaSink* mediaSink, _In_ IMFMediaType* mediaType);
			HRESULT NotifyRequestSample();
			HRESULT NotifyError(HRESULT result);
			virtual DWORD GetIdentifier() noexcept = 0;
			virtual const GUID& GetMajorType() noexcept = 0;
			virtual DWORD GetMediaTypeCount() noexcept = 0;
			virtual HRESULT ValidateMediaType(_In_ IMFMediaType* mediaType) = 0;
			virtual HRESULT GetSupportedMediaType(DWORD index, _Out_ IMFMediaType** mediaType) = 0;
			virtual HRESULT OnProcessSample(_In_ IMFSample* sample) = 0;
			virtual HRESULT OnMediaTypeChange(_In_ IMFMediaType* type) = 0;
			virtual HRESULT OnStart(MFTIME position) = 0;
			virtual HRESULT OnRestart(MFTIME position) = 0;
			virtual HRESULT OnStop() = 0;
			virtual HRESULT OnPause() = 0;
			virtual HRESULT OnPlaceMarker(MFSTREAMSINK_MARKER_TYPE type, PROPVARIANT const* markerValue, PROPVARIANT const* contextValue) = 0;
			virtual HRESULT OnFlush() = 0;
			virtual HRESULT OnFinalize() = 0;
			virtual HRESULT OnShutdown() = 0;

		private:
			struct StartInfo WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IUnknown>
			{
				StartInfo(MFTIME presentationDescriptor, LONGLONG clockStartOffset) :
					StartPosition(presentationDescriptor),
					ClockStartOffset(clockStartOffset)
				{
				}

				const MFTIME StartPosition;
				const LONGLONG ClockStartOffset;
			};

			struct MarkerInfo WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IUnknown>
			{
				MarkerInfo(MFSTREAMSINK_MARKER_TYPE type, _In_ const PROPVARIANT* markerValue, _In_ PROPVARIANT const* contextValue) :
					Type(type),
					MarkerValue(nullptr),
					ContextValue(nullptr)
				{
					if (markerValue != nullptr)
					{
						MarkerValue = std::make_unique<PROPVARIANT>();
						PropVariantCopy(MarkerValue.get(), markerValue);
					}

					if (contextValue != nullptr)
					{
						ContextValue = std::make_unique<PROPVARIANT>();
						PropVariantCopy(ContextValue.get(), contextValue);
					}
				}

				const MFSTREAMSINK_MARKER_TYPE Type;
				std::unique_ptr<PROPVARIANT> MarkerValue;
				std::unique_ptr<PROPVARIANT> ContextValue;
			};

			IFACEMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue);
			IFACEMETHODIMP Invoke(IMFAsyncResult* pAsyncResult);
			HRESULT Start(MFTIME position, LONGLONG clockStartOffset);
			HRESULT Restart(MFTIME position);
			HRESULT Pause();
			HRESULT Stop();
			HRESULT ProcessQueuedSamples();
			HRESULT Finalize(_In_ IMFAsyncCallback* callback);
			HRESULT OnAsyncStart(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncRestart(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncStop(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncPause(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncPlaceMarker(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncProcessSamples(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncFinalize(_In_ IMFAsyncResult* asyncResult);

			CriticalSection m_criticalSection;
			StreamSinkState m_state;
			DWORD m_workQueueId;
			ComPtr<IMFMediaEventQueue> m_events;
			ComPtr<IMFMediaType> m_mediaType;
			std::queue<ComPtr<IMFSample>> m_queuedSamples;
			MediaSink* m_mediaSink;
		};

	}
}
