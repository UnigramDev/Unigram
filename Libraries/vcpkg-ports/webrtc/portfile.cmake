# Prebuilt: webrtc is built from the UnigramDev/webrtc-uwp fork by the scripts in UnigramDev/deps
# and published as release archives there. Building it from source needs depot_tools, a ~1.5 hour
# sync and ~20 GB of disk, for a result that changes a few times a year.
#
# Headers and libraries are separate archives so that a build downloads only the configuration it
# links: the two libraries are 87 MB and 137 MB compressed, against 18 MB of headers shared by all.

vcpkg_check_linkage(ONLY_STATIC_LIBRARY)

set(WEBRTC_HEADERS_SHA512 177b9ee06adc6c1d938753a2349100e86d8ed679a931790af43511b07b9ee51d9089c121973d4e580537ef7e74acdccf0b56857dbd546c634d0616de0c9ba5dc)

if(VCPKG_TARGET_ARCHITECTURE STREQUAL "x64")
    set(WEBRTC_RELEASE_SHA512 8df214ea44f69e5eb73853f679229ad554c69488be0498a276edf4897e880ed404d20af7966a7a4aa6506ce4cff40c667b560d052e2f29655e27dc7a88901665)
    set(WEBRTC_DEBUG_SHA512 39ac3ca249fbd75875c886722e08dc1969aef9723c5fa8f9ed63dcd68f76b4cec3756f02999cc26ddce4696902373680fae0cb43ce281a2ab7e749f4dc248d50)
elseif(VCPKG_TARGET_ARCHITECTURE STREQUAL "arm64")
    set(WEBRTC_RELEASE_SHA512 5d25307d19e861177a7c5876ee372330448dbc0a01ad08c2b6916b743a8491d0b15f6a7eded330b4e7d2591600516d701fa3b89c2ba4fd843c879bfa51f7275e)
    set(WEBRTC_DEBUG_SHA512 177f9c6aac765b7af68907a91cbdbf61ac779fe3b89480b0d4d065ae96a8ee2d33ee637427d41b47cdf1a18be3ea2fac27f6513e48fe4cce813a81bf884140c3)
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
