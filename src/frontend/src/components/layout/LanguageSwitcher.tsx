import { Dropdown } from 'antd';
import { GlobalOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { normalizeLanguage, SUPPORTED_LANGUAGES, type AppLanguage } from '@/i18n';

const LABEL_KEY: Record<AppLanguage, 'italian' | 'english'> = {
  it: 'italian',
  en: 'english',
};

export function LanguageSwitcher() {
  const { t, i18n } = useTranslation();
  const current = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language);

  return (
    <Dropdown
      placement="bottomRight"
      menu={{
        selectable: true,
        selectedKeys: [current],
        onClick: ({ key }) => {
          if (key !== current) void i18n.changeLanguage(key);
        },
        items: SUPPORTED_LANGUAGES.map((lng) => ({
          key: lng,
          label: t(`common.${LABEL_KEY[lng]}`),
        })),
      }}
    >
      <span
        role="button"
        aria-label={t('common.languageSwitcher')}
        title={t('common.languageSwitcher')}
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: 6,
          cursor: 'pointer',
          padding: '4px 8px',
          fontSize: 13,
        }}
      >
        <GlobalOutlined style={{ fontSize: 16 }} />
        <span style={{ textTransform: 'uppercase', fontWeight: 500 }}>{current}</span>
      </span>
    </Dropdown>
  );
}
