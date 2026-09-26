import { Icon } from '../components/Icon'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { VerificationStatus } from '../api/types'
import { QueryState, StatusPill } from '../components/QueryState'

export function HospitalsPage() {
  const [status, setStatus] = useState<VerificationStatus | ''>('Pending')
  const client = useQueryClient()
  const hospitals = useQuery({ queryKey: ['hospitals', status], queryFn: () => api.hospitals.list(status || undefined) })
  const verify = useMutation({
    mutationFn: ({ id, decision }: { id: string; decision: VerificationStatus }) => api.hospitals.verify(id, decision),
    onSuccess: () => client.invalidateQueries({ queryKey: ['hospitals'] }),
  })

  return <>
    <header className="page-header"><div><p className="eyebrow">Requester access</p><h1>Hospital verification</h1><p>Review registered facilities before they can submit blood requests.</p></div><span className="count-chip">{hospitals.data?.totalCount ?? 0} facilities</span></header>
    <section className="toolbar"><select aria-label="Hospital verification status" value={status} onChange={event => setStatus(event.target.value as VerificationStatus | '')}><option value="">All verification states</option><option>Pending</option><option>Verified</option><option>Rejected</option><option>Suspended</option></select></section>
    <QueryState loading={hospitals.isLoading} error={hospitals.error} empty={!hospitals.data?.items.length}>
      <section className="request-grid">{hospitals.data?.items.map(hospital => <article className="request-card" key={hospital.id}><div className="request-top"><span className="activity-icon"><Icon name="hospital" /></span><div><strong>{hospital.name}</strong><small>{hospital.registrationNumber}</small></div><StatusPill value={hospital.verificationStatus} /></div><p>{hospital.address}</p><div className="header-actions"><button className="secondary-button" disabled={verify.isPending || hospital.verificationStatus === 'Verified'} onClick={() => verify.mutate({ id: hospital.id, decision: 'Verified' })}>Verify</button><button className="secondary-button" disabled={verify.isPending || hospital.verificationStatus === 'Rejected'} onClick={() => verify.mutate({ id: hospital.id, decision: 'Rejected' })}>Reject</button>{hospital.verificationStatus === 'Verified' && <button className="secondary-button" disabled={verify.isPending} onClick={() => verify.mutate({ id: hospital.id, decision: 'Suspended' })}>Suspend</button>}</div>{verify.error && verify.variables?.id === hospital.id && <div className="form-error" role="alert">{verify.error.message}</div>}</article>)}</section>
    </QueryState>
  </>
}
