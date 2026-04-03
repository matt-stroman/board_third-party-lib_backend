import test from "node:test";
import assert from "node:assert/strict";

import { buildStableSeedUuid, buildSupabaseReadyProbeHeaders } from "./migration-seed";

test("buildSupabaseReadyProbeHeaders includes the service-role api key for all probes", () => {
  const headers = buildSupabaseReadyProbeHeaders("service-role-secret");

  assert.deepEqual(headers, {
    apikey: "service-role-secret",
    authorization: "Bearer service-role-secret",
    accept: "application/json"
  });
});

test("buildStableSeedUuid stays deterministic and UUID-shaped for additive seed fixtures", () => {
  const first = buildStableSeedUuid("title-report:orbit-orchard:missing-release-notes");
  const second = buildStableSeedUuid("title-report:orbit-orchard:missing-release-notes");
  const different = buildStableSeedUuid("notification:orbit-orchard:player");

  assert.equal(first, second);
  assert.notEqual(first, different);
  assert.match(first, /^[0-9a-f]{8}-[0-9a-f]{4}-5[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
});
