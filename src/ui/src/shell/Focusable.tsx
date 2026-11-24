import { FocusContext, useFocusable } from '@noriginmedia/norigin-spatial-navigation';
import { useEffect, type ButtonHTMLAttributes, type ReactNode } from 'react';

/** A real <button> that arrow keys and the gamepad can reach. Enter and A arrive as a normal click. */
export function FocusButton({ onFocused, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { onFocused?: () => void }) {
  // The navigation library swallows Enter before the browser turns it into a click, so click here.
  const { ref } = useFocusable<HTMLButtonElement>({ onFocus: onFocused, onEnterPress: () => ref.current?.click() });
  return <button ref={ref} type="button" {...props} />;
}

/** Groups focusable children; remembers which child was focused last. */
export function FocusGroup({
  focusKey,
  autoFocus,
  className,
  children,
}: {
  focusKey: string;
  autoFocus?: boolean;
  className?: string;
  children: ReactNode;
}) {
  const { ref, focusKey: key, focusSelf } = useFocusable({ focusKey, saveLastFocusedChild: true, trackChildren: true });
  useEffect(() => {
    if (autoFocus) focusSelf();
  }, [autoFocus, focusSelf]);
  return (
    <FocusContext.Provider value={key}>
      <div ref={ref} className={className}>
        {children}
      </div>
    </FocusContext.Provider>
  );
}
