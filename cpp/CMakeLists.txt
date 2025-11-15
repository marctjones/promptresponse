cmake_minimum_required(VERSION 3.14)
project(PromptResponse VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Options
option(BUILD_EXAMPLES "Build examples" ON)
option(BUILD_TESTS "Build tests" OFF)
option(WITH_SIGNATURES "Enable digital signature support (requires OpenSSL)" ON)

# Dependencies
find_package(nlohmann_json 3.11.0 QUIET)
if(NOT nlohmann_json_FOUND)
    message(STATUS "nlohmann_json not found, using FetchContent")
    include(FetchContent)
    FetchContent_Declare(json
        URL https://github.com/nlohmann/json/releases/download/v3.11.3/json.tar.xz)
    FetchContent_MakeAvailable(json)
endif()

if(WITH_SIGNATURES)
    find_package(OpenSSL REQUIRED)
    add_compile_definitions(WITH_SIGNATURES)
endif()

# Library
add_library(promptresponse INTERFACE)
target_include_directories(promptresponse INTERFACE
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>
)
target_link_libraries(promptresponse INTERFACE nlohmann_json::nlohmann_json)

if(WITH_SIGNATURES)
    target_link_libraries(promptresponse INTERFACE OpenSSL::SSL OpenSSL::Crypto)
endif()

# Examples
if(BUILD_EXAMPLES)
    add_subdirectory(examples)
endif()

# Tests
if(BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests)
endif()

# Installation
install(DIRECTORY include/ DESTINATION include)
