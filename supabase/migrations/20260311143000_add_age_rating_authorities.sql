create table if not exists public.age_rating_authorities (
    code text primary key,
    display_name text not null unique,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

insert into public.age_rating_authorities (code, display_name)
values
    ('ESRB', 'ESRB'),
    ('PEGI', 'PEGI'),
    ('USK', 'USK'),
    ('CERO', 'CERO'),
    ('ACB', 'ACB')
on conflict (code) do update
set
    display_name = excluded.display_name,
    updated_at = now();

alter table public.age_rating_authorities enable row level security;
