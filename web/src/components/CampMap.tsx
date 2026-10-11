import { useEffect, useRef, useState } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

export interface MapPoint { lat: number; lng: number }
export interface CampPin extends MapPoint { id: string; name: string; location: string; when: string; booked: boolean }

/** Great-circle distance in kilometres (haversine). */
export function distanceKm(a: MapPoint, b: MapPoint) {
  const rad = (d: number) => d * Math.PI / 180, r = 6371
  const dLat = rad(b.lat - a.lat), dLng = rad(b.lng - a.lng)
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(rad(a.lat)) * Math.cos(rad(b.lat)) * Math.sin(dLng / 2) ** 2
  return 2 * r * Math.asin(Math.sqrt(h))
}

const pin = (kind: 'you' | 'camp' | 'booked') => L.divIcon({ className: `map-pin map-pin-${kind}`, html: '<span></span>', iconSize: [22, 22], iconAnchor: [11, 11], popupAnchor: [0, -12] })

/** Popup content built from DOM nodes so user-entered camp names are never parsed as HTML. */
function popup(title: string, ...lines: string[]) {
  const box = document.createElement('div'); const strong = document.createElement('strong'); strong.textContent = title; box.append(strong)
  for (const line of lines) { const p = document.createElement('div'); p.textContent = line; box.append(p) }
  return box
}

/**
 * OpenStreetMap view of donation camps and the donor. "You" starts at the donor's saved address and can switch to the
 * browser's current position; booked camps are highlighted and joined to the donor with a dashed line and distance.
 */
export function CampMap({ home, camps }: { home: MapPoint | null; camps: CampPin[] }) {
  const element = useRef<HTMLDivElement>(null); const map = useRef<L.Map | null>(null); const layer = useRef<L.LayerGroup | null>(null)
  const [here, setHere] = useState<MapPoint | null>(null); const [locating, setLocating] = useState(false); const [geoError, setGeoError] = useState('')
  const you = here ?? home

  useEffect(() => {
    if (!element.current || map.current) return
    map.current = L.map(element.current, { scrollWheelZoom: false }).setView([7.87, 80.77], 7) // Sri Lanka
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' }).addTo(map.current)
    layer.current = L.layerGroup().addTo(map.current)
    return () => { map.current?.remove(); map.current = null }
  }, [])

  useEffect(() => {
    const m = map.current, group = layer.current; if (!m || !group) return
    group.clearLayers(); const bounds: L.LatLngExpression[] = []
    if (you) { L.marker([you.lat, you.lng], { icon: pin('you'), title: 'Your location' }).bindPopup(popup(here ? 'You are here' : 'Your saved address')).addTo(group); bounds.push([you.lat, you.lng]) }
    for (const camp of camps) {
      const away = you ? `${distanceKm(you, camp).toFixed(1)} km from you` : ''
      L.marker([camp.lat, camp.lng], { icon: pin(camp.booked ? 'booked' : 'camp'), title: camp.name }).bindPopup(popup(camp.name, camp.location, camp.when, ...(camp.booked ? ['Your slot is booked'] : []), ...(away ? [away] : []))).addTo(group)
      if (you && camp.booked) L.polyline([[you.lat, you.lng], [camp.lat, camp.lng]], { className: 'map-route', dashArray: '6 8' }).addTo(group)
      bounds.push([camp.lat, camp.lng])
    }
    if (bounds.length > 1) m.fitBounds(L.latLngBounds(bounds), { padding: [32, 32], maxZoom: 12 }); else if (bounds.length === 1) m.setView(bounds[0], 12)
  }, [you, here, camps])

  function locate() {
    if (!navigator.geolocation) { setGeoError('Your browser cannot share its location.'); return }
    setLocating(true); setGeoError('')
    navigator.geolocation.getCurrentPosition(p => { setHere({ lat: p.coords.latitude, lng: p.coords.longitude }); setLocating(false) }, () => { setGeoError('Location permission was denied, so the map uses your saved address.'); setLocating(false) }, { enableHighAccuracy: true, timeout: 10_000 })
  }

  const nearest = you && camps.length ? camps.reduce((best, c) => distanceKm(you, c) < distanceKm(you, best) ? c : best) : null
  return <div className="camp-map-card">
    <div className="camp-map-toolbar">
      <ul className="map-legend" aria-label="Map legend"><li><i className="map-pin map-pin-you" />You</li><li><i className="map-pin map-pin-booked" />Booked camp</li><li><i className="map-pin map-pin-camp" />Other camps</li></ul>
      <button type="button" className="text-button" onClick={locate} disabled={locating}>{locating ? 'Locating…' : here ? 'Update my location' : 'Use my current location'}</button>
    </div>
    <div ref={element} className="camp-map" role="region" aria-label="Map of donation camps and your location" />
    {geoError && <p className="field-hint" role="status">{geoError}</p>}
    {!you && <p className="field-hint">Add your address coordinates or use your current location to see distances.</p>}
    {nearest && you && <p className="field-hint">Nearest camp: <strong>{nearest.name}</strong>, {distanceKm(you, nearest).toFixed(1)} km away.</p>}
  </div>
}
