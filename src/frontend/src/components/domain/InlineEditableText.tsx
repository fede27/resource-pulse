import { useEffect, useRef, useState, type CSSProperties } from 'react';
import { Input } from 'antd';
import type { InputRef } from 'antd';
import { createStyles } from 'antd-style';

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

const useStyles = createStyles(({ token, css }) => ({
  editWrap: css`
    display: inline-flex;
    flex-direction: column;
    gap: 2px;
  `,
  input: css`
    min-width: 180px;
  `,
  error: css`
    font-size: 11px;
    color: ${token.colorError};
  `,
  display: css`
    display: inline-block;
    cursor: text;
    border-radius: ${token.borderRadiusSM}px;
    padding: 1px 4px;
    margin: 0 -4px;
    transition: background ${token.motionDurationFast};
    &:hover {
      background: ${token.colorFillTertiary};
    }
  `,
  displayDisabled: css`
    cursor: default;
    &:hover {
      background: transparent;
    }
  `,
  placeholder: css`
    color: ${token.colorTextTertiary};
  `,
}));

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
  const { styles, cx } = useStyles();
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
      <span className={styles.editWrap}>
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
          className={styles.input}
          // dynamic: typography mirrors the surrounding heading the caller
          // placed this in, so size/weight/width are author-time unknowns.
          style={{ fontSize, fontWeight, width }}
        />
        {error && <span className={styles.error}>{error}</span>}
      </span>
    );
  }

  return (
    <span
      onClick={() => {
        if (!disabled) beginEdit();
      }}
      title={disabled ? undefined : 'Clic per modificare'}
      className={cx(styles.display, disabled && styles.displayDisabled)}
      // dynamic: see above — display type matches the caller's surrounding text.
      style={{ fontSize, fontWeight, width }}
    >
      {value || <span className={styles.placeholder}>{placeholder}</span>}
    </span>
  );
}
