import { describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { CommitmentLevel, ProjectStatus, ProjectType } from '@/api/generated/schemas';
import type { LoadBand } from '@/lib/loadBands';
import { renderWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';
import { CoverDemandModal, type CoverSubmitRow } from './CoverDemandModal';
import type { BoardProject, DemandRow } from './boardModel';

// The study's case: DevOps Engineer on «Test 01», 2–27 Nov 2026, 160 h.
const bands: LoadBand[] = [
  { label: 'Sotto', lowerBound: 0 },
  { label: 'In salute', lowerBound: 50 },
  { label: 'Pieno', lowerBound: 86 },
  { label: 'Sovraccarico', lowerBound: 101 },
];

const demand: DemandRow = {
  demandId: 'd-devops',
  nodeId: 'f-test',
  roleId: 'role-devops',
  roleName: 'DevOps Engineer',
  inferred: false,
  requiredH: 160,
  coveredH: 0,
  gapH: 160,
  usefulH: 0,
  overH: 0,
  ownerResourceId: null,
  ownerName: null,
  coverage: [],
  uncovered: true,
  mismatch: false,
  status: 'uncovered',
};

const project: BoardProject = {
  id: 'p1',
  name: 'Portale clienti',
  client: null,
  ownerId: null,
  ownerName: null,
  critical: false,
  proposed: false,
  status: ProjectStatus.Active,
  type: ProjectType.Grow,
  commitmentLevel: CommitmentLevel.Committed,
  from: '2026-10-01',
  to: '2026-12-31',
  followCount: 0,
  phases: [{ id: 'f-test', label: 'Test 01', from: '2026-11-02', to: '2026-11-27', followCount: 0 }],
  demands: [demand],
  holes: [demand],
  people: [],
  totals: { requiredH: 160, usefulH: 0, gapH: 160, overH: 0 },
};

const pool = [
  { id: 'chiara', name: 'Chiara Galli', roleName: 'DevOps Engineer' },
  { id: 'davide', name: 'Davide Moretti', roleName: 'DevOps Engineer' },
  { id: 'paolo', name: 'Paolo Ricci', roleName: 'Sistemista' },
];

const WEEKS = [
  ['2026-11-02', '2026-11-06'],
  ['2026-11-09', '2026-11-13'],
  ['2026-11-16', '2026-11-20'],
  ['2026-11-23', '2026-11-27'],
] as const;
const week = (i: number) => ({ from: WEEKS[i]![0], to: WEEKS[i]![1], hoursPerDay: '08:00:00' });

// Davide: 50% elsewhere the first two weeks, 20% the third, free the fourth.
// Chiara: 30% elsewhere, on holiday in week three. Paolo: nothing on.
function seedReads() {
  server.use(
    http.get('*/api/resources/capacity', () =>
      HttpResponse.json([
        { resourceId: 'davide', segments: [week(0), week(1), week(2), week(3)] },
        { resourceId: 'chiara', segments: [week(0), week(1), week(3)] },
        { resourceId: 'paolo', segments: [week(0), week(1), week(2), week(3)] },
      ]),
    ),
    http.get('*/api/resources/load-profiles', () =>
      HttpResponse.json([
        {
          resourceId: 'davide',
          segments: [
            { from: '2026-11-02', to: '2026-11-15', percent: 50 },
            { from: '2026-11-16', to: '2026-11-22', percent: 20 },
            { from: '2026-11-23', to: '2026-11-27', percent: 0 },
          ],
        },
        { resourceId: 'chiara', segments: [{ from: '2026-11-02', to: '2026-11-27', percent: 30 }] },
        { resourceId: 'paolo', segments: [] },
      ]),
    ),
  );
}

function renderModal(onSubmit: (rows: CoverSubmitRow[]) => void = vi.fn()) {
  seedReads();
  return renderWithProviders(
    <CoverDemandModal
      state={{ demand, project }}
      submitting={false}
      personPool={pool}
      bands={bands}
      onSubmit={onSubmit}
      onCancel={vi.fn()}
    />,
  );
}

type User = ReturnType<typeof renderModal>['user'];

async function pick(user: User, rowIndex: number, name: RegExp) {
  await user.click(screen.getAllByRole('combobox', { name: 'Persona' })[rowIndex]!);
  const hits = await screen.findAllByText(name);
  // Every row keeps its own closed dropdown in the DOM; the one just opened is
  // the last to be appended.
  await user.click(hits.filter((el) => el.closest('.ant-select-item-option')).at(-1)!);
}

describe('<CoverDemandModal>', () => {
  it('shows the demand before anything is chosen: what is asked, where, what stays open', async () => {
    renderModal();

    expect(await screen.findByText('DevOps Engineer su «Test 01»')).toBeInTheDocument();
    expect(screen.getByText(/2 nov – 27 nov 2026 · 160 h richieste/)).toBeInTheDocument();
    // Nobody picked yet: this dialog adds nothing and everything is open.
    expect(screen.getByText('questa copertura 0 h')).toBeInTheDocument();
    expect(screen.getByText('resta scoperto').parentElement).toHaveTextContent('resta scoperto 160 h');
    expect(screen.getByRole('button', { name: 'Copri 0 h' })).toBeDisabled();
  });

  it('the open picker puts the role of the demand first, with free hours and the quota needed', async () => {
    const { user } = renderModal();
    await screen.findByText('DevOps Engineer su «Test 01»');

    await user.click(screen.getByRole('combobox', { name: 'Persona' }));

    // The same 160 h are 100% of Davide and 133,3% of Chiara, who is away a week.
    expect(await screen.findByText('112 h libere · 100%')).toBeInTheDocument();
    expect(screen.getByText('84 h libere · 133,3%')).toBeInTheDocument();
    const groups = [...document.querySelectorAll('.ant-select-item-group')].map((g) => g.textContent);
    expect(groups).toEqual(['DevOps Engineer', 'Sistemista']);
  });

  it('writes hours, derives the quota for that person, and shows the load it produces', async () => {
    const { user } = renderModal();
    await screen.findByText('DevOps Engineer su «Test 01»');
    await pick(user, 0, /Davide Moretti/);

    expect(await screen.findByText('= 100%')).toBeInTheDocument();
    expect(screen.getByText("Davide c'è 20 giorni: 160 h di capacità nel periodo, 112 h libere.")).toBeInTheDocument();
    // Week 45: 50% elsewhere → 150% with this coverage.
    const first = screen.getByText(/sett\. 45/).parentElement!;
    expect(within(first).getByText('50%')).toBeInTheDocument();
    expect(within(first).getByText('150%')).toHaveAttribute('title', 'Sovraccarico');
    expect(screen.getByRole('button', { name: 'Copri 160 h' })).toBeEnabled();
  });

  it('«Ore libere» fits in total and still overloads the busy weeks — and says so', async () => {
    const { user } = renderModal();
    await screen.findByText('DevOps Engineer su «Test 01»');
    await pick(user, 0, /Davide Moretti/);

    await user.click(await screen.findByText('Ore libere · 112 h'));

    expect(screen.getByText('= 70%')).toBeInTheDocument();
    expect(screen.getByText('resta scoperto').parentElement).toHaveTextContent('resta scoperto 48 h');
    const first = screen.getByText(/sett\. 45/).parentElement!;
    expect(within(first).getByText('120%')).toBeInTheDocument();
    const last = screen.getByText(/sett\. 48/).parentElement!;
    expect(within(last).getByText('70%')).toBeInTheDocument();
  });

  it('a week without capacity is undefined, not 0% and not overload', async () => {
    const { user } = renderModal();
    await screen.findByText('DevOps Engineer su «Test 01»');
    await pick(user, 0, /Chiara Galli/);

    expect(await screen.findByText('= 133,3%')).toBeInTheDocument();
    const holiday = screen.getByText(/sett\. 47/).parentElement!;
    expect(within(holiday).getAllByText('—')).toHaveLength(2);
    expect(within(holiday).getByText('capacità zero')).toBeInTheDocument();
  });

  it('flipping to quota keeps the quantity and sends a percentage', async () => {
    const onSubmit = vi.fn();
    const { user } = renderModal(onSubmit);
    await screen.findByText('DevOps Engineer su «Test 01»');
    await pick(user, 0, /Davide Moretti/);
    await user.click(await screen.findByText('Ore libere · 112 h'));

    await user.click(screen.getByText('Quota %'));

    expect(await screen.findByText('= 112 h')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Copri 112 h' }));
    expect(onSubmit).toHaveBeenCalledWith([{ resourceId: 'davide', hours: null, percent: 70 }]);
  });

  it('a second person is a second row: the demand stays one and so does its residual', async () => {
    const onSubmit = vi.fn();
    const { user } = renderModal(onSubmit);
    await screen.findByText('DevOps Engineer su «Test 01»');
    await pick(user, 0, /Davide Moretti/);
    await user.click(await screen.findByText('Ore libere · 112 h'));

    // The link names what the first person leaves open, and the new row starts from it.
    await user.click(screen.getByRole('button', { name: /Aggiungi persona · resta scoperto 48 h/ }));
    await pick(user, 1, /Paolo Ricci/);

    expect(await screen.findByText('= 30%')).toBeInTheDocument();
    expect(screen.getByText('resta scoperto').parentElement).toHaveTextContent('resta scoperto 0 h');
    await user.click(screen.getByRole('button', { name: 'Crea 2 coperture · 160 h' }));
    expect(onSubmit).toHaveBeenCalledWith([
      { resourceId: 'davide', hours: 112, percent: null },
      { resourceId: 'paolo', hours: 48, percent: null },
    ]);
  });

  it('going over the request is legal: shown, and the button stays enabled', async () => {
    const { user } = renderModal();
    await screen.findByText('DevOps Engineer su «Test 01»');
    await pick(user, 0, /Davide Moretti/);

    const hours = screen.getByRole('spinbutton', { name: 'Ore' });
    await user.clear(hours);
    await user.type(hours, '180');

    expect(await screen.findByText('oltre la richiesta')).toBeInTheDocument();
    expect(screen.getByText('oltre la richiesta').parentElement).toHaveTextContent('oltre la richiesta 20 h');
    expect(screen.getByRole('button', { name: 'Copri 180 h' })).toBeEnabled();
  });
});
