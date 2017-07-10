#include "pch.h"
#include <Windows.h>
#include "DatacenterCryptography.h"
#include "TLBinaryWriter.h"
#include "Wrappers\OpenSSL.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;
using namespace Telegram::Api::Native::Wrappers;


void DatacenterCryptography::WriteTLBigNum(BYTE* buffer, BIGNUM* number, UINT32 length)
{
	if (length < 254)
	{
		buffer[0] = length;

		BN_bn2bin(number, buffer + 1);
	}
	else
	{
		buffer[0] = 254;
		buffer[1] = length & 0xff;
		buffer[2] = (length >> 8) & 0xff;
		buffer[3] = (length >> 16) & 0xff;

		BN_bn2bin(number, buffer + 4);
	}
}

bool DatacenterCryptography::FactorizePQ(UINT64 pq, UINT32& p, UINT32& q)
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

INT64 DatacenterCryptography::ComputePublickKeyFingerprint(::RSA* key)
{
	auto nLength = BN_num_bytes(key->n);
	auto nTLLength = TLMemoryBinaryWriter::GetByteArrayLength(nLength);
	auto eLength = BN_num_bytes(key->e);
	auto eTLLength = TLMemoryBinaryWriter::GetByteArrayLength(eLength);

	std::vector<BYTE> buffer(nTLLength + eTLLength);
	WriteTLBigNum(buffer.data(), key->n, nLength);
	WriteTLBigNum(buffer.data() + nTLLength, key->e, eLength);

	BYTE sha1[SHA_DIGEST_LENGTH];
	SHA1(buffer.data(), buffer.size(), sha1);

	return (static_cast<INT64>(sha1[19]) << 56LL) | (static_cast<INT64>(sha1[18]) << 48LL) |
		(static_cast<INT64>(sha1[17]) << 40LL) | (static_cast<INT64>(sha1[16]) << 32LL) |
		(static_cast<INT64>(sha1[15]) << 24LL) | (static_cast<INT64>(sha1[14]) << 16LL) |
		(static_cast<INT64>(sha1[13]) << 8LL) | static_cast<INT64>(sha1[12]);
}

