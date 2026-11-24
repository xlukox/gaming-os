import type { Feature } from '../../shell/features';
import { LibraryScreen } from './LibraryScreen';

export default { id: 'library', order: 10, titleKey: 'library.title', Screen: LibraryScreen } satisfies Feature;
