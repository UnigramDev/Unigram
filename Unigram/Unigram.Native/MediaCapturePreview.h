// Copyright (c) 2016 Lorenzo Rossoni

#pragma once
#include <queue>
#include <map>
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

using namespace Microsoft::WRL;
using ABI::Windows::Media::MediaProperties::IVideoEncodingProperties;

namespace Unigram
{
	namespace Native
	{

		ref class MediaCapturePreviewSource;

		class MediaCapturePreviewMediaSink WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IMFMediaSink, IMFClockStateSink, ABI::Windows::Media::IMediaExtension>
		{
			friend class MediaCapturePreviewStreamSink;

			InspectableClass(L"Unigram.Native.MediaCapturePreviewSourceMediaSink", BaseTrust);

		public:
			MediaCapturePreviewMediaSink();
			~MediaCapturePreviewMediaSink();

			STDMETHODIMP RuntimeClassInitialize(_In_ MediaCapturePreviewSource^ previewSource, _In_ IMFMediaType* mediaType);
			STDMETHODIMP SetProperties(ABI::Windows::Foundation::Collections::IPropertySet* configuration);
			STDMETHODIMP GetCharacteristics(DWORD* pdwCharacteristics);
			STDMETHODIMP AddStreamSink(DWORD dwStreamSinkIdentifier, IMFMediaType* pMediaType, IMFStreamSink** ppStreamSink);
			STDMETHODIMP RemoveStreamSink(DWORD dwStreamSinkIdentifier);
			STDMETHODIMP GetStreamSinkById(DWORD dwIdentifier, IMFStreamSink** ppStreamSink);
			STDMETHODIMP GetStreamSinkByIndex(DWORD dwIndex, IMFStreamSink** ppStreamSink);
			STDMETHODIMP GetStreamSinkCount(DWORD* pcStreamSinkCount);
			STDMETHODIMP GetPresentationClock(IMFPresentationClock** ppPresentationClock);
			STDMETHODIMP SetPresentationClock(IMFPresentationClock* pPresentationClock);
			STDMETHODIMP Shutdown();
			STDMETHODIMP OnClockPause(MFTIME hnsSystemTime);
			STDMETHODIMP OnClockRestart(MFTIME hnsSystemTime);
			STDMETHODIMP OnClockSetRate(MFTIME hnsSystemTime, float flRate);
			STDMETHODIMP OnClockStart(MFTIME hnsSystemTime, LONGLONG llClockStartOffset);
			STDMETHODIMP OnClockStop(MFTIME hnsSystemTime);
			HRESULT RequestSample();

		private:
			enum class MediaSinkState
			{
				Shutdown = -1,
				None,
				Stopped,
				Started
			};

			inline MediaSinkState GetState() const
			{
				return m_state;
			}

			inline MediaCapturePreviewSource^ GetPreviewSource() const
			{
				return m_previewSource;
			}

			MediaSinkState m_state;
			CriticalSection m_criticalSection;
			ComPtr<IMFMediaEventQueue> m_events;
			ComPtr<IMFPresentationClock> m_clock;
			ComPtr<MediaCapturePreviewStreamSink> m_stream;
			MediaCapturePreviewSource^ m_previewSource;
		};

		class MediaCapturePreviewMediaSource WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IMFMediaSource, IMFMediaEventGenerator, IMFGetService, IMFAsyncCallback,
			ABI::Windows::Media::Core::IMediaSource, ABI::Windows::Media::Playback::IMediaPlaybackSource>
		{
			friend class MediaCapturePreviewMediaStream;

			InspectableClass(L"Unigram.Native.MediaCapturePreviewSourceMediaSource", BaseTrust);

		public:
			MediaCapturePreviewMediaSource();
			~MediaCapturePreviewMediaSource();

			STDMETHODIMP RuntimeClassInitialize(_In_ MediaCapturePreviewSource^ previewSource, _In_ IMFMediaType* mediaType);
			STDMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			STDMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			STDMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			STDMETHODIMP QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val);
			STDMETHODIMP GetService(REFGUID guidService, REFIID riid, LPVOID* ppvObject);
			STDMETHODIMP GetCharacteristics(DWORD* pdwCharacteristics);
			STDMETHODIMP CreatePresentationDescriptor(IMFPresentationDescriptor** ppPresentationDescriptor);
			STDMETHODIMP Start(IMFPresentationDescriptor* pPresentationDescriptor, GUID const* pguidTimeFormat, PROPVARIANT const* pvarStartPosition);
			STDMETHODIMP Stop();
			STDMETHODIMP Pause();
			STDMETHODIMP Shutdown();
			HRESULT NotifyEndOfStream();
			HRESULT DeliverSample(_In_ IMFSample* sample);

