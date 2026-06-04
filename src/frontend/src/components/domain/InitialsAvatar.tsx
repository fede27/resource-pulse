import type { CSSProperties } from 'react';
import { Avatar } from 'antd';
import { colorForString, initialsOf } from './initials';

export type InitialsAvatarProps = {
  name: string;
  size?: number;
  /** Override the seed used to pick the color (defaults to `name`). */
  seed?: string;
  style?: CSSProperties;
};

export function InitialsAvatar({ name, size = 36, seed, style }: InitialsAvatarProps) {
  return (
    <Avatar
      size={size}
      style={{
        backgroundColor: colorForString(seed ?? name),
        fontWeight: 500,
        flexShrink: 0,
        ...style,
      }}
    >
      {initialsOf(name)}
    </Avatar>
  );
}
