-- Extensions required by HMIS modules (runs once when the data volume is first created).
CREATE EXTENSION IF NOT EXISTS pg_trgm;   -- fuzzy patient name search / duplicate detection
CREATE EXTENSION IF NOT EXISTS citext;    -- case-insensitive identifiers where needed
