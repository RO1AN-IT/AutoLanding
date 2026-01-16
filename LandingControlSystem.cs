using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoLanding
{
    public struct Vector3D
    {
        public double x, y, z;

        public Vector3D(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3D operator +(Vector3D a, Vector3D b) => new Vector3D(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3D operator -(Vector3D a, Vector3D b) => new Vector3D(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3D operator *(Vector3D a, double scalar) => new Vector3D(a.x * scalar, a.y * scalar, a.z * scalar);
        public static Vector3D operator /(Vector3D a, double scalar) => new Vector3D(a.x / scalar, a.y / scalar, a.z / scalar);
        public static Vector3D operator -(Vector3D a) => new Vector3D(-a.x, -a.y, -a.z);

        public double Length => Math.Sqrt(x * x + y * y + z * z);

        public Vector3D Normalized
        {
            get
            {
                double len = Length;
                return len > 1e-6 ? (this / len) : new Vector3D(0, 0, 0);
            }
        }

        public Vector3 ToUnityVector3()
        {
            return new Vector3((float)x, (float)z, (float)y);
        }

        public static Vector3D FromUnityVector3(Vector3 v)
        {
            return new Vector3D(v.x, v.z, v.y);
        }
    }

    public struct Orientation
    {
        public double pitch, roll, yaw;

        public Orientation(double pitch, double roll, double yaw)
        {
            this.pitch = pitch;
            this.roll = roll;
            this.yaw = yaw;
        }
    }

    public struct Pose
    {
        public Vector3D position;
        public Orientation orientation;

        public Pose(Vector3D position, Orientation orientation)
        {
            this.position = position;
            this.orientation = orientation;
        }
    }

    public struct MotionState
    {
        public Vector3D velocity;
        public Vector3D acceleration;
        public Vector3D angular_velocity;
        public Vector3D angular_acceleration;

        public MotionState(Vector3D velocity, Vector3D acceleration, Vector3D angular_velocity, Vector3D angular_acceleration)
        {
            this.velocity = velocity;
            this.acceleration = acceleration;
            this.angular_velocity = angular_velocity;
            this.angular_acceleration = angular_acceleration;
        }
    }

    public struct ShipState
    {
        public Pose pose;
        public MotionState motion;
        public double fuel;

        public ShipState(Pose pose, MotionState motion, double fuel)
        {
            this.pose = pose;
            this.motion = motion;
            this.fuel = fuel;
        }
    }

    public struct ThrustProfile
    {
        public Vector3D positive;
        public Vector3D negative;

        public ThrustProfile(Vector3D positive, Vector3D negative)
        {
            this.positive = positive;
            this.negative = negative;
        }
    }

    [Serializable]
    public struct EnvironmentParams
    {
        public Vector3D gravity;
        public Vector3D wind_velocity;
        public double air_density;
        public double drag_coefficient;
        public double cross_sectional_area;
    }

    [Serializable]
    public struct ShipParameters
    {
        public double mass;
        public ThrustProfile thrust;
        public ThrustProfile attitude_thrust;
        public EnvironmentParams environment;
        public Orientation orientation_limits;
        public Vector3D angular_rate_limit;
        public double fuel_consumption_rate;
        public List<Vector3D> gear_points_body;
        public double max_speed;
    }

    public struct ObstacleSensors
    {
        public bool front, back, left, right, top, bottom;

        public ObstacleSensors(bool front, bool back, bool left, bool right, bool top, bool bottom)
        {
            this.front = front;
            this.back = back;
            this.left = left;
            this.right = right;
            this.top = top;
            this.bottom = bottom;
        }
    }

    public struct LandingTarget
    {
        public Pose pose;
        public double initial_dist;

        public LandingTarget(Pose pose, double initial_dist = 0.0)
        {
            this.pose = pose;
            this.initial_dist = initial_dist;
        }
    }

    public struct ControlInput
    {
        public Vector3D thrust_ratio;
        public Vector3D angular_thrust_ratio;

        public ControlInput(Vector3D thrust_ratio, Vector3D angular_thrust_ratio)
        {
            this.thrust_ratio = thrust_ratio;
            this.angular_thrust_ratio = angular_thrust_ratio;
        }
    }

    public enum LandingStatus
    {
        InFlight,
        Landed
    }

    public static class TransformUtils
    {
        private const double PI = Math.PI;

        public static Vector3D BodyToWorld(Vector3D body_vec, Orientation ori)
        {
            double cp = Math.Cos(ori.pitch);
            double sp = Math.Sin(ori.pitch);
            double cr = Math.Cos(ori.roll);
            double sr = Math.Sin(ori.roll);
            double cy = Math.Cos(ori.yaw);
            double sy = Math.Sin(ori.yaw);

            return new Vector3D(
                body_vec.x * (cy * cp) + body_vec.y * (cy * sp * sr - sy * cr) + body_vec.z * (cy * sp * cr + sy * sr),
                body_vec.x * (sy * cp) + body_vec.y * (sy * sp * sr + cy * cr) + body_vec.z * (sy * sp * cr - cy * sr),
                body_vec.x * (-sp) + body_vec.y * (cp * sr) + body_vec.z * (cp * cr)
            );
        }

        public static Vector3D WorldToBody(Vector3D world_vec, Orientation ori)
        {
            double cp = Math.Cos(ori.pitch);
            double sp = Math.Sin(ori.pitch);
            double cr = Math.Cos(ori.roll);
            double sr = Math.Sin(ori.roll);
            double cy = Math.Cos(ori.yaw);
            double sy = Math.Sin(ori.yaw);

            return new Vector3D(
                world_vec.x * (cy * cp) + world_vec.y * (sy * cp) + world_vec.z * (-sp),
                world_vec.x * (cy * sp * sr - sy * cr) + world_vec.y * (sy * sp * sr + cy * cr) + world_vec.z * (cp * sr),
                world_vec.x * (cy * sp * cr + sy * sr) + world_vec.y * (sy * sp * cr - cy * sr) + world_vec.z * (cp * cr)
            );
        }
    }

    public class LandingControlSystem
    {
        private ShipParameters params_;
        private ShipState state_;
        private LandingTarget target_;

        public LandingControlSystem(ShipParameters parameters, ShipState initialState, LandingTarget target)
        {
            params_ = parameters;
            state_ = initialState;
            target_ = target;
        }
        
        public void UpdateParameters(ShipParameters newParams)
        {
            params_ = newParams;
        }

        public void Integrate(ControlInput input, double dt)
        {
            ControlInput actualInput = input;

            if (state_.fuel <= 0.0)
            {
                state_.fuel = 0.0;
                actualInput = new ControlInput(new Vector3D(0, 0, 0), new Vector3D(0, 0, 0));
            }
            else
            {
                double usage = Math.Abs(input.thrust_ratio.x) + Math.Abs(input.thrust_ratio.y) + Math.Abs(input.thrust_ratio.z) +
                              Math.Abs(input.angular_thrust_ratio.x) + Math.Abs(input.angular_thrust_ratio.y) + Math.Abs(input.angular_thrust_ratio.z);
                state_.fuel -= usage * params_.fuel_consumption_rate * dt;
                if (state_.fuel < 0.0) state_.fuel = 0.0;
            }

            double AxisAcceleration(double val, double pos, double neg)
            {
                return (val >= 0.0 ? pos : neg) * val / params_.mass;
            }

            double AxisAngularAcceleration(double val, double pos, double neg)
            {
                return (val >= 0.0 ? pos : neg) * val / (params_.mass * 10.0);
            }

            Vector3D body_acc = new Vector3D(
                AxisAcceleration(actualInput.thrust_ratio.x, params_.thrust.positive.x, params_.thrust.negative.x),
                AxisAcceleration(actualInput.thrust_ratio.y, params_.thrust.positive.y, params_.thrust.negative.y),
                AxisAcceleration(actualInput.thrust_ratio.z, params_.thrust.positive.z, params_.thrust.negative.z)
            );

            Vector3D new_acceleration = TransformUtils.BodyToWorld(body_acc, state_.pose.orientation) + params_.environment.gravity;
            Vector3D new_velocity = state_.motion.velocity + new_acceleration * dt;
            
            if (params_.max_speed > 0)
            {
                double currentSpeed = new_velocity.Length;
                if (currentSpeed > params_.max_speed)
                {
                    new_velocity = new_velocity.Normalized * params_.max_speed;
                }
            }
            
            Vector3D new_position = state_.pose.position + new_velocity * dt;

            Vector3D angular_acc = new Vector3D(
                AxisAngularAcceleration(actualInput.angular_thrust_ratio.x, params_.attitude_thrust.positive.x, params_.attitude_thrust.negative.x),
                AxisAngularAcceleration(actualInput.angular_thrust_ratio.y, params_.attitude_thrust.positive.y, params_.attitude_thrust.negative.y),
                AxisAngularAcceleration(actualInput.angular_thrust_ratio.z, params_.attitude_thrust.positive.z, params_.attitude_thrust.negative.z)
            );

            Vector3D new_angular_velocity = state_.motion.angular_velocity + angular_acc * dt;
            double new_pitch = state_.pose.orientation.pitch + new_angular_velocity.y * dt;
            double new_roll = state_.pose.orientation.roll + new_angular_velocity.x * dt;
            double new_yaw = state_.pose.orientation.yaw + new_angular_velocity.z * dt;

            while (new_yaw > Math.PI) new_yaw -= 2 * Math.PI;
            while (new_yaw < -Math.PI) new_yaw += 2 * Math.PI;

            if (new_position.z <= 0.0)
            {
                new_position = new Vector3D(new_position.x, new_position.y, 0.0);
                new_velocity = new Vector3D(0, 0, 0);
                new_acceleration = new Vector3D(0, 0, 0);
                new_angular_velocity = new Vector3D(0, 0, 0);
            }

            state_.pose.position = new_position;
            state_.pose.orientation = new Orientation(new_pitch, new_roll, new_yaw);
            state_.motion.velocity = new_velocity;
            state_.motion.acceleration = new_acceleration;
            state_.motion.angular_velocity = new_angular_velocity;
        }

        public LandingStatus EvaluateStatus()
        {
            Vector3D pos_err = target_.pose.position - state_.pose.position;
            double height_err = Math.Abs(pos_err.z);
            double dist_xy = Math.Sqrt(pos_err.x * pos_err.x + pos_err.y * pos_err.y);
            return (height_err < 0.5 && dist_xy < 5.0 && state_.motion.velocity.Length < 2.0) ? LandingStatus.Landed : LandingStatus.InFlight;
        }

        public ShipState GetState() => state_;
        public ShipParameters GetParameters() => params_;
        public LandingTarget GetLandingTarget() => target_;

        public List<Vector3D> GetGearPointsWorld()
        {
            List<Vector3D> world_points = new List<Vector3D>();
            foreach (var body_point in params_.gear_points_body)
            {
                Vector3D world_point = TransformUtils.BodyToWorld(body_point, state_.pose.orientation);
                world_point = world_point + state_.pose.position;
                world_points.Add(world_point);
            }
            return world_points;
        }
    }

    public static class ControlLaw
    {
        private const double PI = Math.PI;

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static ControlInput DefaultControlLaw(ShipState state, ShipParameters parameters, LandingTarget target, ObstacleSensors sensors)
        {
            ControlInput input = new ControlInput();
            Vector3D pos_err = target.pose.position - state.pose.position;
            Vector3D vel = state.motion.velocity;
            double dist_xy = Math.Sqrt(pos_err.x * pos_err.x + pos_err.y * pos_err.y);
            double height_err = Math.Abs(pos_err.z);

            bool near_target = (dist_xy < 50.0 && height_err < 50.0);

            double thrust_x = 0.0, thrust_y = 0.0, thrust_z = 0.0;
            double t_pitch = 0.0, t_roll = 0.0;
            bool hazard = false;
            int hazardCount = 0;

            if (!near_target)
            {
                if (sensors.front) 
                { 
                    thrust_x -= 1.5; 
                    thrust_z += 1.5; 
                    t_pitch += 0.6; 
                    hazard = true;
                    hazardCount++;
                }
                if (sensors.back) 
                { 
                    thrust_x += 1.5; 
                    t_pitch -= 0.6; 
                    hazard = true;
                    hazardCount++;
                }
                if (sensors.left) 
                { 
                    thrust_y += 1.5; 
                    t_roll += 0.6; 
                    hazard = true;
                    hazardCount++;
                }
                if (sensors.right) 
                { 
                    thrust_y -= 1.5; 
                    t_roll -= 0.6; 
                    hazard = true;
                    hazardCount++;
                }
                if (sensors.bottom) 
                { 
                    thrust_z += 1.5; 
                    hazard = true;
                    hazardCount++;
                }
                if (sensors.top) 
                { 
                    thrust_z -= 1.5; 
                    hazard = true;
                    hazardCount++;
                }
                
                if (hazardCount > 1)
                {
                    double norm = Math.Max(Math.Abs(thrust_x), Math.Max(Math.Abs(thrust_y), Math.Abs(thrust_z)));
                    if (norm > 1.0)
                    {
                        thrust_x = Clamp(thrust_x / norm, -1.0, 1.0);
                        thrust_y = Clamp(thrust_y / norm, -1.0, 1.0);
                        thrust_z = Clamp(thrust_z / norm, -1.0, 1.0);
                    }
                    t_pitch = Clamp(t_pitch / hazardCount, -0.5, 0.5);
                    t_roll = Clamp(t_roll / hazardCount, -0.5, 0.5);
                }
                else
                {
                    thrust_x = Clamp(thrust_x, -1.0, 1.0);
                    thrust_y = Clamp(thrust_y, -1.0, 1.0);
                    thrust_z = Clamp(thrust_z, -1.0, 1.0);
                    t_pitch = Clamp(t_pitch, -0.5, 0.5);
                    t_roll = Clamp(t_roll, -0.5, 0.5);
                }
            }

            if (!hazard)
            {
                Vector3D pos_body = TransformUtils.WorldToBody(pos_err, state.pose.orientation);
                Vector3D vel_body = TransformUtils.WorldToBody(vel, state.pose.orientation);
                thrust_x = Clamp(pos_body.x * 0.8 - vel_body.x * 1.5, -1.0, 1.0);
                thrust_y = Clamp(pos_body.y * 0.8 - vel_body.y * 1.5, -1.0, 1.0);

                t_pitch = Clamp(thrust_x * -0.25, -0.3, 0.3);
                t_roll = Clamp(thrust_y * 0.25, -0.3, 0.3);
            }

            double target_yaw = Math.Atan2(pos_err.y, pos_err.x);
            double yaw_err = target_yaw - state.pose.orientation.yaw;
            while (yaw_err > PI) yaw_err -= 2 * PI;
            while (yaw_err < -PI) yaw_err += 2 * PI;

            Vector3D thrust = new Vector3D(thrust_x, thrust_y, thrust_z);
            Vector3D angular_thrust = new Vector3D(
                Clamp((t_roll - state.pose.orientation.roll) * 15.0 - state.motion.angular_velocity.x * 25.0, -1.0, 1.0),
                Clamp((t_pitch - state.pose.orientation.pitch) * 15.0 - state.motion.angular_velocity.y * 25.0, -1.0, 1.0),
                Clamp(yaw_err * 10.0 - state.motion.angular_velocity.z * 30.0, -1.0, 1.0)
            );

            if (!hazard || (!sensors.bottom && !sensors.top))
            {
                double height_offset = (dist_xy < 20.0) ? 0.0 : Math.Max(30.0, dist_xy * 0.2);
                double target_alt = target.pose.position.z + height_offset;
                double target_v_z = Clamp((target_alt - state.pose.position.z) * 0.8, -10.0, 40.0);
                double req_a_z = (target_v_z - vel.z) * 4.0 - parameters.environment.gravity.z;
                thrust_z = Clamp(req_a_z / (parameters.thrust.positive.z / parameters.mass), 0.0, 1.0);
                thrust = new Vector3D(thrust_x, thrust_y, thrust_z);
            }

            input = new ControlInput(thrust, angular_thrust);

            return input;
        }

        public static LandingTarget DefaultLandingTarget()
        {
            return new LandingTarget(new Pose(new Vector3D(-3600, 4500.0, 0.0), new Orientation(0, 0, 0)));
        }
    }
}

