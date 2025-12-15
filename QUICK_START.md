# Быстрый старт - Интеграция AutoLanding с Unity

## Шаг 1: Компиляция DLL

### macOS/Linux:
```bash
cd Assets/Code/AutoLanding
./build.sh
```

### Windows:
```cmd
cd Assets\Code\AutoLanding
mkdir build
cd build
cmake ..
cmake --build . --config Release
```

## Шаг 2: Копирование DLL в Unity

**Важно**: Скрипт `build.sh` автоматически копирует библиотеку в `Assets/Plugins/` и удаляет её из папки `build/`. 

Если вы компилируете вручную, создайте папку `Assets/Plugins/` (если её нет) и скопируйте туда скомпилированную библиотеку:

- **macOS**: `libLandingControlSystemWrapper.dylib`
- **Linux**: `libLandingControlSystemWrapper.so`  
- **Windows**: `LandingControlSystemWrapper.dll`

**Примечание**: Убедитесь, что библиотека находится ТОЛЬКО в `Assets/Plugins/`, а не в папке `build/`, иначе Unity выдаст ошибку о дублировании плагинов.

## Шаг 3: Настройка в Unity

1. Откройте Unity проект
2. Найдите объект корабля в сцене (или создайте новый)
3. Добавьте компонент `AutoLandingController` к объекту корабля
4. В инспекторе настройте:
   - **Ship Transform**: Трансформ корабля (если не указан, используется текущий объект)
   - **Gear Points**: Массив из 4 трансформов точек шасси
   - **Landing Target Transform**: Трансформ целевой точки посадки
   - Остальные параметры можно оставить по умолчанию

## Шаг 4: Создание точек шасси

Создайте 4 дочерних объекта для точек шасси:
1. Создайте пустые GameObject'ы как дочерние объекты корабля
2. Расположите их в позициях шасси относительно центра масс корабля
3. Назначьте их в массив `Gear Points` компонента `AutoLandingController`

## Шаг 5: Создание целевой точки посадки

1. Создайте пустой GameObject в сцене
2. Расположите его в точке, куда должен приземлиться корабль
3. Назначьте его в поле `Landing Target Transform` компонента `AutoLandingController`

## Шаг 6: Запуск

- Установите флаг `Auto Start` в инспекторе для автоматического запуска при старте сцены
- Или вызовите `StartLanding()` программно из другого скрипта

## Пример использования в коде:

```csharp
using AutoLanding;
using UnityEngine;

public class LandingManager : MonoBehaviour
{
    public AutoLandingController landingController;
    
    void Start()
    {
        // Начать посадку через 2 секунды
        Invoke(nameof(StartLanding), 2f);
    }
    
    void StartLanding()
    {
        if (landingController != null)
        {
            landingController.StartLanding();
        }
    }
}
```

## Устранение неполадок

- **DLL не найдена**: Убедитесь, что библиотека находится в `Assets/Plugins/`
- **Ошибка при инициализации**: Проверьте, что все трансформы указаны правильно
- **Корабль не движется**: Убедитесь, что `isLanding` установлен в `true` и система инициализирована

