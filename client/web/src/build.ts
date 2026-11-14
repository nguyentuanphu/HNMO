// The HNMO web client build stamp: a compiled-in ownership marker that ends up in the
// built JS. It mirrors the server's build stamp (see server/src/Integrity/HnmoBuildStamp.cs
// and PROJECT_ID.txt at the repository root). See client/web/tools/hnmo/PROTECTION.md for how
// to read it from a suspect build.

/** The fixed project identifier for this build (kept in PROJECT_ID.txt). */
export const HNMO_PROJECT_ID = '1abdb72e-f72e-457c-93d5-3141ad991109';

/** The date (UTC, yyyyMMdd) this build stamp was minted. */
export const HNMO_BUILD_DATE = '20261114';

/** The full build stamp string compiled into the web client. */
export const HNMO_BUILD_STAMP = `HNMO-${HNMO_PROJECT_ID}-${HNMO_BUILD_DATE}`;
