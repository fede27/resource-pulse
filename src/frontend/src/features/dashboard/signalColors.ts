// Dashboard palette. Hex only ever lives in app/palette.ts (lint-enforced), so
// this file maps triage concepts onto those ramps and nothing else.

import { blue, geekblue, gold, neutral, red, volcano } from '@/app/palette';
import { SignalZone } from '@/api/generated/schemas';
import type { ChangeVerb, SignalTone } from './dashboardModel';

export type ToneColors = { fg: string; bg: string; border: string };

// Tone follows the KIND. A gap is the loudest thing on the page because an
// uncovered role is the only condition here that nobody has decided about yet.
export const TONE: Record<SignalTone, ToneColors> = {
  danger: { fg: volcano[7], bg: volcano[1], border: volcano[3] },
  warning: { fg: gold[8], bg: gold[1], border: gold[3] },
  caution: { fg: red[6], bg: red[1], border: red[3] },
  info: { fg: geekblue[6], bg: geekblue[1], border: geekblue[3] },
  neutral: { fg: neutral.textStrong, bg: neutral.bg, border: neutral.fillSubtle },
};

// The zone stripe. One hue, four weights: the zone is an ORDERED axis (overdue →
// frozen → slushy → liquid), and using four different hues would suggest four
// unrelated categories instead of a single ramp of urgency.
export const ZONE_STRIPE: Record<SignalZone, string> = {
  [SignalZone.Overdue]: red[5],
  [SignalZone.Frozen]: blue[6],
  [SignalZone.Slushy]: blue[3],
  [SignalZone.Liquid]: neutral.disabled,
};

// A signal with no deadline gets no stripe at all — absence of urgency, rendered
// as absence rather than as a fifth colour.
export const NO_ZONE_STRIPE = 'transparent';

// The change-feed verbs.
export const VERB: Record<ChangeVerb, ToneColors> = {
  new: { fg: volcano[7], bg: volcano[1], border: volcano[3] },
  worsened: { fg: red[6], bg: red[1], border: red[3] },
  crossed: { fg: gold[8], bg: gold[1], border: gold[3] },
  resolved: { fg: neutral.textStrong, bg: neutral.bg, border: neutral.fillSubtle },
};
