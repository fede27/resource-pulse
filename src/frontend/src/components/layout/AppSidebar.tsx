import { useMemo } from 'react';
import { Layout, Menu } from 'antd';
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
import { useStyles } from './AppSidebar.styles';

const { Sider } = Layout;

export type AppSidebarProps = {
  collapsed: boolean;
};

export function AppSidebar({ collapsed }: AppSidebarProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
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
        key: 'people',
        icon: <TeamOutlined />,
        label: <Link to="/people">{t('nav.people')}</Link>,
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
        label: <Link to="/settings">{t('nav.settings')}</Link>,
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
      className={styles.sider}
    >
      <div
        onClick={() => navigate({ to: '/' }).catch(() => undefined)}
        className={cx(styles.brand, collapsed && styles.brandCollapsed)}
      >
        <div className={styles.mark}>RP</div>
        {!collapsed && (
          <div className={styles.titleWrap}>
            <div className={styles.title}>Resource Pulse</div>
            <div className={styles.subtitle}>Capacity planner</div>
          </div>
        )}
      </div>
      <Menu
        mode="inline"
        selectedKeys={[selectedKey]}
        inlineCollapsed={collapsed}
        className={styles.menu}
        items={items}
      />
    </Sider>
  );
}

function resolveSelectedKey(pathname: string): string {
  if (pathname.startsWith('/people')) return 'people';
  if (pathname.startsWith('/teams')) return 'teams';
  if (pathname.startsWith('/time-config')) return 'time-config';
  if (pathname.startsWith('/settings')) return 'settings';
  return 'home';
}
