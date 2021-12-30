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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_STR_SPLIT_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_STR_SPLIT_H_

#include <vector>

#include "lang_id/common/lite_strings/stringpiece.h"

namespace libtextclassifier3 {
namespace mobile {

// Splits |text| on |delim|; similar to absl::StrSplit.
//
// Returns a list of tokens.  Each token is represented by a StringPiece that
// indicates a range of chars from |text|.
//
// Example: StrSplit("apple,orange", ',') returns two tokens: a StringPiece that
// points to "apple", and another one for "orange".
//
// If one concatenates all returned tokens with |delim| in between, one gets the
// original |text|.  E.g., If we split "apple,orange," on ',', we get three
// tokens: "apple", "orange" and "" (an empty token).  We do not filter out
// empty tokens.  If necessary, the caller can do that.
//
// Note: if the input text is empty, we return an empty list of tokens.  In
// general, the number of returned tokens is 1 + the number of occurences of
// |delim| inside |text|.
std::vector<StringPiece> LiteStrSplit(StringPiece text, char delim);

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_STRINGS_STR_SPLIT_H_
