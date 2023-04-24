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

#include "lang_id/lang-id.h"

#include <stdio.h>

#include <memory>
#include <string>
#include <unordered_map>
#include <vector>

#include "lang_id/common/embedding-feature-interface.h"
#include "lang_id/common/embedding-network-params.h"
#include "lang_id/common/embedding-network.h"
#include "lang_id/common/fel/feature-extractor.h"
#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_strings/numbers.h"
#include "lang_id/common/lite_strings/str-split.h"
#include "lang_id/common/lite_strings/stringpiece.h"
#include "lang_id/common/math/algorithm.h"
#include "lang_id/common/math/softmax.h"
#include "lang_id/custom-tokenizer.h"
#include "lang_id/features/light-sentence-features.h"
// The two features/ headers below are needed only for RegisterClass().
#include "lang_id/features/char-ngram-feature.h"
#include "lang_id/features/relevant-script-feature.h"
#include "lang_id/light-sentence.h"
// The two script/ headers below are needed only for RegisterClass().
#include "lang_id/script/approx-script.h"
#include "lang_id/script/tiny-script-detector.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

namespace {
// Default value for the confidence threshold.  If the confidence of the top
// prediction is below this threshold, then FindLanguage() returns
// LangId::kUnknownLanguageCode.  Note: this is just a default value; if the
// TaskSpec from the model specifies a "reliability_thresh" parameter, then we
// use that value instead.  Note: for legacy reasons, our code and comments use
// the terms "confidence", "probability" and "reliability" equivalently.
static const float kDefaultConfidenceThreshold = 0.50f;
}  // namespace

// Class that performs all work behind LangId.
class LangIdImpl {
 public:
  explicit LangIdImpl(std::unique_ptr<ModelProvider> model_provider)
      : model_provider_(std::move(model_provider)),
        lang_id_brain_interface_("language_identifier") {
    // Note: in the code below, we set valid_ to true only if all initialization
    // steps completed successfully.  Otherwise, we return early, leaving valid_
    // to its default value false.
    if (!model_provider_ || !model_provider_->is_valid()) {
      SAFTM_LOG(ERROR) << "Invalid model provider";
      return;
    }

    auto *nn_params = model_provider_->GetNnParams();
    if (!nn_params) {
      SAFTM_LOG(ERROR) << "No NN params";
      return;
    }
    network_.reset(new EmbeddingNetwork(nn_params));

    languages_ = model_provider_->GetLanguages();
    if (languages_.empty()) {
      SAFTM_LOG(ERROR) << "No known languages";
      return;
    }

    TaskContext context = *model_provider_->GetTaskContext();
    if (!Setup(&context)) {
      SAFTM_LOG(ERROR) << "Unable to Setup() LangId";
      return;
    }
    if (!Init(&context)) {
      SAFTM_LOG(ERROR) << "Unable to Init() LangId";
      return;
    }
    valid_ = true;
  }

  std::string FindLanguage(StringPiece text) const {
    LangIdResult lang_id_result;
    FindLanguages(text, &lang_id_result, /* max_results = */ 1);
    if (lang_id_result.predictions.empty()) {
      return LangId::kUnknownLanguageCode;
    }

    const std::string &language = lang_id_result.predictions[0].first;
    const float probability = lang_id_result.predictions[0].second;
    SAFTM_DLOG(INFO) << "Predicted " << language
                     << " with prob: " << probability << " for \"" << text
                     << "\"";

    // Find confidence threshold for language.
    float threshold = default_threshold_;
    auto it = per_lang_thresholds_.find(language);
    if (it != per_lang_thresholds_.end()) {
      threshold = it->second;
    }
    if (probability < threshold) {
      SAFTM_DLOG(INFO) << "  below threshold => "
                       << LangId::kUnknownLanguageCode;
      return LangId::kUnknownLanguageCode;
    }
    return language;
  }

  void FindLanguages(StringPiece text, LangIdResult *result,
                     int max_results) const {
    if (result == nullptr) return;

    if (max_results <= 0) {
      max_results = languages_.size();
    }
    result->predictions.clear();
    if (!is_valid() || (max_results == 0)) {
      result->predictions.emplace_back(LangId::kUnknownLanguageCode, 1);
      return;
    }

    // Tokenize the input text (this also does some pre-processing, like
    // removing ASCII digits, punctuation, etc).
    LightSentence sentence;
    tokenizer_.Tokenize(text, &sentence);

    // Test input size here, after pre-processing removed irrelevant chars.
    if (IsTooShort(sentence)) {
      result->predictions.emplace_back(LangId::kUnknownLanguageCode, 1);
      return;
    }

    // Extract features from the tokenized text.
    std::vector<FeatureVector> features =
        lang_id_brain_interface_.GetFeaturesNoCaching(&sentence);

    // Run feed-forward neural network to compute scores (softmax logits).
    std::vector<float> scores;
    network_->ComputeFinalScores(features, &scores);

    if (max_results == 1) {
      // Optimization for the case when the user wants only the top result.
      // Computing argmax is faster than the general top-k code.
      int prediction_id = GetArgMax(scores);
      const std::string language = GetLanguageForSoftmaxLabel(prediction_id);
      float probability = ComputeSoftmaxProbability(scores, prediction_id);
      result->predictions.emplace_back(language, probability);
    } else {
      // Compute and sort softmax in descending order by probability and convert
      // IDs to language code strings.  When probabilities are equal, we sort by
      // language code string in ascending order.
      const std::vector<float> softmax = ComputeSoftmax(scores);
      const std::vector<int> indices = GetTopKIndices(max_results, softmax);
      for (const int index : indices) {
        result->predictions.emplace_back(GetLanguageForSoftmaxLabel(index),
                                         softmax[index]);
      }
    }
  }

