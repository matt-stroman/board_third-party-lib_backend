# Worker Email Templates

This directory contains BE-branded email templates rendered directly by the Workers API for transactional flows that do not go through Supabase Auth's built-in email system.

Current templates:

- `marketing-signup-welcome.ts`

Guidance:

- Keep the visual language aligned with [`backend/supabase/templates/`](../../../supabase/templates/).
- Keep copy changes in the template modules rather than re-inlining them into [`service-boundary.ts`](../service-boundary.ts).
- When practical, render both plain-text and HTML variants from the same template module so local Mailpit previews and provider sends stay consistent.
