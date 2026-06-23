cmake_minimum_required(VERSION 3.20)
project(vtuberhub_bridge LANGUAGES CXX)

# Options
# -DGODOT_CPP_DIR=C:/path/to/godot-cpp
# -DWITH_OPENCV=ON/OFF (default ON)
# -DOpenCV_DIR=C:/path/to/opencv/build (or set env OpenCV_DIR)
# -DCMAKE_BUILD_TYPE=Release

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)

option(WITH_OPENCV "Enable OpenCV (required by mediapipe_bridge)" ON)

if(NOT DEFINED GODOT_CPP_DIR)
  message(FATAL_ERROR "Please set GODOT_CPP_DIR to your godot-cpp checkout")
endif()

# godot-cpp
set(GODOT_CPP_INCLUDE_DIR ${GODOT_CPP_DIR}/include)
set(GODOT_CPP_GEN_INCLUDE_DIR ${GODOT_CPP_DIR}/gen/include)

# Try to locate both Release and Debug variants of prebuilt static lib from common godot-cpp output paths.
file(GLOB_RECURSE GODOT_CPP_LIB_RELEASE_GLOB
  ${GODOT_CPP_DIR}/**/libgodot-cpp*.template_release*.lib
)
file(GLOB_RECURSE GODOT_CPP_LIB_DEBUG_GLOB
  ${GODOT_CPP_DIR}/**/libgodot-cpp*.template_debug*.lib
)
# Fallback generic search if specific patterns not found
if(NOT GODOT_CPP_LIB_RELEASE_GLOB AND NOT GODOT_CPP_LIB_DEBUG_GLOB)
  find_library(GODOT_CPP_LIB NAMES godot-cpp
    PATHS
      ${GODOT_CPP_DIR}/bin
      ${GODOT_CPP_DIR}/lib
      ${GODOT_CPP_DIR}/build/bin
      ${GODOT_CPP_DIR}/build/lib
      ${GODOT_CPP_DIR}/build/Release
      ${GODOT_CPP_DIR}/build/Debug
    NO_DEFAULT_PATH)
  if(NOT GODOT_CPP_LIB)
    file(GLOB_RECURSE GODOT_CPP_LIB_GLOB
      ${GODOT_CPP_DIR}/bin/*godot-cpp*.lib
      ${GODOT_CPP_DIR}/lib/*godot-cpp*.lib
      ${GODOT_CPP_DIR}/build/*godot-cpp*.lib)
    list(LENGTH GODOT_CPP_LIB_GLOB _len)
    if(_len GREATER 0)
      list(GET GODOT_CPP_LIB_GLOB 0 GODOT_CPP_LIB)
    endif()
  endif()
else()
  # Prefer the first match in each list
  if(GODOT_CPP_LIB_RELEASE_GLOB)
    list(GET GODOT_CPP_LIB_RELEASE_GLOB 0 GODOT_CPP_LIB_RELEASE)
  endif()
  if(GODOT_CPP_LIB_DEBUG_GLOB)
    list(GET GODOT_CPP_LIB_DEBUG_GLOB 0 GODOT_CPP_LIB_DEBUG)
  endif()
endif()

if(NOT GODOT_CPP_LIB AND NOT GODOT_CPP_LIB_RELEASE AND NOT GODOT_CPP_LIB_DEBUG)
  message(FATAL_ERROR "godot-cpp static lib not found; build godot-cpp first")
endif()

include_directories(
  ${GODOT_CPP_INCLUDE_DIR}
  ${GODOT_CPP_GEN_INCLUDE_DIR}
  ${GODOT_CPP_DIR}/build/gen/include
  ${GODOT_CPP_DIR}/gdextension
)

# OpenCV discovery
if(WITH_OPENCV)
  # 1. Try CONFIG mode first (vcpkg, official cmake config)
  find_package(OpenCV CONFIG QUIET)
  # 2. Try classic module mode with strong hints
  if(NOT OpenCV_FOUND)
    if(DEFINED OpenCV_DIR)
      set(_OpenCV_HINTS ${OpenCV_DIR})
    elseif(DEFINED ENV{OpenCV_DIR})
      set(_OpenCV_HINTS $ENV{OpenCV_DIR})
    elseif(DEFINED OPENCV_DIR)
      set(_OpenCV_HINTS ${OPENCV_DIR})
    endif()
    # Common local paths for this workspace
    list(APPEND _OpenCV_HINTS
      "${CMAKE_CURRENT_LIST_DIR}/../../OpenCV"
      "C:/opencv"
      "C:/tools/opencv"
    )
    find_package(OpenCV QUIET PATHS ${_OpenCV_HINTS})
  endif()
  if(NOT OpenCV_FOUND)
    # Fallback: Look for monolithic opencv_world*.lib in local workspace
    set(_OPENCV_LOCAL "${CMAKE_CURRENT_LIST_DIR}/../../OpenCV")
    if(EXISTS "${_OPENCV_LOCAL}")
      file(GLOB_RECURSE _OPENCV_WORLD_LIB "${_OPENCV_LOCAL}/lib/*/opencv_world*.lib")
      if(_OPENCV_WORLD_LIB)
        # Split into release and debug lists based on trailing 'd'
        set(OpenCV_LIBS_RELEASE "")
        set(OpenCV_LIBS_DEBUG "")
        foreach(_lib IN LISTS _OPENCV_WORLD_LIB)
          if(_lib MATCHES "opencv_world.*[dD]\\.lib$")
            list(APPEND OpenCV_LIBS_DEBUG "${_lib}")
          else()
            list(APPEND OpenCV_LIBS_RELEASE "${_lib}")
          endif()
        endforeach()
        set(OpenCV_FOUND TRUE)
        set(OpenCV_INCLUDE_DIRS "${_OPENCV_LOCAL}/include")
        message(STATUS "Using local OpenCV at ${_OPENCV_LOCAL}")
      endif()
    endif()
  endif()
  if(OpenCV_FOUND)
    add_definitions(-DVTUBERHUB_USE_OPENCV)
    message(STATUS "OpenCV found: ${OpenCV_VERSION}")
  else()
    message(WARNING "WITH_OPENCV=ON but OpenCV not found. mediapipe_bridge parts may fail to link.")
  endif()
endif()

file(GLOB BRIDGE_SRC
  ${CMAKE_CURRENT_LIST_DIR}/*.cpp
  ${CMAKE_CURRENT_LIST_DIR}/*.cxx
)

add_library(vtuberhub_bridge SHARED ${BRIDGE_SRC})

target_include_directories(vtuberhub_bridge PRIVATE ${CMAKE_CURRENT_LIST_DIR})

# Link godot-cpp with proper variant per configuration
if(GODOT_CPP_LIB_RELEASE OR GODOT_CPP_LIB_DEBUG)
  if(GODOT_CPP_LIB_DEBUG)
    message(STATUS "godot-cpp (Debug): ${GODOT_CPP_LIB_DEBUG}")
  endif()
  if(GODOT_CPP_LIB_RELEASE)
    message(STATUS "godot-cpp (Release): ${GODOT_CPP_LIB_RELEASE}")
  endif()
  target_link_libraries(vtuberhub_bridge PRIVATE
    $<$<CONFIG:Debug>:${GODOT_CPP_LIB_DEBUG}>
    $<$<NOT:$<CONFIG:Debug>>:${GODOT_CPP_LIB_RELEASE}>
  )
else()
  target_link_libraries(vtuberhub_bridge PRIVATE ${GODOT_CPP_LIB})
endif()

# Link OpenCV if available; prefer config-specific lists if we constructed them
if(OpenCV_FOUND)
  target_include_directories(vtuberhub_bridge PRIVATE ${OpenCV_INCLUDE_DIRS})
  if(OpenCV_LIBS_RELEASE OR OpenCV_LIBS_DEBUG)
    target_link_libraries(vtuberhub_bridge PRIVATE
      $<$<CONFIG:Debug>:${OpenCV_LIBS_DEBUG}>
      $<$<NOT:$<CONFIG:Debug>>:${OpenCV_LIBS_RELEASE}>
    )
  else()
    target_link_libraries(vtuberhub_bridge PRIVATE ${OpenCV_LIBS})
  endif()
endif()

set_target_properties(vtuberhub_bridge PROPERTIES
  RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
  ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
  LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
  OUTPUT_NAME "vtuberhub_bridge.windows"
)

if(MSVC)
  target_compile_definitions(vtuberhub_bridge PRIVATE _CRT_SECURE_NO_WARNINGS)
  # Align runtime with godot-cpp (which is typically built with /MT for templates)
  # Use /MT for Release and /MTd for Debug to avoid LNK2038 mismatches
  set_property(TARGET vtuberhub_bridge PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif()