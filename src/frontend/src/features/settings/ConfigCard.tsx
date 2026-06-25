import type { ReactNode } from 'react';
import { Button, theme } from 'antd';
import { useTranslation } from 'react-i18next';

export type ConfigCardProps = {
  title: ReactNode;
  subtitle?: ReactNode;
  dirty: boolean;
  valid: boolean;
  saving?: boolean;
  savedAt?: string | null;
  onSave: () => void;
  onReset: () => void;
  children: ReactNode;
};

// Shell for one org-level config aggregate: header + dirty badge + body +
// footer with independent save/reset (each aggregate has its own lifecycle).
export function ConfigCard({
  title,
  subtitle,
  dirty,
  valid,
  saving,
  savedAt,
  onSave,
  onReset,
  children,
}: ConfigCardProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();

  return (
    <div
      style={{
        background: token.colorBgContainer,
        border: `1px solid ${token.colorBorderSecondary}`,
        borderRadius: token.borderRadiusLG,
        overflow: 'hidden',
      }}
    >
      <div
        style={{
          padding: '16px 20px',
          borderBottom: `1px solid ${token.colorBorderSecondary}`,
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          gap: 16,
        }}
      >
        <div style={{ minWidth: 0 }}>
          <div style={{ fontSize: 16, fontWeight: 600 }}>{title}</div>
          {subtitle && (
            <div
              style={{
                fontSize: 13,
                color: token.colorTextTertiary,
                marginTop: 3,
                maxWidth: 640,
                lineHeight: 1.5,
              }}
            >
              {subtitle}
            </div>
          )}
        </div>
        {dirty && (
          <span
            style={{
              flexShrink: 0,
              height: 22,
              padding: '0 8px',
              borderRadius: 11,
              fontSize: 12,
              fontWeight: 500,
              background: token.colorWarningBg,
              border: `1px solid ${token.colorWarningBorder}`,
              color: token.colorWarningText,
              display: 'inline-flex',
              alignItems: 'center',
              gap: 5,
            }}
          >
            <span
              style={{
                width: 6,
                height: 6,
                borderRadius: '50%',
                background: token.colorWarning,
              }}
            />
            {t('settings.dirtyBadge')}
          </span>
        )}
      </div>

      <div style={{ padding: 20 }}>{children}</div>

      <div
        style={{
          padding: '12px 20px',
          borderTop: `1px solid ${token.colorBorderSecondary}`,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 12,
          background: token.colorFillQuaternary,
        }}
      >
        <span style={{ fontSize: 12, color: token.colorTextTertiary }}>
          {savedAt ? t('settings.savedAt', { time: savedAt }) : t('settings.aggregateFootnote')}
        </span>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button size="small" onClick={onReset} disabled={!dirty || !!saving}>
            {t('common.cancel')}
          </Button>
          <Button
            size="small"
            type="primary"
            onClick={onSave}
            loading={!!saving}
            disabled={!dirty || !valid}
          >
            {t('common.save')}
          </Button>
        </div>
      </div>
    </div>
  );
}
