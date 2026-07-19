import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { CommitmentLevel, ProjectType } from '@/api/generated/schemas';
import { NewProjectPanel, type NewProjectPanelProps } from './NewProjectPanel';

const pool = [
  { id: 'r-anna', name: 'Anna Bianchi', roleName: 'Project Manager' },
  { id: 'r-luca', name: 'Luca Ferri', roleName: 'Dev senior' },
];

function setup(over: Partial<NewProjectPanelProps> = {}) {
  const props: NewProjectPanelProps = {
    open: true,
    saving: false,
    onClose: vi.fn(),
    onSubmit: vi.fn(),
    personPool: pool,
    defaultOwnerId: 'r-anna',
    ...over,
  };
  return { props, ...renderWithProviders(<NewProjectPanel {...props} />) };
}

describe('<NewProjectPanel>', () => {
  it('blocks submit and shows required errors on an empty form', async () => {
    const { props, user } = setup();

    await user.click(screen.getByRole('button', { name: /Crea progetto/ }));

    expect(await screen.findByText('Il nome è obbligatorio.')).toBeInTheDocument();
    expect(screen.getAllByText('Data obbligatoria.')).toHaveLength(2);
    expect(props.onSubmit).not.toHaveBeenCalled();
  });

  it('keeps "Aggiungi fase" disabled until the project dates are set', async () => {
    const { user } = setup();

    const addPhase = screen.getByRole('button', { name: /Aggiungi fase/ });
    expect(addPhase).toBeDisabled();
    expect(screen.getByText('Imposta prima le date del progetto.')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Data inizio'), '01/09/2026{enter}');
    await user.type(screen.getByLabelText('Data fine'), '30/09/2026{enter}');

    expect(addPhase).toBeEnabled();
    expect(screen.getByText('Nessuna fase — facoltative.')).toBeInTheDocument();
  });

  it('rejects an end date before the start date', async () => {
    const { props, user } = setup();

    await user.type(screen.getByLabelText('Nome del progetto'), 'Rollout CRM');
    await user.type(screen.getByLabelText('Data inizio'), '15/09/2026{enter}');
    await user.type(screen.getByLabelText('Data fine'), '01/09/2026{enter}');
    await user.click(screen.getByRole('button', { name: /Crea progetto/ }));

    expect(
      await screen.findByText('Deve essere successiva o uguale alla data di inizio.'),
    ).toBeInTheDocument();
    expect(props.onSubmit).not.toHaveBeenCalled();
  });

  it('submits the serialized payload with defaults, keeping named phases and dropping incomplete rows', async () => {
    const { props, user } = setup();

    await user.type(screen.getByLabelText('Nome del progetto'), '  Rollout CRM  ');
    await user.type(screen.getByLabelText('Cliente'), 'ACME S.p.A.');
    await user.type(screen.getByLabelText('Data inizio'), '01/09/2026{enter}');
    await user.type(screen.getByLabelText('Data fine'), '30/09/2026{enter}');

    // Two phase rows (prefilled with the project dates): only the named one survives.
    await user.click(screen.getByRole('button', { name: /Aggiungi fase/ }));
    await user.click(screen.getByRole('button', { name: /Aggiungi fase/ }));
    const [firstPhaseName] = screen.getAllByPlaceholderText('Nome fase (es. Analisi)');
    await user.type(firstPhaseName!, 'Analisi');

    await user.click(screen.getByRole('button', { name: /Crea progetto/ }));

    expect(props.onSubmit).toHaveBeenCalledTimes(1);
    expect(props.onSubmit).toHaveBeenCalledWith({
      name: 'Rollout CRM',
      client: 'ACME S.p.A.',
      ownerId: 'r-anna',
      type: ProjectType.Customer,
      commitmentLevel: CommitmentLevel.Planned,
      startISO: '2026-09-01',
      endISO: '2026-09-30',
      phases: [{ name: 'Analisi', startISO: '2026-09-01', endISO: '2026-09-30' }],
    });
  });
});
