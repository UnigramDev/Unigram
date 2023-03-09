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

// JNI wrapper for LangId.

#ifndef LIBTEXTCLASSIFIER_LANG_ID_LANG_ID_JNI_H_
#define LIBTEXTCLASSIFIER_LANG_ID_LANG_ID_JNI_H_

#include <jni.h>

#include <string>

#include "utils/java/jni-base.h"

#ifndef TC3_LANG_ID_CLASS_NAME
#define TC3_LANG_ID_CLASS_NAME LangIdModel
#endif

#define TC3_LANG_ID_CLASS_NAME_STR TC3_ADD_QUOTES(TC3_LANG_ID_CLASS_NAME)

#ifdef __cplusplus
extern "C" {
#endif

TC3_JNI_METHOD(jlong, TC3_LANG_ID_CLASS_NAME, nativeNew)
(JNIEnv* env, jobject clazz, jint fd);

TC3_JNI_METHOD(jlong, TC3_LANG_ID_CLASS_NAME, nativeNewFromPath)
(JNIEnv* env, jobject clazz, jstring path);

TC3_JNI_METHOD(jlong, TC3_LANG_ID_CLASS_NAME, nativeNewWithOffset)
(JNIEnv* env, jobject clazz, jint fd, jlong offset, jlong size);

TC3_JNI_METHOD(jobjectArray, TC3_LANG_ID_CLASS_NAME, nativeDetectLanguages)
(JNIEnv* env, jobject thiz, jlong ptr, jstring text);

TC3_JNI_METHOD(void, TC3_LANG_ID_CLASS_NAME, nativeClose)
(JNIEnv* env, jobject thiz, jlong ptr);

TC3_JNI_METHOD(jint, TC3_LANG_ID_CLASS_NAME, nativeGetVersion)
(JNIEnv* env, jobject thiz, jlong ptr);

TC3_JNI_METHOD(jint, TC3_LANG_ID_CLASS_NAME, nativeGetVersionFromFd)
(JNIEnv* env, jobject clazz, jint fd);

TC3_JNI_METHOD(jfloat, TC3_LANG_ID_CLASS_NAME, nativeGetLangIdThreshold)
(JNIEnv* env, jobject thizz, jlong ptr);

TC3_JNI_METHOD(jfloat, TC3_LANG_ID_CLASS_NAME, nativeGetLangIdNoiseThreshold)
(JNIEnv* env, jobject thizz, jlong ptr);

TC3_JNI_METHOD(jint, TC3_LANG_ID_CLASS_NAME, nativeGetMinTextSizeInBytes)
(JNIEnv* env, jobject thizz, jlong ptr);

TC3_JNI_METHOD(jint, TC3_LANG_ID_CLASS_NAME, nativeGetVersionWithOffset)
(JNIEnv* env, jobject clazz, jint fd, jlong offset, jlong size);

#ifdef __cplusplus
}
#endif

#endif  // LIBTEXTCLASSIFIER_LANG_ID_LANG_ID_JNI_H_
