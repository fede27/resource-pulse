import { Layout } from 'antd';
import { Outlet } from '@tanstack/react-router';
import { AppSidebar } from './AppSidebar';

const { Header, Content } = Layout;

export function AppLayout() {
  return (
    <Layout style={{ minHeight: '100vh' }}>
      <AppSidebar />
      <Layout>
        <Header
          style={{
            background: '#fff',
            padding: '0 24px',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            borderBottom: '1px solid #f0f0f0',
          }}
        >
          <span style={{ fontSize: 16, fontWeight: 500 }}>Capacity Planning</span>
          <span style={{ color: '#888' }}>Fake User</span>
        </Header>
        <Content style={{ padding: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
