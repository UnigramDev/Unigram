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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_CUSTOM_TOKENIZER_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_CUSTOM_TOKENIZER_H_

#include <string>

#include "lang_id/common/fel/task-context.h"
#include "lang_id/common/lite_strings/stringpiece.h"
#include "lang_id/light-sentence.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

// Custom tokenizer for the LangId model.
class TokenizerForLangId {
 public:
  void Setup(TaskContext *context);

  // Tokenizes |text|, placing the tokens into |sentence|.  Customized for
  // LangId.  Currently (Sep 15, 2016) we tokenize on space, newline, tab, and
  // any other 1-byte UTF8 character which is not a letter, ignore all empty
  // tokens, and (for each of the remaining tokens) prepend "^" (special token
  // begin marker) and append "$" (special token end marker).
  //
  // Tokens are stored into the "repeated Token token;" field of *sentence.
  void Tokenize(StringPiece text, LightSentence *sentence) const;

 private:
  // If true, during tokenization, we use the lowercase version of each Unicode
  // character from the text to tokenize.  E.g., if this is true, the text "Foo
  // bar" is tokenized as ["foo", "bar"]; otherwise, we get ["Foo", "bar"].
  bool lowercase_input_ = false;
};

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_CUSTOM_TOKENIZER_H_
