import { useEffect, useRef, useState, type CSSProperties } from 'react';
import { Input, theme } from 'antd';
import type { InputRef } from 'antd';

export type InlineEditableTextProps = {
  value: string;
  /** Called when the user commits a non-empty change. */
  onSave: (next: string) => void;
  /** Placeholder shown when `value` is empty. */
  placeholder?: string;
  /** Synchronous validation. Return a message to block commit, or null to allow. */
  validate?: (next: string) => string | null;
  /** Display font size — input height adapts to this. */
  fontSize?: number;
  fontWeight?: CSSProperties['fontWeight'];
  /** Width of both the display and edit affordance. */
  width?: number | string;
  disabled?: boolean;
};

/**
 * Click-to-edit text that mirrors the surrounding type. The display element
 * looks like plain text (with a hover tint) and swaps to an AntD `<Input>`
 * on focus. Commit via blur or Enter; revert via Escape.
 */
export function InlineEditableText({
  value,
  onSave,
  placeholder,
  validate,
  fontSize = 14,
  fontWeight = 400,
  width,
  disabled,
}: InlineEditableTextProps) {
  const { token } = theme.useToken();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);
  const [error, setError] = useState<string | null>(null);
  const ref = useRef<InputRef>(null);

  const beginEdit = () => {
    setDraft(value);
    setEditing(true);
    setError(null);
  };

  useEffect(() => {
    if (editing) {
      setTimeout(() => {
        ref.current?.focus({ cursor: 'all' });
      }, 0);
    }
  }, [editing]);

  const commit = () => {
    const next = draft.trim();
    if (validate) {
      const err = validate(next);
      if (err) {
        setError(err);
        return;
      }
    }
    if (next && next !== value) onSave(next);
    setEditing(false);
    setError(null);
  };

  if (editing) {
    return (
      <span style={{ display: 'inline-flex', flexDirection: 'column', gap: 2 }}>
        <Input
          ref={ref}
          value={draft}
          status={error ? 'error' : ''}
          onChange={(e) => {
            setDraft(e.target.value);
            if (error) setError(null);
          }}
          onBlur={commit}
          onPressEnter={commit}
          onKeyDown={(e) => {
            if (e.key === 'Escape') {
              setDraft(value);
              setEditing(false);
              setError(null);
            }
          }}
          placeholder={placeholder}
          style={{
            fontSize,
            fontWeight,
            width,
            minWidth: 180,
          }}
        />
        {error && (
          <span style={{ fontSize: 11, color: token.colorError }}>{error}</span>
        )}
      </span>
    );
  }

  return (
    <span
      onClick={() => {
        if (!disabled) beginEdit();
      }}
      title={disabled ? undefined : 'Clic per modificare'}
      style={{
        fontSize,
        fontWeight,
        cursor: disabled ? 'default' : 'text',
        borderRadius: token.borderRadiusSM,
        padding: '1px 4px',
        margin: '0 -4px',
        transition: `background ${token.motionDurationFast}`,
        display: 'inline-block',
        width,
      }}
      onMouseEnter={(e) => {
        if (!disabled) e.currentTarget.style.background = token.colorFillTertiary;
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.background = 'transparent';
      }}
    >
      {value || (
        <span style={{ color: token.colorTextTertiary }}>{placeholder}</span>
      )}
    </span>
  );
}
