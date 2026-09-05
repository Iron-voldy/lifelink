import type { ReactNode } from 'react'
import { ApiError } from '../api/client'
import { Icon } from './Icon'
export function QueryState({ loading,error,empty,children,onRetry }:{loading:boolean;error:Error|null;empty?:boolean;children:ReactNode;onRetry?:()=>void}){
 if(loading)return <div className="panel center-state" role="status"><span className="spinner"/><strong>Bringing your workspace up to date</strong><span>Loading the latest records…</span></div>
 // Retry re-runs the query; a full page reload would also throw away the in-memory session token.
 if(error)return <div className="panel error-state" role="alert"><Icon name="shield"/><strong>Unable to load data</strong><span>{error instanceof ApiError?error.message:'Please check your connection and try again.'}</span><button className="secondary-button" onClick={()=>onRetry?onRetry():window.location.reload()}>Try again</button></div>
 if(empty)return <div className="panel empty-state" role="status"><Icon name="inventory"/><strong>No matching records</strong><span>Try another filter, or check back when new activity arrives.</span></div>
 return <>{children}</>
}
export function StatusPill({value}:{value:string}){return <span className={`pill pill-${value.toLowerCase()}`}>{value.replace(/([a-z])([A-Z])/g,'$1 $2')}</span>}
const dateOnly=/^\d{4}-\d{2}-\d{2}$/
/** Calendar dates (YYYY-MM-DD) are shown as dates in local time; `new Date('2026-09-09')` would read them as UTC midnight and invent a time. */
export const formatDate=(value?:string,withTime=true)=>{
 if(!value)return '—'
 if(dateOnly.test(value)){const [y,m,d]=value.split('-').map(Number);return new Intl.DateTimeFormat('en-LK',{dateStyle:'medium'}).format(new Date(y,m-1,d))}
 return new Intl.DateTimeFormat('en-LK',withTime?{dateStyle:'medium',timeStyle:'short'}:{dateStyle:'medium'}).format(new Date(value))
}
