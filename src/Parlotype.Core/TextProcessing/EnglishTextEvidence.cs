namespace Parlotype.Core.TextProcessing;

/// <summary>
/// Counts the function words in a transcript that speak for English and those
/// that speak for another Latin-script language (ADR-075).
/// </summary>
/// <remarks>
/// <para>Not language identification. It exists for the engines that report no
/// language (Parakeet, the cloud providers), where the text is all there is,
/// and it only has to answer one question: is there positive evidence that
/// this is <em>not</em> English? Function words make up a third of ordinary
/// speech, so a sentence of five words or more nearly always carries some.</para>
/// <para>The lists are deliberately lopsided. A word that is also ordinary
/// English ("met", "pour", "care", "ten", "die"), a common name ("Sam", "Ben",
/// "Ali"), or what an English speaker dictating about software says ("int",
/// "nav", "meta") is left out of the foreign list even where it is frequent in
/// its own language: wrongly skipping cleanup for an English speaker on every
/// such sentence costs more than letting one foreign sentence through.</para>
/// </remarks>
internal static class EnglishTextEvidence
{
    /// <summary>Weight of a word that is English and, as far as we know, nothing else.</summary>
    private const int StrongWeight = 2;

    /// <summary>Weight of an English word that is also a common word in another language.</summary>
    private const int WeakWeight = 1;

    /// <summary>Weight of a function word of another language.</summary>
    private const int ForeignWeight = 2;

    private static readonly HashSet<string> Strong = Words(
        "the and that this with has you your not but they from what there would can which their about been " +
        "were when it's i'm don't like need should could because then than these those here where how why " +
        "who out get got going know think please some any our his she him them it if are yeah okay yes " +
        "hello thanks thank sorry gonna wanna right well really very much more one two three new now today " +
        "tomorrow make take call look good great did does into after before again only other each every " +
        "between without while something anything everything people thing things way back first last next " +
        "too its i'll we're that's can't didn't doesn't isn't i've you're let's there's what's won't " +
        "they're we'll you'll wasn't aren't couldn't shouldn't wouldn't");

    private static readonly HashSet<string> Weak = Words(
        "to of is was in on for at by so we an as do or be up have just time send let over still want will " +
        "also all my had her am");

    private static readonly HashSet<string> Foreign = Words(
        // German
        "der das und ist nicht ich ein eine mit auch auf für sich dass wir aber wie oder wenn noch nach " +
        "bei mir zu sie es den dem von haben habe wird kann sind hier nur schon sehr dann mal mich dich uns " +
        "danke bitte nein " +
        // Dutch
        "het een en dat niet ik je zijn voor maar ook naar dit wat aan om nog wel geen moet heb hebben wij " +
        "zij hij bij uit kan zou mijn deze heeft wordt worden veel op te nee bedankt goed " +
        // French
        "et est pas les des une que qui dans avec cette elle nous vous ils mais donc très tres suis sont du " +
        "ne au aux sur c'est j'ai n'est qu'il d'un d'une merci oui bonjour " +
        // Spanish
        "por para con una pero como muy del esta este esto tengo porque cuando también tambien se su nos " +
        "si mi tu un uno más mas gracias hola bueno " +
        // Italian
        "il che di è non sono della dei delle nel nella alla ma ti gli hai abbiamo questo questa anche più " +
        "piu molto perché perche quando cosa lui lei noi voi loro sei essere poi grazie bene " +
        // Portuguese
        "não nao uma da das mais ele ela isso isto está são sao foi tem muito você voce meu minha pelo pela " +
        "na ao à é obrigado obrigada sim " +
        // Danish, Norwegian, Swedish
        "og och det ikke inte jeg jag att är har som til på med af av vi han hun hon skal ska vil vill være " +
        "vara blir bliver også också fra från eller efter mig sig mycket meget nej ja ett hvad vad hvor när " +
        "når nu denne dette tak takk " +
        // Finnish, Estonian
        "ei että oli hän mutta kun niin myös ole olen minä sinä tämä mitä joka tai kuin jos nyt sitten hyvä " +
        "kiitos ovat ka ta kui aga või mis siis veel nii seda minu tema kas ning väga kõik " +
        // Hungarian
        "és egy az nem hogy ez csak már még vagy itt ott igen én lesz kell nagyon akkor köszönöm " +
        // Polish
        "nie się sie jest że ze jak po za od czy dla tym tego już juz być byc mam był tylko bardzo może " +
        "moze ty są przez gdy kiedy jeszcze też tez lub oraz dziękuję " +
        // Czech, Slovak
        "jsem som jsou sú není neni ako čo při pri už uz taky tiež který ktorý když keď nebo alebo bude mám " +
        "ano áno jsme sme jste ste tady tam velmi veľmi protože pretože děkuji ďakujem " +
        // Slovenian, Croatian
        "ki kot tudi sem bi če še že kaj kako ker lahko zelo samo biti ima niso nisem su što sto ili će nije " +
        "koji još jos kao ako sve vrlo već vec mogu sada ovdje hvala " +
        // Romanian
        "și este cu din în pe să că mai sunt pentru dar ea acest această foarte când cand dacă daca sau " +
        "fost lui lor mult bine acum aici doar " +
        // Latvian, Lithuanian
        "ir ar uz tas tā vai kas kā lai bija viņš mēs jūs ļoti arī tikai šis šī vēl jau pēc kur labi paldies " +
        "yra kad iš aš jis ji mes jie nėra buvo kaip labai taip tik arba ką čia kai nes apie prie už ačiū " +
        // Maltese
        "li fil hu jien aħna huma mhux għal ghal minn bil kien kienet biex imma iva hemm dak kif wkoll " +
        "ħafna grazzi " +
        // Turkish
        "ve bir bu için icin ile yok sen siz çok cok ama gibi daha değil degil mı evet hayır şey kadar " +
        "sonra olarak olan " +
        // Indonesian
        "yang ini itu dengan untuk tidak dari ke pada saya kami kita akan juga sudah bisa atau karena tapi " +
        "anda mereka dia apa");

