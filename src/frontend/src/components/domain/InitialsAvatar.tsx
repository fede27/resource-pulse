import type { CSSProperties } from 'react';
import { Avatar } from 'antd';
import { colorForString, initialsOf } from './initials';
import { useStyles } from './InitialsAvatar.styles';

export type InitialsAvatarProps = {
  name: string;
  size?: number;
  /** Override the seed used to pick the color (defaults to `name`). */
  seed?: string;
  style?: CSSProperties;
};

export function InitialsAvatar({ name, size = 36, seed, style }: InitialsAvatarProps) {
  const { styles } = useStyles();
  return (
    <Avatar
      size={size}
      className={styles.avatar}
      // dynamic: the background colour is hashed from the person's name/seed so
      // each avatar is stably distinct — it's data-derived, not a theme value.
      // `style` also carries caller font-size overrides keyed to `size`.
      style={{ backgroundColor: colorForString(seed ?? name), ...style }}
    >
      {initialsOf(name)}
    </Avatar>
  );
}
