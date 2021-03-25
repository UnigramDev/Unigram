#include "pch.h"
#include "RemoteMediaStateUpdatedEventArgs.h"
#if __has_include("RemoteMediaStateUpdatedEventArgs.g.cpp")
#include "RemoteMediaStateUpdatedEventArgs.g.cpp"
#endif

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::Calls::implementation
{
	RemoteMediaStateUpdatedEventArgs::RemoteMediaStateUpdatedEventArgs(VoipAudioState audio, VoipVideoState video)
		: m_audio(audio),
		m_video(video)
	{
	}

	VoipAudioState RemoteMediaStateUpdatedEventArgs::Audio() {
		return m_audio;
	}

	VoipVideoState RemoteMediaStateUpdatedEventArgs::Video() {
		return m_video;
	}
} // namespace winrt::Unigram::Native::Calls::implementation
