# Streamline Reflex Plugin for Unity
# Builds a native DLL that wraps Streamline SDK for Unity integration

cmake_minimum_required(VERSION 3.20)
project(StreamlineReflexPlugin VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Only build for Windows x64
if(NOT WIN32)
    message(FATAL_ERROR "Streamline Reflex is only supported on Windows")
endif()

if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "Only 64-bit builds are supported")
endif()

# Streamline SDK path - can be passed via -DSTREAMLINE_SDK_PATH=...
if(NOT DEFINED STREAMLINE_SDK_PATH)
    # Default path relative to project root (native/NvidiaReflex -> project root)
    get_filename_component(PROJECT_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../.." ABSOLUTE)
    set(STREAMLINE_SDK_PATH "${PROJECT_ROOT}/external/Streamline")
endif()

set(STREAMLINE_INCLUDE "${STREAMLINE_SDK_PATH}/include")

# Check if Streamline SDK exists
if(NOT EXISTS "${STREAMLINE_INCLUDE}/sl.h")
    message(FATAL_ERROR "Streamline SDK not found at ${STREAMLINE_SDK_PATH}.\nRun the build script: .\\build-reflex-plugin.ps1")
endif()

message(STATUS "Streamline SDK: ${STREAMLINE_SDK_PATH}")

# Streamline library
set(STREAMLINE_LIB_DIR "${STREAMLINE_SDK_PATH}/lib/x64")
set(STREAMLINE_LIB "${STREAMLINE_LIB_DIR}/sl.interposer.lib")

if(NOT EXISTS "${STREAMLINE_LIB}")
    message(FATAL_ERROR "Streamline library not found at ${STREAMLINE_LIB}")
endif()

message(STATUS "Streamline lib: ${STREAMLINE_LIB}")

# Unity Native Plugin Interface (optional, for advanced integration)
# If you have Unity's plugin interface headers, uncomment and set path:
# set(UNITY_PLUGIN_API "${CMAKE_CURRENT_SOURCE_DIR}/UnityPluginAPI")

# Create the DLL
add_library(StreamlineReflexPlugin SHARED
    StreamlineReflexPlugin.cpp
    StreamlineDLSSPlugin.cpp
)

# Include directories
target_include_directories(StreamlineReflexPlugin PRIVATE
    ${STREAMLINE_INCLUDE}
)

# Platform-specific settings
target_compile_definitions(StreamlineReflexPlugin PRIVATE
    WIN32_LEAN_AND_MEAN
    NOMINMAX
    _CRT_SECURE_NO_WARNINGS
)

# Compiler flags
if(MSVC)
    target_compile_options(StreamlineReflexPlugin PRIVATE
        /W4         # Warning level 4
        /WX-        # Don't treat warnings as errors (Streamline headers have some)
        /MP         # Multi-processor compilation
        /Zi         # Debug info
    )
    
    # Release optimizations
    target_compile_options(StreamlineReflexPlugin PRIVATE
        $<$<CONFIG:Release>:/O2 /Oi /GL>
    )
    target_link_options(StreamlineReflexPlugin PRIVATE
        $<$<CONFIG:Release>:/LTCG>
    )
endif()

# Link against Streamline and Windows libraries
target_link_libraries(StreamlineReflexPlugin PRIVATE
    ${STREAMLINE_LIB}
    kernel32
    user32
)

# Output to Unity's plugin folder (native/NvidiaReflex -> Assets/Plugins/x86_64)
get_filename_component(UNITY_PLUGIN_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../Assets/Plugins/x86_64" ABSOLUTE)
set_target_properties(StreamlineReflexPlugin PROPERTIES
    OUTPUT_NAME "StreamlineReflexPlugin"
    RUNTIME_OUTPUT_DIRECTORY "${UNITY_PLUGIN_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${UNITY_PLUGIN_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${UNITY_PLUGIN_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO "${UNITY_PLUGIN_DIR}"
)

# Post-build: Copy Streamline DLLs to plugin folder
# We need sl.interposer.dll and sl.reflex.dll at minimum
# These come from the Streamline SDK release package
add_custom_command(TARGET StreamlineReflexPlugin POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E echo "Plugin built successfully!"
    COMMAND ${CMAKE_COMMAND} -E echo "Remember to copy Streamline DLLs (sl.interposer.dll, sl.reflex.dll, sl.pcl.dll) to the plugin folder"
)

# Install target (optional)
install(TARGETS StreamlineReflexPlugin
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
)
