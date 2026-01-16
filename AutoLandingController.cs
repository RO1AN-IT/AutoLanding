using System;
using System.Collections.Generic;
using UnityEngine;
using AL = AutoLanding;

[System.Serializable]
public class ThrustProfileData
{
    public Vector3 positive = new Vector3(20000f, 20000f, 50000f);
    public Vector3 negative = new Vector3(15000f, 15000f, 0f);
}

[System.Serializable]
public class EnvironmentData
{
    public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    public Vector3 windVelocity = new Vector3(5f, 0f, 0f);
    [Header("Сопротивление воздуха")]
    [Tooltip("Плотность воздуха (кг/м³). Стандартное значение на уровне моря: 1.225")]
    public double airDensity = 1.225;
    [Tooltip("Коэффициент сопротивления. Типичные значения: 0.3-0.5 для обтекаемых форм, 0.8-1.2 для плоских поверхностей")]
    public double dragCoefficient = 0.5;
    [Tooltip("Площадь поперечного сечения корабля (м²)")]
    public double crossSectionalArea = 10.0;
}

[System.Serializable]
public class ShipParametersData
{
    [Header("Основные параметры")]
    public double mass = 2200.0;
    
    [Header("Тяга")]
    public ThrustProfileData thrust = new ThrustProfileData();
    public ThrustProfileData attitudeThrust = new ThrustProfileData();
    
    [Header("Окружающая среда")]
    public EnvironmentData environment = new EnvironmentData();
    
    [Header("Ограничения")]
    public Vector3 orientationLimits = new Vector3(0.5f, 0.5f, 3.14159f);
    public Vector3 angularRateLimit = new Vector3(1f, 1f, 1f);
    
    [Header("Топливо")]
    public double fuelConsumptionRate = 0.5;
    
    [Header("Точки крепления шасси (локальные координаты)")]
    public List<Vector3> gearPointsBody = new List<Vector3>
    {
        new Vector3(2f, 1.5f, -1f),
        new Vector3(2f, -1.5f, -1f),
        new Vector3(-2f, 1.5f, -1f),
        new Vector3(-2f, -1.5f, -1f)
    };
}

public class AutoLandingController : MonoBehaviour
{
    [Header("Целевая точка посадки")]
    public Transform landingTargetTransform;
    
    public Vector3 landingTargetPosition = new Vector3(-3600f, 4500f, 0f);
    
    [Header("Параметры корабля")]
    public ShipParametersData shipParameters = new ShipParametersData();
    
    [Header("Шасси")]
    public List<Transform> gearObjects = new List<Transform>();
    
    [Header("Начальное состояние")]
    [SerializeField]
    private Vector3 initialPosition;
    
    public bool useCurrentPositionAsInitial = true;
    
    public Vector3 initialVelocity = new Vector3(1.8f, 50f, -60f);
    
    public Vector3 initialOrientation = new Vector3(0.30f, -0.18f, 0.2f);
    
    public bool useCurrentRotationAsInitial = false;
    
    public double initialFuel = 500.0;
    
    [Header("Сенсоры препятствий")]
    public Transform frontSensor;
    public Transform backSensor;
    public Transform leftSensor;
    public Transform rightSensor;
    public Transform topSensor;
    public Transform bottomSensor;
    
    [Header("Параметры сенсоров")]
    public bool useTriggerDetection = true;
    
    public float sensorRange = 10f;
    
    public float obstacleIgnoreDistance = 20f;
    
    public LayerMask obstacleLayer = -1;
    
    [Header("Триггеры сенсоров")]
    public Collider frontSensorCollider;
    public Collider backSensorCollider;
    public Collider leftSensorCollider;
    public Collider rightSensorCollider;
    public Collider topSensorCollider;
    public Collider bottomSensorCollider;
    
    public bool autoCreateTriggers = true;
    
    public Vector3 triggerSize = new Vector3(1f, 1f, 5f);
    
    [Header("Настройки симуляции")]
    public float simulationTimeStep = 0.05f;
    
    public float maxSpeed = 100f;
    
    public float maxSpeedNearObstacles = 30f;
    
    public bool autoStart = true;
    
    [Header("Визуализация")]
    public bool showGearPoints = true;
    
    public Color gearPointColor = Color.red;
    
    public float gearPointSize = 0.2f;
    
    [Header("Отладочная информация")]
    public bool showDebugInfo = true;
    
    [Header("Визуализация квадрокоптера")]
    public AL.PropellerRotation propellerRotation;
    
    private AL.LandingControlSystem controlSystem;
    private bool isLanding = false;
    private double accumulatedTime = 0.0;
    
    private HashSet<Collider> frontObstacles = new HashSet<Collider>();
    private AL.LazySequence<Vector3> landingTrajectory;
    private AL.LazySequence<AL.ObstacleSensors> sensorHistory;
    private HashSet<Collider> backObstacles = new HashSet<Collider>();
    private HashSet<Collider> leftObstacles = new HashSet<Collider>();
    private HashSet<Collider> rightObstacles = new HashSet<Collider>();
    private HashSet<Collider> topObstacles = new HashSet<Collider>();
    private HashSet<Collider> bottomObstacles = new HashSet<Collider>();
    