		private:
			enum class MediaSourceState
			{
				Shutdown = -1,
				None,
				Stopped,
				Started
			};

			struct StartInfo WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IUnknown>
			{
				StartInfo(_In_ IMFPresentationDescriptor* presentationDescriptor, _In_ PROPVARIANT const* startPosition) :
					PresentationDescriptor(presentationDescriptor)
				{
					PropVariantCopy(&StartPosition, startPosition);
				}

				ComPtr<IMFPresentationDescriptor> PresentationDescriptor;
				PROPVARIANT StartPosition;
			};

			inline MediaSourceState GetState() const
			{
				return m_state;
			}

			inline MediaCapturePreviewSource^ GetPreviewSource() const
			{
				return m_previewSource;
			}

			STDMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue);
			STDMETHODIMP Invoke(IMFAsyncResult* pAsyncResult);
			HRESULT OnAsyncStart(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncStop(_In_ IMFAsyncResult* asyncResult);

			DWORD m_workQueueId;
			MediaSourceState m_state;
			CriticalSection m_criticalSection;
			ComPtr<IMFMediaEventQueue> m_events;
			ComPtr<IMFPresentationDescriptor> m_presentationDescriptor;
			ComPtr<MediaCapturePreviewMediaStream> m_stream;
			MediaCapturePreviewSource^ m_previewSource;
		};

		class MediaCapturePreviewStreamSink WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IMFStreamSink, IMFMediaEventGenerator, IMFMediaTypeHandler, IMFAsyncCallback>
		{
			friend class MediaCapturePreviewMediaSink;

		public:
			MediaCapturePreviewStreamSink();
			~MediaCapturePreviewStreamSink();

			STDMETHODIMP RuntimeClassInitialize(_In_ MediaCapturePreviewMediaSink* mediaSink, _In_ IMFMediaType* mediaType);
			STDMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			STDMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			STDMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			STDMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, PROPVARIANT const* pvValue);
			STDMETHODIMP GetMediaSource(IMFMediaSource** ppMediaSource);
			STDMETHODIMP Shutdown();
			STDMETHODIMP IsMediaTypeSupported(IMFMediaType* pMediaType, IMFMediaType** ppMediaType);
			STDMETHODIMP GetMediaTypeCount(DWORD* pdwTypeCount);
			STDMETHODIMP GetMediaTypeByIndex(DWORD dwIndex, IMFMediaType** ppType);
			STDMETHODIMP SetCurrentMediaType(IMFMediaType* pMediaType);
			STDMETHODIMP GetCurrentMediaType(IMFMediaType** ppMediaType);
			STDMETHODIMP GetMajorType(GUID* pguidMajorType);
			STDMETHODIMP GetMediaSink(IMFMediaSink** ppMediaSink);
			STDMETHODIMP GetIdentifier(DWORD* pdwIdentifier);
			STDMETHODIMP GetMediaTypeHandler(IMFMediaTypeHandler** ppHandler);
			STDMETHODIMP ProcessSample(IMFSample* pSample);
			STDMETHODIMP PlaceMarker(MFSTREAMSINK_MARKER_TYPE eMarkerType, PROPVARIANT const* pvarMarkerValue, PROPVARIANT const* pvarContextValue);
			STDMETHODIMP Flush();
			HRESULT RequestSample();

