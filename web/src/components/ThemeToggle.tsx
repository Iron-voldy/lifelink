import { useEffect, useState } from 'react'
import { Icon } from './Icon'
export function ThemeToggle() {
 const [dark, setDark] = useState(() => document.documentElement.dataset.theme === 'dark')
 useEffect(() => {
  const media = window.matchMedia?.('(prefers-color-scheme: dark)')
  const sync = () => {
   let saved: string | null = null
   try { saved = localStorage.getItem('lifelink.theme') } catch { /* Storage is optional. */ }
   const next = saved ? saved === 'dark' : Boolean(media?.matches)
   document.documentElement.dataset.theme = next ? 'dark' : 'light'; setDark(next)
  }
  sync(); media?.addEventListener('change', sync)
  return () => media?.removeEventListener('change', sync)
 }, [])
 function toggle() {
  const next = !dark; setDark(next); document.documentElement.dataset.theme = next ? 'dark' : 'light'
  try { localStorage.setItem('lifelink.theme', next ? 'dark' : 'light') } catch { /* Keep in-memory choice. */ }
 }
 return <button type="button" className="theme-toggle" onClick={toggle} aria-label={`Switch to ${dark ? 'light' : 'dark'} mode`}><Icon name={dark ? 'sun' : 'moon'} /><span>{dark ? 'Light mode' : 'Dark mode'}</span></button>
}
