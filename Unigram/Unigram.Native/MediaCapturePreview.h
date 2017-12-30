// Copyright (c) 2016 Lorenzo Rossoni

#pragma once
#include <queue>
#include <map>
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
			IFACEMETHODIMP SetProperties(ABI::Windows::Foundation::Collections::IPropertySet* configuration);
			IFACEMETHODIMP GetCharacteristics(DWORD* pdwCharacteristics);
			IFACEMETHODIMP AddStreamSink(DWORD dwStreamSinkIdentifier, IMFMediaType* pMediaType, IMFStreamSink** ppStreamSink);
			IFACEMETHODIMP RemoveStreamSink(DWORD dwStreamSinkIdentifier);
			IFACEMETHODIMP GetStreamSinkById(DWORD dwIdentifier, IMFStreamSink** ppStreamSink);
			IFACEMETHODIMP GetStreamSinkByIndex(DWORD dwIndex, IMFStreamSink** ppStreamSink);
			IFACEMETHODIMP GetStreamSinkCount(DWORD* pcStreamSinkCount);
			IFACEMETHODIMP GetPresentationClock(IMFPresentationClock** ppPresentationClock);
			IFACEMETHODIMP SetPresentationClock(IMFPresentationClock* pPresentationClock);
			IFACEMETHODIMP Shutdown();
			IFACEMETHODIMP OnClockPause(MFTIME hnsSystemTime);
			IFACEMETHODIMP OnClockRestart(MFTIME hnsSystemTime);
			IFACEMETHODIMP OnClockSetRate(MFTIME hnsSystemTime, float flRate);
			IFACEMETHODIMP OnClockStart(MFTIME hnsSystemTime, LONGLONG llClockStartOffset);
			IFACEMETHODIMP OnClockStop(MFTIME hnsSystemTime);
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
			IFACEMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			IFACEMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val);
			IFACEMETHODIMP GetService(REFGUID guidService, REFIID riid, LPVOID* ppvObject);
			IFACEMETHODIMP GetCharacteristics(DWORD* pdwCharacteristics);
			IFACEMETHODIMP CreatePresentationDescriptor(IMFPresentationDescriptor** ppPresentationDescriptor);
			IFACEMETHODIMP Start(IMFPresentationDescriptor* pPresentationDescriptor, GUID const* pguidTimeFormat, PROPVARIANT const* pvarStartPosition);
			IFACEMETHODIMP Stop();
			IFACEMETHODIMP Pause();
			IFACEMETHODIMP Shutdown();
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

			IFACEMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue);
			IFACEMETHODIMP Invoke(IMFAsyncResult* pAsyncResult);
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

			IFACEMETHODIMP RuntimeClassInitialize(_In_ MediaCapturePreviewMediaSink* mediaSink, _In_ IMFMediaType* mediaType);
			IFACEMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			IFACEMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, PROPVARIANT const* pvValue);
			IFACEMETHODIMP GetMediaSource(IMFMediaSource** ppMediaSource);
			IFACEMETHODIMP Shutdown();
			IFACEMETHODIMP IsMediaTypeSupported(IMFMediaType* pMediaType, IMFMediaType** ppMediaType);
			IFACEMETHODIMP GetMediaTypeCount(DWORD* pdwTypeCount);
			IFACEMETHODIMP GetMediaTypeByIndex(DWORD dwIndex, IMFMediaType** ppType);
			IFACEMETHODIMP SetCurrentMediaType(IMFMediaType* pMediaType);
			IFACEMETHODIMP GetCurrentMediaType(IMFMediaType** ppMediaType);
			IFACEMETHODIMP GetMajorType(GUID* pguidMajorType);
			IFACEMETHODIMP GetMediaSink(IMFMediaSink** ppMediaSink);
			IFACEMETHODIMP GetIdentifier(DWORD* pdwIdentifier);
			IFACEMETHODIMP GetMediaTypeHandler(IMFMediaTypeHandler** ppHandler);
			IFACEMETHODIMP ProcessSample(IMFSample* pSample);
			IFACEMETHODIMP PlaceMarker(MFSTREAMSINK_MARKER_TYPE eMarkerType, PROPVARIANT const* pvarMarkerValue, PROPVARIANT const* pvarContextValue);
			IFACEMETHODIMP Flush();
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

			IFACEMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue);
			IFACEMETHODIMP Invoke(IMFAsyncResult* pAsyncResult);
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

			IFACEMETHODIMP RuntimeClassInitialize(_In_ MediaCapturePreviewMediaSource* mediaSource, _In_ IMFMediaType* mediaType);
			IFACEMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			IFACEMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, PROPVARIANT const* pvValue);
			IFACEMETHODIMP GetMediaSource(IMFMediaSource** ppMediaSource);
			IFACEMETHODIMP GetStreamDescriptor(IMFStreamDescriptor** ppStreamDescriptor);
			IFACEMETHODIMP RequestSample(IUnknown* pToken);
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

			IFACEMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue);
			IFACEMETHODIMP Invoke(IMFAsyncResult* pAsyncResult);
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