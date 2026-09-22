using System.Diagnostics;
using IntesaVincente.Models;
using Timer = System.Timers.Timer;

namespace IntesaVincente.Services;

/// <summary>
/// Lo stato della partita, condiviso da tutte le pagine aperte.
/// </summary>
/// <remarks>
/// <para>
/// È registrato come <b>singleton</b>: esiste una sola partita per processo e tutte le
/// pagine ne vedono la stessa istanza. La regia (<c>/admin</c>) comanda, il Display
/// (<c>/display</c>) mostra la parola ai suggeritori, il telecomando (<c>/remote</c>) e
/// l'endpoint <c>/api/freeze</c> possono fermare il tempo. Chi vuole restare aggiornato
/// si iscrive a <see cref="OnChange"/>.
/// </para>
/// <para>
/// <b>Il giro della partita.</b> <see cref="Start"/> manda la prima parola da 1 punto e
/// avvia il tempo (<see cref="GameStatus.Live"/>). Un giudizio ferma il tempo e registra
/// il punteggio (<see cref="GameStatus.Waiting"/>): i secondi persi tra una parola e
/// l'altra non contano. <see cref="SendNext"/> mostra la parola dopo e fa ripartire il
/// tempo. Il tempo si può fermare anche dall'esterno (<see cref="GameStatus.Frozen"/>)
/// e riprendere senza assegnare punti.
/// </para>
/// <para>
/// <b>Il cronometro va a scadenza, non a decrementi.</b> Si tiene un cronometro
/// monotono e i secondi che restavano quando è stato fermato: i secondi rimanenti si
/// <i>calcolano</i>. Decrementare un contatore a ogni tick accumulerebbe deriva e
/// sbaglierebbe ogni volta che un tick arriva in ritardo, che su 60 secondi di gioco
/// si nota. Il timer serve soltanto a far aggiornare le pagine.
/// </para>
/// <para>
/// <b>Concorrenza.</b> Ogni scrittura passa da <c>_lock</c>; i metodi che riusano altra
/// logica di scrittura usano le varianti <c>...Locked()</c> per non prendere il lock due
/// volte. <see cref="Notify"/> si chiama <b>fuori</b> dal lock, altrimenti il re-render
/// dei componenti avverrebbe a lock tenuto.
/// </para>
/// </remarks>
public sealed class GameService : IDisposable
{
    /// <summary>Ogni quanto si controlla il cronometro. Non è la precisione del tempo:
    /// quella la dà il cronometro monotono. È solo la frequenza di aggiornamento.</summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>Limiti dei valori configurabili.</summary>
    private const int MinTimerSeconds = 5;

    /// <inheritdoc cref="MinTimerSeconds"/>
    private const int MaxTimerSeconds = 3600;

    /// <inheritdoc cref="MinTimerSeconds"/>
    private const int MaxAllowance = 99;

    /// <summary>Punteggio minimo per poter chiamare una parola da 2 punti.</summary>
    private const int DoubleCallThreshold = 2;

    private readonly object _lock = new();
    private readonly Random _random = new();
    private readonly WordLibrary _library;
    private readonly ILogger<GameService> _logger;

    /// <summary>Cronometro monotono: misura quanto è passato da quando il tempo è partito.</summary>
    private readonly Stopwatch _clock = new();

    /// <summary>Quanto tempo restava quando il cronometro è stato fermato.</summary>
    private TimeSpan _remainingAtStop;

    /// <summary>Timer di aggiornamento: fa solo scattare <see cref="OnChange"/>.</summary>
    private readonly Timer _ticker;

    /// <summary>Ultimo secondo già comunicato alle pagine, per non notificare a vuoto.</summary>
    private int _lastNotifiedSecond;

    /// <summary>
    /// Crea il servizio e seleziona il primo tema disponibile, così la regia si apre già
    /// con delle parole pronte invece che vuota.
    /// </summary>
    public GameService(IWebHostEnvironment environment, ILogger<GameService> logger)
    {
        _logger = logger;
        _library = new WordLibrary(Path.Combine(environment.ContentRootPath, "temi"));

        _remainingAtStop = TimeSpan.FromSeconds(TimerSeconds);
        _lastNotifiedSecond = TimerSeconds;

        _ticker = new Timer(TickInterval.TotalMilliseconds) { AutoReset = true };
        _ticker.Elapsed += (_, _) => OnTick();

        if (_library.Themes.Count > 0)
            UseProgramThemes(new[] { _library.Themes[0].Id });
        else
            _logger.LogWarning("Nessun tema trovato in {Path}: la regia partirà senza parole.", _library.ThemesPath);
    }

