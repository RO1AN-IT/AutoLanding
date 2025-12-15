#!/bin/bash

# Скрипт для сборки DLL для Unity проекта

echo "Building LandingControlSystemWrapper..."

# Создаем папку для сборки
mkdir -p build
cd build

# Запускаем CMake
cmake ..

# Компилируем
cmake --build . --config Release

echo "Build complete!"
echo ""

# Копируем библиотеку в Assets/Plugins и удаляем из build
if [[ "$OSTYPE" == "darwin"* ]]; then
    LIB_NAME="libLandingControlSystemWrapper.dylib"
    PLUGINS_DIR="../../../../Plugins"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    LIB_NAME="libLandingControlSystemWrapper.so"
    PLUGINS_DIR="../../../../Plugins"
elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" ]]; then
    LIB_NAME="Release/LandingControlSystemWrapper.dll"
    PLUGINS_DIR="../../../../Plugins"
fi

if [ -f "$LIB_NAME" ]; then
    mkdir -p "$PLUGINS_DIR"
    cp "$LIB_NAME" "$PLUGINS_DIR/"
    rm -f "$LIB_NAME"
    echo "Library copied to $PLUGINS_DIR/ and removed from build/"
else
    echo "Warning: Library file $LIB_NAME not found!"
fi

