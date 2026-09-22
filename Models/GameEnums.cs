namespace IntesaVincente.Models;

/// <summary>Fase in cui si trova la partita.</summary>
public enum GameStatus
{
    /// <summary>Partita non avviata: si configura e si scelgono le parole.</summary>
    Idle,

    /// <summary>Parola a schermo e tempo che scorre.</summary>
    Live,

    /// <summary>
    /// Tempo fermato dall'esterno (telecomando o pulsantiera) con la parola ancora
    /// a schermo e non giudicata: la regia può riprendere il tempo o assegnare i punti.
    /// </summary>
    Frozen,

    /// <summary>
    /// Parola giudicata e tempo fermo, in attesa che la regia mandi la prossima.
    /// È qui che si perde il "tempo morto" senza consumare secondi di gioco.
    /// </summary>
    Waiting,

    /// <summary>Partita conclusa: tempo scaduto o parole esaurite.</summary>
    Finished
}

/// <summary>Da dove arrivano le parole caricate in regia.</summary>
public enum WordSource
{
    /// <summary>Temi inclusi nel programma, un file <c>.txt</c> per tema nella cartella <c>temi/</c>.</summary>
    Program,

    /// <summary>File <c>.txt</c> caricato dall'utente dal pannello di regia.</summary>
    File,

    /// <summary>Parole incollate a mano nel pannello di regia.</summary>
    Manual
}

/// <summary>Perché la partita è finita.</summary>
public enum FinishReason
{
    /// <summary>La partita non è finita.</summary>
    None,

    /// <summary>Il tempo è scaduto.</summary>
    Time,

    /// <summary>Non c'è più nessuna parola da mandare a schermo.</summary>
    NoWords
}

/// <summary>Come la regia ha giudicato una parola.</summary>
public enum Judgement
{
    /// <summary>Indovinata: punti in più, pari al valore della parola.</summary>
    Correct,

    /// <summary>Sbagliata: punti in meno, pari al valore della parola.</summary>
    Wrong,

    /// <summary>Passata: nessun punto, ma consuma uno dei passi disponibili.</summary>
    Passed
}
