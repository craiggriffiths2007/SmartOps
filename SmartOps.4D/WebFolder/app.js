'use strict';
const $=s=>document.querySelector(s);
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const money=v=>new Intl.NumberFormat('en-GB',{style:'currency',currency:'GBP'}).format(Number(v)||0);
const statuses={0:'Reported',10:'Survey booked',20:'Survey completed',40:'Awaiting approval',60:'Authorised',70:'Fitting booked',80:'Fitting completed',90:'Invoiced'};
const jobTypes=['Survey','Fitting','Remedial','Complaint','Securing'];
const today=()=>new Date().toLocaleDateString('en-CA');
const label=s=>s.replace(/([a-z])([A-Z])/g,'$1 $2').replace(/^Go /,'');
const state={token:null,role:null,username:null,company:null,schemas:null,offset:0,table:'Customer',date:today(),screen:'contracts'};
const staff=()=>['Admin','Staff'].includes(state.role);
let renderVersion=0;
async function api(path,method='GET',body){
 const r=await fetch('/api/'+path,{method,headers:{...(state.token?{Authorization:'Bearer '+state.token}:{}),...(body!==undefined?{'Content-Type':'application/json'}:{})},...(body!==undefined?{body:JSON.stringify(body)}:{})});
 const data=await r.json();
 if(!r.ok){if(r.status===401&&state.token)signOutLocal();throw Error(data.error||'The request failed');}
 return data;
}
function toast(message){$('#toast').textContent=message;$('#toast').style.display='block';setTimeout(()=>$('#toast').style.display='none',3500);}
function signOutLocal(){state.token=null;state.schemas=null;$('#workspace').hidden=true;$('#login').hidden=false;$('#loginForm').elements.Password.value='';$('#modal').close();}
$('#loginForm').onsubmit=async e=>{
 e.preventDefault();const button=e.target.querySelector('button');button.disabled=true;$('#loginError').textContent='';
 try{const data=await api('auth/login','POST',Object.fromEntries(new FormData(e.target)));Object.assign(state,data);$('#login').hidden=true;$('#workspace').hidden=false;$('#company').textContent=data.company;$('#identity').textContent=data.username+' · '+data.role;navigation();await route();}
 catch(error){$('#loginError').textContent=error.message;}finally{button.disabled=false;}
};
$('#logout').onclick=async()=>{try{await api('auth/logout','POST',{});signOutLocal();}catch(e){toast(e.message);}};
$('#password').onclick=()=>form('Change password',[
 {name:'OldPassword',label:'Current password',type:'password',required:true},{name:'NewPassword',label:'New password (12–64 characters)',type:'password',required:true,minLength:12,maxLength:64}
],{},async values=>{await api('auth/password','POST',values);signOutLocal();toast('Password changed. Sign in again.');},'Change password');
$('#closeModal').onclick=()=>$('#modal').close();
function navigation(){
 const links=[['contracts',state.role==='Customer'?'My repairs':'Contracts']];
 if(staff())links.push(['diary','Diary'],['records','Records'],['surveys','Survey uploads'],['media','Photos & videos']);
 if(staff()||state.role==='Insurer')links.push(['claims','Claims']);
 if(staff()||state.role==='Customer')links.push(['support','Support']);
 if(state.role==='Admin')links.push(['accounts','Accounts'],['audit','Audit trail']);
 $('#nav').innerHTML=links.map(([key,title])=>`<a href="#${key}" data-page="${key}">${esc(title)}</a>`).join('');
}
window.addEventListener('hashchange',()=>{state.offset=0;route();});
async function route(){
 if(!state.token)return;
 const ver=++renderVersion;
 state.screen=location.hash.slice(1).split('/')[0]||'contracts';
 $('#notice').textContent='';$('#content').innerHTML='<div class="loading">Loading workspace…</div>';$('#tools').innerHTML='';
 document.querySelectorAll('nav a').forEach(a=>a.classList.toggle('active',a.dataset.page===state.screen));
 try{
  const screens={contracts:contracts,diary:diary,records:records,surveys:surveys,media:media,claims:claims,support:support,accounts:accounts,audit:audit};
  await(screens[state.screen]||contracts)(ver);
 }catch(e){if(ver===renderVersion){$('#content').innerHTML='';$('#notice').textContent=e.message;}}
}
function title(text,section='WORKSPACE'){$('#heading').textContent=text;$('#sectionLabel').textContent=section;}
function button(text,action,cls=''){const b=document.createElement('button');b.textContent=text;b.className=cls;b.onclick=()=>Promise.resolve(action()).catch(e=>toast(e.message));return b;}
function tool(text,action,cls=''){$('#tools').append(button(text,action,cls));}
function pill(status,cancelled=false){return `<span class="pill ${status>=60?'green':status===40?'amber':'blue'}">${esc(cancelled?'Cancelled':statuses[status]??status)}</span>`;}
function empty(text){return `<div class="empty">${esc(text)}</div>`;}
function table(columns,items,actions){
 if(!items.length)return empty('No records to show.');
 return `<div class="tableWrap"><table><thead><tr>${columns.map(c=>`<th>${esc(c[1])}</th>`).join('')}${actions?'<th>Actions</th>':''}</tr></thead><tbody>${items.map((item,i)=>`<tr>${columns.map(([key,,formatter])=>`<td class="${key.includes('Description')||key==='Subject'?'wrap':''}">${formatter?formatter(item[key],item):esc(item[key])}</td>`).join('')}${actions?`<td><div class="actions">${actions(item,i)}</div></td>`:''}</tr>`).join('')}</tbody></table></div>`;
}
function bind(container,selector,items,fn){$(container).querySelectorAll(selector).forEach(el=>el.onclick=()=>Promise.resolve(fn(items[Number(el.dataset.index)])).catch(e=>toast(e.message)));}
function pager(result){
 const p=document.createElement('div');p.className='pager';const prev=button('← Previous',()=>{state.offset=Math.max(0,state.offset-100);return route();},'quiet');prev.disabled=state.offset===0;
 const next=button('Next →',()=>{state.offset+=100;return route();},'quiet');next.disabled=state.offset+100>=result.total;
 const text=document.createElement('span');text.className='muted';text.textContent=result.total?`${state.offset+1}–${Math.min(state.offset+100,result.total)} of ${result.total}`:'0 records';p.append(prev,text,next);$('#content').append(p);
}
function showModal(heading,html){$('#modalTitle').textContent=heading;$('#modalBody').innerHTML=html;if(!$('#modal').open)$('#modal').showModal();}
function input(field,value){
 const name=esc(field.name),v=value??field.default??'',type=field.type||'text';
 if(type==='checkbox')return `<label><input name="${name}" type="checkbox" ${v?'checked':''}>${esc(field.label||label(field.name))}</label>`;
 let control;
 const attrs=`${field.required?'required':''} ${field.min!==undefined?`min="${field.min}"`:''} ${field.max!==undefined?`max="${field.max}"`:''} ${field.minLength?`minlength="${field.minLength}"`:''} maxlength="${field.maxLength||32000}"`;
 if(field.options)control=`<select name="${name}" ${attrs}>${field.options.map(o=>`<option value="${esc(o.value)}" ${String(o.value)===String(v)?'selected':''}>${esc(o.label)}</option>`).join('')}</select>`;
 else if(type==='textarea')control=`<textarea name="${name}" ${attrs}>${esc(v)}</textarea>`;
 else control=`<input name="${name}" type="${type}" value="${esc(v)}" ${attrs} ${type==='number'?`step="${field.step||'any'}"`:''}>`;
 return `<label class="${type==='textarea'?'wide':''}">${esc(field.label||label(field.name))}${control}</label>`;
}
function form(heading,fields,values,onSave,saveLabel='Save'){
 showModal(heading,`<form id="editForm"><div class="formGrid">${fields.map(f=>input(f,values[f.name])).join('')}</div><div id="extraFields"></div><p class="error" role="alert" id="formError"></p><div class="dialogActions"><button type="button" class="quiet" id="cancelForm">Cancel</button><button id="saveForm">${esc(saveLabel)}</button></div></form>`);
 $('#cancelForm').onclick=()=>$('#modal').close();
 $('#editForm').onsubmit=async e=>{e.preventDefault();const data={};for(const f of fields){const el=e.target.elements[f.name];data[f.name]=f.type==='checkbox'?el.checked:(f.type==='number'||f.numeric)?(el.value===''?null:Number(el.value)):el.value;}
  $('#saveForm').disabled=true;$('#formError').textContent='';
  try{await onSave(data,e.target);$('#modal').close();toast('Saved successfully');await route();}catch(error){if($('#formError'))$('#formError').textContent=error.message;}finally{if($('#saveForm'))$('#saveForm').disabled=false;}
 };
}
const field=(name,opts={})=>({name,...opts});
async function options(tableName,textField){const data=await api('data/'+tableName);return data.items.map(i=>({value:i.Id,label:`${i.Id} · ${i[textField]||i.Name||i.Code||''}`}));}
async function contracts(ver){
 title(state.role==='Customer'?'My repairs':'Contracts',state.role==='Insurer'?'INSURER PORTAL':state.role==='Customer'?'CUSTOMER PORTAL':'REPAIR OPERATIONS');
 const result=await api('portal?offset='+state.offset);if(ver!==renderVersion)return;
 if(staff())tool('+ New contract',newContract);
 const items=result.items;
 $('#content').innerHTML=(staff()?`<div class="stats"><div class="stat"><span>Contracts in company</span><b>${result.total}</b></div><div class="stat"><span>Awaiting approval · this page</span><b>${items.filter(c=>c.Status===40&&!c.IsCancelled).length}</b></div><div class="stat"><span>Work booked · this page</span><b>${items.filter(c=>[10,70].includes(c.Status)&&!c.IsCancelled).length}</b></div><div class="stat"><span>Completed · this page</span><b>${items.filter(c=>c.Status>=80).length}</b></div></div>`:'')+table([['ContractCode','Contract'],['CustomerReference','Reference'],['DamageDescription','Repair'],['Status','Progress',(_,c)=>pill(c.Status,c.IsCancelled)]],items,(c,i)=>`<button class="small quiet detail" data-index="${i}">View repair →</button>`);
 bind('#content','.detail',items,c=>contractDetail(c.Id));pager(result);
}
async function newContract(){
 const [customers,insurers]=await Promise.all([options('Customer','Name'),options('Insurers','Name')]);
 form('Report a new repair',[field('CustomerId',{options:customers,numeric:true,required:true,label:'Customer'}),field('InsurerId',{options:[{value:'',label:'No insurer'},...insurers],numeric:true,label:'Insurer'}),field('CustomerReference'),field('PolicyNumber'),field('IncidentDate',{type:'date',required:true,default:today()}),field('Excess',{default:'100.00'}),field('CauseOfDamage',{required:true}),field('DamageDescription',{type:'textarea',required:true})],{},v=>api('contracts','POST',v),'Create contract');
}
async function contractDetail(id){
 const d=await api('contracts/'+id),c=d.contract;
 showModal('Repair '+c.ContractCode,`<div class="card"><header><h3>${esc(c.DamageDescription)}</h3>${pill(c.Status,c.IsCancelled)}</header><div class="facts"><div class="fact"><small>Customer</small><b>${esc(d.customer.Name)}</b><p>${esc(d.customer.Add1)}<br>${esc(d.customer.Postcode)}</p></div><div class="fact"><small>Policy</small><b>${esc(c.PolicyNumber)}</b><p>Excess ${esc(c.Excess)}</p></div><div class="fact"><small>Incident</small><b>${esc(c.IncidentDate)}</b><p>${esc(c.CauseOfDamage)}</p></div></div><div class="actions" id="contractActions"></div></div><div class="card"><h3>Appointments</h3><div id="detailJobs"></div></div><div class="card"><h3>Estimates & invoices</h3><div id="finance"></div></div><div class="card"><h3>Repair progress</h3><div class="timeline">${d.updates.length?d.updates.map(u=>`<article><small>${esc(u.DateTime)}${u.CustomerVisible?'':' · Internal'}</small><h3>${esc(u.Title)}</h3><p>${esc(u.Description)}</p></article>`).join(''):'<p>No updates yet.</p>'}</div></div>${state.role==='Insurer'||staff()?'<div class="card"><h3>Insurer correspondence</h3><div id="messages"></div></div>':''}`);
 const actions=$('#contractActions');
 if(staff()&&!c.IsCancelled){
  actions.append(button('Book appointment',()=>booking(c)));
  if(c.Status<60)actions.append(button('Prepare estimate',()=>estimateForm(c),'quiet'));
  if(c.Status===80)actions.append(button('Issue invoice',()=>form('Issue invoice',[field('Description',{required:true,default:'Glazing repair and fitting'}),field('BillingParty',{options:[{value:0,label:'Customer'},{value:1,label:'Insurer'}],numeric:true})],{},v=>api('invoices','POST',{...v,ContractId:id}),'Issue invoice'),'quiet'));
  actions.append(button('Add update',()=>form('Add repair update',[field('Title',{required:true}),field('Description',{type:'textarea',required:true}),field('CustomerVisible',{type:'checkbox',default:true})],{},v=>api('timeline','POST',{...v,ContractId:id})),'quiet'));
  if(c.Status!==90)actions.append(button('Cancel repair',()=>form('Cancel repair',[field('Reason',{type:'textarea',required:true})],{},v=>api('contracts/cancel','POST',{...v,ContractId:id,GoRevision:c.GoRevision}),'Cancel repair'),'danger'));
 }
 $('#detailJobs').innerHTML=table([['DiaryDate','Date'],['Time','Time'],['JobType','Type',v=>esc(jobTypes[v])],['AgentId','Agent']],d.jobs,staff()?(j,i)=>`<button class="small quiet complete" data-index="${i}">Complete</button><button class="small quiet rebook" data-index="${i}">Rebook</button><button class="small danger cancelJob" data-index="${i}">Cancel</button>`:null);
 bind('#detailJobs','.complete',d.jobs,j=>completeJob(j));bind('#detailJobs','.rebook',d.jobs,j=>booking(c,j));bind('#detailJobs','.cancelJob',d.jobs,j=>cancelBooking(j));
 const finance=$('#finance');
 for(const e of d.estimates){const row=document.createElement('div');row.className='actions';row.style.margin='12px 0';row.append(document.createTextNode(`Estimate ${e.Id} · ${money(e.Total)} `),button('View',()=>documentView('estimates',e.Id),'small quiet'));
  if((staff()||state.role==='Insurer')&&c.Status===40&&c.SelectedEstimateId===e.Id&&!c.IsCancelled)row.append(button('Authorise',async()=>{await api('estimates/authorise','POST',{EstimateId:e.Id,GoRevision:e.GoRevision});toast('Repair authorised');await contractDetail(id);},'small'));
  finance.append(row);
 }
 for(const i of d.invoices){const row=document.createElement('div');row.className='actions';row.style.margin='12px 0';row.append(document.createTextNode(`Invoice INV-${String(i.Id).padStart(8,'0')} · ${money(i.Total)} `),button('View / print',()=>documentView('invoices',i.Id),'small quiet'));finance.append(row);}
 if(!d.estimates.length&&!d.invoices.length)finance.innerHTML='<p class="muted">No documents yet.</p>';
 if($('#messages'))await messages(c);
}
function completeJob(j){form('Complete '+jobTypes[j.JobType],[field('Description',{label:'Completion notes',type:'textarea',required:true})],{},v=>api('jobs/complete','POST',{...v,Id:j.Id}),'Mark complete');}
function cancelBooking(j){form('Cancel appointment',[field('Reason',{type:'textarea',required:true})],{},v=>api('diary/cancel','POST',{...v,Id:j.Id,GoRevision:j.GoRevision}),'Cancel appointment');}
async function booking(c,job={}){
 const agents=await options('Agent','Name');
 form(job.Id?'Move appointment':'Book appointment',[field('AgentId',{options:agents,numeric:true,required:true,label:'Agent'}),field('JobType',{options:jobTypes.map((label,value)=>({label,value})),numeric:true}),field('DiaryDate',{type:'date',required:true,default:state.date}),field('Time',{type:'time',required:true,default:'09:00'}),field('DurationMinutes',{type:'number',min:15,max:480,step:1,required:true,default:60}),field('Instructions',{type:'textarea'})],{...job,DurationMinutes:job.GoEndMinute?job.GoEndMinute-job.GoStartMinute:60},v=>api('diary/book','POST',{...v,ContractId:c.Id,...(job.Id?{Id:job.Id,GoRevision:job.GoRevision}:{})}),'Save appointment');
}
function estimateForm(c){
 form('Prepare estimate',[field('ServiceLevel',{default:'Standard repair',required:true}),field('VatRate',{type:'number',min:0,max:100,default:20,required:true})],{},async(v,formEl)=>{
  const lines=[...formEl.querySelectorAll('.lineRow')].map(row=>({Details:row.querySelector('[name=lineDetails]').value,Cost:Number(row.querySelector('[name=lineCost]').value)}));
  await api('estimates','POST',{...v,ContractId:c.Id,Lines:lines});
 },'Create estimate');
 $('#extraFields').innerHTML='<h3>Estimate lines</h3><div id="estimateLines"></div><button type="button" class="quiet" id="addLine">+ Add line</button>';
 const addLine=()=>{const row=document.createElement('div');row.className='lineRow';row.innerHTML='<input name="lineDetails" placeholder="Work description" maxlength="2000" required aria-label="Line description"><input name="lineCost" type="number" min="0" max="1000000" step=".01" required placeholder="Cost (£)" aria-label="Line cost"><button type="button" class="quiet" aria-label="Remove line">×</button>';row.querySelector('button').onclick=()=>row.remove();$('#estimateLines').append(row);};
 $('#addLine').onclick=addLine;addLine();
}
async function documentView(type,id){
 const {document:d,lines}=await api(type+'/'+id),invoice=type==='invoices';
 showModal(invoice?'Invoice':'Estimate',`<div class="document"><p class="eyebrow">SMARTOPS 4D</p><h1>${invoice?'Invoice INV-':'Estimate EST-'}${String(id).padStart(8,'0')}</h1>${invoice?`<div class="grid"><div><h3>${esc(d.SellerName)}</h3><p>${esc(d.SellerAddress)}<br>${esc(d.SellerEmail)}</p></div><div><small>Bill to</small><h3>${esc(d.BillToName)}</h3><p>${esc(d.BillToAddress)}</p></div></div>`:`<p>${esc(d.ServiceLevel)} · Contract ID ${esc(d.ContractId)}</p>`}<p>${esc(d.Description||'')}</p>${table([['Details','Description'],['Cost','Amount',v=>money(v)]],lines)}<p style="text-align:right">Subtotal ${money(d.Subtotal)}<br>VAT ${esc(d.VatRate)}% ${money(d.VatAmount)}</p><p class="total">Total ${money(d.Total)}</p><p class="muted">${esc(d.IssuedUtc||d.CreatedUtc)}</p><button class="noPrint" id="printDocument">Print / save as PDF</button></div>`);
 $('#printDocument').onclick=()=>window.print();
}
async function messages(c){
 const result=await api('messages?contractId='+c.Id);if(!$('#messages'))return;
 $('#messages').innerHTML=result.items.map(m=>`<div class="bubble"><small>${esc(m.SenderRole)} · ${esc(m.CreatedAt)}</small>${esc(m.Body)}</div>`).join('')||'<p class="muted">No messages yet.</p>';
 $('#messages').append(button('Send message',()=>form('Message insurer / repair company',[field('Body',{type:'textarea',required:true,label:'Message'})],{},v=>api('messages','POST',{...v,ContractId:c.Id})),'quiet'));
}
async function diary(ver){
 title('Diary','SCHEDULING');
 const [result,agents]=await Promise.all([api('diary?date='+state.date+'&offset='+state.offset),api('data/Agent')]);if(ver!==renderVersion)return;
 const map=Object.fromEntries(agents.items.map(a=>[a.Id,a.Name]));
 const date=document.createElement('input');date.type='date';date.value=state.date;date.setAttribute('aria-label','Diary date');date.onchange=()=>{state.date=date.value;state.offset=0;route();};$('#tools').append(date);
 tool('Book repair',async()=>{const repairs=await api('portal');form('Choose a contract',[field('ContractId',{options:repairs.items.filter(c=>!c.IsCancelled).map(c=>({value:c.Id,label:c.ContractCode+' · '+c.DamageDescription})),numeric:true,required:true})],{},async v=>{const chosen=repairs.items.find(c=>c.Id===v.ContractId);setTimeout(()=>booking(chosen).catch(e=>toast(e.message)),0);},'Continue');});
 $('#content').innerHTML=table([['Time','Start'],['GoEndMinute','Finish',v=>`${String(Math.floor(v/60)).padStart(2,'0')}:${String(v%60).padStart(2,'0')}`],['AgentId','Agent',v=>esc(map[v]||v)],['ContractCode','Contract'],['JobType','Visit',v=>esc(jobTypes[v])],['Instructions','Instructions']],result.items,(j,i)=>`<button class="small quiet detail" data-index="${i}">View repair</button><button class="small quiet rebook" data-index="${i}">Move</button>`);
 bind('#content','.detail',result.items,j=>contractDetail(j.ContractId));bind('#content','.rebook',result.items,j=>booking({Id:j.ContractId},j));pager(result);
}
async function records(ver){
 title('Company records','ADMINISTRATION');
 if(!state.schemas)state.schemas=(await api('schema')).items.sort((a,b)=>a.name.localeCompare(b.name));
 const schema=state.schemas.find(s=>s.name===state.table)||state.schemas[0];state.table=schema.name;
 const select=document.createElement('select');select.setAttribute('aria-label','Record type');select.innerHTML=state.schemas.map(s=>`<option ${s.name===state.table?'selected':''}>${esc(s.name)}</option>`).join('');select.onchange=()=>{state.table=select.value;state.offset=0;route();};$('#tools').append(select);
 tool('+ New record',()=>editRecord(schema,{}));
 const result=await api('data/'+state.table+'?offset='+state.offset);if(ver!==renderVersion)return;
 const preferred=['Id','Name','Code','Registration','CustomerId','ContractId','Description','Postcode','Email','Active','IsActive'];let cols=preferred.filter(c=>schema.publicFields.includes(c)).slice(0,7);if(cols.length<3)cols=schema.publicFields.filter(c=>!c.startsWith('Go')&&c!=='TenantId').slice(0,7);
 $('#content').innerHTML=`<p class="muted">${esc(state.table)} · ${schema.editableFields.length} editable fields</p>`+table(cols.map(c=>[c,label(c)]),result.items,(r,i)=>`<button class="small quiet edit" data-index="${i}">Edit</button>`);
 bind('#content','.edit',result.items,r=>editRecord(schema,r));pager(result);
}
function editRecord(schema,row){
 const fields=schema.fields.filter(f=>schema.editableFields.includes(f.name)).map(f=>field(f.name,{type:f.kind==='boolean'?'checkbox':['integer','number'].includes(f.kind)?'number':f.format==='date-time'?'text':f.name.match(/Description|Instructions|Notes|Json/)?'textarea':'text',step:f.kind==='integer'?1:'any',maxLength:f.maxLength||32000,default:!f.nullable&&['integer','number'].includes(f.kind)?0:''}));
 form(`${row.Id?'Edit':'New'} ${schema.name}`,fields,row,v=>api('data/'+schema.name+(row.Id?'/'+row.Id:''),row.Id?'PUT':'POST',{...v,...(row.Id?{GoRevision:row.GoRevision}:{})}));
}
async function claims(ver){
 title('Claims','INSURER OPERATIONS');
 const result=await api('claims?offset='+state.offset);if(ver!==renderVersion)return;
 tool('+ New claim',newClaim);
 $('#content').innerHTML=table([['ClaimReference','Claim'],['CustomerName','Customer'],['Postcode','Postcode'],['DamageDescription','Damage'],['Status','Status',v=>esc(['New','On job board','Assigned','Survey booked','Awaiting approval','Repair booked','In progress','Completed','Cancelled'][v]||v)]],result.items,(c,i)=>`${staff()&&c.Status===0?`<button class="small quiet convert" data-index="${i}">Create repair</button>`:''}<button class="small quiet viewClaim" data-index="${i}">Details</button>`);
 bind('#content','.convert',result.items,c=>form('Create repair from claim',[field('Confirm',{label:'Create a customer and repair contract for this claim',type:'checkbox',default:true})],{},v=>{if(!v.Confirm)throw Error('Select the checkbox to continue');return api('claims/convert','POST',{Id:c.Id,GoRevision:c.GoRevision});},'Create repair'));
 bind('#content','.viewClaim',result.items,c=>showModal('Claim '+c.ClaimReference,`<div class="facts">${['CustomerName','Address1','Postcode','Email','Telephone','PolicyNumber','IncidentDate','CauseOfDamage','DamageDescription'].map(k=>`<div class="fact"><small>${esc(label(k))}</small><b>${esc(c[k])}</b></div>`).join('')}</div>`));pager(result);
}
async function newClaim(){
 const fields=[field('ClaimReference',{required:true,maxLength:50}),field('CustomerName',{required:true,maxLength:100}),field('Address1'),field('Postcode'),field('Email',{type:'email'}),field('Telephone'),field('PolicyNumber'),field('IncidentDate',{type:'date',required:true,default:today()}),field('Excess',{type:'number',default:100,min:0}),field('CauseOfDamage',{required:true}),field('DamageDescription',{type:'textarea',required:true})];
 if(staff())fields.unshift(field('InsurerId',{options:await options('Insurers','Name'),numeric:true,required:true}));
 form('New insurance claim',fields,{},v=>api('claims','POST',v),'Submit claim');
}
async function support(ver){
 title(state.role==='Customer'?'Help with your repair':'Customer support','SUPPORT');
 const result=await api('tickets?offset='+state.offset);if(ver!==renderVersion)return;
 tool('+ New ticket',async()=>{const fields=[field('Subject',{required:true,maxLength:200}),field('Body',{type:'textarea',required:true,label:'Message'})];if(staff())fields.unshift(field('CustomerId',{options:await options('Customer','Name'),numeric:true,required:true}));form('New support ticket',fields,{},v=>api('tickets','POST',v),'Send ticket');});
 $('#content').innerHTML=table([['Id','Ticket'],['Subject','Subject'],['Status','Status'],['CreatedAt','Opened']],result.items,(t,i)=>`<button class="small quiet viewTicket" data-index="${i}">Conversation →</button>`);
 bind('#content','.viewTicket',result.items,t=>ticket(t));pager(result);
}
async function ticket(t){
 const result=await api('ticket-messages?ticketId='+t.Id);
 showModal('Ticket #'+t.Id+' · '+t.Subject,`<p class="muted">${esc(t.Status)}</p>${result.items.map(m=>`<div class="bubble"><small>${esc(m.SenderRole)} · ${esc(m.CreatedAt)}</small>${esc(m.Body)}</div>`).join('')}<div class="actions" id="ticketActions"></div>`);
 if(t.Status!=='Closed')$('#ticketActions').append(button('Reply',()=>form('Reply to ticket',[field('Body',{type:'textarea',required:true,label:'Message'})],{},v=>api('ticket-messages','POST',{...v,TicketId:t.Id}))));
 if(staff()&&t.Status!=='Closed')$('#ticketActions').append(button('Close ticket',async()=>{await api('tickets/close','POST',{Id:t.Id,GoRevision:t.GoRevision});$('#modal').close();toast('Ticket closed');await route();},'quiet'));
}
async function accounts(ver){
 title('User accounts','ADMINISTRATION');const result=await api('accounts?offset='+state.offset);if(ver!==renderVersion)return;
 tool('+ New account',async()=>{
  const [customers,insurers]=await Promise.all([options('Customer','Name'),options('Insurers','Name')]);
  form('Create account',[field('Username',{required:true}),field('Password',{type:'password',required:true,minLength:12,maxLength:64}),field('Role',{options:['Staff','Admin','Customer','Insurer'].map(v=>({value:v,label:v}))}),field('CustomerId',{numeric:true,options:[{value:'',label:'Not a customer account'},...customers]}),field('InsurerId',{numeric:true,options:[{value:'',label:'Not an insurer account'},...insurers]})],{},v=>api('accounts','POST',v),'Create account');
 });
 $('#content').innerHTML=table([['Id','ID'],['Username','Username'],['Role','Role'],['CustomerId','Customer ID'],['InsurerId','Insurer ID'],['Active','Active']],result.items);pager(result);
}
async function audit(ver){title('Audit trail','ADMINISTRATION');const result=await api('audit?offset='+state.offset);if(ver!==renderVersion)return;$('#content').innerHTML=table([['At','Time (UTC)'],['AccountId','Account'],['Action','Action'],['TableName','Record type'],['RecordId','Record ID']],result.items);pager(result);}

