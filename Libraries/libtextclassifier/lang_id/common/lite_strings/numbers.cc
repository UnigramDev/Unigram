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

#include "lang_id/common/lite_strings/numbers.h"

#include <ctype.h>
#include <stdlib.h>

#include <climits>

namespace libtextclassifier3 {
namespace mobile {

// Returns true if the characters that start at address ptr (inclusive) and stop
// at the first '\0' consist of only whitespaces, as determined by isspace().
// Note: this function returns false if ptr is nullptr.
static bool OnlyWhitespaces(const char *ptr) {
  if (ptr == nullptr) {
    return false;
  }
  for (; *ptr != '\0'; ++ptr) {
    if (!isspace(*ptr)) {
      return false;
    }
  }
  return true;
}

bool LiteAtoi(const char *c_str, int *value) {
  if (c_str == nullptr) {
    return false;
  }

  // Short version of man strtol:
  //
  // strtol parses some optional whitespaces, an optional +/- sign, and next a
  // succession of digits.  If it finds some digits, it sets temp to point to
  // the first character after that succession of digits and returns the parsed
  // integer.
  //
  // If there were no digits at all, strtol() sets temp to be c_str (the start
  // address) and returns 0.
  char *temp = nullptr;
  const long int parsed_value = strtol(c_str, &temp, 0);  // NOLINT

  // Check for overflow.  Note: to simplify the code, we assume that LONG_MIN /
  // LONG_MAX means that strtol encountered an overflow (normally, in that case,
  // one should also inspect errno).  Hence, we maybe give up the possibility to
  // parse one extreme value on each side (min/max).  That should be ok.
  if ((parsed_value == LONG_MIN) || (parsed_value == LONG_MAX) ||
      (parsed_value < INT_MIN) || (parsed_value > INT_MAX)) {
    return false;
  }
  *value = static_cast<int>(parsed_value);

  // First part of the expression below means that the input string contained at
  // least one digit.  The other part checks that what remains after the number
  // (if anything) consists only of whitespaces.
  return (temp != c_str) && OnlyWhitespaces(temp);
}

bool LiteAtof(const char *c_str, float *value) {
  if (c_str == nullptr) {
    return false;
  }

  // strtof is similar to strtol, see more detailed comments inside LiteAtoi.
  char *temp = nullptr;
  *value = strtof(c_str, &temp);
  return (temp != c_str) && OnlyWhitespaces(temp);
}

}  // namespace mobile
}  // namespace nlp_saft
