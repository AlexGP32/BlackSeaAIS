# PortConstantaTracker

## Descriere

O aplicație C# care se conectează la stream-ul AIS de la aisstream.io, urmărește nave din Marea Neagră în timp real, și salvează pozițiile lor curente + istoricul de mișcare într-o bază de date Postgres (momentan).

## Tehnologii

- C# / .NET
- Npgsql
- WebSocket
- Postgres / Supabase

## Rulare

### 1. Cheie API aisstream.io

Creează un cont gratuit pe [aisstream.io](https://aisstream.io) și generează o cheie API.

### 2. Bază de date Postgres

Creează un proiect propriu pe [Supabase](https://supabase.com) (sau folosește orice instanță Postgres). Rulează SQL-ul din secțiunea **Structura bazei de date** de mai jos, ca să creezi tabelele necesare, înainte de prima rulare.

### 3. Variabile de mediu

Creează un nou fisier în folderul 'BlackSeaAIS' denumit '.env' si setează  următoarele trei variabile de mediu:

| Variabilă | Descriere |
|---|---|
| `AISSTREAM_API_KEY` | Cheia API obținută de pe aisstream.io |
| `PASSWORD` | Parola bazei tale de date Supabase |
| SUPABASE_CONNECTION_STRING | stringul de conectare din supabase


### 4. Rulare aplicație

```bash
dotnet run
```

## Structura bazei de date

### Tabelul `ships`

Ține **ultima poziție cunoscută** pentru fiecare navă (upsert pe MMSI).

```sql
CREATE TABLE IF NOT EXISTS ships (
    mmsi BIGINT PRIMARY KEY,
    ship_name TEXT,
    last_latitude DOUBLE PRECISION,
    last_longitude DOUBLE PRECISION,
    sog DOUBLE PRECISION,
    cog DOUBLE PRECISION,
    true_heading INTEGER,
    navigational_status INTEGER,
    last_update TIMESTAMPTZ
);
```

### Tabelul `position_history`

Ține **istoricul complet** de poziții per navă (un rând nou pentru fiecare mesaj primit).

```sql
CREATE TABLE IF NOT EXISTS position_history (
    id SERIAL PRIMARY KEY,
    mmsi BIGINT,
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    recorded_time TIMESTAMPTZ,
    FOREIGN KEY (mmsi) REFERENCES ships(mmsi)
);
```

## Funcționalități

- Conectare live la stream-ul AIS, filtrat pe o zonă geografică (bounding box) din Marea Neagră
- Deserializare a mesajelor AIS în obiecte C# tipizate
- Upsert automat al ultimei poziții per navă
- Salvare a istoricului complet de mișcare
- Reconectare automată la WebSocket în caz de întrerupere a conexiunii
- Gestionare a erorilor de deserializare și de bază de date, fără oprirea programului