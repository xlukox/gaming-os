import type { Feature } from '../../shell/features';
import { SettingsScreen } from './SettingsScreen';

export default { id: 'settings', order: 90, titleKey: 'settings.title', Screen: SettingsScreen } satisfies Feature;
