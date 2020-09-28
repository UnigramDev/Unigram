// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once

// abseil is using some C++ features that were deprecated in C++17.
// Since we build with warning as errors. The following preprocessor
// variables make STL to not emit the warnings.
#define _SILENCE_CXX17_OLD_ALLOCATOR_MEMBERS_DEPRECATION_WARNING
#define _SILENCE_ALL_CXX17_DEPRECATION_WARNINGS

#include <unknwn.h>
#include <winrt/base.h>

#include <winrt/Microsoft.Graphics.Canvas.UI.Composition.h>
#include <winrt/Microsoft.Graphics.Canvas.UI.Xaml.h>
#include <winrt/Microsoft.Graphics.Canvas.h>
#include <winrt/Windows.ApplicationModel.DataTransfer.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.h>
#include <winrt/Windows.Media.Capture.h>
#include <winrt/Windows.Media.h>
#include <winrt/Windows.Networking.Sockets.h>
#include <winrt/Windows.Security.Cryptography.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Xaml.Controls.Primitives.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Hosting.h>
#include <winrt/Windows.UI.Xaml.Interop.h>
#include <winrt/Windows.UI.Xaml.Markup.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.Web.Http.Headers.h>
#include <winrt/Windows.Web.Http.h>

#include <winrt/Windows.ApplicationModel.Core.h>
#include <winrt/Windows.ApplicationModel.h>

#include "winrt/Unigram.Native.Calls.h"

inline std::string from_wstring(const wchar_t* begin, size_t size) {
	size_t result_len = 0;
	for (size_t i = 0; i < size; i++) {
		unsigned int cur = begin[i];
		if ((cur & 0xF800) == 0xD800) {
			if (i < size) {
				unsigned int next = begin[++i];
				if ((next & 0xFC00) == 0xDC00 && (cur & 0x400) == 0) {
					result_len += 4;
					continue;
				}
			}

			return {};
		}
		result_len += 1 + (cur >= 0x80) + (cur >= 0x800);
	}

	std::string result(result_len, '\0');
	if (result_len) {
		char* res = &result[0];
		for (size_t i = 0; i < size; i++) {
			unsigned int cur = begin[i];
			// TODO conversion unsigned int -> signed char is implementation defined
			if (cur <= 0x7f) {
				*res++ = static_cast<char>(cur);
			}
			else if (cur <= 0x7ff) {
				*res++ = static_cast<char>(0xc0 | (cur >> 6));
				*res++ = static_cast<char>(0x80 | (cur & 0x3f));
			}
			else if ((cur & 0xF800) != 0xD800) {
				*res++ = static_cast<char>(0xe0 | (cur >> 12));
				*res++ = static_cast<char>(0x80 | ((cur >> 6) & 0x3f));
				*res++ = static_cast<char>(0x80 | (cur & 0x3f));
			}
			else {
				unsigned int next = begin[++i];
				unsigned int val = ((cur - 0xD800) << 10) + next - 0xDC00 + 0x10000;

				*res++ = static_cast<char>(0xf0 | (val >> 18));
				*res++ = static_cast<char>(0x80 | ((val >> 12) & 0x3f));
				*res++ = static_cast<char>(0x80 | ((val >> 6) & 0x3f));
				*res++ = static_cast<char>(0x80 | (val & 0x3f));
			}
		}
	}
	return result;
}

inline std::string string_to_unmanaged(winrt::hstring str) {
	//if (!str) {
	//	return std::string();
	//}
	return from_wstring(str.data(), str.size());
}