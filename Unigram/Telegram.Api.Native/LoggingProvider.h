#pragma once
#include <wrl.h>
#include <stdio.h>
#include "MultithreadObject.h"
#include "Telegram.Api.Native.h"

#ifdef TRACE
#define LOG_TRACE(logger, logLevel, ...) logger->LogTrace(logLevel, __VA_ARGS__)
#else
#define LOG_TRACE(logger, logLevel, ...)
#endif

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::Diagnostics::ILogger;
using ABI::Telegram::Api::Native::Diagnostics::LogLevel;
using Telegram::Api::Native::MultiThreadObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace Diagnostics
			{

				class LoggingProvider abstract : public virtual MultiThreadObject
				{
				public:
					template<unsigned int sizeDest>
					inline HRESULT LogTrace(LogLevel logLevel, wchar_t const (&message)[sizeDest])
					{
						return LogTrace(logLevel, HString::MakeReference(message).Get());
					}

					template<unsigned int sizeDest>
					inline HRESULT LogTrace(LogLevel logLevel, std::wstring const& message)
					{
						return LogTrace(logLevel, HStringReference(message.c_str(), message.size()).Get());
					}

					inline HRESULT LogTrace(LogLevel logLevel, LPCWSTR pwhFormat, ...)
					{
						va_list args;
						va_start(args, pwhFormat);

						WCHAR buffer[1024];
						auto length = vswprintf_s(buffer, 1024, pwhFormat, args);

						va_end(args);

						if (length < 0)
						{
							return E_INVALIDARG;
						}

						return LogTrace(logLevel, HString::MakeReference<1024>(buffer, static_cast<UINT32>(length)).Get());
					}

					inline HRESULT LogTrace(LogLevel logLevel, HSTRING message)
					{
						ComPtr<ILogger> logger;

						{
							auto lock = LockCriticalSection();

							if (m_logger == nullptr)
							{
								return S_OK;
							}

							logger = m_logger;
						}

						return logger->Log(logLevel, message);
					}

					inline HRESULT LogTraceError(DWORD error)
					{
						if (error == 0)
						{
							return S_FALSE;
						}

						WCHAR* text;
						UINT32 length;
						if ((length = FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, nullptr,
							error, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), reinterpret_cast<LPWSTR>(&text), 0, nullptr)) == 0)
						{
							return LogTrace(LogLevel::Error, L"An error with code 0x%08x occurred\n", error);
						}
						else
						{
							/*if (text[length - 2] == '\r' && text[length - 1] == '\n')
							{
								length -= 2;
							}*/

							auto result = LogTrace(LogLevel::Error, HStringReference(text, length).Get());

							LocalFree(text);

							return result;
						}
					}

				protected:
					STDMETHODIMP get_Logger(_Out_ ILogger** value)
					{
						if (value == nullptr)
						{
							return E_POINTER;
						}

						auto lock = LockCriticalSection();

						return m_logger.CopyTo(value);
					}

					STDMETHODIMP put_Logger(_In_ ILogger* value)
					{
						auto lock = LockCriticalSection();

						m_logger = value;
						return S_OK;
					}

				private:
					ComPtr<ILogger> m_logger;
				};

			}
		}
	}
}