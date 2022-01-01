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

// Common feature types for parser components.

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_FEATURE_TYPES_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_FEATURE_TYPES_H_

#include <algorithm>
#include <map>
#include <string>
#include <utility>

#include "lang_id/common/lite_base/integral-types.h"
#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_strings/str-cat.h"

namespace libtextclassifier3 {
namespace mobile {

// TODO(djweiss) Clean this up as well.
// Use the same type for feature values as is used for predicated.
typedef int64 Predicate;
typedef Predicate FeatureValue;

// Each feature value in a feature vector has a feature type. The feature type
// is used for converting feature type and value pairs to predicate values. The
// feature type can also return names for feature values and calculate the size
// of the feature value domain. The FeatureType class is abstract and must be
// specialized for the concrete feature types.
class FeatureType {
 public:
  // Initializes a feature type.
  explicit FeatureType(const std::string &name)
      : name_(name),
        base_(0),
        is_continuous_(name.find("continuous") != std::string::npos) {}

  virtual ~FeatureType() {}

  // Converts a feature value to a name.
  virtual std::string GetFeatureValueName(FeatureValue value) const = 0;

  // Returns the size of the feature values domain.
  virtual int64 GetDomainSize() const = 0;

  // Returns the feature type name.
  const std::string &name() const { return name_; }

  Predicate base() const { return base_; }
  void set_base(Predicate base) { base_ = base; }

  // Returns true iff this feature is continuous; see FloatFeatureValue.
  bool is_continuous() const { return is_continuous_; }

 private:
  // Feature type name.
  std::string name_;

  // "Base" feature value: i.e. a "slot" in a global ordering of features.
  Predicate base_;

  // See doc for is_continuous().
  bool is_continuous_;
};

// Feature type that is defined using an explicit map from FeatureValue to
// string values.  This can reduce some of the boilerplate when defining
// features that generate enum values.  Example usage:
//
//   class BeverageSizeFeature : public FeatureFunction<Beverage>
//     enum FeatureValue { SMALL, MEDIUM, LARGE };  // values for this feature
//     void Init(TaskContext *context) override {
//       set_feature_type(new EnumFeatureType("beverage_size",
//           {{SMALL, "SMALL"}, {MEDIUM, "MEDIUM"}, {LARGE, "LARGE"}});
//     }
//     [...]
//   };
class EnumFeatureType : public FeatureType {
 public:
  EnumFeatureType(const std::string &name,
                  const std::map<FeatureValue, std::string> &value_names)
      : FeatureType(name), value_names_(value_names) {
    for (const auto &pair : value_names) {
      SAFTM_CHECK_GE(pair.first, 0)
          << "Invalid feature value: " << pair.first << ", " << pair.second;
      domain_size_ = std::max(domain_size_, pair.first + 1);
    }
  }

  // Returns the feature name for a given feature value.
  std::string GetFeatureValueName(FeatureValue value) const override {
    auto it = value_names_.find(value);
    if (it == value_names_.end()) {
      SAFTM_LOG(ERROR) << "Invalid feature value " << value << " for "
                       << name();
      return "<INVALID>";
    }
    return it->second;
  }

  // Returns the number of possible values for this feature type. This is one
  // greater than the largest value in the value_names map.
  FeatureValue GetDomainSize() const override { return domain_size_; }

 protected:
  // Maximum possible value this feature could take.
  FeatureValue domain_size_ = 0;

  // Names of feature values.
  std::map<FeatureValue, std::string> value_names_;
};

// Feature type for binary features.
class BinaryFeatureType : public FeatureType {
 public:
  BinaryFeatureType(const std::string &name, const std::string &off,
                    const std::string &on)
      : FeatureType(name), off_(off), on_(on) {}

  // Returns the feature name for a given feature value.
  std::string GetFeatureValueName(FeatureValue value) const override {
    if (value == 0) return off_;
    if (value == 1) return on_;
    return "";
  }

  // Binary features always have two feature values.
  FeatureValue GetDomainSize() const override { return 2; }

 private:
  // Feature value names for on and off.
  std::string off_;
  std::string on_;
};

// Feature type for numeric features.
class NumericFeatureType : public FeatureType {
 public:
  // Initializes numeric feature.
  NumericFeatureType(const std::string &name, FeatureValue size)
      : FeatureType(name), size_(size) {}

  // Returns numeric feature value.
  std::string GetFeatureValueName(FeatureValue value) const override {
    if (value < 0) return "";
    return LiteStrCat(value);
  }

  // Returns the number of feature values.
  FeatureValue GetDomainSize() const override { return size_; }

 private:
  // The underlying size of the numeric feature.
  FeatureValue size_;
};

// Feature type for byte features, including an "outside" value.
class ByteFeatureType : public NumericFeatureType {
 public:
  explicit ByteFeatureType(const std::string &name)
      : NumericFeatureType(name, 257) {}

  std::string GetFeatureValueName(FeatureValue value) const override {
    if (value == 256) {
      return "<NULL>";
    }
    std::string result;
    result += static_cast<char>(value);
    return result;
  }
};

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_FEATURE_TYPES_H_