    /// <summary>Scatta a ogni cambiamento di stato: le pagine si ridisegnano.</summary>
    public event Action? OnChange;

    // ============================================================
    //  Configurazione
    // ============================================================

    /// <summary>Durata della partita in secondi.</summary>
    public int TimerSeconds { get; private set; } = 60;

    /// <summary>Quanti "passo" si possono usare in una partita.</summary>
    public int MaxPasses { get; private set; } = 3;

    /// <summary>Quante parole da 2 punti si possono chiamare in una partita.</summary>
    public int MaxDoubles { get; private set; } = 3;

    /// <summary>Se mandare le parole in ordine casuale.</summary>
    public bool Shuffle { get; private set; }

    /// <summary>Da dove arrivano le parole attualmente caricate.</summary>
    public WordSource Source { get; private set; } = WordSource.Program;

    /// <summary>
    /// Applica la configurazione. Non fa nulla a partita in corso: cambiare il tempo o
    /// i passi mentre si gioca falserebbe la partita.
    /// </summary>
    /// <param name="timerSeconds">Durata della partita, fra 5 secondi e un'ora.</param>
    /// <param name="maxPasses">Passi disponibili, da 0 a 99.</param>
    /// <param name="maxDoubles">Raddoppi disponibili, da 0 a 99.</param>
    /// <param name="shuffle">Se mescolare le parole.</param>
    public void Configure(int timerSeconds, int maxPasses, int maxDoubles, bool shuffle)
    {
        lock (_lock)
        {
            if (IsActive)
                return;

            TimerSeconds = Math.Clamp(timerSeconds, MinTimerSeconds, MaxTimerSeconds);
            MaxPasses = Math.Clamp(maxPasses, 0, MaxAllowance);
            MaxDoubles = Math.Clamp(maxDoubles, 0, MaxAllowance);
            Shuffle = shuffle;

            // A partita ferma il tempo mostrato si allinea subito alla nuova durata.
            ResetClockLocked();
        }

        Notify();
    }

    // ============================================================
    //  Parole
    // ============================================================

    private WordSet _loaded = WordSet.Empty;
    private List<string> _singlesOrder = new();
    private List<string> _doublesOrder = new();
    private HashSet<string> _selectedThemeIds = new(StringComparer.OrdinalIgnoreCase);
    private int _singleIndex = -1;
    private int _doubleIndex = -1;
    private string? _currentWord;

    /// <summary>I temi inclusi nel programma.</summary>
    public IReadOnlyList<ThemeInfo> AvailableThemes => _library.Themes;

    /// <summary>Gli identificatori dei temi attualmente spuntati in regia.</summary>
    public IReadOnlyCollection<string> SelectedThemeIds => _selectedThemeIds;

    /// <summary>Quante parole da 1 punto sono caricate.</summary>
    public int SingleCount => _loaded.Singles.Count;

    /// <summary>Quante parole da 2 punti sono caricate.</summary>
    public int DoubleCount => _loaded.Doubles.Count;

    /// <summary>Quante parole sono caricate in tutto.</summary>
    public int WordCount => _loaded.Total;

    /// <summary>Riepilogo leggibile di cosa è caricato, mostrato in regia.</summary>
    public string WordsInfo { get; private set; } = "Nessuna parola caricata";

    /// <summary>Carica le parole dei temi indicati, unendole nell'ordine della cartella.</summary>
    /// <returns>Se c'è almeno una parola, e il riepilogo da mostrare in regia.</returns>
    public (bool Ok, string Message) UseProgramThemes(IEnumerable<string> themeIds)
    {
        var ids = new HashSet<string>(themeIds, StringComparer.OrdinalIgnoreCase);
        var words = _library.GetWords(ids);
        var names = _library.GetThemeNames(ids);

        var message = names.Count == 0
            ? "Nessun tema selezionato"
            : $"{(names.Count == 1 ? "Tema" : "Temi")}: {string.Join(", ", names)} — {words.Describe()}";

        lock (_lock)
        {
            _selectedThemeIds = ids;
            LoadWordsLocked(words, WordSource.Program, message);
        }

        Notify();
        return (!words.IsEmpty, message);
    }

