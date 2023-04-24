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
#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_COMPACT_LOGGING_LEVELS_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_COMPACT_LOGGING_LEVELS_H_

#undef ERROR

namespace libtextclassifier3 {
namespace mobile {
namespace internal_logging {

enum LogSeverity {
  FATAL = 0,
  ERROR,
  WARNING,
  INFO,

  // In debug mode, DFATAL has the same semantics as FATAL.  Otherwise, it
  // behaves like ERROR.
  DFATAL,
};

}  // namespace internal_logging
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_COMPACT_LOGGING_LEVELS_H_
