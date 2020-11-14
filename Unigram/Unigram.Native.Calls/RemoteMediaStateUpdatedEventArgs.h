// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once

#include "RemoteMediaStateUpdatedEventArgs.g.h"

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::Calls::implementation
{
	struct RemoteMediaStateUpdatedEventArgs : RemoteMediaStateUpdatedEventArgsT<RemoteMediaStateUpdatedEventArgs>
	{
		RemoteMediaStateUpdatedEventArgs() = default;
		RemoteMediaStateUpdatedEventArgs(VoipAudioState audio, VoipVideoState video);

		VoipAudioState Audio();
		VoipVideoState Video();

	private:
		VoipAudioState m_audio;
		VoipVideoState m_video;
	};
} // namespace winrt::Unigram::Native::Calls::implementation
