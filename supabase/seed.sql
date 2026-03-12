insert into public.migration_wave_state(key, value)
values
    ('stack', 'workers-supabase'),
    ('status', 'configured')
on conflict (key) do update
set value = excluded.value,
    updated_at = now();

