import { useState } from 'react';
import { Button, Modal, Spin } from 'antd';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import type { PlanCommandResult } from '@/api/generated/schemas';
import { bandLabelFor, loadColor, type LoadBand } from '@/lib/loadBands';
import {
  deltaDays,
  directionOf,
  hoursOf,
  movedBlockCount,
  movedDate,
  type ReplanGesture,
  type StateChange,
} from './replanModel';
import type { ReplanState } from './useNodeReplan';
import { ACCOUNT_WIDTH, useStyles } from './ReplanBar.styles';

const day = (iso: string) => dayjs(iso).format('D MMM');
const signed = (n: number) => (n > 0 ? `+${n}` : `${n}`);
const hours = (n: number) => n.toLocaleString('it-IT', { maximumFractionDigits: 1 });
const signedHours = (n: number) => (n > 0 ? `+${hours(n)}` : hours(n));

export type ReplanBarProps = {
  state: ReplanState;
  bands: LoadBand[];
  onAccept: () => void;
  onDismiss: () => void;
  onUndo: () => void;
};

// The confirmation of a node move (ADR-0036). Two faces of one slot:
//   – nothing changes state  → the gesture is already done: a RECEIPT
//   – something changes state → a BAR that grows by exactly those rows and
//     names, on its button, what is being accepted
// Quantities that cross no boundary are never shown here. They are in the
// account, one click away, computed by the same command.
export function ReplanBar({ state, bands, onAccept, onDismiss, onUndo }: ReplanBarProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const [accountOpen, setAccountOpen] = useState(false);

  if (state.phase === 'idle') return null;
  const { gesture } = state;

  if (state.phase === 'pricing') {
    return (
      <div className={styles.dock}>
        <div className={cx(styles.bar, styles.receipt)}>
          <div className={styles.receiptBody}>
            <Spin size="small" />
            <span className={styles.receiptWhat}>{t('projects.replan.pricing')}</span>
          </div>
        </div>
      </div>
    );
  }

  const { result, changes } = state;
  const direction = directionOf(gesture);
  const moved = movedBlockCount(result);
  const { before, after } = movedDate(gesture);
  const days = deltaDays(gesture);
  const edgeLabel = gesture.edge === 'start' ? t('projects.replan.edgeStart') : t('projects.replan.edgeEnd');

  const followLine =
    moved === 0
      ? gesture.edge === 'start'
        ? t('projects.replan.followNoneStart')
        : t('projects.replan.followNoneEnd')
      : moved === 1
        ? gesture.edge === 'start'
          ? t('projects.replan.followOneStart')
          : t('projects.replan.followOneEnd')
        : gesture.edge === 'start'
          ? t('projects.replan.followManyStart', { count: moved })
          : t('projects.replan.followManyEnd', { count: moved });

  const account = (
    <ReplanAccount
      open={accountOpen}
      gesture={gesture}
      result={result}
      bands={bands}
      onClose={() => setAccountOpen(false)}
    />
  );

  if (state.phase === 'done') {
    const blocksLine =
      moved === 0
        ? followLine
        : direction === 'lengthen'
          ? moved === 1
            ? t('projects.replan.lengthenedOne', { days: Math.abs(days) })
            : t('projects.replan.lengthenedMany', { count: moved, days: Math.abs(days) })
          : moved === 1
            ? t('projects.replan.shortenedOne', { days: Math.abs(days) })
            : t('projects.replan.shortenedMany', { count: moved, days: Math.abs(days) });

    return (
      <div className={styles.dock}>
        <div className={cx(styles.bar, styles.receipt)} role="status">
          <div className={styles.receiptBody}>
            <span className={styles.receiptWhat}>
              <b>{t('projects.replan.done')}</b>{' '}
              {gesture.edge === 'start'
                ? t('projects.replan.receiptStart', { node: gesture.nodeName, date: day(after) })
                : t('projects.replan.receiptEnd', { node: gesture.nodeName, date: day(after) })}{' '}
              {blocksLine}
            </span>
            {changes.length === 0 && <span className={styles.headSub}>{t('projects.replan.noStateChange')}</span>}
            <span className={styles.footEnd}>
              {moved > 0 && (
                <Button size="small" type="link" onClick={() => setAccountOpen(true)}>
                  {t('projects.replan.seeAccount')}
                </Button>
              )}
              <Button size="small" onClick={onUndo}>
                {t('projects.replan.undo')}
              </Button>
              <Button size="small" type="text" onClick={onDismiss}>
                {t('projects.replan.close')}
              </Button>
            </span>
          </div>
        </div>
        {account}
      </div>
    );
  }

  const committing = state.phase === 'committing';
  const accept =
    direction === 'lengthen'
      ? changes.length === 1
        ? t('projects.replan.acceptLengthenOne')
        : t('projects.replan.acceptLengthenMany', { count: changes.length })
      : changes.length === 1
        ? t('projects.replan.acceptShortenOne')
        : t('projects.replan.acceptShortenMany', { count: changes.length });

  return (
    <div className={styles.dock}>
      <div className={styles.bar} role="alertdialog" aria-label={t('projects.replan.barLabel')}>
        <div className={styles.head}>
          <span className={styles.headTitle}>
            {t('projects.replan.title', {
              node: gesture.nodeName,
              edge: edgeLabel,
              before: day(before),
              after: day(after),
              delta: signed(days),
            })}
          </span>
          <span className={styles.headSub}>{followLine}</span>
        </div>
        {changes.map((c, i) => (
          <StateRow key={i} change={c} bands={bands} />
        ))}
        <div className={styles.foot}>
          <Button size="small" type="link" onClick={() => setAccountOpen(true)}>
            {t('projects.replan.seeAccount')}
          </Button>
          <span className={styles.footEnd}>
            <Button size="small" onClick={onDismiss} disabled={committing}>
              {t('projects.replan.cancel')}
            </Button>
            <Button size="small" type="primary" onClick={onAccept} loading={committing}>
              {accept}
            </Button>
          </span>
        </div>
      </div>
      {account}
    </div>
  );
}