		private:
			enum class StreamSinkState
			{
				Shutdown = -1,
				None,
				Stopped,
				Started
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

			inline StreamSinkState GetState() const
			{
				return m_state;
			}

			STDMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue);
			STDMETHODIMP Invoke(IMFAsyncResult* pAsyncResult);
			HRESULT Start(MFTIME position, LONGLONG clockStartOffset);
			HRESULT Stop();
			HRESULT OnAsyncStart(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncStop(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncPlaceMarker(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncProcessSamples(_In_ IMFAsyncResult* asyncResult);

			DWORD m_workQueueId;
			StreamSinkState m_state;
			CriticalSection m_criticalSection;
			ComPtr<IMFMediaEventQueue> m_events;
			ComPtr<IMFMediaType> m_mediaType;
			ComPtr<MediaCapturePreviewMediaSink> m_mediaSink;
		};

		class MediaCapturePreviewMediaStream WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IMFMediaStream, IMFMediaEventGenerator, IMFAsyncCallback>
		{
			friend class MediaCapturePreviewMediaSource;

		public:
			MediaCapturePreviewMediaStream();
			~MediaCapturePreviewMediaStream();

			STDMETHODIMP RuntimeClassInitialize(_In_ MediaCapturePreviewMediaSource* mediaSource, _In_ IMFMediaType* mediaType);
			STDMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			STDMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			STDMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			STDMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, PROPVARIANT const* pvValue);
			STDMETHODIMP GetMediaSource(IMFMediaSource** ppMediaSource);
			STDMETHODIMP GetStreamDescriptor(IMFStreamDescriptor** ppStreamDescriptor);
			STDMETHODIMP RequestSample(IUnknown* pToken);
			HRESULT Shutdown();
			HRESULT Start(_In_ PROPVARIANT const* position);
			HRESULT Stop();
			HRESULT DeliverSample(_In_ IMFSample* token);

		private:
			enum class MediaStreamState
			{
				Shutdown = -1,
				None,
				Stopped,
				Started
			};

			inline MediaStreamState GetState() const
			{
				return m_state;
			}

			inline bool IsActive() const
			{
				return m_isActive;
			}

			STDMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue);
			STDMETHODIMP Invoke(IMFAsyncResult* pAsyncResult);
			HRESULT Activate(bool active);

			bool m_isActive;
			DWORD m_workQueueId;
			MediaStreamState m_state;
			CriticalSection m_criticalSection;
			ComPtr<IMFMediaEventQueue> m_events;
			ComPtr<IMFMediaType> m_mediaType;
			ComPtr<IMFStreamDescriptor> m_streamDescriptor;
			ComPtr<MediaCapturePreviewMediaSource> m_mediaSource;
		};

		public ref class MediaCapturePreviewSource sealed
		{
			friend class MediaCapturePreviewMediaSink;
			friend class MediaCapturePreviewMediaSource;
			friend class MediaCapturePreviewStreamSink;
			friend class MediaCapturePreviewMediaStream;

		public:
			virtual ~MediaCapturePreviewSource();

			static MediaCapturePreviewSource^ CreateFromVideoEncodingProperties(_In_ Windows::Media::MediaProperties::VideoEncodingProperties^ videoEncodingProperties);

			property Windows::Media::IMediaExtension^ MediaSink
			{
				Windows::Media::IMediaExtension^ get();
			}

			property Windows::Media::Core::IMediaSource^ MediaSource
			{
				Windows::Media::Core::IMediaSource^ get();
			}

		private:
			MediaCapturePreviewSource();

			HRESULT Initialize(_In_ IVideoEncodingProperties* videoEncodingProperties);
			HRESULT RequestSample(_In_ IUnknown* token);
			HRESULT DeliverSample(_In_ IMFSample* sample);

			ComPtr<MediaCapturePreviewMediaSink> m_mediaSink;
			ComPtr<MediaCapturePreviewMediaSource> m_mediaSource;
		};

	}
}