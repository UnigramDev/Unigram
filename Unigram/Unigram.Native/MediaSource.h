// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <queue>
#include <windows.media.core.h>
#include <windows.media.h>
#include <windows.foundation.h>
#include <mfobjects.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <Mferror.h>
#include <mfapi.h>
#include <wrl.h>
#include <wrl\wrappers\corewrappers.h>

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Unigram
{
	namespace Native
	{

		class MediaSource abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, ABI::Windows::Media::Core::IMediaSource,
			ABI::Windows::Foundation::IClosable, ABI::Windows::Media::Playback::IMediaPlaybackSource, IMFAsyncCallback, IMFMediaSource, IMFMediaSourceEx, IMFMediaEventGenerator, IMFGetService>
		{
			friend class MediaStream;

		public:
			MediaSource();
			virtual ~MediaSource();

			IFACEMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			IFACEMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val);
			IFACEMETHODIMP CreatePresentationDescriptor(IMFPresentationDescriptor** ppPresentationDescriptor);
			IFACEMETHODIMP GetCharacteristics(DWORD* pdwCharacteristics);
			IFACEMETHODIMP Start(IMFPresentationDescriptor* pPresentationDescriptor, GUID const* pguidTimeFormat, PROPVARIANT const* pvarStartPosition);
			IFACEMETHODIMP Stop();
			IFACEMETHODIMP Pause();
			IFACEMETHODIMP Shutdown();
			IFACEMETHODIMP GetService(REFGUID guidService, REFIID riid, LPVOID* ppvObject);
			IFACEMETHODIMP GetSourceAttributes(IMFAttributes** ppAttributes);
			IFACEMETHODIMP GetStreamAttributes(DWORD dwStreamIdentifier, IMFAttributes** ppAttributes);
			IFACEMETHODIMP SetD3DManager(IUnknown* pManager);

		protected:
			enum class MediaSourceState
			{
				Shutdown = -1,
				None,
				Stopped,
				Paused,
				Started
			};

			inline MediaSourceState GetState() const
			{
				return m_state;
			}

			inline CriticalSection& GetCriticalSection()
			{
				return m_criticalSection;
			}

			HRESULT RuntimeClassInitialize(_In_ IMFPresentationDescriptor* streamDescriptor);
			HRESULT NotifyError(HRESULT result);
			virtual HRESULT ValidatePresentationDescriptor(_In_ IMFPresentationDescriptor* presentetionDescriptor);
			virtual DWORD GetCharacteristics() noexcept;
			virtual DWORD GetMediaStreamCount() noexcept = 0;
			virtual MediaStream* GetMediaStreamByIndex(DWORD streamIndex) noexcept = 0;
			virtual MediaStream* GetMediaStreamById(DWORD streamId) noexcept = 0;
			virtual HRESULT OnStart(MFTIME position) = 0;
			virtual HRESULT OnSeek(MFTIME position) = 0;
			virtual HRESULT OnPause() = 0;
			virtual HRESULT OnStop() = 0;
			virtual HRESULT OnShutdown() = 0;

		private:
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

			IFACEMETHODIMP Close();
			IFACEMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue);
			IFACEMETHODIMP Invoke(IMFAsyncResult* pAsyncResult);
			HRESULT NotifyEndOfStream();
			HRESULT OnAsyncStart(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncStop(_In_ IMFAsyncResult* asyncResult);
			HRESULT OnAsyncPause(_In_ IMFAsyncResult* asyncResult);

			MediaSourceState m_state;
			CriticalSection m_criticalSection;
			DWORD m_activeStreamCount;
			DWORD m_workQueueId;
			ComPtr<IMFPresentationDescriptor> m_presentationDescriptor;
			ComPtr<IMFMediaEventQueue> m_events;
		};

		class MediaStream abstract : public Implements<RuntimeClassFlags<ClassicCom>, IMFMediaStream, IMFMediaEventGenerator>
		{
			friend class MediaSource;

		public:
			MediaStream();
			virtual ~MediaStream();

			IFACEMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			IFACEMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			IFACEMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, PROPVARIANT const* pvValue);
			IFACEMETHODIMP GetMediaSource(IMFMediaSource** ppMediaSource);
			IFACEMETHODIMP GetStreamDescriptor(IMFStreamDescriptor** ppStreamDescriptor);
			IFACEMETHODIMP RequestSample(IUnknown* pToken);
			HRESULT Shutdown();

		protected:
			enum class MediaStreamState
			{
				Shutdown = -1,
				None,
				Stopped,
				Paused,
				Started
			};

			inline MediaStreamState GetState() const
			{
				return m_state;
			}

			inline CriticalSection& GetCriticalSection()
			{
				return m_criticalSection;
			}

			inline bool IsActive() const
			{
				return m_isActive;
			}

			inline MediaSource* GetMediaSource() const
			{
				return m_mediaSource;
			}

			inline HRESULT NotifyError(HRESULT result)
			{
				return m_mediaSource->NotifyError(result);
			}

			HRESULT RuntimeClassInitialize(_In_ MediaSource* mediaSource, _In_ IMFStreamDescriptor* streamDescriptor);
			HRESULT NotifyEndOfStream();
			HRESULT DeliverSample(_In_ IMFSample* sample);
			virtual bool IsEndOfStream() noexcept = 0;
			virtual HRESULT OnSampleRequested(_In_ IUnknown* pToken) = 0;
			virtual HRESULT OnStart(MFTIME position) = 0;
			virtual HRESULT OnSeek(MFTIME position) = 0;
			virtual HRESULT OnPause() = 0;
			virtual HRESULT OnStop() = 0;
			virtual HRESULT OnShutdown() = 0;

		private:
			HRESULT Activate(bool active);
			HRESULT Start(_In_ PROPVARIANT const* position);
			HRESULT Pause();
			HRESULT Stop();
			HRESULT DeliverQueuedSamples();

			bool m_isActive;
			MediaStreamState m_state;
			CriticalSection m_criticalSection;
			std::queue<ComPtr<IMFSample>> m_queuedSamples;
			ComPtr<IMFMediaEventQueue> m_events;
			ComPtr<IMFStreamDescriptor> m_streamDescriptor;
			MediaSource* m_mediaSource;
		};

	}
}