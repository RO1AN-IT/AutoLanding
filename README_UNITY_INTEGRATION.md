# Интеграция AutoLanding с Unity

Этот документ описывает, как интегрировать систему автоматической посадки (AutoLanding) с Unity проектом.

## Структура файлов

- `LandingControlSystemWrapper.h` - Заголовочный файл для C++ DLL обёртки
- `LandingControlSystemWrapper.cpp` - Реализация C++ DLL обёртки
- `AutoLandingController.cs` - C# скрипт для Unity
- `CMakeLists.txt` - Файл для сборки DLL с помощью CMake

## Шаги по интеграции

### 1. Компиляция C++ DLL

#### Windows:
```bash
mkdir build
cd build
cmake ..
cmake --build . --config Release
```

DLL будет создана как `LandingControlSystemWrapper.dll` в папке `build/Release/`

#### macOS:
```bash
mkdir build
cd build
cmake ..
cmake --build . --config Release
```

Библиотека будет создана как `libLandingControlSystemWrapper.dylib` в папке `build/`

#### Linux:
```bash
mkdir build
cd build
cmake ..
cmake --build . --config Release
```

Библиотека будет создана как `libLandingControlSystemWrapper.so` в папке `build/`

### 2. Копирование DLL в Unity проект

**Автоматически**: Скрипт `build.sh` автоматически копирует библиотеку в `Assets/Plugins/` и удаляет её из папки `build/`.

**Вручную**: Скопируйте скомпилированную DLL в папку `Assets/Plugins/` вашего Unity проекта:

- **Windows**: `LandingControlSystemWrapper.dll` → `Assets/Plugins/LandingControlSystemWrapper.dll`
- **macOS**: `libLandingControlSystemWrapper.dylib` → `Assets/Plugins/libLandingControlSystemWrapper.dylib`
- **Linux**: `libLandingControlSystemWrapper.so` → `Assets/Plugins/libLandingControlSystemWrapper.so`

**Важно**: 
- Создайте папку `Plugins`, если её нет.
- **Убедитесь, что библиотека находится ТОЛЬКО в `Assets/Plugins/`**, а не в папке `build/`. Если Unity обнаружит библиотеку в обоих местах, возникнет ошибка о дублировании плагинов.

### 3. Настройка в Unity

1. Откройте Unity проект
2. Найдите скрипт `AutoLandingController.cs` в папке `Assets/Code/AutoLanding/`
3. Добавьте компонент `AutoLandingController` к объекту корабля в сцене

### 4. Настройка компонента AutoLandingController

В инспекторе Unity настройте следующие параметры:

#### Ship Configuration:
- **Ship Transform**: Трансформ основного объекта корабля (если не указан, используется текущий объект)
- **Gear Points**: Массив из 4 трансформов точек шасси (дочерние объекты или отдельные трансформы)

#### Landing Target:
- **Landing Target Transform**: Трансформ целевой точки посадки

#### Ship Parameters:
- **Mass**: Масса корабля в килограммах (по умолчанию: 2200)
- **Thrust Positive**: Максимальная тяга по осям X, Y, Z (по умолчанию: 20000, 20000, 50000)
- **Thrust Negative**: Максимальная обратная тяга по осям X, Y, Z (по умолчанию: 15000, 15000, 0)
- **Angular Rate Limit**: Максимальная угловая скорость в рад/с (по умолчанию: 0.05, 0.05, 0.08)
- **Orientation Limits**: Ограничения ориентации pitch, roll, yaw в радианах (по умолчанию: 0.35, 0.35, 3.14159)
- **Gravity**: Вектор гравитации (по умолчанию: 0, 0, -9.81)
- **Wind Velocity**: Скорость ветра (по умолчанию: 5, 0, 0)

#### Simulation Settings:
- **Simulation Time Step**: Шаг времени симуляции в секундах (по умолчанию: 0.05)
- **Auto Start**: Автоматически начинать посадку при старте (по умолчанию: false)
- **Show Debug Info**: Показывать отладочную информацию на экране (по умолчанию: true)

## Использование

### Программное управление:

```csharp
AutoLandingController controller = GetComponent<AutoLandingController>();

// Начать посадку
controller.StartLanding();

// Остановить посадку
controller.StopLanding();
```

### Автоматический запуск:

Установите флаг `Auto Start` в инспекторе, чтобы посадка начиналась автоматически при старте сцены.

## Важные замечания

1. **Системы координат**: Код автоматически преобразует координаты между Unity (левая система, Y вверх) и C++ (правая система, Z вверх)

2. **Точки шасси**: Убедитесь, что все 4 точки шасси указаны и находятся в правильных позициях относительно центра масс корабля

3. **Rigidbody**: Если у корабля есть Rigidbody компонент, система будет автоматически обновлять его скорость и угловую скорость

4. **Целевая точка**: Целевая точка посадки может быть динамической - система будет автоматически обновлять её позицию каждый кадр

5. **Статус посадки**: Система автоматически определяет статус посадки:
   - `InFlight` - корабль в полёте
   - `Landed` - успешная посадка
   - `Crashed` - аварийная посадка

## Отладка

Включите `Show Debug Info` в инспекторе, чтобы видеть:
- Текущий статус посадки
- Позицию корабля
- Скорость корабля

Все сообщения об ошибках выводятся в консоль Unity.

## Требования

- Unity 2019.4 или новее
- C++ компилятор с поддержкой C++17
- CMake 3.10 или новее (для сборки DLL)

