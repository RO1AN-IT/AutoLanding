using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AutoLanding
{
    // Вспомогательные структуры и классы для работы с C++ DLL
    // Структуры для передачи данных между C# и C++
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3D
    {
    public double x;
    public double y;
    public double z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OrientationD
    {
    public double pitch;
    public double roll;
    public double yaw;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ShipStateD
    {
    public Vector3D position;
    public OrientationD orientation;
    public Vector3D velocity;
    public Vector3D acceleration;
    public Vector3D angular_velocity;
    public Vector3D angular_acceleration;
    public double dt;
    public UIntPtr step;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ShipParametersD
    {
    public Vector3D thrust_positive;
    public Vector3D thrust_negative;
    public Vector3D attitude_thrust_positive;
    public Vector3D attitude_thrust_negative;
    public Vector3D angular_rate_limit;
    public double max_pitch;
    public double max_roll;
    public double max_yaw;
    public Vector3D gravity;
    public Vector3D wind_velocity;
    public double mass;
    public double default_dt;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public Vector3D[] gear_points;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LandingTargetD
    {
    public Vector3D position;
    public OrientationD orientation;
    public Vector3D velocity;
    public Vector3D acceleration;
    public Vector3D angular_velocity;
    public Vector3D angular_acceleration;
    }

    public enum LandingStatusD
    {
    InFlight = 0,
    Landed = 1,
    Crashed = 2
    }

    // P/Invoke объявления для вызова C++ DLL
    public static class LandingControlSystemNative
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private const string DLL_NAME = "LandingControlSystemWrapper";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    private const string DLL_NAME = "libLandingControlSystemWrapper";
#elif UNITY_STANDALONE_LINUX
    private const string DLL_NAME = "libLandingControlSystemWrapper";
#else
    private const string DLL_NAME = "LandingControlSystemWrapper";
#endif

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr CreateLandingSystem(
        ref ShipParametersD params_,
        ref ShipStateD initial_state,
        ref LandingTargetD target);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void DestroyLandingSystem(IntPtr handle);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void GetCurrentState(IntPtr handle, out ShipStateD state);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Step(IntPtr handle, out ShipStateD state);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern LandingStatusD GetStatus(IntPtr handle);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void GetGearPointsWorld(IntPtr handle, [Out] Vector3D[] gear_points);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetLandingTarget(IntPtr handle, ref LandingTargetD target);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetParameters(IntPtr handle, ref ShipParametersD params_);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Reset(IntPtr handle);
    }

}

// Unity MonoBehaviour скрипт для управления посадкой
// Класс вынесен из namespace для правильной работы с Unity Inspector
public class AutoLandingController : MonoBehaviour
{
    [Header("Ship Configuration")]
    [Tooltip("Объект корабля (основной объект)")]
    public Transform shipTransform;

    [Tooltip("Массив из 4 точек шасси (дочерние объекты или отдельные трансформы)")]
    public Transform[] gearPoints = new Transform[4];

    [Header("Landing Target")]
    [Tooltip("Точка посадки (Transform объекта)")]
    public Transform landingTargetTransform;

    [Header("Ship Parameters")]
    [Tooltip("Масса корабля (кг)")]
    public double mass = 2200.0;

    [Tooltip("Максимальная тяга по осям (X, Y, Z)")]
    public Vector3 thrustPositive = new Vector3(20000f, 20000f, 50000f);

    [Tooltip("Максимальная обратная тяга по осям (X, Y, Z)")]
    public Vector3 thrustNegative = new Vector3(15000f, 15000f, 0f);

    [Tooltip("Максимальная угловая скорость (рад/с)")]
    public Vector3 angularRateLimit = new Vector3(0.05f, 0.05f, 0.08f);

    [Tooltip("Ограничения ориентации (pitch, roll, yaw в радианах)")]
    public Vector3 orientationLimits = new Vector3(0.35f, 0.35f, 3.14159f);

    [Header("Rotation Smoothing")]
    [Tooltip("Максимальная скорость поворота (градусов/сек). Чем меньше, тем плавнее разворот")]
    [Range(1f, 180f)]
    public float maxRotationSpeed = 30f;

    [Tooltip("Минимальное расстояние для начала плавного разворота (метры)")]
    public float smoothRotationStartDistance = 50f;

    [Tooltip("Использовать только угловую скорость для поворота (не применять целевую ориентацию напрямую)")]
    public bool useAngularVelocityOnly = true;

    [Tooltip("Гравитация (X, Y, Z)")]
    public Vector3 gravity = new Vector3(0f, 0f, -9.81f);

    [Tooltip("Скорость ветра (X, Y, Z)")]
    public Vector3 windVelocity = new Vector3(5f, 0f, 0f);

    [Header("Simulation Settings")]
    [Tooltip("Шаг времени симуляции (секунды)")]
    public double simulationTimeStep = 0.05;

        [Tooltip("Автоматически начинать посадку при старте")]
        public bool autoStart = true;

    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugInfo = true;

    private IntPtr landingSystemHandle = IntPtr.Zero;
    private bool isInitialized = false;
    private bool isLanding = false;
    private AutoLanding.LandingStatusD currentStatus = AutoLanding.LandingStatusD.InFlight;
    private Vector3 lastAppliedPosition = Vector3.zero;
    private ulong lastStepNumber = 0;

    // Вспомогательные функции для преобразования координат
    // Unity использует левую систему координат (Y вверх), C++ использует правую систему (Z вверх)
    private AutoLanding.Vector3D UnityToNative(Vector3 unityVec)
    {
        // Преобразование: Unity (X, Y, Z) -> Native (X, Z, Y)
        // Y в Unity становится Z в Native (вертикальная ось)
        return new AutoLanding.Vector3D
        {
            x = unityVec.x,
            y = unityVec.z,  // Unity Z -> Native Y
            z = unityVec.y   // Unity Y -> Native Z (вертикальная ось)
        };
    }

    private Vector3 NativeToUnity(AutoLanding.Vector3D nativeVec)
    {
        // Преобразование: Native (X, Y, Z) -> Unity (X, Y, Z)
        // Z в Native становится Y в Unity (вертикальная ось)
        return new Vector3(
            (float)nativeVec.x,
            (float)nativeVec.z,  // Native Z -> Unity Y (вертикальная ось)
            (float)nativeVec.y    // Native Y -> Unity Z
        );
    }

    // Преобразование Quaternion в pitch/roll/yaw
    private AutoLanding.OrientationD QuaternionToOrientation(Quaternion quat)
    {
        // Unity использует левую систему координат
        // Извлекаем углы Эйлера из Quaternion
        Vector3 euler = quat.eulerAngles;
        
        // Конвертируем градусы в радианы и нормализуем углы
        double pitch = (euler.x > 180f ? euler.x - 360f : euler.x) * Mathf.Deg2Rad;
        double roll = (euler.z > 180f ? euler.z - 360f : euler.z) * Mathf.Deg2Rad;
        double yaw = (euler.y > 180f ? euler.y - 360f : euler.y) * Mathf.Deg2Rad;
        
        return new AutoLanding.OrientationD { pitch = pitch, roll = roll, yaw = yaw };
    }

    // Преобразование pitch/roll/yaw в Quaternion
    private Quaternion OrientationToQuaternion(AutoLanding.OrientationD ori)
    {
        // Конвертируем радианы в градусы
        float pitch = (float)(ori.pitch * Mathf.Rad2Deg);
        float roll = (float)(ori.roll * Mathf.Rad2Deg);
        float yaw = (float)(ori.yaw * Mathf.Rad2Deg);
        
        // Unity использует порядок ZXY для углов Эйлера
        return Quaternion.Euler(pitch, yaw, roll);
    }

    void Start()
    {
        // Если shipTransform не указан, используем текущий объект
        if (shipTransform == null)
        {
            shipTransform = transform;
        }

        // Проверяем наличие всех необходимых объектов
        if (shipTransform == null)
        {
            Debug.LogError("AutoLandingController: Ship Transform не указан!");
            return;
        }

        if (gearPoints == null || gearPoints.Length != 4)
        {
            Debug.LogError("AutoLandingController: Необходимо указать 4 точки шасси!");
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            if (gearPoints[i] == null)
            {
                Debug.LogError($"AutoLandingController: Точка шасси {i} не указана!");
                return;
            }
        }

        if (landingTargetTransform == null)
        {
            Debug.LogError("AutoLandingController: Landing Target Transform не указан!");
            return;
        }

        if (autoStart)
        {
            StartLanding();
        }
    }

    void OnDestroy()
    {
        if (landingSystemHandle != IntPtr.Zero)
        {
            AutoLanding.LandingControlSystemNative.DestroyLandingSystem(landingSystemHandle);
            landingSystemHandle = IntPtr.Zero;
        }
    }

    public void StartLanding()
    {
        if (isLanding)
        {
            Debug.LogWarning("AutoLandingController: Посадка уже начата!");
            return;
        }

        Debug.Log("AutoLandingController: Инициализация системы посадки...");
        InitializeLandingSystem();
        if (isInitialized)
        {
            isLanding = true;
            Debug.Log("AutoLandingController: Посадка начата! Корабль начнет движение.");
        }
        else
        {
            Debug.LogError("AutoLandingController: Не удалось инициализировать систему посадки!");
        }
    }

    public void StopLanding()
    {
        isLanding = false;
        Debug.Log("AutoLandingController: Посадка остановлена!");
    }

    private void InitializeLandingSystem()
    {
        // Проверяем, что DLL доступна
        try
        {
            AutoLanding.ShipParametersD testParams = new AutoLanding.ShipParametersD();
            AutoLanding.ShipStateD testState = new AutoLanding.ShipStateD();
            AutoLanding.LandingTargetD testTarget = new AutoLanding.LandingTargetD();
            
            IntPtr testHandle = AutoLanding.LandingControlSystemNative.CreateLandingSystem(
                ref testParams,
                ref testState,
                ref testTarget);
            if (testHandle != IntPtr.Zero)
            {
                AutoLanding.LandingControlSystemNative.DestroyLandingSystem(testHandle);
            }
        }
        catch (DllNotFoundException)
        {
            Debug.LogError("AutoLandingController: DLL не найдена! Убедитесь, что библиотека скопирована в Assets/Plugins/");
            isInitialized = false;
            return;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AutoLandingController: Предупреждение при проверке DLL: {e.Message}");
        }

        try
        {
            // Получаем текущее состояние корабля из Unity
            Vector3 shipPosition = shipTransform.position;
            Quaternion shipRotation = shipTransform.rotation;
            Rigidbody shipRigidbody = shipTransform.GetComponent<Rigidbody>();

            // Создаем начальное состояние
            AutoLanding.ShipStateD initialState = new AutoLanding.ShipStateD
            {
                position = UnityToNative(shipPosition),
                orientation = QuaternionToOrientation(shipRotation),
                velocity = UnityToNative(shipRigidbody != null ? shipRigidbody.linearVelocity : Vector3.zero),
                acceleration = new AutoLanding.Vector3D { x = 0, y = 0, z = 0 },
                angular_velocity = UnityToNative(shipRigidbody != null ? shipRigidbody.angularVelocity : Vector3.zero),
                angular_acceleration = new AutoLanding.Vector3D { x = 0, y = 0, z = 0 },
                dt = simulationTimeStep,
                step = UIntPtr.Zero
            };

            // Создаем параметры корабля
            AutoLanding.ShipParametersD parameters = new AutoLanding.ShipParametersD
            {
                thrust_positive = UnityToNative(thrustPositive),
                thrust_negative = UnityToNative(thrustNegative),
                attitude_thrust_positive = new AutoLanding.Vector3D { x = 5000, y = 5000, z = 3000 },
                attitude_thrust_negative = new AutoLanding.Vector3D { x = 5000, y = 5000, z = 3000 },
                angular_rate_limit = UnityToNative(angularRateLimit),
                max_pitch = orientationLimits.x,
                max_roll = orientationLimits.y,
                max_yaw = orientationLimits.z,
                gravity = UnityToNative(gravity),
                wind_velocity = UnityToNative(windVelocity),
                mass = mass,
                default_dt = simulationTimeStep,
                gear_points = new AutoLanding.Vector3D[4]
            };

            // Вычисляем локальные координаты шасси относительно центра масс корабля
            for (int i = 0; i < 4; i++)
            {
                Vector3 localPos = shipTransform.InverseTransformPoint(gearPoints[i].position);
                parameters.gear_points[i] = UnityToNative(localPos);
            }

            // Создаем целевую точку посадки
            AutoLanding.LandingTargetD target = new AutoLanding.LandingTargetD
            {
                position = UnityToNative(landingTargetTransform.position),
                orientation = QuaternionToOrientation(landingTargetTransform.rotation),
                velocity = new AutoLanding.Vector3D { x = 0, y = 0, z = 0 },
                acceleration = new AutoLanding.Vector3D { x = 0, y = 0, z = 0 },
                angular_velocity = new AutoLanding.Vector3D { x = 0, y = 0, z = 0 },
                angular_acceleration = new AutoLanding.Vector3D { x = 0, y = 0, z = 0 }
            };

            // Создаем систему управления посадкой
            landingSystemHandle = AutoLanding.LandingControlSystemNative.CreateLandingSystem(
                ref parameters,
                ref initialState,
                ref target);

            if (landingSystemHandle == IntPtr.Zero)
            {
                Debug.LogError("AutoLandingController: Не удалось создать систему управления посадкой!");
                isInitialized = false;
                return;
            }

            isInitialized = true;
            Vector3 startPos = shipTransform.position;
            Vector3 targetPos = landingTargetTransform.position;
            float distance = Vector3.Distance(startPos, targetPos);
            
            // Инициализируем отслеживание позиции
            lastAppliedPosition = startPos;
            lastStepNumber = 0;
            
            Debug.Log($"AutoLandingController: Система управления посадкой инициализирована! Handle: {landingSystemHandle}");
            Debug.Log($"AutoLandingController: Начальная позиция корабля: {startPos}");
            Debug.Log($"AutoLandingController: Целевая позиция: {targetPos}");
            Debug.Log($"AutoLandingController: Расстояние до цели: {distance:F2} м");
            
            if (distance < 0.1f)
            {
                Debug.LogWarning("AutoLandingController: Корабль уже находится очень близко к цели! Возможно, нужно переместить корабль или цель.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"AutoLandingController: Ошибка при инициализации: {e.Message}");
            isInitialized = false;
        }
    }

    private int debugFrameCounter = 0;
    
    void FixedUpdate()
    {
        if (!isInitialized || !isLanding || landingSystemHandle == IntPtr.Zero)
        {
            // Выводим предупреждение только раз в секунду
            if (debugFrameCounter % 50 == 0)
            {
                if (!isInitialized) Debug.LogWarning("AutoLandingController: Система не инициализирована!");
                else if (!isLanding) Debug.LogWarning($"AutoLandingController: Посадка не запущена! (isLanding={isLanding})");
                else if (landingSystemHandle == IntPtr.Zero) Debug.LogError("AutoLandingController: Handle системы равен нулю!");
            }
            debugFrameCounter++;
            return;
        }
        
        debugFrameCounter++;
        
        // Отладочная информация для первых кадров
        if (debugFrameCounter <= 10)
        {
            Debug.Log($"AutoLandingController: FixedUpdate вызван - isInitialized: {isInitialized}, isLanding: {isLanding}, handle: {landingSystemHandle}");
        }

        try
        {
            // ВАЖНО: НЕ вызываем SetLandingTarget каждый кадр, так как это переинициализирует последовательность
            // и сбрасывает индекс шага обратно в 0!
            // Обновляем целевую точку посадки только если она действительно изменилась
            // (это можно сделать позже, если нужно)
            
            // Выполняем один шаг симуляции
            AutoLanding.ShipStateD newState;
            AutoLanding.LandingControlSystemNative.Step(landingSystemHandle, out newState);

            // Применяем новое состояние к объекту в Unity
            Vector3 targetPosition = NativeToUnity(newState.position);
            Quaternion targetRotation = OrientationToQuaternion(newState.orientation);
            Vector3 currentPosition = shipTransform.position;
            
            // Отладочная информация для первых нескольких шагов
            ulong stepNumber = newState.step.ToUInt64();
            
            // Проверяем, действительно ли шаг увеличился
            if (stepNumber == lastStepNumber)
            {
                Debug.LogWarning($"AutoLandingController: Шаг не увеличился! Текущий шаг: {stepNumber}, Последний шаг: {lastStepNumber}");
            }
            
            // Проверяем, изменилась ли позиция в симуляции
            float positionChange = Vector3.Distance(targetPosition, lastAppliedPosition);
            if (stepNumber <= 10)
            {
                Debug.Log($"AutoLandingController: Шаг {stepNumber} - Позиция из симуляции: {targetPosition}, Последняя примененная позиция: {lastAppliedPosition}, Изменение: {positionChange:F4} м");
            }
            
            lastStepNumber = stepNumber;
            
            // Отладочная информация (каждую секунду)
            if (debugFrameCounter % 50 == 0)
            {
                Vector3 newVel = NativeToUnity(newState.velocity);
                float distToTarget = Vector3.Distance(targetPosition, landingTargetTransform.position);
                float posChange = Vector3.Distance(currentPosition, targetPosition);
                Debug.Log($"AutoLandingController: Шаг {stepNumber} - Новая позиция: {targetPosition}, Текущая позиция: {currentPosition}, Изменение: {posChange:F4} м, Скорость: {newVel.magnitude:F2} м/с, Расстояние до цели: {distToTarget:F2} м");
                
                // Проверяем, применилась ли позиция после изменения
                Vector3 actualPos = shipTransform.position;
                float actualChange = Vector3.Distance(actualPos, targetPosition);
                if (actualChange > 0.01f)
                {
                    Debug.LogWarning($"AutoLandingController: Позиция не применилась! Ожидалось: {targetPosition}, Фактически: {actualPos}, Разница: {actualChange:F4} м");
                }
            }
            
            // ВСЕГДА используем прямое изменение transform.position
            // MovePosition может не работать правильно в некоторых случаях
            Vector3 oldPos = shipTransform.position;
            shipTransform.position = targetPosition;
            
            // Применяем ориентацию постепенно через угловую скорость вместо прямого изменения
            // Это позволяет кораблю плавно разворачиваться к цели, а не мгновенно
            Vector3 angularVelocity = NativeToUnity(newState.angular_velocity);
            Vector3 linearVelocity = NativeToUnity(newState.velocity);
            
            // Вычисляем расстояние до цели для плавного разворота
            float distanceToTarget = Vector3.Distance(targetPosition, landingTargetTransform.position);
            float rotationSpeedMultiplier = 1f;
            
            // Чем дальше от цели, тем медленнее разворот (для более плавного движения)
            if (distanceToTarget > smoothRotationStartDistance)
            {
                // На больших расстояниях замедляем разворот
                rotationSpeedMultiplier = Mathf.Clamp01(smoothRotationStartDistance / distanceToTarget);
            }
            
            Rigidbody rb = shipTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Если есть Rigidbody, применяем скорости для физики
                rb.linearVelocity = linearVelocity;
                
                if (useAngularVelocityOnly)
                {
                    // ВАРИАНТ 1: Используем ТОЛЬКО угловую скорость из симуляции
                    // Это позволяет кораблю естественно разворачиваться постепенно
                    // Ограничиваем угловую скорость для более плавного поворота
                    Vector3 limitedAngularVelocity = angularVelocity * rotationSpeedMultiplier;
                    
                    // Дополнительно ограничиваем максимальную скорость поворота
                    float currentAngularSpeed = limitedAngularVelocity.magnitude * Mathf.Rad2Deg;
                    if (currentAngularSpeed > maxRotationSpeed)
                    {
                        limitedAngularVelocity = limitedAngularVelocity.normalized * (maxRotationSpeed * Mathf.Deg2Rad);
                    }
                    
                    rb.angularVelocity = limitedAngularVelocity;
                    
                    // НЕ применяем ориентацию напрямую - пусть физика делает свою работу
                    // Корабль будет поворачиваться естественно через угловую скорость
                }
                else
                {
                    // ВАРИАНТ 2: Применяем ориентацию постепенно с ограничением скорости
                    rb.angularVelocity = angularVelocity;
                    
                    Quaternion currentRot = shipTransform.rotation;
                    
                    // Вычисляем максимальный поворот за кадр с учетом расстояния до цели
                    float rotationSpeed = angularVelocity.magnitude * Mathf.Rad2Deg * rotationSpeedMultiplier;
                    float maxRotationPerFrame = Mathf.Min(rotationSpeed * Time.fixedDeltaTime, maxRotationSpeed * Time.fixedDeltaTime);
                    
                    if (maxRotationPerFrame > 0.1f)
                    {
                        // Поворачиваем постепенно с учетом ограничений
                        shipTransform.rotation = Quaternion.RotateTowards(currentRot, targetRotation, maxRotationPerFrame);
                    }
                    else
                    {
                        // Если угловая скорость очень мала, применяем ориентацию напрямую
                        shipTransform.rotation = targetRotation;
                    }
                }
            }
            else
            {
                // Если нет Rigidbody, применяем ориентацию постепенно
                Quaternion currentRot = shipTransform.rotation;
                
                // Вычисляем максимальный поворот за кадр с учетом расстояния до цели
                float rotationSpeed = angularVelocity.magnitude * Mathf.Rad2Deg * rotationSpeedMultiplier;
                float maxRotationPerFrame = Mathf.Min(rotationSpeed * Time.fixedDeltaTime, maxRotationSpeed * Time.fixedDeltaTime);
                
                if (maxRotationPerFrame > 0.1f)
                {
                    // Поворачиваем постепенно с учетом ограничений
                    shipTransform.rotation = Quaternion.RotateTowards(currentRot, targetRotation, maxRotationPerFrame);
                }
                else
                {
                    // Если угловая скорость очень мала, применяем напрямую
                    shipTransform.rotation = targetRotation;
                }
            }
            
            // Сохраняем примененную позицию для следующего кадра
            lastAppliedPosition = targetPosition;
            
            // Проверяем, действительно ли позиция изменилась
            Vector3 newActualPos = shipTransform.position;
            if (stepNumber <= 10)
            {
                float posDiff = Vector3.Distance(oldPos, newActualPos);
                float expectedDiff = Vector3.Distance(oldPos, targetPosition);
                Debug.Log($"AutoLandingController: Шаг {stepNumber} - Старая позиция: {oldPos}, Новая позиция: {newActualPos}, Ожидалось изменение: {expectedDiff:F4} м, Фактическое изменение: {posDiff:F4} м");
                
                if (Mathf.Abs(expectedDiff - posDiff) > 0.01f)
                {
                    Debug.LogWarning($"AutoLandingController: Позиция не применилась правильно! Разница: {Mathf.Abs(expectedDiff - posDiff):F4} м");
                }
            }
            
            // Скорость уже применена выше при обработке ориентации
            
            // Проверяем, действительно ли позиция изменилась
            if (debugFrameCounter % 50 == 0)
            {
                Vector3 actualPosition = shipTransform.position;
                float actualChange = Vector3.Distance(currentPosition, actualPosition);
                if (actualChange < 0.001f && Vector3.Distance(targetPosition, currentPosition) > 0.1f)
                {
                    Debug.LogWarning($"AutoLandingController: Позиция не изменилась! Ожидалось: {targetPosition}, Получилось: {actualPosition}");
                }
            }

            // Проверяем статус посадки
            currentStatus = AutoLanding.LandingControlSystemNative.GetStatus(landingSystemHandle);
            
            // Отладочная информация для первых нескольких шагов
            if (stepNumber <= 5)
            {
                Debug.Log($"AutoLandingController: Шаг {stepNumber} - Статус: {currentStatus}, isLanding: {isLanding}");
            }
            
            // Не останавливаем посадку слишком рано - проверяем расстояние до цели
            // Используем уже вычисленное расстояние distanceToTarget из строки 527
            
            if (currentStatus == AutoLanding.LandingStatusD.Landed)
            {
                if (distanceToTarget < 1.0f) // Только если действительно близко к цели
                {
                    Debug.Log($"AutoLandingController: Посадка успешно завершена на шаге {stepNumber}!");
                    isLanding = false;
                }
                else
                {
                    // Статус Landed, но мы еще далеко - продолжаем движение
                    if (stepNumber <= 10)
                    {
                        Debug.LogWarning($"AutoLandingController: Статус Landed на шаге {stepNumber}, но расстояние до цели: {distanceToTarget:F2} м. Продолжаем движение.");
                    }
                }
            }
            else if (currentStatus == AutoLanding.LandingStatusD.Crashed)
            {
                Debug.LogWarning($"AutoLandingController: Корабль разбился на шаге {stepNumber}!");
                isLanding = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"AutoLandingController: Ошибка при выполнении шага симуляции: {e.Message}");
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo || !isInitialized)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Box("Auto Landing Debug Info");
        GUILayout.Label($"Status: {currentStatus}");
        GUILayout.Label($"Is Landing: {isLanding}");
        
        if (landingSystemHandle != IntPtr.Zero)
        {
            AutoLanding.ShipStateD state;
            AutoLanding.LandingControlSystemNative.GetCurrentState(landingSystemHandle, out state);
            GUILayout.Label($"Position: ({state.position.x:F2}, {state.position.y:F2}, {state.position.z:F2})");
            GUILayout.Label($"Velocity: ({state.velocity.x:F2}, {state.velocity.y:F2}, {state.velocity.z:F2})");
        }
        
        GUILayout.EndArea();
    }
}

