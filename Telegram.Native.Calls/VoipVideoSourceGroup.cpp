#include "pch.h"
#include "VoipVideoSourceGroup.h"
#if __has_include("VoipVideoSourceGroup.g.cpp")
#include "VoipVideoSourceGroup.g.cpp"
#endif

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipVideoSourceGroup::VoipVideoSourceGroup(hstring semantics, IVector<int32_t> sourceIds)
        : m_semantics(semantics)
        , m_sourceIds(sourceIds)
    {

    }

    hstring VoipVideoSourceGroup::Semantics()
    {
        return m_semantics;
    }

    void VoipVideoSourceGroup::Semantics(hstring value)
    {
        m_semantics = value;
    }

    IVector<int32_t> VoipVideoSourceGroup::SourceIds()
    {
        return m_sourceIds;
    }

    void VoipVideoSourceGroup::SourceIds(IVector<int32_t> value)
    {
        m_sourceIds = value;
    }
}