    /// <summary>Carica le parole da un file <c>.txt</c> scelto dall'utente.</summary>
    /// <returns>Se c'è almeno una parola, e il riepilogo da mostrare in regia.</returns>
    public (bool Ok, string Message) SetWordsFromUpload(string fileName, string content)
    {
        var words = WordLibrary.Classify(content);
        var message = $"File \"{fileName}\" — {words.Describe()}";

        lock (_lock)
            LoadWordsLocked(words, WordSource.File, message);

        Notify();
        return (!words.IsEmpty, message);
    }

    /// <summary>Carica le parole incollate a mano in regia.</summary>
    /// <returns>Se c'è almeno una parola, e il riepilogo da mostrare in regia.</returns>
    public (bool Ok, string Message) SetWordsFromText(string text)
    {
        var words = WordLibrary.Classify(text);
        var message = $"Parole inserite a mano — {words.Describe()}";

        lock (_lock)
            LoadWordsLocked(words, WordSource.Manual, message);

        Notify();
        return (!words.IsEmpty, message);
    }

    private void LoadWordsLocked(WordSet words, WordSource source, string message)
    {
        _loaded = words;
        Source = source;
        WordsInfo = message;
    }

    // ============================================================
    //  Stato della partita
    // ============================================================

    /// <summary>Fase corrente della partita.</summary>
    public GameStatus Status { get; private set; } = GameStatus.Idle;

    /// <summary>Perché la partita è finita, se è finita.</summary>
    public FinishReason EndReason { get; private set; } = FinishReason.None;

    /// <summary>Punteggio corrente; non scende mai sotto zero.</summary>
    public int Score { get; private set; }

    /// <summary>Vero se la parola a schermo vale 2 punti.</summary>
    public bool CurrentIsDouble { get; private set; }

    /// <summary>Quanto vale la parola a schermo: 1 oppure 2.</summary>
    public int CurrentValue => CurrentIsDouble ? 2 : 1;

    /// <summary>Quante parole sono state indovinate.</summary>
    public int CorrectCount { get; private set; }

    /// <summary>Quante parole sono state sbagliate.</summary>
    public int WrongCount { get; private set; }

    /// <summary>Quante parole sono state passate.</summary>
    public int PassedCount { get; private set; }

    /// <summary>Quante parole sono già state giocate.</summary>
    public int PlayedCount => CorrectCount + WrongCount + PassedCount;

    /// <summary>Passi ancora disponibili.</summary>
    public int PassesRemaining => Math.Max(0, MaxPasses - PassesUsed);

    /// <summary>Raddoppi ancora disponibili.</summary>
    public int DoublesAllowedRemaining => Math.Max(0, MaxDoubles - DoublesUsed);

    /// <summary>Passi già usati.</summary>
    public int PassesUsed { get; private set; }

    /// <summary>Raddoppi già chiamati.</summary>
    public int DoublesUsed { get; private set; }

    /// <summary>Vero se la partita è avviata e non ancora conclusa.</summary>
    public bool IsActive => Status is GameStatus.Live or GameStatus.Frozen or GameStatus.Waiting;

    /// <summary>Vero se si può avviare una partita adesso.</summary>
    public bool CanStart => Status is (GameStatus.Idle or GameStatus.Finished) && SingleCount > 0;

    /// <summary>Vero se la parola a schermo si può giudicare adesso.</summary>
    public bool CanJudge => Status is GameStatus.Live or GameStatus.Frozen;

    /// <summary>Quante parole da 1 punto restano da mandare.</summary>
    public int SinglesRemaining => Math.Max(0, _singlesOrder.Count - (_singleIndex + 1));

    /// <summary>Quante parole da 2 punti restano da mandare.</summary>
    public int DoublesRemaining => Math.Max(0, _doublesOrder.Count - (_doubleIndex + 1));

    /// <summary>
    /// Vero se si può chiamare una parola da 2 punti: serve un punteggio di almeno 2,
    /// così una risposta sbagliata non può mai portare il punteggio in negativo.
    /// </summary>
    public bool CanCallDouble =>
        Score >= DoubleCallThreshold && DoublesRemaining > 0 && DoublesAllowedRemaining > 0;

