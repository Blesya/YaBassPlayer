-- Схема БД версии 5: TrackId глобально уникален (inline UNIQUE), источник уже есть
-- в колонке SourceType, listensHistory хранит контекст в колонке source,
-- справочники Artists/Albums/TrackArtists ещё без SourceType.
CREATE TABLE Tracks (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    TrackId        TEXT UNIQUE,
    Artist         TEXT,
    Title          TEXT,
    Album          TEXT,
    RemoteCoverUrl TEXT,
    LocalCoverPath TEXT,
    SourceTrackId  TEXT,
    LocalFilePath  TEXT,
    UpdatedAt      INTEGER,
    DurationMs     INTEGER,
    Year           INTEGER,
    CoverUrl       TEXT,
    Genres         TEXT,
    AlbumId        TEXT,
    SourceType     TEXT DEFAULT 'yandex',
    FolderId       INTEGER,
    TrackNumber    INTEGER,
    Lyrics         TEXT
);

CREATE TABLE listensHistory (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    trackId          TEXT    NOT NULL,
    utcTime          TEXT    NOT NULL,
    utcOffsetMinutes INTEGER NOT NULL,
    source           TEXT    NOT NULL DEFAULT 'Regular'
);

CREATE TABLE Artists (
    Id          TEXT PRIMARY KEY,
    Name        TEXT NOT NULL,
    CoverUrl    TEXT,
    Description TEXT,
    UpdatedAt   INTEGER
);

CREATE TABLE Albums (
    Id          TEXT PRIMARY KEY,
    Title       TEXT NOT NULL,
    Year        INTEGER,
    CoverUrl    TEXT,
    Genre       TEXT,
    TrackCount  INTEGER,
    UpdatedAt   INTEGER
);

CREATE TABLE TrackArtists (
    TrackId  TEXT NOT NULL,
    ArtistId TEXT NOT NULL,
    PRIMARY KEY (TrackId, ArtistId)
);

CREATE TABLE favoriteLocalTracks (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    trackId TEXT UNIQUE NOT NULL,
    addedAt INTEGER NOT NULL
);
