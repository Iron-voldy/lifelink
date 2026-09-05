import { useState, type FormEvent } from 'react'
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { BloodType, Camp, CampSlot, Donor, EligibilityEvaluation } from '../api/types'
import { PortalShell } from '../components/PortalShell'
import { QueryState, StatusPill, formatDate } from '../components/QueryState'

const bloodTypes: BloodType[] = ['APositive', 'ANegative', 'BPositive', 'BNegative', 'ABPositive', 'ABNegative', 'OPositive', 'ONegative']
export const bloodLabel = (type: string) => type.replace('Positive', '+').replace('Negative', '−')
/** Today as YYYY-MM-DD in the person's own time zone (what a date input expects). */
const localToday = (now = new Date()) => now.toLocaleDateString('en-CA')
/** The same date `years` years before `now`, as YYYY-MM-DD. */
const yearsAgo = (years: number, now = new Date()) => { const d = new Date(now); d.setFullYear(d.getFullYear() - years); return localToday(d) }

/** Mirrors DonorService.Validate so problems are explained before the request is sent. */
export function validateDonorProfile(dateOfBirth: string, address: string, medicalFlags: string[], now = new Date()): string | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(dateOfBirth)) return 'Enter your date of birth.'
  if (dateOfBirth > localToday(now)) return 'Date of birth cannot be in the future.'
  if (dateOfBirth > yearsAgo(18, now)) return 'Donors must be at least 18 years old.'
  if (dateOfBirth < yearsAgo(120, now)) return 'Date of birth cannot be more than 120 years ago.'
  if (!address.trim()) return 'Address is required.'
  if (address.trim().length > 500) return 'Address must be at most 500 characters.'
  if (medicalFlags.length > 20) return 'Add at most 20 medical notes.'
  if (medicalFlags.some(x => x.length > 100)) return 'Each medical note must be at most 100 characters.'
  return null
}

export function DonorPortalPage() {
  const profile = useQuery({ queryKey: ['donor-me'], queryFn: api.donors.mine })
  return <PortalShell roleLabel="Blood donor">
    <header className="page-header"><div><p className="eyebrow">Donor space</p><h1>Your donor profile</h1><p>Keep your details current, check your eligibility and book a donation camp.</p></div>{profile.data && <StatusPill value={profile.data.eligibilityStatus} />}</header>
    <QueryState loading={profile.isLoading} error={profile.error} onRetry={() => profile.refetch()}>
      <div className="portal-grid">
        <ProfileForm donor={profile.data ?? null} />
        {profile.data ? <Eligibility donor={profile.data} /> : <section className="panel portal-card"><p className="eyebrow">Next step</p><h2>Create your profile</h2><p className="muted">Save your blood type, date of birth and address. Then check your eligibility and book a camp.</p></section>}
      </div>
      {profile.data && <CampBookings donor={profile.data} />}
      {profile.data && <History donor={profile.data} />}
    </QueryState>
  </PortalShell>
}

