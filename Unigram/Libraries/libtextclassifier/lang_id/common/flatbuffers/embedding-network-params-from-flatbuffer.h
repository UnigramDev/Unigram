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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_FLATBUFFERS_EMBEDDING_NETWORK_PARAMS_FROM_FLATBUFFER_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_FLATBUFFERS_EMBEDDING_NETWORK_PARAMS_FROM_FLATBUFFER_H_

#include <algorithm>
#include <memory>
#include <string>
#include <utility>

#include "lang_id/common/embedding-network-params.h"
#include "lang_id/common/flatbuffers/embedding-network_generated.h"
#include "lang_id/common/lite_base/float16.h"
#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_strings/stringpiece.h"

namespace libtextclassifier3 {
namespace mobile {

// EmbeddingNetworkParams implementation backed by a flatbuffer.
//
// For info on our flatbuffer schema, see embedding-network.fbs.
class EmbeddingNetworkParamsFromFlatbuffer : public EmbeddingNetworkParams {
 public:
  // Constructs an EmbeddingNetworkParamsFromFlatbuffer instance, using the
  // flatbuffer from |bytes|.
  //
  // IMPORTANT #1: caller should make sure |bytes| are alive during the lifetime
  // of this EmbeddingNetworkParamsFromFlatbuffer instance.  To avoid overhead,
  // this constructor does not copy |bytes|.
  //
  // IMPORTANT #2: immediately after this constructor returns, we suggest you
  // call is_valid() on the newly-constructed object and do not call any other
  // method if the answer is negative (false).
  explicit EmbeddingNetworkParamsFromFlatbuffer(StringPiece bytes);

  bool UpdateTaskContextParameters(mobile::TaskContext *task_context) override {
    // This class does not provide access to the overall TaskContext.  It
    // provides only parameters for the Neurosis neural network.
    SAFTM_LOG(DFATAL) << "Not supported";
    return false;
  }

  bool is_valid() const override { return valid_; }

  int embeddings_size() const override { return SafeGetNumInputChunks(); }

  int embeddings_num_rows(int i) const override {
    const saft_fbs::Matrix *matrix = SafeGetEmbeddingMatrix(i);
    return SafeGetNumRows(matrix);
  }

  int embeddings_num_cols(int i) const override {
    const saft_fbs::Matrix *matrix = SafeGetEmbeddingMatrix(i);
    return SafeGetNumCols(matrix);
  }

  const void *embeddings_weights(int i) const override {
    const saft_fbs::Matrix *matrix = SafeGetEmbeddingMatrix(i);
    return SafeGetValuesOfMatrix(matrix);
  }

  QuantizationType embeddings_quant_type(int i) const override {
    const saft_fbs::Matrix *matrix = SafeGetEmbeddingMatrix(i);
    return SafeGetQuantizationType(matrix);
  }

  const float16 *embeddings_quant_scales(int i) const override {
    const saft_fbs::Matrix *matrix = SafeGetEmbeddingMatrix(i);
    return SafeGetScales(matrix);
  }

  int hidden_size() const override {
    // -1 because last layer is always the softmax layer.
    return (std::max)(SafeGetNumLayers() - 1, 0);
  }

  int hidden_num_rows(int i) const override {
    const saft_fbs::Matrix *weights = SafeGetLayerWeights(i);
    return SafeGetNumRows(weights);
  }

  int hidden_num_cols(int i) const override {
    const saft_fbs::Matrix *weights = SafeGetLayerWeights(i);
    return SafeGetNumCols(weights);
  }

  QuantizationType hidden_weights_quant_type(int i) const override {
    const saft_fbs::Matrix *weights = SafeGetLayerWeights(i);
    return SafeGetQuantizationType(weights);
  }

  const void *hidden_weights(int i) const override {
    const saft_fbs::Matrix *weights = SafeGetLayerWeights(i);
    return SafeGetValuesOfMatrix(weights);
  }

  int hidden_bias_size() const override { return hidden_size(); }

  int hidden_bias_num_rows(int i) const override {
    const saft_fbs::Matrix *bias = SafeGetLayerBias(i);
    return SafeGetNumRows(bias);
  }

  int hidden_bias_num_cols(int i) const override {
    const saft_fbs::Matrix *bias = SafeGetLayerBias(i);
    return SafeGetNumCols(bias);
  }

  const void *hidden_bias_weights(int i) const override {
    const saft_fbs::Matrix *bias = SafeGetLayerBias(i);
    return SafeGetValues(bias);
  }

  int softmax_size() const override { return (SafeGetNumLayers() > 0) ? 1 : 0; }

  int softmax_num_rows(int i) const override {
    const saft_fbs::Matrix *weights = SafeGetSoftmaxWeights();
    return SafeGetNumRows(weights);
  }

  int softmax_num_cols(int i) const override {
    const saft_fbs::Matrix *weights = SafeGetSoftmaxWeights();
    return SafeGetNumCols(weights);
  }

  QuantizationType softmax_weights_quant_type(int i) const override {
    const saft_fbs::Matrix *weights = SafeGetSoftmaxWeights();
    return SafeGetQuantizationType(weights);
  }

  const void *softmax_weights(int i) const override {
    const saft_fbs::Matrix *weights = SafeGetSoftmaxWeights();
    return SafeGetValuesOfMatrix(weights);
  }

  int softmax_bias_size() const override { return softmax_size(); }

  int softmax_bias_num_rows(int i) const override {
    const saft_fbs::Matrix *bias = SafeGetSoftmaxBias();
    return SafeGetNumRows(bias);
  }

