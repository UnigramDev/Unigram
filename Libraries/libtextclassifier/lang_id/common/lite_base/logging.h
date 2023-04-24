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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_LOGGING_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_LOGGING_H_

#ifdef SAFTM_COMPACT_LOGGING

// One gets the compact logging only one requests it explicitly, by passing
// --define saftm_compact_logging=true on the blaze command-line.
#include "lang_id/common/lite_base/compact-logging.h"

#else

// Otherwise, one gets the standard base/logging.h You should do so, unless you
// have a really good reason to switch to the compact logging.
#include "base/logging.h"

#define SAFTM_LOG LOG
#define SAFTM_CHECK CHECK
#define SAFTM_CHECK_EQ CHECK_EQ
#define SAFTM_CHECK_LT CHECK_LT
#define SAFTM_CHECK_LE CHECK_LE
#define SAFTM_CHECK_GT CHECK_GT
#define SAFTM_CHECK_GE CHECK_GE
#define SAFTM_CHECK_NE CHECK_NE

#define SAFTM_DLOG DLOG
#define SAFTM_DCHECK DCHECK
#define SAFTM_DCHECK_EQ DCHECK_EQ
#define SAFTM_DCHECK_LT DCHECK_LT
#define SAFTM_DCHECK_LE DCHECK_LE
#define SAFTM_DCHECK_GT DCHECK_GT
#define SAFTM_DCHECK_GE DCHECK_GE
#define SAFTM_DCHECK_NE DCHECK_NE

#endif  // SAFTM_COMPACT_LOGGING

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_LOGGING_H_
