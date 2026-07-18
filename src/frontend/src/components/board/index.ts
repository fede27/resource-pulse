// Shared board-timeline building blocks (bounded-domain gantt): pixel geometry,
// the scrollable timeline shell (fence zones + today indicator + axis), the
// date-navigation controls and the fence palette. Used by the Progetti and
// Persone boards — do not re-implement these per feature.

export {
  BUCKET_DAYPX,
  buildGeo,
  fenceEnd,
  isoWeek,
  mondayOf,
  type BoardGeo,
  type FenceBoundaries,
  type MajorBand,
  type UnitTick,
} from './boardGeo';
export { BoardTimeline, type BoardTimelineProps } from './BoardTimeline';
export { UNBOUNDED_X, useVisibleXRange, type VisibleXRange } from './useVisibleXRange';
export { UNBOUNDED_Y, useVisibleYRange, type VisibleYRange } from './useVisibleYRange';
export {
  windowRows,
  type RowItem,
  type RowSegment,
  type WindowedRows,
} from './boardRowLayout';
export { RowGap, useWindowedRows } from './useWindowedRows';
export {
  BoardDateControls,
  type BoardDateControlsProps,
  type BoardDomain,
} from './BoardDateControls';
export {
  ENVELOPE_H,
  HEADER_FENCE_H,
  HEADER_MAJOR_H,
  HEADER_TICKS_H,
  LANE_H,
  LEFT_W,
  PAST_HATCH,
  PAST_HATCH_STRONG,
} from './BoardTimeline.styles';
