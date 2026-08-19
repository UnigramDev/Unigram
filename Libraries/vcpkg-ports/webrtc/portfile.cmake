# Prebuilt: webrtc is built from the UnigramDev/webrtc-uwp fork by the scripts in UnigramDev/deps
# and published as release archives there. Building it from source needs depot_tools, a ~1.5 hour
# sync and ~20 GB of disk, for a result that changes a few times a year.
#
# Headers and libraries are separate archives so that a build downloads only the configuration it
# links: the two libraries are 87 MB and 137 MB compressed, against 18 MB of headers shared by all.

vcpkg_check_linkage(ONLY_STATIC_LIBRARY)

set(WEBRTC_HEADERS_SHA512 8612689817f65ce5db3b9f704576a743efd863b1ab8b00c3172c5287ca707a7d5ac5f6692e6bf668cdb02f19020cb54cd34851264f4c526973009c918f652628)

if(VCPKG_TARGET_ARCHITECTURE STREQUAL "x64")
    set(WEBRTC_RELEASE_SHA512 eedfe3b492eaf3d78edb13104aa9fecebc0c2edfed8040ce22ea9e805c4f8100a3771a3a62af4cb6a5ad51da00f937e9c14994ef2c0faeed6bcd26c24a0329cd)
    set(WEBRTC_DEBUG_SHA512 cd3eeb25800916d56346e3caa529271be29842b13f86b4abd280cf1c10380e49f629d3b8182ef04971c0c7ebe22ee6c5b20e128f5be120f77a08f3e725e7e329)
elseif(VCPKG_TARGET_ARCHITECTURE STREQUAL "arm64")
    set(WEBRTC_RELEASE_SHA512 2447865c2f16d83932019861d0ef02de3b76f68f939b3327ea8cdd271433a3ce6391a074736b6bd705441602b98feedcc66f449aa749c21f5ca476465bc9b5b1)
    set(WEBRTC_DEBUG_SHA512 5d2d4f63cbc3cd627122857307dac46199f5ae02b7eae29e3b1f2ba510687374ea6acd7c93359a6b3f17e79a8ebafa7b07308ef650152a6361cb50fe621ddb18)
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
