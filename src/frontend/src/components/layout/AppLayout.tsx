import { useState } from 'react';
import { Avatar, Badge, Breadcrumb, Button, Dropdown, Layout, Space, theme } from 'antd';
import {
  BellOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { Outlet, useRouterState } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { AppSidebar } from './AppSidebar';
import { LanguageSwitcher } from './LanguageSwitcher';

const { Header, Content } = Layout;

export function AppLayout() {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const { location } = useRouterState();
  const [collapsed, setCollapsed] = useState(false);
  const crumbs = resolveBreadcrumb(location.pathname, t);

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <AppSidebar collapsed={collapsed} />
      <Layout style={{ background: token.colorBgLayout }}>
        <Header
          style={{
            background: token.colorBgContainer,
            padding: '0 16px 0 8px',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            borderBottom: `1px solid ${token.colorBorderSecondary}`,
            position: 'sticky',
            top: 0,
            zIndex: 10,
          }}
        >
          <Space size={8}>
            <Button
              type="text"
              icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
              onClick={() => setCollapsed((v) => !v)}
              aria-label={collapsed ? t('common.expandMenu') : t('common.collapseMenu')}
            />
            <Breadcrumb
              items={crumbs.map((c) => ({ title: c }))}
              style={{ fontSize: 13 }}
            />
          </Space>
          <Space size={12} align="center">
            <LanguageSwitcher />
            <span
              style={{
                width: 1,
                height: 24,
                background: token.colorBorderSecondary,
                display: 'inline-block',
              }}
            />
            <Badge count={0} size="small">
              <Button
                type="text"
                shape="circle"
                icon={<BellOutlined style={{ fontSize: 18 }} />}
                aria-label={t('common.notifications')}
              />
            </Badge>
            <span
              style={{
                width: 1,
                height: 24,
                background: token.colorBorderSecondary,
                display: 'inline-block',
              }}
            />
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
              <Space size={8} style={{ cursor: 'pointer' }}>
                <Avatar
                  size={32}
                  icon={<UserOutlined />}
                  style={{ background: token.colorPrimary }}
                />
                <span style={{ fontSize: 14 }}>Dev User</span>
              </Space>
            </Dropdown>
          </Space>
        </Header>
        <Content style={{ background: token.colorBgLayout }}>
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
  if (pathname.startsWith('/teams/new'))
    return [t('breadcrumb.configuration'), t('breadcrumb.teams'), t('breadcrumb.teamNew')];
  if (pathname.startsWith('/teams/'))
    return [
      t('breadcrumb.configuration'),
      t('breadcrumb.teams'),
      t('breadcrumb.teamDetail'),
    ];
  if (pathname === '/teams')
    return [t('breadcrumb.configuration'), t('breadcrumb.teams')];
  if (pathname.startsWith('/time-config'))
    return [t('breadcrumb.configuration'), t('breadcrumb.timeConfig')];
  return [pathname];
}
