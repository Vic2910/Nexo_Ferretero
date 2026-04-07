-- Agrega control de inventario en la tabla productos
-- Ejecutar en Supabase SQL Editor

ALTER TABLE public.productos
ADD COLUMN IF NOT EXISTS stock integer NOT NULL DEFAULT 0;

ALTER TABLE public.productos
ADD COLUMN IF NOT EXISTS stock_minimo integer NOT NULL DEFAULT 0;

-- Opcional: valida que no existan valores negativos
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'productos_stock_non_negative'
    ) THEN
        ALTER TABLE public.productos
        ADD CONSTRAINT productos_stock_non_negative CHECK (stock >= 0);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'productos_stock_minimo_non_negative'
    ) THEN
        ALTER TABLE public.productos
        ADD CONSTRAINT productos_stock_minimo_non_negative CHECK (stock_minimo >= 0);
    END IF;
END $$;
