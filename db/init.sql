
CREATE TABLE IF NOT EXISTS ships (
    mmsi BIGINT PRIMARY KEY,
    ship_name TEXT,
    last_latitude DOUBLE PRECISION,
    last_longitude DOUBLE PRECISION,
    sog DOUBLE PRECISION,
    cog DOUBLE PRECISION,
    true_heading INTEGER,
    navigational_status INTEGER,
    last_update TIMESTAMPTZ,
    is_plausible BOOLEAN DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS position_history (
    id SERIAL PRIMARY KEY,
    mmsi BIGINT,
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    recorded_time TIMESTAMPTZ,
    is_plausible BOOLEAN DEFAULT TRUE,
    FOREIGN KEY (mmsi) REFERENCES ships(mmsi)
);

CREATE INDEX IF NOT EXISTS idx_position_history_mmsi_time
    ON position_history (mmsi, recorded_time DESC);
