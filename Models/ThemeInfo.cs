namespace IntesaVincente.Models;

/// <summary>Un tema di parole incluso nel programma, cioè un file <c>.txt</c> in <c>temi/</c>.</summary>
/// <param name="Id">Nome del file senza estensione, usato come identificatore.</param>
/// <param name="Name">Nome leggibile mostrato in regia.</param>
/// <param name="SingleCount">Quante parole da 1 punto contiene.</param>
/// <param name="DoubleCount">Quante parole da 2 punti contiene.</param>
public readonly record struct ThemeInfo(string Id, string Name, int SingleCount, int DoubleCount)
{
    /// <summary>Quante parole contiene in tutto.</summary>
    public int Total => SingleCount + DoubleCount;
}
