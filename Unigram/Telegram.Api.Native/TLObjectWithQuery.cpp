#include "pch.h"
#include "TLObjectWithQuery.h"

using namespace Telegram::Api::Native;

TLObjectWithQuery::TLObjectWithQuery(ITLObject* query) :
	m_query(query)
{
}

TLObjectWithQuery::~TLObjectWithQuery()
{
}

HRESULT TLObjectWithQuery::get_Query(ITLObject** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_query.CopyTo(value);
}