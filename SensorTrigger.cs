using UnityEngine;

public class SensorTrigger : MonoBehaviour
{
    public AutoLandingController controller;
    public string sensorDirection;
    
    void OnTriggerEnter(Collider other)
    {
        if (controller != null)
        {
            controller.OnSensorTriggerEnter(sensorDirection, other);
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (controller != null)
        {
            controller.OnSensorTriggerExit(sensorDirection, other);
        }
    }
    
    void OnTriggerStay(Collider other)
    {
        if (controller != null)
        {
            controller.OnSensorTriggerStay(sensorDirection, other);
        }
    }
}
