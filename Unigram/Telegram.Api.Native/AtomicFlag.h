#pragma once
#include <type_traits>
#include <Windows.h>

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace Details
			{

				template<typename T>
				inline T AtomicRead(volatile T* destination)
				{
					static_assert(false, "AtomicRead<T> must be specialized");
				}

				template<typename T>
				inline T AtomicWrite(volatile T* destination, T value)
				{
					static_assert(false, "AtomicWrite<T> must be specialized");
				}

				template<typename T>
				inline T AtomicAnd(volatile T* destination, T value)
				{
					static_assert(false, "AtomicAnd<T> must be specialized");
				}

				template<typename T>
				inline T AtomicOr(volatile T* destination, T value)
				{
					static_assert(false, "AtomicOr<T> must be specialized");
				}

				template<typename T>
				inline T AtomicXor(volatile T* destination, T value)
				{
					static_assert(false, "AtomicXor<T> must be specialized");
				}

				template<>
				inline CHAR AtomicRead<CHAR>(volatile CHAR* destination)
				{
					return ::InterlockedExchange8(destination, *destination);
				}

				template<>
				inline CHAR AtomicWrite<CHAR>(volatile CHAR* destination, CHAR value)
				{
					return ::InterlockedExchange8(destination, value);
				}

				template<>
				inline CHAR AtomicAnd<CHAR>(volatile CHAR* destination, CHAR value)
				{
					return ::InterlockedAnd8(destination, value);
				}

				template<>
				inline CHAR AtomicOr<CHAR>(volatile CHAR* destination, CHAR value)
				{
					return ::InterlockedOr8(destination, value);
				}

				template<>
				inline CHAR AtomicXor<CHAR>(volatile CHAR* destination, CHAR value)
				{
					return ::InterlockedXor8(destination, value);
				}

				template<>
				inline SHORT AtomicRead<SHORT>(volatile SHORT* destination)
				{
					return ::InterlockedCompareExchange16(destination, 0, 0);
				}

				template<>
				inline SHORT AtomicWrite<SHORT>(volatile SHORT* destination, SHORT value)
				{
					return ::InterlockedExchange16(destination, value);
				}

				template<>
				inline SHORT AtomicAnd<SHORT>(volatile SHORT* destination, SHORT value)
				{
					return ::InterlockedAnd16(destination, value);
				}

				template<>
				inline SHORT AtomicOr<SHORT>(volatile SHORT* destination, SHORT value)
				{
					return ::InterlockedOr16(destination, value);
				}

				template<>
				inline SHORT AtomicXor<SHORT>(volatile SHORT* destination, SHORT value)
				{
					return ::InterlockedXor16(destination, value);
				}

				template<>
				inline LONG AtomicRead<LONG>(volatile LONG* destination)
				{
					return ::InterlockedCompareExchange(destination, 0, 0);
				}

				template<>
				inline LONG AtomicWrite<LONG>(volatile LONG* destination, LONG value)
				{
					return ::InterlockedExchange(destination, value);
				}

				template<>
				inline LONG AtomicAnd<LONG>(volatile LONG* destination, LONG value)
				{
					return ::InterlockedAnd(destination, value);
				}

				template<>
				inline LONG AtomicOr<LONG>(volatile LONG* destination, LONG value)
				{
					return ::InterlockedOr(destination, value);
				}

				template<>
				inline LONG AtomicXor<LONG>(volatile LONG* destination, LONG value)
				{
					return ::InterlockedXor(destination, value);
				}

				template<>
				inline INT AtomicRead<INT>(volatile INT* destination)
				{
					return ::InterlockedCompareExchange(reinterpret_cast<volatile LONG*>(destination), 0, 0);
				}

				template<>
				inline INT AtomicWrite<INT>(volatile INT* destination, INT value)
				{
					return ::InterlockedExchange(reinterpret_cast<volatile LONG*>(destination), value);
				}

				template<>
				inline INT AtomicAnd<INT>(volatile INT* destination, INT value)
				{
					return ::InterlockedAnd(reinterpret_cast<volatile LONG*>(destination), value);
				}

				template<>
				inline INT AtomicOr<INT>(volatile INT* destination, INT value)
				{
					return ::InterlockedOr(reinterpret_cast<volatile LONG*>(destination), value);
				}

				template<>
				inline INT AtomicXor<INT>(volatile INT* destination, INT value)
				{
					return ::InterlockedXor(reinterpret_cast<volatile LONG*>(destination), value);
				}

				template<>
				inline LONG64 AtomicRead<LONG64>(volatile LONG64* destination)
				{
					return ::InterlockedCompareExchange64(destination, 0, 0);
				}

				template<>
				inline LONG64 AtomicWrite<LONG64>(volatile LONG64* destination, LONG64 value)
				{
					return ::InterlockedExchange64(destination, value);
				}

				template<>
				inline LONG64 AtomicAnd<LONG64>(volatile LONG64* destination, LONG64 value)
				{
					return ::InterlockedAnd64(destination, value);
				}

				template<>
				inline LONG64 AtomicOr<LONG64>(volatile LONG64* destination, LONG64 value)
				{
					return ::InterlockedOr64(destination, value);
				}

				template<>
				inline LONG64 AtomicXor<LONG64>(volatile LONG64* destination, LONG64 value)
				{
					return ::InterlockedXor64(destination, value);
				}

			}

			template<typename TFlag>
			struct AtomicFlag
			{
				typedef typename std::underlying_type<typename TFlag>::type IntegralType;

			public:
				AtomicFlag()
				{
				}

				AtomicFlag(TFlag& value) :
					m_value(value)
				{
				}

				inline operator TFlag() const
				{
					return static_cast<TFlag>(Details::AtomicRead<IntegralType>(reinterpret_cast<IntegralType*>(&m_value)));
				}

				inline operator TFlag const*() const
				{
					return &m_value;
				}

				inline AtomicFlag& operator |=(const TFlag& rhs)
				{
					Details::AtomicOr<IntegralType>(reinterpret_cast<IntegralType*>(&m_value), static_cast<IntegralType>(rhs));
					return *this;
				}

				inline AtomicFlag& operator &=(const TFlag& rhs)
				{
					Details::AtomicAnd<IntegralType>(reinterpret_cast<IntegralType*>(&m_value), static_cast<IntegralType>(rhs));
					return *this;
				}

				inline AtomicFlag& operator ^=(const TFlag& rhs)
				{
					Details::AtomicXor<IntegralType>(reinterpret_cast<IntegralType*>(&m_value), static_cast<IntegralType>(rhs));
					return *this;
				}

				inline AtomicFlag& operator =(const TFlag& rhs)
				{
					Details::AtomicWrite<IntegralType>(reinterpret_cast<IntegralType*>(&m_value), static_cast<IntegralType>(rhs));
					return *this;
				}

				inline TFlag operator |(const TFlag& rhs)
				{
					return static_cast<TFlag>(Details::AtomicRead<IntegralType>(reinterpret_cast<IntegralType*>(&m_value))) | rhs;
				}

				inline TFlag operator &(const TFlag& rhs)
				{
					return static_cast<TFlag>(Details::AtomicRead<IntegralType>(reinterpret_cast<IntegralType*>(&m_value))) & rhs;
				}

				inline TFlag operator ^(const TFlag& rhs)
				{
					return static_cast<TFlag>(Details::AtomicRead<IntegralType>(reinterpret_cast<IntegralType*>(&m_value))) ^ rhs;
				}

				inline TFlag operator ~()
				{
					return ~static_cast<TFlag>(Details::AtomicRead<IntegralType>(reinterpret_cast<IntegralType*>(&m_value)));
				}

				inline bool operator ==(const TFlag& rhs)
				{
					return static_cast<TFlag>(Details::AtomicRead<IntegralType>(reinterpret_cast<IntegralType*>(&m_value))) == rhs;
				}

				inline bool operator !=(const TFlag& rhs)
				{
					return static_cast<TFlag>(Details::AtomicRead<IntegralType>(reinterpret_cast<IntegralType*>(&m_value))) != rhs;
				}

			private:
				TFlag m_value;
			};

		}
	}
}