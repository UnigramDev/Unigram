vcpkg_from_git(
    OUT_SOURCE_PATH SOURCE_PATH
    URL https://chromium.googlesource.com/libyuv/libyuv
    REF d98915a654d3564e4802a0004add46221c4e4348
    PATCHES
        cmake.diff
        clang-cl.diff
)

# libyuv's SIMD is GCC-style inline assembly, so MSVC builds it almost entirely scalar, and on ARM64
# without any NEON at all.
set(clang_cl "$ENV{VCINSTALLDIR}Tools/Llvm/$ENV{VSCMD_ARG_HOST_ARCH}/bin/clang-cl.exe")
if(NOT EXISTS "${clang_cl}")
    message(FATAL_ERROR "libyuv is built with clang-cl, which was not found at ${clang_cl}. "
        "Install the \"C++ Clang Compiler for Windows\" component of Visual Studio.")
endif()

if(VCPKG_TARGET_ARCHITECTURE STREQUAL "arm64")
    set(clang_target aarch64-pc-windows-msvc)
elseif(VCPKG_TARGET_ARCHITECTURE STREQUAL "x64")
    set(clang_target x86_64-pc-windows-msvc)
else()
    message(FATAL_ERROR "No clang-cl target for ${VCPKG_TARGET_ARCHITECTURE}.")
endif()

# CMake's compiler probe links an executable without the standard libraries; with cl, the C++/CX
# forced using below pulled WindowsApp.lib in.
string(APPEND VCPKG_LINKER_FLAGS " WindowsApp.lib")

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        "-DCMAKE_C_COMPILER=${clang_cl}"
        "-DCMAKE_CXX_COMPILER=${clang_cl}"
        -DCMAKE_C_COMPILER_TARGET=${clang_target}
        -DCMAKE_CXX_COMPILER_TARGET=${clang_target}
        # Link with link.exe, like every other DLL in the app, rather than the lld-link CMake prefers.
        -DCMAKE_LINKER_TYPE=MSVC
        # The UWP toolchain otherwise forces /FU platform.winmd for C++/CX, which clang-cl rejects,
        # and try_compile reruns the toolchain with only the variables listed here.
        -DVCPKG_MSVC_CXX_WINRT_EXTENSIONS=OFF
        -DCMAKE_TRY_COMPILE_PLATFORM_VARIABLES=VCPKG_MSVC_CXX_WINRT_EXTENSIONS
        # Only the MJPEG decoder uses it, and nothing calls that.
        -DCMAKE_DISABLE_FIND_PACKAGE_JPEG=ON
        -DBUILD_TOOLS=OFF
)

vcpkg_cmake_install()
vcpkg_cmake_config_fixup()

if(VCPKG_LIBRARY_LINKAGE STREQUAL "dynamic")
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libyuv/basic_types.h" "defined(LIBYUV_USING_SHARED_LIBRARY)" "1")
endif()

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/share")

file(COPY "${CMAKE_CURRENT_LIST_DIR}/libyuv-config.cmake" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}")
file(COPY "${CMAKE_CURRENT_LIST_DIR}/usage" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}")

vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/LICENSE")
