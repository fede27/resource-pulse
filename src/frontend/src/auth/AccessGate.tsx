import type { ReactNode } from 'react';
import { Button, Flex, Result, Spin, Typography } from 'antd';
import { useTranslation } from 'react-i18next';

import { useMeGet } from '@/api/generated/me/me';
import { useAccess } from '@/auth/access';
import { useAuthStyles } from '@/auth/auth.styles';

/**
 * Stands between a signed-in user and the application shell (ADR-0030).
 *
 * Access is fail-close: authenticating proves who you are, never that anyone
 * granted you a place in this tenant. Somebody with no membership therefore gets
 * an explanation and an address to copy, not an empty application — which is the
 * whole reason `GET /api/me` is exempt from the membership requirement.
 */
export function AccessGate({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const { styles } = useAuthStyles();
  const { isMember, isLoading } = useAccess();
  const { data } = useMeGet();

  if (isLoading) {
    return (
      <Flex className={styles.centered} vertical gap="middle" align="center" justify="center">
        <Spin size="large" />
        <Typography.Text type="secondary">{t('common.loading')}</Typography.Text>
      </Flex>
    );
  }

  if (!isMember) {
    return (
      <Flex className={styles.centered} vertical align="center" justify="center">
        <Result
          status="403"
          title={t('access.noAccessTitle')}
          subTitle={t('access.noAccessBody')}
          extra={
            <Flex vertical gap="small" align="center">
              {data?.email ? (
                <Typography.Text copyable code>
                  {data.email}
                </Typography.Text>
              ) : null}
              <Button onClick={() => window.location.reload()}>{t('access.recheck')}</Button>
            </Flex>
          }
        />
      </Flex>
    );
  }

  return <>{children}</>;
}
