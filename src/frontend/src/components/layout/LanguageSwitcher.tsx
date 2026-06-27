import { Dropdown } from 'antd';
import { GlobalOutlined } from '@ant-design/icons';
import { createStyles } from 'antd-style';
import { useTranslation } from 'react-i18next';
import { normalizeLanguage, SUPPORTED_LANGUAGES, type AppLanguage } from '@/i18n';

const LABEL_KEY: Record<AppLanguage, 'italian' | 'english'> = {
  it: 'italian',
  en: 'english',
};

const useStyles = createStyles(({ token, css }) => ({
  trigger: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
    cursor: pointer;
    padding: ${token.paddingXXS}px ${token.paddingXS}px;
    font-size: ${token.fontSizeSM}px;
  `,
  icon: css`
    font-size: ${token.fontSizeLG}px;
  `,
  code: css`
    text-transform: uppercase;
    font-weight: 500;
  `,
}));

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
