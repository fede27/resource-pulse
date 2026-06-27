import { Dropdown } from 'antd';
import { GlobalOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { normalizeLanguage, SUPPORTED_LANGUAGES, type AppLanguage } from '@/i18n';
import { useStyles } from './LanguageSwitcher.styles';

const LABEL_KEY: Record<AppLanguage, 'italian' | 'english'> = {
  it: 'italian',
  en: 'english',
};

export function LanguageSwitcher() {
  const { t, i18n } = useTranslation();
  const { styles } = useStyles();
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
        className={styles.trigger}
      >
        <GlobalOutlined className={styles.icon} />
        <span className={styles.code}>{current}</span>
      </span>
    </Dropdown>
  );
}
