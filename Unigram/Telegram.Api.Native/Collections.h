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
		namespace Collections
		{
			namespace Details
			{

				template<typename TItem>
				struct VectorItemTraits
				{
					typedef TItem WrappedItemType;

					static HRESULT Wrap(TItem value, _Out_ WrappedItemType* result)
					{
						*result = value;
						return S_OK;
					}

					static HRESULT Unwrap(WrappedItemType const& value, _Out_ TItem* result)
					{
						*result = value;
						return S_OK;
					}

					static bool Equals(WrappedItemType const& x, TItem const& y)
					{
						return x == y;
					}
				};

				template<typename TItem>
				struct VectorItemTraits<TItem*>
				{
					typedef ComPtr<TItem> WrappedItemType;

					static HRESULT Wrap(TItem* value, _Out_ WrappedItemType* result)
					{
						result->Attach(value);
						return S_OK;
					}

					static HRESULT Unwrap(WrappedItemType const& value, _Out_ TItem** result)
					{
						return value.CopyTo(result);
					}

					static bool Equals(WrappedItemType const& x, TItem* y)
					{
						return x.Get() == y;
					}
				};

				template<>
				struct VectorItemTraits<HSTRING>
				{
					typedef HString WrappedItemType;

					static HRESULT Wrap(HSTRING value, _Out_ WrappedItemType* result)
					{
						result->Attach(value);
						return S_OK;
					}

					static HRESULT Unwrap(WrappedItemType const& value, _Out_ HSTRING* result)
					{
						return value.CopyTo(result);
					}

					static bool Equals(WrappedItemType const& x, HSTRING y)
					{
						return x == y;
					}
				};

			}

			template<typename TItem>
			class VectorView WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ABI::Windows::Foundation::Collections::IVectorView<TItem>, ABI::Windows::Foundation::Collections::IIterable<TItem>>
			{
				InspectableClass(ABI::Windows::Foundation::Collections::IVectorView<TItem>::z_get_rc_name_impl(), BaseTrust);

			public:
				typedef typename TItem ItemType;
				typedef typename ABI::Windows::Foundation::Internal::GetAbiType<typename ABI::Windows::Foundation::Collections::IVectorView<TItem>::T_complex>::type TAbiType;
				typedef Details::VectorItemTraits<TAbiType> ItemTraits;

				VectorView(_In_ std::vector<typename ItemTraits::WrappedItemType>& items) :
					m_items(items)
				{
				}

				VectorView()
				{
				}

				~VectorView()
				{
				}

				IFACEMETHODIMP GetAt(_In_ unsigned index, _Out_ TAbiType* item)
				{
					if (item == nullptr)
					{
						return E_POINTER;
					}

					if (index >= m_items.size())
					{
						return E_BOUNDS;
					}

					return ItemTraits::Unwrap(m_items[index], item);
				}

				IFACEMETHODIMP get_Size(_Out_ unsigned* size)
				{
					if (size == nullptr)
					{
						return E_POINTER;
					}

					*size = static_cast<unsigned>(m_items.size());
					return S_OK;
				}

				IFACEMETHODIMP IndexOf(_In_opt_ TAbiType value, _Out_ unsigned* index, _Out_ boolean* found)
				{
					if (value == nullptr || index == nullptr || found == nullptr)
					{
						return E_POINTER;
					}

					for (size_t i = 0; i < m_items.size(); i++)
					{
						if (ItemTraits::Equals(m_items[i], value))
						{
							*index = static_cast<unsigned>(i);
							*found = true;
							break;
						}
					}

					return S_OK;
				}

				IFACEMETHODIMP First(_Outptr_result_maybenull_ ABI::Windows::Foundation::Collections::IIterator<TItem>** first)
				{
					if (first == nullptr)
					{
						return E_POINTER;
					}

					*first = Make<VectorIterator<TItem, VectorView<TItem>>>(this).Detach();
					return S_OK;
				}

				inline std::vector<typename ItemTraits::WrappedItemType>& GetItems()
				{
					return m_items;
				}

				inline size_t GetSize() const
				{
					return m_items.size();
				}

			private:
				std::vector<typename ItemTraits::WrappedItemType> m_items;
			};

			template<typename TItem, typename TVector>
			class VectorIterator : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ABI::Windows::Foundation::Collections::IIterator<TItem>>
			{
				InspectableClass(ABI::Windows::Foundation::Collections::IIterator<TItem>::z_get_rc_name_impl(), BaseTrust);

			public:
				VectorIterator(_In_ TVector* vector) :
					m_index(0),
					m_vector(vector)
				{
				}

				~VectorIterator()
				{
				}

				IFACEMETHODIMP get_Current(_Out_ typename TVector::TAbiType* current)
				{
					return m_vector->GetAt(m_index, current);
				}

				IFACEMETHODIMP get_HasCurrent(_Out_ boolean* hasCurrent)
				{
					if (hasCurrent == nullptr)
					{
						return E_POINTER;
					}

					*hasCurrent = m_index < m_vector->GetSize();
					return S_OK;
				}

				IFACEMETHODIMP MoveNext(_Out_ boolean* hasCurrent)
				{
					if (hasCurrent == nullptr)
					{
						return E_POINTER;
					}

					auto size = m_vector->GetSize();
					if (m_index >= size)
					{
						return E_BOUNDS;
					}

					m_index++;
					*hasCurrent = (m_index < size);
					return S_OK;
				}

			private:
				ComPtr<TVector> m_vector;
				unsigned m_index;
			};

		}
	}
}