  int softmax_bias_num_cols(int i) const override {
    const saft_fbs::Matrix *bias = SafeGetSoftmaxBias();
    return SafeGetNumCols(bias);
  }

  const void *softmax_bias_weights(int i) const override {
    const saft_fbs::Matrix *bias = SafeGetSoftmaxBias();
    return SafeGetValues(bias);
  }

  int embedding_num_features_size() const override {
    return SafeGetNumInputChunks();
  }

  int embedding_num_features(int i) const override {
    if (!InRangeIndex(i, embedding_num_features_size(),
                      "embedding num features")) {
      return 0;
    }
    const saft_fbs::InputChunk *input_chunk = SafeGetInputChunk(i);
    if (input_chunk == nullptr) {
      return 0;
    }
    return input_chunk->num_features();
  }

  bool has_is_precomputed() const override { return false; }
  bool is_precomputed() const override { return false; }

 private:
  // Returns true if and only if index is in [0, limit).  info should be a
  // pointer to a zero-terminated array of chars (ideally a literal string,
  // e.g. "layer") indicating what the index refers to; info is used to make log
  // messages more informative.
  static bool InRangeIndex(int index, int limit, const char *info);

  // Returns network_->input_chunks()->size(), if all dereferences are safe
  // (i.e., no nullptr); otherwise, returns 0.
  int SafeGetNumInputChunks() const;

  // Returns network_->input_chunks()->Get(i), if all dereferences are safe
  // (i.e., no nullptr) otherwise, returns nullptr.
  const saft_fbs::InputChunk *SafeGetInputChunk(int i) const;

  // Returns network_->input_chunks()->Get(i)->embedding(), if all dereferences
  // are safe (i.e., no nullptr); otherwise, returns nullptr.
  const saft_fbs::Matrix *SafeGetEmbeddingMatrix(int i) const;

  // Returns network_->layers()->size(), if all dereferences are safe (i.e., no
  // nullptr); otherwise, returns 0.
  int SafeGetNumLayers() const;

  // Returns network_->layers()->Get(i), if all dereferences are safe
  // (i.e., no nullptr); otherwise, returns nullptr.
  const saft_fbs::NeuralLayer *SafeGetLayer(int i) const;

  // Returns network_->layers()->Get(i)->weights(), if all dereferences are safe
  // (i.e., no nullptr); otherwise, returns nullptr.
  const saft_fbs::Matrix *SafeGetLayerWeights(int i) const;

  // Returns network_->layers()->Get(i)->bias(), if all dereferences are safe
  // (i.e., no nullptr); otherwise, returns nullptr.
  const saft_fbs::Matrix *SafeGetLayerBias(int i) const;

  static int SafeGetNumRows(const saft_fbs::Matrix *matrix) {
    return (matrix == nullptr) ? 0 : matrix->rows();
  }

  static int SafeGetNumCols(const saft_fbs::Matrix *matrix) {
    return (matrix == nullptr) ? 0 : matrix->cols();
  }

  // Returns matrix->values()->data() if all dereferences are safe (i.e., no
  // nullptr); otherwise, returns nullptr.
  static const float *SafeGetValues(const saft_fbs::Matrix *matrix);

  // Returns matrix->quantized_values()->data() if all dereferences are safe
  // (i.e., no nullptr); otherwise, returns nullptr.
  static const uint8_t *SafeGetQuantizedValues(const saft_fbs::Matrix *matrix);

  // Returns matrix->scales()->data() if all dereferences are safe (i.e., no
  // nullptr); otherwise, returns nullptr.
  static const float16 *SafeGetScales(const saft_fbs::Matrix *matrix);

  // Returns network_->layers()->Get(last_index) with last_index =
  // SafeGetNumLayers() - 1, if all dereferences are safe (i.e., no nullptr) and
  // there exists at least one layer; otherwise, returns nullptr.
  const saft_fbs::NeuralLayer *SafeGetSoftmaxLayer() const;

  const saft_fbs::Matrix *SafeGetSoftmaxWeights() const {
    const saft_fbs::NeuralLayer *layer = SafeGetSoftmaxLayer();
    return (layer == nullptr) ? nullptr : layer->weights();
  }

  const saft_fbs::Matrix *SafeGetSoftmaxBias() const {
    const saft_fbs::NeuralLayer *layer = SafeGetSoftmaxLayer();
    return (layer == nullptr) ? nullptr : layer->bias();
  }

  // Returns the quantization type for |matrix|.  Returns NONE in case of
  // problems (e.g., matrix is nullptr or unknown quantization type).
  QuantizationType SafeGetQuantizationType(
      const saft_fbs::Matrix *matrix) const;

  // Returns a pointer to the values (float, uint8, or float16, depending on
  // quantization) from |matrix|, in row-major order.  Returns nullptr in case
  // of a problem.
  const void *SafeGetValuesOfMatrix(const saft_fbs::Matrix *matrix) const;

  // Performs some validity checks.  E.g., check that dimensions of the network
  // layers match.  Also checks that all pointers we return are inside the
  // |bytes| passed to the constructor, such that client that reads from those
  // pointers will not run into troubles.
  bool ValidityChecking(StringPiece bytes) const;

  // True if these params are valid.  May be false if the original proto was
  // corrupted.  We prefer to set this to false to CHECK-failing.
  bool valid_ = false;

  // EmbeddingNetwork flatbuffer from the bytes passed as parameter to the
  // constructor; see constructor doc.
  const saft_fbs::EmbeddingNetwork *network_ = nullptr;
};

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_FLATBUFFERS_EMBEDDING_NETWORK_PARAMS_FROM_FLATBUFFER_H_
