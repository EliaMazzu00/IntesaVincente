using IntesaVincente.Models;

namespace IntesaVincente.Services;

/// <summary>
/// Legge le parole: quelle incluse nel programma (un file <c>.txt</c> per tema nella
/// cartella <c>temi/</c>), quelle di un file caricato dall'utente e quelle incollate a
/// mano. Si occupa solo di leggere e classificare il testo: dello stato della partita
/// non sa nulla.
/// </summary>
/// <remarks>
/// Ogni riga di un file di temi è una voce. Righe vuote e righe che iniziano con
/// <c>#</c> sono ignorate, tranne <c># TEMA: Nome</c> che dà il nome leggibile al tema.
/// Il valore si deduce dal numero di parole della riga: una sola parola vale 1 punto,
/// due o più ne valgono 2.
/// <para>
/// Le parole dei temi vengono lette dal disco una volta sola e tenute in memoria: in
/// regia si spuntano e si togliono temi in continuazione, e rileggere venti file a
/// ogni clic non avrebbe senso.
/// </para>
/// </remarks>
public sealed class WordLibrary
{
    private const string ThemeNameTag = "# TEMA:";

    /// <summary>Parole di ogni tema, lette dal disco al primo utilizzo.</summary>
    private readonly Dictionary<string, WordSet> _wordsByTheme = new(StringComparer.OrdinalIgnoreCase);

    private List<ThemeInfo> _themes = new();

    /// <summary>Crea la libreria leggendo i temi presenti nella cartella indicata.</summary>
    /// <param name="themesPath">Cartella che contiene i file <c>.txt</c> dei temi.</param>
    public WordLibrary(string themesPath)
    {
        ThemesPath = themesPath;
        Reload();
    }

    /// <summary>Cartella da cui vengono letti i temi del programma.</summary>
    public string ThemesPath { get; }

    /// <summary>I temi disponibili, in ordine di nome file.</summary>
    public IReadOnlyList<ThemeInfo> Themes => _themes;

    /// <summary>
    /// Rilegge la cartella dei temi da zero, svuotando la cache. Utile se i file delle
    /// parole vengono modificati mentre l'applicazione è in esecuzione.
    /// </summary>
    public void Reload()
    {
        _wordsByTheme.Clear();
        var found = new List<ThemeInfo>();

        try
        {
            if (Directory.Exists(ThemesPath))
            {
                var files = Directory
                    .EnumerateFiles(ThemesPath, "*.txt")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

                foreach (var path in files)
                {
                    var lines = File.ReadAllLines(path);
                    var id = Path.GetFileNameWithoutExtension(path);
                    var words = Classify(lines);

                    // Le parole finiscono subito in cache: le abbiamo già lette per contarle.
                    _wordsByTheme[id] = words;
                    found.Add(new ThemeInfo(id, ReadThemeName(lines, id), words.Singles.Count, words.Doubles.Count));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cartella illeggibile: si resta senza temi inclusi. L'utente può sempre
            // caricare un file o incollare le parole a mano.
            found.Clear();
            _wordsByTheme.Clear();
        }

        _themes = found;
    }

    /// <summary>
    /// Unisce le parole dei temi indicati, mantenendo l'ordine in cui i temi appaiono
    /// nella cartella. Gli identificatori sconosciuti vengono ignorati.
    /// </summary>
    public WordSet GetWords(IEnumerable<string> themeIds)
    {
        var wanted = new HashSet<string>(themeIds, StringComparer.OrdinalIgnoreCase);
        var singles = new List<string>();
        var doubles = new List<string>();

        foreach (var theme in _themes)
        {
            if (!wanted.Contains(theme.Id) || !_wordsByTheme.TryGetValue(theme.Id, out var words))
                continue;

            singles.AddRange(words.Singles);
            doubles.AddRange(words.Doubles);
        }

        return new WordSet(singles, doubles);
    }

    /// <summary>I nomi leggibili dei temi indicati, nell'ordine della cartella.</summary>
    public List<string> GetThemeNames(IEnumerable<string> themeIds)
    {
        var wanted = new HashSet<string>(themeIds, StringComparer.OrdinalIgnoreCase);
        return _themes.Where(t => wanted.Contains(t.Id)).Select(t => t.Name).ToList();
    }

    /// <summary>
    /// Interpreta un testo a più righe come elenco di parole, una per riga.
    /// Accetta indifferentemente fine-riga Windows o Unix.
    /// </summary>
    public static WordSet Classify(string text) =>
        Classify(text.ReplaceLineEndings("\n").Split('\n'));

    /// <summary>
    /// Divide le righe nei due mazzi in base al numero di parole: una sola parola vale
    /// 1 punto, due o più ne valgono 2. Righe vuote e commenti vengono scartati.
    /// </summary>
    public static WordSet Classify(IEnumerable<string> lines)
    {
        var singles = new List<string>();
        var doubles = new List<string>();

        foreach (var line in lines)
        {
            var word = line.Trim();
            if (!IsContent(word))
                continue;

            if (CountWords(word) == 1)
                singles.Add(word);
            else
                doubles.Add(word);
        }

        return new WordSet(singles, doubles);
    }

    /// <summary>Scarta le righe vuote e i commenti.</summary>
    private static bool IsContent(string line) =>
        line.Length > 0 && !line.StartsWith('#');

    /// <summary>Quante parole contiene la riga, ignorando gli spazi in eccesso.</summary>
    private static int CountWords(string line) =>
        line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>
    /// Ricava il nome del tema dalla riga <c># TEMA: Nome</c>; se manca, lo deduce dal
    /// nome del file (<c>01-casa-e-vita-quotidiana</c> → <c>Casa e vita quotidiana</c>).
    /// </summary>
    private static string ReadThemeName(IEnumerable<string> lines, string fileName)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(ThemeNameTag, StringComparison.OrdinalIgnoreCase))
                continue;

            var declared = trimmed[ThemeNameTag.Length..].Trim();
            if (declared.Length > 0)
                return declared;
        }

        return PrettifyFileName(fileName);
    }

    /// <summary><c>01-casa-e-vita</c> → <c>Casa e vita</c>.</summary>
    private static string PrettifyFileName(string fileName)
    {
        var name = fileName;

        // Via il prefisso numerico che serve solo a ordinare i file.
        int dash = name.IndexOf('-');
        if (dash > 0 && int.TryParse(name[..dash], out _))
            name = name[(dash + 1)..];

        name = name.Replace('-', ' ').Replace('_', ' ').Trim();

        return name.Length > 0
            ? char.ToUpperInvariant(name[0]) + name[1..]
            : fileName;
    }
}