    void OnValidate()
    {
        if (gearObjects != null && gearObjects.Count > 0 && shipParameters.gearPointsBody != null)
        {
            shipParameters.gearPointsBody.Clear();
            foreach (var gearObject in gearObjects)
            {
                if (gearObject != null)
                {
                    Vector3 localPos = transform.InverseTransformPoint(gearObject.position);
                    shipParameters.gearPointsBody.Add(localPos);
                }
            }
        }
        
        if (useCurrentPositionAsInitial && Application.isPlaying == false)
        {
            initialPosition = transform.position;
        }
    }
    
    void Start()
    {
        if (useTriggerDetection)
        {
            if (autoCreateTriggers)
            {
                if (frontSensorCollider == null && frontSensor != null)
                    frontSensorCollider = CreateTriggerCollider(frontSensor, "front");
                if (backSensorCollider == null && backSensor != null)
                    backSensorCollider = CreateTriggerCollider(backSensor, "back");
                if (leftSensorCollider == null && leftSensor != null)
                    leftSensorCollider = CreateTriggerCollider(leftSensor, "left");
                if (rightSensorCollider == null && rightSensor != null)
                    rightSensorCollider = CreateTriggerCollider(rightSensor, "right");
                if (topSensorCollider == null && topSensor != null)
                    topSensorCollider = CreateTriggerCollider(topSensor, "top");
                if (bottomSensorCollider == null && bottomSensor != null)
                    bottomSensorCollider = CreateTriggerCollider(bottomSensor, "bottom");
            }
            
            CheckTriggerSetup();
            
            InitializeSensorTriggers();
        }
        
        InitializeSystem();
        
        if (autoStart)
        {
            StartLanding();
        }
    }
    
    private void InitializeSensorTriggers()
    {
        SetupSensorTrigger(frontSensorCollider, frontSensor, "front");
        SetupSensorTrigger(backSensorCollider, backSensor, "back");
        SetupSensorTrigger(leftSensorCollider, leftSensor, "left");
        SetupSensorTrigger(rightSensorCollider, rightSensor, "right");
        SetupSensorTrigger(topSensorCollider, topSensor, "top");
        SetupSensorTrigger(bottomSensorCollider, bottomSensor, "bottom");
    }
    
    private void SetupSensorTrigger(Collider collider, Transform sensorTransform, string direction)
    {
        if (collider != null && sensorTransform != null)
        {
            SensorTrigger sensorTrigger = sensorTransform.gameObject.GetComponent<SensorTrigger>();
            if (sensorTrigger == null)
            {
                sensorTrigger = sensorTransform.gameObject.AddComponent<SensorTrigger>();
            }
            sensorTrigger.controller = this;
            sensorTrigger.sensorDirection = direction;
        }
    }
    
    private Collider CreateTriggerCollider(Transform sensorTransform, string name)
    {
        BoxCollider trigger = sensorTransform.gameObject.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = sensorTransform.gameObject.AddComponent<BoxCollider>();
        }
        trigger.isTrigger = true;
        trigger.size = triggerSize;
        trigger.name = name + "_Trigger";
        
        SensorTrigger sensorTrigger = sensorTransform.gameObject.GetComponent<SensorTrigger>();
        if (sensorTrigger == null)
        {
            sensorTrigger = sensorTransform.gameObject.AddComponent<SensorTrigger>();
        }
        sensorTrigger.controller = this;
        sensorTrigger.sensorDirection = name.ToLower().Replace("sensor", "");
        
