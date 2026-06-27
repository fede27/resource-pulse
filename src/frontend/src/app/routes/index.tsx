import { Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { PageContainer } from '@/components/layout/PageContainer';

const { Title, Paragraph } = Typography;

export function HomePage() {
  const { t } = useTranslation();
  return (
    <PageContainer>
      <Title level={2}>{t('home.title')}</Title>
      <Paragraph>{t('home.intro')}</Paragraph>
      <Paragraph type="secondary">{t('home.hint')}</Paragraph>
    </PageContainer>
  );
}
