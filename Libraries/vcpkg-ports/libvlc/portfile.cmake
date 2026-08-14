# Prebuilt: libvlc is built from the UnigramDev/vlc fork by the scripts in UnigramDev/deps and
# published as a release archive there. Building it here is not practical -- it needs Docker and
# the VideoLAN contrib toolchain -- and the result changes about once a year.

vcpkg_check_linkage(ONLY_DYNAMIC_LIBRARY)

# VLC's UWP build does not pass /APPCONTAINER when linking libvlc.dll and libvlccore.dll. These
# are the same binaries the app ships today through the NuGet package, so this is the status quo
# rather than a regression -- but it is worth knowing if store certification ever objects.
set(VCPKG_POLICY_SKIP_APPCONTAINER_CHECK enabled)

if(VCPKG_TARGET_ARCHITECTURE STREQUAL "x64")
    set(LIBVLC_SHA512 c451cdbe610bc6fe16c4c7e39f2a702bd288d5f439f6983887bc4a26d21f7f7c2e7d6b983b582f2a634fa3e9f840206cced3618d03b5b6ae4f1bd537f39d0395)
elseif(VCPKG_TARGET_ARCHITECTURE STREQUAL "arm64")
    set(LIBVLC_SHA512 ab0357f90f207068d00d4325de6809a595b1098c57fec9cc74382140af7d10b6faeb2b7489951515e9e3b8a707ee444a9d28c880371d49bb4524bce9c5419aed)
else()
    message(FATAL_ERROR "libvlc: no prebuilt archive for ${VCPKG_TARGET_ARCHITECTURE}")
endif()

vcpkg_download_distfile(ARCHIVE
    URLS "https://github.com/UnigramDev/deps/releases/download/libvlc-${VERSION}-2/libvlc-${VERSION}-${VCPKG_TARGET_ARCHITECTURE}-uwp.zip"
    FILENAME "libvlc-${VERSION}-${VCPKG_TARGET_ARCHITECTURE}-uwp.zip"
    SHA512 "${LIBVLC_SHA512}"
)

vcpkg_extract_source_archive(SOURCE_PATH
    ARCHIVE "${ARCHIVE}"
    NO_REMOVE_ONE_LEVEL
)

file(COPY "${SOURCE_PATH}/include" DESTINATION "${CURRENT_PACKAGES_DIR}")
file(COPY "${SOURCE_PATH}/lib" DESTINATION "${CURRENT_PACKAGES_DIR}")
file(COPY "${SOURCE_PATH}/bin" DESTINATION "${CURRENT_PACKAGES_DIR}")

# The plugins keep their directory structure: their relative paths are recorded in the generated
# plugins.dat cache, so flattening them would invalidate it and cost a scan on every launch.
file(COPY "${SOURCE_PATH}/share/libvlc" DESTINATION "${CURRENT_PACKAGES_DIR}/share")

# Upstream publishes release binaries only. vcpkg still expects a debug tree, and a Debug build of
# the app has to link and load something -- these are C ABI DLLs, so the release build is correct
# to use rather than merely tolerable.
file(COPY "${CURRENT_PACKAGES_DIR}/lib" DESTINATION "${CURRENT_PACKAGES_DIR}/debug")
file(COPY "${CURRENT_PACKAGES_DIR}/bin" DESTINATION "${CURRENT_PACKAGES_DIR}/debug")

vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/copyright")
