import type { Feature } from '../../shell/features';
import { LibraryScreen } from './LibraryScreen';

export default {
  id: 'library',
  order: 10,
  titleKey: 'library.title',
  icon: 'M4 5a2 2 0 0 1 2-2h6v16H6a2 2 0 0 0-2 2zM20 5a2 2 0 0 0-2-2h-6v16h6a2 2 0 0 1 2 2z',
  Screen: LibraryScreen,
} satisfies Feature;
