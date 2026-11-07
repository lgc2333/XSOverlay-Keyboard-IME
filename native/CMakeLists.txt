cmake_minimum_required(VERSION 3.18)

project(XSOverlayKeyboardIme.Native CXX)

# ADR 0023: one shared library holds IngameIME, its C++ dependencies and the C ABI the
# plugin calls, so the plugin ships two files. The whole library is licensed under the
# GNU Lesser General Public License 2.1, and so is the shim source that goes into it.
# See THIRD-PARTY-NOTICES.md.
#
# The runtime library is pinned to the DLL form. Upstream defaults to the static one,
# which would leave a second CRT inside this library for no gain.
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Both libraries must land in the one directory the plugin's deploy target copies from.
# The build script uses the Ninja generator, which builds a single configuration.
if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Release CACHE STRING "Build configuration")
endif()
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/${CMAKE_BUILD_TYPE}")

# The upstream repositories are submodules, and both are used by path rather than
# through add_subdirectory. Upstream's own CMakeLists pins the static C runtime library,
# so it cannot be overridden from the command line, and it reaches its copy of
# IngameIME-Common through a nested SSH submodule that a plain clone cannot fetch.
set(INGAMEIME_WIN32 "${CMAKE_CURRENT_SOURCE_DIR}/IngameIME_Win32")
set(INGAMEIME_COMMON "${CMAKE_CURRENT_SOURCE_DIR}/IngameIME-Common")

if(NOT EXISTS "${INGAMEIME_WIN32}/src/IngameIMEImpl.cpp" OR NOT EXISTS "${INGAMEIME_COMMON}/include/IngameIME.hpp")
  message(FATAL_ERROR "the IngameIME submodules are missing; run git submodule update --init")
endif()

# The version string defines a static data member of InputContext. It has to exist to
# link, and nothing reads it.
execute_process(
  COMMAND git -C "${INGAMEIME_WIN32}" describe --tags --always --dirty=-dirty
  OUTPUT_VARIABLE INGAMEIME_VERSION
  OUTPUT_STRIP_TRAILING_WHITESPACE
  ERROR_QUIET)
if(NOT INGAMEIME_VERSION)
  set(INGAMEIME_VERSION "unknown")
endif()
message(STATUS "IngameIME version: ${INGAMEIME_VERSION}")

set(VERSION "${INGAMEIME_VERSION}")
configure_file("${INGAMEIME_WIN32}/src/version.cpp.in" "${CMAKE_CURRENT_BINARY_DIR}/version.cpp" @ONLY)

file(GLOB INGAMEIME_SOURCES "${INGAMEIME_WIN32}/src/*.cpp")
list(APPEND INGAMEIME_SOURCES
  "${CMAKE_CURRENT_BINARY_DIR}/version.cpp"
  "${CMAKE_CURRENT_SOURCE_DIR}/shim/shim.cpp"
  "${CMAKE_CURRENT_SOURCE_DIR}/shim/profile-notifications.cpp"
  "${CMAKE_CURRENT_SOURCE_DIR}/shim/keyboard-layout-name.cpp")
list(APPEND INGAMEIME_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/shim/shell-mode-button.cpp")

# One module, so the C++ symbols resolve internally. Only the shim's entry points are
# exported, and it marks them itself.
add_library(IngameIME SHARED ${INGAMEIME_SOURCES})
target_include_directories(IngameIME
  PRIVATE "${INGAMEIME_WIN32}/include"
          "${INGAMEIME_COMMON}/include")
