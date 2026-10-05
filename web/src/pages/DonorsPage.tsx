import { useEffect, useState, type FormEvent } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { BloodType, Donor, EligibilityStatus } from '../api/types'
import { QueryState, StatusPill, formatDate } from '../components/QueryState'

const bloodTypes: BloodType[] = ['APositive', 'ANegative', 'BPositive', 'BNegative', 'ABPositive', 'ABNegative', 'OPositive', 'ONegative']
const label = (type: string) => type.replace('Positive', '+').replace('Negative', '−')
const words = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2')
/** Latest donation date the server accepts: it compares against today's UTC date, which can be behind the local date. */
export const latestDonationDate = (now = new Date()) => { const local = now.toLocaleDateString('en-CA'), utc = now.toISOString().slice(0, 10); return local < utc ? local : utc }

export function DonorsPage() {
  const [search, setSearch] = useState(''); const [term, setTerm] = useState('')
  const [status, setStatus] = useState<EligibilityStatus | ''>(''); const [bloodType, setBloodType] = useState<BloodType | ''>(''); const [includeInactive, setIncludeInactive] = useState(false); const [page, setPage] = useState(1)
  const [notice, setNotice] = useState('')
  // Sorting: the API accepts 'email', 'bloodtype' and 'status'; any other value falls back to registration date.
  const [sortBy, setSortBy] = useState('created'); const [descending, setDescending] = useState(true)
  // Wait for a pause in typing so each keystroke does not fire a request.
  useEffect(() => { const id = setTimeout(() => { setTerm(search.trim()); setPage(1) }, 300); return () => clearTimeout(id) }, [search])
  const client = useQueryClient()
  const query = useQuery({ queryKey: ['donors', term, status, bloodType, includeInactive, page, sortBy, descending], queryFn: () => api.donors.list({ search: term, eligibilityStatus: status || undefined, bloodType: bloodType || undefined, includeInactive, page, sortBy, descending }), placeholderData: keepPreviousData })
  const refresh = () => client.invalidateQueries({ queryKey: ['donors'] })
  const evaluate = useMutation({ mutationFn: (donor: Donor) => api.donors.evaluate(donor.id), onMutate: () => setNotice(''), onSuccess: (result, donor) => { setNotice(`${donor.email}: ${words(result.previousStatus)} → ${words(result.status)}. ${result.reasons.length ? result.reasons.join(' ') : 'All eligibility checks passed.'}`); refresh() } })
  const deactivate = useMutation({ mutationFn: (donor: Donor) => api.donors.deactivate(donor.id), onMutate: () => setNotice(''), onSuccess: (_, donor) => { setNotice(`${donor.email} was deactivated and will no longer be matched or notified.`); refresh() } })
  const [recording, setRecording] = useState<Donor>()
  const record = useMutation({ mutationFn: ({ donor, input }: { donor: Donor; input: Parameters<typeof api.donors.recordDonation>[1] }) => api.donors.recordDonation(donor.id, input), onMutate: () => setNotice(''), onSuccess: (donation, { donor }) => { setRecording(undefined); setNotice(`Recorded ${donation.units} unit(s) from ${donor.email} on ${formatDate(donation.donationDate)}. The donor is now in the 90-day cooldown.`); refresh() } })
  function submitDonation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!recording) return; const data = new FormData(event.currentTarget); const notes = String(data.get('notes')).trim()
    record.mutate({ donor: recording, input: { donationDate: String(data.get('donationDate')), units: Number(data.get('units')), location: String(data.get('location')).trim(), notes: notes || undefined } })
  }
  const totalPages = query.data?.totalPages ?? 1

  return <>
    <header className="page-header"><div><p className="eyebrow">Registry</p><h1>Donors</h1><p>Eligibility, availability and profile oversight.</p></div><span className="count-chip">{query.data?.totalCount ?? 0} records</span></header>
    <section className="toolbar"><input aria-label="Search donors" value={search} onChange={e => setSearch(e.target.value)} placeholder="Search email or location…" />
      <select aria-label="Eligibility status" value={status} onChange={e => { setStatus(e.target.value as EligibilityStatus | ''); setPage(1) }}><option value="">All eligibility states</option>{(['PendingVerification', 'Eligible', 'TemporarilyIneligible', 'PermanentlyIneligible'] as const).map(x => <option key={x} value={x}>{words(x)}</option>)}</select>
      <select aria-label="Blood type" value={bloodType} onChange={e => { setBloodType(e.target.value as BloodType | ''); setPage(1) }}><option value="">All blood types</option>{bloodTypes.map(x => <option key={x} value={x}>{label(x)}</option>)}</select>
      <label className="check-label"><input type="checkbox" checked={includeInactive} onChange={e => { setIncludeInactive(e.target.checked); setPage(1) }} />Show deactivated</label>
      <select aria-label="Sort donors" value={sortBy} onChange={e => { setSortBy(e.target.value); setPage(1) }}><option value="created">Newest registered</option><option value="email">Email</option><option value="bloodtype">Blood type</option><option value="status">Eligibility status</option></select>
      <label className="check-label"><input type="checkbox" checked={descending} onChange={e => { setDescending(e.target.checked); setPage(1) }} />Descending</label></section>
    {recording && <form className="panel form-panel" onSubmit={submitDonation} aria-label="Record donation"><h2>Record a donation from {recording.email}</h2><div className="form-grid"><label>Donation date<input name="donationDate" type="date" required max={latestDonationDate()} min={recording.dateOfBirth} defaultValue={latestDonationDate()} /></label><label>Units<input name="units" type="number" min="1" max="4" defaultValue="1" required /></label><label>Location<input name="location" required maxLength={500} placeholder="e.g. Narahenpita blood centre" /></label><label>Notes<input name="notes" maxLength={1000} /></label></div>{record.error && <div className="form-error" role="alert">{record.error.message}</div>}<div className="action-row"><button className="primary-button" disabled={record.isPending}>{record.isPending ? 'Recording…' : 'Record donation'}</button><button type="button" className="secondary-button" onClick={() => { setRecording(undefined); record.reset() }}>Cancel</button></div></form>}
    {(evaluate.error || deactivate.error) && <div className="form-error" role="alert">{(evaluate.error || deactivate.error)?.message}</div>}{notice && <div className="form-notice" role="status">{notice}</div>}
    <QueryState loading={query.isLoading} error={query.error} empty={!query.data?.items.length} onRetry={() => query.refetch()}><section className="panel table-panel"><table><thead><tr><th scope="col">Donor</th><th scope="col">Blood</th><th scope="col">Eligibility</th><th scope="col">Last donation</th><th scope="col">Location</th><th scope="col"><span className="visually-hidden">Actions</span></th></tr></thead><tbody>{query.data?.items.map(donor => <tr key={donor.id} className={donor.isActive ? '' : 'row-muted'}><td data-label="Donor"><strong>{donor.email}</strong><small>{donor.isActive ? donor.medicalFlags.length ? `Medical review: ${donor.medicalFlags.join(', ')}` : 'No declared flags' : 'Deactivated'}</small></td><td data-label="Blood type"><span className="blood-badge">{label(donor.bloodType)}</span></td><td data-label="Eligibility"><StatusPill value={donor.eligibilityStatus} /></td><td data-label="Last donation">{donor.lastDonationDate ? formatDate(donor.lastDonationDate) : 'No record'}</td><td data-label="Location">{donor.address}</td><td data-label="Actions">{donor.isActive && <div className="action-row"><button className="text-button" disabled={evaluate.isPending} onClick={() => evaluate.mutate(donor)}>{evaluate.isPending && evaluate.variables?.id === donor.id ? 'Checking…' : 'Recheck eligibility'}</button>{donor.eligibilityStatus !== 'PermanentlyIneligible' && <button className="text-button" onClick={() => { setRecording(donor); setNotice(''); record.reset() }}>Record donation</button>}<button className="text-button danger-text" disabled={deactivate.isPending} onClick={() => window.confirm(`Deactivate ${donor.email}? They will no longer be matched or notified.`) && deactivate.mutate(donor)}>Deactivate</button></div>}</td></tr>)}</tbody></table></section>
      {totalPages > 1 && <nav className="pager" aria-label="Donor pages"><button className="secondary-button" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</button><span>Page {page} of {totalPages}</span><button className="secondary-button" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Next</button></nav>}
    </QueryState>
  </>
}