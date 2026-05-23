import { Layout, Menu } from 'antd';
import { Link, useRouterState } from '@tanstack/react-router';

const { Sider } = Layout;

export function AppSidebar() {
  const { location } = useRouterState();
  const selectedKey = resolveSelectedKey(location.pathname);

  return (
    <Sider theme="light" width={220} style={{ borderRight: '1px solid #f0f0f0' }}>
      <div
        style={{
          height: 64,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontWeight: 600,
          fontSize: 18,
          borderBottom: '1px solid #f0f0f0',
        }}
      >
        Resource Pulse
      </div>
      <Menu
        mode="inline"
        selectedKeys={[selectedKey]}
        style={{ borderRight: 0 }}
        items={[
          { key: 'home', label: <Link to="/">Home</Link> },
          { key: 'teams', label: <Link to="/teams">Team</Link> },
          { key: 'resources', label: 'Risorse', disabled: true },
          { key: 'projects', label: 'Progetti', disabled: true },
          { key: 'allocations', label: 'Allocazioni', disabled: true },
        ]}
      />
    </Sider>
  );
}

function resolveSelectedKey(pathname: string): string {
  if (pathname.startsWith('/teams')) return 'teams';
  return 'home';
}
