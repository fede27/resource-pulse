import { Typography } from 'antd';
import { useTranslation } from 'react-i18next';

const { Title, Paragraph } = Typography;

export function HomePage() {
  const { t } = useTranslation();
  return (
    <div style={{ padding: 24, maxWidth: 1440 }}>
      <Title level={2}>{t('home.title')}</Title>
      <Paragraph>{t('home.intro')}</Paragraph>
      <Paragraph type="secondary">{t('home.hint')}</Paragraph>
    </div>
  );
}
