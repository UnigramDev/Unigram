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
#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_UTF8_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_UTF8_H_

#include <stddef.h>

#include <string>

namespace libtextclassifier3 {
namespace mobile {
namespace utils {

// Returns the length (number of bytes) of the UTF8 code point starting at src,
// by reading only the byte from address src.
//
// The result is a number from the set {1, 2, 3, 4}.
static inline int OneCharLen(const char *src) {
  // On most platforms, char is unsigned by default, but iOS is an exception.
  // The cast below makes sure we always interpret *src as an unsigned char.
  return "\1\1\1\1\1\1\1\1\1\1\1\1\2\2\3\4"
      [(*(reinterpret_cast<const unsigned char *>(src)) & 0xFF) >> 4];
}

// Returns a pointer "end" inside [data, data + size) such that the prefix from
// [data, end) is the largest one that does not contain '\0' and offers the
// following guarantee: if one starts with
//
//   curr = text.data()
//
// and keeps executing
//
//   curr += OneCharLen(curr)
//
// one would eventually reach curr == end (the pointer returned by this
// function) without accessing data outside the string.  This guards against
// scenarios like a broken UTF8 string which has only e.g., the first 2 bytes
// from a 3-byte UTF8 sequence.
//
// Preconditions: data != nullptr.
const char *GetSafeEndOfUtf8String(const char *data, size_t size);

static inline const char *GetSafeEndOfUtf8String(const std::string &text) {
  return GetSafeEndOfUtf8String(text.data(), text.size());
}

}  // namespace utils
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_UTF8_H_
