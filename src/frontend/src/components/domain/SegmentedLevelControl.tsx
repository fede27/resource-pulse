import { Segmented, Tooltip } from 'antd';

export type LevelOption<V extends string | number> = {
  value: V;
  label: string;
  description?: string;
};

export type SegmentedLevelControlProps<V extends string | number> = {
  value: V;
  options: ReadonlyArray<LevelOption<V>>;
  onChange: (next: V) => void;
  disabled?: boolean;
  size?: 'small' | 'middle' | 'large';
};

/**
 * Small 2- to 4-option segmented picker tuned for "level" choices
 * (Base / Intermedio / Avanzato, etc). Wraps AntD `<Segmented>` so
 * tooltips per option are pre-baked.
 */
export function SegmentedLevelControl<V extends string | number>({
  value,
  options,
  onChange,
  disabled = false,
  size = 'small',
}: SegmentedLevelControlProps<V>) {
  return (
    <Segmented<V>
      size={size}
      value={value}
      disabled={disabled}
      onChange={onChange}
      options={options.map((opt) => ({
        value: opt.value,
        label: opt.description ? (
          <Tooltip title={opt.description} mouseEnterDelay={0.3}>
            <span>{opt.label}</span>
          </Tooltip>
        ) : (
          opt.label
        ),
      }))}
    />
  );
}
