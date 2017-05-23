#pragma once

#define FAST_PQ_FACTORIZATION 1

#ifdef FAST_PQ_FACTORIZATION
#define GCD SteinGCD
#define FactorizePQ BrentFactorizePQ
#else
#define GCD EuclideGCD
#define FactorizePQ PollardFactorizePQ
#endif

inline UINT64 SteinGCD(UINT64 a, UINT64 b)
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

inline UINT64 EuclideGCD(UINT64 a, UINT64 b)
{
	UINT64 r;

	while (b != 0)
	{
		r = a % b;
		a = b;
		b = r;
	}

	return a;
}

inline boolean PollardFactorizePQ(UINT64 pq, UINT32& p, UINT32& q)
{
	return true;
}

inline boolean BrentFactorizePQ(UINT64 pq, UINT32& p, UINT32& q)
{
	return true;
}