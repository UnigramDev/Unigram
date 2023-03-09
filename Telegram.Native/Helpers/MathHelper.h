#pragma once
#include <type_traits>
#include <algorithm>

#if defined(_M_IX86) || defined(_M_X64)
#include <xmmintrin.h>
#include <emmintrin.h>
#elif defined (_M_ARM)
#include <arm_neon.h>
#endif

#define PI 3.1415926535897931
#define TWOPI 2.0 * PI
#define PIOVERTWO PI / 2.0
#define ONEOVERPI 1.0 / PI
#define TWOOVERPI 2.0 / PI
#define LN_2 0.69314718056

namespace math
{

	template<typename _Ty>
	inline typename std::enable_if<std::is_arithmetic<_Ty>::value, _Ty>::type min(_Ty a, _Ty b)
	{
		return std::min(a, b);
	}

	template<>
	inline float min<float>(float a, float b)
	{
#if defined(_M_IX86) || defined(_M_X64)
		_mm_store_ss(&a, _mm_min_ss(_mm_set_ss(a), _mm_set_ss(b)));
#elif defined (_M_ARM)
		vst1_f32(&a, vmin_f32(vld1_f32(&a), vld1_f32(&b)));
#endif

		return a;
	}

	template<>
	inline double min<double>(double a, double b)
	{
#if defined(_M_IX86) || defined(_M_X64)
		_mm_store_sd(&a, _mm_min_sd(_mm_set_sd(a), _mm_set_sd(b)));

		return a;
#else
		return std::min(a, b);
#endif
	}

	template<typename _Ty>
	inline typename std::enable_if<std::is_arithmetic<_Ty>::value, _Ty>::type max(_Ty a, _Ty b)
	{
		return std::max(a, b);
	}

	template<>
	inline float max<float>(float a, float b)
	{
#if defined(_M_IX86) || defined(_M_X64)
		_mm_store_ss(&a, _mm_max_ss(_mm_set_ss(a), _mm_set_ss(b)));
#elif defined (_M_ARM)
		vst1_f32(&a, vmax_f32(vld1_f32(&a), vld1_f32(&b)));
#endif

		return a;
	}

	template<>
	inline double max<double>(double a, double b)
	{
#if defined(_M_IX86) || defined(_M_X64)
		_mm_store_sd(&a, _mm_max_sd(_mm_set_sd(a), _mm_set_sd(b)));

		return a;
#else
		return std::max(a, b);
#endif
	}

	template<typename _Ty>
	inline typename std::enable_if<std::is_arithmetic<_Ty>::value, _Ty>::type clamp(_Ty value, _Ty min, _Ty max)
	{
		return std::min(std::max(value, min), max);
	}

	template<>
	inline float clamp<float>(float value, float min, float max)
	{
#if defined(_M_IX86) || defined(_M_X64)
		_mm_store_ss(&value, _mm_min_ss(_mm_max_ss(_mm_set_ss(value), _mm_set_ss(min)), _mm_set_ss(max)));
#elif defined (_M_ARM)
		vst1_f32(&value, vmin_f32(vmax_f32(vld1_f32(&value), vld1_f32(&min)), vld1_f32(&max)));
#endif

		return value;
	}

	template<>
	inline double clamp<double>(double value, double min, double max)
	{
#if defined(_M_IX86) || defined(_M_X64)
		_mm_store_sd(&value, _mm_min_sd(_mm_max_sd(_mm_set_sd(value), _mm_set_sd(min)), _mm_set_sd(max)));

		return value;
#else
		return std::min(std::max(value, min), max);
#endif
	}

}
