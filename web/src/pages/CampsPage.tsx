import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { Camp } from '../api/types'
import { QueryState, StatusPill, formatDate } from '../components/QueryState'

const next: Record<string, string | undefined> = { Draft: 'Scheduled', Scheduled: 'InProgress', InProgress: 'Closed' }

/** Local 'YYYY-MM-DDTHH:mm', the format datetime-local inputs use for value and min. */
const toLocalInput = (date: Date) => new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)

/** Mirrors CampService.Validate so an impossible schedule is explained before it is sent. Returns null when valid. */
export function validateCampSchedule(startsAt: string, endsAt: string, capacity: number, slotMinutes: number, now = new Date()): string | null {
  const start = new Date(startsAt), end = new Date(endsAt)
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return 'Enter both a start and an end date and time.'
  if (start <= now) return 'Start time must be in the future.'
  const latest = new Date(now); latest.setFullYear(latest.getFullYear() + 2)
  if (start > latest) return 'Start time must be within two years; check the year.'
  if (end <= start) return 'End time must be after the start time.'
  const needed = capacity * slotMinutes
  if (needed > (end.getTime() - start.getTime()) / 60_000) return `${capacity} slots of ${slotMinutes} minutes need ${needed} minutes, which is longer than the camp. Extend the end time, or reduce capacity or slot length.`
  return null
}

/** Roster and attendance for one camp; donors can be checked in while the camp is in progress. */
export function CampAttendance({ camp }: { camp: Camp }) {
  const client = useQueryClient()
  const roster = useQuery({ queryKey: ['camp-roster', camp.id], queryFn: () => api.camps.roster(camp.id) })
  const report = useQuery({ queryKey: ['camp-attendance', camp.id], queryFn: () => api.camps.attendance(camp.id) })
  const checkIn = useMutation({ mutationFn: (slotId: string) => api.camps.checkIn(camp.id, slotId), onSettled: () => Promise.all([['camp-roster', camp.id], ['camp-attendance', camp.id], ['camp-slots', camp.id], ['camps']].map(queryKey => client.invalidateQueries({ queryKey }))) })
  const time = (value: string) => new Intl.DateTimeFormat('en-LK', { timeStyle: 'short' }).format(new Date(value))
  return <div className="camp-attendance" aria-label={`Attendance for ${camp.name}`}>
    {report.data && <p><strong>{report.data.checkedIn}</strong> checked in of <strong>{report.data.booked}</strong> booked · {report.data.noShows} no-show(s) · {report.data.cancelled} cancelled · {report.data.attendanceRate}% attendance</p>}
    {checkIn.error && <div className="form-error" role="alert">{checkIn.error.message}</div>}
    <QueryState loading={roster.isLoading} error={roster.error || report.error} empty={!roster.data?.length} onRetry={() => { roster.refetch(); report.refetch() }}>
      <ul className="activity-list">{roster.data?.map(entry => <li key={entry.slotId}><span>{time(entry.slotTimeUtc)}</span><strong>{entry.donorEmail}</strong><StatusPill value={entry.status} />{camp.status === 'InProgress' && entry.status === 'Booked' && <button className="text-button" disabled={checkIn.isPending} onClick={() => checkIn.mutate(entry.slotId)}>Check in</button>}</li>)}</ul>
    </QueryState>
  </div>
}

