# Prebuilt: webrtc is built from the UnigramDev/webrtc-uwp fork by the scripts in UnigramDev/deps
# and published as release archives there. Building it from source needs depot_tools, a ~1.5 hour
# sync and ~20 GB of disk, for a result that changes a few times a year.
#
# Headers and libraries are separate archives so that a build downloads only the configuration it
# links: the two libraries are 87 MB and 137 MB compressed, against 18 MB of headers shared by all.

vcpkg_check_linkage(ONLY_STATIC_LIBRARY)

set(WEBRTC_HEADERS_SHA512 d87c5429644a122e365303039ecb03ecff08f7cd3c39e9e8bb21f06030e58cf685c7975dba277387e7d7c1e42cc42f1acae21ec971c45ff2b99a62b5c22281dd)

# -uwp triplets link the winuwp build, -windows ones the desktop build of the same checkout.
if(VCPKG_CMAKE_SYSTEM_NAME STREQUAL "WindowsStore")
    set(WEBRTC_TARGET "uwp")
else()
    set(WEBRTC_TARGET "windows")
endif()

if(WEBRTC_TARGET STREQUAL "uwp" AND VCPKG_TARGET_ARCHITECTURE STREQUAL "x64")
    set(WEBRTC_RELEASE_SHA512 54a7bc415c8f0c11d25fd0b7f787cc115e0b93bd62e82bb4af4f90a13c4d747d4fd2454b29122128f641fd3c96ff06f452ef5dd40d851d846326b015050d49db)
    set(WEBRTC_DEBUG_SHA512 b6f080c1762b505aec90838b3aceb583a831bb5928a9d08e4d3c06a1d811950948c05d9ecaffb8997fbbdf22e08f2990bac9a57df0307549463c157005bc9e3b)
elseif(WEBRTC_TARGET STREQUAL "uwp" AND VCPKG_TARGET_ARCHITECTURE STREQUAL "arm64")
    set(WEBRTC_RELEASE_SHA512 a37488220f2e768350ea5fb96ceb20a333be14155a9f744b1f8dae198aac835c9a38c8eb8d99c9f70ca2ab45ef7b8bc4fddd9df758750f8aa7cfb1548d59c7eb)
    set(WEBRTC_DEBUG_SHA512 f2536654701ba50de6e2036afc0ebc8b6d0cdcbc97aa1c7f9b7c802ce9dbade72dc3662bffbbd89c4b682e310ae2bf733032a94987a9c1808e850b04360f5b37)
elseif(WEBRTC_TARGET STREQUAL "windows" AND VCPKG_TARGET_ARCHITECTURE STREQUAL "x64")
    set(WEBRTC_RELEASE_SHA512 261110b6966be88873af562efeff198e8253fe53c7b2484daef72cf7a1cd386127fc9451092ab75870b8bb5f112caca302b323b35495861e7aa026baf7181672)
    set(WEBRTC_DEBUG_SHA512 b6b1a8be4867da8e3c546cab3e77f739b9878bed6a0c67f923d150bd9b4d8f71833f38df7f33eb31a172e724b72b9b09408a0c47623e09d0d64c6a4fcf50f7ff)
elseif(WEBRTC_TARGET STREQUAL "windows" AND VCPKG_TARGET_ARCHITECTURE STREQUAL "arm64")
    set(WEBRTC_RELEASE_SHA512 ed9b54f7867662a2e6f4550182d54dc1f21b7b6382b6daf5ccc36200cd1edfdb1afb5d46b006195a9bb17bed21e7e14ba942d3066d4e7d8d2e3b95a5890ff18b)
    set(WEBRTC_DEBUG_SHA512 e94cf55010cd504bce316560d27c10969ec704ed031ba242b09dbb7d36bdd5a0d57f092e7da696279270b7d67c851bab33f9fb8f2d9ca2157873fb72077a61da)
else()
    message(FATAL_ERROR "webrtc: no prebuilt archive for ${VCPKG_TARGET_ARCHITECTURE}-${WEBRTC_TARGET}")
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
        URLS "${WEBRTC_BASE_URL}/webrtc-${VERSION}-${VCPKG_TARGET_ARCHITECTURE}-${WEBRTC_TARGET}-release.zip"
        FILENAME "webrtc-${VERSION}-${VCPKG_TARGET_ARCHITECTURE}-${WEBRTC_TARGET}-release.zip"
        SHA512 "${WEBRTC_RELEASE_SHA512}"
    )
    vcpkg_extract_source_archive(RELEASE_PATH ARCHIVE "${RELEASE_ARCHIVE}" NO_REMOVE_ONE_LEVEL)
    file(COPY "${RELEASE_PATH}/lib" DESTINATION "${CURRENT_PACKAGES_DIR}")
endif()

if(NOT VCPKG_BUILD_TYPE STREQUAL "release")
    vcpkg_download_distfile(DEBUG_ARCHIVE
        URLS "${WEBRTC_BASE_URL}/webrtc-${VERSION}-${VCPKG_TARGET_ARCHITECTURE}-${WEBRTC_TARGET}-debug.zip"
        FILENAME "webrtc-${VERSION}-${VCPKG_TARGET_ARCHITECTURE}-${WEBRTC_TARGET}-debug.zip"
        SHA512 "${WEBRTC_DEBUG_SHA512}"
    )
    vcpkg_extract_source_archive(DEBUG_PATH ARCHIVE "${DEBUG_ARCHIVE}" NO_REMOVE_ONE_LEVEL)
    file(COPY "${DEBUG_PATH}/lib" DESTINATION "${CURRENT_PACKAGES_DIR}/debug")
endif()

vcpkg_install_copyright(FILE_LIST "${HEADERS_PATH}/LICENSE")
