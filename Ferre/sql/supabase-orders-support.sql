-- Pedidos (cabecera)
create extension if not exists pgcrypto;

create table if not exists public.client_purchases (
    id uuid primary key default gen_random_uuid(),
    user_email text not null,
    receipt_number text not null unique,
    created_at_utc timestamptz not null default timezone('utc', now()),
    payment_method text not null,
    status text not null,
    total numeric(12,2) not null default 0,
    constraint client_purchases_status_chk check (lower(status) in ('pendiente', 'pagado', 'cancelado')),
    constraint client_purchases_total_chk check (total >= 0)
);

create index if not exists ix_client_purchases_user_email on public.client_purchases (user_email);
create index if not exists ix_client_purchases_created_at_utc on public.client_purchases (created_at_utc desc);

-- Pedidos (detalle)
create table if not exists public.client_purchase_lines (
    id uuid primary key default gen_random_uuid(),
    purchase_id uuid not null references public.client_purchases(id) on delete cascade,
    product_id uuid not null,
    product_name text not null,
    quantity integer not null,
    unit_price numeric(12,2) not null,
    line_total numeric(12,2) not null,
    created_at_utc timestamptz not null default timezone('utc', now()),
    constraint client_purchase_lines_quantity_chk check (quantity > 0),
    constraint client_purchase_lines_unit_price_chk check (unit_price >= 0),
    constraint client_purchase_lines_line_total_chk check (line_total >= 0)
);

create index if not exists ix_client_purchase_lines_purchase_id on public.client_purchase_lines (purchase_id);

-- Mensajes del formulario de contacto
create table if not exists public.client_contact_messages (
    id uuid primary key default gen_random_uuid(),
    conversation_id uuid not null default gen_random_uuid(),
    created_at_utc timestamptz not null default timezone('utc', now()),
    name text not null,
    email text not null,
    subject text not null,
    "message" text not null,
    sender_role text not null default 'cliente',
    status text not null default 'pendiente',
    is_system_event boolean not null default false,
    constraint client_contact_messages_sender_role_chk check (lower(sender_role) in ('cliente', 'administrador')),
    constraint client_contact_messages_status_chk check (lower(status) in ('pendiente', 'resuelto'))
);

create index if not exists ix_client_contact_messages_created_at_utc on public.client_contact_messages (created_at_utc desc);

-- Compatibilidad para instalaciones previas sin defaults en IDs
alter table public.client_purchases alter column id set default gen_random_uuid();
alter table public.client_purchase_lines alter column id set default gen_random_uuid();
alter table public.client_contact_messages alter column id set default gen_random_uuid();
alter table public.client_contact_messages add column if not exists conversation_id uuid;
update public.client_contact_messages set conversation_id = id where conversation_id is null;
alter table public.client_contact_messages alter column conversation_id set default gen_random_uuid();
alter table public.client_contact_messages alter column conversation_id set not null;
alter table public.client_contact_messages add column if not exists sender_role text;
update public.client_contact_messages set sender_role = 'cliente' where sender_role is null;
alter table public.client_contact_messages alter column sender_role set default 'cliente';
alter table public.client_contact_messages alter column sender_role set not null;
alter table public.client_contact_messages add column if not exists status text;
update public.client_contact_messages set status = 'pendiente' where status is null;
alter table public.client_contact_messages alter column status set default 'pendiente';
alter table public.client_contact_messages alter column status set not null;
alter table public.client_contact_messages add column if not exists is_system_event boolean;
update public.client_contact_messages set is_system_event = false where is_system_event is null;
alter table public.client_contact_messages alter column is_system_event set default false;
alter table public.client_contact_messages alter column is_system_event set not null;
create index if not exists ix_client_contact_messages_conversation_id on public.client_contact_messages (conversation_id);

-- Grants explícitos para evitar errores de permisos con anon/authenticated
grant usage on schema public to anon, authenticated;
grant select, insert, update, delete on table public.client_purchases to anon, authenticated;
grant select, insert, update, delete on table public.client_purchase_lines to anon, authenticated;
grant select, insert, update, delete on table public.client_contact_messages to anon, authenticated;

-- RLS (si se usa anon key desde el backend)
alter table public.client_purchases enable row level security;
alter table public.client_purchase_lines enable row level security;
alter table public.client_contact_messages enable row level security;

-- Políticas para permitir operaciones al backend (anon/authenticated)
drop policy if exists "client_purchases_all_backend" on public.client_purchases;
create policy "client_purchases_all_backend"
on public.client_purchases
for all
to anon, authenticated
using (true)
with check (true);

drop policy if exists "client_purchase_lines_all_backend" on public.client_purchase_lines;
create policy "client_purchase_lines_all_backend"
on public.client_purchase_lines
for all
to anon, authenticated
using (true)
with check (true);

drop policy if exists "client_contact_messages_all_backend" on public.client_contact_messages;
create policy "client_contact_messages_all_backend"
on public.client_contact_messages
for all
to anon, authenticated
using (true)
with check (true);
