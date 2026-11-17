using System.Collections.Generic;
using UnityEngine;

public enum Difficulty { Easy, Medium, Hard }

[System.Serializable]
public class Riddle
{
    public string id;
    public string question;
    public string[] options;
    public int correctIndex;

    public Riddle(string question, int correctIndex, params string[] options)
    {
        this.question = question;
        this.correctIndex = correctIndex;
        this.options = options;
    }

    // Kopie met geschudde antwoorden.
    public Riddle Shuffled()
    {
        var order = new List<int>();
        for (int i = 0; i < options.Length; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var newOptions = new string[options.Length];
        int newCorrect = 0;
        for (int i = 0; i < order.Count; i++)
        {
            newOptions[i] = options[order[i]];
            if (order[i] == correctIndex) newCorrect = i;
        }
        return new Riddle(question, newCorrect, newOptions) { id = id };
    }
}

// 30 raadsels per moeilijkheid; antwoord 0 is juist, nieuwe raadsels eerst.
public static class RiddleBank
{
    private static readonly Riddle[] Easy =
    {
        new Riddle("Wat heeft een hals, maar geen hoofd?", 0, "Een fles", "Een paard", "Een slang", "Een uil"),
        new Riddle("Hoe meer je ervan wegneemt, hoe groter het wordt. Wat is het?", 0, "Een gat", "Een berg", "Een schaduw", "Een ballon"),
        new Riddle("Wat gaat altijd omhoog, maar komt nooit naar beneden?", 0, "Je leeftijd", "Een vogel", "Regen", "Een lift"),
        new Riddle("Hoeveel minuten zitten er in 2,5 uur?", 0, "150", "120", "125", "250"),
        new Riddle("Welke kleur krijg je als je blauw en geel mengt?", 0, "Groen", "Paars", "Oranje", "Bruin"),
        new Riddle("Wat heeft een gezicht en twee wijzers, maar geen armen?", 0, "Een klok", "Een kompas", "Een robot", "Een radio"),
        new Riddle("Welk getal komt hierna: 2, 4, 8, 16, ...?", 0, "32", "24", "20", "64"),
        new Riddle("Wat wordt natter naarmate het meer droogt?", 0, "Een handdoek", "Een spons", "Een paraplu", "Zand"),
        new Riddle("Wat kan je vangen, maar nooit gooien?", 0, "Een verkoudheid", "Een bal", "Een vis", "Een frisbee"),
        new Riddle("Wat zendt een radiozender uit?", 0, "Radiogolven", "Geluidsgolven", "Rontgenstralen", "Watergolven"),
        new Riddle("Hoeveel poten hebben 3 spinnen samen?", 0, "24", "18", "32", "21"),
        new Riddle("Wat heeft een bedding en een monding, maar slaapt en praat nooit?", 0, "Een rivier", "Een hotel", "Een vulkaan", "Een grot"),
        new Riddle("Welk dier noemen we de koning van de jungle?", 0, "De leeuw", "De olifant", "De tijger", "De gorilla"),
        new Riddle("Hoeveel dagen telt een schrikkeljaar?", 0, "366", "365", "364", "367"),
        new Riddle("Wat heeft vier poten, maar kan niet lopen?", 0, "Een tafel", "Een hond", "Een kat", "Een paard"),
        new Riddle("Wat heeft ogen, maar kan niet zien?", 0, "Een aardappel", "Een uil", "Een vis", "Een kat"),
        new Riddle("Welke planeet staat het dichtst bij de zon?", 0, "Mercurius", "Venus", "Mars", "De aarde"),
        new Riddle("Hoeveel is 7 x 8?", 0, "56", "54", "48", "64"),
        new Riddle("Wat kan je breken zonder het aan te raken?", 0, "Een belofte", "Een glas", "Een ei", "Een tak"),
        new Riddle("Welke kleur heeft een smaragd?", 0, "Groen", "Rood", "Blauw", "Paars"),
        new Riddle("Wat is de hoofdstad van België?", 0, "Brussel", "Antwerpen", "Gent", "Brugge"),
        new Riddle("Wat heeft een kop en een munt, maar geen lichaam?", 0, "Een muntstuk", "Een slang", "Een vis", "Een hond"),
        new Riddle("Wat wordt kleiner hoe langer het brandt?", 0, "Een kaars", "Een lamp", "De zon", "Een tv"),
        new Riddle("Wat moet je eerst breken voor je het kan gebruiken?", 0, "Een ei", "Een stoel", "Een boek", "Een fiets"),
        new Riddle("Hoeveel seconden zitten er in een minuut?", 0, "60", "100", "30", "90"),
        new Riddle("Welk getal komt hierna: 5, 10, 15, 20, ...?", 0, "25", "30", "22", "40"),
        new Riddle("Welke vogel kan niet vliegen, maar wel heel snel lopen?", 0, "De struisvogel", "De mus", "De zwaluw", "De adelaar"),
        new Riddle("Hoeveel hoeken heeft een driehoek?", 0, "3", "4", "5", "6"),
        new Riddle("In welk seizoen vallen de bladeren van de bomen?", 0, "De herfst", "De lente", "De zomer", "De winter"),
        new Riddle("Wat ligt altijd voor je, maar kan je nooit zien?", 0, "De toekomst", "De lucht", "Je schaduw", "De weg"),
    };

