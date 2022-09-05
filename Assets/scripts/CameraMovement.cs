using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// From https://emmaprats.com/p/how-to-rotate-the-camera-around-an-object-in-unity3d/
public class CameraMovement : MonoBehaviour
{
    [SerializeField] private Camera cam;
    // [SerializeField] private Transform target;
    [SerializeField] private float distanceToTarget = 3.0f;

    private Vector3 previousPosition;
    private float scale = 0.1f;

    private float minFov = 10.0f;
    private float maxFov = 100.0f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            previousPosition = cam.ScreenToViewportPoint(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 newPosition = cam.ScreenToViewportPoint(Input.mousePosition);
            Vector3 direction = previousPosition - newPosition;

            float rotationAroundYAxis = -direction.x * 180;         // camera moves horizontally
            float rotationAroundXAxis = direction.y * 180;          // camera moves vertically

            cam.transform.position = new Vector3(0, 0, 0); // look at world origin instead of target.position;

            cam.transform.Rotate(new Vector3(1, 0, 0), rotationAroundXAxis);
            cam.transform.Rotate(new Vector3(0, 1, 0), rotationAroundYAxis, Space.World);           // <— This is what makes it work!

            cam.transform.Translate(new Vector3(0, 0, -distanceToTarget));

            previousPosition = newPosition;
        }

        if (Input.mouseScrollDelta.y != 0)
        {
            float fov = cam.fieldOfView;
            fov += Input.mouseScrollDelta.y * scale;
            fov = Mathf.Clamp(fov, minFov, maxFov);
            cam.fieldOfView = fov;
        }

    }
}
