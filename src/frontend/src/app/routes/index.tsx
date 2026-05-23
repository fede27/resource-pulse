import { Typography } from 'antd';

const { Title, Paragraph } = Typography;

export function HomePage() {
  return (
    <div>
      <Title level={2}>Benvenuto</Title>
      <Paragraph>
        Resource Pulse — pianificazione di capacità per team e progetti.
      </Paragraph>
      <Paragraph type="secondary">
        Seleziona "Team" dal menu per gestire la rubrica dei team.
      </Paragraph>
    </div>
  );
}
