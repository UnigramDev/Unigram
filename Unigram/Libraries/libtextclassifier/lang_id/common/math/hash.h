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

#ifndef TC3_STD_STRING_IMPORT
#define TC3_STD_STRING_IMPORT
#include <string>

namespace libtextclassifier3 {
using string = std::string;
template <class CharT, class Traits = std::char_traits<CharT>,
          class Allocator = std::allocator<CharT> >
using basic_string = std::basic_string<CharT, Traits, Allocator>;
}  // namespace libtextclassifier3
#endif
#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_HASH_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_HASH_H_

#include <string>

#include "lang_id/common/lite_base/integral-types.h"

namespace libtextclassifier3 {
namespace mobile {
namespace utils {

// Returns a 32 bit hash of the |n| bytes that start at |data|, using |seed| for
// internal initialization.  By changing the seed, one effectively gets
// different hash functions.
//
// NOTE: this function is guaranteed not to change in the future.
//
// IMPORTANT: for speed reasons, this method does not check its parameters
// |data| and |n|.  The caller should ensure that n >= 0 and that one can read
// from the memory area [data, data + n).
uint32 Hash32(const char *data, size_t n, uint32 seed);

static inline uint32 Hash32WithDefaultSeed(const char *data, size_t n) {
  return Hash32(data, n, 0xBEEF);
}

static inline uint32 Hash32WithDefaultSeed(const std::string &input) {
  return Hash32WithDefaultSeed(input.data(), input.size());
}

}  // namespace utils
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_HASH_H_
