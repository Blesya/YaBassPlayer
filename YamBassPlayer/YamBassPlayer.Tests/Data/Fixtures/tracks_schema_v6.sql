-- Схема БД версии 6: Tracks с составной уникальностью (SourceType, TrackId),
-- listensHistory хранит контекст в колонке source и не знает источник,
-- справочники Artists/Albums/TrackArtists ещё без SourceType,
-- локальное избранное живёт в таблице favoriteLocalTracks.
CREATE TABLE Tracks (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    TrackId        TEXT,
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
    Lyrics         TEXT,
    UNIQUE(SourceType, TrackId)
);

INSERT INTO Tracks (TrackId, Artist, Title, Album, AlbumId, SourceType, SourceTrackId) VALUES
    ('987654', 'Яндекс Артист', 'Яндекс Трек', 'Яндекс Альбом', '111', 'yandex', '987654'),
    ('C:\music\a.mp3', 'Локальный Артист', 'Локальный Трек', 'Локальный Альбом', '222', 'local', 'C:\music\a.mp3'),
    -- Один и тот же идентификатор из двух источников: ссылка TrackArtists на него
    -- неоднозначна, поэтому миграция не должна выбирать произвольную строку.
    ('777', 'Яндекс Артист', 'Дубль Яндекса', 'Яндекс Альбом', '111', 'yandex', '777'),
    ('777', 'Локальный Артист', 'Дубль Локальный', 'Локальный Альбом', '222', 'local', '777');

CREATE TABLE listensHistory (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    trackId          TEXT    NOT NULL,
    utcTime          TEXT    NOT NULL,
    utcOffsetMinutes INTEGER NOT NULL,
    source           TEXT    NOT NULL DEFAULT 'Regular'
);

INSERT INTO listensHistory (trackId, utcTime, utcOffsetMinutes, source) VALUES
    ('987654', '2024-01-15T18:00:00.0000000Z', 0, 'Regular'),
    ('C:\music\a.mp3', '2024-01-15T19:00:00.0000000Z', 0, 'MyWave'),
    ('unknown_track', '2024-01-15T20:00:00.0000000Z', 0, 'Weird'),
    ('777', '2024-01-15T21:00:00.0000000Z', 0, 'Regular');

CREATE TABLE Artists (
    Id          TEXT PRIMARY KEY,
    Name        TEXT NOT NULL,
    CoverUrl    TEXT,
    Description TEXT,
    UpdatedAt   INTEGER
);

INSERT INTO Artists (Id, Name) VALUES
    ('a-yandex', 'Яндекс Артист'),
    ('a-local', 'Локальный Артист'),
    ('a-orphan', 'Сирота');

CREATE TABLE Albums (
    Id          TEXT PRIMARY KEY,
    Title       TEXT NOT NULL,
    Year        INTEGER,
    CoverUrl    TEXT,
    Genre       TEXT,
    TrackCount  INTEGER,
    UpdatedAt   INTEGER
);

INSERT INTO Albums (Id, Title) VALUES
    ('111', 'Яндекс Альбом'),
    ('222', 'Локальный Альбом'),
    ('333', 'Альбом-сирота');

CREATE TABLE TrackArtists (
    TrackId  TEXT NOT NULL,
    ArtistId TEXT NOT NULL,
    PRIMARY KEY (TrackId, ArtistId)
);

INSERT INTO TrackArtists (TrackId, ArtistId) VALUES
    ('987654', 'a-yandex'),
    ('C:\music\a.mp3', 'a-local'),
    ('777', 'a-yandex'),
    ('missing_track', 'a-yandex');

CREATE TABLE favoriteLocalTracks (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    trackId TEXT UNIQUE NOT NULL,
    addedAt INTEGER NOT NULL
);

INSERT INTO favoriteLocalTracks (trackId, addedAt) VALUES
    ('C:\music\old.mp3', 1000),
    ('C:\music\newest.mp3', 3000),
    ('C:\music\middle.mp3', 2000);
