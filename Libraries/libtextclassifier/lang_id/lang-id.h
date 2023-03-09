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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_LANG_ID_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_LANG_ID_H_


#include <stddef.h>

#include <memory>
#include <string>
#include <utility>
#include <vector>

#include "lang_id/common/lite_base/macros.h"
#include "lang_id/model-provider.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

// Forward-declaration of the class that performs all underlying work.
class LangIdImpl;

struct LangIdResult {
  // An n-best list of possible language codes for a given input sorted in
  // descending order according to each code's respective probability.
  //
  // This list is guaranteed to be non-empty after calling
  // LangId::FindLanguages.  The most likely language code is always the first
  // item in this array.
  //
  // If the model cannot make a prediction, this array contains a single result:
  // a language code LangId::kUnknownLanguageCode with probability 1.
  std::vector<std::pair<std::string, float>> predictions;
};

// Class for detecting the language of a document.
//
// Note: this class does not handle the details of loading the actual model.
// Those details have been "outsourced" to the ModelProvider class.
//
// This class is thread safe.
class LangId {
 public:
  // Standard BCP-47 language code for Unknown/Undetermined language.
  static const char kUnknownLanguageCode[];

  // Constructs a LangId object, based on |model_provider|.
  //
  // Note: we don't crash if we detect a problem at construction time (e.g., the
  // model provider can't read an underlying file).  Instead, we mark the
  // newly-constructed object as invalid; clients can invoke FindLanguage() on
  // an invalid object: nothing crashes, but accuracy will be bad.
  explicit LangId(std::unique_ptr<ModelProvider> model_provider);

  virtual ~LangId();

  // Computes the n-best list of language codes and probabilities corresponding
  // to the most likely languages the given input text is written in.  That list
  // includes the most likely |max_results| languages and is sorted in
  // descending order by language probability.
  //
  // The input text consists of the |num_bytes| bytes that starts at |data|.
  //
  // If max_results <= 0, we report probabilities for all languages known by
  // this LangId object (as always, in decreasing order of their probabilities).
  //
  // Note: If this LangId object is not valid (see is_valid()) or if this LangId
  // object can't make a prediction, this method sets the LangIdResult to
  // contain a single entry with kUnknownLanguageCode with probability 1.
  //
  void FindLanguages(const char *data, size_t num_bytes, LangIdResult *result,
                     int max_results = 0) const;

  // Convenience version of FindLanguages(const char *, size_t, LangIdResult *).
  void FindLanguages(const std::string &text, LangIdResult *result,
                     int max_results = 0) const {
    FindLanguages(text.data(), text.size(), result, max_results);
  }

  // Returns language code for the most likely language for a piece of text.
  //
  // The input text consists of the |num_bytes| bytes that start at |data|.
  //
  // Note: this method reports the most likely (1-best) language only if its
  // probability is high enough; otherwise, it returns
  // LangId::kUnknownLanguageCode.  The specific probability threshold is tuned
  // to the needs of an early client.  If you need a different threshold, you
  // can use FindLanguages (plural) to get the full LangIdResult, and apply your
  // own threshold.
  //
  // Note: if this LangId object is not valid (see is_valid()) or if this LangId
  // object can't make a prediction, then this method returns
  // LangId::kUnknownLanguageCode.
  //
  std::string FindLanguage(const char *data, size_t num_bytes) const;

  // Convenience version of FindLanguage(const char *, size_t).
  std::string FindLanguage(const std::string &text) const {
    return FindLanguage(text.data(), text.size());
  }

  // Returns true if this object has been correctly initialized and is ready to
  // perform predictions.  For more info, see doc for LangId
  // constructor above.
  bool is_valid() const;

  // Returns the version of the model used by this LangId object.  On success,
  // the returned version number is a strictly positive integer.  Returns 0 if
  // the model version can not be determined (e.g., for old models that do not
  // specify a version number).
  int GetModelVersion() const;

  // Returns a typed property stored in the model file.
  float GetFloatProperty(const std::string &property,
                         float default_value) const;

 private:
  // Pimpl ("pointer to implementation") pattern, to hide all internals from our
  // clients.
  std::unique_ptr<LangIdImpl> pimpl_;

  SAFTM_DISALLOW_COPY_AND_ASSIGN(LangId);
};

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_LANG_ID_H_
