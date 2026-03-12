create table if not exists public.title_metadata_versions (
    title_id uuid not null references public.titles(id) on delete cascade,
    revision_number integer not null,
    is_current boolean not null default false,
    is_frozen boolean not null default false,
    display_name text not null,
    short_description text not null,
    description text not null,
    genre_display text not null,
    min_players integer not null,
    max_players integer not null,
    age_rating_authority text not null,
    age_rating_value text not null,
    min_age_years integer not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (title_id, revision_number),
    constraint title_metadata_versions_player_bounds check (min_players > 0 and max_players >= min_players)
);

create unique index if not exists idx_title_metadata_versions_current
    on public.title_metadata_versions(title_id)
    where is_current;

create table if not exists public.title_releases (
    id uuid primary key default gen_random_uuid(),
    title_id uuid not null references public.titles(id) on delete cascade,
    version text not null,
    status text not null check (status in ('draft', 'published', 'withdrawn')),
    metadata_revision_number integer not null,
    is_current boolean not null default false,
    published_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint title_releases_title_version_unique unique (title_id, version)
);

create unique index if not exists idx_title_releases_current
    on public.title_releases(title_id)
    where is_current;

create table if not exists public.release_artifacts (
    id uuid primary key default gen_random_uuid(),
    release_id uuid not null references public.title_releases(id) on delete cascade,
    artifact_kind text not null,
    package_name text not null,
    version_code bigint not null check (version_code > 0),
    sha256 text null,
    file_size_bytes bigint null check (file_size_bytes is null or file_size_bytes > 0),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.studio_integration_connections (
    id uuid primary key default gen_random_uuid(),
    studio_id uuid not null references public.studios(id) on delete cascade,
    supported_publisher_id uuid null references public.supported_publishers(id) on delete set null,
    custom_publisher_display_name text null,
    custom_publisher_homepage_url text null,
    configuration jsonb null,
    is_enabled boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint studio_integration_connections_provider_required check (
        supported_publisher_id is not null
        or custom_publisher_display_name is not null
    )
);

create table if not exists public.title_integration_bindings (
    id uuid primary key default gen_random_uuid(),
    title_id uuid not null references public.titles(id) on delete cascade,
    integration_connection_id uuid not null references public.studio_integration_connections(id) on delete cascade,
    acquisition_url text not null,
    acquisition_label text null,
    configuration jsonb null,
    is_primary boolean not null default false,
    is_enabled boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists idx_title_integration_bindings_primary
    on public.title_integration_bindings(title_id)
    where is_primary;

insert into public.supported_publishers (id, key, display_name, homepage_url)
values
    ('11111111-1111-1111-1111-111111111111', 'itch-io', 'itch.io', 'https://itch.io'),
    ('22222222-2222-2222-2222-222222222222', 'humble', 'Humble', 'https://www.humblebundle.com')
on conflict (id) do update
set
    key = excluded.key,
    display_name = excluded.display_name,
    homepage_url = excluded.homepage_url;

create or replace function public.reset_migration_demo_data()
returns void
language sql
security definer
as $$
    truncate table
        public.title_integration_bindings,
        public.studio_integration_connections,
        public.release_artifacts,
        public.title_releases,
        public.title_metadata_versions,
        public.title_report_messages,
        public.title_reports,
        public.player_wishlist_titles,
        public.player_library_titles,
        public.title_media_assets,
        public.titles,
        public.studio_links,
        public.studio_memberships,
        public.studios,
        public.user_board_profiles,
        public.app_user_roles,
        public.app_users
    restart identity cascade;
$$;

alter table public.title_metadata_versions enable row level security;
alter table public.title_releases enable row level security;
alter table public.release_artifacts enable row level security;
alter table public.studio_integration_connections enable row level security;
alter table public.title_integration_bindings enable row level security;
