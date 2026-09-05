export type IconName = 'overview' | 'donors' | 'hospital' | 'requests' | 'inventory' | 'calendar' | 'workflow' | 'sun' | 'moon' | 'menu' | 'close' | 'logout' | 'arrow' | 'shield' | 'drop'
const paths: Record<IconName, string> = {
 overview: 'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
 donors: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M16 3a4 4 0 0 1 0 8 M22 21v-2a4 4 0 0 0-3-3.87 M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0',
 hospital: 'M4 21V5h16v16 M9 21v-5h6v5 M9 9h6 M12 6v6 M2 21h20',
 requests: 'M9 3H5v18h14V3h-4 M9 2h6v4H9z M8 11h8 M8 15h5',
 inventory: 'M3 7l9-5 9 5v10l-9 5-9-5z M3 7l9 5 9-5 M12 12v10 M7 4l10 6',
 calendar: 'M4 5h16v16H4z M8 2v6 M16 2v6 M4 11h16 M8 15h2 M14 15h2',
 workflow: 'M12 3v5 M5 16v-4h14v4 M12 8v4 M9 1h6v5H9z M2 16h6v6H2z M16 16h6v6h-6z',
 sun: 'M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0 M12 2v2 M12 20v2 M2 12h2 M20 12h2 M5 5l1 1 M18 18l1 1 M5 19l1-1 M18 6l1-1',
 moon: 'M21 13a9 9 0 0 1-10-10 9 9 0 1 0 10 10', menu: 'M4 6h16 M4 12h16 M4 18h16', close: 'M6 6l12 12 M6 18L18 6',
 logout: 'M9 4H3v16h6 M9 12h12 M17 8l4 4-4 4', arrow: 'M4 12h16 M14 6l6 6-6 6',
 shield: 'M12 2l8 3v6c0 5-4 9-8 11-4-2-8-6-8-11V5z M8 12l3 3 5-6', drop: 'M12 2S5 10 5 15a7 7 0 0 0 14 0c0-5-7-13-7-13z M8 15a4 4 0 0 0 4 4',
}
export function Icon({ name, className = '' }: { name: IconName; className?: string }) {
 return <svg className={`icon ${className}`} width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]} /></svg>
}
