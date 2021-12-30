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

#include "lang_id/custom-tokenizer.h"

#include <ctype.h>

#include <string>

#include "lang_id/common/lite_base/attributes.h"
#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/utf8.h"
#include "utf.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

namespace {
inline bool IsTokenSeparator(int num_bytes, const char *curr) {
  if (num_bytes != 1) {
    return false;
  }
  return !isalpha(*curr);
}

// Appends to *word the UTF8 encoding for the lowercase version of the UTF8
// character that starts at |curr| and has |num_bytes| bytes.
//
// NOTE: if the current UTF8 character does not have a lowercase version, then
// we append the original UTF8 character.
inline SAFTM_ATTRIBUTE_ALWAYS_INLINE void AppendLowerCase(const char *curr,
                                                          int num_bytes,
                                                          std::string *word) {
  if (num_bytes == 1) {
    // Optimize the ASCII case.
    word->push_back(tolower(*curr));
    return;
  }

  // Harder, general case.
  //
  // NOTE: for lowercasing, we use the utils from utf.h:
  // charntorune + tolowerrune + runetochar.  Unfortunately, that library does
  // not contain any fast util for determining the number of bytes for the UTF8
  // character that starts at a given address *without* converting to a full
  // codepoint (like our utils::OneCharLen, which is used intensively by the
  // rest of our code, including by the performance-critical char ngram
  // feature).  Hence, the rest of our code continues to use utils::OneCharLen,
  // and here, when we append the bytes to *word, we make sure that's consistent
  // with utils::OneCharLen.

  // charntorune() below reads the UTF8 character that starts at curr (using at
  // most num_bytes bytes) and stores the corresponding codepoint into rune.
  Rune rune;
  charntorune(&rune, curr, num_bytes);
  if (rune != Runeerror) {
    Rune lower = tolowerrune(rune);
    char lower_buf[UTFmax];
    runetochar(lower_buf, &lower);

    // When appending the UTF8 bytes to word, we do not use the number of bytes
    // returned by runetochar(); instead, we use utils::OneCharLen(), the same
    // method used by the char ngram feature.  We expect them to be equal, but
    // just in case.
    int lower_num_bytes = utils::OneCharLen(lower_buf);

    // Using lower_num_bytes below is safe, because, by definition of UTFmax,
    SAFTM_DCHECK_GE(UTFmax, 4);

    // And, by implementation of utils::OneCharLen():
    SAFTM_DCHECK_GT(lower_num_bytes, 0);
    SAFTM_DCHECK_LE(lower_num_bytes, 4);
    word->append(lower_buf, lower_num_bytes);
  } else {
    // There are sequences of bytes that charntorune() can't convert into a
    // valid Rune (a special case is [0xEF, 0xBF, 0xBD], the UTF8 encoding for
    // the U+FFFD special Unicode character, which is also the value of
    // Runeerror).  We keep those bytes unchanged.
    word->append(curr, num_bytes);
  }
}
}  // namespace

void TokenizerForLangId::Setup(TaskContext *context) {
  lowercase_input_ = context->Get("lang_id_lowercase_input", false);
}

void TokenizerForLangId::Tokenize(StringPiece text,
                                  LightSentence *sentence) const {
  const char *const start = text.data();
  const char *curr = start;
  const char *end = utils::GetSafeEndOfUtf8String(start, text.size());

  // Corner case: the safe part of the text is empty ("").
  if (curr >= end) {
    return;
  }

  // Number of bytes for UTF8 character starting at *curr.  Note: the loop below
  // is guaranteed to terminate because in each iteration, we move curr by at
  // least num_bytes, and num_bytes is guaranteed to be > 0.
  int num_bytes = utils::OneCharLen(curr);
  while (curr < end) {
    // Jump over consecutive token separators.
    while (IsTokenSeparator(num_bytes, curr)) {
      curr += num_bytes;
      if (curr >= end) {
        return;
      }
      num_bytes = utils::OneCharLen(curr);
    }

    // If control reaches this point, we are at beginning of a non-empty token.
    sentence->emplace_back();
    std::string *word = &(sentence->back());

    // Add special token-start character.
    word->push_back('^');

    // Add UTF8 characters to word, until we hit the end of the safe text or a
    // token separator.
    while (true) {
      if (lowercase_input_) {
        AppendLowerCase(curr, num_bytes, word);
      } else {
        word->append(curr, num_bytes);
      }
      curr += num_bytes;
      if (curr >= end) {
        break;
      }
      num_bytes = utils::OneCharLen(curr);
      if (IsTokenSeparator(num_bytes, curr)) {
        curr += num_bytes;
        if (curr >= end) {
          break;
        }
        num_bytes = utils::OneCharLen(curr);
        break;
      }
    }
    word->push_back('$');
  }
}

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft
