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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_FEATURE_DESCRIPTORS_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_FEATURE_DESCRIPTORS_H_

#include <memory>
#include <string>
#include <vector>

#include "lang_id/common/lite_base/integral-types.h"
#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_base/macros.h"

namespace libtextclassifier3 {
namespace mobile {

// Named feature parameter.
class Parameter {
 public:
  Parameter() {}

  void set_name(const std::string &name) { name_ = name; }
  const std::string &name() const { return name_; }

  void set_value(const std::string &value) { value_ = value; }
  const std::string &value() const { return value_; }

 private:
  std::string name_;
  std::string value_;
};

// Descriptor for a feature function.  Used to store the results of parsing one
// feature function.
class FeatureFunctionDescriptor {
 public:
  FeatureFunctionDescriptor() {}

  // Accessors for the feature function type.  The function type is the string
  // that the feature extractor code is registered under.
  void set_type(const std::string &type) { type_ = type; }
  const std::string &type() const { return type_; }

  // Accessors for the feature function name.  The function name (if available)
  // is used for some log messages.  Otherwise, a more precise, but also more
  // verbose name based on the feature specification is used.
  void set_name(const std::string &name) { name_ = name; }
  const std::string &name() const { return name_; }

  // Accessors for the default (name-less) parameter.
  void set_argument(int32 argument) { argument_ = argument; }
  bool has_argument() const {
    // If argument has not been specified, clients should treat it as 0.  This
    // makes the test below correct, without having a separate has_argument_
    // bool field.
    return argument_ != 0;
  }
  int32 argument() const { return argument_; }

  // Accessors for the named parameters.
  Parameter *add_parameter() {
    parameters_.emplace_back();
    return &(parameters_.back());
  }
  int parameter_size() const { return parameters_.size(); }
  const Parameter &parameter(int i) const {
    SAFTM_DCHECK((i >= 0) && (i < parameter_size()));
    return parameters_[i];
  }

  // Accessors for the sub (i.e., nested) features.  Nested features: as in
  // offset(1).label.
  FeatureFunctionDescriptor *add_feature() {
    sub_features_.emplace_back(new FeatureFunctionDescriptor());
    return sub_features_.back().get();
  }
  int feature_size() const { return sub_features_.size(); }
  const FeatureFunctionDescriptor &feature(int i) const {
    SAFTM_DCHECK((i >= 0) && (i < feature_size()));
    return *(sub_features_[i].get());
  }

  // Returns human-readable representation of this FeatureFunctionDescriptor.
  std::string DebugString() const;

 private:
  // See comments for set_type().
  std::string type_;

  // See comments for set_name().
  std::string name_;

  // See comments for set_argument().
  int32 argument_ = 0;

  // See comemnts for add_parameter().
  std::vector<Parameter> parameters_;

  // See comments for add_feature().
  std::vector<std::unique_ptr<FeatureFunctionDescriptor>> sub_features_;

  SAFTM_DISALLOW_COPY_AND_ASSIGN(FeatureFunctionDescriptor);
};

// List of FeatureFunctionDescriptors.  Used to store the result of parsing the
// spec for several feature functions.
class FeatureExtractorDescriptor {
 public:
  FeatureExtractorDescriptor() {}

  int feature_size() const { return features_.size(); }

  FeatureFunctionDescriptor *add_feature() {
    features_.emplace_back(new FeatureFunctionDescriptor());
    return features_.back().get();
  }

  const FeatureFunctionDescriptor &feature(int i) const {
    SAFTM_DCHECK((i >= 0) && (i < feature_size()));
    return *(features_[i].get());
  }

  // Returns human-readable representation of this FeatureExtractorDescriptor.
  std::string DebugString() const;

 private:
  std::vector<std::unique_ptr<FeatureFunctionDescriptor>> features_;

  SAFTM_DISALLOW_COPY_AND_ASSIGN(FeatureExtractorDescriptor);
};

// Appends to |*output| the FEL representation of the top-level feature from
// |function|, without diving into the nested features.
void ToFELFunction(const FeatureFunctionDescriptor &function,
                   std::string *output);

// Appends to |*output| the FEL representation of |function|.
void ToFEL(const FeatureFunctionDescriptor &function, std::string *output);

// Appends to |*output| the FEL representation of |extractor|.
void ToFEL(const FeatureExtractorDescriptor &extractor, std::string *output);

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_FEATURE_DESCRIPTORS_H_
