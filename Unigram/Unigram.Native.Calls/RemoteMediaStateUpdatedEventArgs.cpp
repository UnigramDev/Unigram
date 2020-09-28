// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// clang-format off
#include "pch.h"
#include "RemoteMediaStateUpdatedEventArgs.h"
#include "RemoteMediaStateUpdatedEventArgs.g.cpp"
// clang-format on

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