function ProfileForm({ donor }: { donor: Donor | null }) {
  const client = useQueryClient(); const [notice, setNotice] = useState(''); const [problem, setProblem] = useState('')
  const [flags, setFlags] = useState(donor?.medicalFlags.join(', ') ?? '')
  const save = useMutation({
    mutationFn: (input: Parameters<typeof api.donors.saveMine>[1]) => api.donors.saveMine(donor?.id, input),
    onSuccess: saved => { client.setQueryData(['donor-me'], saved); client.invalidateQueries({ queryKey: ['donor-history'] }); setNotice(donor ? 'Profile updated.' : 'Profile created. Check your eligibility next.') },
  })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setNotice(''); const data = new FormData(event.currentTarget)
    const dateOfBirth = String(data.get('dateOfBirth') ?? ''); const address = String(data.get('address') ?? '').trim(); const medicalFlags = flags.split(',').map(x => x.trim()).filter(Boolean)
    const invalid = validateDonorProfile(dateOfBirth, address, medicalFlags); setProblem(invalid ?? ''); if (invalid) return
    save.mutate({ bloodType: String(data.get('bloodType')) as BloodType, dateOfBirth, address, latitude: donor?.latitude ?? null, longitude: donor?.longitude ?? null, medicalFlags })
  }
  return <form className="panel form-panel" onSubmit={submit}><h2>{donor ? 'Profile details' : 'Create your donor profile'}</h2>
    <div className="form-grid">
      <label>Blood type<select name="bloodType" defaultValue={donor?.bloodType ?? 'OPositive'}>{bloodTypes.map(x => <option key={x} value={x}>{bloodLabel(x)}</option>)}</select></label>
      <label>Date of birth<input name="dateOfBirth" type="date" required max={yearsAgo(18)} min={yearsAgo(120)} defaultValue={donor?.dateOfBirth} /></label>
      <label className="span-2">Address<input name="address" required maxLength={500} defaultValue={donor?.address} placeholder="Street, city" /></label>
      <label className="span-2">Medical notes for review<input value={flags} onChange={e => setFlags(e.target.value)} placeholder="Comma separated, e.g. On antibiotics" /><small className="field-hint">Any note means a clinician reviews your eligibility.</small></label>
    </div>
    {(problem || save.error) && <div className="form-error" role="alert">{problem || save.error?.message}</div>}{notice && <div className="form-notice" role="status">{notice}</div>}
    <button className="primary-button" disabled={save.isPending}>{save.isPending ? 'Saving…' : donor ? 'Save changes' : 'Create profile'}</button></form>
}

function Eligibility({ donor }: { donor: Donor }) {
  const client = useQueryClient(); const [result, setResult] = useState<EligibilityEvaluation>()
  const check = useMutation({ mutationFn: () => api.donors.evaluate(donor.id), onSuccess: evaluation => { setResult(evaluation); client.invalidateQueries({ queryKey: ['donor-me'] }); client.invalidateQueries({ queryKey: ['donor-history'] }) } })
  return <section className="panel portal-card"><p className="eyebrow">Eligibility</p><h2><StatusPill value={donor.eligibilityStatus} /></h2>
    <p className="muted">Last donation: {donor.lastDonationDate ? formatDate(donor.lastDonationDate, false) : 'none recorded'}. Donors need to be 18–60 with 90 days between donations. Your check can flag problems, but blood bank staff confirm that you are eligible; changing your blood type, date of birth or medical notes sends you back for review.</p>
    {result && (result.status === 'Eligible' ? <div className="form-notice" role="status">All checks passed. You can book a camp below.</div> : <ul className="reason-list">{result.reasons.map(x => <li key={x}>{x}</li>)}</ul>)}
    {check.error && <div className="form-error" role="alert">{check.error.message}</div>}
    <button className="secondary-button" disabled={check.isPending} onClick={() => check.mutate()}>{check.isPending ? 'Checking…' : 'Check my eligibility'}</button></section>
}

