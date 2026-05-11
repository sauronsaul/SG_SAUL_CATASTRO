-- ============================================================
-- SG_SAUL_CATASTRO — Extensiones PostgreSQL
-- Ejecutado automáticamente al primer arranque del contenedor
-- ============================================================

-- Geometría espacial (PostGIS)
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;

-- Búsqueda de texto con trigrams (búsqueda aproximada por nombre/código)
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Normalización de texto con acentos (búsquedas insensibles a acentos)
CREATE EXTENSION IF NOT EXISTS unaccent;

-- Generación de UUIDs
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
