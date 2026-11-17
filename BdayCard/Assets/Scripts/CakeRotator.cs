using UnityEngine;
using UnityEngine.InputSystem;
 
// taart ronddraaien met vinger of muisclick
public class CakeRotator : MonoBehaviour
{
    [Tooltip("Hoe snel de taart draait relatief aan de sleepbeweging (graden per pixel, ruwweg).")]
    public float rotateSpeed = 0.3f;
 
    [Tooltip("Enkel draaien als de sleepbeweging op het model zelf begint (raycast-check). " +
             "Uitschakelen = draaien werkt overal op het scherm, simpeler maar minder precies.")]
    public bool requireTouchOnModel = false;
 
    Vector2 _lastPointerPos;
    bool _dragging;
 
    void Update()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            HandleTouch();
        else if (Mouse.current != null)
            HandleMouse();
    }
 
    void HandleMouse()
    {
        var mouse = Mouse.current;
 
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 pos = mouse.position.ReadValue();
            if (requireTouchOnModel && !PointerHitsModel(pos))
                return;
            _dragging = true;
            _lastPointerPos = pos;
        }
        else if (mouse.leftButton.wasReleasedThisFrame)
        {
            _dragging = false;
        }
        else if (_dragging && mouse.leftButton.isPressed)
        {
            Vector2 current = mouse.position.ReadValue();
            Rotate(current.x - _lastPointerPos.x);
            _lastPointerPos = current;
        }
    }
 
    void HandleTouch()
    {
        var touch = Touchscreen.current.primaryTouch;
        Vector2 pos = touch.position.ReadValue();
 
        switch (touch.phase.ReadValue())
        {
            case UnityEngine.InputSystem.TouchPhase.Began:
                if (requireTouchOnModel && !PointerHitsModel(pos))
                    return;
                _lastPointerPos = pos;
                _dragging = true;
                break;
            case UnityEngine.InputSystem.TouchPhase.Moved:
                if (_dragging)
                {
                    Rotate(pos.x - _lastPointerPos.x);
                    _lastPointerPos = pos;
                }
                break;
            case UnityEngine.InputSystem.TouchPhase.Ended:
            case UnityEngine.InputSystem.TouchPhase.Canceled:
                _dragging = false;
                break;
        }
    }
 
    bool PointerHitsModel(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return true; // geen camera gevonden, laat gewoon draaien toe
 
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            return hit.transform == transform || hit.transform.IsChildOf(transform);
        return false;
    }
 
    void Rotate(float deltaX)
    {
        transform.Rotate(Vector3.up, -deltaX * rotateSpeed, Space.World);
    }
}