    /// <summary>Vero se c'è ancora almeno una parola mandabile.</summary>
    public bool CanSendAnything => SinglesRemaining > 0 || CanCallDouble;

    /// <summary>
    /// La parola a schermo. Vale solo mentre è effettivamente esposta: fra una parola e
    /// l'altra, e a partita finita, è <c>null</c>, così non resta appesa da nessuna parte.
    /// </summary>
    public string? CurrentWord
    {
        get
        {
            lock (_lock)
                return Status is GameStatus.Live or GameStatus.Frozen ? _currentWord : null;
        }
    }

    // ============================================================
    //  Cronometro
    // ============================================================

    /// <summary>Quanto tempo resta, calcolato dal cronometro e non decrementato.</summary>
    private TimeSpan Remaining
    {
        get
        {
            if (!_clock.IsRunning)
                return _remainingAtStop;

            var left = _remainingAtStop - _clock.Elapsed;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// I secondi rimanenti da mostrare. Arrotondati per eccesso, così la partita si apre
    /// mostrando la durata piena e lo zero compare solo a tempo davvero scaduto.
    /// </summary>
    public int RemainingSeconds => (int)Math.Ceiling(Remaining.TotalSeconds);

    /// <summary>Frazione di tempo già consumata, da 0 a 1, per le barre di avanzamento.</summary>
    public double ElapsedFraction
    {
        get
        {
            if (TimerSeconds <= 0)
                return 1;

            double used = TimerSeconds - Remaining.TotalSeconds;
            return Math.Clamp(used / TimerSeconds, 0, 1);
        }
    }

    private void StartClockLocked()
    {
        _clock.Restart();
        _ticker.Start();
    }

    private void StopClockLocked()
    {
        // Prima si fotografa quanto restava, poi si ferma: l'ordine conta.
        _remainingAtStop = Remaining;
        _clock.Reset();
        _ticker.Stop();
    }

    private void ResetClockLocked()
    {
        _clock.Reset();
        _ticker.Stop();
        _remainingAtStop = TimeSpan.FromSeconds(TimerSeconds);
        _lastNotifiedSecond = TimerSeconds;
    }

    /// <summary>
    /// Controlla il cronometro. Notifica le pagine solo quando cambia il secondo
    /// mostrato, non a ogni tick: cinque aggiornamenti al secondo per ogni pagina
    /// aperta non servirebbero a nulla.
    /// </summary>
    private void OnTick()
    {
        bool changed = false;

        lock (_lock)
        {
            if (Status != GameStatus.Live)
                return;

            int seconds = RemainingSeconds;

            if (seconds != _lastNotifiedSecond)
            {
                _lastNotifiedSecond = seconds;
                changed = true;
            }

            if (Remaining <= TimeSpan.Zero)
            {
                FinishLocked(FinishReason.Time);
                changed = true;
            }
        }

        if (changed)
            Notify();
    }

    // ============================================================
    //  Conduzione della partita
    // ============================================================

    /// <summary>
    /// Avvia una nuova partita: prima parola da 1 punto a schermo e tempo che scorre.
    /// Senza parole da 1 punto non fa nulla, perché la partita comincia sempre da quelle.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (IsActive || _loaded.Singles.Count == 0)
                return;

            _singlesOrder = Order(_loaded.Singles);
            _doublesOrder = Order(_loaded.Doubles);

            Score = 0;
            CorrectCount = 0;
            WrongCount = 0;
            PassedCount = 0;
            PassesUsed = 0;
            DoublesUsed = 0;
            EndReason = FinishReason.None;

            _singleIndex = 0;
            _doubleIndex = -1;
            _currentWord = _singlesOrder[0];
            CurrentIsDouble = false;

            ResetClockLocked();
            Status = GameStatus.Live;
            StartClockLocked();
        }

        Notify();
    }

    /// <summary>Prepara l'ordine di uscita delle parole, mescolandole se richiesto.</summary>
    private List<string> Order(IReadOnlyList<string> words)
    {
        var ordered = words.ToList();

        if (!Shuffle)
            return ordered;

        // Fisher-Yates: ogni ordine è equiprobabile.
        for (int i = ordered.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
        }

        return ordered;
    }

