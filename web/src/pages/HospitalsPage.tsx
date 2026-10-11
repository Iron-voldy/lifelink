import { Icon } from '../components/Icon'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { Hospital, VerificationStatus } from '../api/types'
import { QueryState, StatusPill, formatDate } from '../components/QueryState'
import { DetailList, DetailModal, LocationMapPanel, openOnRowClick } from '../components/DetailModal'

export function HospitalsPage() {
  const [status, setStatus] = useState<VerificationStatus | ''>('Pending')
  const client = useQueryClient(); const [viewing, setViewing] = useState<string>()
  const hospitals = useQuery({ queryKey: ['hospitals', status], queryFn: () => api.hospitals.list(status || undefined) })
  const verify = useMutation({
    mutationFn: ({ id, decision }: { id: string; decision: VerificationStatus }) => api.hospitals.verify(id, decision),
    onSuccess: () => client.invalidateQueries({ queryKey: ['hospitals'] }),
  })

  return <>
    <header className="page-header"><div><p className="eyebrow">Requester access</p><h1>Hospital verification</h1><p>Review registered facilities before they can submit blood requests.</p></div><span className="count-chip">{hospitals.data?.totalCount ?? 0} facilities</span></header>
    <section className="toolbar"><select aria-label="Hospital verification status" value={status} onChange={event => setStatus(event.target.value as VerificationStatus | '')}><option value="">All verification states</option><option>Pending</option><option>Verified</option><option>Rejected</option><option>Suspended</option></select></section>
    <QueryState loading={hospitals.isLoading} error={hospitals.error} empty={!hospitals.data?.items.length}>
      <section className="request-grid">{hospitals.data?.items.map(hospital => <article className="request-card clickable-card" key={hospital.id} onClick={openOnRowClick(() => setViewing(hospital.id))}><div className="request-top"><span className="activity-icon"><Icon name="hospital" /></span><div><strong><button type="button" className="row-link" onClick={() => setViewing(hospital.id)}>{hospital.name}</button></strong><small>{hospital.registrationNumber}</small></div><StatusPill value={hospital.verificationStatus} /></div><p>{hospital.address}</p><div className="header-actions"><button className="secondary-button" disabled={verify.isPending || hospital.verificationStatus === 'Verified'} onClick={() => verify.mutate({ id: hospital.id, decision: 'Verified' })}>Verify</button><button className="secondary-button" disabled={verify.isPending || hospital.verificationStatus === 'Rejected'} onClick={() => verify.mutate({ id: hospital.id, decision: 'Rejected' })}>Reject</button>{hospital.verificationStatus === 'Verified' && <button className="secondary-button" disabled={verify.isPending} onClick={() => verify.mutate({ id: hospital.id, decision: 'Suspended' })}>Suspend</button>}</div>{verify.error && verify.variables?.id === hospital.id && <div className="form-error" role="alert">{verify.error.message}</div>}</article>)}</section>
    </QueryState>
    {viewing && hospitals.data?.items.find(x => x.id === viewing) && (() => { const hospital = hospitals.data!.items.find(x => x.id === viewing)!; return <HospitalDetails hospital={hospital} onClose={() => setViewing(undefined)}
      actions={<>{verify.error && verify.variables?.id === hospital.id && <div className="form-error" role="alert">{verify.error.message}</div>}<button className="secondary-button" disabled={verify.isPending || hospital.verificationStatus === 'Verified'} onClick={() => verify.mutate({ id: hospital.id, decision: 'Verified' })}>Verify</button><button className="secondary-button" disabled={verify.isPending || hospital.verificationStatus === 'Rejected'} onClick={() => verify.mutate({ id: hospital.id, decision: 'Rejected' })}>Reject</button>{hospital.verificationStatus === 'Verified' && <button className="secondary-button" disabled={verify.isPending} onClick={() => verify.mutate({ id: hospital.id, decision: 'Suspended' })}>Suspend</button>}</>} /> })()}
  </>
}

const explanations: Record<VerificationStatus, string> = { Pending: 'Awaiting verification. Staff cannot submit blood requests yet.', Verified: 'Verified. Staff can submit, edit and escalate blood requests.', Rejected: 'Registration rejected. Staff cannot submit requests.', Suspended: 'Suspended. Staff can view or withdraw existing requests but not submit new ones.' }

/** One hospital in full: registration, verification state and what it allows, and its location. */
function HospitalDetails({ hospital, onClose, actions }: { hospital: Hospital; onClose: () => void; actions: React.ReactNode }) {
  return <DetailModal eyebrow="Hospital" title={hospital.name} onClose={onClose} actions={actions}>
    <DetailList items={[
      ['Registration number', hospital.registrationNumber],
      ['Verification', <StatusPill value={hospital.verificationStatus} />],
      ['Registered', formatDate(hospital.createdAtUtc)],
      ['What this allows', explanations[hospital.verificationStatus]],
    ]} />
    <section><h3>Location</h3><p className="muted">{hospital.address}</p><LocationMapPanel pins={hospital.latitude != null && hospital.longitude != null ? [{ lat: hospital.latitude, lng: hospital.longitude, label: hospital.name, kind: 'camp', lines: [hospital.address] }] : []} /></section>
  </DetailModal>
}
