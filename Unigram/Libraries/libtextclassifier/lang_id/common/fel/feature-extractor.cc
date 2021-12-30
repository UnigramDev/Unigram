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

#include "lang_id/common/fel/feature-extractor.h"

#include <string>

#include "lang_id/common/fel/feature-types.h"
#include "lang_id/common/fel/fel-parser.h"
#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_strings/numbers.h"

namespace libtextclassifier3 {
namespace mobile {

constexpr FeatureValue GenericFeatureFunction::kNone;

GenericFeatureExtractor::GenericFeatureExtractor() {}

GenericFeatureExtractor::~GenericFeatureExtractor() {}

bool GenericFeatureExtractor::Parse(const std::string &source) {
  // Parse feature specification into descriptor.
  FELParser parser;

  if (!parser.Parse(source, mutable_descriptor())) {
    SAFTM_LOG(ERROR) << "Error parsing the FEL spec " << source;
    return false;
  }

  // Initialize feature extractor from descriptor.
  return InitializeFeatureFunctions();
}

bool GenericFeatureExtractor::InitializeFeatureTypes() {
  // Register all feature types.
  GetFeatureTypes(&feature_types_);
  for (size_t i = 0; i < feature_types_.size(); ++i) {
    FeatureType *ft = feature_types_[i];
    ft->set_base(i);

    // Check for feature space overflow.
    double domain_size = ft->GetDomainSize();
    if (domain_size < 0) {
      SAFTM_LOG(ERROR) << "Illegal domain size for feature " << ft->name()
                       << ": " << domain_size;
      return false;
    }
  }
  return true;
}

std::string GenericFeatureFunction::GetParameter(
    const std::string &name, const std::string &default_value) const {
  // Find named parameter in feature descriptor.
  for (int i = 0; i < descriptor_->parameter_size(); ++i) {
    if (name == descriptor_->parameter(i).name()) {
      return descriptor_->parameter(i).value();
    }
  }
  return default_value;
}

GenericFeatureFunction::GenericFeatureFunction() {}

GenericFeatureFunction::~GenericFeatureFunction() { delete feature_type_; }

int GenericFeatureFunction::GetIntParameter(const std::string &name,
                                            int default_value) const {
  std::string value_str = GetParameter(name, "");
  if (value_str.empty()) {
    // Parameter not specified, use default value for it.
    return default_value;
  }
  int value = 0;
  if (!LiteAtoi(value_str, &value)) {
    SAFTM_LOG(DFATAL) << "Unable to parse '" << value_str
                      << "' as int for parameter " << name;
    return default_value;
  }
  return value;
}

bool GenericFeatureFunction::GetBoolParameter(const std::string &name,
                                              bool default_value) const {
  std::string value = GetParameter(name, "");
  if (value.empty()) return default_value;
  if (value == "true") return true;
  if (value == "false") return false;
  SAFTM_LOG(DFATAL) << "Illegal value '" << value << "' for bool parameter "
                    << name;
  return default_value;
}

void GenericFeatureFunction::GetFeatureTypes(
    std::vector<FeatureType *> *types) const {
  if (feature_type_ != nullptr) types->push_back(feature_type_);
}

FeatureType *GenericFeatureFunction::GetFeatureType() const {
  // If a single feature type has been registered return it.
  if (feature_type_ != nullptr) return feature_type_;

  // Get feature types for function.
  std::vector<FeatureType *> types;
  GetFeatureTypes(&types);

  // If there is exactly one feature type return this, else return null.
  if (types.size() == 1) return types[0];
  return nullptr;
}

std::string GenericFeatureFunction::name() const {
  std::string output;
  if (descriptor_->name().empty()) {
    if (!prefix_.empty()) {
      output.append(prefix_);
      output.append(".");
    }
    ToFEL(*descriptor_, &output);
  } else {
    output = descriptor_->name();
  }
  return output;
}

}  // namespace mobile
}  // namespace nlp_saft