    private static readonly Riddle[] Medium =
    {
        new Riddle("Wat is van jou, maar wordt veel vaker door anderen gebruikt?", 0, "Je naam", "Je gsm", "Je fiets", "Je stem"),
        new Riddle("Een vader en zoon zijn samen 36 jaar. De vader is 30 jaar ouder. Hoe oud is de zoon?", 0, "3", "6", "4", "2"),
        new Riddle("Welk woord wordt altijd fout gespeld?", 0, "'Fout'", "'Altijd'", "'Gespeld'", "'Woord'"),
        new Riddle("Wat breekt al zodra je zijn naam uitspreekt?", 0, "Stilte", "Glas", "Een belofte", "Een record"),
        new Riddle("Welk getal komt hierna: 1, 1, 2, 3, 5, 8, ...?", 0, "13", "11", "12", "16"),
        new Riddle("In welke eenheid druk je de frequentie van een radiozender uit?", 0, "Hertz", "Watt", "Volt", "Decibel"),
        new Riddle("Wat heeft steden maar geen huizen, bossen maar geen bomen en water maar geen vissen?", 0, "Een landkaart", "Een woestijn", "Een droom", "Een schilderij"),
        new Riddle("Als je me hebt, wil je me delen. Als je me deelt, heb je me niet meer. Wat ben ik?", 0, "Een geheim", "Een taart", "Geld", "Een mening"),
        new Riddle("Een slak klimt overdag 3 m in een put van 10 m en zakt 's nachts 2 m terug. Na hoeveel dagen is hij eruit?", 0, "8", "10", "7", "9"),
        new Riddle("Welke maand heeft 28 dagen?", 0, "Alle maanden", "Februari", "Geen enkele", "Enkel in schrikkeljaren"),
        new Riddle("Wat heeft veel tanden, maar bijt nooit?", 0, "Een kam", "Een haai", "Een hond", "Een krokodil"),
        new Riddle("Hoe ziet SOS eruit in morsecode?", 0, "... --- ...", "-.- .-. ...", "--- ... ---", ".-. .-. .-."),
        new Riddle("Hoe vaak kan je 10 aftrekken van 100?", 0, "Een keer, daarna is het 90", "Tien keer", "Negen keer", "Oneindig veel"),
        new Riddle("Een boer heeft 17 schapen. Alle schapen behalve 9 gaan dood. Hoeveel blijven er over?", 0, "9", "8", "17", "0"),
        new Riddle("Wat heeft een hoofdeinde, een voeteneinde en vier poten?", 0, "Een bed", "Een tafel", "Een hond", "Een stoel"),
        new Riddle("Hoeveel graden telt een volledige cirkel?", 0, "360", "180", "100", "400"),
        new Riddle("Welk metaal is vloeibaar bij kamertemperatuur?", 0, "Kwik", "Lood", "Tin", "Aluminium"),
        new Riddle("Wie wordt vaak 'de vader van de radio' genoemd?", 0, "Guglielmo Marconi", "Thomas Edison", "Alexander Graham Bell", "Isaac Newton"),
        new Riddle("Hoe snel reist een radiogolf ongeveer?", 0, "300 000 km per seconde", "340 meter per seconde", "30 000 km per seconde", "3 000 km per seconde"),
        new Riddle("Welk woord van vier letters wordt korter als je er twee letters aan toevoegt?", 0, "Kort", "Lang", "Klein", "Smal"),
        new Riddle("Lisa's moeder heeft vijf kinderen: Nana, Nene, Nini, Nono en ...?", 0, "Lisa", "Nunu", "Nina", "Nanu"),
        new Riddle("Wat is het kleinste priemgetal?", 0, "2", "1", "3", "0"),
        new Riddle("Hoeveel uur zit er in een week?", 0, "168", "144", "172", "120"),
        new Riddle("Je komt met een lucifer in een koude, donkere kamer met een kaars, een kachel en een olielamp. Wat steek je eerst aan?", 0, "De lucifer", "De kaars", "De kachel", "De olielamp"),
        new Riddle("Welke letter komt hierna: E, T, D, V, V, Z, Z, A, ...?", 0, "N", "T", "D", "E"),
        new Riddle("Wat is de chemische formule van water?", 0, "H2O", "CO2", "O2", "NaCl"),
        new Riddle("Een vliegtuig stort neer precies op de grens van België en Nederland. Waar begraaf je de overlevenden?", 0, "Nergens", "In België", "In Nederland", "Op de grens"),
        new Riddle("Hoeveel vlakken heeft een kubus?", 0, "6", "8", "12", "4"),
        new Riddle("Welk getal komt hierna: 3, 6, 12, 24, ...?", 0, "48", "36", "30", "42"),
        new Riddle("Hoeveel bits zitten er in een byte?", 0, "8", "4", "16", "10"),
    };

