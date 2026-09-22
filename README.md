# 🎮 L'Intesa Vincente

Versione giocabile del penultimo gioco di *Reazione a Catena*, da giocare in casa con
più schermi: **due suggeritori** leggono la parola e la fanno indovinare a un terzo
concorrente, mentre la regia conduce e tiene il punteggio.

Scritto in **Blazor Server (.NET 10)**. Nessun database, nessun account, nessuna
connessione a Internet necessaria: basta una rete locale.

---

## Indice

- [Come si avvia](#come-si-avvia)
- [Le quattro schermate](#le-quattro-schermate)
- [Giocare su più schermi](#giocare-su-più-schermi)
- [Le regole](#le-regole)
- [Come si conduce una partita](#come-si-conduce-una-partita)
- [Scorciatoie da tastiera](#scorciatoie-da-tastiera)
- [Le parole](#le-parole)
- [Fermare il tempo da fuori](#fermare-il-tempo-da-fuori)
- [Configurazione](#configurazione)
- [Com'è fatto il progetto](#comè-fatto-il-progetto)
- [Nota sulla sicurezza](#nota-sulla-sicurezza)

---

## Come si avvia

### Con l'eseguibile pronto (consigliato per giocare)

Scarica lo `.zip` dalla pagina [Releases](../../releases), estrailo dove vuoi e fai
doppio clic su **`IntesaVincente.exe`**. Non serve installare .NET.

Poi apri il browser su **<http://localhost:5080>**.

### Dai sorgenti (per chi sviluppa)

Serve l'[SDK .NET 10](https://dotnet.microsoft.com/download).

```bash
dotnet run
```

> ⚠️ Alla prima esecuzione Windows chiede di **autorizzare l'app nel firewall**:
> consenti l'accesso alle **reti private**, altrimenti gli altri dispositivi non
> riescono a collegarsi.

---

## Le quattro schermate

| Schermata | Indirizzo | A cosa serve | Dove aprirla |
|---|---|---|---|
| **Menu** | `/` | I link alle altre schermate e gli indirizzi di rete già pronti | dove capita |
| **Regia** | `/admin` | Configura, giudica le risposte, tiene il punteggio | sul PC che conduce |
| **Display** | `/display` | La parola, il tempo e il punteggio | schermo dei **soli suggeritori** |
| **Telecomando** | `/remote` | Un pulsante grande per fermare il tempo | telefono o tablet |

> 🚫 Il **Display** non va mostrato a chi deve indovinare: è lì che compare la parola.

---

## Giocare su più schermi

1. Avvia l'app sul PC che conduce e apri **`/admin`**.
2. Il **menu** (`/`) mostra già gli indirizzi da usare sugli altri dispositivi,
   tipo `http://192.168.1.50:5080/display`: non serve cercarli con `ipconfig`.
   Il primo della lista è quello giusto anche se hai schede di rete virtuali.
3. Sullo schermo dei suggeritori apri quell'indirizzo e metti il browser a schermo
   intero con **F11**.
4. Per il telecomando, in regia c'è un **codice QR**: inquadralo col telefono e si
   apre direttamente il pulsante STOP.

Tutti gli schermi restano sincronizzati. Se un dispositivo perde la rete per un
momento, si ricollega da solo: **la partita vive nel server**, non nel browser.

---

## Le regole

- **Tempo**: 60 secondi di default, configurabile. Scorre **solo mentre la parola è a
  schermo**: fra un giudizio e la parola successiva è fermo, così il tempo che la regia
  impiega a decidere non viene conteggiato. La partita finisce sempre allo scadere del tempo.
- **Punteggio**: risposta giusta **+1**, sbagliata **−1**, passo **0**.
  Il punteggio **non scende mai sotto zero**.
- **Passi**: 3 di default, configurabili. Finiti i passi, il pulsante si disattiva.
- **Parole da 2 punti** ("la chiamata"): valgono **+2 / −2** e si possono chiamare
  **solo con almeno 2 punti**, così una risposta sbagliata non può mai portare in
  negativo. Sono anche limitate nel numero: 3 raddoppi di default.
- **Parole finite**: se non resta più niente da mandare, la partita si chiude con
  *Parole finite* invece di aspettare lo scadere del tempo.

### Il valore di una parola si deduce dal testo

Non c'è nessun marcatore da scrivere: conta quante parole ci sono nella riga.

| Riga | Vale |
|---|---|
| `Tavolo` | **1 punto** |
| `Lago di Garda` | **2 punti** |

---

## Come si conduce una partita

1. Scegli i **temi** (o carica un file, o incolla le parole) e applica le **regole**.
2. **▶ Avvia partita** → la prima parola da 1 punto appare sul Display e parte il tempo.
3. Quando il concorrente risponde, premi **✅ Giusta**, **❌ Sbagliata** o **⏭ Passo**:
   il tempo **si ferma** e il punteggio si aggiorna.
4. **▶ Parola ×1** manda la parola successiva e **fa ripartire il tempo**;
   **▶▶ Chiamata ×2** manda una parola da 2 punti (da 2 punti in su).
5. Ripeti dal punto 3 fino allo scadere del tempo.

### Fermare e riprendere il tempo

- Dal **telecomando** sul telefono, o dal pulsante **⏸ Ferma tempo** in regia.
- **▶ Riprendi tempo** fa ripartire il tempo sulla stessa parola, senza assegnare punti.
- A tempo fermo si può anche giudicare direttamente la parola.

---

## Scorciatoie da tastiera

In regia, per non staccare le mani dalla tastiera mentre si conduce:

| Tasto | Cosa fa |
|---|---|
| `G` | risposta giusta |
| `S` | risposta sbagliata |
| `P` | passo |
| `Invio` | avvia la partita, poi manda la prossima parola ×1 |
| `D` | chiama la parola ×2 |
| `Spazio` | ferma o riprende il tempo |

Non valgono mentre stai scrivendo in un campo di testo, così configurare la partita
non fa scattare giudizi per sbaglio.

---

## Le parole

Nella cartella [`temi/`](temi) ci sono **20 temi da 600 parole ciascuno**
(**12.000 parole** in tutto): 500 da 1 punto e 100 da 2 punti per tema.

Casa e vita quotidiana, natura, animali, cibo, sport, musica e spettacolo, geografia,
scienza e scuola, corpo e mestieri, feste e fantasia, azioni, aggettivi, emozioni,
lavoro e denaro, abbigliamento, trasporti, storia antica, arte e letteratura, spazio,
mondo dei bambini.

Si possono selezionare anche **più temi insieme**: le parole vengono unite.

### Formato di un file di parole

Un file `.txt`, una voce per riga:

```
# TEMA: Casa e vita quotidiana
# Righe vuote e righe che iniziano con # vengono ignorate.

Tavolo
Sedia
Ferro da stiro
Macchina del caffè
```

- la riga `# TEMA: Nome` dà il nome leggibile al tema;
- le righe vuote e quelle che iniziano con `#` sono ignorate;
- **una parola** per riga vale 1 punto, **due o più** valgono 2 punti;
- l'ordine dentro al file non conta: in regia si può scegliere *mescolate*.

Per aggiungere un tema basta mettere un nuovo `.txt` in `temi/` e riavviare.
In alternativa, dalla regia puoi **caricare un file** dal tuo computer o
**incollare le parole** a mano senza toccare nessun file.

---

## Fermare il tempo da fuori

### Dal telefono

Apri `/remote`, o inquadra il **codice QR** che trovi in regia: compare un pulsante
STOP grande quanto lo schermo, col tempo che resta. Il tempo riparte solo dalla regia.

### Via HTTP (pulsantiera, ESP32, script, tasto macro)

| Richiesta | Cosa fa |
|---|---|
| `GET`/`POST` `/api/freeze` | ferma il tempo |
| `GET` `/api/stato` | fotografia della partita |

```bash
curl http://192.168.1.50:5080/api/freeze
# {"ok":true,"stato":"Frozen","rimasti":42,"punti":3}

curl http://192.168.1.50:5080/api/stato
# {"stato":"Live","rimasti":37,"punti":3,"valoreParola":1,"giocate":4,...}
```

`/api/stato` **non restituisce mai la parola**: quella la vedono solo i suggeritori.

#### Esempio: pulsantone arcade con ESP32 (Arduino IDE)

Collega un pulsante fra il pin `GPIO 4` e `GND`. Sostituisci i valori segnaposto con
il nome della tua rete, la sua password e l'IP del PC che fa da server.

```cpp
#include <WiFi.h>
#include <HTTPClient.h>

const char* SSID = "NOME-DELLA-TUA-RETE";
const char* PASS = "PASSWORD-DELLA-TUA-RETE";
const char* URL  = "http://INDIRIZZO-IP-DEL-PC:5080/api/freeze";
const int   PIN  = 4;

void setup() {
  pinMode(PIN, INPUT_PULLUP);
  WiFi.begin(SSID, PASS);
  while (WiFi.status() != WL_CONNECTED) delay(300);
}

void loop() {
  if (digitalRead(PIN) == LOW) {   // pulsante premuto
    HTTPClient http;
    http.begin(URL);
    http.GET();                     // ferma il tempo
    http.end();
    delay(400);                     // anti-rimbalzo
  }
}
```

---

## Configurazione

La porta si cambia in [`appsettings.json`](appsettings.json):

```json
{
  "Server": {
    "Port": 5080
  }
}
```

oppure con una variabile d'ambiente, senza toccare i file:

```powershell
$env:Server__Port = "8080"; .\IntesaVincente.exe
```

L'app resta in ascolto su **tutte** le schede di rete, così gli altri dispositivi la
raggiungono. La porta 5080 lascia libera la 5090, usata dal progetto gemello
**La Ruota della Fortuna**: le due app possono girare insieme sullo stesso PC.

---

## Com'è fatto il progetto

```
Models/                    tipi di dati, senza logica
  WordSet, ThemeInfo, enum di stato

Services/
  WordLibrary.cs           legge e classifica i file delle parole, con cache per tema
  GameService.cs           lo stato della partita, condiviso da tutte le pagine
  NetworkInfo.cs           trova gli indirizzi di rete da suggerire

Components/
  Pages/                   Home, Admin, Display, Remote (+ CSS e JS accanto)
  Layout/                  cornici delle pagine

wwwroot/app.css            colori, tipografia e spaziature in un posto solo
wwwroot/gong.js            il gong di fine tempo
temi/                      i 20 temi di parole
```

Tre idee portanti:

1. **`GameService` è un singleton** con lo stato della partita. Ogni pagina si iscrive
   al suo evento `OnChange` e si ridisegna: è tutto il "tempo reale" che serve, senza
   scrivere nemmeno un'API. Le scritture passano da un lock, perché le pagine aperte
   sono più di una e gli endpoint HTTP arrivano da altri thread.

2. **Il cronometro va a scadenza, non a decrementi.** Si tiene un cronometro monotono e
   i secondi che restavano quando è stato fermato: i secondi rimanenti si *calcolano*.
   Decrementare un contatore a ogni tick accumulerebbe deriva e sbaglierebbe ogni volta
   che un tick arriva in ritardo — e su 60 secondi di gioco si nota.

3. **Niente framework CSS e niente font esterni.** La grafica è CSS scritto a mano, con
   i colori e le misure come variabili in `wwwroot/app.css` e un file *scoped* per
   pagina. Così l'app funziona identica anche senza Internet, che è il caso normale in
   una sala.

### Compilare e pubblicare

```bash
dotnet build                      # compilazione (deve restare a 0 warning)
dotnet run                        # avvia in locale
dotnet publish -c Release         # eseguibile autonomo per Windows x64
```

Il profilo di pubblicazione è in
[`Properties/PublishProfiles/FolderProfile.pubxml`](Properties/PublishProfiles/FolderProfile.pubxml)
e produce la cartella `publish-win-x64-self-contained/`, che gira **senza .NET installato**.

---

## Nota sulla sicurezza

L'app **non ha autenticazione**: chiunque sia sulla stessa rete può aprire `/admin` e
usare gli endpoint `/api/*`. È voluto — un microcontrollore non può gestire un login, e
in casa non serve — ma vuol dire che:

- va usata su una **rete locale di cui hai il controllo**;
- **non va esposta su Internet** né aperta sul router.

Nel repository non ci sono password, chiavi o token: i file di configurazione contengono
soltanto il livello dei log e la porta.

---

## Licenza

Progetto personale, per giocare in famiglia. Il format televisivo e il nome appartengono
ai rispettivi proprietari; questo è un gioco fatto in casa, senza alcun legame con la
trasmissione.
