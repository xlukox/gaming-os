import type { Feature } from '../../shell/features';
import { SettingsScreen } from './SettingsScreen';

export default {
  id: 'settings',
  order: 90,
  titleKey: 'settings.title',
  icon: 'M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6zM12 2v3M12 19v3M2 12h3M19 12h3M5 5l2 2M17 17l2 2M5 19l2-2M17 7l2-2',
  Screen: SettingsScreen,
} satisfies Feature;
