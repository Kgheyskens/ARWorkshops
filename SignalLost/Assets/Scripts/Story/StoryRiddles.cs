using System.Collections.Generic;
using UnityEngine;

// Verhaalgebonden inleidingen en thema-raadsels per moeilijkheid.
public static class StoryRiddles
{
    private class Set
    {
        public string[] intros;
        public Riddle[] easy, medium, hard;
    }

    private static readonly Dictionary<string, Set> sets = new Dictionary<string, Set>
    {
        ["echo"] = new Set
        {
            intros = new[] { "Mira's beveiligingscode:", "Station Echo vraagt om verificatie:", "Een vervormde stem vraagt:" },
            easy = new[]
            {
                new Riddle("Mira's ontvanger heeft een knop 'FM'. Waarvoor staat de M?", 0, "Modulatie", "Muziek", "Microfoon", "Megahertz"),
                new Riddle("Station Echo is genoemd naar een echo. Wat is een echo?", 0, "Weerkaatst geluid", "Een soort antenne", "Een vogel", "Een lichtflits"),
            },
            medium = new[]
            {
                new Riddle("Waardoor reist geluid sneller?", 0, "Door water", "Door lucht", "Door een vacuüm", "Overal even snel"),
                new Riddle("Mira's signaal staat op nul hertz. Hoeveel trillingen per seconde is dat?", 0, "Geen enkele", "Een", "Duizend", "Oneindig veel"),
            },
            hard = new[]
            {
                new Riddle("Je roept naar een rotswand en hoort de echo na 2 seconden. Geluid gaat ±340 m/s. Hoe ver is de wand?", 0, "340 meter", "680 meter", "170 meter", "1 kilometer"),
                new Riddle("Welke toon kan een volwassen mens NIET horen?", 0, "50 000 Hz", "440 Hz", "1 000 Hz", "10 000 Hz"),
            },
        },
        ["nachtegaal"] = new Set
        {
            intros = new[] { "Rik's quizvraag van de nacht:", "Een luisteraar belt in met een vraag:", "Op de tape staat een vraag:" },
            easy = new[]
            {
                new Riddle("Vanwaar zond Radio Nachtegaal uit?", 0, "Vanop een schip", "Vanuit een vliegtuig", "Vanuit een kerktoren", "Vanuit een trein"),
                new Riddle("Hoeveel snaren heeft een gewone gitaar?", 0, "6", "4", "5", "8"),
            },
            medium = new[]
            {
                new Riddle("Waarop werd muziek in 1967 vooral verkocht?", 0, "Vinylplaten", "Cd's", "Mp3's", "Streaming"),
                new Riddle("Een single draait op '45 toeren'. Wat betekent dat?", 0, "45 omwentelingen per minuut", "45 omwentelingen per seconde", "45 liedjes per plaat", "45 minuten muziek"),
            },
            hard = new[]
            {
                new Riddle("De noot A trilt op 440 Hz. Hoe snel trilt de A een octaaf hoger?", 0, "880 Hz", "660 Hz", "220 Hz", "1760 Hz"),
                new Riddle("Hoeveel tellen heeft een maat in een wals (3/4-maat)?", 0, "3", "4", "2", "6"),
            },
        },
        ["zilvervos"] = new Set
        {
            intros = new[] { "Het codeboek vraagt:", "Beveiligingsvraag van de thuisbasis:", "Zilvervos heeft het bericht beveiligd:" },
            easy = new[]
            {
                new Riddle("Waarom gebruikt een geheim agent een codenaam?", 0, "Om de echte naam geheim te houden", "Om sneller te typen", "Omdat het verplicht is", "Om indruk te maken"),
                new Riddle("Elke letter is een plaats opgeschoven (B betekent A). Wat betekent 'WPT'?", 0, "VOS", "UNR", "XQU", "VOT"),
            },
            medium = new[]
            {
                new Riddle("Caesar-code: schuif elke letter 3 plaatsen terug. Wat betekent 'DJHQW'?", 0, "AGENT", "AGENS", "DOELT", "BLIND"),
                new Riddle("Welke muur viel in 1989 als symbool van het einde van de Koude Oorlog?", 0, "De Berlijnse Muur", "De Chinese Muur", "De Klaagmuur", "De Muur van Hadrianus"),
            },
            hard = new[]
            {
                new Riddle("In het NAVO-spellingsalfabet is A 'Alfa' en B 'Bravo'. Wat is Z?", 0, "Zulu", "Zebra", "Zeta", "Zorro"),
                new Riddle("Code: A=1, B=2, ... Z=26. Welk woord is 6-15-24?", 0, "FOX", "FIX", "DOG", "BOX"),
            },
        },
        ["vesta"] = new Set
        {
            intros = new[] { "LUNA> verificatie vereist:", "LUNA> beantwoord om het pakket te ontgrendelen:", "LUNA> testvraag voor menselijke operator:" },
            easy = new[]
            {
                new Riddle("Welke planeet noemen we 'de rode planeet'?", 0, "Mars", "Venus", "Jupiter", "Saturnus"),
                new Riddle("Wat is de grootste planeet van ons zonnestelsel?", 0, "Jupiter", "Saturnus", "De aarde", "Neptunus"),
            },
            medium = new[]
            {
                new Riddle("Hoe lang doet licht van de zon erover om de aarde te bereiken?", 0, "Ongeveer 8 minuten", "Ongeveer 8 seconden", "Ongeveer 8 uur", "Het is er meteen"),
                new Riddle("LUNA vond priemgetallen. Welk getal is GEEN priemgetal?", 0, "21", "13", "17", "19"),
            },
            hard = new[]
            {
                new Riddle("Waarom hoor je in de ruimte geen ontploffingen?", 0, "Er is geen lucht om geluid te dragen", "Het is er te koud", "Het is er te donker", "Geluid gaat er te snel"),
                new Riddle("LUNA rekent binair. Wat is 1111 in ons gewone talstelsel?", 0, "15", "16", "14", "11"),
            },
        },
        ["nachtuil"] = new Set
        {
            intros = new[] { "De Nachtuil fluistert:", "Een verzoekje uit het niets:", "De studioklok tikt. Een stem vraagt:" },
            easy = new[]
            {
                new Riddle("Hoe noem je een dier dat vooral 's nachts actief is?", 0, "Een nachtdier", "Een winterslaper", "Een trekvogel", "Een roofvogel"),
                new Riddle("De Nachtuil begint om 3 uur 's nachts en zendt een uur uit. Hoe laat stopt ze?", 0, "4 uur", "2 uur", "3 uur", "5 uur"),
            },
            medium = new[]
            {
                new Riddle("Hoe ver kan een uil zijn kop ongeveer draaien?", 0, "270 graden", "90 graden", "180 graden", "360 graden"),
                new Riddle("De plaat speelt achterstevoren. Welk woord blijft hetzelfde als je het achterstevoren leest?", 0, "Lepel", "Lamp", "Radio", "Nacht"),
            },
            hard = new[]
            {
                new Riddle("De Nachtuil verdween 30 jaar geleden. Als het nu 2026 is, in welk jaar was dat?", 0, "1996", "1986", "2006", "1994"),
                new Riddle("Welke klok toont twee keer per dag exact de juiste tijd?", 0, "Een stilgevallen klok", "Een digitale klok", "Een klok die 1 minuut voorloopt", "Een zonnewijzer"),
            },
        },
        ["noorderlicht"] = new Set
        {
            intros = new[] { "Kapitein Vandamme test haar bemanning:", "Het logboek is versleuteld met een vraag:", "De stuurman vraagt:" },
            easy = new[]
            {
                new Riddle("Welk dier leeft op de Noordpool?", 0, "De ijsbeer", "De pinguïn", "De kangoeroe", "De leeuw"),
                new Riddle("Uit welke stad vertrok de Noorderlicht?", 0, "Antwerpen", "Oostende", "Rotterdam", "Brugge"),
            },
            medium = new[]
            {
                new Riddle("Wat veroorzaakt het noorderlicht?", 0, "Deeltjes van de zon die botsen met de atmosfeer", "Weerkaatsing van het ijs", "Bliksem", "Maanlicht op de wolken"),
                new Riddle("Bij hoeveel graden Celsius bevriest zuiver water?", 0, "0", "-10", "4", "-4"),
            },
            hard = new[]
            {
                new Riddle("Hoeveel procent van een ijsberg zit ongeveer onder water?", 0, "Ongeveer 90%", "Ongeveer 50%", "Ongeveer 30%", "Ongeveer 10%"),
                new Riddle("Hoeveel volledige weken zitten er tussen dag 1 en dag 97 van de reis?", 0, "13", "14", "12", "97"),
            },
        },
    };

    // Inleidende zin boven het raadsel.
    public static string RandomIntro(string storyId)
    {
        if (storyId == null || !sets.TryGetValue(storyId, out Set set) || set.intros.Length == 0)
            return "Ontcijfer de code:";
        return set.intros[Random.Range(0, set.intros.Length)];
    }

    // Thema-raadsels van een verhaal per moeilijkheid.
    public static Riddle[] For(string storyId, Difficulty difficulty)
    {
        if (storyId == null || !sets.TryGetValue(storyId, out Set set)) return new Riddle[0];
        Riddle[] list = difficulty == Difficulty.Easy ? set.easy : difficulty == Difficulty.Medium ? set.medium : set.hard;
        for (int i = 0; i < list.Length; i++) list[i].id = storyId + "_" + difficulty + i;
        return list;
    }
}
