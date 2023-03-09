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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_NUMBERS_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_NUMBERS_H_

#include <string>

#include "lang_id/common/lite_strings/stringpiece.h"

namespace libtextclassifier3 {
namespace mobile {

// Parses an int from a C-style string; similar to absl::SimpleAtoi.
//
// c_str should point to a zero-terminated array of chars that contains the
// number representation as (a) "<radix-10-number>" (e.g., "721"), (b)
// "0x<radix-16-number>" (e.g., "0xa1"), or (c) "0<radix-8-number>" (e.g.,
// "017201").  Whitespaces (as determined by isspace()) are allowed before and
// after the number representation (but obviously not in the middle).
//
// Stores parsed number into *value.  Returns true on success, false on error.
// Note: presence of extra non-whitespace characters after the number counts as
// an error: e.g., parsing "123a" will return false due to the extra "a" (which
// is not a valid radix-10 digit).  This function also returns false for strings
// that do not contain any digit (e.g., ""), as well as for overflows /
// underflows.
bool LiteAtoi(const char *c_str, int *value);

inline bool LiteAtoi(const std::string &s, int *value) {
  return LiteAtoi(s.c_str(), value);
}

inline bool LiteAtoi(StringPiece sp, int *value) {
  // Unfortunately, we can't directly call LiteAtoi(sp.data()): LiteAtoi(const
  // char *) needs a zero-terminated string.
  const std::string temp(sp.data(), sp.size());
  return LiteAtoi(temp.c_str(), value);
}

// Like LiteAtoi, but for float; similar to absl::SimpleAtof.
//
// NOTE: currently, does not properly handle overflow / underflow.
// TODO(salcianu): fix that.
bool LiteAtof(const char *c_str, float *value);

inline bool LiteAtof(const std::string &s, float *value) {
  return LiteAtof(s.c_str(), value);
}

inline bool LiteAtof(StringPiece sp, float *value) {
  // Unfortunately, we can't directly call LiteAtoi(sp.data()): LiteAtoi(const
  // char *) needs a zero-terminated string.
  const std::string temp(sp.data(), sp.size());
  return LiteAtof(temp.c_str(), value);
}

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_NUMBERS_H_
