#pragma once
#include <string>
#include <vector>
#include <memory>
#include <Wrl.h>
#include <openssl/rand.h>
#include <openssl/sha.h>
#include <openssl/pem.h>
#include <openssl/aes.h>
#include <openssl/bn.h>
#include "DatacenterServer.h"

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class DatacenterCryptography
			{
			public:
				static bool FactorizePQ(UINT64 pq, UINT32& p, UINT32& q);

				static INT64 ComputePublickKeyFingerprint(_In_::RSA* key);

				static bool GetDatacenterPublicKey(_In_ std::vector<INT64> const& fingerprints, _Out_ ServerPublicKey const** publicKey);

				static bool IsGoodGaAndGb(_In_ BIGNUM* ga, _In_ BIGNUM* p);

				static bool IsGoodPrime(_In_ BIGNUM* p, UINT32 g);

				static BN_CTX* GetBNContext();

				inline static bool CheckNonces(_In_reads_(16) BYTE const* a, _In_reads_(16) BYTE const* b)
				{
					return memcmp(a, b, 16) == 0;
				}

				inline static bool CheckPrime(_In_ BIGNUM* p, _In_ BN_CTX* bnContext)
				{
					int result = 0;
					if (!BN_primality_test(&result, p, BN_prime_checks, bnContext, 0, NULL))
					{
						return false;
					}

					return result != 0;
				}

				inline static ::RSA* GetRSAPublicKey(HString const& key)
				{
					UINT32 length;
					auto buffer = key.GetRawBuffer(&length);
					auto mbLength = WideCharToMultiByte(CP_UTF8, 0, buffer, length, nullptr, 0, nullptr, nullptr);
					auto mbString = std::make_unique<char[]>(mbLength);
					WideCharToMultiByte(CP_UTF8, 0, buffer, length, mbString.get(), mbLength, nullptr, nullptr);

					/*Wrappers::BIO keyBio(BIO_new(BIO_s_mem()));
					BIO_write(keyBio.Get(), mbString.get(), mbLength);*/

					Wrappers::BIO keyBio(BIO_new_mem_buf(mbString.get(), mbLength));
					return PEM_read_bio_RSAPublicKey(keyBio.Get(), nullptr, nullptr, nullptr);
				}

			private:
				static void WriteTLBigNum(_In_ BYTE* buffer, _In_ BIGNUM* number, UINT32 length);

				inline static ::RSA* GetRSAPublicKey(std::string const& key)
				{
					/*Wrappers::BIO keyBio(BIO_new(BIO_s_mem()));
					BIO_write(keyBio.Get(), key.c_str(), static_cast<int>(key.size()));*/

					Wrappers::BIO keyBio(BIO_new_mem_buf(key.c_str(), static_cast<int>(key.size())));
					return PEM_read_bio_RSAPublicKey(keyBio.Get(), nullptr, nullptr, nullptr);
				}

				inline static int BN_primality_test(int *is_probably_prime, const BIGNUM *candidate, int checks, BN_CTX* ctx, int do_trial_division, BN_GENCB* cb)
				{
					switch (BN_is_prime_fasttest_ex(candidate, checks, ctx, do_trial_division, cb))
					{
					case 1:
						*is_probably_prime = 1;
						return 1;
					case 0:
						*is_probably_prime = 0;
						return 1;
					default:
						*is_probably_prime = 0;
						return 0;
					}
				}

				inline static UINT64 GCD(UINT64 a, UINT64 b)
				{
					while (a != 0 && b != 0)
					{
						while ((b & 1) == 0)
						{
							b >>= 1;
						}

						while ((a & 1) == 0)
						{
							a >>= 1;
						}

						if (a > b)
						{
							a -= b;
						}
						else
						{
							b -= a;
						}
					}

					return b == 0 ? a : b;
				}

				inline static UINT64 Random(UINT64 max)
				{
					return static_cast<UINT64>(max * (rand() / (RAND_MAX + 1.0)));
				}
			};

		}
	}
}