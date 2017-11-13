#pragma once
#include <unordered_map>
#include <string>
#include <type_traits>
#include "LoggingProvider.h"

#ifdef TRACE
#define _CONCAT_TOKEN(a, b) _CONCAT_TOKEN1(a, b)
#define _CONCAT_TOKEN1(a, b) _CONCAT_TOKEN2(~, a ## b)
#define _CONCAT_TOKEN2(p, res) res

#define LOG_TRACE_METHOD(logger) const Telegram::Api::Native::Diagnostics::MethodLogger _CONCAT_TOKEN(_methodLogger, __COUNTER__)(logger, _STRINGIFY_W(__FUNCTION__))
#define LOG_TRACE_HRESULT(logger, result) Telegram::Api::Native::Diagnostics::MethodLogger::LogMethodError(_STRINGIFY_W(__FUNCTION__), _STRINGIFY_W("line " _STRINGIFY(__LINE__)), result);

#ifdef ReturnIfFailed
#undef ReturnIfFailed

#define ReturnIfFailed(result, method) \
	if(FAILED(result = method)) \
	{ \
		Telegram::Api::Native::Diagnostics::MethodLogger::LogMethodHRESULT(_STRINGIFY_W(__FUNCTION__), _STRINGIFY_W("line " _STRINGIFY(__LINE__)), result); \
		return result; \
	} 
#endif

#ifdef BreakIfFailed
#undef BreakIfFailed

#define BreakIfFailed(result, method) \
	if(FAILED(result = method)) \
	{ \
		Telegram::Api::Native::Diagnostics::MethodLogger::LogMethodHRESULT(_STRINGIFY_W(__FUNCTION__), _STRINGIFY_W("line " _STRINGIFY(__LINE__)), result); \
		break; \
	}
#endif

#else
#define LOG_TRACE_METHOD(logger)
#define LOG_TRACE_HRESULT(logger, result)
#endif

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace Diagnostics
			{

				class MethodLogger
				{
				public:
					MethodLogger(LoggingProvider* logger, LPCWSTR methodName);
					~MethodLogger();

					static void LogMethodHRESULT(LPCWSTR methodName, LPCWSTR expression, HRESULT result);

				private:
					static std::unordered_map<std::wstring, MethodLogger*>& GetLoggers();

					LPCWSTR m_methodName;
					ULONGLONG m_startTime;
					LoggingProvider* m_logger;

					static thread_local UINT32 s_indent;
				};

			}
		}
	}
}