using System.Text;
using TMPro;
using UnityEngine;

//debug script voor mezelf om eraa uit te kunnen
public class DebugHUD : MonoBehaviour
{
    public TMP_Text text;

    private PlayerLocation player;
    private WaypointManager waypoints;
    private readonly StringBuilder sb = new StringBuilder();

    void Start()
    {
        player = FindFirstObjectByType<PlayerLocation>();
        waypoints = FindFirstObjectByType<WaypointManager>();
    }

    void Update()
    {
        if (text == null || player == null) return;

        sb.Clear();

        if (!player.IsReady)
        {
            sb.Append(player.Error == PlayerLocation.LocationError.None
                ? "Locatie starten...\n\nEven geduld"
                : "Fout: " + player.Error);
        }
        else
        {
            sb.AppendLine(player.IsSimulated ? "<color=#FFB000>[SIMULATIE]</color>" : "<color=#4CFF7A>[GPS]</color>");
            sb.AppendLine($"Lat {player.Latitude:F6}   Lon {player.Longitude:F6}");
            sb.AppendLine($"Nauwkeurigheid ±{player.Accuracy:F0} m   Richting {player.Heading:F0}°");

            if (waypoints != null)
            {
                if (waypoints.IsRouteComplete)
                {
                    sb.AppendLine("\nRoute voltooid!");
                }
                else if (waypoints.Current != null)
                {
                    sb.AppendLine($"\nDoel {waypoints.CurrentIndex + 1}/{waypoints.route.Count}: {waypoints.Current.name}");
                    sb.AppendLine($"Afstand {waypoints.DistanceToCurrent:F0} m   Peiling {waypoints.BearingToCurrent:F0}°");
                    if (waypoints.IsInReachRadius) sb.AppendLine("<color=#4CFF7A>BINNEN BEREIK</color>");
                }
            }
        }

        text.text = sb.ToString();
    }
}
