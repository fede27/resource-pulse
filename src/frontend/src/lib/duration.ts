// Wire-duration parsing, shared across features (teams heatmap, projects board).
//
// This backend serializes TimeSpan in the constant format ("09:00:00"); we also
// accept ISO-8601 ("PT8H") defensively. Do NOT introduce a decimal-hours
// converter — all TimeSpan DTOs must stay in lockstep (backend CLAUDE.md).
export function parseDurationHours(s: string | null | undefined): number {
  if (!s) return 0;
  const iso = /^(-)?P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:([\d.]+)S)?)?$/i.exec(s);
  if (iso) {
    const sign = iso[1] ? -1 : 1;
    return (
      sign *
      (Number(iso[2] ?? 0) * 24 +
        Number(iso[3] ?? 0) +
        Number(iso[4] ?? 0) / 60 +
        Number(iso[5] ?? 0) / 3600)
    );
  }
  const c = /^(-)?(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(s);
  if (c) {
    const sign = c[1] ? -1 : 1;
    return (
      sign *
      (Number(c[2] ?? 0) * 24 +
        Number(c[3] ?? 0) +
        Number(c[4] ?? 0) / 60 +
        Number(c[5] ?? 0) / 3600)
    );
  }
  return 0;
}
