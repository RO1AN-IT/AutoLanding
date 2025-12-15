cmake_minimum_required(VERSION 3.10)
project(LandingControlSystemWrapper)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Определение для экспорта функций DLL
if(WIN32)
    add_definitions(-DLANDINGCONTROLSYSTEM_EXPORTS)
    add_definitions(-D_USE_MATH_DEFINES)
endif()

# Определение M_PI, если его нет
if(NOT DEFINED M_PI)
    add_definitions(-DM_PI=3.14159265358979323846)
endif()

# Исходные файлы
set(SOURCES
    LandingControlSystemWrapper.cpp
    LandingControlSystem.h
    ArraySequence.h
    DynamicArray.h
    NewLazySequence.h
    Sequence.h
    SmrtPTRs.h
    PrettyPrint.h
)

# Создание DLL/Shared Library
if(WIN32)
    add_library(LandingControlSystemWrapper SHARED ${SOURCES})
    set_target_properties(LandingControlSystemWrapper PROPERTIES
        OUTPUT_NAME "LandingControlSystemWrapper"
        PREFIX ""
    )
else()
    add_library(LandingControlSystemWrapper SHARED ${SOURCES})
    set_target_properties(LandingControlSystemWrapper PROPERTIES
        OUTPUT_NAME "LandingControlSystemWrapper"
        PREFIX "lib"
    )
endif()

# Для macOS
if(APPLE)
    set_target_properties(LandingControlSystemWrapper PROPERTIES
        MACOSX_RPATH ON
        INSTALL_NAME_DIR "@rpath"
    )
endif()

