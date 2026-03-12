create table if not exists public.player_library_titles (
    user_id uuid not null references public.app_users(id) on delete cascade,
    title_id uuid not null references public.titles(id) on delete cascade,
    created_at timestamptz not null default now(),
    primary key (user_id, title_id)
);

create table if not exists public.player_wishlist_titles (
    user_id uuid not null references public.app_users(id) on delete cascade,
    title_id uuid not null references public.titles(id) on delete cascade,
    created_at timestamptz not null default now(),
    primary key (user_id, title_id)
);

create table if not exists public.title_reports (
    id uuid primary key default gen_random_uuid(),
    title_id uuid not null references public.titles(id) on delete cascade,
    reporter_user_id uuid not null references public.app_users(id) on delete cascade,
    status text not null check (status in (
        'open',
        'needs_developer_response',
        'needs_player_response',
        'developer_responded',
        'player_responded',
        'validated',
        'invalidated'
    )),
    reason text not null,
    resolution_note text null,
    resolved_by_user_id uuid null references public.app_users(id) on delete set null,
    resolved_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.title_report_messages (
    id uuid primary key default gen_random_uuid(),
    report_id uuid not null references public.title_reports(id) on delete cascade,
    author_user_id uuid not null references public.app_users(id) on delete cascade,
    author_role text not null check (author_role in ('player', 'developer', 'moderator')),
    audience text not null check (audience in ('all', 'player', 'developer')),
    message text not null,
    created_at timestamptz not null default now()
);

create index if not exists idx_player_library_titles_user_id on public.player_library_titles(user_id);
create index if not exists idx_player_wishlist_titles_user_id on public.player_wishlist_titles(user_id);
create index if not exists idx_title_reports_reporter_user_id on public.title_reports(reporter_user_id);
create index if not exists idx_title_reports_title_id on public.title_reports(title_id);
create index if not exists idx_title_reports_status on public.title_reports(status);
create index if not exists idx_title_report_messages_report_id on public.title_report_messages(report_id);

create or replace function public.reset_migration_demo_data()
returns void
language sql
security definer
as $$
    truncate table
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

alter table public.player_library_titles enable row level security;
alter table public.player_wishlist_titles enable row level security;
alter table public.title_reports enable row level security;
alter table public.title_report_messages enable row level security;
