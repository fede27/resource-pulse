import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import { BoardToolbar } from './BoardToolbar';

describe('<BoardToolbar>', () => {
  it('stacks its rows as siblings of one card', () => {
    renderWithProviders(
      <BoardToolbar>
        <BoardToolbar.Row>first</BoardToolbar.Row>
        <BoardToolbar.Row>second</BoardToolbar.Row>
      </BoardToolbar>,
    );

    const first = screen.getByText('first');
    const second = screen.getByText('second');
    expect(first.parentElement).toBe(second.parentElement);
    // Both rows carry the same class: the hairline between bands is a sibling
    // rule, so a row never has to know whether it is the first one.
    expect(first.className).toBe(second.className);
  });

  it('labels a control and toggles it when rendered as a <label>', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    renderWithProviders(
      <BoardToolbar>
        <BoardToolbar.Row>
          <BoardToolbar.Label as="label" title="hint">
            <input type="checkbox" onChange={onChange} /> Conteggia tentative
          </BoardToolbar.Label>
        </BoardToolbar.Row>
      </BoardToolbar>,
    );

    await user.click(screen.getByText(/Conteggia tentative/));
    expect(onChange).toHaveBeenCalled();
  });

  it('renders spacer and count content', () => {
    renderWithProviders(
      <BoardToolbar>
        <BoardToolbar.Row>
          <BoardToolbar.Divider />
          <BoardToolbar.Spacer>
            <BoardToolbar.Count>
              <strong>2</strong> persone
            </BoardToolbar.Count>
          </BoardToolbar.Spacer>
        </BoardToolbar.Row>
      </BoardToolbar>,
    );

    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText(/persone/)).toBeInTheDocument();
  });
});
