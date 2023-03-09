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

#include "lang_id/common/fel/fel-parser.h"

#include <ctype.h>

#include <string>

#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_strings/numbers.h"

namespace libtextclassifier3 {
namespace mobile {

namespace {
inline bool IsValidCharAtStartOfIdentifier(char c) {
  return isalpha(c) || (c == '_') || (c == '/');
}

// Returns true iff character c can appear inside an identifier.
inline bool IsValidCharInsideIdentifier(char c) {
  return isalnum(c) || (c == '_') || (c == '-') || (c == '/');
}

// Returns true iff character c can appear at the beginning of a number.
inline bool IsValidCharAtStartOfNumber(char c) {
  return isdigit(c) || (c == '+') || (c == '-');
}

// Returns true iff character c can appear inside a number.
inline bool IsValidCharInsideNumber(char c) {
  return isdigit(c) || (c == '.');
}
}  // namespace

bool FELParser::Initialize(const std::string &source) {
  // Initialize parser state.
  source_ = source;
  current_ = source_.begin();
  item_start_ = line_start_ = current_;
  line_number_ = item_line_number_ = 1;

  // Read first input item.
  return NextItem();
}

void FELParser::ReportError(const std::string &error_message) {
  const int position = item_start_ - line_start_ + 1;
  const std::string line(line_start_, current_);

  SAFTM_LOG(ERROR) << "Error in feature model, line " << item_line_number_
                   << ", position " << position << ": " << error_message
                   << "\n    " << line << " <--HERE";
}

void FELParser::Next() {
  // Move to the next input character. If we are at a line break update line
  // number and line start position.
  if (CurrentChar() == '\n') {
    ++line_number_;
    ++current_;
    line_start_ = current_;
  } else {
    ++current_;
  }
}

bool FELParser::NextItem() {
  // Skip white space and comments.
  while (!eos()) {
    if (CurrentChar() == '#') {
      // Skip comment.
      while (!eos() && CurrentChar() != '\n') Next();
    } else if (isspace(CurrentChar())) {
      // Skip whitespace.
      while (!eos() && isspace(CurrentChar())) Next();
    } else {
      break;
    }
  }

  // Record start position for next item.
  item_start_ = current_;
  item_line_number_ = line_number_;

  // Check for end of input.
  if (eos()) {
    item_type_ = END;
    return true;
  }

  // Parse number.
  if (IsValidCharAtStartOfNumber(CurrentChar())) {
    std::string::iterator start = current_;
    Next();
    while (!eos() && IsValidCharInsideNumber(CurrentChar())) Next();
    item_text_.assign(start, current_);
    item_type_ = NUMBER;
    return true;
  }

  // Parse string.
  if (CurrentChar() == '"') {
    Next();
    std::string::iterator start = current_;
    while (CurrentChar() != '"') {
      if (eos()) {
        ReportError("Unterminated string");
        return false;
      }
      Next();
    }
    item_text_.assign(start, current_);
    item_type_ = STRING;
    Next();
    return true;
  }

  // Parse identifier name.
  if (IsValidCharAtStartOfIdentifier(CurrentChar())) {
    std::string::iterator start = current_;
    while (!eos() && IsValidCharInsideIdentifier(CurrentChar())) {
      Next();
    }
    item_text_.assign(start, current_);
    item_type_ = NAME;
    return true;
  }

  // Single character item.
  item_type_ = CurrentChar();
  Next();
  return true;
}

bool FELParser::Parse(const std::string &source,
                      FeatureExtractorDescriptor *result) {
  // Initialize parser.
  if (!Initialize(source)) {
    return false;
  }

  while (item_type_ != END) {
    // Current item should be a feature name.
    if (item_type_ != NAME) {
      ReportError("Feature type name expected");
      return false;
    }
    std::string name = item_text_;
    if (!NextItem()) {
      return false;
    }

    if (item_type_ == '=') {
      ReportError("Invalid syntax: feature expected");
      return false;
    } else {
      // Parse feature.
      FeatureFunctionDescriptor *descriptor = result->add_feature();
      descriptor->set_type(name);
      if (!ParseFeature(descriptor)) {
        return false;
      }
    }
  }

  return true;
}

bool FELParser::ParseFeature(FeatureFunctionDescriptor *result) {
  // Parse argument and parameters.
  if (item_type_ == '(') {
    if (!NextItem()) return false;
    if (!ParseParameter(result)) return false;
    while (item_type_ == ',') {
      if (!NextItem()) return false;
      if (!ParseParameter(result)) return false;
    }

    if (item_type_ != ')') {
      ReportError(") expected");
      return false;
    }
    if (!NextItem()) return false;
  }

  // Parse feature name.
  if (item_type_ == ':') {
    if (!NextItem()) return false;
    if (item_type_ != NAME && item_type_ != STRING) {
      ReportError("Feature name expected");
      return false;
    }
    std::string name = item_text_;
    if (!NextItem()) return false;

    // Set feature name.
    result->set_name(name);
  }

  // Parse sub-features.
  if (item_type_ == '.') {
    // Parse dotted sub-feature.
    if (!NextItem()) return false;
    if (item_type_ != NAME) {
      ReportError("Feature type name expected");
      return false;
    }
    std::string type = item_text_;
    if (!NextItem()) return false;

    // Parse sub-feature.
    FeatureFunctionDescriptor *subfeature = result->add_feature();
    subfeature->set_type(type);
    if (!ParseFeature(subfeature)) return false;
  } else if (item_type_ == '{') {
    // Parse sub-feature block.
    if (!NextItem()) return false;
    while (item_type_ != '}') {
      if (item_type_ != NAME) {
        ReportError("Feature type name expected");
        return false;
      }
      std::string type = item_text_;
      if (!NextItem()) return false;

      // Parse sub-feature.
      FeatureFunctionDescriptor *subfeature = result->add_feature();
      subfeature->set_type(type);
      if (!ParseFeature(subfeature)) return false;
    }
    if (!NextItem()) return false;
  }
  return true;
}

bool FELParser::ParseParameter(FeatureFunctionDescriptor *result) {
  if (item_type_ == NUMBER) {
    int argument;
    if (!LiteAtoi(item_text_, &argument)) {
      ReportError("Unable to parse number");
      return false;
    }
    if (!NextItem()) return false;

    // Set default argument for feature.
    result->set_argument(argument);
  } else if (item_type_ == NAME) {
    std::string name = item_text_;
    if (!NextItem()) return false;
    if (item_type_ != '=') {
      ReportError("= expected");
      return false;
    }
    if (!NextItem()) return false;
    if (item_type_ >= END) {
      ReportError("Parameter value expected");
      return false;
    }
    std::string value = item_text_;
    if (!NextItem()) return false;

    // Add parameter to feature.
    Parameter *parameter;
    parameter = result->add_parameter();
    parameter->set_name(name);
    parameter->set_value(value);
  } else {
    ReportError("Syntax error in parameter list");
    return false;
  }
  return true;
}

}  // namespace mobile
}  // namespace nlp_saft
