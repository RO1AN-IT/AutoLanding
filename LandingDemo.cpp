#include <cmath>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <string>

#include "LandingControlSystem.h"

int main() {
    std::ofstream log_file("landing_log.txt");
    if (!log_file.is_open()) {
        std::cerr << "Failed to open landing_log.txt for writing.\n";
        return 1;
    }

    ShipParameters params;
    params.mass = 2200.0;
    params.thrust.positive = {20000.0, 20000.0, 50000.0};
    params.thrust.negative = {15000.0, 15000.0, 0.0};
    params.attitude_thrust.positive = {10000.0, 10000.0, 10000.0};
    params.attitude_thrust.negative = {10000.0, 10000.0, 10000.0};
    params.environment.gravity = {0.0, 0.0, -9.81};
    params.environment.wind_velocity = {5.0, 0.0, 0.0};
    params.orientation_limits = {0.5, 0.5, 3.14159};
    params.angular_rate_limit = {1.0, 1.0, 1.0};
    params.fuel_consumption_rate = 0.5; // Расход топлива: 0.5 единицы на 100% тяги в секунду
    // Точки крепления шасси в локальной системе координат (относительно центра масс)
    params.gear_points_body = {
        {2.0, 1.5, -1.0},   // Переднее правое
        {2.0, -1.5, -1.0},  // Переднее левое
        {-2.0, 1.5, -1.0},  // Заднее правое
        {-2.0, -1.5, -1.0}  // Заднее левое
    };

    ShipState initial{};
    initial.pose.position = {0.0, 0.0, 1000.0};
    initial.motion.velocity = {1.8, 50, -60.0};
    initial.pose.orientation = {0.30, -0.18, 0.2};
    initial.fuel = 500.0; // Начальный запас топлива

    LandingTarget target = DefaultLandingTarget();
    LandingControlSystem controller(params, initial, target);

    log_file << std::fixed << std::setprecision(2);
    log_file << "Simulating powered landing with fuel consumption...\n";

    for (int i = 0; i < 40000; ++i) {
        const ShipState& s = controller.GetState();
        ObstacleSensors sensors;
        
        // --- ТЕСТ: Помехи ---
        if (i >= 4000 && i < 4100) {
            sensors.left = true;
        }
        if (i >= 4100 && i <= 4200) {
            sensors.right = true;
        }

        // Авто-сенсор снизу
        double dist_xy = std::sqrt(std::pow(target.pose.position.x - s.pose.position.x, 2) + 
                                   std::pow(target.pose.position.y - s.pose.position.y, 2));
        if (s.pose.position.z < 10.0 && dist_xy > 50.0) {
            sensors.bottom = true; 
        }

        // Логирование
        auto p = s.pose.position;
        auto o = s.pose.orientation;
        
        std::string h_status = "none";
        if (sensors.left) h_status = "LEFT!";
        else if (sensors.right) h_status = "RIGHT!";
        else if (sensors.bottom) h_status = "UP!";

        log_file << "step=" << i
                 << " pos=(" << p.x << "," << p.y << "," << p.z << ")"
                 << " fuel=" << s.fuel
                 << " hazard=" << h_status
                 << " roll=" << o.roll << "\n";

        // Управление
        ControlInput in = DefaultControlLaw(s, params, target, sensors);
        controller.Integrate(in, 0.05);

        if (controller.EvaluateStatus() == LandingStatus::Landed) {
            log_file << "Flight result: successful landing.\n";
            auto gear_points = controller.GetGearPointsWorld();
            log_file << "Gear points (world): ";
            for (size_t j = 0; j < gear_points.size(); ++j) {
                log_file << "(" << gear_points[j].x << "," << gear_points[j].y << "," << gear_points[j].z << ")";
                if (j < gear_points.size() - 1) log_file << " ";
            }
            log_file << "\n";
            std::cout << "Landed successfully! Fuel left: " << s.fuel << std::endl;
            break;
        }

        if (s.fuel <= 0.0 && s.pose.position.z > 0.1) {
            log_file << "CRASH: Out of fuel at height " << s.pose.position.z << "\n";
            std::cout << "CRASH: Out of fuel!" << std::endl;
            break;
        }
    }

    std::cout << "Telemetry saved to landing_log.txt\n";
    return 0;
}