    /// <summary>
    /// Scores <paramref name="text"/>: how strongly its words speak for English
    /// and how strongly for another language. Both are zero for text that
    /// carries no listed word, such as a name or a two-word reply.
    /// </summary>
    public static (int English, int Foreign) Score(string text)
    {
        var english = 0;
        var foreign = 0;

        var strong = Strong.GetAlternateLookup<ReadOnlySpan<char>>();
        var weak = Weak.GetAlternateLookup<ReadOnlySpan<char>>();
        var other = Foreign.GetAlternateLookup<ReadOnlySpan<char>>();

        Span<char> buffer = stackalloc char[MaxWordLength];
        var span = text.AsSpan();
        var i = 0;
        while (i < span.Length)
        {
            if (!char.IsLetter(span[i]))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < span.Length && IsWordChar(span, i))
                i++;

            var word = span[start..i];
            if (word.Length > MaxWordLength || IsAcronym(word))
                continue;

            var length = 0;
            foreach (var ch in word)
                buffer[length++] = ch == '’' ? '\'' : char.ToLowerInvariant(ch);
            var key = buffer[..length];

            if (strong.Contains(key))
                english += StrongWeight;
            else if (weak.Contains(key))
                english += WeakWeight;
            else if (other.Contains(key))
                foreign += ForeignWeight;
        }

        return (english, foreign);
    }

    /// <summary>No listed word is longer; anything longer cannot match.</summary>
    private const int MaxWordLength = 16;

    /// <summary>A letter, or an apostrophe between two letters ("don't", "c'est").</summary>
    private static bool IsWordChar(ReadOnlySpan<char> text, int index)
    {
        var ch = text[index];
        if (char.IsLetter(ch))
            return true;

        return (ch == '\'' || ch == '’')
            && index + 1 < text.Length
            && char.IsLetter(text[index + 1]);
    }

    /// <summary>
    /// "AI", "EU", "OS", "UN": written in capitals they are English
    /// abbreviations, lowercased they are function words of other languages.
    /// </summary>
    private static bool IsAcronym(ReadOnlySpan<char> word)
    {
        if (word.Length < 2)
            return false;

        foreach (var ch in word)
        {
            if (!char.IsUpper(ch))
                return false;
        }

        return true;
    }

    private static HashSet<string> Words(string list) =>
        new(list.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
}
