import type { ReactNode } from 'react';
import { theme } from 'antd';
import { LockOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

// Surfaces the "this is a constant, not a knob" boundary (ADR-0020, §1
// explainability). Each card explains what it deliberately does NOT expose.
export function ConstantNote({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const { token } = theme.useToken();

  return (
    <div
      style={{
        display: 'flex',
        gap: 8,
        padding: '8px 12px',
        marginTop: 14,
        background: token.colorFillQuaternary,
        border: `1px solid ${token.colorBorderSecondary}`,
        borderRadius: token.borderRadius,
        fontSize: 12,
        color: token.colorTextSecondary,
        lineHeight: 1.5,
      }}
    >
      <span style={{ flexShrink: 0, marginTop: 1, color: token.colorTextQuaternary }}>
        <LockOutlined style={{ fontSize: 12 }} />
      </span>
      <div>
        <strong style={{ color: token.colorTextSecondary, fontWeight: 500 }}>
          {t('settings.constantLabel')}
        </strong>{' '}
        {children}
      </div>
    </div>
  );
}