// ── One state change ─────────────────────────────────────────────────────

function BandChip({ pct, bands }: { pct: number; bands: LoadBand[] }) {
  const { styles } = useStyles();
  const c = loadColor(pct, bands);
  return (
    // dynamic: band colours resolved from the configured bands.
    <span className={styles.chip} style={{ background: c.bg, color: c.fg, borderColor: c.solid }}>
      <span>{Math.round(pct)}%</span>
      {bandLabelFor(pct, bands)}
    </span>
  );
}

function StateRow({ change, bands }: { change: StateChange; bands: LoadBand[] }) {
  const { t } = useTranslation();
  const { styles } = useStyles();

  if (change.kind === 'frozen') {
    return (
      <div className={styles.row}>
        <span className={styles.rowKind}>{t('projects.replan.kindFrozen')}</span>
        <span>
          <div className={styles.rowText}>{t('projects.replan.frozenText')}</div>
          <div className={styles.rowSub}>
            {day(change.from)} – {day(change.to)}
          </div>
        </span>
        <span />
      </div>
    );
  }

  if (change.kind === 'demand') {
    const where = change.nodeName ? { role: change.roleName, node: change.nodeName } : null;
    return (
      <div className={styles.row}>
        <span className={styles.rowKind}>{t('projects.replan.kindDemand')}</span>
        <span className={styles.rowText}>
          {change.covered
            ? where
              ? t('projects.replan.demandCovered', where)
              : t('projects.replan.demandCoveredBare', { role: change.roleName })
            : where
              ? t('projects.replan.demandUncovered', where)
              : t('projects.replan.demandUncoveredBare', { role: change.roleName })}
        </span>
        <span className={styles.rowValue}>
          {t('projects.replan.uncovered')} {hours(change.gapBefore)} h <span className={styles.arrow}>→</span>{' '}
          <b>{hours(change.gapAfter)} h</b>
        </span>
      </div>
    );
  }

  const b = change.beyondAvailability;
  return (
    <div className={styles.row}>
      <span className={styles.rowKind}>{t('projects.replan.kindLoad')}</span>
      <span>
        <div className={styles.rowText}>
          {t('projects.replan.loadText', {
            name: change.resourceName,
            band: bandLabelFor(change.percentAfter, bands),
          })}
        </div>
        <div className={styles.rowSub}>
          {day(change.from)} – {day(change.to)}
          {change.otherProjects.length > 0 &&
            ` · ${t('projects.replan.alsoOn', { projects: change.otherProjects.join(', ') })}`}
          {b && ` · ${t('projects.replan.beyond', { from: day(b.from), to: day(b.to), boundary: day(b.boundary) })}`}
        </div>
      </span>
      <span className={styles.rowValue}>
        <BandChip pct={change.percentBefore} bands={bands} />
        <span className={styles.arrow}>→</span>
        <BandChip pct={change.percentAfter} bands={bands} />
      </span>
    </div>
  );
}

// ── The account: everything the command computed, in three sections ──────