    private static readonly Riddle[] Hard =
    {
        new Riddle("Ik spreek zonder mond en hoor zonder oren. Ik heb geen lichaam, maar de bergen geven me leven. Wat ben ik?", 0, "Een echo", "Een radio", "Een spook", "De wind"),
        new Riddle("9 identieke munten, 1 ervan is zwaarder. Hoe vaak moet je minimaal wegen met een balansweegschaal om zeker te zijn?", 0, "2", "3", "4", "1"),
        new Riddle("Hoe meer je ervan hebt, hoe minder je ziet. Wat is het?", 0, "Duisternis", "Licht", "Brillen", "Ogen"),
        new Riddle("Een man zegt over een foto: 'Ik heb geen broers of zussen, maar de vader van die man is de zoon van mijn vader.' Wie staat op de foto?", 0, "Zijn zoon", "Hijzelf", "Zijn vader", "Zijn neef"),
        new Riddle("Welk getal komt hierna: 1, 11, 21, 1211, 111221, ...?", 0, "312211", "112221", "1112221", "122211"),
        new Riddle("Wat komt een keer voor in een minuut, twee keer in een moment, maar nooit in duizend jaar?", 0, "De letter M", "Een seconde", "Een gedachte", "De letter N"),
        new Riddle("Een radiozender zendt uit op 100 MHz. Hoe lang is de golflengte ongeveer?", 0, "3 meter", "30 cm", "30 meter", "300 meter"),
        new Riddle("Je doet mee aan een race en haalt de persoon op plaats twee in. Op welke plaats sta je nu?", 0, "Tweede", "Eerste", "Derde", "Dat hangt ervan af"),
        new Riddle("Wat kan de hele wereld rondreizen terwijl het in een hoekje blijft?", 0, "Een postzegel", "Een spin", "Een satelliet", "Een radiogolf"),
        new Riddle("Een bat en een bal kosten samen 1,10 euro. De bat kost 1 euro meer dan de bal. Wat kost de bal?", 0, "5 cent", "10 cent", "1 euro", "15 cent"),
        new Riddle("Wat heeft vier vingers en een duim, maar leeft niet?", 0, "Een handschoen", "Een hand", "Een standbeeld", "Een robotarm"),
        new Riddle("Welke letter komt hierna: J, F, M, A, M, J, J, A, ...?", 0, "S", "O", "N", "D"),
        new Riddle("Welk getal komt hierna: 2, 3, 5, 7, 11, 13, ...?", 0, "17", "15", "19", "16"),
        new Riddle("Hoeveel driehoeken tel je in een vierkant waarin beide diagonalen getekend zijn?", 0, "8", "4", "6", "10"),
        new Riddle("5 machines maken 5 producten in 5 minuten. Hoe lang doen 100 machines over 100 producten?", 0, "5 minuten", "100 minuten", "20 minuten", "50 minuten"),
        new Riddle("Een waterlelie verdubbelt elke dag. Na 48 dagen bedekt ze het hele meer. Na hoeveel dagen was de helft bedekt?", 0, "47", "24", "46", "36"),
        new Riddle("Hoe vaak liggen de uurwijzer en de minutenwijzer precies over elkaar in 12 uur?", 0, "11 keer", "12 keer", "24 keer", "10 keer"),
        new Riddle("Welke letter is in morsecode een enkele punt?", 0, "E", "T", "A", "S"),
        new Riddle("Een kamer heeft 4 hoeken. In elke hoek zit een kat en tegenover elke kat zitten 3 katten. Hoeveel katten zijn er?", 0, "4", "12", "16", "3"),
        new Riddle("Wat is de helft van 2/3 van 3/4 van 4/5 van 100?", 0, "20", "25", "10", "40"),
        new Riddle("Welk getal ontbreekt: 8, 27, 64, ?, 216", 0, "125", "100", "128", "150"),
        new Riddle("In een donkere lade liggen 10 zwarte en 10 witte sokken. Hoeveel moet je er minstens blind nemen voor een zeker paar?", 0, "3", "2", "11", "20"),
        new Riddle("Twee vaders en twee zonen vangen elk een vis, maar ze hebben samen maar 3 vissen. Hoe kan dat?", 0, "Het zijn opa, vader en zoon", "Een vis ontsnapte", "Ze deelden een vis", "Een van hen ving niets"),
        new Riddle("Welk hemellichaam past niet in de rij: Mercurius, Venus, Pluto, Mars?", 0, "Pluto", "Venus", "Mars", "Mercurius"),
        new Riddle("Hoe schrijf je het getal 10 in binaire code?", 0, "1010", "1100", "1001", "0110"),
        new Riddle("Hoe vaak komt het cijfer 9 voor als je alle getallen van 1 tot 100 opschrijft?", 0, "20", "10", "19", "11"),
        new Riddle("Een trein van 1 km lang rijdt 60 km/u door een tunnel van 1 km. Hoe lang duurt het tot hij er volledig door is?", 0, "2 minuten", "1 minuut", "3 minuten", "30 seconden"),
        new Riddle("Een getal van drie cijfers: de tientallen zijn 5 meer dan de eenheden, de honderdtallen 8 minder dan de tientallen. Welk getal?", 0, "194", "183", "295", "149"),
        new Riddle("Twee touwen branden elk precies een uur, maar ongelijkmatig. Hoe meet je 45 minuten?", 0, "Touw 1 aan beide kanten + touw 2 aan een kant aansteken, en als touw 1 op is ook de 2e kant van touw 2", "Beide touwen in het midden doorknippen", "Touw 1 aan een kant aansteken en dan touw 2", "Dat kan niet"),
        new Riddle("Hoeveel keer per dag staan de wijzers van een klok in een rechte hoek (90 graden)?", 0, "44", "24", "48", "22"),
    };

