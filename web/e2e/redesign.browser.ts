import { test, expect, type Page } from '@playwright/test'
const now='2026-09-29T08:00:00Z'
const pageOf=(items:unknown[])=>({items,page:1,pageSize:100,totalCount:items.length,totalPages:1})
const flow={id:'flow-1',bloodRequestId:'request-1',attemptNumber:1,objective:'Coordinate urgent O+ supply for Colombo General',status:'PendingApproval',correlationId:'review-correlation-123456789',startedAtUtc:now,planJson:'{"units": 4, "requiresHumanApproval": true}',steps:[{id:'step-1',sequence:1,agentName:'Supply coordination',status:'Completed',outputJson:'{"availableUnits": 12}',toolCallsJson:'[]'}],approvals:[]}
async function fixtures(page:Page,theme:string){
 await page.addInitScript(theme=>{if(!localStorage.getItem('lifelink.theme'))localStorage.setItem('lifelink.theme',theme);sessionStorage.setItem('lifelink.refresh-token','review-only-token')},theme)
 await page.route('http://localhost:5080/api/**',async route=>{
  const url=new URL(route.request().url());const p=url.pathname
  let data:unknown=[]
  if(p.endsWith('/auth/refresh'))data={accessToken:'review-access',refreshToken:'review-only-token'}
  else if(p.endsWith('/auth/me'))data={id:'admin',email:'admin@lifelink.lk',role:'BloodBankAdmin'}
  else if(p.endsWith('/requests/reports/summary'))data={open:18,critical:4,fulfilled:126,total:144}
  else if(p.includes('/agent-workflows'))data=p.endsWith('execution-summary')?flow:[flow,{...flow,id:'flow-2',status:'Completed',objective:'Review blood supply coverage across the network'},{...flow,id:'flow-3',status:'Running',objective:'Match eligible donors to hospital demand'}]
  else if(p.endsWith('/reports/stock-levels'))data=['APositive','ANegative','BPositive','BNegative','ABPositive','ABNegative','OPositive','ONegative'].map((bloodType,i)=>({bloodType,locationId:'loc-1',locationName:'Colombo Central',availableUnits:[48,16,32,12,24,8,64,20][i]}))
  else if(p.endsWith('/inventory/locations'))data=[{id:'loc-1',name:'Colombo Central'}]
  else if(p.endsWith('/inventory/expiring-soon'))data=[{id:'lot-1'}]
  else if(p.endsWith('/inventory'))data=pageOf(['APositive','ONegative','BPositive'].map((bloodType,i)=>({id:`lot-${i+1}`,bloodType,locationName:'Colombo Central',unitsAvailable:24,unitsReceived:40,expiryDate:'2026-10-04',source:'Community donation camp',status:i===1?'Reserved':'Available'})))
  else if(p.endsWith('/donors'))data=pageOf(['APositive','ONegative','BPositive'].map((bloodType,i)=>({id:`donor-${i}`,email:['amal@example.test','nisha@example.test','kasun@example.test'][i],bloodType,eligibilityStatus:i===1?'PendingVerification':'Eligible',medicalFlags:[],lastDonationDate:now,address:'Colombo, Western Province',isActive:true})))
  else if(p.endsWith('/hospitals'))data=pageOf(['Colombo General Hospital','Kandy Teaching Hospital','Galle District Hospital'].map((name,i)=>({id:`hospital-${i}`,name,registrationNumber:`SL-MOH-001${i}`,verificationStatus:i===1?'Verified':'Pending',address:['Regent Street, Colombo','William Gopallawa Mawatha, Kandy','Karapitiya, Galle'][i]})))
  else if(p.endsWith('/requests'))data=pageOf(['Critical','Urgent','Routine'].map((urgency,i)=>({id:`request-${i}`,hospitalName:['Colombo General Hospital','Kandy Teaching Hospital','Galle District Hospital'][i],bloodType:['OPositive','ANegative','BPositive'][i],quantityUnits:4,urgency,status:'Submitted',notes:'Blood required for scheduled patient care. Please review available supply.',requiredByUtc:now})))
  else if(p.endsWith('/camps'))data=pageOf(['Colombo community donation day','University donor drive','Kandy community blood camp'].map((name,i)=>({id:`camp-${i}`,name,location:['Colombo Town Hall','University of Colombo','Kandy Community Centre'][i],startsAtUtc:'2026-10-04T08:00:00Z',endsAtUtc:'2026-10-04T16:00:00Z',capacity:100,bookedSlots:35+i*20,status:'Scheduled'})))
  await route.fulfill({json:data})
 })
}
const routes=[['dashboard','Command overview'],['donors','Donors'],['hospitals','Hospital verification'],['requests','Request triage'],['inventory','Blood inventory'],['camps','Camp schedule'],['workflows','AI workflow monitor']]
for(const width of [320,390,820,1440])for(const theme of ['light','dark'])test(`all pages ${width}px ${theme}`,async({page},info)=>{
 await page.setViewportSize({width,height:1000});await fixtures(page,theme)
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message))
 for(const [route,heading]of routes){
  await page.goto('/'+route);await expect(page.getByRole('heading',{name:heading,exact:true})).toBeVisible();await expect(page.locator('.center-state')).toHaveCount(0)
  await expect(page.locator('html')).toHaveAttribute('data-theme',theme)
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`${route} must fit viewport`).toBe(true)
  await page.screenshot({path:info.outputPath(`${route}.png`),fullPage:true,animations:'disabled'})
 }
 if(width<=390){await page.getByRole('button',{name:'Open menu',exact:true}).click();await expect(page.getByRole('navigation')).toBeVisible();await page.getByRole('link',{name:'Donors',exact:true}).click();await expect(page.getByRole('heading',{name:'Donors',exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Open menu',exact:true})).toHaveAttribute('aria-expanded','false')}
 await page.getByRole('button',{name:`Switch to ${theme==='light'?'dark':'light'} mode`}).click();await page.reload();await expect(page.locator('html')).toHaveAttribute('data-theme',theme==='light'?'dark':'light')
 expect(errors).toEqual([])
})
for(const width of [390,1440])test(`login ${width}px`,async({page},info)=>{
 await page.setViewportSize({width,height:1000});await page.goto('/login');await expect(page.getByRole('heading',{name:'Welcome back'})).toBeVisible()
 await page.getByLabel('Password',{exact:true}).fill('sample-password');await page.getByRole('button',{name:'Show password'}).click();await expect(page.getByLabel('Password',{exact:true})).toHaveAttribute('type','text')
 for(const theme of ['light','dark']){if(theme==='dark')await page.getByRole('button',{name:'Switch to dark mode'}).click();await page.screenshot({path:info.outputPath(`login-${theme}.png`),fullPage:true,animations:'disabled'});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)}
})
test('forms, empty and failed requests remain usable',async({page},info)=>{
 await fixtures(page,'light');await page.goto('/inventory');await page.getByRole('button',{name:'Stock in',exact:true}).click();await expect(page.getByRole('heading',{name:'Receive blood stock'})).toBeVisible();await page.screenshot({path:info.outputPath('inventory-form.png'),fullPage:true})
 await page.goto('/camps');await page.getByRole('button',{name:'Create camp',exact:true}).click();await expect(page.getByLabel('Capacity',{exact:true})).toBeVisible()
 await page.route('http://localhost:5080/api/donors?**',route=>route.fulfill({json:pageOf([])}));await page.goto('/donors');await expect(page.getByText('No matching records')).toBeVisible()
 await page.route('http://localhost:5080/api/donors?**',route=>route.fulfill({status:503,json:{detail:'The service is temporarily unavailable.'}}));await page.reload();await expect(page.getByText('Unable to load data')).toBeVisible();await expect(page.getByRole('button',{name:'Try again'})).toBeVisible()
})


