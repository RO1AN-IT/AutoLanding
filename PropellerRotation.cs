using System;
using UnityEngine;

namespace AutoLanding
{
    public class PropellerRotation : MonoBehaviour
    {

        [Header("Лопасти")]
        public Transform[] propellers;

        [Header("Вентили")]
        public Transform[] fans;
        
        [Header("Параметры вращения вентилей")]
        [Tooltip("Скорость вращения вентилей (градусов/сек)")]
        public float fanRotationSpeed = 500f;
        
        [Tooltip("Множитель скорости вентилей относительно скорости корабля")]
        public float fanSpeedMultiplier = 0.5f;
        
        [Tooltip("Максимальная скорость корабля для расчета вращения")]
        public float maxShipSpeed = 100f;
        
        [Header("Параметры возврата лопастей при приближении")]
        [Tooltip("Расстояние до цели, при котором лопасти начинают возвращаться к 0 (метры)")]
        public float returnToZeroDistance = 50f;
        
        [Tooltip("Скорость возврата лопастей к 0 (градусов/сек)")]
        public float returnToZeroSpeed = 20f;
        
        private AutoLandingController landingController;
        private Vector3 previousPosition;
        private bool isFirstFrame = true;
        
        void Start()
        {
            landingController = GetComponentInParent<AutoLandingController>();
            if (landingController == null)
            {
                landingController = FindObjectOfType<AutoLandingController>();
            }
        }
        
        void Update()
        {

            if (landingController == null) return;
            
            bool isFlying = landingController.IsLanding();
            LandingStatus status = landingController.GetLandingStatus();
            bool isLanded = (status == LandingStatus.Landed);

            if (isFlying || !isLanded)
            {
            if (propellers != null)
            {
                float maxAngle = 30f;
                float rotationSpeed = 10f;
                
                ShipState currentState = landingController.GetCurrentState();
                Vector3 currentPos = currentState.pose.position.ToUnityVector3();
                
                Vector3 targetPos;
                if (landingController.landingTargetTransform != null)
                {
                    targetPos = landingController.landingTargetTransform.position;
                }
                else
                {
                    targetPos = landingController.landingTargetPosition;
                }
                
                float distanceToTarget = Vector3.Distance(currentPos, targetPos);
                
                bool shouldReturnToZero = distanceToTarget < returnToZeroDistance;
                
                Vector3 currentPosition = landingController.transform.position;
                
                if (isFirstFrame)
                {
                    previousPosition = currentPosition;
                    isFirstFrame = false;
                }
                
                if (shouldReturnToZero)
                {
                    for(int i = 0; i < propellers.Length; i++)
                    {
                        if (propellers[i] == null) continue;
                        
                        float currentAngle = NormalizeAngle(propellers[i].localEulerAngles.x);
                        
                        if (Mathf.Abs(currentAngle) > 0.1f)
                        {
                            float targetAngle = 0f;
                            float newAngle;
                            
                            if (currentAngle > 0)
                            {
                                newAngle = currentAngle - returnToZeroSpeed * Time.deltaTime;
                                if (newAngle < 0) newAngle = 0;
                            }
                            else
                            {
                                newAngle = currentAngle + returnToZeroSpeed * Time.deltaTime;
                                if (newAngle > 0) newAngle = 0;
                            }
                            
                            propellers[i].localRotation = Quaternion.Euler(newAngle, propellers[i].localEulerAngles.y, propellers[i].localEulerAngles.z);
                        }
                    }
                }
                else
                {
                    Vector3 positionDelta = currentPosition - previousPosition;
                    float movementDistance = positionDelta.magnitude;
                    
                    if (movementDistance > 0.01f)
                    {
                        Transform shipTransform = landingController.transform;
                        Vector3 localMovement = shipTransform.InverseTransformDirection(positionDelta);
                        float forwardMovement = localMovement.z;
                        
                        rotationSpeed = Mathf.Abs(forwardMovement) * 30f;
                        
                        if (forwardMovement > 0)
                    {
                        for(int i = 0; i < propellers.Length; i++)
                        {
                            if (propellers[i] == null) continue;
                            
                            float currentAngle = NormalizeAngle(propellers[i].localEulerAngles.x);
                            
                            if ((i == 0 || i == 1) && currentAngle < maxAngle)
                            {
                                float newAngle = currentAngle + rotationSpeed * Time.deltaTime;
                                if (newAngle > maxAngle) newAngle = maxAngle;
                                propellers[i].localRotation = Quaternion.Euler(newAngle, propellers[i].localEulerAngles.y, propellers[i].localEulerAngles.z);
                            }
                            else if ((i == 2 || i == 3) && currentAngle > -maxAngle)
                            {
                                float newAngle = currentAngle - rotationSpeed * Time.deltaTime;
                                if (newAngle < -maxAngle) newAngle = -maxAngle;
                                propellers[i].localRotation = Quaternion.Euler(newAngle, propellers[i].localEulerAngles.y, propellers[i].localEulerAngles.z);
                            }
                        }
                    }
                    else if (forwardMovement < 0)
                    {
                        for(int i = 0; i < propellers.Length; i++)
                        {
                            if (propellers[i] == null) continue;
                            
                            float currentAngle = NormalizeAngle(propellers[i].localEulerAngles.x);
                            
                            if ((i == 0 || i == 1) && currentAngle > -maxAngle)
                            {
                                float newAngle = currentAngle - rotationSpeed * Time.deltaTime;
                                if (newAngle < -maxAngle) newAngle = -maxAngle;
                                propellers[i].localRotation = Quaternion.Euler(newAngle, propellers[i].localEulerAngles.y, propellers[i].localEulerAngles.z);
                            }
                            else if ((i == 2 || i == 3) && currentAngle < maxAngle)
                            {
                                float newAngle = currentAngle + rotationSpeed * Time.deltaTime;
                                if (newAngle > maxAngle) newAngle = maxAngle;
                                propellers[i].localRotation = Quaternion.Euler(newAngle, propellers[i].localEulerAngles.y, propellers[i].localEulerAngles.z);
                            }
                        }
                    }
                }
                }
                
                previousPosition = currentPosition;
            }
            }
            else
            {
                foreach (var propeller in propellers)
                {
                    if (propeller != null)
                    {
                        propeller.Rotate(0, 0, 0, Space.Self);
                    }
                }
            }



            
            
            if (!isFlying || isLanded)
            {
                StopRotation();
                return;
            }
            
            ShipState state = landingController.GetCurrentState();
            Vector3 shipVelocity = state.motion.velocity.ToUnityVector3();
            float shipSpeed = shipVelocity.magnitude;
            
            if (fans != null)
            {
                float fanSpeed = (fanRotationSpeed + shipSpeed * fanSpeedMultiplier) * Time.deltaTime;
                foreach (var fan in fans)
                {
                    if (fan == null) continue;
                    fan.Rotate(0, fanSpeed, 0, Space.Self);
                }
            }
        }
        
        private void StopRotation()
        {
            if (fans != null)
            {
                foreach (var fan in fans)
                {
                    if (fan != null)
                    {
                        fan.Rotate(0, 0, 0, Space.Self);
                    }
                }
            }
        }
        
        private float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
        
    }
}
