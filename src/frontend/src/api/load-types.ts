// Interim shim — mirrors ResourcePulse.Services.Load.DailyLoadDto 1-to-1.
//
// The `LoadController` GETs now carry `[ProducesResponseType<T>]` on the
// backend, but this frontend was generated before that change, so the orval
// hooks for `/api/resources/{id}/load` are still typed `void`. We feed this
// type as the explicit `TData` to the generated query-options factory; the
// axios mutator already returns the parsed body at runtime. Delete this file
// and the explicit generics once `npm run generate:api` runs against a backend
// that has the annotations. Same finite-lifetime pattern as the former
// `people-types.ts` / `config.ts` shims.

// `Hours` is a .NET TimeSpan → ISO-8601 duration string on the wire (e.g.
// "PT8H", "PT7H30M"). Parse with `parseDurationHours` in the team load model.
export interface DailyLoadDto {
  date: string; // DateOnly → "YYYY-MM-DD"
  hours: string; // TimeSpan → ISO-8601 duration
  // decimal.MaxValue sentinel = "load on a zero-capacity day". The team grid
  // derives load from hours/capacity directly and never reads this field.
  loadPercent: number;
}
