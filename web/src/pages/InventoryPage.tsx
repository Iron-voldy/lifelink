import { useEffect, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { BloodType, InventoryLot } from '../api/types'
import { QueryState, StatusPill, formatDate } from '../components/QueryState'

const bloodTypes: BloodType[] = ['APositive', 'ANegative', 'BPositive', 'BNegative', 'ABPositive', 'ABNegative', 'OPositive', 'ONegative']
const label = (type: string) => type.replace('Positive', '+').replace('Negative', '−')
const inDays = (days: number) => { const d = new Date(); d.setDate(d.getDate() + days); return d.toLocaleDateString('en-CA') }
/** Mirrors InventoryService.MaxShelfLifeDays so a mistyped year is caught in the form. */
const maxShelfLifeDays = 366

/** Every screen that shows stock reads one of these queries; refresh them together so no view keeps old numbers. */
export const refreshStock = (client: QueryClient) => Promise.all(['inventory', 'inventory-expiring', 'stock-report', 'locations'].map(key => client.invalidateQueries({ queryKey: [key] })))

export function InventoryPage() {
  const client = useQueryClient()
  const [showStock, setShowStock] = useState(false)
  const [showLocation, setShowLocation] = useState(false)
  const [notice, setNotice] = useState('')
  const [highlight, setHighlight] = useState<string>()
  const [editing, setEditing] = useState<string>()
  const lots = useQuery({ queryKey: ['inventory'], queryFn: api.inventory.list })
  const locations = useQuery({ queryKey: ['locations'], queryFn: api.inventory.locations })
  const expiring = useQuery({ queryKey: ['inventory-expiring'], queryFn: api.inventory.expiring })
  const report = useQuery({ queryKey: ['stock-report'], queryFn: api.inventory.report })
  const done = (message: string, lot?: InventoryLot) => { setNotice(message); setHighlight(lot?.id); setEditing(undefined); return refreshStock(client) }
  const stockIn = useMutation({ mutationFn: api.inventory.stockIn, onSuccess: lot => { setShowStock(false); done(`Received ${lot.unitsReceived} unit(s) of ${label(lot.bloodType)} at ${lot.locationName}, expiring ${formatDate(lot.expiryDate)}.`, lot) } })
  const createLocation = useMutation({ mutationFn: api.inventory.createLocation, onSuccess: location => { setShowLocation(false); done(`Location ${location.name} added. You can now receive stock there.`) } })
  const adjust = useMutation({ mutationFn: ({ lot, units }: { lot: InventoryLot; units: number }) => api.inventory.adjust(lot.id, units, lot.version), onSuccess: lot => done(`${label(lot.bloodType)} lot at ${lot.locationName} now has ${lot.unitsAvailable} unit(s) available.`, lot), onError: () => refreshStock(client) })
  const quarantine = useMutation({ mutationFn: api.inventory.quarantine, onSuccess: lot => done(`${label(lot.bloodType)} lot at ${lot.locationName} was quarantined and removed from available stock.`, lot) })
  const actionError = stockIn.error || createLocation.error || adjust.error || quarantine.error || locations.error

  // Bring the lot that just changed into view; FEFO ordering can place it far down the table.
  useEffect(() => { if (highlight) document.getElementById(`lot-${highlight}`)?.scrollIntoView({ block: 'center', behavior: 'smooth' }) }, [highlight, lots.data])

  function submitStock(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setNotice(''); const data = new FormData(event.currentTarget); stockIn.mutate({ locationId: String(data.get('locationId')), bloodType: String(data.get('bloodType')) as BloodType, units: Number(data.get('units')), expiryDate: String(data.get('expiryDate')), source: String(data.get('source')).trim() }) }
  function submitLocation(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setNotice(''); const data = new FormData(event.currentTarget); createLocation.mutate({ name: String(data.get('name')).trim(), address: String(data.get('address')).trim(), latitude: Number(data.get('latitude')), longitude: Number(data.get('longitude')) }) }
  function submitAdjust(event: FormEvent<HTMLFormElement>, lot: InventoryLot) { event.preventDefault(); setNotice(''); adjust.mutate({ lot, units: Number(new FormData(event.currentTarget).get('units')) }) }

  // Totals come from the server report so they cover every lot, not just the page of lots listed below.
  const totals = bloodTypes.map(type => [type, (report.data ?? []).filter(x => x.bloodType === type).reduce((sum, x) => sum + x.availableUnits, 0)] as const)

  return <>
    <header className="page-header"><div><p className="eyebrow">Supply network</p><h1>Blood inventory</h1><p>Lot-level availability, expiry pressure and dispatch readiness.</p></div><div className="header-actions"><button className="secondary-button" onClick={() => setShowLocation(v => !v)}>Add location</button><button className="primary-button" onClick={() => setShowStock(v => !v)}>Stock in</button></div></header>
    {showLocation && <form className="panel form-panel" onSubmit={submitLocation}><h2>New storage location</h2><div className="form-grid"><label>Name<input name="name" required /></label><label>Address<input name="address" required /></label><label>Latitude<input name="latitude" type="number" step="any" min="-90" max="90" required /></label><label>Longitude<input name="longitude" type="number" step="any" min="-180" max="180" required /></label></div><button className="primary-button" disabled={createLocation.isPending}>Create location</button></form>}
    {showStock && <form className="panel form-panel" onSubmit={submitStock}><h2>Receive blood stock</h2><div className="form-grid"><label>Location<select name="locationId" required><option value="">Select location</option>{locations.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label><label>Blood type<select name="bloodType">{bloodTypes.map(x => <option key={x} value={x}>{label(x)}</option>)}</select></label><label>Units<input name="units" type="number" min="1" max="10000" required /></label><label>Expiry date<input name="expiryDate" type="date" min={inDays(1)} max={inDays(maxShelfLifeDays)} required /></label><label>Source<input name="source" required maxLength={200} placeholder="e.g. Kandy donation camp" /></label></div><button className="primary-button" disabled={stockIn.isPending}>{stockIn.isPending ? 'Receiving…' : 'Receive stock'}</button></form>}
    {actionError && <div className="form-error" role="alert">{actionError.message}</div>}{notice && <div className="form-notice" role="status">{notice}</div>}
    <QueryState loading={lots.isLoading || expiring.isLoading} error={lots.error || expiring.error || report.error} empty={!lots.data?.items.length} onRetry={() => { lots.refetch(); expiring.refetch(); report.refetch() }}>
      <section className="stock-summary" aria-label="Available units by blood type">{totals.map(([type, units]) => <div key={type}><span className="blood-badge">{label(type)}</span><strong>{units}</strong><small>units</small></div>)}</section>
      <div className="notice-strip"><strong>{expiring.data?.length ?? 0} lots expire within seven days</strong><span>FEFO matching automatically prioritises the earliest safe stock. Showing {lots.data?.items.length} of {lots.data?.totalCount} lots.</span></div>
      <section className="panel table-panel"><table><thead><tr><th scope="col">Blood</th><th scope="col">Location</th><th scope="col">Available</th><th scope="col">Expiry</th><th scope="col">Source</th><th scope="col">Status</th><th scope="col"><span className="visually-hidden">Actions</span></th></tr></thead><tbody>{lots.data?.items.map(lot => <tr key={lot.id} id={`lot-${lot.id}`} className={highlight === lot.id ? 'row-highlight' : ''}>
        <td data-label="Blood type"><span className="blood-badge">{label(lot.bloodType)}</span></td><td data-label="Location"><strong>{lot.locationName}</strong><small>Updated {formatDate(lot.updatedAtUtc)}</small></td>
        <td data-label="Available">{editing === lot.id ? <form className="inline-form" onSubmit={event => submitAdjust(event, lot)}><input aria-label="Units available" name="units" type="number" min="0" max={lot.unitsReceived} defaultValue={lot.unitsAvailable} autoFocus required /><button className="text-button" disabled={adjust.isPending}>Save</button><button type="button" className="text-button" onClick={() => setEditing(undefined)}>Cancel</button></form> : <><strong>{lot.unitsAvailable}</strong> / {lot.unitsReceived}</>}</td>
        <td data-label="Expiry" className={expiring.data?.some(x => x.id === lot.id) ? 'danger-text' : ''}>{formatDate(lot.expiryDate)}</td><td data-label="Source">{lot.source}</td><td data-label="Status"><StatusPill value={lot.status} /></td>
        <td data-label="Actions">{lot.status !== 'Quarantined' && editing !== lot.id && <div className="action-row"><button className="text-button" onClick={() => { setEditing(lot.id); setNotice('') }}>Adjust</button><button className="text-button danger-text" disabled={quarantine.isPending} onClick={() => window.confirm(`Quarantine this ${label(lot.bloodType)} lot? Its units stop counting as available.`) && quarantine.mutate(lot.id)}>Quarantine</button></div>}</td>
      </tr>)}</tbody></table></section>
    </QueryState>
  </>
}
