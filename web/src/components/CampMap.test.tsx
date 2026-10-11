import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { CampMap, distanceKm } from './CampMap'

describe('CampMap', () => {
  it('measures great-circle distance between Colombo and Kandy', () => {
    expect(distanceKm({ lat: 6.9271, lng: 79.8612 }, { lat: 7.2906, lng: 80.6337 })).toBeCloseTo(94, 0)
  })

  it('shows the legend and the nearest camp to the donor', () => {
    render(<CampMap home={{ lat: 6.9271, lng: 79.8612 }} camps={[
      { id: '1', name: 'Kandy Camp', location: 'Kandy', when: 'Mon', booked: true, lat: 7.2906, lng: 80.6337 },
      { id: '2', name: 'Jaffna Camp', location: 'Jaffna', when: 'Tue', booked: false, lat: 9.6615, lng: 80.0255 },
    ]} />)
    expect(screen.getByRole('region', { name: 'Map of donation camps and your location' })).toBeInTheDocument()
    expect(screen.getByLabelText('Map legend')).toHaveTextContent('Booked camp')
    expect(screen.getByText('Kandy Camp')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Use my current location' })).toBeEnabled()
  })
})
