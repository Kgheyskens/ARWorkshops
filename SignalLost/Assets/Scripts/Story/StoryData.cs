using System.Collections.Generic;
using UnityEngine;

// Een verhaal: intro, 10 fragmenten, winst- en verlieseinde.
public class StoryDefinition
{
    public string id;
    public string title;
    public string subtitle;
    public string fragmentLabel;   // bv. "FRAGMENT", "TAPE", "LOGBOEK"
    public string winTitle;
    public string loseTitle;
    public string intro;
    public string[] fragments;     // altijd 10; 1, 4, 7 en 10 vertellen samen de kern
    public string finale;
    public string failure;

    // Kies 4, 6, 8 of 10 fragmenten zodat het verhaal klopt.
    public List<string> GetFragments(int count)
    {
        int[] indices;
        switch (count)
        {
            case 4: indices = new[] { 0, 3, 6, 9 }; break;
            case 6: indices = new[] { 0, 1, 3, 6, 8, 9 }; break;
            case 8: indices = new[] { 0, 1, 2, 3, 5, 6, 8, 9 }; break;
            default: indices = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }; break;
        }
        var list = new List<string>();
        for (int i = 0; i < count; i++) list.Add(fragments[indices[i % indices.Length]]);
        return list;
    }
}

// Alle verhalen van Signaal Kwijt.
public static class StoryData
{
    public static readonly List<StoryDefinition> All = new List<StoryDefinition>
    {
        // ------------------------------------------------------------------ 1
        new StoryDefinition
        {
            id = "echo", title = "Station Echo", subtitle = "Radio-operator Mira Vos is spoorloos",
            fragmentLabel = "FRAGMENT", winTitle = "SIGNAAL\nHERSTELD", loseTitle = "SIGNAAL\nVERLOREN",
            intro =
                "Drie nachten geleden verdween radio-operator Mira Vos van Station Echo.\n\n" +
                "Haar laatste noodsignaal is in stukken gebroken. De fragmenten zweven rond tussen zendmasten in jouw buurt.\n\n" +
                "Volg het signaal met je radio en los bij elke mast de code op, voordat de tijd op is.",
            fragments = new[]
            {
                "...hier Mira Vos, Station Echo... als iemand dit hoort: ik heb iets opgevangen dat er niet hoort te zijn...",
                "...een signaal op nul hertz. Onmogelijk. En toch staat de naald diep in het rood...",
                "...de storingen verplaatsen zich. Ze volgen mij. Ik heb mijn ontvanger afgeschermd met lood...",
                "...het signaal komt niet uit de lucht. Het komt van onder de grond. Ik ga kijken...",
                "...ik heb de bron gevonden: een oude zender, ouder dan de stad zelf...",
                "...hij zendt een melodie uit. Drie tonen. Steeds opnieuw. Steeds dichterbij...",
                "...de zender stuurt geen bericht. Hij roept. En nu antwoordt er iets...",
                "...mijn eigen stem komt terug uit de luidspreker, vertraagd, alsof ik vastzit in een echo...",
                "...als je dit hoort: zet je radio niet uit. Elke ontvanger maakt mijn signaal sterker...",
                "...je hebt alle stukken. Je hebt mij gevonden. Draai de knop naar nul hertz... en luister.",
            },
            finale = "Uit de ruis klinkt Mira's stem, voor het eerst helder:\n\"Dank je. Ik hoor je. Ik kom naar huis.\"\n\nStation Echo is terug in de lucht.",
            failure = "Ergens tussen de masten sterft Mira's stem weg in de ruis...\nMaar zolang er iemand luistert, is ze niet helemaal weg.",
        },

        // ------------------------------------------------------------------ 2
        new StoryDefinition
        {
            id = "nachtegaal", title = "Radio Nachtegaal", subtitle = "De laatste uitzending van een piratenzender",
            fragmentLabel = "TAPE", winTitle = "TAPE\nGERED", loseTitle = "TAPE\nVERLOREN",
            intro =
                "1967. Piratenzender Radio Nachtegaal zond elke nacht muziek uit vanop een roestig schip voor de kust. Tot de politie het schip enterde.\n\n" +
                "DJ Rik 'de Nachtegaal' Ramon knipte op het laatste moment zijn mastertape in stukken en verstopte ze.\n\n" +
                "Vandaag vangt jouw radio opnieuw zijn stem op. Zet de tape terug in elkaar voor de oude zendertjes uitvallen.",
            fragments = new[]
            {
                "...goeienavond, nachtbrakers! Hier is Radio Nachtegaal, de enige zender die nooit slaapt...",
                "...vanavond een plaatje dat de staatsradio nooit zou durven draaien. Volume open!",
                "...er vaart een boot van de rijkswacht onze richting uit. Geen paniek, we draaien gewoon door...",
                "...ze komen aan boord. Ik knip de tape in stukken. Wie dit vindt: zet ze terug in elkaar...",
                "...de laatste plaat van vanavond heb ik zelf geschreven. Niemand heeft ze ooit gehoord...",
                "...het refrein zit verstopt in de stukken. Elk stuk is een regel...",
                "...ze nemen mijn microfoon af, maar de golven kunnen ze niet tegenhouden...",
                "...als je dit hoort, ben je een van ons: een nachtegaal...",
                "...nog even, nog een stuk, dan speelt de plaat helemaal...",
                "...daar is ze dan, mijn laatste plaat. Voor iedereen die ooit 's nachts heeft liggen luisteren.",
            },
            finale = "Voor het eerst in zestig jaar klinkt de laatste plaat van Radio Nachtegaal volledig. Ergens zingt een oude man mee.\n\n\"Bedankt, nachtegaal. Tot morgennacht.\"",
            failure = "Het laatste stuk tape ruist weg en de plaat blijft onaf...\nVoorlopig toch.",
        },

        // ------------------------------------------------------------------ 3
        new StoryDefinition
        {
            id = "zilvervos", title = "Operatie Zilvervos", subtitle = "Geheime codeberichten uit de Koude Oorlog",
            fragmentLabel = "CODEBERICHT", winTitle = "MISSIE\nGESLAAGD", loseTitle = "MISSIE\nMISLUKT",
            intro =
                "1962, midden in de Koude Oorlog. Agent 'Zilvervos' smokkelde geheime plannen het land uit via gecodeerde radioberichten. Haar laatste reeks berichten werd nooit opgepikt.\n\n" +
                "Verborgen zendertjes sturen ze nog altijd automatisch uit. Spoor ze op en kraak de codes voordat de zelfvernietiging de zenders uitschakelt.",
            fragments = new[]
            {
                "ZILVERVOS AAN THUISBASIS. Ik ben ontdekt. Ik verstop de plannen in stukken. Volg de zenders.",
                "Gebruik de oude codeboeken niet meer. Ze lezen mee.",
                "Iemand in ons eigen team speelt dubbelspel. Vertrouw niemand die naar sigaren ruikt.",
                "De plannen gaan niet over wapens. Ze gaan over een satelliet die met elke radio kan meeluisteren.",
                "De satelliet heet 'Oor'. Hij wordt volgende maand gelanceerd.",
                "Ik heb de lanceercodes gewijzigd. Zonder mijn laatste bericht start hij nooit.",
                "Ze staan voor mijn deur. Dit is mijn voorlaatste uitzending.",
                "De dubbelspion is de man die mij dit codeboek gaf.",
                "Als jullie dit decoderen, is mijn missie geslaagd, met of zonder mij.",
                "LAATSTE BERICHT: de satelliet zal nooit vertrekken. Zilvervos meldt zich af. Over en sluiten.",
            },
            finale = "De satelliet 'Oor' bleef voor altijd aan de grond. Pas nu, zestig jaar later, weet iemand waarom.\n\nZilvervos was een held, en jij bent de eerste die het weet.",
            failure = "De zenders hebben zichzelf vernietigd.\nDe laatste berichten van Zilvervos zijn verloren... of toch niet?",
        },

        // ------------------------------------------------------------------ 4
        new StoryDefinition
        {
            id = "vesta", title = "Signaal uit de Ruimte", subtitle = "Een neergestorte sonde roept om hulp",
            fragmentLabel = "DATAPAKKET", winTitle = "CONTACT\nGELEGD", loseTitle = "VERBINDING\nVERBROKEN",
            intro =
                "Vannacht zagen honderden mensen een vallende ster. Het was geen ster: het was ruimtesonde VESTA-7, na veertig jaar terug uit de diepe ruimte.\n\n" +
                "Bij de terugkeer is ze in stukken gebroken. Elk stuk zendt nog een zwak datapakket uit. Boordcomputer LUNA kan alleen herstarten als jij alle pakketten verzamelt, voordat de batterijen leeg zijn.",
            fragments = new[]
            {
                "LUNA> opstartfout... geheugen verspreid... ik heb hulp nodig van een mens met een ontvanger...",
                "LUNA> logboek jaar 3: Jupiter gepasseerd. De stormen zingen op lage frequenties.",
                "LUNA> logboek jaar 12: ik ben alleen. Ik heb leren tellen tot oneindig. Het helpt niet.",
                "LUNA> logboek jaar 27: ik heb iets opgevangen. Een signaal dat niet van de aarde komt.",
                "LUNA> het signaal herhaalt een patroon: priemgetallen. Iemand probeert te praten.",
                "LUNA> ik heb geantwoord. Met de muziek van de gouden plaat aan boord.",
                "LUNA> logboek jaar 39: ze hebben teruggeantwoord. Ik keer terug om het te vertellen.",
                "LUNA> hitteschild 12%... ik moet de boodschap in stukken opslaan...",
                "LUNA> als je dit leest: je bent bijna bij de kern. Mijn stem wordt sterker.",
                "LUNA> herstart voltooid. Hier is hun boodschap, voor de hele mensheid: 'Wij hebben jullie gehoord.'",
            },
            finale = "LUNA zendt de boodschap uit naar elke radio op aarde. Voor het eerst weten we het zeker: we zijn niet alleen.\n\nEn jij was de eerste die luisterde.",
            failure = "De batterijen zijn leeg. LUNA valt stil,\nen de boodschap blijft verborgen in de ruis...",
        },

        // ------------------------------------------------------------------ 5
        new StoryDefinition
        {
            id = "nachtuil", title = "Het Spookstation", subtitle = "Een presentatrice die nooit afsloot",
            fragmentLabel = "STEMOPNAME", winTitle = "UITZENDING\nAFGESLOTEN", loseTitle = "TE\nLAAT",
            intro =
                "Elke nacht om 3 uur precies verschijnt er een zender die al dertig jaar niet meer bestaat: Radio Middernacht. De stem is die van 'De Nachtuil', die tijdens haar laatste uitzending spoorloos verdween.\n\n" +
                "Ze heeft haar programma nooit kunnen afsluiten, en daarom blijft ze terugkomen. Verzamel haar stemopnames zodat ze eindelijk 'goedenacht' kan zeggen, voordat de zon opkomt.",
            fragments = new[]
            {
                "...goedenavond, slapelozen. Hier is De Nachtuil. Ben jij daar? Ik hoor je ademen door de ruis...",
                "...de verzoekjes van vannacht zijn vreemd. Iedereen vraagt hetzelfde liedje. Een liedje dat niet bestaat...",
                "...er staat iemand achter het studioraam. Hij heeft geen gezicht, enkel een koptelefoon...",
                "...het licht in de studio flikkert. Mijn stem komt een seconde te laat uit de speakers...",
                "...ik heb de deur geprobeerd. Achter de deur is gewoon... nog een studio...",
                "...ik draai mijn laatste plaat. Ze speelt achterstevoren...",
                "...ik denk dat ik al lang niet meer in de studio ben. Ik zit in de golven zelf...",
                "...luisteraar, jij bent de enige die nog afstemt. Blijf bij me...",
                "...nog een ding moet ik doen. Iets wat ik dertig jaar geleden vergat...",
                "...dit was De Nachtuil, op Radio Middernacht. Bedankt om te luisteren. Slaap zacht... en goedenacht.",
            },
            finale = "De zender valt stil, maar niet zoals vroeger. De ruis klinkt rustig, bijna als een zucht van opluchting.\n\nDe Nachtuil heeft eindelijk afgesloten.",
            failure = "De zon is op. De stem van De Nachtuil vervaagt midden in een zin.\nMorgennacht om 3 uur is ze er weer...",
        },

        // ------------------------------------------------------------------ 6
        new StoryDefinition
        {
            id = "noorderlicht", title = "Expeditie Noorderlicht", subtitle = "Een poolschip dat nooit terugkeerde",
            fragmentLabel = "LOGBOEK", winTitle = "EXPEDITIE\nGEVONDEN", loseTitle = "VERDWENEN\nIN HET IJS",
            intro =
                "1928. Het schip Noorderlicht vertrok met kapitein Elise Vandamme richting de Noordpool. Na drie maanden stopten de radioberichten en het schip werd nooit teruggevonden.\n\n" +
                "De laatste logboekberichten weerkaatsen nog altijd door de atmosfeer, en vandaag vallen ze in jouw buurt op. Verzamel ze en ontdek wat er met de Noorderlicht gebeurd is.",
            fragments = new[]
            {
                "DAG 1: Vertrokken uit Antwerpen. De bemanning is vol goede moed.",
                "DAG 23: Eerste ijsbergen gezien. Zo groot als kathedralen.",
                "DAG 41: De kok heeft de laatste chocolade verdeeld. Het moraal blijft goed.",
                "DAG 58: We zitten vast in het pakijs. Het schip kraakt elke nacht.",
                "DAG 66: Vreemd licht aan de horizon. Groen en dansend. Het kompas draait in rondjes.",
                "DAG 71: We hebben het schip verlaten en trekken te voet verder over het ijs.",
                "DAG 80: Een eiland dat niet op de kaart staat. Er brandt licht in een oude hut.",
                "DAG 84: De hut was van een eerdere expeditie. Er lag genoeg voorraad voor een jaar.",
                "DAG 90: De radio werkt nog maar half. Ik zend onze positie uit, steeds opnieuw.",
                "DAG 97: Alle bemanningsleden gezond. Kom ons halen. Kapitein E. Vandamme.",
            },
            finale = "Met de laatste berichten vindt een onderzoeksteam het naamloze eiland, en in de hut een logboek vol namen en een foto van een lachende bemanning.\n\nDe Noorderlicht is eindelijk thuisgekomen.",
            failure = "De berichten verdwijnen in het noorderlicht.\nHet laatste logboek blijft voor altijd onder het ijs...",
        },
    };

    // Willekeurig verhaal, liefst niet het vorige.
    public static StoryDefinition Random(string avoidId)
    {
        var options = All.FindAll(s => s.id != avoidId);
        if (options.Count == 0) options = All;
        return options[UnityEngine.Random.Range(0, options.Count)];
    }
}
