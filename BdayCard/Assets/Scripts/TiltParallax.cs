using UnityEngine;
using UnityEngine.InputSystem;

//Paralax voor extra functie, dus de balonenn draaien licht mee met hoe je je telefoon kantelt.
public class TiltParallax : MonoBehaviour
{
    [Tooltip("Hoe ver dit object maximaal verschuift (in lokale eenheden) bij een volledige kanteling.")]
    public Vector2 maxOffset = new Vector2(0.05f, 0.03f);

    [Tooltip("Hoeveel graden dit object maximaal mee kantelt.")]
    public float maxTiltDegrees = 6f;

    [Tooltip("Hoe snel het object de kanteling volgt. Hoger = directer/stijver, lager = vloeiender/logger.")]
    public float smoothSpeed = 4f;

    Vector3 _baseLocalPosition;
    Quaternion _baseLocalRotation;

    void OnEnable()
    {
        // Sensoren staan standaard uit (batterij-besparing); zonder dit blijft de accelerometer
        // op een oude/nul-waarde staan.
        if (Accelerometer.current != null)
            InputSystem.EnableDevice(Accelerometer.current);
    }

    void OnDisable()
    {
        if (Accelerometer.current != null)
            InputSystem.DisableDevice(Accelerometer.current);
    }

    void Start()
    {
        _baseLocalPosition = transform.localPosition;
        _baseLocalRotation = transform.localRotation;
    }

    void Update()
    {
        Vector2 tilt = GetTiltInput();

        Vector3 targetOffset = new Vector3(tilt.x * maxOffset.x, tilt.y * maxOffset.y, 0f);
        Quaternion targetTilt = Quaternion.Euler(-tilt.y * maxTiltDegrees, tilt.x * maxTiltDegrees, 0f);

        Vector3 targetPosition = _baseLocalPosition + targetOffset;
        Quaternion targetRotation = _baseLocalRotation * targetTilt;

        float f = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime); // vloeiende, framerate-onafhankelijke interpolatie
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, f);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, f);
    }

    /// <summary>Geeft een genormaliseerde (ruwweg -1..1) kanteling terug: echte accelerometer op
    /// een toestel, of de muispositie als test-vervanger wanneer er geen accelerometer is.</summary>
    Vector2 GetTiltInput()
    {
        if (Accelerometer.current != null)
        {
            Vector3 acc = Accelerometer.current.acceleration.ReadValue();
            return new Vector2(Mathf.Clamp(acc.x, -1f, 1f), Mathf.Clamp(acc.y, -1f, 1f));
        }

        if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            return new Vector2(
                Mathf.Clamp((mousePos.x / Screen.width) * 2f - 1f, -1f, 1f),
                Mathf.Clamp((mousePos.y / Screen.height) * 2f - 1f, -1f, 1f)
            );
        }

        return Vector2.zero;
    }
}