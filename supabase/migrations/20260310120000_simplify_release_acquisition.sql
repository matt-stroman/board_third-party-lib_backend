alter table public.title_releases
    add column if not exists acquisition_url text null;

update public.title_releases as release
set acquisition_url = title.acquisition_url
from public.titles as title
where release.title_id = title.id
  and release.is_current
  and release.acquisition_url is null
  and title.acquisition_url is not null;

drop table if exists public.title_integration_bindings;
drop table if exists public.studio_integration_connections;
drop table if exists public.supported_publishers;

alter table public.titles drop column if exists acquisition_label;
alter table public.titles drop column if exists acquisition_provider_display_name;
alter table public.titles drop column if exists acquisition_provider_homepage_url;

create or replace function public.reset_migration_demo_data()
returns void
language sql
security definer
as $$
    truncate table
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