    /// <summary>
    /// Ferma il tempo con la parola ancora a schermo: lo fa il telecomando, l'endpoint
    /// <c>/api/freeze</c> o il pulsante di stop della regia.
    /// </summary>
    public void FreezeClock()
    {
        lock (_lock)
        {
            if (Status != GameStatus.Live)
                return;

            StopClockLocked();
            Status = GameStatus.Frozen;
        }

        Notify();
    }

    /// <summary>Fa ripartire il tempo sulla stessa parola, senza assegnare punti.</summary>
    public void ResumeClock()
    {
        lock (_lock)
        {
            if (Status != GameStatus.Frozen)
                return;

            Status = GameStatus.Live;
            StartClockLocked();
        }

        Notify();
    }

    /// <summary>Registra che la parola è stata indovinata.</summary>
    public void MarkCorrect() => Judge(Judgement.Correct);

    /// <summary>Registra che la parola è stata sbagliata.</summary>
    public void MarkWrong() => Judge(Judgement.Wrong);

    /// <summary>Registra un passo, se ne restano.</summary>
    public void MarkPass() => Judge(Judgement.Passed);

    /// <summary>
    /// Giudica la parola a schermo: aggiorna punteggio e statistiche, ferma il tempo e
    /// mette la partita in attesa della prossima parola.
    /// </summary>
    public void Judge(Judgement judgement)
    {
        lock (_lock)
        {
            if (!CanJudge)
                return;

            switch (judgement)
            {
                case Judgement.Correct:
                    Score += CurrentValue;
                    CorrectCount++;
                    break;

                case Judgement.Wrong:
                    // Il punteggio non scende sotto zero.
                    Score = Math.Max(0, Score - CurrentValue);
                    WrongCount++;
                    break;

                case Judgement.Passed:
                    if (PassesRemaining <= 0)
                        return;

                    PassesUsed++;
                    PassedCount++;
                    break;
            }

            StopClockLocked();

            // Se non resta niente da mandare la partita finisce qui.
            if (CanSendAnything)
                Status = GameStatus.Waiting;
            else
                FinishLocked(FinishReason.NoWords);
        }

        Notify();
    }

    /// <summary>
    /// Manda la prossima parola e fa ripartire il tempo.
    /// </summary>
    /// <param name="doubled">
    /// <c>true</c> per chiamare una parola da 2 punti: ammesso solo se
    /// <see cref="CanCallDouble"/> è vero.
    /// </param>
    public void SendNext(bool doubled)
    {
        lock (_lock)
        {
            if (Status != GameStatus.Waiting)
                return;

            if (doubled)
            {
                if (!CanCallDouble)
                    return;

                _doubleIndex++;
                _currentWord = _doublesOrder[_doubleIndex];
                CurrentIsDouble = true;
                DoublesUsed++;
            }
            else
            {
                if (SinglesRemaining <= 0)
                    return;

                _singleIndex++;
                _currentWord = _singlesOrder[_singleIndex];
                CurrentIsDouble = false;
            }

            Status = GameStatus.Live;
            StartClockLocked();
        }

        Notify();
    }

    /// <summary>Riporta tutto al punto di partenza, pronto per una nuova partita.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            ResetClockLocked();

            Status = GameStatus.Idle;
            EndReason = FinishReason.None;
            Score = 0;
            CorrectCount = 0;
            WrongCount = 0;
            PassedCount = 0;
            PassesUsed = 0;
            DoublesUsed = 0;
            _singleIndex = -1;
            _doubleIndex = -1;
            _currentWord = null;
            CurrentIsDouble = false;
        }

        Notify();
    }

    /// <summary>Chiude la partita. Va chiamato tenendo il lock.</summary>
    private void FinishLocked(FinishReason reason)
    {
        StopClockLocked();

        Status = GameStatus.Finished;
        EndReason = reason;
        _currentWord = null;
        CurrentIsDouble = false;

        if (reason == FinishReason.Time)
            _remainingAtStop = TimeSpan.Zero;
    }

    private void Notify() => OnChange?.Invoke();

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            _ticker.Stop();
            _ticker.Dispose();
            _clock.Reset();
        }
    }
}
