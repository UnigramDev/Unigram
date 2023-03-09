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

#include "lang_id/lang-id_jni.h"

#include <jni.h>

#include <type_traits>
#include <vector>

#include "lang_id/lang-id-wrapper.h"
#include "utils/base/logging.h"
#include "utils/java/jni-helper.h"
#include "lang_id/fb_model/lang-id-from-fb.h"
#include "lang_id/lang-id.h"

using libtextclassifier3::JniHelper;
using libtextclassifier3::JStringToUtf8String;
using libtextclassifier3::ScopedLocalRef;
using libtextclassifier3::StatusOr;
using libtextclassifier3::mobile::lang_id::GetLangIdFromFlatbufferFile;
using libtextclassifier3::mobile::lang_id::GetLangIdFromFlatbufferFileDescriptor;
using libtextclassifier3::mobile::lang_id::LangId;
using libtextclassifier3::mobile::lang_id::LangIdResult;

namespace {

StatusOr<ScopedLocalRef<jobjectArray>> LangIdResultToJObjectArray(
    JNIEnv* env,
    const std::vector<std::pair<std::string, float>>& lang_id_predictions) {
  TC3_ASSIGN_OR_RETURN(
      const ScopedLocalRef<jclass> result_class,
      JniHelper::FindClass(
          env, TC3_PACKAGE_PATH TC3_LANG_ID_CLASS_NAME_STR "$LanguageResult"));

  TC3_ASSIGN_OR_RETURN(const jmethodID result_class_constructor,
                       JniHelper::GetMethodID(env, result_class.get(), "<init>",
                                              "(Ljava/lang/String;F)V"));
  TC3_ASSIGN_OR_RETURN(
      ScopedLocalRef<jobjectArray> results,
      JniHelper::NewObjectArray(env, lang_id_predictions.size(),
                                result_class.get(), nullptr));
  for (int i = 0; i < lang_id_predictions.size(); i++) {
    TC3_ASSIGN_OR_RETURN(
        const ScopedLocalRef<jstring> predicted_language,
        JniHelper::NewStringUTF(env, lang_id_predictions[i].first.c_str()));
    TC3_ASSIGN_OR_RETURN(
        const ScopedLocalRef<jobject> result,
        JniHelper::NewObject(
            env, result_class.get(), result_class_constructor,
            predicted_language.get(),
            static_cast<jfloat>(lang_id_predictions[i].second)));
    JniHelper::SetObjectArrayElement(env, results.get(), i, result.get());
  }
  return results;
}

float GetNoiseThreshold(const LangId& model) {
  return model.GetFloatProperty("text_classifier_langid_noise_threshold", -1.0);
}
}  // namespace

TC3_JNI_METHOD(jlong, TC3_LANG_ID_CLASS_NAME, nativeNew)
(JNIEnv* env, jobject clazz, jint fd) {
  std::unique_ptr<LangId> lang_id = GetLangIdFromFlatbufferFileDescriptor(fd);
  if (!lang_id->is_valid()) {
    return reinterpret_cast<jlong>(nullptr);
  }
  return reinterpret_cast<jlong>(lang_id.release());
}

TC3_JNI_METHOD(jlong, TC3_LANG_ID_CLASS_NAME, nativeNewFromPath)
(JNIEnv* env, jobject clazz, jstring path) {
  TC3_ASSIGN_OR_RETURN_0(const std::string path_str,
                         JStringToUtf8String(env, path));
  std::unique_ptr<LangId> lang_id = GetLangIdFromFlatbufferFile(path_str);
  if (!lang_id->is_valid()) {
    return reinterpret_cast<jlong>(nullptr);
  }
  return reinterpret_cast<jlong>(lang_id.release());
}

