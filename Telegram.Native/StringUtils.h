#pragma once

#include <string>

#ifndef STRING_UTILS_H
#define STRING_UTILS_H
template<typename... ARGS>
class wstrprintf : public std::basic_string<wchar_t>
{
public:
	wstrprintf(const wchar_t* format, ARGS... args)
	{
		int len = _scwprintf(format, args...);
		resize(len);
		swprintf_s((wchar_t* const)c_str(), len + 1, format, args...);
	}
};

/// checks UTF-8 string for correctness
//bool check_utf8(char* str);

/// checks if a code unit is a first code unit of a UTF-8 character
inline bool is_utf8_character_first_code_unit(unsigned char c) {
	return (c & 0xC0) != 0x80;
}

/// returns length of UTF-8 string in characters
inline size_t utf8_length(const char* begin, size_t size) {
	size_t result = 0;
	for (int i = 0; i < size; i++) {
		auto c = begin[i];
		result += is_utf8_character_first_code_unit(c);
	}
	return result;
}

/// returns length of UTF-8 string in UTF-16 code units
inline size_t utf8_utf16_length(const char* begin, size_t size) {
	size_t result = 0;
	for (int i = 0; i < size; i++) {
		auto c = begin[i];
		result += is_utf8_character_first_code_unit(c) + ((c & 0xf8) == 0xf0);
	}
	return result;
}

inline std::wstring to_wstring(const char* begin, size_t size) {
	//if (!check_utf8(slice)) {
	//	return Status::Error("Wrong encoding");
	//}

	size_t wstring_len = utf8_utf16_length(begin, size);

	std::wstring result(wstring_len, static_cast<wchar_t>(0));
	if (wstring_len) {
		wchar_t* res = &result[0];
		for (size_t i = 0; i < size;) {
			unsigned int a = static_cast<unsigned char>(begin[i++]);
			if (a >= 0x80) {
				unsigned int b = static_cast<unsigned char>(begin[i++]);
				if (a >= 0xe0) {
					unsigned int c = static_cast<unsigned char>(begin[i++]);
					if (a >= 0xf0) {
						unsigned int d = static_cast<unsigned char>(begin[i++]);
						unsigned int val = ((a & 0x07) << 18) + ((b & 0x3f) << 12) + ((c & 0x3f) << 6) + (d & 0x3f) - 0x10000;
						*res++ = static_cast<wchar_t>(0xD800 + (val >> 10));
						*res++ = static_cast<wchar_t>(0xDC00 + (val & 0x3ff));
					}
					else {
						*res++ = static_cast<wchar_t>(((a & 0x0f) << 12) + ((b & 0x3f) << 6) + (c & 0x3f));
					}
				}
				else {
					*res++ = static_cast<wchar_t>(((a & 0x1f) << 6) + (b & 0x3f));
				}
			}
			else {
				*res++ = static_cast<wchar_t>(a);
			}
		}
		//CHECK(res == &result[0] + wstring_len);
	}
	return result;
}

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

inline winrt::hstring string_from_unmanaged(const std::string& from) {
	auto tmp = to_wstring(from.data(), from.size());
	return winrt::hstring(tmp);
}

inline const wchar_t* GetExceptionMessage(DWORD code)
{
	switch (code) {
	case EXCEPTION_ACCESS_VIOLATION: return L"ACCESS_VIOLATION";
	case EXCEPTION_ARRAY_BOUNDS_EXCEEDED: return L"ARRAY_BOUNDS_EXCEEDED";
	case EXCEPTION_BREAKPOINT: return L"EXCEPTION_BREAKPOINT";
	case EXCEPTION_DATATYPE_MISALIGNMENT: return L"DATATYPE_MISALIGNMENT";
	case EXCEPTION_FLT_DENORMAL_OPERAND: return L"FLT_DENORMAL_OPERAND";
	case EXCEPTION_FLT_DIVIDE_BY_ZERO: return L"FLT_DIVIDE_BY_ZERO";
	case EXCEPTION_FLT_INEXACT_RESULT: return L"FLT_INEXACT_RESULT";
	case EXCEPTION_FLT_INVALID_OPERATION: return L"FLT_INVALID_OPERATION";
	case EXCEPTION_FLT_OVERFLOW: return L"FLT_OVERFLOW";
	case EXCEPTION_FLT_STACK_CHECK: return L"FLT_STACK_CHECK";
	case EXCEPTION_FLT_UNDERFLOW: return L"FLT_UNDERFLOW";
	case EXCEPTION_ILLEGAL_INSTRUCTION: return L"ILLEGAL_INSTRUCTION";
	case EXCEPTION_IN_PAGE_ERROR: return L"IN_PAGE_ERROR";
	case EXCEPTION_INT_DIVIDE_BY_ZERO: return L"INT_DIVIDE_BY_ZERO";
	case EXCEPTION_INT_OVERFLOW: return L"INT_OVERFLOW";
	case EXCEPTION_INVALID_DISPOSITION: return L"INVALID_DISPOSITION";
	case EXCEPTION_NONCONTINUABLE_EXCEPTION: return L"NONCONTINUABLE_EXCEPTION";
	case EXCEPTION_PRIV_INSTRUCTION: return L"PRIV_INSTRUCTION";
	case EXCEPTION_SINGLE_STEP: return L"SINGLE_STEP";
	case EXCEPTION_STACK_OVERFLOW: return L"STACK_OVERFLOW";
	default: return L"UNKNOWN";
	};
}

// From http://davidpritchard.org/archives/907
inline std::wstring GetBacktrace(DWORD code)
{
	constexpr uint32_t TRACE_MAX_STACK_FRAMES = 99;
	void* stack[TRACE_MAX_STACK_FRAMES];

	ULONG hash;
	const int numFrames = CaptureStackBackTrace(1, TRACE_MAX_STACK_FRAMES, stack, &hash);

	const wchar_t* message = GetExceptionMessage(code);
	std::wstring result = wstrprintf(L"Unhandled exception: %s\n", message);

	for (int i = 0; i < numFrames; ++i)
	{
		void* moduleBaseVoid = nullptr;
		RtlPcToFileHeader(stack[i], &moduleBaseVoid);

		auto moduleBase = (const unsigned char*)moduleBaseVoid;
		constexpr auto MODULE_BUF_SIZE = 4096U;
		wchar_t modulePath[MODULE_BUF_SIZE];
		const wchar_t* moduleFilename = modulePath;

		if (moduleBase != nullptr)
		{
			GetModuleFileName((HMODULE)moduleBase, modulePath, MODULE_BUF_SIZE);

			int moduleFilenamePos = std::wstring(modulePath).find_last_of(L"\\");
			if (moduleFilenamePos >= 0)
			{
				moduleFilename += moduleFilenamePos + 1;
			}

			result += wstrprintf(L"   at %s+0x%08lx\n", moduleFilename, (uint32_t)((unsigned char*)stack[i] - moduleBase));
		}
		else
		{
			result += wstrprintf(L"   at %s+0x%016llx\n", moduleFilename, (uint64_t)stack[i]);
		}
	}

	return result;
}
#endif