export function CampsPage() {
  const [showCreate, setShowCreate] = useState(false); const [openCamp, setOpenCamp] = useState<string>()
  const [startsAt, setStartsAt] = useState(''); const [endsAt, setEndsAt] = useState(''); const [formError, setFormError] = useState(''); const [notice, setNotice] = useState('')
  const client = useQueryClient()
  const query = useQuery({ queryKey: ['camps'], queryFn: api.camps.list })
  const refresh = () => client.invalidateQueries({ queryKey: ['camps'] })
  const transition = useMutation({ mutationFn: ({ id, status }: { id: string; status: string }) => api.camps.transition(id, status), onSuccess: camp => { client.invalidateQueries({ queryKey: ['camp-roster', camp.id] }); client.invalidateQueries({ queryKey: ['camp-attendance', camp.id] }); setNotice(`${camp.name} is now ${camp.status.replace(/([a-z])([A-Z])/g, '$1 $2')}.`); refresh() } })
  const create = useMutation({ mutationFn: api.camps.create, onSuccess: camp => { setShowCreate(false); setStartsAt(''); setEndsAt(''); setNotice(`${camp.name} was created as a draft with ${camp.capacity} slots. Move it to Scheduled to open bookings.`); refresh() } })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setNotice(''); const data = new FormData(event.currentTarget)
    const capacity = Number(data.get('capacity')), slotMinutes = Number(data.get('slotMinutes'))
    const problem = validateCampSchedule(startsAt, endsAt, capacity, slotMinutes); setFormError(problem ?? ''); if (problem) return
    create.mutate({ name: String(data.get('name')).trim(), location: String(data.get('location')).trim(), startsAtUtc: new Date(startsAt).toISOString(), endsAtUtc: new Date(endsAt).toISOString(), capacity, slotMinutes })
  }
  const minStart = toLocalInput(new Date())

  return <>
    <header className="page-header"><div><p className="eyebrow">Donation events</p><h1>Camp schedule</h1><p>Capacity, bookings and field attendance at a glance.</p></div><div className="header-actions"><span className="count-chip">{query.data?.totalCount ?? 0} camps</span><button className="primary-button" onClick={() => setShowCreate(v => !v)}>Create camp</button></div></header>
    <section className="community-banner"><p className="eyebrow">Give together. Grow together.</p><h2>Small moments. Life-changing impact.</h2><p>Bring your community together for the next donation day.</p></section>
    {(create.error || transition.error) && <div className="form-error" role="alert">{(create.error || transition.error)?.message}</div>}
    {showCreate && <form className="panel form-panel" onSubmit={submit}><h2>Schedule a donation camp</h2><div className="form-grid"><label>Name<input name="name" required /></label><label>Location<input name="location" required /></label><label>Starts<input name="startsAtUtc" type="datetime-local" required min={minStart} value={startsAt} onChange={e => { setStartsAt(e.target.value); setFormError(''); if (endsAt && e.target.value >= endsAt) setEndsAt('') }} /></label><label>Ends<input name="endsAtUtc" type="datetime-local" required min={startsAt || minStart} disabled={!startsAt} value={endsAt} onChange={e => { setEndsAt(e.target.value); setFormError('') }} aria-describedby="camp-end-hint" /><small id="camp-end-hint" className="field-hint">{startsAt ? 'Must be after the start time.' : 'Choose the start time first.'}</small></label><label>Capacity<input name="capacity" type="number" min="1" max="1000" required /></label><label>Slot minutes<input name="slotMinutes" type="number" min="5" max="240" defaultValue="15" required /></label></div>{formError && <div className="form-error" role="alert">{formError}</div>}<button className="primary-button" disabled={create.isPending}>{create.isPending ? 'Scheduling…' : 'Schedule camp'}</button></form>}
    {notice && <div className="form-notice" role="status">{notice}</div>}
    <QueryState loading={query.isLoading} error={query.error} empty={!query.data?.items.length} onRetry={() => query.refetch()}><section className="camp-grid">{query.data?.items.map(camp => { const occupancy = camp.capacity ? Math.round(camp.bookedSlots / camp.capacity * 100) : 0; return <article className="panel camp-card" key={camp.id}><div className="camp-date"><strong>{new Date(camp.startsAtUtc).getDate()}</strong><span>{new Intl.DateTimeFormat('en', { month: 'short' }).format(new Date(camp.startsAtUtc))}</span></div><div className="camp-content"><div><StatusPill value={camp.status} /><h2>{camp.name}</h2><p>{camp.location} · {formatDate(camp.startsAtUtc)} – {new Intl.DateTimeFormat('en-LK', { timeStyle: 'short' }).format(new Date(camp.endsAtUtc))}</p></div><div className="occupancy"><span><strong>{camp.bookedSlots}</strong> / {camp.capacity} booked</span><div><i style={{ width: `${Math.min(100, occupancy)}%` }} /></div></div><div className="action-row">{next[camp.status] && <button className="secondary-button" disabled={transition.isPending} onClick={() => transition.mutate({ id: camp.id, status: next[camp.status]! })}>Move to {next[camp.status]!.replace(/([a-z])([A-Z])/g, '$1 $2')}</button>}{(camp.status === 'Draft' || camp.status === 'Scheduled') && <button className="text-button danger-text" disabled={transition.isPending} onClick={() => window.confirm(`Cancel ${camp.name}? Booked donors lose their slots.`) && transition.mutate({ id: camp.id, status: 'Cancelled' })}>Cancel camp</button>}{camp.status !== 'Draft' && <button className="text-button" aria-expanded={openCamp === camp.id} onClick={() => setOpenCamp(id => id === camp.id ? undefined : camp.id)}>{openCamp === camp.id ? 'Hide attendance' : 'Attendance'}</button>}</div>{openCamp === camp.id && <CampAttendance camp={camp} />}</div></article> })}</section></QueryState>
  </>
}
