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

// Basic integer type definitions.

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_INTEGRAL_TYPES_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_INTEGRAL_TYPES_H_

#include <cstdint>

namespace libtextclassifier3 {
namespace mobile {

typedef unsigned int uint32;
typedef uint64_t uint64;

#ifndef SWIG
typedef int int32;
typedef unsigned char uint8;    // NOLINT
typedef unsigned short uint16;  // NOLINT

// A type to represent a Unicode code-point value. As of Unicode 4.0,
// such values require up to 21 bits.
// (For type-checking on pointers, make this explicitly signed,
// and it should always be the signed version of whatever int32 is.)
typedef signed int char32;
#endif  // SWIG

using int64 = int64_t;

// Some compile-time assertions that our new types have the intended size.
static_assert(sizeof(int) == 4, "Our typedefs depend on int being 32 bits");
static_assert(sizeof(uint32) == 4, "wrong size");
static_assert(sizeof(int32) == 4, "wrong size");
static_assert(sizeof(uint8) == 1, "wrong size");
static_assert(sizeof(uint16) == 2, "wrong size");
static_assert(sizeof(char32) == 4, "wrong size");
static_assert(sizeof(int64) == 8, "wrong size");

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_INTEGRAL_TYPES_H_