  bool is_valid() const { return valid_; }

  int GetModelVersion() const { return model_version_; }

  // Returns a property stored in the model file.
  template <typename T, typename R>
  R GetProperty(const std::string &property, T default_value) const {
    return model_provider_->GetTaskContext()->Get(property, default_value);
  }

  // Perform any necessary static initialization.
  // This function is thread-safe.
  // It's also safe to call this function multiple times.
  //
  // We explicitly call RegisterClass() rather than relying on alwayslink=1 in
  // the BUILD file, because the build process for some users of this code
  // doesn't support any equivalent to alwayslink=1 (in particular the
  // Firebase C++ SDK build uses a Kokoro-based CMake build).  While it might
  // be possible to add such support, avoiding the need for an equivalent to
  // alwayslink=1 is preferable because it avoids unnecessarily bloating code
  // size in apps that link against this code but don't use it.
  static void RegisterClasses() {
    static bool initialized = []() -> bool {
      libtextclassifier3::mobile::ApproxScriptDetector::RegisterClass();
      libtextclassifier3::mobile::lang_id::ContinuousBagOfNgramsFunction::RegisterClass();
      libtextclassifier3::mobile::lang_id::TinyScriptDetector::RegisterClass();
      libtextclassifier3::mobile::lang_id::RelevantScriptFeature::RegisterClass();
      return true;
    }();
    (void)initialized;  // Variable used only for initializer's side effects.
  }

 private:
  bool Setup(TaskContext *context) {
    tokenizer_.Setup(context);
    if (!lang_id_brain_interface_.SetupForProcessing(context)) return false;

    min_text_size_in_bytes_ = context->Get("min_text_size_in_bytes", 0);
    default_threshold_ =
        context->Get("reliability_thresh", kDefaultConfidenceThreshold);

    // Parse task parameter "per_lang_reliability_thresholds", fill
    // per_lang_thresholds_.
    const std::string thresholds_str =
        context->Get("per_lang_reliability_thresholds", "");
    std::vector<StringPiece> tokens = LiteStrSplit(thresholds_str, ',');
    for (const auto &token : tokens) {
      if (token.empty()) continue;
      std::vector<StringPiece> parts = LiteStrSplit(token, '=');
      float threshold = 0.0f;
      if ((parts.size() == 2) && LiteAtof(parts[1], &threshold)) {
        per_lang_thresholds_[std::string(parts[0])] = threshold;
      } else {
        SAFTM_LOG(ERROR) << "Broken token: \"" << token << "\"";
      }
    }
    model_version_ = context->Get("model_version", model_version_);
    return true;
  }

  bool Init(TaskContext *context) {
    return lang_id_brain_interface_.InitForProcessing(context);
  }

  // Returns language code for a softmax label.  See comments for languages_
  // field.  If label is out of range, returns LangId::kUnknownLanguageCode.
  std::string GetLanguageForSoftmaxLabel(int label) const {
    if ((label >= 0) && (label < languages_.size())) {
      return languages_[label];
    } else {
      SAFTM_LOG(ERROR) << "Softmax label " << label << " outside range [0, "
                       << languages_.size() << ")";
      return LangId::kUnknownLanguageCode;
    }
  }

  bool IsTooShort(const LightSentence &sentence) const {
    int text_size = 0;
    for (const std::string &token : sentence) {
      // Each token has the form ^...$: we subtract 2 because we want to count
      // only the real text, not the chars added by us.
      text_size += token.size() - 2;
    }
    return text_size < min_text_size_in_bytes_;
  }

  std::unique_ptr<ModelProvider> model_provider_;

  TokenizerForLangId tokenizer_;

  EmbeddingFeatureInterface<LightSentenceExtractor, LightSentence>
      lang_id_brain_interface_;

  // Neural network to use for scoring.
  std::unique_ptr<EmbeddingNetwork> network_;

  // True if this object is ready to perform language predictions.
  bool valid_ = false;

  // The model returns LangId::kUnknownLanguageCode for input text that has
  // fewer than min_text_size_in_bytes_ bytes (excluding ASCII whitespaces,
  // digits, and punctuation).
  int min_text_size_in_bytes_ = 0;

  // Only predictions with a probability (confidence) above this threshold are
  // reported.  Otherwise, we report LangId::kUnknownLanguageCode.
  float default_threshold_ = kDefaultConfidenceThreshold;

  std::unordered_map<std::string, float> per_lang_thresholds_;

  // Recognized languages: softmax label i means languages_[i] (something like
  // "en", "fr", "ru", etc).
  std::vector<std::string> languages_;

  // Version of the model used by this LangIdImpl object.  Zero means that the
  // model version could not be determined.
  int model_version_ = 0;
};

const char LangId::kUnknownLanguageCode[] = "und";

LangId::LangId(std::unique_ptr<ModelProvider> model_provider)
    : pimpl_(new LangIdImpl(std::move(model_provider))) {
  LangIdImpl::RegisterClasses();
}

LangId::~LangId() = default;

std::string LangId::FindLanguage(const char *data, size_t num_bytes) const {
  StringPiece text(data, num_bytes);
  return pimpl_->FindLanguage(text);
}

void LangId::FindLanguages(const char *data, size_t num_bytes,
                           LangIdResult *result, int max_results) const {
  SAFTM_DCHECK(result) << "LangIdResult must not be null.";
  StringPiece text(data, num_bytes);
  pimpl_->FindLanguages(text, result, max_results);
}

bool LangId::is_valid() const { return pimpl_->is_valid(); }

int LangId::GetModelVersion() const { return pimpl_->GetModelVersion(); }

float LangId::GetFloatProperty(const std::string &property,
                               float default_value) const {
  return pimpl_->GetProperty<float, float>(property, default_value);
}

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft
