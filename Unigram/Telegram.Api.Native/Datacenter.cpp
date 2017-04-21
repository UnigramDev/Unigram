#include "pch.h"
#include "Datacenter.h"

using namespace Telegram::Api::Native;

Datacenter::Datacenter(uint32 id) :
	m_id(id)
{
}

uint32 Datacenter::Id::get()
{
	return m_id;
}