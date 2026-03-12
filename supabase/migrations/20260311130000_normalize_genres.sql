create table if not exists public.genres (
    slug text primary key,
    display_name text not null unique,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.title_metadata_version_genres (
    title_id uuid not null,
    revision_number integer not null,
    genre_slug text not null references public.genres(slug) on delete restrict,
    display_order integer not null default 0,
    created_at timestamptz not null default now(),
    primary key (title_id, revision_number, genre_slug),
    constraint title_metadata_version_genres_metadata_fk
        foreign key (title_id, revision_number)
        references public.title_metadata_versions(title_id, revision_number)
        on delete cascade,
    constraint title_metadata_version_genres_display_order_nonnegative
        check (display_order >= 0)
);

create index if not exists idx_title_metadata_version_genres_lookup
    on public.title_metadata_version_genres(title_id, revision_number, display_order);

insert into public.genres (slug, display_name)
values
    ('adventure', 'Adventure'),
    ('arcade', 'Arcade'),
    ('cozy', 'Cozy'),
    ('collection', 'Collection'),
    ('co-op', 'Co-op'),
    ('community', 'Community'),
    ('companion', 'Companion'),
    ('competitive', 'Competitive'),
    ('crafting', 'Crafting'),
    ('creative', 'Creative'),
    ('dashboard', 'Dashboard'),
    ('delivery', 'Delivery'),
    ('exploration', 'Exploration'),
    ('family', 'Family'),
    ('festival', 'Festival'),
    ('harbor', 'Harbor'),
    ('management', 'Management'),
    ('planning', 'Planning'),
    ('platforming', 'Platforming'),
    ('puzzle', 'Puzzle'),
    ('qa', 'QA'),
    ('racing', 'Racing'),
    ('relaxing', 'Relaxing'),
    ('sandbox', 'Sandbox'),
    ('sci-fi', 'Sci-Fi'),
    ('simulation', 'Simulation'),
    ('strategy', 'Strategy'),
    ('survival', 'Survival'),
    ('tactics', 'Tactics'),
    ('travel', 'Travel'),
    ('utility', 'Utility'),
    ('workshop', 'Workshop')
on conflict (slug) do update
set
    display_name = excluded.display_name,
    updated_at = now();

with metadata_tokens as (
    select
        version.title_id,
        version.revision_number,
        trim(token.value) as display_name,
        lower(regexp_replace(trim(token.value), '[^a-z0-9]+', '-', 'g')) as genre_slug,
        token.ordinality - 1 as display_order
    from public.title_metadata_versions as version
    cross join lateral unnest(string_to_array(version.genre_display, ',')) with ordinality as token(value, ordinality)
    where trim(token.value) <> ''
),
missing_genres as (
    select distinct
        metadata_tokens.genre_slug as slug,
        metadata_tokens.display_name
    from metadata_tokens
    left join public.genres on public.genres.slug = metadata_tokens.genre_slug
    where public.genres.slug is null
)
insert into public.genres (slug, display_name)
select slug, display_name
from missing_genres
on conflict (slug) do update
set
    display_name = excluded.display_name,
    updated_at = now();

with metadata_tokens as (
    select
        version.title_id,
        version.revision_number,
        lower(regexp_replace(trim(token.value), '[^a-z0-9]+', '-', 'g')) as genre_slug,
        token.ordinality - 1 as display_order
    from public.title_metadata_versions as version
    cross join lateral unnest(string_to_array(version.genre_display, ',')) with ordinality as token(value, ordinality)
    where trim(token.value) <> ''
)
insert into public.title_metadata_version_genres (title_id, revision_number, genre_slug, display_order)
select
    metadata_tokens.title_id,
    metadata_tokens.revision_number,
    metadata_tokens.genre_slug,
    metadata_tokens.display_order
from metadata_tokens
on conflict (title_id, revision_number, genre_slug) do update
set display_order = excluded.display_order;

create or replace function public.reset_migration_demo_data()
returns void
language sql
security definer
as $$
    truncate table
        public.title_metadata_version_genres,
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

alter table public.genres enable row level security;
alter table public.title_metadata_version_genres enable row level security;
