#pragma once 
#include <string>
#include <wrl\wrappers\corewrappers.h>

using Microsoft::WRL::Wrappers::HString;

inline static void WideCharToMultiByte(_In_reads_(length) LPWSTR buffer, UINT32 length, _Out_ std::string& mbString)
{
	mbString.resize(::WideCharToMultiByte(CP_UTF8, 0, buffer, length, nullptr, 0, nullptr, nullptr));
	::WideCharToMultiByte(CP_UTF8, 0, buffer, length, &mbString[0], static_cast<int>(mbString.size()), nullptr, nullptr);
}

inline static void WideCharToMultiByte(_In_ HString const& wString, _Out_ std::string& mbString)
{
	UINT32 length;
	auto buffer = wString.GetRawBuffer(&length);

	mbString.resize(::WideCharToMultiByte(CP_UTF8, 0, buffer, length, nullptr, 0, nullptr, nullptr));
	::WideCharToMultiByte(CP_UTF8, 0, buffer, length, &mbString[0], static_cast<int>(mbString.size()), nullptr, nullptr);
}

inline static void WideCharToMultiByte(_In_ std::wstring const& wString, _Out_ std::string& mbString)
{
	mbString.resize(::WideCharToMultiByte(CP_UTF8, 0, wString.data(), static_cast<int>(wString.size()), nullptr, 0, nullptr, nullptr));
	::WideCharToMultiByte(CP_UTF8, 0, wString.data(), static_cast<int>(wString.size()), &mbString[0], static_cast<int>(mbString.size()), nullptr, nullptr);
}

inline static void MultiByteToWideChar(_In_reads_(length) LPSTR buffer, UINT32 length, _Out_ std::wstring& wString)
{
	wString.resize(::MultiByteToWideChar(CP_UTF8, 0, buffer, length, nullptr, 0));
	::MultiByteToWideChar(CP_UTF8, 0, buffer, length, &wString[0], static_cast<int>(wString.size()));
}

inline static void MultiByteToWideChar(_In_ std::string const& mbString, _Out_ std::wstring& wString)
{
	wString.resize(::MultiByteToWideChar(CP_UTF8, 0, mbString.data(), static_cast<int>(mbString.size()), nullptr, 0));
	::MultiByteToWideChar(CP_UTF8, 0, mbString.data(), static_cast<int>(mbString.size()), &wString[0], static_cast<int>(wString.size()));
}