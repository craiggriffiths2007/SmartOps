property schema : Object
property debugStage : Text
property logTenantId : Integer

Class constructor()
	This.debugStage:="class constructor"
	This.logTenantId:=0
	This.schema:=JSON Parse(File("/RESOURCES/schema.json").getText())
	
Function reply($status : Integer; $body : Object) : 4D.OutgoingMessage
	var $r:=4D.OutgoingMessage.new()
	$r.setBody($body)
	$r.setStatus($status)
	$r.setHeader("Content-Type"; "application/json; charset=utf-8")
	$r.setHeader("Cache-Control"; "no-store")
	$r.setHeader("X-Content-Type-Options"; "nosniff")
	return $r
	
Function failure($errors : Collection; $path : Text; $verb : Text) : 4D.OutgoingMessage
	var $status:=500
	var $message:="Request failed. Please try again or check the 4D Error Log."
	If ($errors.length>0)
		If (($errors[0].errCode>=400) & ($errors[0].errCode<=499))
			$status:=$errors[0].errCode
			$message:=$errors[0].message
		End if
	End if
	var $id:=Generate UUID
	// Deliberately omit request bodies, query strings, cookies and credentials.
	var $entry:=New object("id";$id;"timestamp";Timestamp;"tenantId";This.logTenantId;"status";$status;"method";$verb;"path";$path;"stage";This.debugStage;"errors";$errors)
	var $log:=cs.SmartOpsErrorLog.new()
	var $saved:=$log.write(This.logTenantId;$entry)
	return This.reply($status;New object("error";$message;"reasonPhrase";$message;"errorId";$id;"errorLogged";$saved))

Function now() : Real
	var $s:=Timestamp
	return (Date(Substring($s; 1; 10)+"T00:00:00")-!2020-01-01!)*86400+Num(Substring($s; 12; 2))*3600+Num(Substring($s; 15; 2))*60+Num(Substring($s; 18; 2))
	
Function save($entity : 4D.Entity)
	$entity.GoRevision:=Num($entity.GoRevision)+1
	var $s:=$entity.save()
	If (Not($s.success))
		throw(409; "Record changed or could not be saved; reload and try again")
	End if 
	
