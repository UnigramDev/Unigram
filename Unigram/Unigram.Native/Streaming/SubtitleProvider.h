#pragma once

#include <string>
#include <codecvt>

#include "CompressedSampleProvider.h"
#include "StreamInfo.h"
#include "NativeBufferFactory.h"


namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class SubtitleProvider abstract : CompressedSampleProvider
			{
			internal:
				SubtitleProvider(FFmpegReader^ reader,
					AVFormatContext* avFormatCtx,
					AVCodecContext* avCodecCtx,
					FFmpegInteropConfig^ config,
					int index,
					TimedMetadataKind timedMetadataKind)
					: CompressedSampleProvider(reader, avFormatCtx, avCodecCtx, config, index)
				{
					this->timedMetadataKind = timedMetadataKind;
				}

				property TimedMetadataTrack^ SubtitleTrack;


				virtual HRESULT Initialize() override
				{
					InitializeNameLanguageCodec();
					SubtitleTrack = ref new TimedMetadataTrack(Name, Language, timedMetadataKind);
					SubtitleTrack->Label = Name != nullptr ? Name : Language;
					cueExitedToken = SubtitleTrack->CueExited += ref new Windows::Foundation::TypedEventHandler<Windows::Media::Core::TimedMetadataTrack ^, Windows::Media::Core::MediaCueEventArgs ^>(this, &Unigram::Native::Streaming::SubtitleProvider::OnCueExited);
					trackFailedToken = SubtitleTrack->TrackFailed += ref new Windows::Foundation::TypedEventHandler<Windows::Media::Core::TimedMetadataTrack ^, Windows::Media::Core::TimedMetadataTrackFailedEventArgs ^>(this, &Unigram::Native::Streaming::SubtitleProvider::OnTrackFailed);
					return S_OK;
				}

				virtual void NotifyVideoFrameSize(int width, int height)
				{
				}

				virtual IMediaCue^ CreateCue(AVPacket* packet, TimeSpan* position, TimeSpan *duration) = 0;

				virtual void QueuePacket(AVPacket *packet) override
				{
					if (packet->pos > maxCuePosition)
					{
						maxCuePosition = packet->pos;
					}
					else if (addedCues.find(packet->pos) != addedCues.end())
					{
						av_packet_free(&packet);
						return;
					}

					TimeSpan position;
					TimeSpan duration;

					position.Duration = LONGLONG(av_q2d(m_pAvStream->time_base) * 10000000 * packet->pts) - m_startOffset;
					duration.Duration = LONGLONG(av_q2d(m_pAvStream->time_base) * 10000000 * packet->duration);

					auto cue = CreateCue(packet, &position, &duration);
					if (cue)
					{
						addedCues[packet->pos] = packet->pos;

						cue->StartTime = position;
						cue->Duration = duration;
						AddCue(cue);
					}
					else
					{
						OutputDebugString(L"Failed to create subtitle cue.");
					}

					av_packet_free(&packet);
				}

				// convert UTF-8 string to wstring
				std::wstring utf8_to_wstring(const std::string& str)
				{
					std::wstring_convert<std::codecvt_utf8<wchar_t>> myconv;
					return myconv.from_bytes(str);
				}

				Platform::String ^ convertFromString(const std::wstring & input)
				{
					return ref new Platform::String(input.c_str(), (unsigned int)input.length());
				}

			private:

				void AddCue(IMediaCue^ cue)
				{
					mutex.lock();
					try
					{
						// to avoid flicker, we try to add new cues only after active cues are finished
						if (m_config->UseAntiFlickerForSubtitles && SubtitleTrack->ActiveCues->Size > 0)
						{
							bool addToPending = true;
							for each (auto active in SubtitleTrack->ActiveCues)
							{
								if (active->StartTime.Duration + active->Duration.Duration > cue->StartTime.Duration)
								{
									addToPending = false;
									break;
								}
							}
							if (addToPending)
							{
								pendingCues.push_back(cue);
							}
							else
							{
								SubtitleTrack->AddCue(cue);
							}
						}
						else
						{
							SubtitleTrack->AddCue(cue);
						}
					}
					catch (...)
					{
						OutputDebugString(L"Failed to add subtitle cue.");
					}
					mutex.unlock();
				}

				void OnCueExited(TimedMetadataTrack ^sender, MediaCueEventArgs ^args)
				{
					mutex.lock();
					try
					{
						for each (auto cue in pendingCues)
						{
							SubtitleTrack->AddCue(cue);
						}
					}
					catch (...)
					{
						OutputDebugString(L"Failed to add subtitle cue.");
					}
					pendingCues.clear();
					mutex.unlock();
				}

				void OnTrackFailed(TimedMetadataTrack ^sender, TimedMetadataTrackFailedEventArgs ^args)
				{
					OutputDebugString(L"Subtitle track error.");
				}

				std::mutex mutex;
				std::vector<IMediaCue^> pendingCues;
				std::map<int64, int64> addedCues;
				int64 maxCuePosition;
				Windows::Foundation::EventRegistrationToken cueExitedToken;
				Windows::Foundation::EventRegistrationToken trackFailedToken;
				TimedMetadataKind timedMetadataKind;

			public:
				virtual ~SubtitleProvider()
				{
					if (SubtitleTrack)
					{
						SubtitleTrack->CueExited -= cueExitedToken;
						SubtitleTrack->TrackFailed -= trackFailedToken;
						SubtitleTrack = nullptr;
					}
				}
			};

		}
	}
}