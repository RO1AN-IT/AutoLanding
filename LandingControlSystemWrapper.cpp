#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

#include "LandingControlSystemWrapper.h"
#include "LandingControlSystem.h"
#include <memory>

extern "C" {

// Вспомогательные функции для преобразования структур
static ShipParameters ConvertFromD(const ShipParametersD* d) {
    ShipParameters params;
    params.thrust.positive = {d->thrust_positive.x, d->thrust_positive.y, d->thrust_positive.z};
    params.thrust.negative = {d->thrust_negative.x, d->thrust_negative.y, d->thrust_negative.z};
    params.attitude_thrust.positive = {d->attitude_thrust_positive.x, d->attitude_thrust_positive.y, d->attitude_thrust_positive.z};
    params.attitude_thrust.negative = {d->attitude_thrust_negative.x, d->attitude_thrust_negative.y, d->attitude_thrust_negative.z};
    params.angular_rate_limit = {d->angular_rate_limit.x, d->angular_rate_limit.y, d->angular_rate_limit.z};
    params.orientation_limits.max_pitch = d->max_pitch;
    params.orientation_limits.max_roll = d->max_roll;
    params.orientation_limits.max_yaw = d->max_yaw;
    params.environment.gravity = {d->gravity.x, d->gravity.y, d->gravity.z};
    params.environment.wind_velocity = {d->wind_velocity.x, d->wind_velocity.y, d->wind_velocity.z};
    params.mass = d->mass;
    params.default_dt = d->default_dt;
    
    // Копируем координаты шасси
    for (int i = 0; i < 4; ++i) {
        params.gear_points[i].local_position = {d->gear_points[i].x, d->gear_points[i].y, d->gear_points[i].z};
    }
    
    return params;
}

static ShipState ConvertFromD(const ShipStateD* d) {
    ShipState state;
    state.pose.position = {d->position.x, d->position.y, d->position.z};
    state.pose.orientation = {d->orientation.pitch, d->orientation.roll, d->orientation.yaw};
    state.motion.velocity = {d->velocity.x, d->velocity.y, d->velocity.z};
    state.motion.acceleration = {d->acceleration.x, d->acceleration.y, d->acceleration.z};
    state.motion.angular_velocity = {d->angular_velocity.x, d->angular_velocity.y, d->angular_velocity.z};
    state.motion.angular_acceleration = {d->angular_acceleration.x, d->angular_acceleration.y, d->angular_acceleration.z};
    state.dt = d->dt;
    state.step = d->step;
    return state;
}

static LandingTarget ConvertFromD(const LandingTargetD* d) {
    LandingTarget target;
    target.pose.position = {d->position.x, d->position.y, d->position.z};
    target.pose.orientation = {d->orientation.pitch, d->orientation.roll, d->orientation.yaw};
    target.motion.velocity = {d->velocity.x, d->velocity.y, d->velocity.z};
    target.motion.acceleration = {d->acceleration.x, d->acceleration.y, d->acceleration.z};
    target.motion.angular_velocity = {d->angular_velocity.x, d->angular_velocity.y, d->angular_velocity.z};
    target.motion.angular_acceleration = {d->angular_acceleration.x, d->angular_acceleration.y, d->angular_acceleration.z};
    return target;
}

static void ConvertToD(const ShipState& state, ShipStateD* d) {
    d->position.x = state.pose.position.x;
    d->position.y = state.pose.position.y;
    d->position.z = state.pose.position.z;
    d->orientation.pitch = state.pose.orientation.pitch;
    d->orientation.roll = state.pose.orientation.roll;
    d->orientation.yaw = state.pose.orientation.yaw;
    d->velocity.x = state.motion.velocity.x;
    d->velocity.y = state.motion.velocity.y;
    d->velocity.z = state.motion.velocity.z;
    d->acceleration.x = state.motion.acceleration.x;
    d->acceleration.y = state.motion.acceleration.y;
    d->acceleration.z = state.motion.acceleration.z;
    d->angular_velocity.x = state.motion.angular_velocity.x;
    d->angular_velocity.y = state.motion.angular_velocity.y;
    d->angular_velocity.z = state.motion.angular_velocity.z;
    d->angular_acceleration.x = state.motion.angular_acceleration.x;
    d->angular_acceleration.y = state.motion.angular_acceleration.y;
    d->angular_acceleration.z = state.motion.angular_acceleration.z;
    d->dt = state.dt;
    d->step = state.step;
}

LANDING_API LandingControlSystemHandle CreateLandingSystem(
    const ShipParametersD* params,
    const ShipStateD* initial_state,
    const LandingTargetD* target) {
    
    try {
        ShipParameters p = ConvertFromD(params);
        ShipState init = ConvertFromD(initial_state);
        LandingTarget t = ConvertFromD(target);
        
        LandingControlSystem* system = new LandingControlSystem(p, init, t);
        return static_cast<LandingControlSystemHandle>(system);
    } catch (...) {
        return nullptr;
    }
}

LANDING_API void DestroyLandingSystem(LandingControlSystemHandle handle) {
    if (handle) {
        LandingControlSystem* system = static_cast<LandingControlSystem*>(handle);
        delete system;
    }
}

LANDING_API void GetCurrentState(LandingControlSystemHandle handle, ShipStateD* state) {
    if (!handle || !state) return;
    
    try {
        LandingControlSystem* system = static_cast<LandingControlSystem*>(handle);
        ShipState current = system->CurrentState();
        ConvertToD(current, state);
    } catch (...) {
        // В случае ошибки оставляем state без изменений
    }
}

LANDING_API void Step(LandingControlSystemHandle handle, ShipStateD* state) {
    if (!handle || !state) return;
    
    try {
        LandingControlSystem* system = static_cast<LandingControlSystem*>(handle);
        ShipState next = system->Step();
        ConvertToD(next, state);
    } catch (...) {
        // В случае ошибки оставляем state без изменений
    }
}

LANDING_API LandingStatusD GetStatus(LandingControlSystemHandle handle) {
    if (!handle) return InFlight;
    
    try {
        LandingControlSystem* system = static_cast<LandingControlSystem*>(handle);
        LandingStatus status = system->GetStatus();
        
        switch (status) {
            case LandingStatus::Landed:
                return Landed;
            case LandingStatus::Crashed:
                return Crashed;
            default:
                return InFlight;
        }
    } catch (...) {
        return InFlight;
    }
}

LANDING_API void GetGearPointsWorld(LandingControlSystemHandle handle, Vector3D gear_points[4]) {
    if (!handle || !gear_points) return;
    
    try {
        LandingControlSystem* system = static_cast<LandingControlSystem*>(handle);
        ShipState current = system->CurrentState();
        std::array<Vector3, 4> gear = system->GetGearPointsWorld(current);
        
        for (int i = 0; i < 4; ++i) {
            gear_points[i].x = gear[i].x;
            gear_points[i].y = gear[i].y;
            gear_points[i].z = gear[i].z;
        }
    } catch (...) {
        // В случае ошибки оставляем gear_points без изменений
    }
}

LANDING_API void SetLandingTarget(LandingControlSystemHandle handle, const LandingTargetD* target) {
    if (!handle || !target) return;
    
    try {
        LandingControlSystem* system = static_cast<LandingControlSystem*>(handle);
        LandingTarget t = ConvertFromD(target);
        system->SetLandingTarget(t);
    } catch (...) {
        // Игнорируем ошибки
    }
}

LANDING_API void SetParameters(LandingControlSystemHandle handle, const ShipParametersD* params) {
    if (!handle || !params) return;
    
    try {
        LandingControlSystem* system = static_cast<LandingControlSystem*>(handle);
        ShipParameters p = ConvertFromD(params);
        system->SetParameters(p);
    } catch (...) {
        // Игнорируем ошибки
    }
}

LANDING_API void Reset(LandingControlSystemHandle handle) {
    if (!handle) return;
    
    try {
        LandingControlSystem* system = static_cast<LandingControlSystem*>(handle);
        system->Reset();
    } catch (...) {
        // Игнорируем ошибки
    }
}

}

