import { createStyles } from 'antd-style';
import { alpha, blue, neutral } from '@/app/palette';
import { ENVELOPE_H, LANE_H, LEFT_W } from '@/components/board/BoardTimeline.styles';

// Decorative-only patterns (not part of the token language).
export const TENTATIVE_HATCH = `repeating-linear-gradient(135deg, ${alpha(blue[5], 0.1)} 0 7px, ${alpha(blue[5], 0.02)} 7px 14px)`;
// Counter-hatch for the part of a block that lies outside its person's declared
// availability. Opposite slant to every other hatch on the boards, and only
// ever INSIDE a block: it is a fact about one person, not about the calendar.
export const BEYOND_AVAILABILITY_HATCH = `repeating-linear-gradient(-45deg, ${alpha(neutral.black, 0.5)} 0 1.5px, transparent 1.5px 5px)`;
// Hit area of a node-edge grip; the row clamps it inside the timeline.
export const EDGE_HANDLE_W = 10;
export const PROPOSED_HATCH = `repeating-linear-gradient(135deg, ${alpha(neutral.black, 0.03)} 0 8px, ${alpha(neutral.black, 0.005)} 8px 16px)`;

export const useStyles = createStyles(({ token, css }) => ({
  block: css`
    border-block-end: 1px solid ${token.colorBorderSecondary};
  `,
  envelopeRow: css`
    display: flex;
    align-items: stretch;
    height: ${ENVELOPE_H}px;
  `,
  envelopeRowAlt: css`
    background: rgba(0, 0, 0, 0.008);
  `,
  labelCell: css`
    width: ${LEFT_W}px;
    flex-shrink: 0;
    position: sticky;
    left: 0;
    z-index: 4;
    background: ${token.colorBgContainer};
    border-inline-end: 1px solid ${token.colorBorderSecondary};
    display: flex;
    height: 100%;
  `,
  labelCellLane: css`
    /* Opaque: the sticky label must fully cover bars scrolling underneath. */
    background: linear-gradient(${token.colorFillQuaternary}, ${token.colorFillQuaternary}),
      ${token.colorBgContainer};
  `,
  stripe: css`
    width: 4px;
    flex-shrink: 0;
  `,
  stripeSoft: css`
    opacity: 0.35;
  `,
  labelBody: css`
    flex: 1;
    min-width: 0;
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 0 ${token.paddingSM}px 0 2px;
  `,
  chevron: css`
    cursor: pointer;
    color: ${token.colorTextTertiary};
    padding: 4px;
    display: inline-flex;
    flex-shrink: 0;
    transition: transform 0.2s;
  `,
  chevronOpen: css`
    transform: rotate(90deg);
  `,
  labelMain: css`
    flex: 1;
    min-width: 0;
    cursor: pointer;
  `,
  nameLine: css`
    display: flex;
    align-items: center;
    gap: 6px;
  `,
  projectName: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  provChip: css`
    flex-shrink: 0;
    font-size: 9px;
    font-weight: 600;
    letter-spacing: 0.03em;
    text-transform: uppercase;
    color: ${token.colorTextTertiary};
    border: 1px solid ${token.colorBorder};
    border-radius: 3px;
    padding: 0 4px;
    line-height: 15px;
  `,
  provChipProposed: css`
    border-style: dashed;
  `,
  criticalDot: css`
    flex-shrink: 0;
    width: 6px;
    height: 6px;
    border-radius: 50%;
  `,
  verdictLine: css`
    display: flex;
    align-items: center;
    gap: 6px;
    margin-block-start: 4px;
  `,
  verdictBadge: css`
    display: inline-flex;
    align-items: center;
    gap: 4px;
    height: 18px;
    padding: 0 7px;
    border-radius: 4px;
    font-size: 11px;
    font-weight: 700;
    flex-shrink: 0;
  `,
  verdictNote: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  axisCell: css`
    flex-shrink: 0;
    position: relative;
  `,
  envelope: css`
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
    height: 38px;
    border-radius: ${token.borderRadius}px;
    cursor: pointer;
    overflow: hidden;
    display: flex;
    align-items: stretch;
    transition: box-shadow 0.15s;

    &:hover {
      box-shadow: ${token.boxShadowTertiary};
    }
  `,
  phaseSeg: css`
    position: absolute;
    top: 0;
    bottom: 0;
    display: flex;
    align-items: center;
    justify-content: center;

    span {
      font-size: 10px;
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      opacity: 0.7;
      white-space: nowrap;
    }
  `,
  envelopeMeta: css`
    display: flex;
    align-items: center;
    padding-inline-start: 10px;
    gap: ${token.marginXS}px;

    span {
      font-size: 11px;
      font-weight: 500;
      white-space: nowrap;
      font-variant-numeric: tabular-nums;
    }
  `,
  lanes: css`
    background: rgba(0, 0, 0, 0.012);
    border-block-start: 1px solid ${token.colorSplit};
  `,
  lane: css`
    display: flex;
    align-items: stretch;
    height: ${LANE_H}px;
    /* Border inside the height: the derived row height (projectRowHeight in
       boardModel.ts) counts exactly LANE_H per lane. */
    box-sizing: border-box;
    border-block-end: 1px solid ${token.colorSplit};

    &:last-child {
      border-block-end: none;
    }
  `,
  laneBody: css`
    flex: 1;
    min-width: 0;
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: 0 10px 0 22px;
  `,
  laneClickable: css`
    cursor: pointer;
  `,
  laneText: css`
    flex: 1;
    min-width: 0;
  `,
  laneName: css`
    font-size: 13px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    line-height: 1.2;
  `,
  laneSub: css`
    display: flex;
    align-items: center;
    gap: 5px;
    min-width: 0;
    font-size: 11px;
    color: ${token.colorTextTertiary};

    > span:first-child {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
  `,
  mismatchTag: css`
    flex-shrink: 0;
    display: inline-flex;
    align-items: center;
    gap: 2px;
    font-size: 9px;
    font-weight: 600;
    border-radius: 3px;
    padding: 0 4px;
    line-height: 14px;
    white-space: nowrap;
  `,
  conflictFlag: css`
    flex-shrink: 0;
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    cursor: pointer;
    line-height: 1.15;

    > span:first-child {
      display: inline-flex;
      align-items: center;
      gap: 3px;
      font-size: 11px;
      font-weight: 600;
      font-variant-numeric: tabular-nums;
    }

    > span:last-child {
      font-size: 9px;
      opacity: 0.85;
    }
  `,
  bar: css`
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
    height: 24px;
    border-radius: ${token.borderRadius}px;
    cursor: pointer;
    overflow: hidden;
    display: flex;
    align-items: center;
    padding: 0 ${token.paddingXS}px;
    gap: 6px;
    transition: box-shadow 0.15s;

    &:hover {
      box-shadow: ${token.boxShadowTertiary};
    }
  `,
  barPct: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  `,
  barNote: css`
    font-size: 10px;
    opacity: 0.7;
    white-space: nowrap;
  `,
  holeIcon: css`
    flex-shrink: 0;
  `,
  holeRole: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  holeNote: css`
    font-size: 10px;
    white-space: nowrap;
  `,
  ghostAvatar: css`
    width: 26px;
    height: 26px;
    border-radius: 50%;
    flex-shrink: 0;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    background: ${token.colorBgContainer};
  `,
  laneKebab: css`
    flex-shrink: 0;
    display: flex;
    align-items: center;
    padding-inline-end: 4px;
  `,
  // ── Boundaries and the node move (ADR-0035 / ADR-0036) ─────────────────
  // The grip on a node's edge. Visible at rest — a gesture nobody can find is
  // not a gesture — but quiet: a short pill that fills in on hover.
  edgeHandle: css`
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
    width: ${EDGE_HANDLE_W}px;
    height: 38px;
    cursor: ew-resize;
    touch-action: none;
    z-index: 2;
    outline: none;

    &::after {
      content: '';
      position: absolute;
      inset-block: 9px;
      inset-inline-start: 3px;
      width: 4px;
      border-radius: 2px;
      background: ${token.colorPrimary};
      opacity: 0.45;
      transition:
        opacity 0.12s,
        inset-block 0.12s;
    }

    &:hover::after,
    &:focus-visible::after {
      inset-block: 3px;
      opacity: 1;
    }
  `,
  edgeHandleRoot: css`
    height: 50px;
  `,
  ghostNode: css`
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
    height: 38px;
    box-sizing: border-box;
    border: 1.5px dashed ${token.colorPrimary};
    border-radius: ${token.borderRadius}px;
    background: color-mix(in srgb, ${token.colorPrimary} 7%, transparent);
    pointer-events: none;
  `,
  ghostNodeCut: css`
    border-color: ${token.colorTextTertiary};
    background: transparent;
  `,
  ghostBar: css`
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
    height: 24px;
    box-sizing: border-box;
    border: 1.5px dashed ${token.colorPrimary};
    border-radius: ${token.borderRadius}px;
    background: color-mix(in srgb, ${token.colorPrimary} 8%, transparent);
    pointer-events: none;
  `,
  dragCounter: css`
    position: absolute;
    top: 0;
    transform: translateX(-50%);
    padding: 0 7px;
    border-radius: ${token.borderRadiusSM}px;
    background: ${token.colorPrimary};
    color: ${token.colorTextLightSolid};
    font-size: 11px;
    font-weight: 600;
    line-height: 16px;
    white-space: nowrap;
    font-variant-numeric: tabular-nums;
    pointer-events: none;
    z-index: 3;
  `,
  followBadge: css`
    && {
      margin-inline-start: 5px;
      padding: 0 4px;
      border-radius: 3px;
      background: ${token.colorBgContainer};
      border: 1px solid ${token.colorPrimaryBorder};
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0;
      opacity: 1;
    }
  `,
  // The mark of a PINNED edge: ink, no hue. Following is the silent case.
  pinMark: css`
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
    width: 2px;
    height: 30px;
    background: ${token.colorTextSecondary};

    &::before,
    &::after {
      content: '';
      position: absolute;
      width: 6px;
      height: 2px;
      background: inherit;
    }

    &::before {
      top: 0;
    }

    &::after {
      bottom: 0;
    }
  `,
  pinMarkStart: css`
    &::before,
    &::after {
      inset-inline-start: 0;
    }
  `,
  pinMarkEnd: css`
    &::before,
    &::after {
      inset-inline-end: 0;
    }
  `,
  availabilityLine: css`
    position: absolute;
    top: 4px;
    bottom: 4px;
    width: 2px;
    margin-inline-start: -1px;
    background: ${token.colorText};
    pointer-events: none;
  `,
  beyondAvailability: css`
    position: absolute;
    top: 0;
    bottom: 0;
    pointer-events: none;
  `,
}));