        return trigger;
    }
    
    void CheckTriggerSetup()
    {
        if (frontSensorCollider != null && !frontSensorCollider.isTrigger)
            Debug.LogWarning("frontSensorCollider должен иметь isTrigger = true для работы обнаружения препятствий!");
        if (backSensorCollider != null && !backSensorCollider.isTrigger)
            Debug.LogWarning("backSensorCollider должен иметь isTrigger = true для работы обнаружения препятствий!");
        if (leftSensorCollider != null && !leftSensorCollider.isTrigger)
            Debug.LogWarning("leftSensorCollider должен иметь isTrigger = true для работы обнаружения препятствий!");
        if (rightSensorCollider != null && !rightSensorCollider.isTrigger)
            Debug.LogWarning("rightSensorCollider должен иметь isTrigger = true для работы обнаружения препятствий!");
        if (topSensorCollider != null && !topSensorCollider.isTrigger)
            Debug.LogWarning("topSensorCollider должен иметь isTrigger = true для работы обнаружения препятствий!");
        if (bottomSensorCollider != null && !bottomSensorCollider.isTrigger)
            Debug.LogWarning("bottomSensorCollider должен иметь isTrigger = true для работы обнаружения препятствий!");
    }
    
    public void OnSensorTriggerEnter(string direction, Collider obstacle)
    {
        if (!useTriggerDetection) return;
        AddObstacle(direction, obstacle);
    }
    
    public void OnSensorTriggerExit(string direction, Collider obstacle)
    {
        if (!useTriggerDetection) return;
        if (obstacle == null) return;
        
        bool wasRemoved = RemoveObstacleFromAllDirections(obstacle);
        
        if (showDebugInfo && wasRemoved)
        {
            Debug.Log($"Препятствие {obstacle.name} удалено из всех направлений при выходе из {direction}");
        }
    }
    
    public void OnSensorTriggerStay(string direction, Collider obstacle)
    {
        if (!useTriggerDetection) return;
        if (obstacle == null) return;
        
        if (obstacle.gameObject != null && obstacle.gameObject.activeInHierarchy)
        {
            Collider triggerCollider = GetTriggerColliderForDirection(direction);
            if (triggerCollider != null && obstacle.bounds.Intersects(triggerCollider.bounds))
            {
                AddObstacle(direction, obstacle);
            }
            else
            {
                RemoveObstacle(direction, obstacle);
            }
        }
        else
        {
            RemoveObstacleFromAllDirections(obstacle);
        }
    }
    
    private Collider GetTriggerColliderForDirection(string direction)
    {
        switch (direction)
        {
            case "front": return frontSensorCollider;
            case "back": return backSensorCollider;
            case "left": return leftSensorCollider;
            case "right": return rightSensorCollider;
            case "top": return topSensorCollider;
            case "bottom": return bottomSensorCollider;
            default: return null;
        }
    }
    
    private void RemoveObstacle(string direction, Collider obstacle)
    {
        if (obstacle == null) return;
        
        switch (direction)
        {
            case "front":
                frontObstacles.Remove(obstacle);
                break;
            case "back":
                backObstacles.Remove(obstacle);
                break;
            case "left":
                leftObstacles.Remove(obstacle);
                break;
            case "right":
                rightObstacles.Remove(obstacle);
                break;
            case "top":
                topObstacles.Remove(obstacle);
                break;
            case "bottom":
                bottomObstacles.Remove(obstacle);
                break;
        }
    }
    
    private bool RemoveObstacleFromAllDirections(Collider obstacle)
    {
        if (obstacle == null) return false;
        
        bool removed = false;
        removed |= frontObstacles.Remove(obstacle);
        removed |= backObstacles.Remove(obstacle);
        removed |= leftObstacles.Remove(obstacle);
        removed |= rightObstacles.Remove(obstacle);
        removed |= topObstacles.Remove(obstacle);
        removed |= bottomObstacles.Remove(obstacle);
        
        return removed;
    }
    
    private void AddObstacle(string direction, Collider obstacle)
    {
        if (obstacle == null || obstacle.gameObject == null)
            return;
        
        if (obstacle.transform.IsChildOf(transform) || obstacle.transform == transform)
            return;
        
        if (obstacleLayer != -1 && (obstacleLayer.value & (1 << obstacle.gameObject.layer)) == 0)
            return;
        
        switch (direction)
        {
            case "front":
                frontObstacles.Add(obstacle);
                break;
            case "back":
                backObstacles.Add(obstacle);
                break;
            case "left":
                leftObstacles.Add(obstacle);
                break;
            case "right":
                rightObstacles.Add(obstacle);
                break;
            case "top":
                topObstacles.Add(obstacle);
                break;
            case "bottom":
                bottomObstacles.Add(obstacle);
                break;
        }
    }
    
    void Update()
    {
        if (isLanding)
        {
            if (useTriggerDetection)
            {
                CleanupInactiveObstacles();
            }
            
            accumulatedTime += Time.deltaTime;
            
            while (accumulatedTime >= simulationTimeStep)
            {
                UpdateSimulation(simulationTimeStep);
                accumulatedTime -= simulationTimeStep;
            }
            
            UpdateUnityTransform();
            
            CheckLandingStatus();
        }
    }
    
    private void CleanupInactiveObstacles()
    {
        var frontToRemove = new System.Collections.Generic.List<Collider>();
        var backToRemove = new System.Collections.Generic.List<Collider>();
        var leftToRemove = new System.Collections.Generic.List<Collider>();
        var rightToRemove = new System.Collections.Generic.List<Collider>();
        var topToRemove = new System.Collections.Generic.List<Collider>();
        var bottomToRemove = new System.Collections.Generic.List<Collider>();
        
        foreach (var c in frontObstacles)
        {
            if (c == null || !c.gameObject.activeInHierarchy)
                frontToRemove.Add(c);
        }
        foreach (var c in backObstacles)
        {
            if (c == null || !c.gameObject.activeInHierarchy)
                backToRemove.Add(c);
        }
        foreach (var c in leftObstacles)
        {
            if (c == null || !c.gameObject.activeInHierarchy)
                leftToRemove.Add(c);
        }
        foreach (var c in rightObstacles)
        {
            if (c == null || !c.gameObject.activeInHierarchy)
                rightToRemove.Add(c);
        }
        foreach (var c in topObstacles)
        {
            if (c == null || !c.gameObject.activeInHierarchy)
                topToRemove.Add(c);
        }
        foreach (var c in bottomObstacles)
        {
            if (c == null || !c.gameObject.activeInHierarchy)
                bottomToRemove.Add(c);
        }
        
        foreach (var c in frontToRemove) frontObstacles.Remove(c);
        foreach (var c in backToRemove) backObstacles.Remove(c);
        foreach (var c in leftToRemove) leftObstacles.Remove(c);
        foreach (var c in rightToRemove) rightObstacles.Remove(c);
        foreach (var c in topToRemove) topObstacles.Remove(c);
        foreach (var c in bottomToRemove) bottomObstacles.Remove(c);
    }
    
    void OnDrawGizmos()
    {
        if (showGearPoints)
        {
            Gizmos.color = gearPointColor;
            
            if (controlSystem != null)
            {
                var gearPoints = controlSystem.GetGearPointsWorld();
                foreach (var point in gearPoints)
                {
                    Gizmos.DrawSphere(point.ToUnityVector3(), gearPointSize);
                }
            }
            else if (gearObjects != null && gearObjects.Count > 0)
            {
                foreach (var gearObject in gearObjects)
                {
                    if (gearObject != null)
                    {
                        Gizmos.DrawSphere(gearObject.position, gearPointSize);
                        Gizmos.DrawLine(transform.position, gearObject.position);
                    }
                }
            }
            else if (shipParameters.gearPointsBody != null && shipParameters.gearPointsBody.Count > 0)
            {
                foreach (var localPoint in shipParameters.gearPointsBody)
                {
                    Vector3 worldPoint = transform.TransformPoint(localPoint);
                    Gizmos.DrawSphere(worldPoint, gearPointSize);
                    Gizmos.DrawLine(transform.position, worldPoint);
                }
            }
        }
        
        if (isLanding)
        {
            if (useTriggerDetection)
            {
                Gizmos.color = Color.yellow;
                DrawTriggerCollider(frontSensorCollider);
                DrawTriggerCollider(backSensorCollider);
                DrawTriggerCollider(leftSensorCollider);
                DrawTriggerCollider(rightSensorCollider);
                DrawTriggerCollider(topSensorCollider);
                DrawTriggerCollider(bottomSensorCollider);
                
                Gizmos.color = Color.red;
                if (frontObstacles.Count > 0) DrawObstacles(frontObstacles);
                if (backObstacles.Count > 0) DrawObstacles(backObstacles);
                if (leftObstacles.Count > 0) DrawObstacles(leftObstacles);
                if (rightObstacles.Count > 0) DrawObstacles(rightObstacles);
                if (topObstacles.Count > 0) DrawObstacles(topObstacles);
                if (bottomObstacles.Count > 0) DrawObstacles(bottomObstacles);
            }
            else
            {
                Gizmos.color = Color.yellow;
                DrawSensor(frontSensor);
                DrawSensor(backSensor);
                DrawSensor(leftSensor);
                DrawSensor(rightSensor);
                DrawSensor(topSensor);
                DrawSensor(bottomSensor);
            }
        }
    }
    
    void OnGUI()
    {
        if (showDebugInfo && isLanding && controlSystem != null)
        {
            var state = controlSystem.GetState();
            var status = controlSystem.EvaluateStatus();
            var target = controlSystem.GetLandingTarget();
            
            AL.Vector3D posErr = target.pose.position - state.pose.position;
            double distance = posErr.Length;
            double distanceXY = Math.Sqrt(posErr.x * posErr.x + posErr.y * posErr.y);
            
            GUILayout.BeginArea(new Rect(10, 10, 400, 250));
            GUILayout.Box("Информация о посадке");
            GUILayout.Label($"Статус: {status}");
            GUILayout.Label($"Позиция (система): ({state.pose.position.x:F2}, {state.pose.position.y:F2}, {state.pose.position.z:F2})");
            GUILayout.Label($"Позиция Unity: ({transform.position.x:F2}, {transform.position.y:F2}, {transform.position.z:F2})");
            GUILayout.Label($"Цель (система): ({target.pose.position.x:F2}, {target.pose.position.y:F2}, {target.pose.position.z:F2})");
            if (landingTargetTransform != null)
            {
                GUILayout.Label($"Цель Unity: ({landingTargetTransform.position.x:F2}, {landingTargetTransform.position.y:F2}, {landingTargetTransform.position.z:F2})");
            }
            GUILayout.Label($"Расстояние до цели: {distance:F2}");
            GUILayout.Label($"Расстояние XY: {distanceXY:F2}");
            double currentSpeed = state.motion.velocity.Length;
            GUILayout.Label($"Скорость: ({state.motion.velocity.x:F2}, {state.motion.velocity.y:F2}, {state.motion.velocity.z:F2})");
            GUILayout.Label($"Модуль скорости: {currentSpeed:F2}");
            bool hasObstacles = useTriggerDetection && (
                frontObstacles.Count > 0 || backObstacles.Count > 0 || 
                leftObstacles.Count > 0 || rightObstacles.Count > 0 || 
                topObstacles.Count > 0 || bottomObstacles.Count > 0);
            GUILayout.Label($"Препятствия обнаружены: {hasObstacles}");
            GUILayout.Label($"Макс. скорость: {(hasObstacles ? maxSpeedNearObstacles : maxSpeed):F2}");
            GUILayout.Label($"Расстояние игнорирования препятствий: {obstacleIgnoreDistance:F2}m");
            
            if (hasObstacles && useTriggerDetection)
            {
                float minDistance = float.MaxValue;
                string closestDirection = "нет";
                if (frontObstacles.Count > 0)
                {
                    float dist = GetClosestObstacleDistance(frontObstacles);
                    if (dist < minDistance) { minDistance = dist; closestDirection = "Front"; }
                }
                if (backObstacles.Count > 0)
                {
                    float dist = GetClosestObstacleDistance(backObstacles);
                    if (dist < minDistance) { minDistance = dist; closestDirection = "Back"; }
                }
                if (leftObstacles.Count > 0)
                {
                    float dist = GetClosestObstacleDistance(leftObstacles);
                    if (dist < minDistance) { minDistance = dist; closestDirection = "Left"; }
                }
                if (rightObstacles.Count > 0)
                {
                    float dist = GetClosestObstacleDistance(rightObstacles);
                    if (dist < minDistance) { minDistance = dist; closestDirection = "Right"; }
                }
                if (topObstacles.Count > 0)
                {
                    float dist = GetClosestObstacleDistance(topObstacles);
                    if (dist < minDistance) { minDistance = dist; closestDirection = "Top"; }
                }
                if (bottomObstacles.Count > 0)
                {
                    float dist = GetClosestObstacleDistance(bottomObstacles);
                    if (dist < minDistance) { minDistance = dist; closestDirection = "Bottom"; }
                }
                if (minDistance < float.MaxValue)
                {
                    GUILayout.Label($"Ближайшее препятствие ({closestDirection}): {minDistance:F2}m");
                }
            }
            
            GUILayout.Label($"Топливо: {state.fuel:F2}");
            GUILayout.Label($"Ориентация: Pitch={state.pose.orientation.pitch:F3}, Roll={state.pose.orientation.roll:F3}, Yaw={state.pose.orientation.yaw:F3}");
            
            var env = controlSystem.GetParameters().environment;
            Vector3 windVelUnity = env.wind_velocity.ToUnityVector3();
            AL.Vector3D relativeWind = state.motion.velocity - env.wind_velocity;
            double relativeWindSpeed = relativeWind.Length;
            double dragForce = 0.5 * env.air_density * relativeWindSpeed * relativeWindSpeed * 
                             env.cross_sectional_area * env.drag_coefficient;
            
            GUILayout.Label($"Ветер: ({windVelUnity.x:F2}, {windVelUnity.y:F2}, {windVelUnity.z:F2}) м/с");
            GUILayout.Label($"Относительная скорость ветра: {relativeWindSpeed:F2} м/с");
            GUILayout.Label($"Сила сопротивления: {dragForce:F2} Н");
            GUILayout.EndArea();
        }
    }
    
    public void InitializeSystem()
    {
        AL.Vector3D startPos;
        if (useCurrentPositionAsInitial)
        {
            startPos = AL.Vector3D.FromUnityVector3(transform.position);
            initialPosition = transform.position;
        }
        else if (initialPosition != Vector3.zero)
        {
            startPos = AL.Vector3D.FromUnityVector3(initialPosition);
        }
        else
        {
            startPos = AL.Vector3D.FromUnityVector3(transform.position);
            initialPosition = transform.position;
        }
        
        AL.Orientation startOrientation;
        if (useCurrentRotationAsInitial)
        {
            Vector3 euler = transform.rotation.eulerAngles;
            double pitch = NormalizeAngle(euler.x) * Mathf.Deg2Rad;
            double roll = NormalizeAngle(euler.z) * Mathf.Deg2Rad;
            double yaw = NormalizeAngle(euler.y) * Mathf.Deg2Rad;
            startOrientation = new AL.Orientation(pitch, roll, yaw);
            initialOrientation = new Vector3((float)pitch, (float)roll, (float)yaw);
        }
        else
        {
            startOrientation = new AL.Orientation(initialOrientation.x, initialOrientation.y, initialOrientation.z);
        }
        
        AL.ShipParameters params_ = ConvertShipParameters();
        
        AL.Pose initialPose = new AL.Pose(startPos, startOrientation);
        
        AL.MotionState initialMotion = new AL.MotionState(
            AL.Vector3D.FromUnityVector3(initialVelocity),
            new AL.Vector3D(0, 0, 0),
            new AL.Vector3D(0, 0, 0),
            new AL.Vector3D(0, 0, 0)
        );
        
        AL.ShipState initialState = new AL.ShipState(initialPose, initialMotion, initialFuel);
        
        AL.LandingTarget target;
        if (landingTargetTransform != null)
        {
            AL.Vector3D targetPos = AL.Vector3D.FromUnityVector3(landingTargetTransform.position);
            target = new AL.LandingTarget(new AL.Pose(targetPos, new AL.Orientation(0, 0, 0)));
        }
        else
        {
            AL.Vector3D targetPos = AL.Vector3D.FromUnityVector3(landingTargetPosition);
            target = new AL.LandingTarget(new AL.Pose(targetPos, new AL.Orientation(0, 0, 0)));
        }
        
        controlSystem = new AL.LandingControlSystem(params_, initialState, target);
        
        UpdateUnityTransform();
    }
    
    public void StartLanding()
    {
        if (controlSystem == null)
        {
            InitializeSystem();
        }
        
        isLanding = true;
        accumulatedTime = 0.0;
    }
    
    public void StopLanding()
    {
        isLanding = false;
    }
    
    public void ResetLanding()
    {
        isLanding = false;
        accumulatedTime = 0.0;
        landingTrajectory = null;
        sensorHistory = null;
        InitializeSystem();
    }
    
    private void UpdateSimulation(double dt)
    {
        if (controlSystem == null) return;
        
        AL.ObstacleSensors sensors = ReadSensors();
        
        if (sensorHistory == null)
        {
            sensorHistory = new AL.LazySequence<AL.ObstacleSensors>(new AL.ArraySequence<AL.ObstacleSensors>());
        }
        sensorHistory = (AL.LazySequence<AL.ObstacleSensors>)sensorHistory.Append(sensors);
        
        AL.ShipState currentState = controlSystem.GetState();
        AL.ShipParameters currentParams = controlSystem.GetParameters();
        
        currentParams.environment.wind_velocity = AL.Vector3D.FromUnityVector3(shipParameters.environment.windVelocity);
        currentParams.environment.air_density = shipParameters.environment.airDensity;
        currentParams.environment.drag_coefficient = shipParameters.environment.dragCoefficient;
        currentParams.environment.cross_sectional_area = shipParameters.environment.crossSectionalArea;
        
        bool hasObstacles = (useTriggerDetection && (
            frontObstacles.Count > 0 || backObstacles.Count > 0 || 
            leftObstacles.Count > 0 || rightObstacles.Count > 0 || 
            topObstacles.Count > 0 || bottomObstacles.Count > 0)) ||
            (!useTriggerDetection && (sensors.front || sensors.back || sensors.left || sensors.right || sensors.top || sensors.bottom));
        
        if (hasObstacles)
        {
            currentParams.max_speed = maxSpeedNearObstacles;
        }
        else
        {
            currentParams.max_speed = maxSpeed > 0 ? maxSpeed : 0;
        }
        
        controlSystem.UpdateParameters(currentParams);
        
        AL.LandingTarget currentTarget = controlSystem.GetLandingTarget();
        
        AL.ControlInput controlInput = AL.ControlLaw.DefaultControlLaw(
            currentState,
            currentParams,
            currentTarget,
            sensors
        );
        
        controlSystem.Integrate(controlInput, dt);
        
        if (landingTrajectory == null)
        {
            Vector3[] initialPos = { transform.position };
            landingTrajectory = new AL.LazySequence<Vector3>(initialPos);
        }
        
        AL.ShipState newState = controlSystem.GetState();
        Vector3 newPosition = newState.pose.position.ToUnityVector3();
        landingTrajectory = (AL.LazySequence<Vector3>)landingTrajectory.Append(newPosition);
    }
    
    private void UpdateUnityTransform()
    {
        if (controlSystem == null) return;
        
        AL.ShipState state = controlSystem.GetState();
        
        transform.position = state.pose.position.ToUnityVector3();
        
        Quaternion rotation = Quaternion.Euler(
            (float)(state.pose.orientation.pitch * Mathf.Rad2Deg),
            (float)(state.pose.orientation.yaw * Mathf.Rad2Deg),
            (float)(state.pose.orientation.roll * Mathf.Rad2Deg)
        );
        transform.rotation = rotation;
    }
    
    private AL.ObstacleSensors ReadSensors()
    {
        AL.ObstacleSensors sensors = new AL.ObstacleSensors();
        
        if (useTriggerDetection)
        {
            CleanupInactiveObstacles();
            
            VerifyObstaclesInTriggers();
            
            sensors.front = CheckObstaclesWithinDistance(frontObstacles, frontSensorCollider);
            sensors.back = CheckObstaclesWithinDistance(backObstacles, backSensorCollider);
            sensors.left = CheckObstaclesWithinDistance(leftObstacles, leftSensorCollider);
            sensors.right = CheckObstaclesWithinDistance(rightObstacles, rightSensorCollider);
            sensors.top = CheckObstaclesWithinDistance(topObstacles, topSensorCollider);
            sensors.bottom = CheckObstaclesWithinDistance(bottomObstacles, bottomSensorCollider);
            
            if (showDebugInfo && (sensors.front || sensors.back || sensors.left || sensors.right || sensors.top || sensors.bottom))
            {
                Debug.Log($"Обнаружены препятствия (в пределах {obstacleIgnoreDistance}m): Front={sensors.front}, " +
                         $"Back={sensors.back}, Left={sensors.left}, Right={sensors.right}, " +
                         $"Top={sensors.top}, Bottom={sensors.bottom}");
            }
        }
        else
        {
            sensors.front = CheckSensor(frontSensor, transform.forward);
            sensors.back = CheckSensor(backSensor, -transform.forward);
            sensors.left = CheckSensor(leftSensor, -transform.right);
            sensors.right = CheckSensor(rightSensor, transform.right);
            sensors.top = CheckSensor(topSensor, transform.up);
            sensors.bottom = CheckSensor(bottomSensor, -transform.up);
        }
        
        return sensors;
    }
    
    private bool CheckObstaclesWithinDistance(HashSet<Collider> obstacles, Collider triggerCollider)
    {
        if (obstacles == null || obstacles.Count == 0)
            return false;
        
        if (obstacleIgnoreDistance <= 0)
        {
            return obstacles.Count > 0;
        }
        
        Vector3 shipPosition = transform.position;
        
        foreach (var obstacle in obstacles)
        {
            if (obstacle == null || !obstacle.gameObject.activeInHierarchy)
                continue;
            
            Vector3 obstaclePosition = obstacle.bounds.center;
            float distance = Vector3.Distance(shipPosition, obstaclePosition);
            
            if (distance <= obstacleIgnoreDistance)
            {
                return true;
            }
        }
        
        return false;
    }
    
    private float GetClosestObstacleDistance(HashSet<Collider> obstacles)
    {
        if (obstacles == null || obstacles.Count == 0)
            return float.MaxValue;
        
        Vector3 shipPosition = transform.position;
        float minDistance = float.MaxValue;
        
        foreach (var obstacle in obstacles)
        {
            if (obstacle == null || !obstacle.gameObject.activeInHierarchy)
                continue;
            
            Vector3 obstaclePosition = obstacle.bounds.center;
            float distance = Vector3.Distance(shipPosition, obstaclePosition);
            
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }
        
        return minDistance;
    }
    
    private void VerifyObstaclesInTriggers()
    {
        VerifyObstaclesInTrigger(frontObstacles, frontSensorCollider, "front");
        VerifyObstaclesInTrigger(backObstacles, backSensorCollider, "back");
        VerifyObstaclesInTrigger(leftObstacles, leftSensorCollider, "left");
        VerifyObstaclesInTrigger(rightObstacles, rightSensorCollider, "right");
        VerifyObstaclesInTrigger(topObstacles, topSensorCollider, "top");
        VerifyObstaclesInTrigger(bottomObstacles, bottomSensorCollider, "bottom");
    }
    
    private void VerifyObstaclesInTrigger(HashSet<Collider> obstacles, Collider triggerCollider, string direction)
    {
        if (triggerCollider == null)
        {
            if (obstacles.Count > 0)
            {
                obstacles.Clear();
                if (showDebugInfo)
                {
                    Debug.Log($"Все препятствия направления {direction} удалены (триггер отсутствует)");
                }
            }
            return;
        }
        
        var toRemove = new System.Collections.Generic.List<Collider>();
        foreach (var obstacle in obstacles)
        {
            if (obstacle == null || !obstacle.gameObject.activeInHierarchy)
            {
                toRemove.Add(obstacle);
            }
            else
            {
                bool intersects = obstacle.bounds.Intersects(triggerCollider.bounds);
                if (!intersects)
                {
                    toRemove.Add(obstacle);
                }
            }
        }
        
        foreach (var obstacle in toRemove)
        {
            obstacles.Remove(obstacle);
            if (showDebugInfo)
            {
                Debug.Log($"Препятствие {obstacle?.name ?? "null"} удалено из {direction} при проверке пересечения");
            }
        }
    }
    
    private bool CheckSensor(Transform sensorTransform, Vector3 direction)
    {
        if (sensorTransform == null)
        {
            return Physics.Raycast(transform.position, direction, sensorRange, obstacleLayer);
        }
        else
        {
            return Physics.Raycast(sensorTransform.position, direction, sensorRange, obstacleLayer);
        }
    }
    
    private void CheckLandingStatus()
    {
        if (controlSystem == null) return;
        
        AL.LandingStatus status = controlSystem.EvaluateStatus();
        AL.ShipState state = controlSystem.GetState();
        
        if (status == AL.LandingStatus.Landed)
        {
            isLanding = false;
            Debug.Log($"Посадка завершена! Остаток топлива: {state.fuel:F2}");
        }
        else if (state.fuel <= 0.0 && state.pose.position.z > 0.1)
        {
            isLanding = false;
            Debug.LogWarning($"КРАХ: Закончилось топливо на высоте {state.pose.position.z:F2}");
        }
    }
    
    private AL.ShipParameters ConvertShipParameters()
    {
        AL.ShipParameters params_ = new AL.ShipParameters();
        
        params_.mass = shipParameters.mass;
        params_.thrust = new AL.ThrustProfile(
            AL.Vector3D.FromUnityVector3(shipParameters.thrust.positive),
            AL.Vector3D.FromUnityVector3(shipParameters.thrust.negative)
        );
        params_.attitude_thrust = new AL.ThrustProfile(
            AL.Vector3D.FromUnityVector3(shipParameters.attitudeThrust.positive),
            AL.Vector3D.FromUnityVector3(shipParameters.attitudeThrust.negative)
        );
        params_.environment = new AL.EnvironmentParams
        {
            gravity = AL.Vector3D.FromUnityVector3(shipParameters.environment.gravity),
            wind_velocity = AL.Vector3D.FromUnityVector3(shipParameters.environment.windVelocity),
            air_density = shipParameters.environment.airDensity,
            drag_coefficient = shipParameters.environment.dragCoefficient,
            cross_sectional_area = shipParameters.environment.crossSectionalArea
        };
        params_.orientation_limits = new AL.Orientation(
            shipParameters.orientationLimits.x,
            shipParameters.orientationLimits.y,
            shipParameters.orientationLimits.z
        );
        params_.angular_rate_limit = AL.Vector3D.FromUnityVector3(shipParameters.angularRateLimit);
        params_.fuel_consumption_rate = shipParameters.fuelConsumptionRate;
        
        bool hasObstacles = (useTriggerDetection && (
            frontObstacles.Count > 0 || backObstacles.Count > 0 || 
            leftObstacles.Count > 0 || rightObstacles.Count > 0 || 
            topObstacles.Count > 0 || bottomObstacles.Count > 0));
        
        if (hasObstacles)
        {
            params_.max_speed = maxSpeedNearObstacles;
        }
        else
        {
            params_.max_speed = maxSpeed > 0 ? maxSpeed : 0;
        }
        
        params_.gear_points_body = new List<AL.Vector3D>();
        
        if (gearObjects != null && gearObjects.Count > 0)
        {
            foreach (var gearObject in gearObjects)
            {
                if (gearObject != null)
                {
                    Vector3 localPos = transform.InverseTransformPoint(gearObject.position);
                    params_.gear_points_body.Add(AL.Vector3D.FromUnityVector3(localPos));
                }
            }
            
            shipParameters.gearPointsBody.Clear();
            foreach (var gearObject in gearObjects)
            {
                if (gearObject != null)
                {
                    Vector3 localPos = transform.InverseTransformPoint(gearObject.position);
                    shipParameters.gearPointsBody.Add(localPos);
                }
            }
        }
        else
        {
            foreach (var point in shipParameters.gearPointsBody)
            {
                params_.gear_points_body.Add(AL.Vector3D.FromUnityVector3(point));
            }
        }
        
        return params_;
    }
    
    private void DrawSensor(Transform sensorTransform)
    {
        if (sensorTransform != null)
        {
            Gizmos.DrawLine(sensorTransform.position, sensorTransform.position + sensorTransform.forward * sensorRange);
        }
    }
    
    private void DrawTriggerCollider(Collider collider)
    {
        if (collider != null && collider is BoxCollider)
        {
            BoxCollider boxCollider = collider as BoxCollider;
            Gizmos.matrix = collider.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
    
    private void DrawObstacles(HashSet<Collider> obstacles)
    {
        foreach (var obstacle in obstacles)
        {
            if (obstacle != null)
            {
                Gizmos.DrawWireSphere(obstacle.bounds.center, 0.5f);
            }
        }
    }
    
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
    
    public AL.LandingStatus GetLandingStatus()
    {
        return controlSystem != null ? controlSystem.EvaluateStatus() : AL.LandingStatus.InFlight;
    }
    
    public AL.ShipState GetCurrentState()
    {
        return controlSystem != null ? controlSystem.GetState() : new AL.ShipState();
    }
    
    public bool IsLanding()
    {
        return isLanding;
    }
    
    public AL.LazySequence<Vector3> GetLandingTrajectory()
    {
        return landingTrajectory;
    }
    
    public AL.LazySequence<AL.ObstacleSensors> GetSensorHistory()
    {
        return sensorHistory;
    }
    
    public AL.ISequence<Vector3> GetFilteredTrajectory(float minDistance)
    {
        if (landingTrajectory == null || landingTrajectory.GetLength() <= 0)
            return new AL.ArraySequence<Vector3>();
        
        Vector3 lastPos = landingTrajectory.GetFirst();
        AL.ArraySequence<Vector3> filtered = new AL.ArraySequence<Vector3>();
        filtered = (AL.ArraySequence<Vector3>)filtered.Append(lastPos);
        
        for (int i = 1; i < landingTrajectory.GetLength(); i++)
        {
            Vector3 currentPos = landingTrajectory.Get(i);
            if (Vector3.Distance(currentPos, lastPos) >= minDistance)
            {
                filtered = (AL.ArraySequence<Vector3>)filtered.Append(currentPos);
                lastPos = currentPos;
            }
        }
        
        return filtered;
    }
    
    public AL.ISequence<AL.ObstacleSensors> GetObstacleEvents()
    {
        if (sensorHistory == null || sensorHistory.GetLength() <= 0)
            return new AL.ArraySequence<AL.ObstacleSensors>();
        
        return sensorHistory.Where(s => s.front || s.back || s.left || s.right || s.top || s.bottom);
    }
}

