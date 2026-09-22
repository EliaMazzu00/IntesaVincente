namespace IntesaVincente.Models;

/// <summary>
/// Le parole di una partita, divise nei due mazzi del gioco.
/// </summary>
/// <remarks>
/// Il valore di una parola non è dichiarato da nessuna parte: si deduce da quante
/// parole ci sono nella riga. <c>Tavolo</c> vale 1 punto, <c>Lago di Garda</c> ne
/// vale 2 ed è una di quelle "chiamabili" dal concorrente per raddoppiare.
/// I due mazzi vanno tenuti separati perché si pescano in modo indipendente:
/// le parole da 1 punto scorrono una dopo l'altra, quelle da 2 solo su richiesta.
/// </remarks>
public sealed class WordSet
{
    /// <summary>Un insieme vuoto, da usare come punto di partenza.</summary>
    public static WordSet Empty { get; } = new(Array.Empty<string>(), Array.Empty<string>());

    /// <summary>Crea un insieme dai due mazzi già divisi.</summary>
    public WordSet(IReadOnlyList<string> singles, IReadOnlyList<string> doubles)
    {
        Singles = singles;
        Doubles = doubles;
    }

    /// <summary>Le parole da 1 punto: una sola parola per riga.</summary>
    public IReadOnlyList<string> Singles { get; }

    /// <summary>Le parole da 2 punti: due o più parole per riga.</summary>
    public IReadOnlyList<string> Doubles { get; }

    /// <summary>Quante parole in tutto.</summary>
    public int Total => Singles.Count + Doubles.Count;

    /// <summary>Vero se non c'è nessuna parola.</summary>
    public bool IsEmpty => Total == 0;

    /// <summary>Riepilogo leggibile della consistenza dei due mazzi, per la regia.</summary>
    public string Describe() => $"{Singles.Count} da 1 punto · {Doubles.Count} da 2 punti";
}
