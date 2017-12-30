// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <vector>
#include <wrl.h>
#include <windows.foundation.h>

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Windows
{
	namespace Foundation
	{

		template<typename TValue>
		class Reference WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ABI::Windows::Foundation::IReference<TValue>>
		{
			InspectableClass(ABI::Windows::Foundation::IReference<TValue>::z_get_rc_name_impl(), BaseTrust);

		public:
			typedef typename ABI::Windows::Foundation::Internal::GetAbiType<typename ABI::Windows::Foundation::IReference<TValue>::T_complex>::type TAbiType;

			Reference(_In_ TAbiType& items) :
				m_value(items)
			{
			}

			~Reference()
			{
			}

			IFACEMETHODIMP get_Value(_Out_ TAbiType* value)
			{
				if (value == nullptr)
				{
					return E_POINTER;
				}

				*value = m_value;
				return S_OK;
			}

			inline TAbiType GetSize() const
			{
				return m_value;
			}

		private:
			TAbiType m_value;
		};

	}
}