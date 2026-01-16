#pragma once
#include <iostream>
#include <vector>
#include <string>
#include <cmath>
#include <algorithm>
#include <fstream>
#include <iomanip>

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

struct Vector3 {
    double x, y, z;
    Vector3 operator+(const Vector3& other) const { return {x + other.x, y + other.y, z + other.z}; }
    Vector3 operator-(const Vector3& other) const { return {x - other.x, y - other.y, z - other.z}; }
    Vector3 operator*(double scalar) const { return {x * scalar, y * scalar, z * scalar}; }
    Vector3 operator/(double scalar) const { return {x / scalar, y / scalar, z / scalar}; }
    Vector3 operator-() const { return {-x, -y, -z}; }

    double Length() const { return std::sqrt(x*x + y*y + z*z); }
    Vector3 Normalized() const { 
        double len = Length(); 
        return len > 1e-6 ? (*this / len) : Vector3{0,0,0}; 
    }
};

struct Orientation {
    double pitch, roll, yaw;
};

struct Pose {
    Vector3 position;
    Orientation orientation;
};

struct MotionState {
    Vector3 velocity;
    Vector3 acceleration;
    Vector3 angular_velocity;
    Vector3 angular_acceleration;
};

struct ShipState {
    Pose pose;
    MotionState motion;
    double fuel;
};

struct ThrustProfile {
    Vector3 positive;
    Vector3 negative;
};

struct ShipParameters {
    double mass;
    ThrustProfile thrust;
    ThrustProfile attitude_thrust;
    struct {
        Vector3 gravity;
        Vector3 wind_velocity;
    } environment;
    Orientation orientation_limits;
    Vector3 angular_rate_limit;
    double fuel_consumption_rate;
    std::vector<Vector3> gear_points_body; // Точки крепления шасси в локальной системе координат
};

struct ObstacleSensors {
    bool front{false}, back{false}, left{false}, right{false}, top{false}, bottom{false};
};

struct LandingTarget {
    Pose pose;
    double initial_dist{0.0};
};

struct ControlInput {
    Vector3 thrust_ratio;
    Vector3 angular_thrust_ratio;
};

enum class LandingStatus { InFlight, Landed };

inline Vector3 BodyToWorld(const Vector3& body_vec, const Orientation& ori) {
    double cp = std::cos(ori.pitch), sp = std::sin(ori.pitch);
    double cr = std::cos(ori.roll), sr = std::sin(ori.roll);
    double cy = std::cos(ori.yaw), sy = std::sin(ori.yaw);
    return {
        body_vec.x*(cy*cp) + body_vec.y*(cy*sp*sr - sy*cr) + body_vec.z*(cy*sp*cr + sy*sr),
        body_vec.x*(sy*cp) + body_vec.y*(sy*sp*sr + cy*cr) + body_vec.z*(sy*sp*cr - cy*sr),
        body_vec.x*(-sp) + body_vec.y*(cp*sr) + body_vec.z*(cp*cr)
    };
}

inline Vector3 WorldToBody(const Vector3& world_vec, const Orientation& ori) {
    double cp = std::cos(ori.pitch), sp = std::sin(ori.pitch);
    double cr = std::cos(ori.roll), sr = std::sin(ori.roll);
    double cy = std::cos(ori.yaw), sy = std::sin(ori.yaw);
    return {
        world_vec.x*(cy*cp) + world_vec.y*(sy*cp) + world_vec.z*(-sp),
        world_vec.x*(cy*sp*sr - sy*cr) + world_vec.y*(sy*sp*sr + cy*cr) + world_vec.z*(cp*sr),
        world_vec.x*(cy*sp*cr + sy*sr) + world_vec.y*(sy*sp*cr - cy*sr) + world_vec.z*(cp*cr)
    };
}

class LandingControlSystem {
public:
    LandingControlSystem(const ShipParameters& p, const ShipState& init, const LandingTarget& t) 
        : params(p), state(init), target(t) {}

