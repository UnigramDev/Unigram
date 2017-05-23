#pragma once
#include <string>
#include <vector>
#include <Windows.h>
#include <openssl/rand.h>
#include <openssl/sha.h>
#include <openssl/bn.h>
#include <openssl/pem.h>
#include <openssl/aes.h>

struct ServerPublicKey
{
	std::wstring Key;
	INT64 Fingerprint;
};

inline UINT64 GCD(UINT64 a, UINT64 b)
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

inline UINT64 Random(UINT64 max)
{
	return static_cast<UINT64>(max * (rand() / (RAND_MAX + 1.0)));
}

inline boolean FactorizePQ(UINT64 pq, UINT32& p, UINT32& q)
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

inline boolean CheckNonces(BYTE const* a, BYTE const* b)
{
	return memcmp(a, b, 16) == 0;
}

boolean SelectPublicKey(std::vector<INT64> const& fingerprints, _Out_ ServerPublicKey const** publicKey)
{
	struct ServerPublicKeys
	{
		ServerPublicKeys() :
			Keys(4)
		{
			Keys[0].Key = L"-----BEGIN RSA PUBLIC KEY-----\n"
				"MIIBCgKCAQEAwVACPi9w23mF3tBkdZz+zwrzKOaaQdr01vAbU4E1pvkfj4sqDsm6\n"
				"lyDONS789sVoD/xCS9Y0hkkC3gtL1tSfTlgCMOOul9lcixlEKzwKENj1Yz/s7daS\n"
				"an9tqw3bfUV/nqgbhGX81v/+7RFAEd+RwFnK7a+XYl9sluzHRyVVaTTveB2GazTw\n"
				"Efzk2DWgkBluml8OREmvfraX3bkHZJTKX4EQSjBbbdJ2ZXIsRrYOXfaA+xayEGB+\n"
				"8hdlLmAjbCVfaigxX0CDqWeR1yFL9kwd9P0NsZRPsmoqVwMbMu7mStFai6aIhc3n\n"
				"Slv8kg9qv1m6XHVQY3PnEw+QQtqSIXklHwIDAQAB\n"
				"-----END RSA PUBLIC KEY-----";
			Keys[0].Fingerprint = 0xc3b42b026ce86b21LL;

			Keys[1].Key = L"-----BEGIN RSA PUBLIC KEY-----\n"
				"MIIBCgKCAQEAxq7aeLAqJR20tkQQMfRn+ocfrtMlJsQ2Uksfs7Xcoo77jAid0bRt\n"
				"ksiVmT2HEIJUlRxfABoPBV8wY9zRTUMaMA654pUX41mhyVN+XoerGxFvrs9dF1Ru\n"
				"vCHbI02dM2ppPvyytvvMoefRoL5BTcpAihFgm5xCaakgsJ/tH5oVl74CdhQw8J5L\n"
				"xI/K++KJBUyZ26Uba1632cOiq05JBUW0Z2vWIOk4BLysk7+U9z+SxynKiZR3/xdi\n"
				"XvFKk01R3BHV+GUKM2RYazpS/P8v7eyKhAbKxOdRcFpHLlVwfjyM1VlDQrEZxsMp\n"
				"NTLYXb6Sce1Uov0YtNx5wEowlREH1WOTlwIDAQAB\n"
				"-----END RSA PUBLIC KEY-----";
			Keys[1].Fingerprint = 0x9a996a1db11c729bLL;

			Keys[2].Key = L"-----BEGIN RSA PUBLIC KEY-----\n"
				"MIIBCgKCAQEAsQZnSWVZNfClk29RcDTJQ76n8zZaiTGuUsi8sUhW8AS4PSbPKDm+\n"
				"DyJgdHDWdIF3HBzl7DHeFrILuqTs0vfS7Pa2NW8nUBwiaYQmPtwEa4n7bTmBVGsB\n"
				"1700/tz8wQWOLUlL2nMv+BPlDhxq4kmJCyJfgrIrHlX8sGPcPA4Y6Rwo0MSqYn3s\n"
				"g1Pu5gOKlaT9HKmE6wn5Sut6IiBjWozrRQ6n5h2RXNtO7O2qCDqjgB2vBxhV7B+z\n"
				"hRbLbCmW0tYMDsvPpX5M8fsO05svN+lKtCAuz1leFns8piZpptpSCFn7bWxiA9/f\n"
				"x5x17D7pfah3Sy2pA+NDXyzSlGcKdaUmwQIDAQAB\n"
				"-----END RSA PUBLIC KEY-----";
			Keys[2].Fingerprint = 0xb05b2a6f70cdea78LL;

			Keys[3].Key = L"-----BEGIN RSA PUBLIC KEY-----\n"
				"MIIBCgKCAQEAwqjFW0pi4reKGbkc9pK83Eunwj/k0G8ZTioMMPbZmW99GivMibwa\n"
				"xDM9RDWabEMyUtGoQC2ZcDeLWRK3W8jMP6dnEKAlvLkDLfC4fXYHzFO5KHEqF06i\n"
				"qAqBdmI1iBGdQv/OQCBcbXIWCGDY2AsiqLhlGQfPOI7/vvKc188rTriocgUtoTUc\n"
				"/n/sIUzkgwTqRyvWYynWARWzQg0I9olLBBC2q5RQJJlnYXZwyTL3y9tdb7zOHkks\n"
				"WV9IMQmZmyZh/N7sMbGWQpt4NMchGpPGeJ2e5gHBjDnlIf2p1yZOYeUYrdbwcS0t\n"
				"UiggS4UeE8TzIuXFQxw7fzEIlmhIaq3FnwIDAQAB\n"
				"-----END RSA PUBLIC KEY-----";
			Keys[3].Fingerprint = 0x71e025b6c76033e3LL;
		}

		std::vector<ServerPublicKey> Keys;
	};

	static const ServerPublicKeys publicKeys;

	for (size_t i = 0; i < publicKeys.Keys.size(); i++)
	{
		for (size_t j = 0; j < fingerprints.size(); j++)
		{
			if (fingerprints[j] == publicKeys.Keys[i].Fingerprint)
			{
				*publicKey = &publicKeys.Keys[i];
				return true;
			}
		}
	}

	return false;
}