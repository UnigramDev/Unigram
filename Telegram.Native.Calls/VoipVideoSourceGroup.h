#pragma once

#include "VoipVideoSourceGroup.g.h"

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipVideoSourceGroup : VoipVideoSourceGroupT<VoipVideoSourceGroup>
    {
        VoipVideoSourceGroup(hstring semantics, IVector<int32_t> sourceIds);

        hstring Semantics();
        void Semantics(hstring value);

        IVector<int32_t> SourceIds();
        void SourceIds(IVector<int32_t> value);

    private:
        hstring m_semantics;
        IVector<int32_t> m_sourceIds;
    };
}

namespace winrt::Telegram::Native::Calls::factory_implementation
{
    struct VoipVideoSourceGroup : VoipVideoSourceGroupT<VoipVideoSourceGroup, implementation::VoipVideoSourceGroup>
    {
    };
}
