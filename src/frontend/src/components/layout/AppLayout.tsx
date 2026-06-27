import { useState } from 'react';
import { Avatar, Badge, Breadcrumb, Button, Dropdown, Layout, Space } from 'antd';
import {
  BellOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { createStyles } from 'antd-style';
import { Outlet, useRouterState } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { AppSidebar } from './AppSidebar';
import { LanguageSwitcher } from './LanguageSwitcher';

const { Header, Content } = Layout;

const useStyles = createStyles(({ token, css }) => ({
  shell: css`
    min-height: 100vh;
  `,
  body: css`
    background: ${token.colorBgLayout};
  `,
  header: css`
    background: ${token.colorBgContainer};
    padding: 0 ${token.padding}px 0 ${token.paddingXS}px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    position: sticky;
    top: 0;
    z-index: 10;
  `,
  breadcrumb: css`
    font-size: ${token.fontSizeSM}px;
  `,
  divider: css`
    width: 1px;
    height: ${token.controlHeightSM}px;
    background: ${token.colorBorderSecondary};
    display: inline-block;
  `,
  bellIcon: css`
    font-size: 18px;
  `,
  user: css`
    cursor: pointer;
  `,
  avatar: css`
    background: ${token.colorPrimary};
  `,
  userName: css`
    font-size: ${token.fontSize}px;
  `,
  content: css`
    background: ${token.colorBgLayout};
  `,
}));

export function AppLayout() {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const { location } = useRouterState();
  const [collapsed, setCollapsed] = useState(false);
  const crumbs = resolveBreadcrumb(location.pathname, t);

  return (
    <Layout className={styles.shell}>
      <AppSidebar collapsed={collapsed} />
      <Layout className={styles.body}>
        <Header className={styles.header}>
          <Space size={8}>
            <Button
              type="text"
              icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
              onClick={() => setCollapsed((v) => !v)}
              aria-label={collapsed ? t('common.expandMenu') : t('common.collapseMenu')}
            />
            <Breadcrumb
              items={crumbs.map((c) => ({ title: c }))}
              className={styles.breadcrumb}
            />
          </Space>
          <Space size={12} align="center">
            <LanguageSwitcher />
            <span className={styles.divider} />
            <Badge count={0} size="small">
              <Button
                type="text"
                shape="circle"
                icon={<BellOutlined className={styles.bellIcon} />}
                aria-label={t('common.notifications')}
              />
            </Badge>
            <span className={styles.divider} />
            <Dropdown
              placement="bottomRight"
              menu={{
                items: [
                  { key: 'profile', label: t('common.profile') },
                  { key: 'prefs', label: t('common.preferences') },
                  { type: 'divider' },
                  { key: 'logout', label: t('common.logout'), danger: true },
                ],
              }}
            >
              <Space size={8} className={styles.user}>
                <Avatar size={32} icon={<UserOutlined />} className={styles.avatar} />
                <span className={styles.userName}>Dev User</span>
              </Space>
            </Dropdown>
          </Space>
        </Header>
        <Content className={styles.content}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}

function resolveBreadcrumb(pathname: string, t: TFunction): string[] {
  if (pathname === '/' || pathname === '') return [t('breadcrumb.dashboard')];
  if (pathname.startsWith('/people'))
    return [t('breadcrumb.planning'), t('breadcrumb.people')];
  if (pathname.startsWith('/teams'))
    return [t('breadcrumb.configuration'), t('breadcrumb.teams')];
  if (pathname.startsWith('/time-config'))
    return [t('breadcrumb.configuration'), t('breadcrumb.timeConfig')];
  if (pathname.startsWith('/settings'))
    return [t('breadcrumb.configuration'), t('nav.settings')];
  return [pathname];
}
