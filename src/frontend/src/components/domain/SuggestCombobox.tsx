import { useMemo, useState } from 'react';
import { AutoComplete, Input, theme, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

const { Text } = Typography;

export type SuggestComboboxOption = {
  /** Stable identifier in the centralized pool. */
  id: string;
  /** Human-readable label shown in the list. */
  label: string;
};

export type SuggestComboboxProps = {
  /** Items from the centralized pool that can be picked. */
  pool: SuggestComboboxOption[];
  /** Already-attached labels (case-insensitive); excluded from the list. */
  exclude?: string[];
  /** Called when the user picks an existing pool item. */
  onPick: (option: SuggestComboboxOption) => void;
  /** Called when the user accepts the "create" affordance for a new value. */
  onCreate?: (rawValue: string) => void;
  /** Disables the "create" affordance even when the value is new. */
  allowCreate?: boolean;
  placeholder?: string;
  /** Custom label for the create row, e.g. "Crea tag". */
  createLabel?: string;
  size?: 'small' | 'middle' | 'large';
  disabled?: boolean;
  loading?: boolean;
};

/**
 * Generic "type-ahead from a shared pool, with create-new affordance".
 *
 * Distinct from AntD's `<Select mode="tags">`: that one stores free strings,
 * we always pick (or create-then-pick) an entry from a centralized catalogue
 * so the consumer can persist by id.
 */
export function SuggestCombobox({
  pool,
  exclude = [],
  onPick,
  onCreate,
  allowCreate = true,
  placeholder,
  createLabel,
  size = 'middle',
  disabled = false,
  loading = false,
}: SuggestComboboxProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const [value, setValue] = useState('');
  const excludeSet = useMemo(
    () => new Set(exclude.map((s) => s.toLowerCase())),
    [exclude],
  );

  const query = value.trim();
  const lower = query.toLowerCase();

  const matches = useMemo(() => {
    return pool
      .filter((o) => !excludeSet.has(o.label.toLowerCase()))
      .filter((o) => !lower || o.label.toLowerCase().includes(lower))
      .sort((a, b) => {
        const aw = a.label.toLowerCase().startsWith(lower) ? 0 : 1;
        const bw = b.label.toLowerCase().startsWith(lower) ? 0 : 1;
        return aw - bw || a.label.localeCompare(b.label);
      })
      .slice(0, 8);
  }, [pool, excludeSet, lower]);

  const exactExists = useMemo(
    () => pool.some((o) => o.label.toLowerCase() === lower),
    [pool, lower],
  );
  const showCreate =
    allowCreate &&
    !!onCreate &&
    query.length >= 1 &&
    !exactExists &&
    !excludeSet.has(lower);

  type RowKind = 'pick' | 'create';
  type RowOption = {
    key: string;
    kind: RowKind;
    payload: SuggestComboboxOption | string;
    value: string;
    label: React.ReactNode;
  };

  const rows: RowOption[] = [
    ...matches.map<RowOption>((m) => ({
      key: `pick-${m.id}`,
      kind: 'pick',
      payload: m,
      value: m.label,
      label: <span>{m.label}</span>,
    })),
    ...(showCreate
      ? [
          {
            key: `create-${query}`,
            kind: 'create' as const,
            payload: query,
            value: query,
            label: (
              <span
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 8,
                  width: '100%',
                }}
              >
                <PlusOutlined style={{ color: token.colorPrimary, fontSize: 12 }} />
                <span>
                  {createLabel ?? t('common.create')}{' '}
                  <strong>«{query}»</strong>
                </span>
              </span>
            ),
          },
        ]
      : []),
  ];

  const commit = (selected: string) => {
    const row = rows.find((r) => r.value === selected);
    if (!row) return;
    if (row.kind === 'pick') {
      onPick(row.payload as SuggestComboboxOption);
    } else {
      onCreate?.((row.payload as string).trim());
    }
    setValue('');
  };

  return (
    <AutoComplete
      value={value}
      onChange={(v) => setValue(v)}
      onSelect={(v) => commit(v as string)}
      options={rows.map((r) => ({ value: r.value, label: r.label }))}
      disabled={disabled}
      style={{ width: '100%' }}
      popupMatchSelectWidth
    >
      <Input
        size={size}
        placeholder={placeholder}
        prefix={<PlusOutlined style={{ color: token.colorTextTertiary, fontSize: 11 }} />}
        suffix={loading ? <Text type="secondary">…</Text> : null}
        onPressEnter={() => {
          if (rows[0]) commit(rows[0].value);
        }}
      />
    </AutoComplete>
  );
}
