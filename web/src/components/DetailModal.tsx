import { lazy, Suspense, useEffect, useId, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Icon } from './Icon'
import type { MapPin } from './CampMap'

/** Leaflet loads on demand, only when a record with coordinates is opened. */
const LazyLocationMap = lazy(() => import('./CampMap').then(m => ({ default: m.LocationMap })))
export function LocationMapPanel({ pins }: { pins: MapPin[] }) {
  if (!pins.length) return <p className="muted">No map coordinates recorded.</p>
  return <Suspense fallback={<div className="detail-map" aria-busy="true" />}><LazyLocationMap pins={pins} /></Suspense>
}

/**
 * Centred modal for a record's details. Closes on Escape, the close button or a backdrop click; keeps keyboard focus
 * inside while open and returns it to the element that opened it.
 */
export function DetailModal({ title, eyebrow, onClose, children, actions }: { title: string; eyebrow?: string; onClose: () => void; children: ReactNode; actions?: ReactNode }) {
  const panel = useRef<HTMLDivElement>(null); const titleId = useId()
  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null
    panel.current?.querySelector<HTMLElement>('[data-autofocus]')?.focus()
    const previousOverflow = document.body.style.overflow; document.body.style.overflow = 'hidden'
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') { event.preventDefault(); onClose(); return }
      if (event.key !== 'Tab' || !panel.current) return
      const focusable = [...panel.current.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])')]
      if (!focusable.length) return
      const first = focusable[0], last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() } else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('keydown', onKey); document.body.style.overflow = previousOverflow; opener?.focus?.() }
  }, [onClose])

  return createPortal(<div className="modal-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) onClose() }}>
    <div ref={panel} className="modal-panel" role="dialog" aria-modal="true" aria-labelledby={titleId}>
      <header className="modal-header"><div>{eyebrow && <p className="eyebrow">{eyebrow}</p>}<h2 id={titleId}>{title}</h2></div><button type="button" className="icon-button" aria-label="Close details" data-autofocus onClick={onClose}><Icon name="close" /></button></header>
      <div className="modal-body">{children}</div>
      {actions && <footer className="modal-footer">{actions}</footer>}
    </div>
  </div>, document.body)
}

/** Two-column label/value list used inside detail modals. */
export function DetailList({ items }: { items: [string, ReactNode][] }) {
  return <dl className="detail-list">{items.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value ?? '—'}</dd></div>)}</dl>
}

/** Lets a whole table row or card open its details with the mouse, without stealing clicks meant for its own buttons. */
export function openOnRowClick(open: () => void) {
  return (event: React.MouseEvent) => { if (!(event.target as HTMLElement).closest('button, a, input, select, textarea, label')) open() }
}
