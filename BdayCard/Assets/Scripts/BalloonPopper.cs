using UnityEngine;
using UnityEngine.InputSystem;
 
//detectie van een tik of click op je scherm --> is verbonden met balloon?cs
public class BalloonPopper : MonoBehaviour
{
    [Tooltip("Camera vanwaaruit getikt/geklikt wordt. Leeg = Camera.main.")]
    public Camera referenceCamera;
 
    [Tooltip("Optioneel: beperk de raycast tot bepaalde layers (bv. een 'Balloons'-layer). " +
             "Standaard (Everything) raakt de raycast alles wat een Collider heeft.")]
    public LayerMask hitLayers = ~0;
 
    [Tooltip("Hoe ver de raycast reikt vanaf de camera.")]
    public float maxDistance = 100f;
 
    void Update()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            TryPopAt(Touchscreen.current.primaryTouch.position.ReadValue());
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPopAt(Mouse.current.position.ReadValue());
        }
    }
 
    void TryPopAt(Vector2 screenPos)
    {
        Camera cam = referenceCamera != null ? referenceCamera : Camera.main;
        if (cam == null) return;
 
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitLayers))
        {
            // GetComponentInParent zodat het ook werkt als de Collider op een child-mesh
            // van de ballon zit in plaats van op de ballon-root zelf.
            Balloon balloon = hit.collider.GetComponentInParent<Balloon>();
            if (balloon != null)
                balloon.Pop();
        }
    }
}
