//*****************************************************************************
//
//	Copyright 2017 Microsoft Corporation
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

using namespace Platform;

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			// Level values from ffmpeg: libavutil/log.h
			public enum class LogLevel
			{
				Panic = 0,
				Fatal = 8,
				Error = 16,
				Warning = 24,
				Info = 32,
				Verbose = 40,
				Debug = 48,
				Trace = 56
			};

			public interface class ILogProvider
			{
				void Log(LogLevel level, String^ message);
			};
		}
	}
}