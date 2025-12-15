#pragma once

#include <cstddef>

#ifdef _WIN32
    #ifdef LANDINGCONTROLSYSTEM_EXPORTS
        #define LANDING_API __declspec(dllexport)
    #else
        #define LANDING_API __declspec(dllimport)
    #endif
#else
    #define LANDING_API __attribute__((visibility("default")))
#endif

extern "C" {

// Структуры для передачи данных между C++ и C#
struct Vector3D {
    double x;
    double y;
    double z;
};

struct OrientationD {
    double pitch;
    double roll;
    double yaw;
};

struct ShipStateD {
    Vector3D position;
    OrientationD orientation;
    Vector3D velocity;
    Vector3D acceleration;
    Vector3D angular_velocity;
    Vector3D angular_acceleration;
    double dt;
    size_t step;
};

struct ShipParametersD {
    Vector3D thrust_positive;
    Vector3D thrust_negative;
    Vector3D attitude_thrust_positive;
    Vector3D attitude_thrust_negative;
    Vector3D angular_rate_limit;
    double max_pitch;
    double max_roll;
    double max_yaw;
    Vector3D gravity;
    Vector3D wind_velocity;
    double mass;
    double default_dt;
    Vector3D gear_points[4]; // Локальные координаты шасси в корпусной системе
};

struct LandingTargetD {
    Vector3D position;
    OrientationD orientation;
    Vector3D velocity;
    Vector3D acceleration;
    Vector3D angular_velocity;
    Vector3D angular_acceleration;
};

enum LandingStatusD {
    InFlight = 0,
    Landed = 1,
    Crashed = 2
};

// Указатель на объект LandingControlSystem (opaque pointer)
typedef void* LandingControlSystemHandle;

// Создание системы управления посадкой
LANDING_API LandingControlSystemHandle CreateLandingSystem(
    const ShipParametersD* params,
    const ShipStateD* initial_state,
    const LandingTargetD* target
);

// Удаление системы управления посадкой
LANDING_API void DestroyLandingSystem(LandingControlSystemHandle handle);

// Получение текущего состояния
LANDING_API void GetCurrentState(LandingControlSystemHandle handle, ShipStateD* state);

// Выполнение одного шага симуляции
LANDING_API void Step(LandingControlSystemHandle handle, ShipStateD* state);

// Получение статуса посадки
LANDING_API LandingStatusD GetStatus(LandingControlSystemHandle handle);

// Получение координат точек шасси в мировой системе координат
LANDING_API void GetGearPointsWorld(LandingControlSystemHandle handle, Vector3D gear_points[4]);

// Обновление целевой точки посадки
LANDING_API void SetLandingTarget(LandingControlSystemHandle handle, const LandingTargetD* target);

// Обновление параметров корабля
LANDING_API void SetParameters(LandingControlSystemHandle handle, const ShipParametersD* params);

// Сброс системы к начальному состоянию
LANDING_API void Reset(LandingControlSystemHandle handle);

}

