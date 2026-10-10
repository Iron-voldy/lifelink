import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { BloodRequest, BloodRequestStatus, BloodType, Hospital, RequestUrgency } from '../api/types'
import { PortalShell } from '../components/PortalShell'
import { QueryState, StatusPill, formatDate } from '../components/QueryState'
import { bloodLabel } from './DonorPortalPage'

const bloodTypes: BloodType[] = ['APositive', 'ANegative', 'BPositive', 'BNegative', 'ABPositive', 'ABNegative', 'OPositive', 'ONegative']
const urgencies: RequestUrgency[] = ['Routine', 'Urgent', 'Critical']
/** Mirrors RequestService.PreApprovalStatuses: until the blood bank approves, the hospital may still escalate or withdraw. */
const preApproval: BloodRequestStatus[] = ['Submitted', 'UnderReview', 'PendingApproval', 'Escalated']
const registrationFormat = /^[A-Z0-9][A-Z0-9/.-]{1,98}[A-Z0-9]$/
const verificationNotes: Record<string, string> = {
  Pending: 'A blood-bank administrator is reviewing your registration. You can submit blood requests once it is verified.',
  Verified: 'Your hospital is verified. You can submit and escalate blood requests.',
  Rejected: 'Your registration was rejected. Contact the blood bank to correct the details.',
  Suspended: 'Your hospital is suspended. New requests are paused until the blood bank reinstates it.',
}

export interface HospitalForm { name: string; registrationNumber: string; address: string; position: string }

/** Mirrors RequestService.ValidateHospital so problems are explained before the request is sent. */
export function validateHospital(form: HospitalForm): string | null {
  const name = form.name.trim(), number = form.registrationNumber.trim().toUpperCase(), address = form.address.trim(), position = form.position.trim()
  if (name.length < 2 || name.length > 250 || !/\p{L}/u.test(name)) return 'Hospital name must be 2-250 characters and contain letters.'
  if (!registrationFormat.test(number)) return "Registration number must be 3-100 characters of letters, digits, '-', '/' or '.', starting and ending with a letter or digit."
  if (address.length < 3 || address.length > 500) return 'Address must be 3-500 characters.'
  if (position.length < 2 || position.length > 150) return 'Your position must be 2-150 characters.'
  return null
}

/** A datetime-local value (local time, no zone) for `hours` from now. */
const localInputValue = (hours: number, now = new Date()) => { const d = new Date(now.getTime() + hours * 3_600_000 - now.getTimezoneOffset() * 60_000); return d.toISOString().slice(0, 16) }

export function HospitalPortalPage() {
  const hospital = useQuery({ queryKey: ['hospital-me'], queryFn: api.hospitals.mine })
  return <PortalShell roleLabel="Hospital staff">
    <header className="page-header"><div><p className="eyebrow">Hospital workspace</p><h1>{hospital.data?.name ?? 'Your hospital'}</h1><p>Register your facility, submit blood requests and follow them through to dispatch.</p></div>{hospital.data && <StatusPill value={hospital.data.verificationStatus} />}</header>
    <QueryState loading={hospital.isLoading} error={hospital.error} onRetry={() => hospital.refetch()}>
      {hospital.data ? <>
        <section className="panel portal-card"><p className="eyebrow">Verification</p><h2>{hospital.data.registrationNumber}</h2><p className="muted">{hospital.data.address}</p><p>{verificationNotes[hospital.data.verificationStatus]}</p></section>
        {hospital.data.verificationStatus === 'Verified' && <div className="portal-grid portal-section"><NewRequestForm /><Summary /></div>}
        <Requests hospital={hospital.data} />
      </> : <div className="portal-grid"><RegistrationForm /><section className="panel portal-card"><p className="eyebrow">How it works</p><h2>Three steps</h2><ol className="steps-list"><li><strong>Register hospital</strong> with your facility name and registration number.</li><li>A blood-bank administrator <strong>verifies your hospital</strong>.</li><li>Then you can <strong>submit and escalate</strong> blood requests here or in the LifeLink mobile app.</li></ol></section></div>}
    </QueryState>
  </PortalShell>
}