Function normalizeJob($job : 4D.Entity)
	// The Razor workspace serializes DateTime as a full SQLite timestamp.
	// Mobile bookings use a date-only diary key and HH:mm start time.
	var $date:=Substring(String($job.DiaryDate); 1; 10)
	var $time:=String($job.GoTime)
	If (Length($time)>5)
		$time:=Substring($time; 12; 5)
	End if
	If (Not(Match regex("^[0-9]{4}-[0-9]{2}-[0-9]{2}$"; $date)) | Not(Match regex("^([01][0-9]|2[0-3]):[0-5][0-9]$"; $time)))
		throw(400; "Booking has an invalid diary date or start time")
	End if
	var $duration:=String($job.JobTime)
	var $start:=Num(Substring($time; 1; 2))*60+Num(Substring($time; 4; 2))
	var $minutes:=Num(Substring($duration; 1; 2))*60+Num(Substring($duration; 4; 2))
	var $end:=$start+$minutes
	If (($job.DiaryDate#$date) | ($job.GoTime#$time) | ($job.GoStartMinute#$start) | ($job.GoEndMinute#$end))
		$job.DiaryDate:=$date
		$job.GoTime:=$time
		$job.GoStartMinute:=$start
		$job.GoEndMinute:=$end
		This.save($job)
	End if

Function newRow($table : Text; $tenant : Integer) : 4D.Entity
	var $e:=ds[This.schema[$table].nativeTable].new()
	$e.GoTenantId:=$tenant
	If (This.schema[$table].publicFields.includes("TenantId"))
		$e.TenantId:=$tenant
	End if 
	return $e
	
Function row($table : Text; $id : Variant; $tenant : Integer) : 4D.Entity
	If (Value type($id)=Is text)
		This.requireText($id; "Record ID"; 450)
	Else
		If (Num($id)<=0)
			throw(400; "A valid record ID is required")
		End if
	End if
	var $e:=ds[This.schema[$table].nativeTable].query(This.schema[$table].key+" = :1 AND GoTenantId = :2"; $id; $tenant).first()
	If ($e=Null)
		throw(404; "Record not found")
	End if 
	return $e
	
Function publicRow($table : Text; $e : 4D.Entity) : Object
	This.debugStage:="publicRow "+$table+" #"+String($e.Id)
	var $o:=New object
	var $f : Text
	For each ($f; This.schema[$table].publicFields)
		This.debugStage:="publicRow "+$table+" field "+$f
		$o[$f]:=$e[This.schema[$table].fieldMap[$f]]
	End for each 
	If ($table="Contract")
		$o.ContractCode:=String($e.ContractNumber; "00000000")
	End if 
	return $o
	
	
Function rows($table : Text; $selection : 4D.EntitySelection; $offset : Integer) : Object
	This.debugStage:="rows "+$table+" started"
	// Iterate ORDA entities directly, rather than indexing a selection.
	// This also handles empty selections and offsets beyond the final row.
	var $items:=New collection
	var $total : Integer
	var $start : Integer
	var $position : Integer
	var $entity : 4D.Entity
	$total:=$selection.length
	This.debugStage:="rows "+$table+" count "+String($total)
	$start:=$offset
	If ($start<0)
		$start:=0
	End if 
	$position:=0
	For each ($entity; $selection)
		If ($position>=$start)
			If ($items.length>=100)
				break
			End if 
			This.debugStage:="rows "+$table+" serializing index "+String($position)
			$items.push(This.publicRow($table; $entity))
		End if 
		$position:=$position+1
	End for each 
	return New object("items"; $items; "total"; $total; "offset"; $start; "limit"; 100)
	
Function requireRole($a : 4D.Entity; $roles : Text)
	If (Not(Split string($roles; ",").includes($a.Role)))
		throw(403; "This action is not available for your role")
	End if 
	
Function requireText($v : Variant; $label : Text; $max : Integer) : Text
	If (Value type($v)#Is text)
		throw(400; $label+" must be text")
	End if 
	var $s:=Trim($v)
	If ((Length($s)=0) | (Length($s)>$max))
		throw(400; $label+" is required and must be at most "+String($max)+" characters")
	End if 
	return $s
	
Function requirePassword($v : Variant) : Text
	If (Value type($v)#Is text)
		throw(400; "Password must be text")
	End if 
	If ((Length($v)=0) | (Length($v)>64))
		throw(400; "Password must contain 1 to 64 characters")
	End if 
	return $v
	
Function audit($a : 4D.Entity; $action : Text; $table : Text; $id : Integer)
	var $e:=This.newRow("GoAudit"; $a.GoTenantId)
	$e.AccountId:=$a.Id
	$e.At:=Timestamp
	$e.Action:=$action
	$e.TableName:=$table
	$e.RecordId:=$id
	This.save($e)
	
Function authenticate($request : 4D.IncomingMessage) : 4D.Entity
	var $header:=$request.getHeader("Authorization")
	
	If (Position("Bearer "; $header)#1)
		This.debugStage:="AUTH: Missing bearer token"
		throw(401; "Please sign in")
	End if 
	
	var $hash:=Generate digest(Substring($header; 8); SHA256 digest)
	
	// Find the session without checking expiry
	var $s:=ds.GoSession.query("TokenHash = :1"; $hash).first()
	
	If ($s=Null)
		This.debugStage:="AUTH: Token not found"
		throw(401; "Authentication failed: token not found")
	End if 
	
	// Check the session expiry separately
	var $currentTime:=This.now()
	var $expiry:=$s.ExpiresAt
	
	If ($expiry<=$currentTime)
		This.debugStage:="AUTH: Session expired"
		throw(401; "Authentication failed: session expired")
	End if 
	
	This.debugStage:="AUTH: Session valid"
	
	var $a:=This.row("GoAccount"; $s.AccountId; $s.GoTenantId)
	This.logTenantId:=$a.GoTenantId
	var $tenant:=ds[This.schema.Tenant.nativeTable].get($a.GoTenantId)
	
	If (($tenant=Null) | (Not($a.Active)))
		throw(401; "Account is inactive")
	End if 
	
	If (Not($tenant.IsActive))
		throw(401; "Company is inactive")
	End if 

    // Sliding expiration: renew only on an authenticated request, and
    // only when fewer than 30 minutes remain on the 60-minute session.
    If (($expiry-$currentTime)<1800)
        $s.ExpiresAt:=$currentTime+3600
        This.save($s)
    End if

	return $a
	
Function login($b : Object) : 4D.OutgoingMessage
	var $code:=This.requireText($b.TenantCode; "Company code"; 32)
	var $user:=Lowercase(This.requireText($b.Username; "Username"; 200))
	var $password:=This.requirePassword($b.Password)
	var $t:=ds[This.schema.Tenant.nativeTable].query("Code = :1 AND IsActive = true"; $code).first()
	If ($t=Null)
		return This.reply(401; New object("error"; "Invalid login"))
	End if 
	var $a:=ds.GoAccount.query("GoTenantId = :1 AND Username = :2 AND Active = true"; $t.Id; $user).first()
	If ($a=Null)
		return This.reply(401; New object("error"; "Invalid login"))
	End if 
	If ($a.LockedUntil>This.now())
		return This.reply(429; New object("error"; "Account temporarily locked; try again in five minutes"))
	End if 
	If (Not(Verify password hash(Generate digest($password; SHA256 digest); $a.PasswordHash)))
		$a.Failures:=$a.Failures+1
		If ($a.Failures>=5)
			$a.LockedUntil:=This.now()+300
			$a.Failures:=0
		End if 
		This.save($a)
		return This.reply(401; New object("error"; "Invalid login"))
	End if 
	$a.Failures:=0
	$a.LockedUntil:=0
	This.save($a)
	var $token:=Generate UUID+Generate UUID
	var $s:=This.newRow("GoSession"; $t.Id)
	$s.AccountId:=$a.Id
	$s.TokenHash:=Generate digest($token; SHA256 digest)
	$s.ExpiresAt:=This.now()+3600
	This.save($s)
	return This.reply(200; New object("token"; $token; "expiresIn"; 3600; "role"; $a.Role; "username"; $a.Username; "company"; $t.Name))
	
Function changeAccountPassword($a : 4D.Entity; $b : Object) : 4D.OutgoingMessage
	var $old:=This.requirePassword($b.OldPassword)
	var $password:=This.requirePassword($b.NewPassword)
	If (Not(Verify password hash(Generate digest($old; SHA256 digest); $a.PasswordHash)))
		throw(400; "Current password is incorrect")
	End if
    return This.resetAccountPassword($a; $password)

// The caller must already be authenticated and authorized for this account.
Function resetAccountPassword($a : 4D.Entity; $newPassword : Variant) : 4D.OutgoingMessage
    var $password:=This.requirePassword($newPassword)
	If (Length($password)<12)
		throw(400; "Use at least 12 characters")
	End if
	$a.PasswordHash:=Generate password hash(Generate digest($password; SHA256 digest); New object("cost"; 12))
	This.save($a)
	If ($a.Role="Agent")
		var $agent:=This.row("Agent"; $a.AgentId; $a.GoTenantId)
		$agent.AuthenticationString:=""
		This.save($agent)
	End if
	var $s : 4D.Entity
	For each ($s; ds.GoSession.query("AccountId = :1"; $a.Id))
		var $d:=$s.drop()
		If (Not($d.success))
			throw(409; "Could not revoke existing sessions")
		End if
	End for each
	This.audit($a; "password"; "GoAccount"; $a.Id)
	return This.reply(200; New object("ok"; True))

Function ownedContract($a : 4D.Entity; $id : Integer) : 4D.Entity
	var $c:=This.row("Contract"; $id; $a.GoTenantId)
	Case of 
		: ($a.Role="Customer")
			If ($c.CustomerId#$a.CustomerId)
				throw(404; "Contract not found")
			End if 
		: ($a.Role="Insurer")
			If ($c.InsurerId#$a.InsurerId)
				throw(404; "Contract not found")
			End if 
		Else 
			This.requireRole($a; "Admin,Staff")
	End case 
	return $c
	
Function checkRevision($e : 4D.Entity; $b : Object)
	If ($b.GoRevision#$e.GoRevision)
		throw(409; "This record has changed. Reload before saving")
	End if 
	
Function applyFields($table : Text; $e : 4D.Entity; $b : Object)
	var $f : Object
	var $name : Text
	For each ($name; $b)
		If (Not(This.schema[$table].editableFields.includes($name)))
			If ($name#"GoRevision")
				throw(400; "Field cannot be edited: "+$name)
			End if 
		End if 
	End for each 
	For each ($f; This.schema[$table].fields)
		$name:=$f.name
		If (OB Is defined($b; $name) & This.schema[$table].editableFields.includes($name))
			If ($b[$name]=Null)
				If (Not($f.nullable))
					throw(400; $name+" cannot be empty")
				End if 
			Else 
				Case of 
					: ($f.kind="text")
						If (Value type($b[$name])#Is text)
							throw(400; $name+" must be text")
						End if 
						If (Length($b[$name])>32000)
							throw(400; $name+" is too long")
						End if 
						If ($f.maxLength#Null)
							If (Length($b[$name])>$f.maxLength)
								throw(400; $name+" exceeds its maximum length")
							End if 
						End if 
					: ($f.kind="boolean")
						If (Value type($b[$name])#Is boolean)
							throw(400; $name+" must be true or false")
						End if 
					Else 
						If (Value type($b[$name])#Is real)
							throw(400; $name+" must be numeric")
						End if 
						If (($f.kind="integer") & ($b[$name]#Int($b[$name])))
							throw(400; $name+" must be a whole number")
						End if 
				End case 
			End if 
			$e[This.schema[$table].fieldMap[$name]]:=$b[$name]
		End if 
	End for each 
	var $r : Object
	For each ($r; This.schema[$table].relations)
		If ($e[This.schema[$table].fieldMap[$r.field]]#Null)
			If ($e[This.schema[$table].fieldMap[$r.field]]#0)
				var $parent:=ds[This.schema[$r.target].nativeTable].get($e[This.schema[$table].fieldMap[$r.field]])
				If ($parent=Null)
					throw(400; "Related record not found: "+$r.field)
				End if 
				If ($parent.GoTenantId#$e.GoTenantId)
					throw(400; "Related record is outside this company")
				End if 
			End if 
		End if 
	End for each 
	
Function contractDetail($a : 4D.Entity; $id : Integer) : Object
	var $c:=This.ownedContract($a; $id)
	var $t:=$a.GoTenantId
	var $updates:=ds.ContractUpdates.query("GoTenantId = :1 AND ContractId = :2"; $t; $id)
	If ($a.Role="Customer")
		$updates:=$updates.query("CustomerVisible = true")
	End if 
	var $est:=ds.Estimates.query("GoTenantId = :1 AND ContractId = :2"; $t; $id)
	If ($a.Role="Customer")
		$est:=$est.query("AuthorizedUtc != :1 AND AuthorizedUtc != null"; "")
	End if 
	var $jobs:=This.rows("Job"; ds.Job.query("GoTenantId = :1 AND ContractId = :2 AND GoCancelled = false"; $t; $id).orderBy("DiaryDate, GoTime"); 0).items
	If (($a.Role="Customer") | ($a.Role="Insurer"))
		var $job : Object
		var $safeJobs:=New collection
		For each ($job; $jobs)
			$safeJobs.push(New object("Id"; $job.Id; "ContractId"; $job.ContractId; "JobType"; $job.JobType; "DiaryDate"; $job.DiaryDate; "Time"; $job.Time; "JobTime"; $job.JobTime))
		End for each 
		$jobs:=$safeJobs
	End if 
	var $o:=New object("contract"; This.publicRow("Contract"; $c); "customer"; This.publicRow("Customer"; This.row("Customer"; $c.CustomerId; $t)); "updates"; This.rows("ContractUpdates"; $updates.orderBy("Id desc"); 0).items; "jobs"; $jobs; "estimates"; This.rows("Estimates"; $est; 0).items; "invoices"; This.rows("Invoices"; ds.Invoices.query("GoTenantId = :1 AND ContractId = :2"; $t; $id); 0).items)
	return $o
	
Function timeline($a : 4D.Entity; $c : 4D.Entity; $title : Text; $description : Text; $visible : Boolean)
	var $u:=This.newRow("ContractUpdates"; $a.GoTenantId)
	$u.ContractId:=$c.Id
	$u.DateTime:=Timestamp
	$u.Status:=$c.Status
	$u.Title:=$title
	$u.Description:=$description
	$u.CustomerVisible:=$visible
	This.save($u)
	
Function nextContractNumber() : Integer
	var $s:=ds.GoSequence.query("Name = :1"; "ContractNumber").first()
	If ($s=Null)
		$s:=This.newRow("GoSequence"; 0)
		$s.Name:="ContractNumber"
		$s.Value:=0
	End if 
	$s.Value:=$s.Value+1
	This.save($s)
	return $s.Value
	
Function createContract($a : 4D.Entity; $b : Object) : Object
	This.requireRole($a; "Admin,Staff")
	var $c:=This.newRow("Contract"; $a.GoTenantId)
	This.applyFields("Contract"; $c; $b)
	This.row("Customer"; Num($c.CustomerId); $a.GoTenantId)
	$c.DamageDescription:=This.requireText($c.DamageDescription; "Damage description"; 4000)
	$c.CauseOfDamage:=This.requireText($c.CauseOfDamage; "Cause of damage"; 1000)
	$c.ContractNumber:=This.nextContractNumber()
	$c.Status:=0
	$c.IsActive:=True
	$c.IsCancelled:=False
	This.save($c)
	This.timeline($a; $c; "Repair reported"; $c.DamageDescription; True)
	This.audit($a; "create"; "Contract"; $c.Id)
	return This.publicRow("Contract"; $c)
	
Function book($a : 4D.Entity; $b : Object) : Object
	This.debugStage:="book: role check"
	This.requireRole($a; "Admin,Staff")
	This.debugStage:="book: lookup contract"
	var $c:=This.row("Contract"; Num($b.ContractId); $a.GoTenantId)
	This.debugStage:="book: lookup agent"
	var $agent:=This.row("Agent"; Num($b.AgentId); $a.GoTenantId)
	If ($c.IsCancelled)
		throw(409; "This contract has been cancelled")
	End if 
	This.debugStage:="book: date validation"
	var $date:=This.requireText($b.DiaryDate; "Diary date"; 10)
	If ((Not(Match regex("^[0-9]{4}-[0-9]{2}-[0-9]{2}$"; $date))) | (Date($date+"T00:00:00")=!00-00-00!))
		throw(400; "Use an ISO diary date, YYYY-MM-DD")
	End if 
	This.debugStage:="book: time validation"
	var $time:=This.requireText($b.Time; "Start time"; 5)
	// Validate the wire value before parsing. In 4D, mixing a comparison
	// with concatenation in the same expression can evaluate boolean + text.
	If (Not(Match regex("^([01][0-9]|2[0-3]):[0-5][0-9]$"; $time)))
		throw(400; "Use a valid time HH:mm")
	End if 
	var $h:=Num(Substring($time; 1; 2))
	var $m:=Num(Substring($time; 4; 2))
	This.debugStage:="book: duration validation"
	var $minutes:=Num($b.DurationMinutes)
	If (($minutes<15) | ($minutes>480) | ($minutes#Int($minutes)))
		throw(400; "Duration must be 15 to 480 whole minutes")
	End if 
	var $start:=$h*60+$m
	var $end:=$start+$minutes
	If ($end>1440)
		throw(400; "Booking must finish on the same day")
	End if 
	var $id:=Num($b.Id)
	This.debugStage:="book: clash query"
	var $clash:=ds.Job.query("GoTenantId = :1 AND AgentId = :2 AND DiaryDate = :3 AND GoCancelled = false AND GoStartMinute < :4 AND GoEndMinute > :5 AND Id != :6"; $a.GoTenantId; $agent.Id; $date; $end; $start; $id)
	If ($clash.length>0)
		throw(409; "This agent already has a booking in that time slot")
	End if 
	This.debugStage:="book: creating or retrieving job"
	var $j : 4D.Entity
	If ($id>0)
		$j:=This.row("Job"; $id; $a.GoTenantId)
		This.checkRevision($j; $b)
		If ($j.ContractId#$c.Id)
			throw(400; "A booking cannot be moved to another contract")
		End if 
	Else 
		$j:=This.newRow("Job"; $a.GoTenantId)
	End if 
	This.debugStage:="book: job type validation"
	var $type:=Num($b.JobType)
	If (($type<0) | ($type>4) | ($type#Int($type)))
		throw(400; "Unknown job type")
	End if 
	If (($type=1) & ($c.Status<60))
		throw(409; "Authorise an estimate before booking fitting")
	End if 
	This.debugStage:="book: assigning job fields"
	$j.ContractId:=$c.Id
	$j.CustomerId:=$c.CustomerId
	$j.AgentId:=$agent.Id
	$j.ContractCode:=String($c.ContractNumber; "00000000")
	$j.JobType:=$type
	$j.DiaryDate:=$date
	$j.GoTime:=$time
	$j.JobTime:=String(Int($minutes/60); "00")+":"+String($minutes%60; "00")+":00"
	$j.GoStartMinute:=$start
	$j.GoEndMinute:=$end
	$j.GoCancelled:=False
	$j.Instructions:=String($b.Instructions)
	This.debugStage:="book: saving job"
	This.save($j)
	Case of 
		: ($type=0) & ($c.Status<20)
			$c.Status:=10
		: ($type=1) & ($c.Status<80)
			$c.Status:=70
	End case 
	This.debugStage:="book: saving contract"
	This.save($c)
	This.debugStage:="book: adding timeline"
	This.timeline($a; $c; "Appointment booked"; $date+" at "+$time; True)
	This.debugStage:="book: audit"
	This.audit($a; "book"; "Job"; $j.Id)
	This.debugStage:="book: response"
	return This.publicRow("Job"; $j)
	
Function estimate($a : 4D.Entity; $b : Object) : Object
	This.requireRole($a; "Admin,Staff")
	var $c:=This.row("Contract"; Num($b.ContractId); $a.GoTenantId)
	If ($c.IsCancelled | ($c.Status>=60))
		throw(409; "Cannot replace the estimate after authorisation or cancellation")
	End if 
	If (Value type($b.Lines)#Is collection)
		throw(400; "Estimate lines are required")
	End if 
	If (($b.Lines.length=0) | ($b.Lines.length>100))
		throw(400; "Use 1 to 100 estimate lines")
	End if 
	var $rate:=Num($b.VatRate)
	If (($rate<0) | ($rate>100))
		throw(400; "VAT rate must be between 0 and 100")
	End if 
	var $e:=This.newRow("Estimates"; $a.GoTenantId)
	$e.ContractId:=$c.Id
	$e.ServiceLevel:=This.requireText($b.ServiceLevel; "Service level"; 200)
	$e.CreatedUtc:=Timestamp
	$e.CreatedBy:=$a.Username
	$e.VatRate:=$rate
	This.save($e)
	var $line : Object
	var $cents:=0
	var $order:=0
	For each ($line; $b.Lines)
		If (Value type($line.Cost)#Is real)
			throw(400; "Line costs must be numeric")
		End if 
		If (($line.Cost<0) | ($line.Cost>1000000))
			throw(400; "Line cost must be between zero and one million pounds")
		End if 
		var $l:=This.newRow("EstimateLines"; $a.GoTenantId)
		$l.EstimateId:=$e.Id
		$l.Details:=This.requireText($line.Details; "Line details"; 2000)
		$l.Cost:=Round($line.Cost*100; 0)/100
		$l.SortOrder:=$order
		$order:=$order+1
		$cents:=$cents+Round($line.Cost*100; 0)
		This.save($l)
	End for each 
	$e.Subtotal:=$cents/100
	$e.VatAmount:=Round($cents*$rate/100; 0)/100
	$e.Total:=$e.Subtotal+$e.VatAmount
	This.save($e)
	$c.Status:=40
	$c.SelectedEstimateId:=$e.Id
	This.save($c)
	This.timeline($a; $c; "Estimate prepared"; "Awaiting authorisation"; True)
	This.audit($a; "estimate"; "Estimates"; $e.Id)
	return This.publicRow("Estimates"; $e)
	
Function authorise($a : 4D.Entity; $b : Object) : Object
	This.requireRole($a; "Admin,Staff,Insurer")
	var $e:=This.row("Estimates"; Num($b.EstimateId); $a.GoTenantId)
	var $c:=This.ownedContract($a; $e.ContractId)
	This.checkRevision($e; $b)
	If ($c.IsCancelled | ($c.SelectedEstimateId#$e.Id) | ($c.Status#40))
		throw(409; "This estimate cannot be authorised")
	End if 
	$e.AuthorizedUtc:=Timestamp
	$e.AuthorizedBy:=$a.Username
	This.save($e)
	$c.Status:=60
	This.save($c)
	This.timeline($a; $c; "Repair authorised"; "Your repair has been approved"; True)
	This.audit($a; "authorise"; "Estimates"; $e.Id)
	return This.publicRow("Estimates"; $e)
	
Function invoice($a : 4D.Entity; $b : Object) : Object
	This.requireRole($a; "Admin,Staff")
	var $c:=This.row("Contract"; Num($b.ContractId); $a.GoTenantId)
	If ($c.IsCancelled | ($c.Status#80))
		throw(409; "Complete fitting before issuing an invoice")
	End if 
	If (ds.Invoices.query("GoTenantId = :1 AND ContractId = :2"; $a.GoTenantId; $c.Id).length>0)
		throw(409; "This contract already has an invoice")
	End if 
	var $e:=This.row("Estimates"; Num($c.SelectedEstimateId); $a.GoTenantId)
	var $customer:=This.row("Customer"; $c.CustomerId; $a.GoTenantId)
	var $tenant:=ds[This.schema.Tenant.nativeTable].get($a.GoTenantId)
	var $i:=This.newRow("Invoices"; $a.GoTenantId)
	$i.ContractId:=$c.Id
	$i.EstimateId:=$e.Id
	$i.ContractCode:=String($c.ContractNumber; "00000000")
	$i.PolicyNumber:=$c.PolicyNumber
	$i.CustomerName:=$customer.Name
	$i.ServiceAddress:=$customer.Add1+", "+$customer.Postcode
	$i.BillToName:=$customer.Name
	$i.BillToAddress:=$i.ServiceAddress
	$i.BillingParty:=0
	If ($b.BillingParty=1)
		var $insurer:=This.row("Insurers"; Num($c.InsurerId); $a.GoTenantId)
		$i.BillingParty:=1
		$i.BillToName:=$insurer.Name
		$i.BillToAddress:=$insurer.Address1+", "+$insurer.Postcode
	End if 
	$i.SellerName:=$tenant.Name
	$i.SellerAddress:=$tenant.Address1+", "+$tenant.Postcode
	$i.SellerEmail:=$tenant.Email
	$i.SellerTelephone:=$tenant.Telephone
	$i.Description:=This.requireText($b.Description; "Invoice description"; 200)
	$i.InvoiceDate:=Substring(Timestamp; 1; 10)
	$i.IssuedUtc:=Timestamp
	$i.IssuedBy:=$a.Username
	$i.Subtotal:=$e.Subtotal
	$i.VatAmount:=$e.VatAmount
	$i.VatRate:=$e.VatRate
	$i.Total:=$e.Total
	This.save($i)
	var $line : 4D.Entity
	For each ($line; ds.EstimateLines.query("GoTenantId = :1 AND EstimateId = :2"; $a.GoTenantId; $e.Id))
		var $il:=This.newRow("InvoiceLines"; $a.GoTenantId)
		$il.InvoiceId:=$i.Id
		$il.Details:=$line.Details
		$il.SortOrder:=$line.SortOrder
		$il.Cost:=$line.Cost
		This.save($il)
	End for each 
	$c.Status:=90
	This.save($c)
	This.timeline($a; $c; "Invoice issued"; "Your repair is complete"; True)
	This.audit($a; "invoice"; "Invoices"; $i.Id)
	return This.publicRow("Invoices"; $i)
	
Function dispatch($r : 4D.IncomingMessage; $b : Object) : 4D.OutgoingMessage
	This.debugStage:="dispatch: begin"
	var $path:=$r.urlPath.join("/")
	var $verb:=$r.verb
	var $resource:=""
	This.debugStage:="dispatch path "+$path
	If ($r.urlPath.length>=2)
		$resource:=$r.urlPath[1]
	End if 
	If (($path="api/health") & ($verb="GET"))
		return This.reply(200; New object("service"; "SmartOps4D"; "version"; "0.3.0-integration"; "surveyApi"; 1; "at"; Timestamp))
	End if 
	If (($path="api/auth/login") & ($verb="POST"))
		return This.login($b)
	End if 
	This.debugStage:="dispatch authenticating"
	var $a:=This.authenticate($r)
	This.debugStage:="dispatch authenticated"
	var $t:=$a.GoTenantId
	If (($path="api/error-log") & ($verb="GET"))
		This.requireRole($a; "Admin")
		var $log:=cs.SmartOpsErrorLog.new()
		return This.reply(200; $log.read($t))
	End if
	This.debugStage:="dispatch tenant read"
	var $staff:=($a.Role="Admin") | ($a.Role="Staff")
	This.debugStage:="dispatch role read"
	// Temporary diagnostic: bypass query-string offset until the failing
	// operation is isolated. First-page requests remain fully supported.
	var $offset : Integer
	$offset:=0
	This.debugStage:="dispatch offset initialised"
	If (($path="api/auth/logout") & ($verb="POST"))
		var $hash:=Generate digest(Substring($r.getHeader("Authorization"); 8); SHA256 digest)
		var $session:=ds.GoSession.query("TokenHash = :1"; $hash).first()
		If ($session#Null)
			var $drop:=$session.drop()
			If (Not($drop.success))
				throw(409; "Could not revoke session")
			End if 
		End if 
		return This.reply(200; New object("ok"; True))
	End if 
    If (($path="api/auth/profile") & ($verb="GET"))
        var $profileBridge:=cs.SmartOpsWebBridge.new(This)
        return This.reply(200; $profileBridge.profile($a))
    End if
    If (($path="api/auth/preferences") & ($verb="GET"))
        var $preferencesBridge:=cs.SmartOpsWebBridge.new(This)
        return This.reply(200; $preferencesBridge.preferences($a))
    End if
    If (($path="api/auth/preferences") & ($verb="POST"))
        var $savePreferencesBridge:=cs.SmartOpsWebBridge.new(This)
        return This.reply(200; $savePreferencesBridge.savePreferences($a; $b))
    End if
    If (($path="api/auth/profile") & ($verb="POST"))
        var $saveProfileBridge:=cs.SmartOpsWebBridge.new(This)
        return This.reply(200; $saveProfileBridge.saveProfile($a; $b))
    End if
    If (($path="api/web/password") & ($verb="POST"))
        This.requireRole($a; "Admin,Staff")
        return This.resetAccountPassword($a; $b.NewPassword)
    End if
    If (($path="api/web/users/save") & ($verb="POST"))
        var $userBridge:=cs.SmartOpsWebBridge.new(This)
        return This.reply(200; $userBridge.saveUser($a; $b))
    End if
    If (($path="api/web/users/delete") & ($verb="POST"))
        var $deleteBridge:=cs.SmartOpsWebBridge.new(This)
        return This.reply(200; $deleteBridge.deleteUser($a; $b))
    End if
	If (($path="api/auth/password") & ($verb="POST"))
		return This.changeAccountPassword($a; $b)
	End if
	If (($path="api/integration/media") & ($verb="GET"))
		This.requireRole($a; "Admin,Staff")
		var $media:=ds.GoMediaFile.query("GoTenantId = :1 AND Completed = true"; $t).orderBy("Id desc")
		return This.reply(200; This.rows("GoMediaFile"; $media; $offset))
	End if 
	// 4D evaluates boolean operands even when the length check is false.
	// Never access urlPath[2] for /api/portal (which has only two segments).
	var $segment2:=""
	If ($r.urlPath.length>=3)
		$segment2:=String($r.urlPath[2])
	End if 
	This.debugStage:="route segments validated"
	If (($r.urlPath.length=4) & ($resource="integration") & ($segment2="media") & ($verb="GET"))
		This.requireRole($a; "Admin,Staff")
		var $mediaRow:=This.row("GoMediaFile"; Num($r.urlPath[3]); $t)
		If (Not($mediaRow.Completed))
			throw(404; "Media upload is incomplete")
		End if 
		var $mediaName:=$mediaRow.Filename
		If ((Not(Match regex("^[A-Za-z0-9_-][A-Za-z0-9_.-]{0,199}$"; $mediaName))) | (Position(".."; $mediaName)>0))
			throw(400; "Invalid stored media filename")
		End if 
		var $file:=Folder(fk database folder).folder("SmartOpsMedia").folder(String($t)).file($mediaName)
		If (Not($file.exists))
			throw(404; "File not found in local media folder")
		End if 
		var $response:=4D.OutgoingMessage.new()
		$response.setBody($file.getContent())
		$response.setStatus(200)
		$response.setHeader("Cache-Control"; "no-store")
		$response.setHeader("X-Content-Type-Options"; "nosniff")
		$response.setHeader("Content-Disposition"; "attachment; filename="+$mediaName)
		$response.setHeader("Content-Type"; "application/octet-stream")
		return $response
	End if 
	If (($path="api/integration/submissions") & ($verb="GET"))
		This.requireRole($a; "Admin,Staff")
		return This.reply(200; This.rows("GoSurveySubmission"; ds.GoSurveySubmission.query("GoTenantId = :1"; $t).orderBy("Id desc"); $offset))
	End if 
	If (($path="api/integration/outbox") & ($verb="GET"))
		This.requireRole($a; "Admin,Staff")
		return This.reply(200; This.rows("GoIntegrationOutbox"; ds.GoIntegrationOutbox.query("GoTenantId = :1"; $t).orderBy("Id desc"); $offset))
	End if 
	If (($path="api/schema") & ($verb="GET"))
		This.requireRole($a; "Admin,Staff")
		var $schema:=New collection
		var $name : Text
		For each ($name; This.schema)
			If (This.schema[$name].generic)
				var $entry:=This.schema[$name]
				var $fields:=New collection
				var $f : Object
				For each ($f; $entry.fields)
					If ($entry.publicFields.includes($f.name))
						$fields.push($f)
					End if 
				End for each 
				$schema.push(New object("name"; $name; "fields"; $fields; "editableFields"; $entry.editableFields; "publicFields"; $entry.publicFields))
			End if 
		End for each 
		return This.reply(200; New object("items"; $schema))
	End if 
	If (($path="api/portal") & ($verb="GET"))
		This.debugStage:="portal: query"
		var $contracts:=ds.Contract.query("GoTenantId = :1"; $t)
		This.debugStage:="portal: role "+$a.Role
		Case of 
			: ($a.Role="Customer")
				$contracts:=$contracts.query("CustomerId = :1"; $a.CustomerId)
			: ($a.Role="Insurer")
				$contracts:=$contracts.query("InsurerId = :1"; $a.InsurerId)
			Else 
				This.requireRole($a; "Admin,Staff")
		End case 
		This.debugStage:="portal: ordering"
		var $ordered:=$contracts.orderBy("Id desc")
		This.debugStage:="portal: collecting"
		var $page:=This.rows("Contract"; $ordered; $offset)
		This.debugStage:="portal: reply"
		return This.reply(200; $page)
	End if 
	If (($path="api/contracts") & ($verb="POST"))
		return This.reply(201; This.createContract($a; $b))
	End if 
	If (($r.urlPath.length=3) & ($resource="contracts") & ($verb="GET"))
		return This.reply(200; This.contractDetail($a; Num($r.urlPath[2])))
	End if 
	If (($path="api/timeline") & ($verb="POST"))
		This.requireRole($a; "Admin,Staff")
		var $c:=This.ownedContract($a; Num($b.ContractId))
		This.timeline($a; $c; This.requireText($b.Title; "Title"; 200); This.requireText($b.Description; "Description"; 4000); $b.CustomerVisible=True)
		This.audit($a; "timeline"; "Contract"; $c.Id)
		return This.reply(201; New object("ok"; True))
	End if 
	If (($path="api/contracts/cancel") & ($verb="POST"))
		This.requireRole($a; "Admin,Staff")
		$c:=This.ownedContract($a; Num($b.ContractId))
		This.checkRevision($c; $b)
		If ($c.Status=90)
			throw(409; "Invoiced contracts cannot be cancelled")
		End if 
		$c.CancellationReason:=This.requireText($b.Reason; "Cancellation reason"; 2000)
		$c.IsCancelled:=True
		$c.IsActive:=False
		$c.CancelledAt:=Timestamp
		This.save($c)
		var $j : 4D.Entity
		For each ($j; ds.Job.query("GoTenantId = :1 AND ContractId = :2"; $t; $c.Id))
			$j.GoCancelled:=True
			This.save($j)
		End for each 
		This.timeline($a; $c; "Repair cancelled"; $c.CancellationReason; True)
		This.audit($a; "cancel"; "Contract"; $c.Id)
		return This.reply(200; This.publicRow("Contract"; $c))
	End if 
	If (($path="api/diary") & ($verb="GET"))
		This.requireRole($a; "Admin,Staff")
		var $jobs:=ds.Job.query("GoTenantId = :1 AND GoCancelled = false AND DiaryDate = :2"; $t; String($r.urlQuery.date)).orderBy("GoTime, AgentId")
		return This.reply(200; This.rows("Job"; $jobs; $offset))
	End if 
	If (($path="api/diary/book") & ($verb="POST"))
		return This.reply(201; This.book($a; $b))
	End if 
	If (($path="api/diary/cancel") & ($verb="POST"))
		This.requireRole($a; "Admin,Staff")
		$j:=This.row("Job"; Num($b.Id); $t)
		This.checkRevision($j; $b)
		$j.GoCancelled:=True
		This.save($j)
		$c:=This.row("Contract"; $j.ContractId; $t)
		This.timeline($a; $c; "Appointment cancelled"; This.requireText($b.Reason; "Reason"; 2000); True)
		This.audit($a; "cancel"; "Job"; $j.Id)
		return This.reply(200; New object("ok"; True))
	End if 
	If (($path="api/jobs/complete") & ($verb="POST"))
		This.requireRole($a; "Admin,Staff")
		$j:=This.row("Job"; Num($b.Id); $t)
		$c:=This.row("Contract"; $j.ContractId; $t)
		If ($j.GoCancelled | $c.IsCancelled)
			throw(409; "Cancelled work cannot be completed")
		End if 
		Case of 
			: ($j.JobType=0) & ($c.Status=10)
				$c.Status:=20
			: ($j.JobType=1) & ($c.Status=70)
				$c.Status:=80
			Else 
				throw(409; "Contract is not ready for this completion")
		End case 
		This.save($c)
		This.timeline($a; $c; "Appointment completed"; This.requireText($b.Description; "Completion notes"; 4000); True)
		This.audit($a; "complete"; "Job"; $j.Id)
		return This.reply(200; This.publicRow("Contract"; $c))
	End if 
	If (($path="api/estimates") & ($verb="POST"))
		return This.reply(201; This.estimate($a; $b))
	End if 
	If (($path="api/estimates/authorise") & ($verb="POST"))
		return This.reply(200; This.authorise($a; $b))
	End if 
	If (($path="api/invoices") & ($verb="POST"))
		return This.reply(201; This.invoice($a; $b))
	End if 
	If (($r.urlPath.length=3) & New collection("estimates"; "invoices").includes($resource) & ($verb="GET"))
		var $isEstimate:=($resource="estimates")
		var $table:=Choose($isEstimate; "Estimates"; "Invoices")
		var $e:=This.row($table; Num($r.urlPath[2]); $t)
		This.ownedContract($a; $e.ContractId)
		If ($isEstimate & ($a.Role="Customer"))
			If (($e.AuthorizedUtc=Null) | ($e.AuthorizedUtc=""))
				throw(404; "Estimate not found")
			End if 
		End if 
		var $lineTable:=Choose($isEstimate; "EstimateLines"; "InvoiceLines")
		var $foreignKey:=Choose($isEstimate; "EstimateId"; "InvoiceId")
		return This.reply(200; New object("document"; This.publicRow($table; $e); "lines"; This.rows($lineTable; ds[$lineTable].query("GoTenantId = :1 AND "+$foreignKey+" = :2"; $t; $e.Id).orderBy("SortOrder"); 0).items))
	End if 
	If (($path="api/claims") & ($verb="GET"))
		This.requireRole($a; "Admin,Staff,Insurer")
		var $claims:=ds.InsuranceClaims.query("GoTenantId = :1"; $t)
		If ($a.Role="Insurer")
			$claims:=$claims.query("InsurerId = :1"; $a.InsurerId)
		End if 
		return This.reply(200; This.rows("InsuranceClaims"; $claims.orderBy("Id desc"); $offset))
	End if 
	If (($path="api/claims") & ($verb="POST"))
		This.requireRole($a; "Admin,Staff,Insurer")
		var $claim:=This.newRow("InsuranceClaims"; $t)
		This.applyFields("InsuranceClaims"; $claim; $b)
		If ($a.Role="Insurer")
			$claim.InsurerId:=$a.InsurerId
		End if 
		This.row("Insurers"; Num($claim.InsurerId); $t)
		$claim.ClaimReference:=This.requireText($claim.ClaimReference; "Claim reference"; 50)
		$claim.CustomerName:=This.requireText($claim.CustomerName; "Customer name"; 100)
		$claim.Created:=Timestamp
		$claim.Status:=0
		This.save($claim)
		This.audit($a; "create"; "InsuranceClaims"; $claim.Id)
		return This.reply(201; This.publicRow("InsuranceClaims"; $claim))
	End if 
	If (($path="api/claims/convert") & ($verb="POST"))
		This.requireRole($a; "Admin,Staff")
		$claim:=This.row("InsuranceClaims"; Num($b.Id); $t)
		This.checkRevision($claim; $b)
		If (ds.Contract.query("GoTenantId = :1 AND InsuranceClaimId = :2"; $t; $claim.Id).length>0)
			throw(409; "Claim already has a contract")
		End if 
		var $customer:=This.newRow("Customer"; $t)
		$customer.Name:=$claim.CustomerName
		$customer.Add1:=$claim.Address1
		$customer.Add2:=$claim.Address2
		$customer.Add3:=$claim.Address3
		$customer.Postcode:=$claim.Postcode
		$customer.Email:=$claim.Email
		$customer.Phone1:=$claim.Telephone
		$customer.IsActive:=True
		This.save($customer)
		var $data:=New object("CustomerId"; $customer.Id; "InsurerId"; $claim.InsurerId; "InsuranceClaimId"; $claim.Id; "DamageDescription"; $claim.DamageDescription; "CauseOfDamage"; $claim.CauseOfDamage; "PolicyNumber"; $claim.PolicyNumber; "IncidentDate"; $claim.IncidentDate; "Excess"; String($claim.Excess); "CustomerReference"; $claim.ClaimReference)
		var $contract:=This.createContract($a; $data)
		$claim.Status:=2
		$claim.AssignedTenantId:=$t
		$claim.AssignedDate:=Timestamp
		This.save($claim)
		This.audit($a; "convert"; "InsuranceClaims"; $claim.Id)
		return This.reply(201; $contract)
	End if 
	If (($path="api/tickets") & ($verb="GET"))
		This.requireRole($a; "Admin,Staff,Customer")
		var $tickets:=ds.GoTicket.query("GoTenantId = :1"; $t)
		If ($a.Role="Customer")
			$tickets:=$tickets.query("CustomerId = :1"; $a.CustomerId)
		End if 
		return This.reply(200; This.rows("GoTicket"; $tickets.orderBy("Id desc"); $offset))
	End if 
	If (($path="api/tickets") & ($verb="POST"))
		This.requireRole($a; "Admin,Staff,Customer")
		var $customerId:=Choose($a.Role="Customer"; $a.CustomerId; Num($b.CustomerId))
		This.row("Customer"; $customerId; $t)
		var $ticket:=This.newRow("GoTicket"; $t)
		$ticket.CustomerId:=$customerId
		$ticket.Subject:=This.requireText($b.Subject; "Subject"; 200)
		$ticket.Status:="Open"
		$ticket.CreatedAt:=Timestamp
		This.save($ticket)
		var $m:=This.newRow("GoTicketMessage"; $t)
		$m.TicketId:=$ticket.Id
		$m.Body:=This.requireText($b.Body; "Message"; 4000)
		$m.SenderRole:=$a.Role
		$m.CreatedAt:=Timestamp
		This.save($m)
		This.audit($a; "ticket"; "GoTicket"; $ticket.Id)
		return This.reply(201; This.publicRow("GoTicket"; $ticket))
	End if 
	If (($path="api/ticket-messages") & New collection("GET"; "POST").includes($verb))
		This.requireRole($a; "Admin,Staff,Customer")
		var $ticketId:=Choose($verb="GET"; Num($r.urlQuery.ticketId); Num($b.TicketId))
		$ticket:=This.row("GoTicket"; $ticketId; $t)
		If (($a.Role="Customer") & ($ticket.CustomerId#$a.CustomerId))
			throw(404; "Ticket not found")
		End if 
		If ($verb="GET")
			return This.reply(200; This.rows("GoTicketMessage"; ds.GoTicketMessage.query("GoTenantId = :1 AND TicketId = :2"; $t; $ticketId).orderBy("Id"); $offset))
		End if 
		If ($ticket.Status="Closed")
			throw(409; "This ticket is closed")
		End if 
		$m:=This.newRow("GoTicketMessage"; $t)
		$m.TicketId:=$ticketId
		$m.Body:=This.requireText($b.Body; "Message"; 4000)
		$m.SenderRole:=$a.Role
		$m.CreatedAt:=Timestamp
		This.save($m)
		This.audit($a; "reply"; "GoTicket"; $ticketId)
		return This.reply(201; This.publicRow("GoTicketMessage"; $m))
	End if 
	If (($path="api/tickets/close") & ($verb="POST"))
		This.requireRole($a; "Admin,Staff")
		$ticket:=This.row("GoTicket"; Num($b.Id); $t)
		This.checkRevision($ticket; $b)
		$ticket.Status:="Closed"
		This.save($ticket)
		This.audit($a; "close"; "GoTicket"; $ticket.Id)
		return This.reply(200; New object("ok"; True))
	End if 
	If (($path="api/messages") & New collection("GET"; "POST").includes($verb))
		This.requireRole($a; "Admin,Staff,Insurer")
		var $contractId:=Choose($verb="GET"; Num($r.urlQuery.contractId); Num($b.ContractId))
		$c:=This.ownedContract($a; $contractId)
		If ($verb="GET")
			return This.reply(200; This.rows("GoMessage"; ds.GoMessage.query("GoTenantId = :1 AND ContractId = :2"; $t; $contractId).orderBy("Id"); $offset))
		End if 
		This.row("Insurers"; Num($c.InsurerId); $t)
		$m:=This.newRow("GoMessage"; $t)
		$m.ContractId:=$contractId
		$m.InsurerId:=$c.InsurerId
		$m.Body:=This.requireText($b.Body; "Message"; 4000)
		$m.SenderRole:=$a.Role
		$m.CreatedAt:=Timestamp
		This.save($m)
		This.audit($a; "message"; "Contract"; $contractId)
		return This.reply(201; This.publicRow("GoMessage"; $m))
	End if 
	If (($path="api/web/snapshot") & ($verb="GET"))
		var $webBridge:=cs.SmartOpsWebBridge.new(This)
		return This.reply(200; $webBridge.snapshot($a))
	End if 
	If (($path="api/web/commit") & ($verb="POST"))
		This.debugStage:="commit: creating bridge"
		$webBridge:=cs.SmartOpsWebBridge.new(This)
		This.debugStage:="commit: executing"
		return This.reply(200; $webBridge.commit($a; $b))
	End if 
	If (($path="api/accounts") & ($verb="GET"))
		This.requireRole($a; "Admin")
		return This.reply(200; This.rows("GoAccount"; ds.GoAccount.query("GoTenantId = :1"; $t).orderBy("Id"); $offset))
	End if 
	If (($path="api/accounts") & ($verb="POST"))
		This.requireRole($a; "Admin")
		var $user:=Lowercase(This.requireText($b.Username; "Username"; 200))
		$password:=This.requirePassword($b.Password)
		var $role:=This.requireText($b.Role; "Role"; 20)
		If (Not(New collection("Admin"; "Staff"; "Customer"; "Insurer").includes($role)))
			throw(400; "Unknown account role")
		End if 
		If (Length($password)<12)
			throw(400; "Use at least 12 password characters")
		End if 
		If (ds.GoAccount.query("GoTenantId = :1 AND Username = :2"; $t; $user).length>0)
			throw(409; "Username already exists")
		End if 
		var $account:=This.newRow("GoAccount"; $t)
		$account.Username:=$user
		$account.PasswordHash:=Generate password hash(Generate digest($password; SHA256 digest); New object("cost"; 12))
		$account.Role:=$role
		$account.Active:=True
		If ($role="Customer")
			$account.CustomerId:=This.row("Customer"; Num($b.CustomerId); $t).Id
		End if 
		If ($role="Insurer")
			$account.InsurerId:=This.row("Insurers"; Num($b.InsurerId); $t).Id
		End if 
		This.save($account)
		This.audit($a; "create"; "GoAccount"; $account.Id)
		return This.reply(201; This.publicRow("GoAccount"; $account))
	End if 
	If (($path="api/audit") & ($verb="GET"))
		This.requireRole($a; "Admin")
		return This.reply(200; This.rows("GoAudit"; ds.GoAudit.query("GoTenantId = :1"; $t).orderBy("Id desc"); $offset))
	End if 
	If (($r.urlPath.length>=3) & ($resource="data"))
		This.requireRole($a; "Admin,Staff")
		$table:=String($r.urlPath[2])
		If ($table="Branches")
			This.requireRole($a; "Admin")
		End if
		If (Not(OB Is defined(This.schema; $table)))
			throw(404; "Unknown table")
		End if 
		If (Not(This.schema[$table].generic))
			throw(403; "Use the dedicated workflow for this table")
		End if 
		var $id:=0
		If ($r.urlPath.length=4)
			$id:=Num($r.urlPath[3])
		End if 
		Case of 
			: ($verb="GET") & ($r.urlPath.length=3)
				return This.reply(200; This.rows($table; ds[This.schema[$table].nativeTable].query("GoTenantId = :1"; $t).orderBy("Id desc"); $offset))
			: ($verb="GET") & ($id>0)
				return This.reply(200; This.publicRow($table; This.row($table; $id; $t)))
			: ($verb="POST") & ($r.urlPath.length=3)
                cs.SmartOpsWebBridge.new(This).requireLockedBranch($a; $table; $b; 0)
				$e:=This.newRow($table; $t)
				This.applyFields($table; $e; $b)
				This.save($e)
				This.audit($a; "create"; $table; $e.Id)
				return This.reply(201; This.publicRow($table; $e))
			: ($verb="PUT") & ($id>0)
                cs.SmartOpsWebBridge.new(This).requireLockedBranch($a; $table; $b; $id)
				$e:=This.row($table; $id; $t)
				This.checkRevision($e; $b)
				This.applyFields($table; $e; $b)
				This.save($e)
				This.audit($a; "edit"; $table; $e.Id)
				return This.reply(200; This.publicRow($table; $e))
		End case 
	End if 
	return This.reply(404; New object("error"; "No such API route or HTTP method"))
