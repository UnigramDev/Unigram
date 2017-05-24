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
				inline static boolean FactorizePQ(UINT64 pq, UINT32& p, UINT32& q)
				{
					UINT32 it = 0;
					UINT64 g = 0;

					for (UINT32 i = 0; i < 3 || it < 1000; i++)
					{
						UINT64 t = ((Random(128) & 15) + 17) % pq;
						UINT64 x = Random(1000000000) % (pq - 1) + 1;
						UINT64 y = x;

						for (INT32 j = 1; j < 1 << (i + 18); j++)
						{
							++it;
							UINT64 a = x;
							UINT64 b = x;
							UINT64 c = t;

							while (b)
							{
								if (b & 1)
								{
									c += a;

									if (c >= pq)
									{
										c -= pq;
									}
								}

								a += a;
								if (a >= pq)
								{
									a -= pq;
								}

								b >>= 1;
							}
							x = c;

							UINT64 z = x < y ? pq + x - y : x - y;

							g = GCD(z, pq);

							if (g != 1)
							{
								break;
							}

							if (!(j & (j - 1)))
							{
								y = x;
							}
						}

						if (g > 1 && g < pq)
						{
							break;
						}
					}

					if (g > 1 && g < pq)
					{
						p = static_cast<UINT32>(g);
						q = static_cast<UINT32>(pq / g);

						if (p > q)
						{
							UINT32 tmp = p;
							p = q;
							q = tmp;
						}

						return true;
					}
					else
					{
						p = 0;
						q = 0;
						return false;
					}
				}

				inline static boolean CheckNonces(BYTE const* a, BYTE const* b)
				{
					return memcmp(a, b, 16) == 0;
				}

				static boolean SelectPublicKey(std::vector<INT64> const& fingerprints, _Out_ ServerPublicKey const** publicKey);

				static BN_CTX* GetBNContext();

			private:
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