function RegistrationForm() {
  const client = useQueryClient(); const [problem, setProblem] = useState('')
  const register = useMutation({ mutationFn: api.hospitals.register, onSuccess: saved => client.setQueryData(['hospital-me'], saved) })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget)
    const form: HospitalForm = { name: String(data.get('name') ?? ''), registrationNumber: String(data.get('registrationNumber') ?? ''), address: String(data.get('address') ?? ''), position: String(data.get('position') ?? '') }
    const invalid = validateHospital(form); setProblem(invalid ?? ''); if (invalid) return
    register.mutate({ name: form.name.trim(), registrationNumber: form.registrationNumber.trim(), address: form.address.trim(), position: form.position.trim(), latitude: null, longitude: null })
  }
  return <form className="panel form-panel" onSubmit={submit} noValidate><h2>Register hospital</h2>
    <div className="form-grid">
      <label className="span-2">Hospital name<input name="name" required maxLength={250} placeholder="e.g. Teaching Hospital Kandy" /></label>
      <label>Registration number<input name="registrationNumber" required maxLength={100} placeholder="e.g. MOH-HOSP-0042" /></label>
      <label>Your position<input name="position" required maxLength={150} placeholder="e.g. Blood bank officer" /></label>
      <label className="span-2">Address<input name="address" required maxLength={500} placeholder="Street, city" /></label>
    </div>
    {(problem || register.error) && <div className="form-error" role="alert">{problem || register.error?.message}</div>}
    <button className="primary-button" disabled={register.isPending}>{register.isPending ? 'Submitting…' : 'Submit for verification'}</button></form>
}

function NewRequestForm() {
  const client = useQueryClient(); const [notice, setNotice] = useState(''); const [problem, setProblem] = useState('')
  const create = useMutation({ mutationFn: api.requests.create, onSuccess: saved => { setNotice(`Request submitted with ${saved.urgency.toLowerCase()} urgency.`); client.invalidateQueries({ queryKey: ['hospital-requests'] }); client.invalidateQueries({ queryKey: ['hospital-summary'] }) } })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setNotice(''); const form = event.currentTarget; const data = new FormData(form)
    const requiredBy = new Date(String(data.get('requiredBy') ?? '')); const units = Number(data.get('quantityUnits'))
    const invalid = !Number.isInteger(units) || units < 1 || units > 100 ? 'Quantity must be between 1 and 100 units.' : Number.isNaN(requiredBy.getTime()) || requiredBy <= new Date() ? 'Required-by time must be in the future.' : requiredBy.getTime() > Date.now() + 30 * 86_400_000 ? 'Required-by time must be within 30 days.' : null
    setProblem(invalid ?? ''); if (invalid) return
    const notes = String(data.get('notes') ?? '').trim()
    create.mutate({ bloodType: String(data.get('bloodType')) as BloodType, quantityUnits: units, requestedUrgency: String(data.get('requestedUrgency')) as RequestUrgency, notes: notes || null, requiredByUtc: requiredBy.toISOString() }, { onSuccess: () => form.reset() })
  }
  return <form className="panel form-panel" onSubmit={submit} noValidate><h2>New blood request</h2>
    <div className="form-grid">
      <label>Blood type<select name="bloodType" defaultValue="OPositive">{bloodTypes.map(x => <option key={x} value={x}>{bloodLabel(x)}</option>)}</select></label>
      <label>Units<input name="quantityUnits" type="number" min={1} max={100} defaultValue={1} required /></label>
      <label>Urgency<select name="requestedUrgency" defaultValue="Routine">{urgencies.map(x => <option key={x}>{x}</option>)}</select></label>
      <label>Required by<input name="requiredBy" type="datetime-local" required defaultValue={localInputValue(24)} /></label>
      <label className="span-2">Clinical notes<input name="notes" maxLength={2000} placeholder="Optional, e.g. Emergency caesarean section" /><small className="field-hint">LifeLink raises urgency automatically for large or short-notice requests.</small></label>
    </div>
    {(problem || create.error) && <div className="form-error" role="alert">{problem || create.error?.message}</div>}{notice && <div className="form-notice" role="status">{notice}</div>}
    <button className="primary-button" disabled={create.isPending}>{create.isPending ? 'Submitting…' : 'Submit request'}</button></form>
}

