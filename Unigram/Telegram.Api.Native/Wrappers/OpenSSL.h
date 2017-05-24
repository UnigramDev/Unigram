#pragma once
#include <openssl/rand.h>
#include <openssl/sha.h>
#include <openssl/bn.h>
#include <openssl/pem.h>
#include <openssl/aes.h>
#include <wrl\wrappers\corewrappers.h>

using namespace Microsoft::WRL::Wrappers;


namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace Wrappers
			{
				namespace HandleTraits
				{

					struct BIGNUMTraits
					{
						typedef BIGNUM* Type;

						inline static bool Close(_In_ Type h) throw()
						{
							BN_free(h);
							return true;
						}

						inline static Type GetInvalidValue() throw()
						{
							return nullptr;
						}
					};

					struct BIOTraits
					{
						typedef ::BIO* Type;

						inline static bool Close(_In_ Type h) throw()
						{
							BIO_free(h);
							return true;
						}

						inline static Type GetInvalidValue() throw()
						{
							return nullptr;
						}
					};

					struct RSATraits
					{
						typedef ::RSA* Type;

						inline static bool Close(_In_ Type h) throw()
						{
							RSA_free(h);
							return true;
						}

						inline static Type GetInvalidValue() throw()
						{
							return nullptr;
						}
					};

					struct BNCTXTraits
					{
						typedef BN_CTX* Type;

						inline static bool Close(_In_ Type h) throw()
						{
							BN_CTX_free(h);
							return true;
						}

						inline static Type GetInvalidValue() throw()
						{
							return nullptr;
						}
					};

				}

				class BigNum : public HandleT<HandleTraits::BIGNUMTraits>
				{
				public:
					explicit BigNum(BIGNUM* h = HandleT::Traits::GetInvalidValue()) throw() : HandleT(h)
					{
					}

					BigNum(_Inout_ BigNum&& h) throw() : HandleT(::Microsoft::WRL::Details::Move(h))
					{
					}

					BigNum& operator=(_Inout_ BigNum&& h) throw()
					{
						*static_cast<HandleT*>(this) = ::Microsoft::WRL::Details::Move(h);
						return *this;
					}
				};

				class BIO : public HandleT<HandleTraits::BIOTraits>
				{
				public:
					explicit BIO(::BIO* h = HandleT::Traits::GetInvalidValue()) throw() : HandleT(h)
					{
					}

					BIO(_Inout_ BIO&& h) throw() : HandleT(::Microsoft::WRL::Details::Move(h))
					{
					}

					BIO& operator=(_Inout_ BIO&& h) throw()
					{
						*static_cast<HandleT*>(this) = ::Microsoft::WRL::Details::Move(h);
						return *this;
					}
				};

				class RSA : public HandleT<HandleTraits::RSATraits>
				{
				public:
					explicit RSA(::RSA* h = HandleT::Traits::GetInvalidValue()) throw() : HandleT(h)
					{
					}

					RSA(_Inout_ RSA&& h) throw() : HandleT(::Microsoft::WRL::Details::Move(h))
					{
					}

					RSA& operator=(_Inout_ RSA&& h) throw()
					{
						*static_cast<HandleT*>(this) = ::Microsoft::WRL::Details::Move(h);
						return *this;
					}

					::RSA* operator->() const throw()
					{
						return handle_;
					}
				};

				class BigNumContext : public HandleT<HandleTraits::BNCTXTraits>
				{
				public:
					explicit BigNumContext(::BN_CTX* h = HandleT::Traits::GetInvalidValue()) throw() : HandleT(h)
					{
					}

					BigNumContext(_Inout_ BigNumContext&& h) throw() : HandleT(::Microsoft::WRL::Details::Move(h))
					{
					}

					BigNumContext& operator=(_Inout_ BigNumContext&& h) throw()
					{
						*static_cast<HandleT*>(this) = ::Microsoft::WRL::Details::Move(h);
						return *this;
					}
				};

			}
		}
	}
}