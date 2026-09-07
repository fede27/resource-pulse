import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import {
  getSignalsGetChangesMockHandler,
  getSignalsGetMockHandler,
  getSignalsGetSweepMockHandler,
} from '@/api/generated/signals/signals.msw';
import { getMeGetMockHandler } from '@/api/generated/me/me.msw';
import {
  AppRole,
  SignalChange,
  SignalKind,
  SignalShape,
  SignalTier,
  SignalZone,
  type SignalDto,
  type SignalSweepDto,
} from '@/api/generated/schemas';
import { renderWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';
import { DashboardPage } from './DashboardPage';

const gap = (over: Partial<SignalDto> = {}): SignalDto => ({
  id: 'sig-gap',
  kind: SignalKind.Gap,
  tier: SignalTier.Breach,
  shape: SignalShape.Subject,
  subjectId: 'demand-1',
  deadlineAt: '2026-06-30',
  magnitude: 40,
  hardCommitted: true,
  roleName: 'Backend',
  rootProjectName: 'Portale ACME',
  ownerName: 'Anna Bianchi',
  link: { rootProjectId: 'proj-1', demandId: 'demand-1' },
  firstDetectedAt: '2026-06-24T06:00:00Z',
  lastObservedAt: '2026-06-24T06:00:00Z',
  isUnseen: true,
  isAcknowledged: false,
  memberNames: [],
  contributions: [],
  ...over,
});

const freshSweep: SignalSweepDto = {
  lastSweptAt: new Date().toISOString(),
  liveCount: 1,
  staleAfterHours: 48,
  lastVisitedAt: '2026-06-23T09:00:00Z',
  queueBudget: 7,
};

function arrange(signals: SignalDto[], sweep: SignalSweepDto = freshSweep) {
  server.use(
    getSignalsGetMockHandler(signals),
    getSignalsGetSweepMockHandler(sweep),
    getSignalsGetChangesMockHandler([]),
  );
}

describe('DashboardPage', () => {
  it('renders a gap row with its title, its reason and its verb', async () => {
    arrange([gap({ zone: SignalZone.Frozen })]);
    renderWithProviders(<DashboardPage />);

    expect(await screen.findByText(/Backend scoperto su Portale ACME/)).toBeInTheDocument();
    // The reason is mandatory: the ranking decides what exists, so the row has to
    // say why it is here. The verdict line above states the same hours, hence
    // *All* — two mentions is the design, not a duplicate.
    expect(screen.getAllByText(/40h scoperte/).length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText(/decisione frozen · owner Anna Bianchi/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Apri la copertura/ })).toBeInTheDocument();
  });

  it('states the overflow instead of expanding past the budget', async () => {
    arrange(Array.from({ length: 10 }, (_, i) => gap({ id: `sig-${i}`, subjectId: `d-${i}` })));
    renderWithProviders(<DashboardPage />);

    expect(await screen.findByText(/7 di 10/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /coda completa/i })).not.toBeInTheDocument();
  });

  it('celebrates an empty queue ONLY when the detector has actually run', async () => {
    arrange([], freshSweep);
    renderWithProviders(<DashboardPage />);

    expect(await screen.findByText('Niente da triare')).toBeInTheDocument();
    expect(screen.queryByText('Non abbiamo ancora guardato')).not.toBeInTheDocument();
  });

  it('says so when the detector has never run — an empty table is not a verdict', async () => {
    arrange([], { ...freshSweep, lastSweptAt: null, liveCount: 0 });
    renderWithProviders(<DashboardPage />);

    expect(await screen.findByText('Non abbiamo ancora guardato')).toBeInTheDocument();
  });

  it('warns when the last pass is stale, so a stuck worker is visible', async () => {
    arrange([gap()], {
      ...freshSweep,
      lastSweptAt: '2026-01-01T00:00:00Z',
      staleAfterHours: 48,
    });
    renderWithProviders(<DashboardPage />);

    expect(await screen.findByText('Ultimo controllo non recente')).toBeInTheDocument();
  });

  it('keeps an accepted risk in the queue, labelled and with its reason', async () => {
    arrange([
      gap({
        isAcknowledged: true,
        acknowledgedBy: 'elena@acme',
        acknowledgedReason: 'il cliente ha confermato lo slittamento',
      }),
    ]);
    renderWithProviders(<DashboardPage />);

    expect(await screen.findByText(/Backend scoperto su Portale ACME/)).toBeInTheDocument();
    expect(screen.getByText('Rischio assunto')).toBeInTheDocument();
    expect(
      screen.getByText(/Rischio assunto: il cliente ha confermato lo slittamento/),
    ).toBeInTheDocument();
    // Accepted is not resolved: the way back is offered.
    expect(screen.getByRole('button', { name: 'Riapri' })).toBeInTheDocument();
  });

  it('offers the inline confirm only for a tentative in the frozen zone', async () => {
    arrange([
      gap({
        id: 'sig-tentative',
        kind: SignalKind.TentativeInFrozen,
        resourceName: 'Luca Ferri',
        link: { rootProjectId: 'proj-1', allocationId: 'alloc-1' },
      }),
    ]);
    renderWithProviders(<DashboardPage />);

    expect(await screen.findByRole('button', { name: 'Conferma allocazione' })).toBeInTheDocument();
  });

  it('does not offer a gap an inline fix — choosing who covers needs the context', async () => {
    arrange([gap()]);
    renderWithProviders(<DashboardPage />);

    await screen.findByText(/Backend scoperto/);
    expect(screen.queryByRole('button', { name: 'Conferma allocazione' })).not.toBeInTheDocument();
  });

  it('explains the risk stays visible before accepting it', async () => {
    arrange([gap()]);
    renderWithProviders(<DashboardPage />);

    await userEvent.click(await screen.findByRole('button', { name: 'Accetta' }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/resta visibile in coda come rischio assunto/)).toBeInTheDocument();
  });

  it('hides the decisions from a viewer — hide, do not disable', async () => {
    server.use(
      getMeGetMockHandler({
        isAuthenticated: true,
        sub: 'viewer',
        email: 'v@x',
        name: 'Viewer',
        isMember: true,
        accessRole: AppRole.Viewer,
      }),
    );
    arrange([gap()]);
    renderWithProviders(<DashboardPage />);

    // The queue itself is readable — a Viewer sees the plan, and simply has no
    // decision to take on it.
    expect(await screen.findByText(/Backend scoperto su Portale ACME/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Accetta' })).not.toBeInTheDocument();
  });

  it('names aggregate members alphabetically and without a figure', async () => {
    arrange([
      gap({
        id: 'sig-slack',
        kind: SignalKind.UnderBand,
        tier: SignalTier.Slack,
        shape: SignalShape.Aggregate,
        subjectId: null,
        deadlineAt: null,
        magnitude: 2,
        roleName: null,
        rootProjectName: null,
        memberNames: ['Anna Bianchi', 'Zoe Rossi'],
        link: {},
      }),
    ]);
    renderWithProviders(<DashboardPage />);

    expect(await screen.findByText(/2 persone sotto la banda sana/)).toBeInTheDocument();
    expect(screen.getByText('Anna Bianchi · Zoe Rossi')).toBeInTheDocument();
  });

  it('renders the change feed with the two figures the sentence compares', async () => {
    server.use(
      getSignalsGetMockHandler([]),
      getSignalsGetSweepMockHandler(freshSweep),
      getSignalsGetChangesMockHandler([
        {
          signalId: 'sig-over',
          kind: SignalKind.Overcommit,
          change: SignalChange.MagnitudeWorsened,
          at: '2026-06-24T07:00:00Z',
          resourceName: 'Luca Ferri',
          magnitude: 120,
          previousMagnitude: 105,
          link: {},
        },
      ]),
    );
    renderWithProviders(<DashboardPage />);

    expect(await screen.findByText('Peggiorato')).toBeInTheDocument();
    expect(screen.getByText(/Luca Ferri è passato da 105 a 120/)).toBeInTheDocument();
  });
});
