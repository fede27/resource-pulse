import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import type { PlanCommandResult } from '@/api/generated/schemas';
import type { LoadBand } from '@/lib/loadBands';
import { renderWithProviders } from '@/test/render';
import { ReplanBar } from './ReplanBar';
import { stateChangesOf, type ReplanGesture } from './replanModel';
import type { ReplanState } from './useNodeReplan';

const bands: LoadBand[] = [
  { label: 'Sotto', lowerBound: 0 },
  { label: 'In salute', lowerBound: 50 },
  { label: 'Pieno', lowerBound: 86 },
  { label: 'Sovraccarico', lowerBound: 101 },
];

const gesture: ReplanGesture = {
  projectId: 'p1',
  nodeId: 'f2',
  nodeName: 'Fase 2 · Build',
  edge: 'end',
  oldFrom: '2026-07-01',
  oldTo: '2026-08-30',
  newFrom: '2026-07-01',
  newTo: '2026-09-09',
};

// Design case 1b.
const result: PlanCommandResult = {
  commandKind: 'replanNode',
  changes: [{ id: 'b1' }, { id: 'b2' }],
  consequence: {
    today: '2026-06-22',
    frozenUntil: '2026-07-06',
    touchesFrozen: false,
    blocks: [
      {
        id: 'b1',
        resourceId: 'marco',
        allocationPercent: 80,
        oldStart: '2026-07-01',
        oldEnd: '2026-08-30',
        newStart: '2026-07-01',
        newEnd: '2026-09-09',
        hoursDelta: '2.03:12:00',
      },
    ],
    people: [
      {
        resourceId: 'marco',
        resourceName: 'Marco Rossi',
        hoursDelta: '2.03:12:00',
        intervals: [
          {
            from: '2026-08-31',
            to: '2026-09-09',
            percentBefore: 40,
            percentAfter: 120,
            bandChanged: true,
            overloadedAfter: true,
            otherProjects: [{ id: 'x', name: 'Intranet Regione' }],
          },
        ],
      },
    ],
    demands: [
      {
        demandId: 'd1',
        roleName: 'Dev senior',
        nodeName: 'Fase 2',
        requiredHours: '10.00:00:00',
        coveredBefore: '8.14:24:00',
        coveredAfter: '10.04:48:00',
        gapBefore: '1.09:36:00',
        gapAfter: '-04:48:00',
        stateChanged: true,
      },
    ],
  },
};

const changes = stateChangesOf(result.consequence);
const noop = () => {};

const renderBar = (state: ReplanState, handlers: Partial<Record<'onAccept' | 'onDismiss' | 'onUndo', () => void>> = {}) =>
  renderWithProviders(
    <ReplanBar
      state={state}
      bands={bands}
      onAccept={handlers.onAccept ?? noop}
      onDismiss={handlers.onDismiss ?? noop}
      onUndo={handlers.onUndo ?? noop}
    />,
  );

describe('<ReplanBar>', () => {
  it('renders nothing at rest', () => {
    renderBar({ phase: 'idle' });
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('pending: one row per state change, and the button names what is accepted', async () => {
    const onAccept = vi.fn();
    const { user } = renderBar({ phase: 'pending', gesture, result, changes }, { onAccept });

    expect(screen.getByText('Fase 2 · Build: fine 30 ago → 9 set (+10 gg)')).toBeInTheDocument();
    expect(screen.getByText('2 coperture seguono la fine')).toBeInTheDocument();
    expect(screen.getByText('Marco Rossi passa a Sovraccarico')).toBeInTheDocument();
    expect(screen.getByText(/anche su Intranet Regione/)).toBeInTheDocument();
    expect(screen.getByText('Dev senior su Fase 2 risulta coperta')).toBeInTheDocument();
    expect(screen.getByText('33,6 h', { exact: false })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Allunga e accetta i 2 cambi' }));
    expect(onAccept).toHaveBeenCalledOnce();
  });

  it('pending: the chips carry the band the percentage falls in', () => {
    renderBar({ phase: 'pending', gesture, result, changes });
    expect(screen.getByText('40%').parentElement).toHaveTextContent('40%Sotto');
    expect(screen.getByText('120%').parentElement).toHaveTextContent('120%Sovraccarico');
  });

  it('pending: the account is one click away and lists what the bar leaves out', async () => {
    const { user } = renderBar({ phase: 'pending', gesture, result, changes });

    await user.click(screen.getByRole('button', { name: 'Vedi il conto' }));

    expect(await screen.findByText('Il conto · Fase 2 · Build')).toBeInTheDocument();
    expect(screen.getByText('Contenitore')).toBeInTheDocument();
    expect(screen.getAllByText('+51,2 h', { exact: false }).length).toBeGreaterThan(0);
    expect(screen.getByText('240 h richieste · coperte 206,4 h → 244,8 h')).toBeInTheDocument();
  });

  it('done with no state change: a receipt that says so, and a way back', async () => {
    const onUndo = vi.fn();
    const { user } = renderBar({ phase: 'done', gesture, result, changes: [] }, { onUndo });

    expect(screen.getByText('Fatto.')).toBeInTheDocument();
    expect(screen.getByText(/Fase 2 · Build: fine spostata al 9 set\./)).toBeInTheDocument();
    expect(screen.getByText(/2 coperture allungate di 10 giorni\./)).toBeInTheDocument();
    expect(screen.getByText('Nessun cambio di fascia o di copertura.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Annulla' }));
    expect(onUndo).toHaveBeenCalledOnce();
  });

  it('done after an accepted change: the receipt does not claim nothing changed', () => {
    renderBar({ phase: 'done', gesture, result, changes });
    expect(screen.queryByText('Nessun cambio di fascia o di copertura.')).not.toBeInTheDocument();
  });

  it('a shortening is named as one', () => {
    const shorter = { ...gesture, newTo: '2026-08-16' };
    renderBar({ phase: 'pending', gesture: shorter, result, changes });
    expect(screen.getByRole('button', { name: 'Accorcia e accetta i 2 cambi' })).toBeInTheDocument();
  });
});
