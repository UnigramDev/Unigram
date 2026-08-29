// rlottie's build configuration. Upstream generates this from cmake/config.h.in; it is checked in
// instead because the library is built here as a plain static library with no CMake step, and
// because the choices below are ours rather than defaults.
//
// Carried over verbatim from the RLottie.UWP build, where the same four decisions were made:
//
// - LOTTIE_MODULE off, so no dynamic image-loader module. Stickers are pure vector and the module
//   would only add a runtime dependency to resolve.
// - LOTTIE_THREAD off, deliberately: rlottie's own render thread pool would compete with the frame
//   cache service and the animation scheduler, both of which already decide when work happens.
//   LOTTIE_THREAD_SAFE stays on because those callers come from more than one thread.
// - LOTTIE_CACHE on for rlottie's internal model cache, which is a parsed-animation cache and has
//   nothing to do with the frame cache in Telegram.Native/Cache.

#pragma once

//#define LOTTIE_MODULE

#ifdef LOTTIE_MODULE
#define LOTTIE_IMAGE_MODULE_SUPPORT
#endif

//#define LOTTIE_THREAD
#define LOTTIE_THREAD_SAFE

#ifdef LOTTIE_THREAD
#define LOTTIE_THREAD_SUPPORT
#endif

#define LOTTIE_CACHE

#ifdef LOTTIE_CACHE
#define LOTTIE_CACHE_SUPPORT
#endif
