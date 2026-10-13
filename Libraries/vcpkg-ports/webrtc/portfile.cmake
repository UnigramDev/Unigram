# Prebuilt: webrtc is built from the UnigramDev/webrtc-uwp fork by the scripts in UnigramDev/deps
# and published as release archives there. Building it from source needs depot_tools, a ~1.5 hour
# sync and ~20 GB of disk, for a result that changes a few times a year.
#
# Headers and libraries are separate archives so that a build downloads only the configuration it
# links: the two libraries are 87 MB and 137 MB compressed, against 18 MB of headers shared by all.

vcpkg_check_linkage(ONLY_STATIC_LIBRARY)

set(WEBRTC_HEADERS_SHA512 698feeca0deb09a1e17830b08761f32db7df97da5ecf722cc9cac1656d675b84cf419f2f7f2db08b3b914c7eed6ecb5aade75b26b5cb06f9ecbe6cdced90c532)

if(VCPKG_TARGET_ARCHITECTURE STREQUAL "x64")
    set(WEBRTC_RELEASE_SHA512 8365314f92ccf6a3add092d99730a72ccca666db1585ee46241cc1296e356a5053ece88da03faadb36fc87cf65543189e0ffb8a36b638adf3e7ae9573424b810)
    set(WEBRTC_DEBUG_SHA512 a8c265c012ed6b636c9b168143ded7eee425ded26e73fe92022ece37e938c7f4fdd67925aee43f8a8b78917e0895ef192fb938aac47ee5f5a637be3726dfe4fa)
elseif(VCPKG_TARGET_ARCHITECTURE STREQUAL "arm64")
    set(WEBRTC_RELEASE_SHA512 54332885cd78c8e3c75f4a6c41914d51a66a25620d427f3aa8abffa90c7bd37bfe4ce462d08bf70e815ba5018d4e88f2e41f957e4487cc255a05323e67a6f4e5)
    set(WEBRTC_DEBUG_SHA512 488cfc06d056271e6568246945935b4d3f1d347097f31eee925cae0669b6666cec02b4441a4e7540f9658cc37d8ff6675d8398f4066782928012a53185737d11)
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
