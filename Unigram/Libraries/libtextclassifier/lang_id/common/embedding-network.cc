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

#include "lang_id/common/embedding-network.h"

#include "lang_id/common/lite_base/integral-types.h"
#include "lang_id/common/lite_base/logging.h"

namespace libtextclassifier3 {
namespace mobile {
namespace {

void CheckNoQuantization(const EmbeddingNetworkParams::Matrix &matrix) {
  SAFTM_CHECK_EQ(static_cast<int>(QuantizationType::NONE),
                 static_cast<int>(matrix.quant_type))
      << "Quantization not allowed here";
}

int GetMatrixRowSizeInBytes(const EmbeddingNetworkParams::Matrix &matrix) {
  int cols = matrix.cols;
  QuantizationType quant_type = matrix.quant_type;
  switch (quant_type) {
    case QuantizationType::NONE:
      return cols * sizeof(float);
    case QuantizationType::UINT8:
      return cols * sizeof(uint8);
    case QuantizationType::UINT4:
      SAFTM_DCHECK_EQ(cols % 2, 0) << "UINT4 with odd #cols = " << cols;
      return cols / 2;
    case QuantizationType::FLOAT16:
      return cols * sizeof(float16);
    default:
      SAFTM_LOG(FATAL) << "Unknown quant type: "
                       << static_cast<int>(quant_type);
  }
}

// Computes y = weights * Relu(x) + b where Relu is optionally applied.
//
// weights and b are the weight matrix, respectively the bias vector of a neural
// network layer.
//
// Note: in the research literature, usually Relu (the activation function) is
// the last part of a neural layer.  From that perspective, this function
// computes the Relu part of the previous layer (if any) and next the first half
// (the computation of the state) for the current layer.
//
// Note: weights is expected to be the transposed version of the real weight
// matrix.  Hence, instead of computing a linear combination of the columns of
// weights, we compute a linear combination of its rows; but we are mindful that
// these rows are the columns of the original matrix, hence the name
// weights_col_i in the code.
void SparseReluProductPlusBias(bool apply_relu,
                               const EmbeddingNetworkParams::Matrix &weights,
                               const EmbeddingNetworkParams::Matrix &b,
                               const std::vector<float> &x,
                               std::vector<float> *y) {
  // Initialize y to b.  b is a column matrix (i.e., nb.cols == 1); we already
  // CHECK-ed that the EmbeddingNetwork constructor.
  const float *b_start = reinterpret_cast<const float *>(b.elements);
  SAFTM_DCHECK_EQ(b.cols, 1);
  y->assign(b_start, b_start + b.rows);

  float *const y_data = y->data();
  const int y_size = y->size();
  SAFTM_CHECK_EQ(weights.cols, y_size);
  const int x_size = x.size();
  SAFTM_CHECK_EQ(weights.rows, x_size);

  // NOTE: the code below reads x_size * y_size elements from weights; these
  // reads are safe as long as weights.elements contains weights.rows *
  // weights.cols elements (where the element size depends on the quantization
  // type).  That requirement is checked by the params provider, e.g., by
  // EmbeddingNetworkParamsFromFlatbuffer.

  // There is some code duplication between the two main cases of the switch
  // below: the idea was to "lift" the switch outside the loops, to reduce the
  // number of tests at runtime.
  switch (weights.quant_type) {
    case QuantizationType::NONE: {
      // We compute a linear combination of the rows from |weights|, using
      // elements of x (optionally, Relu(x)) as scaling factors (the i-th row
      // gets multiplied by x[i] before being added with the other rows).  Note:
      // elements of |weights| are stored in row-major order: first the elements
      // of row #0, next the elements of row #1, etc.  In the comments below, we
      // write "weights[i][j]" to refer to the j-th element from the i-th row of
      // weights.
      const float *weight_ptr =
          reinterpret_cast<const float *>(weights.elements);
      for (int i = 0; i < x_size; ++i) {
        // Invariant 1: weight_ptr points to the beginning of the i-th row from
        // weights (i.e., weights[i][0]).
        const float scale = x[i];
        if (!apply_relu || (scale > 0)) {
          for (int j = 0; j < y_size; ++j, ++weight_ptr) {
            // Invariant 2: weight_ptr points to weights[i][j].
            y_data[j] += (*weight_ptr) * scale;
          }
        } else {
          // We don't update y_data, but we still have to move weight_ptr to the
          // next row (to satisfy Invariant 1).  We do this by adding y_size ==
          // weights.cols() (see earlier CHECK_EQ).
          weight_ptr += y_size;
        }
      }
      break;
    }
    case QuantizationType::FLOAT16: {
      // See comments for the QuantizationType::NONE case: the code is almost
      // identical, except for float16 (instead of float) and the Float16To32
      // conversion.  We could unify these two cases using a template, but since
      // this is a critical loop, don't want to risk that e.g., inlining of the
      // conversion function doesn't happen.
      const float16 *weight_ptr =
          reinterpret_cast<const float16 *>(weights.elements);
      for (int i = 0; i < x_size; ++i) {
        const float scale = x[i];
        if (!apply_relu || (scale > 0)) {
          for (int j = 0; j < y_size; ++j, ++weight_ptr) {
            y_data[j] += Float16To32(*weight_ptr) * scale;
          }
        } else {
          weight_ptr += y_size;
        }
      }
      break;
    }
    default:
      SAFTM_LOG(FATAL) << "Unsupported weights quantization type: "
                       << static_cast<int>(weights.quant_type);
  }
}
}  // namespace

void EmbeddingNetwork::ConcatEmbeddings(
    const std::vector<FeatureVector> &feature_vectors,
    std::vector<float> *concat) const {
  concat->resize(concat_layer_size_);

  // "es_index" stands for "embedding space index".
  for (int es_index = 0; es_index < feature_vectors.size(); ++es_index) {
    const int concat_offset = concat_offset_[es_index];

    const EmbeddingNetworkParams::Matrix &embedding_matrix =
        embedding_matrices_[es_index];
    const int embedding_dim = embedding_matrix.cols;
    const int embedding_row_size_in_bytes =
        embedding_row_size_in_bytes_[es_index];

    const FeatureVector &feature_vector = feature_vectors[es_index];
    const int num_features = feature_vector.size();
    for (int fi = 0; fi < num_features; ++fi) {
      const FeatureType *feature_type = feature_vector.type(fi);
      int feature_offset = concat_offset + feature_type->base() * embedding_dim;
      SAFTM_CHECK_LE(feature_offset + embedding_dim, concat->size());

      // Weighted embeddings will be added starting from this address.
      float *concat_ptr = concat->data() + feature_offset;

      // Multiplier for each embedding weight.  Includes feature weight (for
      // continuous features) and quantization scale (for quantized embeddings).
      float multiplier;
      int feature_id;
      const FeatureValue feature_value = feature_vector.value(fi);
      if (feature_type->is_continuous()) {
        // Continuous features (encoded as FloatFeatureValue).
        FloatFeatureValue float_feature_value(feature_value);
        feature_id = float_feature_value.id;
        multiplier = float_feature_value.weight;
      } else {
        // Discrete features: every present feature has implicit value 1.0.
        feature_id = feature_value;
        multiplier = 1.0;
      }

      SAFTM_CHECK_GE(feature_id, 0);
      SAFTM_CHECK_LT(feature_id, embedding_matrix.rows);

      // Pointer to float / uint8 weights for relevant embedding.
      const void *embedding_data =
          (reinterpret_cast<const char *>(embedding_matrix.elements) +
           feature_id * embedding_row_size_in_bytes);

      switch (embedding_matrix.quant_type) {
        case QuantizationType::NONE: {
          const float *weights =
              reinterpret_cast<const float *>(embedding_data);
          for (int i = 0; i < embedding_dim; ++i, ++weights, ++concat_ptr) {
            *concat_ptr += *weights * multiplier;
          }
          break;
        }
        case QuantizationType::UINT8: {
          multiplier *= Float16To32(embedding_matrix.quant_scales[feature_id]);
          const uint8 *quant_weights =
              reinterpret_cast<const uint8 *>(embedding_data);
          for (int i = 0; i < embedding_dim;
               ++i, ++quant_weights, ++concat_ptr) {
            // 128 is bias for UINT8 quantization.
            *concat_ptr +=
                (static_cast<int>(*quant_weights) - 128) * multiplier;
          }
          break;
        }
        case QuantizationType::UINT4: {
          multiplier *= Float16To32(embedding_matrix.quant_scales[feature_id]);
          const uint8 *quant_weights =
              reinterpret_cast<const uint8 *>(embedding_data);
          for (int i = 0; i < embedding_dim / 2; ++i, ++quant_weights) {
            const uint8 qq = *quant_weights;
            concat_ptr[0] +=
                (static_cast<int>((qq & 0xF0) | 0x08) - 128) * multiplier;
            concat_ptr[1] +=
                (static_cast<int>(((qq & 0x0F) << 4) | 0x08) - 128) *
                multiplier;
            concat_ptr += 2;
          }
          break;
        }
        default:
          // We already checked (in GetMatrixRowSizeInBytes) that each embedding
          // matrix has a known quantization type.  Hence, DLOG is enough here.
          SAFTM_DLOG(ERROR) << "Unknown embeddings quantization type "
                            << static_cast<int>(embedding_matrix.quant_type);
          break;
      }
    }
  }
}

void EmbeddingNetwork::ComputeFinalScores(
    const std::vector<FeatureVector> &features,
    std::vector<float> *scores) const {
  ComputeFinalScores(features, {}, scores);
}

void EmbeddingNetwork::ComputeFinalScores(
    const std::vector<FeatureVector> &features,
    const std::vector<float> &extra_inputs, std::vector<float> *scores) const {
  // Construct the input layer for our feed-forward neural network (FFNN).
  std::vector<float> input;
  ConcatEmbeddings(features, &input);
  if (!extra_inputs.empty()) {
    input.reserve(input.size() + extra_inputs.size());
    for (int i = 0; i < extra_inputs.size(); i++) {
      input.push_back(extra_inputs[i]);
    }
  }

  // Propagate input through all layers of our FFNN.

  // Alternating storage for activations of the different layers.  We can't use
  // a single vector because all activations of the previous layer are required
  // when computing the activations of the next one.
  std::vector<float> storage[2];
  const std::vector<float> *v_in = &input;
  const int num_layers = layer_weights_.size();
  for (int i = 0; i < num_layers; ++i) {
    std::vector<float> *v_out = nullptr;
    if (i == num_layers - 1) {
      // Final layer: write results directly into |scores|.
      v_out = scores;
    } else {
      // Hidden layer: write results into the alternating storage.  The i % 2
      // trick ensures the alternation.
      v_out = &(storage[i % 2]);
    }
    const bool apply_relu = i > 0;
    SparseReluProductPlusBias(
        apply_relu, layer_weights_[i], layer_bias_[i], *v_in, v_out);
    v_in = v_out;
  }
}

EmbeddingNetwork::EmbeddingNetwork(const EmbeddingNetworkParams *model)
    : model_(model) {
  int offset_sum = 0;
  for (int i = 0; i < model_->embedding_num_features_size(); ++i) {
    concat_offset_.push_back(offset_sum);
    EmbeddingNetworkParams::Matrix matrix = model_->GetEmbeddingMatrix(i);
    offset_sum += matrix.cols * model_->embedding_num_features(i);

    // NOTE: each Matrix is a small struct that doesn't own the actual matrix
    // weights.  Hence, the push_back below is fast.
    embedding_matrices_.push_back(matrix);
    embedding_row_size_in_bytes_.push_back(GetMatrixRowSizeInBytes(matrix));
  }
  concat_layer_size_ = offset_sum;

  SAFTM_CHECK_EQ(model_->hidden_size(), model_->hidden_bias_size());
  for (int i = 0; i < model_->hidden_size(); ++i) {
    layer_weights_.push_back(model_->GetHiddenLayerMatrix(i));

    EmbeddingNetworkParams::Matrix bias = model_->GetHiddenLayerBias(i);
    SAFTM_CHECK_EQ(1, bias.cols);
    CheckNoQuantization(bias);
    layer_bias_.push_back(bias);
  }

  SAFTM_CHECK(model_->HasSoftmax());
  layer_weights_.push_back(model_->GetSoftmaxMatrix());

  EmbeddingNetworkParams::Matrix softmax_bias = model_->GetSoftmaxBias();
  SAFTM_CHECK_EQ(1, softmax_bias.cols);
  CheckNoQuantization(softmax_bias);
  layer_bias_.push_back(softmax_bias);
}

}  // namespace mobile
}  // namespace nlp_saft