function CampBookings({ donor }: { donor: Donor }) {
  const client = useQueryClient(); const [notice, setNotice] = useState('')
  const camps = useQuery({ queryKey: ['donor-camps'], queryFn: api.camps.upcoming })
  const slotQueries = useQueries({ queries: (camps.data?.items ?? []).map(camp => ({ queryKey: ['camp-slots', camp.id], queryFn: () => api.camps.slots(camp.id) })) })
  const mine = (camp: Camp): CampSlot | undefined => slotQueries[camps.data!.items.indexOf(camp)]?.data?.find(x => x.donorId === donor.id && x.status === 'Booked')
  const refresh = (campId: string) => Promise.all([client.invalidateQueries({ queryKey: ['camp-slots', campId] }), client.invalidateQueries({ queryKey: ['donor-camps'] })])
  const book = useMutation({ mutationFn: (camp: Camp) => api.camps.book(camp.id), onSuccess: (slot, camp) => { setNotice(`Booked ${camp.name} at ${formatDate(slot.slotTimeUtc)}.`); refresh(camp.id) } })
  const cancel = useMutation({ mutationFn: ({ camp, slot }: { camp: Camp; slot: CampSlot }) => api.camps.cancelBooking(camp.id, slot.id), onSuccess: (_, { camp }) => { setNotice(`Booking for ${camp.name} cancelled.`); refresh(camp.id) } })
  const eligible = donor.eligibilityStatus === 'Eligible'
  return <section className="portal-section"><div className="panel-title"><div><p className="eyebrow">Give together</p><h2>Upcoming donation camps</h2></div></div>
    {!eligible && <div className="notice-strip"><strong>Booking opens once you are eligible.</strong><span>{donor.eligibilityStatus === 'PendingVerification' ? 'Blood bank staff will verify your profile before you can book.' : 'Run the eligibility check above after updating your profile.'}</span></div>}
    {(book.error || cancel.error) && <div className="form-error" role="alert">{(book.error || cancel.error)?.message}</div>}{notice && <div className="form-notice" role="status">{notice}</div>}
    <QueryState loading={camps.isLoading} error={camps.error} empty={!camps.data?.items.length} onRetry={() => camps.refetch()}><div className="camp-grid">{camps.data?.items.map(camp => { const slot = camps.data && mine(camp); return <article className="panel camp-card" key={camp.id}><div className="camp-date"><strong>{new Date(camp.startsAtUtc).getDate()}</strong><span>{new Intl.DateTimeFormat('en', { month: 'short' }).format(new Date(camp.startsAtUtc))}</span></div><div className="camp-content"><div><h2>{camp.name}</h2><p>{camp.location} · {formatDate(camp.startsAtUtc)} – {formatDate(camp.endsAtUtc)}</p></div><div className="occupancy"><span><strong>{camp.availableSlots}</strong> slots left</span></div>
      {slot ? <div className="action-row"><span className="pill pill-scheduled">Your slot {formatDate(slot.slotTimeUtc)}</span><button className="text-button danger-text" disabled={cancel.isPending} onClick={() => cancel.mutate({ camp, slot })}>Cancel booking</button></div>
        : <button className="secondary-button" disabled={!eligible || camp.availableSlots === 0 || book.isPending} onClick={() => book.mutate(camp)}>{camp.availableSlots === 0 ? 'Fully booked' : 'Book a slot'}</button>}</div></article> })}</div></QueryState></section>
}

function History({ donor }: { donor: Donor }) {
  const history = useQuery({ queryKey: ['donor-history', donor.id], queryFn: async () => { const [eligibility, donations] = await Promise.all([api.donors.eligibilityHistory(donor.id), api.donors.donationHistory(donor.id)]); return { eligibility, donations } } })
  return <section className="portal-grid portal-section">
    <div className="panel portal-card"><h2>Donation history</h2>{history.data?.donations.length ? <ul className="history-list">{history.data.donations.map(x => <li key={x.id}><strong>{x.units} unit{x.units === 1 ? '' : 's'} · {formatDate(x.donationDate, false)}</strong><small>{x.location}</small></li>)}</ul> : <p className="muted">No donations recorded yet.</p>}</div>
    <div className="panel portal-card"><h2>Eligibility history</h2>{history.data?.eligibility.length ? <ul className="history-list">{history.data.eligibility.map(x => <li key={x.id}><strong>{x.previousStatus.replace(/([a-z])([A-Z])/g, '$1 $2')} → {x.newStatus.replace(/([a-z])([A-Z])/g, '$1 $2')}</strong><small>{x.reason} · {formatDate(x.changedAtUtc)}</small></li>)}</ul> : <p className="muted">No eligibility changes yet.</p>}</div>
  </section>
}
