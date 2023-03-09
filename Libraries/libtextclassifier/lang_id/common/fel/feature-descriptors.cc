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

#include "lang_id/common/fel/feature-descriptors.h"

#include <string>

#include "lang_id/common/lite_strings/str-cat.h"

namespace libtextclassifier3 {
namespace mobile {

void ToFELFunction(const FeatureFunctionDescriptor &function,
                   std::string *output) {
  LiteStrAppend(output, function.type());
  if (function.argument() != 0 || function.parameter_size() > 0) {
    LiteStrAppend(output, "(");
    bool first = true;
    if (function.argument() != 0) {
      LiteStrAppend(output, function.argument());
      first = false;
    }
    for (int i = 0; i < function.parameter_size(); ++i) {
      if (!first) LiteStrAppend(output, ",");
      LiteStrAppend(output, function.parameter(i).name(), "=\"",
                    function.parameter(i).value(), "\"");
      first = false;
    }
    LiteStrAppend(output, ")");
  }
}

void ToFEL(const FeatureFunctionDescriptor &function, std::string *output) {
  ToFELFunction(function, output);
  if (function.feature_size() == 1) {
    LiteStrAppend(output, ".");
    ToFEL(function.feature(0), output);
  } else if (function.feature_size() > 1) {
    LiteStrAppend(output, " { ");
    for (int i = 0; i < function.feature_size(); ++i) {
      if (i > 0) LiteStrAppend(output, " ");
      ToFEL(function.feature(i), output);
    }
    LiteStrAppend(output, " } ");
  }
}

void ToFEL(const FeatureExtractorDescriptor &extractor, std::string *output) {
  for (int i = 0; i < extractor.feature_size(); ++i) {
    ToFEL(extractor.feature(i), output);
    LiteStrAppend(output, "\n");
  }
}

std::string FeatureFunctionDescriptor::DebugString() const {
  std::string str;
  ToFEL(*this, &str);
  return str;
}

std::string FeatureExtractorDescriptor::DebugString() const {
  std::string str;
  ToFEL(*this, &str);
  return str;
}

}  // namespace mobile
}  // namespace nlp_saft