    void Integrate(const ControlInput& in, double dt) {
        ControlInput actual_in = in;

        if (state.fuel <= 0.0) {
            state.fuel = 0.0;
            actual_in.thrust_ratio = {0, 0, 0};
            actual_in.angular_thrust_ratio = {0, 0, 0};
        } else {
            double usage = (std::abs(in.thrust_ratio.x) + std::abs(in.thrust_ratio.y) + std::abs(in.thrust_ratio.z) +
                            std::abs(in.angular_thrust_ratio.x) + std::abs(in.angular_thrust_ratio.y) + std::abs(in.angular_thrust_ratio.z));
            state.fuel -= usage * params.fuel_consumption_rate * dt;
            if (state.fuel < 0.0) state.fuel = 0.0;
        }

        auto AxisAcceleration = [&](double val, double pos, double neg) {
            return (val >= 0.0 ? pos : neg) * val / params.mass;
        };
        auto AxisAngularAcceleration = [&](double val, double pos, double neg) {
            return (val >= 0.0 ? pos : neg) * val / (params.mass * 10.0);
        };

        Vector3 body_acc{
            AxisAcceleration(actual_in.thrust_ratio.x, params.thrust.positive.x, params.thrust.negative.x),
            AxisAcceleration(actual_in.thrust_ratio.y, params.thrust.positive.y, params.thrust.negative.y),
            AxisAcceleration(actual_in.thrust_ratio.z, params.thrust.positive.z, params.thrust.negative.z)
        };

        state.motion.acceleration = BodyToWorld(body_acc, state.pose.orientation) + params.environment.gravity;
        state.motion.velocity = state.motion.velocity + state.motion.acceleration * dt;
        state.pose.position = state.pose.position + state.motion.velocity * dt;

        Vector3 angular_acc{
            AxisAngularAcceleration(actual_in.angular_thrust_ratio.x, params.attitude_thrust.positive.x, params.attitude_thrust.negative.x),
            AxisAngularAcceleration(actual_in.angular_thrust_ratio.y, params.attitude_thrust.positive.y, params.attitude_thrust.negative.y),
            AxisAngularAcceleration(actual_in.angular_thrust_ratio.z, params.attitude_thrust.positive.z, params.attitude_thrust.negative.z)
        };

        state.motion.angular_velocity = state.motion.angular_velocity + angular_acc * dt;
        state.pose.orientation.pitch += state.motion.angular_velocity.y * dt;
        state.pose.orientation.roll += state.motion.angular_velocity.x * dt;
        state.pose.orientation.yaw += state.motion.angular_velocity.z * dt;

        while (state.pose.orientation.yaw > M_PI) state.pose.orientation.yaw -= 2*M_PI;
        while (state.pose.orientation.yaw < -M_PI) state.pose.orientation.yaw += 2*M_PI;

        if (state.pose.position.z <= 0.0) {
            state.pose.position.z = 0.0;
            state.motion.velocity = {0, 0, 0};
            state.motion.acceleration = {0, 0, 0};
            state.motion.angular_velocity = {0, 0, 0};
        }
    }

    LandingStatus EvaluateStatus() const {
        const Vector3 pos_err = target.pose.position - state.pose.position;
        return (state.pose.position.z <= 0.1 && pos_err.Length() < 5.0) ? LandingStatus::Landed : LandingStatus::InFlight;
    }

    const ShipState& GetState() const { return state; }
    const ShipParameters& GetParameters() const { return params; }
    const LandingTarget& GetLandingTarget() const { return target; }

    std::vector<Vector3> GetGearPointsWorld() const {
        std::vector<Vector3> world_points;
        for (const auto& body_point : params.gear_points_body) {
            Vector3 world_point = BodyToWorld(body_point, state.pose.orientation);
            world_point = world_point + state.pose.position;
            world_points.push_back(world_point);
        }
        return world_points;
    }

private:
    ShipParameters params;
    ShipState state;
    LandingTarget target;
};

