using System.Collections.Generic;
using UnityEngine;

// Een shop-radio: uiterlijk + perk.
[System.Serializable]
public class RadioDefinition
{
    public string id;
    public string displayName;
    public string description;
    public int price;

    public Color primary;     // hoofdkleur UI
    public Color accent;      // tweede kleur
    public Color background;  // achtergrond menu's
    public float beepFrequency = 880f;

    // Perks
    public float extraSeconds;                 // extra tijd op de klok
    public float rangeMultiplier = 1f;         // signaal vanop grotere afstand opvangen
    public bool showDistance;                  // exacte afstand tonen
    public float dangerPenaltyMultiplier = 1f; // tijdverlies in storingsvelden
    public float partsMultiplier = 1f;         // meer onderdelen als beloning

    public string PerkText
    {
        get
        {
            var perks = new List<string>();
            if (extraSeconds > 0) perks.Add($"+{extraSeconds:F0} s op de klok");
            if (rangeMultiplier > 1f) perks.Add($"+{(rangeMultiplier - 1f) * 100f:F0}% bereik");
            if (showDistance) perks.Add("toont exacte afstand");
            if (dangerPenaltyMultiplier < 1f) perks.Add($"storing kost {(1f - dangerPenaltyMultiplier) * 100f:F0}% minder tijd");
            if (partsMultiplier > 1f) perks.Add($"+{(partsMultiplier - 1f) * 100f:F0}% onderdelen");
            return perks.Count == 0 ? "geen extra's" : string.Join(" | ", perks);
        }
    }
}

// Alle radio's in de shop.
public static class RadioCatalog
{
    public const string DefaultId = "zakradio";
    // Ultiem shop-item: verandert het startscherm.
    public const string LegendId = "legende";

    public static readonly List<RadioDefinition> All = new List<RadioDefinition>
    {
        new RadioDefinition
        {
            id = "zakradio", displayName = "Zakradio", price = 0,
            description = "Je trouwe startradio. Krakerig, maar hij doet wat hij moet doen.",
            primary = Hex("#F2A541"), accent = Hex("#FFD9A0"), background = Hex("#1B1A17"),
            beepFrequency = 880f
        },
        new RadioDefinition
        {
            id = "veldradio", displayName = "Veldradio M-42", price = 150,
            description = "Robuust legerexemplaar met een reservebatterij in de achterklep.",
            primary = Hex("#9BC95F"), accent = Hex("#D8F0B0"), background = Hex("#141A10"),
            beepFrequency = 660f, extraSeconds = 60f
        },
        new RadioDefinition
        {
            id = "scanner", displayName = "Scanner X-9", price = 300,
            description = "Digitale scanner met display. Reikt verder en toont de exacte afstand.",
            primary = Hex("#3FD7F2"), accent = Hex("#B5F3FF"), background = Hex("#0D1820"),
            beepFrequency = 1250f, rangeMultiplier = 1.4f, showDistance = true
        },
        new RadioDefinition
        {
            id = "lood", displayName = "Loden Ontvanger", price = 450,
            description = "Afgeschermd met lood, net zoals Mira's eigen radio.",
            primary = Hex("#E0875F"), accent = Hex("#FFC9AE"), background = Hex("#1F1410"),
            beepFrequency = 740f, dangerPenaltyMultiplier = 0.5f
        },
        new RadioDefinition
        {
            id = "kristal", displayName = "Gouden Kristalradio", price = 800,
            description = "Legendarisch toestel. Van alles een beetje, en meer onderdelen per verhaal.",
            primary = Hex("#FFD34D"), accent = Hex("#FFF1B8"), background = Hex("#1E190A"),
            beepFrequency = 990f, extraSeconds = 30f, rangeMultiplier = 1.2f,
            dangerPenaltyMultiplier = 0.75f, partsMultiplier = 1.5f
        },
        new RadioDefinition
        {
            id = LegendId, displayName = "Station Echo Zender", price = 2500,
            description = "De legendarische zender van Station Echo zelf. Wie deze bezit, heeft alle signalen onder controle. Verandert je startscherm voor altijd.",
            primary = Hex("#FFE27A"), accent = Hex("#FFFFFF"), background = Hex("#1A0F24"),
            beepFrequency = 1100f, extraSeconds = 90f, rangeMultiplier = 1.5f, showDistance = true,
            dangerPenaltyMultiplier = 0.25f, partsMultiplier = 2f
        },
    };

    public static RadioDefinition Get(string id)
    {
        foreach (var r in All)
            if (r.id == id) return r;
        return All[0];
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;
    }
}
