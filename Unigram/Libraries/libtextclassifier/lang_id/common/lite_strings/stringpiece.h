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
#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_STRINGPIECE_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_STRINGPIECE_H_

#include <stddef.h>
#include <string.h>

#include <ostream>
#include <string>

namespace libtextclassifier3 {
namespace mobile {

// Read-only "view" of a piece of data.  Does not own the underlying data.
class StringPiece {
 public:
  StringPiece() : StringPiece(nullptr, 0) {}

  StringPiece(const char *str)  // NOLINT
      : start_(str), size_(strlen(str)) {}

  StringPiece(const char *start, size_t size) : start_(start), size_(size) {}

  // Intentionally no "explicit" keyword: in function calls, we want strings to
  // be converted to StringPiece implicitly.
  StringPiece(const std::string &s)  // NOLINT
      : StringPiece(s.data(), s.size()) {}

  StringPiece(const std::string &s, int offset, int len)
      : StringPiece(s.data() + offset, len) {}

  char operator[](size_t i) const { return start_[i]; }

  // Returns start address of underlying data.
  const char *data() const { return start_; }

  // Returns number of bytes of underlying data.
  size_t size() const { return size_; }

  // Returns true if this StringPiece does not refer to any characters.
  bool empty() const { return size() == 0; }

  template <typename A>
  explicit operator std::basic_string<char, std::char_traits<char>, A>() const {
    if (!data()) return {};
    return std::basic_string<char, std::char_traits<char>, A>(data(), size());
  }

 private:
  const char *start_;  // Not owned.
  size_t size_;
};

inline std::ostream &operator<<(std::ostream &out, StringPiece sp) {
  return out.write(sp.data(), sp.size());
}

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_STRINGPIECE_H_
