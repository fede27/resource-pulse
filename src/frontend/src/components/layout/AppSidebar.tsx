import { useMemo } from 'react';
import { Layout, Menu, theme } from 'antd';
import { Link, useRouterState, useNavigate } from '@tanstack/react-router';
import {
  AppstoreOutlined,
  CalendarOutlined,
  ClockCircleOutlined,
  FolderOutlined,
  PieChartOutlined,
  SettingOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

const { Sider } = Layout;

export type AppSidebarProps = {
  collapsed: boolean;
};

export function AppSidebar({ collapsed }: AppSidebarProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const { location } = useRouterState();
  const navigate = useNavigate();
  const selectedKey = resolveSelectedKey(location.pathname);

  const items = useMemo(
    () => [
      { type: 'group' as const, label: t('nav.groupPlanning'), key: 'g-planning' },
      {
        key: 'home',
        icon: <PieChartOutlined />,
        label: <Link to="/">{t('nav.dashboard')}</Link>,
      },
      {
        key: 'resources',
        icon: <TeamOutlined />,
        label: t('nav.resources'),
        disabled: true,
      },
      {
        key: 'projects',
        icon: <FolderOutlined />,
        label: t('nav.projects'),
        disabled: true,
      },
      {
        key: 'allocations',
        icon: <CalendarOutlined />,
        label: t('nav.allocations'),
        disabled: true,
      },
      { type: 'group' as const, label: t('nav.groupConfiguration'), key: 'g-config' },
      {
        key: 'time-config',
        icon: <ClockCircleOutlined />,
        label: <Link to="/time-config">{t('nav.timeConfig')}</Link>,
      },
      {
        key: 'teams',
        icon: <AppstoreOutlined />,
        label: <Link to="/teams">{t('nav.teams')}</Link>,
      },
      {
        key: 'settings',
        icon: <SettingOutlined />,
        label: t('nav.settings'),
        disabled: true,
      },
    ],
    [t],
  );

  return (
    <Sider
      theme="light"
      width={220}
      collapsedWidth={80}
      collapsed={collapsed}
      trigger={null}
      style={{ borderRight: `1px solid ${token.colorBorderSecondary}` }}
    >
      <div
        onClick={() => navigate({ to: '/' }).catch(() => undefined)}
        style={{
          height: 64,
          display: 'flex',
          alignItems: 'center',
          gap: 10,
          padding: collapsed ? 0 : '0 16px',
          justifyContent: collapsed ? 'center' : 'flex-start',
          borderBottom: `1px solid ${token.colorBorderSecondary}`,
          cursor: 'pointer',
        }}
      >
        <div
          style={{
            width: 28,
            height: 28,
            borderRadius: token.borderRadius,
            background: 'linear-gradient(135deg, #1677ff 0%, #0958d9 100%)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#fff',
            fontWeight: 700,
            fontSize: 14,
            boxShadow: '0 2px 6px rgba(22,119,255,.25)',
            flexShrink: 0,
          }}
        >
          RP
        </div>
        {!collapsed && (
          <div style={{ minWidth: 0 }}>
            <div style={{ fontWeight: 600, fontSize: 15, lineHeight: 1 }}>
              Resource Pulse
            </div>
            <div
              style={{
                fontSize: 11,
                color: token.colorTextTertiary,
                marginTop: 3,
                letterSpacing: '.02em',
              }}
            >
              Capacity planner
            </div>
          </div>
        )}
      </div>
      <Menu
        mode="inline"
        selectedKeys={[selectedKey]}
        inlineCollapsed={collapsed}
        style={{ borderRight: 0 }}
        items={items}
      />
    </Sider>
  );
}

function resolveSelectedKey(pathname: string): string {
  if (pathname.startsWith('/teams')) return 'teams';
  if (pathname.startsWith('/time-config')) return 'time-config';
  return 'home';
}
