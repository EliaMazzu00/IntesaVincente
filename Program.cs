using IntesaVincente.Components;
using IntesaVincente.Models;
using IntesaVincente.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server: l'interfaccia vive sul server e arriva al browser via SignalR,
// così regia, Display e telecomando restano sincronizzati senza scrivere API.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Una sola partita per processo, condivisa da tutte le pagine aperte.
builder.Services.AddSingleton<GameService>();

// In ascolto su tutte le interfacce di rete, così gli altri dispositivi della LAN
// possono aprire /display o /remote puntando all'IP di questo PC.
// La porta si cambia da appsettings.json ("Server:Port") o con la variabile
// d'ambiente Server__Port, senza ricompilare. La 5080 lascia libera la 5090,
// usata dal progetto gemello "La Ruota della Fortuna": le due app possono
// girare insieme sullo stesso PC.
int port = builder.Configuration.GetValue("Server:Port", 5080);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Niente redirect a HTTPS: l'app gira in HTTP semplice sulla rete locale, dove un
// certificato non sarebbe verificabile dagli altri dispositivi.
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

MapGameApi(app);

app.Logger.LogInformation("L'Intesa Vincente è in ascolto sulla porta {Port}.", port);
app.Run();

// ================================================================
//  API di gioco
// ================================================================

// Espone i comandi di gioco come endpoint HTTP, per fermare il tempo da qualcosa che
// non sia una pagina web: un pulsantone arcade, un ESP32, un tasto macro, uno script.
//
// Gli endpoint sono SENZA AUTENTICAZIONE e senza antiforgery: è una scelta voluta,
// perché un microcontrollore non può gestire né login né token. Vale finché
// l'applicazione resta su una rete locale di cui si ha il controllo: non va esposta
// su Internet.
static void MapGameApi(WebApplication app)
{
    var api = app.MapGroup("/api").DisableAntiforgery();

    // GET|POST /api/freeze — ferma il tempo. Il tempo riparte solo dalla regia.
    api.MapMethods("/freeze", new[] { "GET", "POST" }, (GameService game) =>
    {
        game.FreezeClock();

        return Results.Json(new
        {
            ok = true,
            stato = game.Status.ToString(),
            rimasti = game.RemainingSeconds,
            punti = game.Score
        });
    });

    // GET /api/stato — fotografia della partita, per pannelli e display esterni.
    // La parola a schermo non viene mai restituita: la vedono solo i suggeritori.
    api.MapGet("/stato", (GameService game) => Results.Json(new
    {
        stato = game.Status.ToString(),
        rimasti = game.RemainingSeconds,
        punti = game.Score,
        valoreParola = game.CanJudge ? game.CurrentValue : (int?)null,
        giocate = game.PlayedCount,
        giuste = game.CorrectCount,
        sbagliate = game.WrongCount,
        passate = game.PassedCount,
        passiRimasti = game.PassesRemaining,
        raddoppiRimasti = game.DoublesAllowedRemaining,
        fine = game.EndReason == FinishReason.None ? null : game.EndReason.ToString()
    }));
}
