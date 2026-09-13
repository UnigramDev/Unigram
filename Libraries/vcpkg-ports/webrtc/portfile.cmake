# Prebuilt: webrtc is built from the UnigramDev/webrtc-uwp fork by the scripts in UnigramDev/deps
# and published as release archives there. Building it from source needs depot_tools, a ~1.5 hour
# sync and ~20 GB of disk, for a result that changes a few times a year.
#
# Headers and libraries are separate archives so that a build downloads only the configuration it
# links: the two libraries are 87 MB and 137 MB compressed, against 18 MB of headers shared by all.

vcpkg_check_linkage(ONLY_STATIC_LIBRARY)

set(WEBRTC_HEADERS_SHA512 488e24363ccac62841526adeb5cbf78b18580b18a1ff445a5230206aaf478e22140dfab62938c56cec7b1365aca2a8e515a621dc7b3bc0246942e8d9967e385e)

if(VCPKG_TARGET_ARCHITECTURE STREQUAL "x64")
    set(WEBRTC_RELEASE_SHA512 9c85997fc208097704112a42f790b6ebb26fd5263d4d69215313fc5d6b7f0aee2bc6239fa5ccc46ec5483278776f165a6211d5e945ef72766698c6f213f6e9d0)
    set(WEBRTC_DEBUG_SHA512 1b46477651ea46443e68b47b18486577ba716d6211c0bc12ff487cf17913cb8eaf962d592714d27f2ade501089143a21f0e8fc6a07441ead3f6d6b8a5f5bb6b4)
elseif(VCPKG_TARGET_ARCHITECTURE STREQUAL "arm64")
    set(WEBRTC_RELEASE_SHA512 1cee311f516812a2962eee9d0a8376098c46283731e8dec2314d47d3c68a3c6621a2513a263bfa50dae6426792124e6073973bb24426f2cc4538e2f3e1156d92)
    set(WEBRTC_DEBUG_SHA512 d04c7dca15622c452d279c8228c3ab55cb555a1868693eb6c64b2e17055b8b5c4d1156a324afbb9e264cfa8fc3ecf0eb2caf6a938e6dff226573e12939206f76)
else()
    message(FATAL_ERROR "webrtc: no prebuilt archive for ${VCPKG_TARGET_ARCHITECTURE}")
endif()

set(WEBRTC_BASE_URL "https://github.com/UnigramDev/deps/releases/download/webrtc-${VERSION}-1")

vcpkg_download_distfile(HEADERS_ARCHIVE
    URLS "${WEBRTC_BASE_URL}/webrtc-${VERSION}-headers.zip"
    FILENAME "webrtc-${VERSION}-headers.zip"
    SHA512 "${WEBRTC_HEADERS_SHA512}"
)
vcpkg_extract_source_archive(HEADERS_PATH ARCHIVE "${HEADERS_ARCHIVE}" NO_REMOVE_ONE_LEVEL)
file(COPY "${HEADERS_PATH}/include" DESTINATION "${CURRENT_PACKAGES_DIR}")

# The three include roots the projects used to pass by hand -- the checkout root, abseil-cpp and
# libyuv/include -- are collapsed into this one directory by the packaging script.

if(NOT VCPKG_BUILD_TYPE STREQUAL "debug")
    vcpkg_download_distfile(RELEASE_ARCHIVE
        URLS "${WEBRTC_BASE_URL}/webrtc-${VERSION}-${VCPKG_TARGET_ARCHITECTURE}-uwp-release.zip"
        FILENAME "webrtc-${VERSION}-${VCPKG_TARGET_ARCHITECTURE}-uwp-release.zip"
        SHA512 "${WEBRTC_RELEASE_SHA512}"
    )
    vcpkg_extract_source_archive(RELEASE_PATH ARCHIVE "${RELEASE_ARCHIVE}" NO_REMOVE_ONE_LEVEL)
    file(COPY "${RELEASE_PATH}/lib" DESTINATION "${CURRENT_PACKAGES_DIR}")
endif()

if(NOT VCPKG_BUILD_TYPE STREQUAL "release")
    vcpkg_download_distfile(DEBUG_ARCHIVE
        URLS "${WEBRTC_BASE_URL}/webrtc-${VERSION}-${VCPKG_TARGET_ARCHITECTURE}-uwp-debug.zip"
        FILENAME "webrtc-${VERSION}-${VCPKG_TARGET_ARCHITECTURE}-uwp-debug.zip"
        SHA512 "${WEBRTC_DEBUG_SHA512}"
    )
    vcpkg_extract_source_archive(DEBUG_PATH ARCHIVE "${DEBUG_ARCHIVE}" NO_REMOVE_ONE_LEVEL)
    file(COPY "${DEBUG_PATH}/lib" DESTINATION "${CURRENT_PACKAGES_DIR}/debug")
endif()

vcpkg_install_copyright(FILE_LIST "${HEADERS_PATH}/LICENSE")