async function surveys(ver){
 title('Survey uploads','MOBILE APP');
 const result=await api('integration/submissions?offset='+state.offset);if(ver!==renderVersion)return;
 $('#content').innerHTML=table([['Id','ID'],['ContractId','Contract'],['JobId','Job'],['JobType','Type'],['ReceivedAt','Received'],['SubmissionKey','Submission key']],result.items,(item,i)=>'<button class="small quiet inspectSurvey" data-index="'+i+'">View payload</button>');
 bind('#content','.inspectSurvey',result.items,item=>{
  let payload;try{payload=JSON.stringify(JSON.parse(item.Payload),null,2);}catch{payload=item.Payload||'No payload saved';}
  showModal('Survey submission #'+item.Id,'<pre class="surveyPayload">'+esc(payload)+'</pre>');
 });
 pager(result);
}
async function media(ver){
 title('Photos & videos','LOCAL MEDIA STORAGE');
 const result=await api('integration/media?offset='+state.offset);if(ver!==renderVersion)return;
 $('#content').innerHTML='<p class="muted">Completed uploads are saved to SmartOpsMedia/&lt;tenant ID&gt;/ beside the 4D project. Select Download to inspect an original file.</p>'+table([['Id','ID'],['Filename','File'],['ReceivedAt','Received'],['TotalChunks','Chunks'],['Digest','SHA-256']],result.items,(item,i)=>'<button class="small quiet downloadMedia" data-index="'+i+'">Download</button>');
 bind('#content','.downloadMedia',result.items,async item=>{
  const response=await fetch('/api/integration/media/'+item.Id,{headers:{Authorization:'Bearer '+state.token}});
  if(!response.ok)throw Error('Unable to download the media file');
  const blob=await response.blob();const url=URL.createObjectURL(blob);
  const a=document.createElement('a');a.href=url;a.download=item.Filename;a.click();
  setTimeout(()=>URL.revokeObjectURL(url),30000);
 });
 pager(result);
}