function Summary() {
  const summary = useQuery({ queryKey: ['hospital-summary'], queryFn: api.requests.summary })
  return <section className="panel portal-card"><p className="eyebrow">At a glance</p><h2>Your requests</h2>
    {summary.data ? <ul className="history-list"><li><strong>{summary.data.open} open</strong><small>{summary.data.critical} at critical urgency</small></li><li><strong>{summary.data.fulfilled} fulfilled</strong><small>{summary.data.closedUnfulfilled} closed unfulfilled · {summary.data.total} in total</small></li></ul> : <p className="muted">{summary.error?.message ?? 'Loading…'}</p>}</section>
}

function Requests({ hospital }: { hospital: Hospital }) {
  const client = useQueryClient(); const [notice, setNotice] = useState(''); const [open, setOpen] = useState<string>()
  const requests = useQuery({ queryKey: ['hospital-requests', hospital.id], queryFn: () => api.requests.list() })
  const refresh = () => Promise.all([client.invalidateQueries({ queryKey: ['hospital-requests'] }), client.invalidateQueries({ queryKey: ['hospital-summary'] }), client.invalidateQueries({ queryKey: ['request-history'] })])
  const escalate = useMutation({ mutationFn: (r: BloodRequest) => api.requests.escalate(r.id), onSuccess: saved => { setNotice(`Escalated to ${saved.urgency.toLowerCase()} urgency.`); refresh() } })
  const withdraw = useMutation({ mutationFn: (r: BloodRequest) => api.requests.withdraw(r.id), onSuccess: () => { setNotice('Request withdrawn.'); refresh() } })
  const verified = hospital.verificationStatus === 'Verified'; const busy = escalate.isPending || withdraw.isPending
  return <section className="portal-section"><div className="panel-title"><div><p className="eyebrow">Track</p><h2>Blood requests</h2></div></div>
    {(escalate.error || withdraw.error) && <div className="form-error" role="alert">{(escalate.error || withdraw.error)?.message}</div>}{notice && <div className="form-notice" role="status">{notice}</div>}
    <QueryState loading={requests.isLoading} error={requests.error} empty={!requests.data?.items.length} onRetry={() => requests.refetch()}>
      <section className="request-grid">{requests.data?.items.map(r => {
        const editable = verified && preApproval.includes(r.status); const canEscalate = editable && !(r.status === 'Escalated' && r.urgency === 'Critical')
        return <article className="request-card" key={r.id}><div className="request-top"><span className="blood-badge">{bloodLabel(r.bloodType)}</span><div><strong>{r.quantityUnits} unit{r.quantityUnits === 1 ? '' : 's'} · {r.urgency}</strong><small>Needed by {formatDate(r.requiredByUtc)}</small></div><StatusPill value={r.status} /></div>
          {r.notes && <p>{r.notes}</p>}
          <div className="header-actions">
            {canEscalate && <button className="secondary-button" disabled={busy} onClick={() => escalate.mutate(r)}>Escalate</button>}
            {editable && <button className="text-button danger-text" disabled={busy} onClick={() => withdraw.mutate(r)}>Withdraw</button>}
            <button className="text-button" aria-expanded={open === r.id} onClick={() => setOpen(open === r.id ? undefined : r.id)}>{open === r.id ? 'Hide history' : 'History'}</button>
          </div>
          {open === r.id && <History id={r.id} />}</article>
      })}</section>
    </QueryState></section>
}

function History({ id }: { id: string }) {
  const history = useQuery({ queryKey: ['request-history', id], queryFn: () => api.requests.history(id) })
  if (history.isLoading) return <p className="muted">Loading history…</p>
  if (history.error) return <div className="form-error" role="alert">{history.error.message}</div>
  return <ul className="history-list">{history.data?.map(x => <li key={x.id}><strong>{x.previousStatus === x.newStatus ? x.newStatus.replace(/([a-z])([A-Z])/g, '$1 $2') : `${x.previousStatus.replace(/([a-z])([A-Z])/g, '$1 $2')} → ${x.newStatus.replace(/([a-z])([A-Z])/g, '$1 $2')}`}</strong><small>{x.reason ? `${x.reason} · ` : ''}{formatDate(x.changedAtUtc)}</small></li>)}</ul>
}
