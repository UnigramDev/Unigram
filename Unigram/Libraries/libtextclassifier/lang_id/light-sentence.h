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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_LIGHT_SENTENCE_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_LIGHT_SENTENCE_H_

#include <string>
#include <vector>

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

// Very simplified alternative to heavy sentence.proto, for the purpose of
// LangId.  It turns out that in this case, all we need is a vector of strings,
// which uses a lot less code size than a Sentence proto.
using LightSentence = std::vector<std::string>;

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_LIGHT_SENTENCE_H_