TC3_JNI_METHOD(jlong, TC3_LANG_ID_CLASS_NAME, nativeNewWithOffset)
(JNIEnv* env, jobject clazz, jint fd, jlong offset, jlong size) {
  std::unique_ptr<LangId> lang_id =
      GetLangIdFromFlatbufferFileDescriptor(fd, offset, size);
  if (!lang_id->is_valid()) {
    return reinterpret_cast<jlong>(nullptr);
  }
  return reinterpret_cast<jlong>(lang_id.release());
}

TC3_JNI_METHOD(jobjectArray, TC3_LANG_ID_CLASS_NAME, nativeDetectLanguages)
(JNIEnv* env, jobject thiz, jlong ptr, jstring text) {
  LangId* model = reinterpret_cast<LangId*>(ptr);
  if (!model) {
    return nullptr;
  }

  TC3_ASSIGN_OR_RETURN_NULL(const std::string text_str,
                            JStringToUtf8String(env, text));

  const std::vector<std::pair<std::string, float>>& prediction_results =
      libtextclassifier3::langid::GetPredictions(model, text_str);

  TC3_ASSIGN_OR_RETURN_NULL(
      ScopedLocalRef<jobjectArray> results,
      LangIdResultToJObjectArray(env, prediction_results));
  return results.release();
}

TC3_JNI_METHOD(void, TC3_LANG_ID_CLASS_NAME, nativeClose)
(JNIEnv* env, jobject thiz, jlong ptr) {
  if (!ptr) {
    TC3_LOG(ERROR) << "Trying to close null LangId.";
    return;
  }
  LangId* model = reinterpret_cast<LangId*>(ptr);
  delete model;
}

TC3_JNI_METHOD(jint, TC3_LANG_ID_CLASS_NAME, nativeGetVersion)
(JNIEnv* env, jobject thiz, jlong ptr) {
  if (!ptr) {
    return -1;
  }
  LangId* model = reinterpret_cast<LangId*>(ptr);
  return model->GetModelVersion();
}

TC3_JNI_METHOD(jint, TC3_LANG_ID_CLASS_NAME, nativeGetVersionFromFd)
(JNIEnv* env, jobject clazz, jint fd) {
  std::unique_ptr<LangId> lang_id = GetLangIdFromFlatbufferFileDescriptor(fd);
  if (!lang_id->is_valid()) {
    return -1;
  }
  return lang_id->GetModelVersion();
}

TC3_JNI_METHOD(jfloat, TC3_LANG_ID_CLASS_NAME, nativeGetLangIdThreshold)
(JNIEnv* env, jobject thizz, jlong ptr) {
  if (!ptr) {
    return -1.0;
  }
  LangId* model = reinterpret_cast<LangId*>(ptr);
  return model->GetFloatProperty("text_classifier_langid_threshold", -1.0);
}

TC3_JNI_METHOD(jfloat, TC3_LANG_ID_CLASS_NAME, nativeGetLangIdNoiseThreshold)
(JNIEnv* env, jobject thizz, jlong ptr) {
  if (!ptr) {
    return -1.0;
  }
  LangId* model = reinterpret_cast<LangId*>(ptr);
  return GetNoiseThreshold(*model);
}

TC3_JNI_METHOD(jint, TC3_LANG_ID_CLASS_NAME, nativeGetMinTextSizeInBytes)
(JNIEnv* env, jobject thizz, jlong ptr) {
  if (!ptr) {
    return 0;
  }
  LangId* model = reinterpret_cast<LangId*>(ptr);
  return model->GetFloatProperty("min_text_size_in_bytes", 0);
}

TC3_JNI_METHOD(jint, TC3_LANG_ID_CLASS_NAME, nativeGetVersionWithOffset)
(JNIEnv* env, jobject clazz, jint fd, jlong offset, jlong size) {
  std::unique_ptr<LangId> lang_id =
      GetLangIdFromFlatbufferFileDescriptor(fd, offset, size);
  if (!lang_id->is_valid()) {
    return -1;
  }
  return lang_id->GetModelVersion();
}
