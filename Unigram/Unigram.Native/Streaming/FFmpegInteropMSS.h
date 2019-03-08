//*****************************************************************************
//
//	Copyright 2015 Microsoft Corporation
//
//	Licensed under the Apache License, Version 2.0 (the "License");
//	you may not use this file except in compliance with the License.
//	You may obtain a copy of the License at
//
//	http ://www.apache.org/licenses/LICENSE-2.0
//
//	Unless required by applicable law or agreed to in writing, software
//	distributed under the License is distributed on an "AS IS" BASIS,
//	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//	See the License for the specific language governing permissions and
//	limitations under the License.
//
//*****************************************************************************

#pragma once
#include <queue>
#include <mutex>
#include <pplawait.h>
#include "FFmpegReader.h"
#include "MediaSampleProvider.h"
#include "MediaThumbnailData.h"
#include "VideoFrame.h"
#include "AvEffectDefinition.h"
#include "StreamInfo.h"
#include "SubtitleProvider.h"


using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Media::Core;
using namespace Windows::Media::Playback;
using namespace Telegram::Td;
using namespace Telegram::Td::Api;

namespace WFM = Windows::Foundation::Metadata;

extern "C"
{
#include <libavformat/avformat.h>
}

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{

			public ref class FFmpegInteropMSS sealed
			{
			public:
				IAsyncOperation<FFmpegInteropMSS^>^ CreateFromFileAsync(Client^ client, File^ file, FFmpegInteropConfig^ config);
				IAsyncOperation<FFmpegInteropMSS^>^ CreateFromFileAsync(Client^ client, File^ file) { return CreateFromFileAsync(client, file, ref new FFmpegInteropConfig()); }



				void UpdateFile(File^ file);

				void SetAudioEffects(IVectorView<AvEffectDefinition^>^ audioEffects);
				void SetVideoEffects(IVectorView<AvEffectDefinition^>^ videoEffects);
				void DisableAudioEffects();
				void DisableVideoEffects();
				MediaThumbnailData^ ExtractThumbnail();

				MediaStreamSource^ GetMediaStreamSource();
				MediaPlaybackItem^ CreateMediaPlaybackItem();
				MediaPlaybackItem^ CreateMediaPlaybackItem(TimeSpan startTime);
				MediaPlaybackItem^ CreateMediaPlaybackItem(TimeSpan startTime, TimeSpan durationLimit);

				virtual ~FFmpegInteropMSS();

				// Properties

				property TimeSpan Duration
				{
					TimeSpan get()
					{
						return mediaDuration;
					};
				};

				property VideoStreamInfo^ VideoStream
				{
					VideoStreamInfo^ get() { return videoStreamInfo; }
				}

				property IVectorView<AudioStreamInfo^>^ AudioStreams
				{
					IVectorView<AudioStreamInfo^>^ get() { return audioStreamInfos; }
				}

				property IVectorView<SubtitleStreamInfo^>^ SubtitleStreams
				{
					IVectorView<SubtitleStreamInfo^>^ get() { return subtitleStreamInfos; }
				}

				property bool HasThumbnail
				{
					bool get() { return thumbnailStreamIndex; }
				}

				[WFM::Deprecated("Use the AudioStreams property.", WFM::DeprecationType::Deprecate, 0x0)]
				property AudioStreamDescriptor^ AudioDescriptor
				{
					AudioStreamDescriptor^ get()
					{
						return currentAudioStream ? dynamic_cast<AudioStreamDescriptor^>(currentAudioStream->StreamDescriptor) : nullptr;
					};
				};

				[WFM::Deprecated("Use the VideoStream property.", WFM::DeprecationType::Deprecate, 0x0)]
				property VideoStreamDescriptor^ VideoDescriptor
				{
					VideoStreamDescriptor^ get()
					{
						return videoStream ? dynamic_cast<VideoStreamDescriptor^>(videoStream->StreamDescriptor) : nullptr;
					};
				};

				[WFM::Deprecated("Use the VideoStream property.", WFM::DeprecationType::Deprecate, 0x0)]
				property String^ VideoCodecName
				{
					String^ get()
					{
						return videoStream ? videoStream->CodecName : nullptr;
					};
				};

				[WFM::Deprecated("Use the AudioStreams property.", WFM::DeprecationType::Deprecate, 0x0)]
				property String^ AudioCodecName
				{
					String^ get()
					{
						return audioStreamInfos->Size > 0 ? audioStreamInfos->GetAt(0)->CodecName : nullptr;
					};
				};

				property MediaPlaybackItem^ PlaybackItem
				{
					MediaPlaybackItem^ get()
					{
						return playbackItem;
					}
				}


				FFmpegInteropMSS(FFmpegInteropConfig^ config);

			private:
				HRESULT CreateMediaStreamSource(Client^ client, File^ file, MediaStreamSource^ mss);
				HRESULT InitFFmpegContext();
				MediaSource^ CreateMediaSource();
				MediaSampleProvider^ CreateAudioStream(AVStream * avStream, int index);
				MediaSampleProvider^ CreateVideoStream(AVStream * avStream, int index);
				SubtitleProvider^ CreateSubtitleSampleProvider(AVStream * avStream, int index);
				MediaSampleProvider^ CreateAudioSampleProvider(AVStream * avStream, AVCodecContext* avCodecCtx, int index);
				MediaSampleProvider^ CreateVideoSampleProvider(AVStream * avStream, AVCodecContext* avCodecCtx, int index);
				HRESULT ParseOptions(PropertySet^ ffmpegOptions);
				void OnStarting(MediaStreamSource ^sender, MediaStreamSourceStartingEventArgs ^args);
				void OnSampleRequested(MediaStreamSource ^sender, MediaStreamSourceSampleRequestedEventArgs ^args);
				void OnSwitchStreamsRequested(MediaStreamSource^ sender, MediaStreamSourceSwitchStreamsRequestedEventArgs^ args);
				void OnAudioTracksChanged(MediaPlaybackItem ^sender, IVectorChangedEventArgs ^args);
				void OnPresentationModeChanged(MediaPlaybackTimedMetadataTrackList ^sender, TimedMetadataPresentationModeChangedEventArgs ^args);
				void InitializePlaybackItem(MediaPlaybackItem^ playbackitem);


			internal:

				FFmpegInteropMSS^ CreateFromFile(Client^ client, File^ file, FFmpegInteropConfig^ config, MediaStreamSource^ mss);
				HRESULT Seek(TimeSpan position);

				property MediaSampleProvider^ VideoSampleProvider
				{
					MediaSampleProvider^ get()
					{
						return videoStream;
					}
				}

				AVDictionary * avDict;
				AVIOContext* avIOCtx;
				AVFormatContext* avFormatCtx;

				Client^ m_client;
				File^ m_file;
				HANDLE m_handle;
				HANDLE m_event;
				int64_t m_offset;
				int32_t m_size;

			private:

				MediaStreamSource ^ mss;
				Windows::Foundation::EventRegistrationToken startingRequestedToken;
				Windows::Foundation::EventRegistrationToken sampleRequestedToken;
				Windows::Foundation::EventRegistrationToken switchStreamRequestedToken;
				MediaPlaybackItem^ playbackItem;

				FFmpegInteropConfig ^ config;
				std::vector<MediaSampleProvider^> sampleProviders;
				std::vector<MediaSampleProvider^> audioStreams;
				std::vector<SubtitleProvider^> subtitleStreams;
				MediaSampleProvider^ videoStream;
				MediaSampleProvider^ currentAudioStream;
				IVectorView<AvEffectDefinition^>^ currentAudioEffects;
				int thumbnailStreamIndex;

				Windows::Foundation::EventRegistrationToken audioTracksChangedToken;
				Windows::Foundation::EventRegistrationToken subtitlePresentationModeChangedToken;

				VideoStreamInfo^ videoStreamInfo;
				IVectorView<AudioStreamInfo^>^ audioStreamInfos;
				IVectorView<SubtitleStreamInfo^>^ subtitleStreamInfos;

				std::recursive_mutex mutexGuard;

				String^ videoCodecName;
				String^ audioCodecName;
				TimeSpan mediaDuration;
				IStream* fileStreamData;
				unsigned char* fileStreamBuffer;
				FFmpegReader^ m_pReader;
				bool isFirstSeek;
			};

		}
	}
}