    private const string SeenKey = "seenRiddles";

    private static Riddle[] Source(Difficulty d) => d == Difficulty.Easy ? Easy : d == Difficulty.Medium ? Medium : Hard;
    private static string Prefix(Difficulty d) => d == Difficulty.Easy ? "E" : d == Difficulty.Medium ? "M" : "H";

    // Kies raadsels voor een verhaal: thema-raadsels + algemene.
    public static List<Riddle> Pick(Difficulty difficulty, int count, string storyId)
    {
        var themed = new List<Riddle>(StoryRiddles.For(storyId, difficulty));
        Shuffle(themed);
        int themedCount = Mathf.Min(themed.Count, Mathf.Max(1, count / 3));

        List<Riddle> result = Pick(difficulty, count - themedCount);
        for (int i = 0; i < themedCount; i++)
            result.Insert(Random.Range(0, result.Count + 1), themed[i].Shuffled());
        return result;
    }

    // Kies algemene raadsels, ongeziene eerst.
    public static List<Riddle> Pick(Difficulty difficulty, int count)
    {
        if (count <= 0) return new List<Riddle>();
        Riddle[] source = Source(difficulty);
        string prefix = Prefix(difficulty);
        for (int i = 0; i < source.Length; i++) source[i].id = prefix + i;

        var seen = new HashSet<string>(PlayerPrefs.GetString(SeenKey, "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        var unseen = new List<Riddle>();
        var already = new List<Riddle>();
        foreach (var r in source) (seen.Contains(r.id) ? already : unseen).Add(r);
        Shuffle(unseen);
        Shuffle(already);

        var pool = new List<Riddle>(unseen);
        pool.AddRange(already);

        var result = new List<Riddle>();
        for (int i = 0; i < count; i++)
        {
            Riddle r = pool[i % pool.Count];
            result.Add(r.Shuffled());
            seen.Add(r.id);
        }

        // Alles van dit niveau gezien? Dan opnieuw beginnen (behalve wat we nu net kozen).
        bool allSeen = true;
        foreach (var r in source) if (!seen.Contains(r.id)) { allSeen = false; break; }
        if (allSeen)
        {
            seen.RemoveWhere(id => id.StartsWith(prefix));
            foreach (var r in result) seen.Add(r.id);
        }

        PlayerPrefs.SetString(SeenKey, string.Join(",", seen));
        PlayerPrefs.Save();
        return result;
    }

    public static int Count(Difficulty d) => Source(d).Length;

    private static void Shuffle(List<Riddle> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
