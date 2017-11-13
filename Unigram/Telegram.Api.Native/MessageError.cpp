#include "pch.h"
#include "MessageError.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


MessageError::MessageError(INT32 code, HString&& text) :
	m_code(code),
	m_text(std::move(text))
{
}

MessageError::MessageError()
{
}

MessageError::~MessageError()
{
}

HRESULT MessageError::RuntimeClassInitialize(INT32 code, HSTRING text)
{
	m_code = code;
	return m_text.Set(text);
}

HRESULT MessageError::RuntimeClassInitialize(HRESULT error)
{
	WCHAR* text;
	UINT32 length;
	HRESULT result;
	if ((length = FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, nullptr,
		error, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), reinterpret_cast<LPWSTR>(&text), 0, nullptr)) == 0)
	{
		ReturnIfFailed(result, m_text.Set(L"Unknown error"));
	}
	else
	{
		if (text[length - 2] == '\r' && text[length - 1] == '\n')
		{
			length -= 2;
		}

		ReturnIfFailed(result, m_text.Set(text, length));
		LocalFree(text);
	}

	m_code = error;
	return S_OK;
}

HRESULT MessageError::get_Code(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_code;
	return S_OK;
}

HRESULT MessageError::get_Text(HSTRING* value)
{
	return m_text.CopyTo(value);
}

HRESULT MessageError::get_Exception(HRESULT* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_code & 0x8000)
	{
		*value = m_code;
	}
	else
	{
		*value = E_INVALID_PROTOCOL_OPERATION;
	}

	return S_OK;
}