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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_STR_CAT_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_STR_CAT_H_

// Less efficient but more compact versions of several absl string utils.
//
// "More compact" means "pulls in fewer code dependencies".  That's useful if
// one tries to minimize the code size.
//
// Note: the name and the signature of the functions from this header were
// chosen to minimize the effort of converting code that uses absl::LiteStrCat &
// co to our more compact functions.

#include <string>

#ifdef COMPILER_MSVC
#include <sstream>
#endif  // COMPILER_MSVC

namespace libtextclassifier3 {
namespace mobile {

// Less efficient but more compact version of absl::LiteStrCat().
//
// Given a value v (see supported types below) LiteStrCat(v) returns a new
// string that contains the representation of v.  For examples, see
// str-cat_test.cc.
template <typename T>
inline std::string LiteStrCat(T v) {
#ifdef COMPILER_MSVC
  std::stringstream stream;
  stream << v;
  return stream.str();
#else
  return std::to_string(v);
#endif
}

template <>
inline std::string LiteStrCat(const char *v) {
  return std::string(v);
}

// TODO(salcianu): use a reference type (const std::string &).  For some reason,
// I couldn't get that to work on a first try.
template <>
inline std::string LiteStrCat(std::string v) {
  return v;
}

template <>
inline std::string LiteStrCat(char v) {
  return std::string(1, v);
}

// Less efficient but more compact version of absl::LiteStrAppend().
template <typename T>
inline void LiteStrAppend(std::string *dest, T v) {
  dest->append(LiteStrCat(v));  // NOLINT
}

template <typename T1, typename T2>
inline void LiteStrAppend(std::string *dest, T1 v1, T2 v2) {
  dest->append(LiteStrCat(v1));  // NOLINT
  dest->append(LiteStrCat(v2));  // NOLINT
}

template <typename T1, typename T2, typename T3>
inline void LiteStrAppend(std::string *dest, T1 v1, T2 v2, T3 v3) {
  LiteStrAppend(dest, v1, v2);
  dest->append(LiteStrCat(v3));  // NOLINT
}

template <typename T1, typename T2, typename T3, typename T4>
inline void LiteStrAppend(std::string *dest, T1 v1, T2 v2, T3 v3, T4 v4) {
  LiteStrAppend(dest, v1, v2, v3);
  dest->append(LiteStrCat(v4));  // NOLINT
}

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_STR_CAT_H_
