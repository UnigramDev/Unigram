#pragma once

#include "pch.h"

using namespace Platform;

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			public interface class IStreamInfo
			{
				property String^ Name { String^ get(); }
				property String^ Language { String^ get(); }
				property String^ CodecName { String^ get(); }
				property int64 Bitrate { int64 get(); }
				property bool IsDefault { bool get(); }
			};

			public ref class AudioStreamInfo sealed : public IStreamInfo
			{
			public:
				AudioStreamInfo(String^ name, String^ language, String^ codecName, int64 bitrate, bool isDefault,
					int channels, int sampleRate, int bitsPerSample)
				{
					this->name = name;
					this->language = language;
					this->codecName = codecName;
					this->bitrate = bitrate;
					this->isDefault = isDefault;

					this->channels = channels;
					this->sampleRate = sampleRate;
					this->bitsPerSample = bitsPerSample;
				}

				virtual property String^ Name { String^ get() { return name; } }
				virtual property String^ Language { String^ get() { return language; } }
				virtual property String^ CodecName { String^ get() { return codecName; } }
				virtual property int64 Bitrate { int64 get() { return bitrate; } }
				virtual property bool IsDefault { bool get() { return isDefault; } }

				property int Channels { int get() { return channels; } }
				property int SampleRate { int get() { return sampleRate; } }
				property int BitsPerSample { int get() { return bitsPerSample; } }

			private:
				String ^ name;
				String^ language;
				String^ codecName;
				int64 bitrate;
				bool isDefault;

				int channels;
				int sampleRate;
				int bitsPerSample;
			};

			public ref class VideoStreamInfo sealed : public IStreamInfo
			{
			public:
				VideoStreamInfo(String^ name, String^ language, String^ codecName, int64 bitrate, bool isDefault,
					int pixelWidth, int pixelHeight, int bitsPerSample)
				{
					this->name = name;
					this->language = language;
					this->codecName = codecName;
					this->bitrate = bitrate;
					this->isDefault = isDefault;

					this->pixelWidth = pixelWidth;
					this->pixelHeight = pixelHeight;
					this->bitsPerSample = bitsPerSample;
				}

				virtual property String^ Name { String^ get() { return name; } }
				virtual property String^ Language { String^ get() { return language; } }
				virtual property String^ CodecName { String^ get() { return codecName; } }
				virtual property int64 Bitrate { int64 get() { return bitrate; } }
				virtual property bool IsDefault { bool get() { return isDefault; } }

				property int PixelWidth { int get() { return pixelWidth; } }
				property int PixelHeight { int get() { return pixelHeight; } }
				property int BitsPerSample { int get() { return bitsPerSample; } }

			private:
				String ^ name;
				String^ language;
				String^ codecName;
				int64 bitrate;
				bool isDefault;

				int pixelWidth;
				int pixelHeight;
				int bitsPerSample;
			};


			public ref class SubtitleStreamInfo sealed : public IStreamInfo
			{
			public:
				SubtitleStreamInfo(String^ name, String^ language, String^ codecName, bool isDefault, bool isForced, TimedMetadataTrack^ track)
				{
					this->name = name;
					this->language = language;
					this->codecName = codecName;
					this->isDefault = isDefault;
					this->isForced = isForced;
					this->track = track;
				}

				virtual property String^ Name { String^ get() { return name; } }
				virtual property String^ Language { String^ get() { return language; } }
				virtual property String^ CodecName { String^ get() { return codecName; } }
				virtual property int64 Bitrate { int64 get() { return 0; } }
				virtual property bool IsDefault { bool get() { return isDefault; } }

				property bool IsForced { bool get() { return isForced; } }

				property TimedMetadataTrack^ SubtitleTrack
				{
					TimedMetadataTrack^ get()
					{
						return track;
					}

				internal:
					void set(TimedMetadataTrack^ value)
					{
						track = value;
					}
				}
			private:
				String ^ name;
				String^ language;
				String^ codecName;
				bool isDefault;
				bool isForced;
				TimedMetadataTrack^ track;
			};
		}
	}
}