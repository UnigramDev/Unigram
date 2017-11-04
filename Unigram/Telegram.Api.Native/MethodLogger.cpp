#include "pch.h"
#include "MethodLogger.h"

using namespace Telegram::Api::Native::Diagnostics;

thread_local UINT32 MethodLogger::s_indent = 0;

MethodLogger::MethodLogger(LoggingProvider* logger, LPCWSTR methodName) :
	m_startTime(GetTickCount64()),
	m_logger(logger),
	m_methodName(methodName)
{
	auto& loggers = MethodLogger::GetLoggers();
	loggers[methodName] = this;

	m_logger->LogTrace(LogLevel::Information, L"%sMethod %s, thread: 0x%04X\n", std::wstring(InterlockedIncrement(&s_indent) - 1, '\t').c_str(), methodName, GetCurrentThreadId());
}

MethodLogger::~MethodLogger()
{
	m_logger->LogTrace(LogLevel::Information, L"%sCompleted in %I64u ms\n", std::wstring(InterlockedDecrement(&s_indent), '\t').c_str(), GetTickCount64() - m_startTime);

	auto& loggers = MethodLogger::GetLoggers();
	auto iterator = loggers.find(m_methodName);
	if (iterator != loggers.end())
	{
		loggers.erase(iterator);
	}
}

void MethodLogger::LogMethodHRESULT(LPCWSTR methodName, LPCWSTR expression, HRESULT result)
{
	auto& loggers = MethodLogger::GetLoggers();
	auto iterator = loggers.find(methodName);
	if (iterator != loggers.end())
	{
		WCHAR* text;
		UINT32 length;
		if ((length = FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, nullptr,
			result, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), reinterpret_cast<LPWSTR>(&text), 0, nullptr)) == 0)
		{
			iterator->second->m_logger->LogTrace(LogLevel::Error, L"%s%s failed with HRESULT 0x%04X\n", std::wstring(s_indent, '\t').c_str(), expression, result);
		}
		else
		{
			if (text[length - 2] == '\r' && text[length - 1] == '\n')
			{
				text[length - 2] = '\0';
			}

			iterator->second->m_logger->LogTrace(LogLevel::Error, L"%s%s failed with HRESULT 0x%04X (%s)\n", std::wstring(s_indent, '\t').c_str(), expression, result, text);

			LocalFree(text);
		}
	}
}

std::unordered_map<std::wstring, MethodLogger*>& MethodLogger::GetLoggers()
{
	static thread_local std::unordered_map<std::wstring, MethodLogger*> loggers;
	return loggers;
}