bool DatacenterCryptography::GetDatacenterPublicKey(std::vector<INT64> const& fingerprints, _Out_ ServerPublicKey const** publicKey)
{
	struct ServerPublicKeys
	{
		ServerPublicKeys() :
			Keys(4)
		{
			Keys[0].Key.Attach(GetRSAPublicKey("-----BEGIN RSA PUBLIC KEY-----\n"
				"MIIBCgKCAQEAwVACPi9w23mF3tBkdZz+zwrzKOaaQdr01vAbU4E1pvkfj4sqDsm6\n"
				"lyDONS789sVoD/xCS9Y0hkkC3gtL1tSfTlgCMOOul9lcixlEKzwKENj1Yz/s7daS\n"
				"an9tqw3bfUV/nqgbhGX81v/+7RFAEd+RwFnK7a+XYl9sluzHRyVVaTTveB2GazTw\n"
				"Efzk2DWgkBluml8OREmvfraX3bkHZJTKX4EQSjBbbdJ2ZXIsRrYOXfaA+xayEGB+\n"
				"8hdlLmAjbCVfaigxX0CDqWeR1yFL9kwd9P0NsZRPsmoqVwMbMu7mStFai6aIhc3n\n"
				"Slv8kg9qv1m6XHVQY3PnEw+QQtqSIXklHwIDAQAB\n"
				"-----END RSA PUBLIC KEY-----"));
			Keys[0].Fingerprint = 0xc3b42b026ce86b21LL;

			Keys[1].Key.Attach(GetRSAPublicKey("-----BEGIN RSA PUBLIC KEY-----\n"
				"MIIBCgKCAQEAxq7aeLAqJR20tkQQMfRn+ocfrtMlJsQ2Uksfs7Xcoo77jAid0bRt\n"
				"ksiVmT2HEIJUlRxfABoPBV8wY9zRTUMaMA654pUX41mhyVN+XoerGxFvrs9dF1Ru\n"
				"vCHbI02dM2ppPvyytvvMoefRoL5BTcpAihFgm5xCaakgsJ/tH5oVl74CdhQw8J5L\n"
				"xI/K++KJBUyZ26Uba1632cOiq05JBUW0Z2vWIOk4BLysk7+U9z+SxynKiZR3/xdi\n"
				"XvFKk01R3BHV+GUKM2RYazpS/P8v7eyKhAbKxOdRcFpHLlVwfjyM1VlDQrEZxsMp\n"
				"NTLYXb6Sce1Uov0YtNx5wEowlREH1WOTlwIDAQAB\n"
				"-----END RSA PUBLIC KEY-----"));
			Keys[1].Fingerprint = 0x9a996a1db11c729bLL;

			Keys[2].Key.Attach(GetRSAPublicKey("-----BEGIN RSA PUBLIC KEY-----\n"
				"MIIBCgKCAQEAsQZnSWVZNfClk29RcDTJQ76n8zZaiTGuUsi8sUhW8AS4PSbPKDm+\n"
				"DyJgdHDWdIF3HBzl7DHeFrILuqTs0vfS7Pa2NW8nUBwiaYQmPtwEa4n7bTmBVGsB\n"
				"1700/tz8wQWOLUlL2nMv+BPlDhxq4kmJCyJfgrIrHlX8sGPcPA4Y6Rwo0MSqYn3s\n"
				"g1Pu5gOKlaT9HKmE6wn5Sut6IiBjWozrRQ6n5h2RXNtO7O2qCDqjgB2vBxhV7B+z\n"
				"hRbLbCmW0tYMDsvPpX5M8fsO05svN+lKtCAuz1leFns8piZpptpSCFn7bWxiA9/f\n"
				"x5x17D7pfah3Sy2pA+NDXyzSlGcKdaUmwQIDAQAB\n"
				"-----END RSA PUBLIC KEY-----"));
			Keys[2].Fingerprint = 0xb05b2a6f70cdea78LL;

			Keys[3].Key.Attach(GetRSAPublicKey("-----BEGIN RSA PUBLIC KEY-----\n"
				"MIIBCgKCAQEAwqjFW0pi4reKGbkc9pK83Eunwj/k0G8ZTioMMPbZmW99GivMibwa\n"
				"xDM9RDWabEMyUtGoQC2ZcDeLWRK3W8jMP6dnEKAlvLkDLfC4fXYHzFO5KHEqF06i\n"
				"qAqBdmI1iBGdQv/OQCBcbXIWCGDY2AsiqLhlGQfPOI7/vvKc188rTriocgUtoTUc\n"
				"/n/sIUzkgwTqRyvWYynWARWzQg0I9olLBBC2q5RQJJlnYXZwyTL3y9tdb7zOHkks\n"
				"WV9IMQmZmyZh/N7sMbGWQpt4NMchGpPGeJ2e5gHBjDnlIf2p1yZOYeUYrdbwcS0t\n"
				"UiggS4UeE8TzIuXFQxw7fzEIlmhIaq3FnwIDAQAB\n"
				"-----END RSA PUBLIC KEY-----"));
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

bool DatacenterCryptography::IsGoodGaAndGb(BIGNUM* ga, BIGNUM* p)
{
	if (BN_num_bytes(ga) > 256 || BN_num_bits(ga) < 2048 - 64 || BN_cmp(p, ga) <= 0)
	{
		return false;
	}

	BigNum  dif(BN_new());
	BN_sub(dif.Get(), p, ga);

	return BN_num_bits(dif.Get()) >= 2048 - 64;
}

bool DatacenterCryptography::IsGoodPrime(BIGNUM* p, UINT32 g)
{
	if (g < 2 || g > 7 || BN_num_bits(p) != 2048)
	{
		return false;
	}

	BigNum t(BN_new());
	BigNum dh_g(BN_new());

	if (!BN_set_word(dh_g.Get(), 4 * g))
	{
		return false;
	}

	auto bnContext = GetBNContext();
	if (!BN_mod(t.Get(), p, dh_g.Get(), bnContext))
	{
		return false;
	}

	UINT64 x = BN_get_word(t.Get());
	if (x >= 4 * g)
	{
		return false;
	}

	bool result = true;
	switch (g)
	{
	case 2:
		if (x != 7)
		{
			result = false;
		}
		break;
	case 3:
		if (x % 3 != 2)
		{
			result = false;
		}
		break;
	case 5:
		if (x % 5 != 1 && x % 5 != 4)
		{
			result = false;
		}
		break;
	case 6:
		if (x != 19 && x != 23)
		{
			result = false;
		}
		break;
	case 7:
		if (x % 7 != 3 && x % 7 != 5 && x % 7 != 6)
		{
			result = false;
		}
		break;
	default:
		break;
	}

	auto prime = std::unique_ptr<char[]>(BN_bn2hex(p));
	static const char* goodPrime = "c71caeb9c6b1c9048e6c522f70f13f73980d40238e3e21c14934d037563d930f48198a0aa7c14058229493d22530f4dbfa336f6e0ac925139543"
		"aed44cce7c3720fd51f69458705ac68cd4fe6b6b13abdc9746512969328454f18faf8c595f642477fe96bb2a941d5bcd1d4ac8cc49880708fa9b378e3c4f3a9060bee67cf9a4a4a"
		"695811051907e162753b56b0f6b410dba74d8a84b2a14b3144e0ef1284754fd17ed950d5965b4b9dd46582db1178d169c6bc465b0d6ff9ca3928fef5b9ae4e418fc15e83ebea0f87"
		"fa9ff5eed70050ded2849f47bf959d956850ce929851f0d8115f635b105ee2e4e15d04b2454bf6f4fadf034b10403119cd8e3b92fcc5b";

	if (!_stricmp(prime.get(), goodPrime))
	{
		return true;
	}

	if (!result || !CheckPrime(p, bnContext))
	{
		return false;
	}

	BigNum b(BN_new());
	if (!BN_set_word(b.Get(), 2))
	{
		return false;
	}

	if (!BN_div(t.Get(), 0, p, b.Get(), bnContext))
	{
		return false;
	}

	if (!CheckPrime(t.Get(), bnContext))
	{
		return false;
	}

	return result;
}

BN_CTX* DatacenterCryptography::GetBNContext()
{
	static BigNumContext bnContext(BN_CTX_new());
	return bnContext.Get();
}