inline ControlInput DefaultControlLaw(const ShipState& state, const ShipParameters& params, const LandingTarget& target, const ObstacleSensors& sensors) {
    ControlInput in{};
    const Vector3 pos_err = target.pose.position - state.pose.position;//текущее положение корабля - целевое положение
    const Vector3 vel = state.motion.velocity;//текущая скорость корабля
    double dist_xy = std::sqrt(pos_err.x*pos_err.x + pos_err.y*pos_err.y);//расстояние между кораблем и целевым положением
    
    bool near_target = (dist_xy < 50.0 && state.pose.position.z < 50.0);//близость к целевому положению
    
    // --- 1. ТЯГА И ВИЗУАЛЬНЫЕ УГЛЫ ---
    Vector3 thrust{0, 0, 0};//тяга
    double t_pitch = 0.0, t_roll = 0.0;//угол тангажа и рыскания
    bool hazard = false;//опасность

    if (!near_target) {//если корабль не близок к целевому положению
        if (sensors.front)  { thrust.x = -1.0; thrust.z = 1.0; t_pitch = 0.4;  hazard = true; }//если спереди корабля есть препятствие, то тяга назад и вверх
        if (sensors.back)   { thrust.x = 1.0;  t_pitch = -0.4; hazard = true; }//если сзади корабля есть препятствие, то тяга вперед
        if (sensors.left)   { thrust.y = 1.0;  t_roll = 0.4;   hazard = true; }//если слева от корабля есть препятствие, то тяга вправо
        if (sensors.right)  { thrust.y = -1.0; t_roll = -0.4;  hazard = true; }//если справа от корабля есть препятствие, то тяга влево
        if (sensors.bottom) { thrust.z = 1.0;  hazard = true; }//если снизу от корабля есть препятствие, то тяга вверх
        if (sensors.top)    { thrust.z = -1.0; hazard = true; }//если сверху от корабля есть препятствие, то тяга вниз
    }

    if (!hazard) {//если нет опасности
        Vector3 pos_body = WorldToBody(pos_err, state.pose.orientation);//положение корабля в телесной системе координат
        Vector3 vel_body = WorldToBody(vel, state.pose.orientation);//скорость корабля в телесной системе координат
        thrust.x = std::clamp(pos_body.x * 0.8 - vel_body.x * 1.5, -1.0, 1.0);//тяга вдоль оси x
        thrust.y = std::clamp(pos_body.y * 0.8 - vel_body.y * 1.5, -1.0, 1.0);//тяга вдоль оси y
        
        t_pitch = std::clamp(thrust.x * -0.25, -0.3, 0.3);//угол тангажа - текущий угол тангажа
        t_roll = std::clamp(thrust.y * 0.25, -0.3, 0.3);//угол рыскания - текущий угол рыскания
    }

    // --- 2. УГЛОВОЕ УПРАВЛЕНИЕ ---
    double target_yaw = std::atan2(pos_err.y, pos_err.x);//угол рыскания
    double yaw_err = target_yaw - state.pose.orientation.yaw;//угол рыскания - текущий угол рыскания
    while (yaw_err > M_PI) yaw_err -= 2*M_PI;//если угол рыскания больше 180 градусов, то вычитаем 360 градусов
    while (yaw_err < -M_PI) yaw_err += 2*M_PI;//если угол рыскания меньше -180 градусов, то прибавляем 360 градусов

    in.thrust_ratio = thrust;//тяга
    in.angular_thrust_ratio.x = std::clamp((t_roll - state.pose.orientation.roll) * 15.0 - state.motion.angular_velocity.x * 25.0, -1.0, 1.0);//угол рыскания - текущий угол рыскания
    in.angular_thrust_ratio.y = std::clamp((t_pitch - state.pose.orientation.pitch) * 15.0 - state.motion.angular_velocity.y * 25.0, -1.0, 1.0);//угол тангажа - текущий угол тангажа
    in.angular_thrust_ratio.z = std::clamp(yaw_err * 10.0 - state.motion.angular_velocity.z * 30.0, -1.0, 1.0);//угол рыскания - текущий угол рыскания

    // --- 3. ВЕРТИКАЛЬ ---
    if (!hazard || (sensors.bottom == false && sensors.top == false)) {
        double target_alt = (dist_xy < 20.0) ? 0.0 : std::max(30.0, dist_xy * 0.2);
        double target_v_z = std::clamp((target_alt - state.pose.position.z) * 0.8, -10.0, 40.0);
        double req_a_z = (target_v_z - vel.z) * 4.0 - params.environment.gravity.z;
        in.thrust_ratio.z = std::clamp(req_a_z / (params.thrust.positive.z / params.mass), 0.0, 1.0);
    }

    return in;
}

inline LandingTarget DefaultLandingTarget() {
    LandingTarget target{};
    target.pose.position = {-3600, 4500.0, 0.0};
    return target;
}