function ReplanAccount({
  open,
  gesture,
  result,
  bands,
  onClose,
}: {
  open: boolean;
  gesture: ReplanGesture;
  result: PlanCommandResult;
  bands: LoadBand[];
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const c = result.consequence;
  const { before, after } = movedDate(gesture);
  const blocks = c?.blocks ?? [];
  const people = c?.people ?? [];
  const demands = c?.demands ?? [];
  const nameOf = (resourceId: string | undefined) =>
    people.find((p) => p.resourceId === resourceId)?.resourceName ?? '—';

  return (
    <Modal
      open={open}
      onCancel={onClose}
      width={ACCOUNT_WIDTH}
      title={t('projects.replan.accountTitle', { node: gesture.nodeName })}
      footer={<Button onClick={onClose}>{t('projects.replan.accountBack')}</Button>}
    >
      <div className={styles.muted}>
        {t('projects.replan.accountSub', { before: day(before), after: day(after), delta: signed(deltaDays(gesture)) })}
      </div>

      <section className={styles.section}>
        <div className={styles.sectionHead}>
          <span className={styles.sectionIndex}>1</span>
          <span className={styles.sectionTitle}>{t('projects.replan.sectionContainer')}</span>
          <span className={styles.sectionHint}>{t('projects.replan.sectionContainerHint')}</span>
        </div>
        <div className={styles.sectionBody}>
          {blocks.map((b) => (
            <div key={b.id} className={styles.diffRow}>
              <span className={styles.diffName}>
                {nameOf(b.resourceId)} · {b.allocationPercent}%
              </span>
              <span className={styles.muted}>
                {day(b.oldStart ?? '')} – {day(b.oldEnd ?? '')}
              </span>
              <span className={styles.muted}>→</span>
              <span>
                {day(b.newStart ?? '')} – {day(b.newEnd ?? '')}
              </span>
              <span className={styles.diffDelta}>{signedHours(hoursOf(b.hoursDelta))} h</span>
            </div>
          ))}
        </div>
      </section>

      <section className={styles.section}>
        <div className={styles.sectionHead}>
          <span className={styles.sectionIndex}>2</span>
          <span className={styles.sectionTitle}>{t('projects.replan.sectionContent')}</span>
          <span className={styles.sectionHint}>{t('projects.replan.sectionContentHint')}</span>
        </div>
        <div className={styles.sectionBody}>
          {people.map((p) => (
            <div key={p.resourceId} className={styles.sectionBody}>
              <div className={styles.line}>
                {p.resourceName} <b>{signedHours(hoursOf(p.hoursDelta))} h</b>
              </div>
              {(p.intervals ?? []).map((i) => (
                <div key={i.from} className={styles.line}>
                  <span className={styles.muted}>
                    {day(i.from ?? '')} – {day(i.to ?? '')}
                  </span>
                  <BandChip pct={i.percentBefore ?? 0} bands={bands} />
                  <span className={styles.muted}>→</span>
                  <BandChip pct={i.percentAfter ?? 0} bands={bands} />
                </div>
              ))}
              {(p.zeroCapacity ?? []).map((z) => (
                <div key={z.from} className={styles.note}>
                  <b>
                    {t('projects.replan.zeroTitle', {
                      from: day(z.from ?? ''),
                      to: day(z.to ?? ''),
                      reason: z.reason ?? '',
                    })}
                  </b>
                  {t('projects.replan.zeroBody')}
                </div>
              ))}
              {p.beyondAvailability && (
                <div className={cx(styles.note, styles.noteWarn)}>
                  <b>
                    {t('projects.replan.beyondTitle', {
                      hours: hours(hoursOf(p.beyondAvailability.hours)),
                      name: p.resourceName ?? '',
                    })}
                  </b>
                  {t('projects.replan.beyondBody', {
                    from: day(p.beyondAvailability.from ?? ''),
                    to: day(p.beyondAvailability.to ?? ''),
                  })}
                </div>
              )}
            </div>
          ))}
        </div>
      </section>

      <section className={styles.section}>
        <div className={styles.sectionHead}>
          <span className={styles.sectionIndex}>3</span>
          <span className={styles.sectionTitle}>{t('projects.replan.sectionReconciliation')}</span>
          <span className={styles.sectionHint}>{t('projects.replan.sectionReconciliationHint')}</span>
        </div>
        <div className={styles.sectionBody}>
          {demands.map((d) => (
            <div key={d.demandId}>
              <div className={styles.line}>
                <b>
                  {d.roleName}
                  {d.nodeName ? ` · ${d.nodeName}` : ''}
                </b>
                {d.requiredHours == null ? (
                  <span className={styles.muted}>{t('projects.replan.bestEffort')}</span>
                ) : (
                  <span>
                    {t('projects.replan.uncovered')} {hours(Math.max(0, hoursOf(d.gapBefore)))} h →{' '}
                    {hours(Math.max(0, hoursOf(d.gapAfter)))} h
                  </span>
                )}
              </div>
              <div className={styles.muted}>
                {d.requiredHours == null
                  ? t('projects.replan.coveredOnly', {
                      before: hours(hoursOf(d.coveredBefore)),
                      after: hours(hoursOf(d.coveredAfter)),
                    })
                  : t('projects.replan.requiredCovered', {
                      required: hours(hoursOf(d.requiredHours)),
                      before: hours(hoursOf(d.coveredBefore)),
                      after: hours(hoursOf(d.coveredAfter)),
                    })}
              </div>
            </div>
          ))}
        </div>
      </section>
    </Modal>
  );
}
