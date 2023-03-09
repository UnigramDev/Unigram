/*
 * Copyright (C) 2018 The Android Open Source Project
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_ENDIAN_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_ENDIAN_H_

#include "lang_id/common/lite_base/integral-types.h"

namespace libtextclassifier3 {
namespace mobile {

#if defined OS_LINUX || defined OS_CYGWIN || defined OS_ANDROID || \
    defined(__ANDROID__)
#include <endian.h>
#endif

// The following guarantees declaration of the byte swap functions, and
// defines __BYTE_ORDER for MSVC
#if defined(__GLIBC__) || defined(__CYGWIN__)
#include <byteswap.h>  // IWYU pragma: export

#else
#ifndef bswap_16
static inline uint16 bswap_16(uint16 x) {
  return (uint16)(((x & 0xFF) << 8) | ((x & 0xFF00) >> 8));  // NOLINT
}
#define bswap_16(x) bswap_16(x)
#endif  // bswap_16

#ifndef bswap_32
static inline uint32 bswap_32(uint32 x) {
  return (((x & 0xFF) << 24) | ((x & 0xFF00) << 8) | ((x & 0xFF0000) >> 8) |
          ((x & 0xFF000000) >> 24));
}
#define bswap_32(x) bswap_32(x)
#endif  // bswap_32

#ifndef bswap_64
#define SAFTM_GG_ULONGLONG(x) x##ULL
static inline uint64 bswap_64(uint64 x) {
  return (((x & SAFTM_GG_ULONGLONG(0xFF)) << 56) |
          ((x & SAFTM_GG_ULONGLONG(0xFF00)) << 40) |
          ((x & SAFTM_GG_ULONGLONG(0xFF0000)) << 24) |
          ((x & SAFTM_GG_ULONGLONG(0xFF000000)) << 8) |
          ((x & SAFTM_GG_ULONGLONG(0xFF00000000)) >> 8) |
          ((x & SAFTM_GG_ULONGLONG(0xFF0000000000)) >> 24) |
          ((x & SAFTM_GG_ULONGLONG(0xFF000000000000)) >> 40) |
          ((x & SAFTM_GG_ULONGLONG(0xFF00000000000000)) >> 56));
}
#define bswap_64(x) bswap_64(x)
#endif  // bswap_64

#endif

// define the macros SAFTM_IS_LITTLE_ENDIAN or SAFTM_IS_BIG_ENDIAN using the
// above endian definitions from endian.h if endian.h was included
#ifdef __BYTE_ORDER
#if __BYTE_ORDER == __LITTLE_ENDIAN
#define SAFTM_IS_LITTLE_ENDIAN
#endif

#if __BYTE_ORDER == __BIG_ENDIAN
#define SAFTM_IS_BIG_ENDIAN
#endif

#else  // __BYTE_ORDER

#if defined(__LITTLE_ENDIAN__)
#define SAFTM_IS_LITTLE_ENDIAN
#elif defined(__BIG_ENDIAN__)
#define SAFTM_IS_BIG_ENDIAN
#elif REG_DWORD == REG_DWORD_LITTLE_ENDIAN
#define SAFTM_IS_LITTLE_ENDIAN
#else
#define SAFTM_IS_BIG_ENDIAN
#endif

// there is also PDP endian ...

#endif  // __BYTE_ORDER

class LittleEndian {
 public:
// Conversion functions.
#ifdef SAFTM_IS_LITTLE_ENDIAN

  static bool IsLittleEndian() { return true; }

#elif defined SAFTM_IS_BIG_ENDIAN

  static uint16 FromHost16(uint16 x) { return gbswap_16(x); }
  static uint16 ToHost16(uint16 x) { return gbswap_16(x); }

  static uint32 FromHost32(uint32 x) { return gbswap_32(x); }
  static uint32 ToHost32(uint32 x) { return gbswap_32(x); }

  static uint64 FromHost64(uint64 x) { return gbswap_64(x); }
  static uint64 ToHost64(uint64 x) { return gbswap_64(x); }

  static bool IsLittleEndian() { return false; }

#endif /* ENDIAN */
};

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_ENDIAN_H_
