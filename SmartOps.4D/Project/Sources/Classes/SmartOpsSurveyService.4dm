// Compatibility boundary: the existing SmartOps.Survey wire contract is retained.
property service : cs.SmartOpsService


Class constructor()
	This.service:=cs.SmartOpsService.new()
	
	
Function reply($status : Integer; $value : Variant) : 4D.OutgoingMessage
	var $response:=4D.OutgoingMessage.new()
	$response.setBody(JSON Stringify($value))
	$response.setHeader("Content-Type"; "application/json; charset=utf-8")
	$response.setHeader("Cache-Control"; "no-store")
	$response.setHeader("X-Content-Type-Options"; "nosniff")
	$response.setStatus($status)
	return $response
	
	
Function value($body : Object; $name : Text) : Variant
	// SurveyEnRoute uses PascalCase; the other existing clients use camelCase.
	If (OB Is defined($body; $name))
		return $body[$name]
	End if 
	
	var $camel:=Lowercase(Substring($name; 1; 1))+Substring($name; 2)
	return $body[$camel]
	
	
Function camel($name : Text) : Text
	// System.Text.Json's acronym handling (CheckID -> checkID, DBId -> dbId).
	var $out:=$name
	var $i : Integer
	For ($i; 1; Length($name))
		var $charCode:=Character code(Substring($name; $i; 1))
		If (($charCode<65) | ($charCode>90))
			break
		End if 
		
		If (($i>1) & ($i<Length($name)))
			var $nextCode:=Character code(Substring($name; $i+1; 1))
			If (($nextCode<65) | ($nextCode>90))
				break
			End if 
		End if 
		
		$out:=Substring($out; 1; $i-1)+Lowercase(Substring($name; $i; 1))+Substring($name; $i+1)
	End for 
	
	return $out
	
	
Function dto($table : Text; $entity : 4D.Entity) : Object
	var $result:=New object
	var $field : Object
	For each ($field; This.service.schema[$table].fields)
		If (This.service.schema[$table].publicFields.includes($field.name) & (Position("Go"; $field.name)#1))
			$result[This.camel($field.name)]:=$entity[$field.nativeName]
			// Stored GUIDs use compact form; System.Text.Json expects hyphens.
			If (($field.sourceType="Guid") | ($field.sourceType="Guid?"))
				var $guid:=Uppercase(Replace string(String($entity[$field.nativeName]); "-"; ""))
				If (Match regex("^[A-F0-9]{32}$"; $guid))
					$result[This.camel($field.name)]:=Substring($guid;1;8)+"-"+Substring($guid;9;4)+"-"+Substring($guid;13;4)+"-"+Substring($guid;17;4)+"-"+Substring($guid;21;12)
				End if
			End if
		End if 
	End for each 
	
	return $result
	
	
Function company($code : Text) : 4D.Entity
	var $company : 4D.Entity
	$company:=ds[This.service.schema.Tenant.nativeTable].query("Code = :1 AND IsActive = true"; $code).first()
	If ($company=Null)
		throw(401; "Invalid company or agent credentials")
	End if 
	
	return $company
	
	

Function login($body : Object) : 4D.OutgoingMessage

    var $company:=This.company(String(This.value($body; "TenantCode")))

    var $agent : 4D.Entity
    $agent:=ds.Agent.query("GoTenantId = :1 AND Code = :2"; $company.Id; String(This.value($body; "AgentCode"))).first()

    If ($agent=Null)
        return This.reply(401; New object("reasonPhrase"; "Agent not found"))
    End if

    // Developer demo: an Agent without an assigned serial number may sign in
    // using their company and AgentCode alone. An Agent with a registered
    // serial retains the existing device checks below.
    var $assignedSerial:=Lowercase(Trim(String($agent.SerialNumber)))
    If (Length($assignedSerial)>0)
        var $serialNumber:=Lowercase(Trim(String(This.value($body; "SerialNumber"))))
        If ((Length($serialNumber)#16) | (Not(Match regex("^[a-f0-9]{16}$"; $serialNumber))))
            return This.reply(401; New object("reasonPhrase"; "Invalid or missing Android device ID"))
        End if

        var $registeredDevice : 4D.Entity
        $registeredDevice:=ds.Agent.query("GoTenantId = :1 AND SerialNumber = :2"; $company.Id; $serialNumber).first()
        If ($registeredDevice=Null)
            return This.reply(401; New object("reasonPhrase"; "Device is not registered for this company"))
        End if

        // Privileged agents with a configured device must use their own device.
        // Ordinary agents with a configured device may use any registered
        // device within their tenant, as before.
        If ($agent.PrivilegedAccess)
            If ($assignedSerial#$serialNumber)
                return This.reply(401; New object("reasonPhrase"; "This account requires its own registered device"))
            End if
        End if
    End if

    // Generate the authentication token used by SmartOps.Survey.
    var $token:=Generate UUID+Generate UUID

    $agent.AuthenticationString:=$token
    This.service.save($agent)

    var $branchCode:=""

    If ($agent.BranchId#Null)
        If ($agent.BranchId>0)
            $branchCode:=This.service.row("Branches"; $agent.BranchId; $company.Id).Code
        End if
    End if

    return This.reply(200; New object("tenantCode"; $company.Code; "agentCode"; $agent.Code; "agentType"; $agent.GoType; "branchCode"; $branchCode; "authenticationString"; $token; "currentVersion"; String(This.value($body; "CurrentVersion")); "currentBuild"; String(This.value($body; "CurrentBuild"))))


	
Function authenticate($body : Object) : 4D.Entity
	var $company:=This.company(String(This.value($body; "TenantCode")))
	This.service.logTenantId:=$company.Id
	var $token:=String(This.value($body; "AuthenticationString"))
	If (Length($token)=0)
		throw(401; "Authentication failed. Sign in again.")
	End if 
	
	var $agent:=ds.Agent.query("GoTenantId = :1 AND Code = :2 AND AuthenticationString = :3"; $company.Id; String(This.value($body; "AgentCode")); $token).first()
	If ($agent=Null)
		throw(401; "Authentication failed. Sign in again.")
	End if 
	
	// Return the Agent principal; no web account lookup for mobile API.
    return $agent

    Function accessibleJobs($account : 4D.Entity) : 4D.EntitySelection
	var $jobs:=ds.Job.query("GoTenantId = :1 AND GoCancelled = false"; $account.GoTenantId)
	If (Not($account.PrivilegedAccess))
		$jobs:=$jobs.query("AgentId = :1"; $account.Id)
	End if 
	
	// Repair older web-created bookings before applying date/time filters.
	var $job : 4D.Entity
	For each ($job; $jobs)
		This.service.normalizeJob($job)
	End for each 
	
	return $jobs
	
	
Function contract($account : 4D.Entity; $code : Text) : 4D.Entity
	If (Not(Match regex("^[0-9]{1,8}$"; $code)))
		throw(400; "Enter a valid contract number")
	End if 
	
	var $c:=ds.Contract.query("GoTenantId = :1 AND ContractNumber = :2"; $account.GoTenantId; Num($code)).first()
	If ($c=Null)
		throw(404; "Contract not found")
	End if 
	
	If (This.accessibleJobs($account).query("ContractId = :1"; $c.Id).length=0)
		throw(404; "Contract is not assigned to this agent")
	End if 
	
	return $c
	
	
Function headForJob($account : 4D.Entity; $job : 4D.Entity) : Object
	var $c:=This.service.row("Contract"; $job.ContractId; $account.GoTenantId)
	var $customer:=This.service.row("Customer"; $c.CustomerId; $account.GoTenantId)
	var $head:=JSON Parse(File("/RESOURCES/survey-head-default.json").getText())
	var $items:=New collection
	
	// Older/demo jobs can have a null or empty visit GUID. Never send null
	// to the .NET MAUI client: JobHeader.Guid is System.Guid, not a string.
	// Store the compact N format for stable server-side matching.
	var $visitGuid:=""
	If ($job.GoVisitGuid#Null)
		$visitGuid:=Uppercase(Replace string(String($job.GoVisitGuid); "-"; ""))
	End if 
	var $existing : 4D.Entity
	$existing:=ds.JobHeader.query("GoTenantId = :1 AND ContractId = :2 AND Guid = :3"; $account.GoTenantId; $c.Id; $visitGuid).first()
	If ($existing#Null)
		// Moving a completed booking to another day/type creates a new visit.
		If ((Substring(String($existing.DiaryDate);1;10)#$job.DiaryDate) | ($existing.JobType#$job.JobType))
			$existing:=Null
			$visitGuid:=""
		End if
	End if
	
	If (Not(Match regex("^[A-F0-9]{32}$"; $visitGuid)))
		$visitGuid:=Uppercase(Replace string(Generate UUID; "-"; ""))
		If (Not(Match regex("^[A-F0-9]{32}$"; $visitGuid)))
			throw(500; "Could not generate a valid visit GUID")
		End if 
		
		$job.GoVisitGuid:=$visitGuid
		This.service.save($job)
	End if 
	
	$head.bSurvey:=False
	If ($existing#Null)
		// Reopen this exact survey, including items retained from earlier sends.
		// The Include Survey flag only controls copying a different survey.
		$head:=This.dto("JobHeader"; $existing)
		var $item : 4D.Entity
		For each ($item; ds.CustomItems.query("GoTenantId = :1 AND HeaderId = :2"; $account.GoTenantId; $existing.Id).orderBy("item_number, Id"))
			$items.push(This.dto("CustomItems"; $item))
		End for each
		$head.bSurvey:=True
	End if
	If ($job.bIncludeSurvey & (($existing=Null) | ($job.JobType#0)))
		// Saved surveys can exist without a mobile upload-log entry. Read
		// the persisted header and every item, including later additions.
		var $survey:=ds.JobHeader.query("GoTenantId = :1 AND ContractId = :2 AND JobType = 0"; $account.GoTenantId; $c.Id).orderBy("DateTimeCompleted desc, Id desc").first()
		If ($survey#Null)
			If ($existing=Null)
				$head:=This.dto("JobHeader"; $survey)
				var $surveyItem : 4D.Entity
				For each ($surveyItem; ds.CustomItems.query("GoTenantId = :1 AND HeaderId = :2"; $account.GoTenantId; $survey.Id).orderBy("item_number, Id"))
					$items.push(This.dto("CustomItems"; $surveyItem))
				End for each
			Else
				// Include the report without replacing saved fitting progress/items.
				$head.report:=$survey.Report
			End if
			$head.bSurvey:=True
		Else
			// Retain compatibility with older payload-only submissions.
			var $previous:=ds.GoSurveySubmission.query("GoTenantId = :1 AND ContractId = :2 AND JobType = 0"; $account.GoTenantId; $c.Id).orderBy("Id desc").first()
			If ($previous#Null)
				var $saved:=JSON Parse($previous.Payload)
				If ($existing=Null)
					$head:=$saved.head
					$items:=$saved.customItems
				Else
					$head.report:=$saved.head.report
				End if
				$head.bSurvey:=True
			End if
		End if
	End if

	// D format is accepted by System.Text.Json's System.Guid converter.
	var $clientGuid:=Substring($visitGuid; 1; 8)+"-"+Substring($visitGuid; 9; 4)+"-"+Substring($visitGuid; 13; 4)+"-"+Substring($visitGuid; 17; 4)+"-"+Substring($visitGuid; 21; 12)
	$head.id:=0
	$head.tenantId:=$account.GoTenantId
	$head.guid:=$clientGuid
	$head.jobType:=$job.JobType
	$head.contractId:=$c.Id
	$head.customerId:=$customer.Id
	$head.agentId:=$job.AgentId
	$head.contractCode:=String($c.ContractNumber; "00000000")
	$head.diaryDate:=$job.DiaryDate+"T00:00:00"
	$head.startTime:=$job.GoTime
	$head.finishTime:=String(Int($job.GoEndMinute/60); "00")+":"+String($job.GoEndMinute%60; "00")
	$head.jobInstructions:=$job.Instructions
	$head.bRequestRepudiation:=$job.bRequestRepudiation
	$head.enRouteNotificationSent:=$job.EnRouteNotificationSent
	$head.enRoutePressedAtUtc:=Null

	If ($job.EnRoutePressedAtUtc#Null)
		var $enRouteDate:=Trim(String($job.EnRoutePressedAtUtc))

		If (Length($enRouteDate)>0)
			$head.enRoutePressedAtUtc:=Replace string($enRouteDate; " "; "T")
		End if
	End if
	$head.customerName:=$customer.Name
	$head.customerAddressLine1:=$customer.Add1
	$head.customerAddressLine2:=String($customer.Add2)
	$head.customerAddressLine3:=String($customer.Add3)
	$head.customerPostcode:=$customer.Postcode
	$head.customerPhoneNumber:=$customer.Phone1
	$head.customerPhoneNumber2:=String($customer.Phone2)
	$head.customerPhoneNumber3:=String($customer.Phone3)
	$head.incidentDate:=$c.IncidentDate
	$head.causeOfDamage:=$c.CauseOfDamage
	$head.policyNumber:=$c.PolicyNumber
	$head.excess:=$c.Excess
	$head.damageDescription:=$c.DamageDescription
	$head.trafficLight:=$c.TrafficLight
	$head.insuranceCompanyName:=""
	If ($c.InsurerId#Null)
		If ($c.InsurerId>0)
			$head.insuranceCompanyName:=This.service.row("Insurers"; $c.InsurerId; $account.GoTenantId).Name
		End if 
	End if 
	
	$head.bComplete:=False
	$head.bSent:=False
	var $images:=New collection
	var $media : 4D.Entity
	For each ($media; ds.GoMediaFile.query("GoTenantId = :1 AND Filename = :2 AND Completed = true"; $account.GoTenantId; $head.contractCode+"_@"))
		$images.push($media.Filename)
	End for each 
	
	return New object("head"; $head; "customItems"; $items; "images"; $images)
	
	
Function projectFields($table : Text; $entity : 4D.Entity; $payload : Object)
	var $field : Object
	For each ($field; This.service.schema[$table].fields)
		If (Not(New collection("Id"; "TenantId"; "GoTenantId"; "GoRevision").includes($field.name)) & (Position("Go"; $field.name)#1) & ($field.kind#"blob"))
			var $key:=This.camel($field.name)
			If (OB Is defined($payload; $key))
				var $v:=$payload[$key]
				If ($v#Null)
					If (($field.kind="text") & (Value type($v)#Is text))
						throw(400; "Invalid text field: "+$key)
					End if 
					
					If (($field.kind="boolean") & (Value type($v)#Is boolean))
						throw(400; "Invalid boolean field: "+$key)
					End if 
					
					If (New collection("integer"; "number").includes($field.kind) & (Value type($v)#Is real))
						throw(400; "Invalid numeric field: "+$key)
					End if 
				End if 
				
				$entity[$field.nativeName]:=$v
			End if 
		End if 
	End for each 
	
	
Function appVersion($account : 4D.Entity; $body : Object; $userAgent : Text) : 4D.OutgoingMessage
	var $company:=ds[This.service.schema.Tenant.nativeTable].get($account.GoTenantId)
	var $agent:=$account
	var $latest:="0"
	var $setting:=ds.SystemSettings.query("GoTenantId = :1 AND Key = :2"; $account.GoTenantId; "SurveyAppLatestBuild").first()
	If ($setting#Null)
		If (Match regex("^[0-9]{1,10}$"; String($setting.Value)))
			$latest:=String($setting.Value)
		End if 
	End if 
	
	var $current:=Substring(String(This.value($body; "CurrentBuild")); 1; 50)
	var $version:=Substring(String(This.value($body; "CurrentVersion")); 1; 50)
	var $installed:=0
	If (Match regex("^[0-9]{1,10}$"; $current))
		$installed:=Num($current)
	End if 
	
	var $available:=(Num($latest)>$installed)
	var $check:=This.service.newRow("SurveyAppRequests"; $account.GoTenantId)
	$check.RequestDate:=Timestamp
	$check.TenantCode:=$company.Code
	$check.AgentCode:=$agent.Code
	$check.CurrentVersion:=$version
	$check.CurrentBuild:=$current
	$check.LatestBuild:=$latest
	$check.UpdateAvailable:=$available
	$check.DeviceManufacturer:=Substring(String(This.value($body; "DeviceManufacturer")); 1; 100)
	$check.DeviceModel:=Substring(String(This.value($body; "DeviceModel")); 1; 100)
	$check.DeviceName:=Substring(String(This.value($body; "DeviceName")); 1; 100)
	$check.Platform:=Substring(String(This.value($body; "Platform")); 1; 50)
	$check.OsVersion:=Substring(String(This.value($body; "OsVersion")); 1; 100)
	$check.UserAgent:=$userAgent
	This.service.save($check)
	$agent.CurrentAppVersion:=$version+" ("+$current+")"
	$agent.LoggedInDateTime:=Timestamp
	This.service.save($agent)
	return This.reply(200; New object("latestBuild"; $latest; "updateAvailable"; $available))
	
	
Function sendJobs($account : 4D.Entity; $body : Object) : 4D.OutgoingMessage
	var $jobs:=This.value($body; "JobHeaders")
	If (Value type($jobs)#Is collection)
		throw(400; "jobHeaders must be an array")
	End if 
	
	If ($jobs.length>100)
		throw(400; "Send at most 100 jobs in one batch")
	End if 
	
	var $packet : Object
	For each ($packet; $jobs)
		If ((Value type($packet.head)#Is object) | (Value type($packet.customItems)#Is collection))
			throw(400; "Each job needs a head and customItems array")
		End if 
		
		var $head:=$packet.head
		var $payload:=JSON Stringify($packet)
		var $digest:=Generate digest($payload; SHA256 digest)
		var $c:=This.contract($account; String($head.contractCode))
		
		// The mobile client sends a canonical GUID with hyphens while 4D
		// stores compact visit GUIDs. Compare normalised values rather than
		// requiring byte-for-byte equality inside an ORDA text query.
		var $guid:=Uppercase(Replace string(String($head.guid); "-"; ""))
		If (Not(Match regex("^[A-F0-9]{32}$"; $guid)))
			throw(400; "Invalid survey visit GUID")
		End if 
		
		var $job : 4D.Entity
		$job:=Null
		var $recoveredAgent:=False
		var $candidate : 4D.Entity
		var $matches:=This.accessibleJobs($account).query("ContractId = :1"; $c.Id)
		For each ($candidate; $matches)
			var $storedGuid:=""
			If ($candidate.GoVisitGuid#Null)
				$storedGuid:=Uppercase(Replace string(String($candidate.GoVisitGuid); "-"; ""))
			End if 
			
			If ($storedGuid=$guid)
				$job:=$candidate
				break
			End if 
		End for each 
		
		If ($job=Null)
			// Recovery for earlier SmartOps.Survey builds that replaced a received
			// visit GUID with Guid.NewGuid(). Never accept ambiguous bookings:
			// require a single current booking for the same contract, agent,
			// visit type and date. Previously submitted bookings only permit an
			// exact replay of the already accepted legacy submission.
			This.service.debugStage:="sendJobs: checking legacy mobile GUID recovery"
			var $eligible:=New collection
			For each ($candidate; $matches)
				If (($candidate.CustomerId=Num($head.customerId)) & ($candidate.JobType=Num($head.jobType)) & ($candidate.DiaryDate=Substring(String($head.diaryDate); 1; 10)))
					If ($candidate.AgentId=Num($head.agentId))
						$eligible.push($candidate.Id)
					Else 
						// Older clients can retain the agent from a previous fitting
						// when downloading a later survey for the same contract.
						// Require an all-jobs account, exact current visit details,
						// and an earlier fitting for that same stale agent/customer.
						If ($account.PrivilegedAccess & ($candidate.JobType=0) & (Num($head.contractId)=$c.Id) & (Num($head.customerId)=$c.CustomerId) & ($candidate.GoTime=String($head.startTime)))
							var $priorFitting:=ds.Job.query("GoTenantId = :1 AND ContractId = :2 AND CustomerId = :3 AND AgentId = :4 AND JobType = 1 AND DiaryDate < :5 AND GoCancelled = false"; $account.GoTenantId; $c.Id; $c.CustomerId; Num($head.agentId); $candidate.DiaryDate)
							If ($priorFitting.length>0)
								$eligible.push($candidate.Id)
							End if 
						End if 
						
						// Older tablets retain the survey agent when receiving a
						// fitting visit. Recovery needs an all-jobs account, its own
						// stale ID, saved survey evidence and an exact fitting time.
						If ($account.PrivilegedAccess & (Num($head.agentId)=$account.Id) & (Num($head.contractId)=$c.Id) & (Num($head.customerId)=$c.CustomerId))
							If (($candidate.JobType=1) & ($candidate.GoTime=String($head.startTime)))
								This.service.debugStage:="sendJobs: checking prior survey for fitting agent recovery"
								var $previousSurvey:=ds.JobHeader.query("GoTenantId = :1 AND ContractId = :2 AND CustomerId = :3 AND AgentId = :4 AND JobType = 0 AND bSent = true"; $account.GoTenantId; $c.Id; $c.CustomerId; $account.Id)
								If ($previousSurvey.length>0)
									$eligible.push($candidate.Id)
								End if 
							End if 
						End if 
					End if 
				End if 
			End for each 
			
			If ($eligible.length=1)
				var $recoveryJob:=This.service.row("Job"; Num($eligible[0]); $account.GoTenantId)
				var $alreadySubmitted:=ds.GoSurveySubmission.query("GoTenantId = :1 AND JobId = :2"; $account.GoTenantId; $recoveryJob.Id)
				var $canRecover:=($alreadySubmitted.length=0)
				If (Not($canRecover))
					// A lost HTTP response can make the tablet resend a successful
					// legacy submission. Only allow its exact stored payload through
					// to the idempotency check; different payloads remain rejected.
					var $recoveryKey:=String($recoveryJob.Id)+":"+$guid
					$canRecover:=($alreadySubmitted.query("SubmissionKey = :1 AND Digest = :2"; $recoveryKey; $digest).length>0)
				End if 
				
				If ($canRecover)
					$job:=$recoveryJob
					$recoveredAgent:=($job.AgentId#Num($head.agentId))
					This.service.debugStage:="sendJobs: recovered legacy locally generated GUID"
				End if 
			End if 
			
			If ($job=Null)
				// Keep identity-only diagnostics for a rejected offline survey.
				// Do not log the survey answers or full request payload.
				This.service.debugStage:="sendJobs: unmatched visit "+String($head.contractCode)+"; agent="+String($head.agentId)+"; customer="+String($head.customerId)+"; type="+String($head.jobType)+"; date="+String($head.diaryDate)+"; time="+String($head.startTime)+"; eligible="+String($eligible.length)
				throw(409; "Visit GUID does not match an unsubmitted unique booking for contract "+String($head.contractCode)+". Keep the completed survey on the tablet.")
			End if 
		End if 
		
		If (($head.contractId#$c.Id) | ($head.customerId#$c.CustomerId) | (($head.agentId#$job.AgentId) & Not($recoveredAgent)) | ($head.jobType#$job.JobType) | (Substring($head.diaryDate; 1; 10)#$job.DiaryDate))
			throw(400; "Job ownership or visit details do not match the booking")
		End if 
		
		If (Not($head.bComplete=True))
			throw(400; "Only completed jobs can be submitted")
		End if 
		
		var $key:=String($job.Id)+":"+$guid
		var $existing:=ds.GoSurveySubmission.query("GoTenantId = :1 AND SubmissionKey = :2 AND Digest = :3"; $account.GoTenantId; $key; $digest).first()
		If ($existing#Null)
			continue
		End if 
		
		var $submission:=This.service.newRow("GoSurveySubmission"; $account.GoTenantId)
		$submission.AccountId:=$account.Id
		$submission.JobId:=$job.Id
		$submission.ContractId:=$c.Id
		$submission.JobType:=$job.JobType
		$submission.SubmissionKey:=$key
		$submission.Digest:=$digest
		$submission.Payload:=$payload
		$submission.ReceivedAt:=Timestamp
		This.service.save($submission)
		This.service.debugStage:="sendJobs: finding survey header"
		
		// Declare nullable lookup results explicitly: inferring the variable
		// from Null raises error 54 when a new entity is assigned in 4D 21 R4.
		var $serverHead : 4D.Entity
		$serverHead:=ds.JobHeader.query("GoTenantId = :1 AND Guid = :2"; $account.GoTenantId; $guid).first()
		If ($serverHead=Null)
			This.service.debugStage:="sendJobs: creating survey header"
			$serverHead:=This.service.newRow("JobHeader"; $account.GoTenantId)
		End if 
		
		This.service.debugStage:="sendJobs: projecting survey header"
		This.projectFields("JobHeader"; $serverHead; $head)
		$serverHead.Guid:=$guid
		$serverHead.AgentId:=$job.AgentId
		$serverHead.CustomerId:=$c.CustomerId
		$serverHead.ContractId:=$c.Id
		$serverHead.bSent:=True
		This.service.save($serverHead)
		This.service.debugStage:="sendJobs: saving survey items"
		var $item : Object
		For each ($item; $packet.customItems)
			If (Value type($item)#Is object)
				throw(400; "Invalid survey item")
			End if 
			
			var $itemGuid:=Uppercase(Replace string(String($item.guid); "-"; ""))
			If (Not(Match regex("^[A-F0-9]{32}$"; $itemGuid)))
				throw(400; "Survey item GUID is invalid")
			End if 
			
			var $serverItem : 4D.Entity
			$serverItem:=ds.CustomItems.query("GoTenantId = :1 AND Guid = :2"; $account.GoTenantId; $itemGuid).first()
			If ($serverItem=Null)
				$serverItem:=This.service.newRow("CustomItems"; $account.GoTenantId)
			Else 
				If ($serverItem.HeaderId#$serverHead.Id)
					throw(409; "Survey item GUID is already used by another visit")
				End if 
			End if 
			
			This.projectFields("CustomItems"; $serverItem; $item)
			$serverItem.Guid:=$itemGuid
			$serverItem.HeaderId:=$serverHead.Id
			This.service.save($serverItem)
		End for each 
		Case of 
			: ($job.JobType=0) & ($c.Status<=20)
				$c.Status:=20
			: ($job.JobType=1) & ($c.Status=70)
				$c.Status:=80
		End case 
		
		This.service.save($c)
		This.service.timeline($account; $c; "Survey app submission received"; "Visit "+String($job.Id)+" completed"; True)
		This.queue($account; "SurveyCompleted"; $key; $packet)
		This.service.audit($account; "survey-submit"; "JobHeader"; $serverHead.Id)
	End for each 
	
	This.service.debugStage:="sendJobs: complete"
	return This.reply(200; New object("status"; "success"))
	
	
Function queue($account : 4D.Entity; $type : Text; $key : Text; $payload : Object)
	var $event:=This.service.newRow("GoIntegrationOutbox"; $account.GoTenantId)
	$event.AccountId:=$account.Id
	$event.EventType:=$type
	$event.EventKey:=$key
	$event.Payload:=JSON Stringify($payload)
	$event.Status:="TestCaptured"
	$event.CreatedAt:=Timestamp
	This.service.save($event)
	
	
Function upsertCheck($account : 4D.Entity; $table : Text; $body : Object) : 4D.Entity
	var $guid:=String($body.guid)
	If (Not(Match regex("^[A-Fa-f0-9-]{32,36}$"; $guid)))
		throw(400; "Each check requires its stable GUID")
	End if 
	
	var $entity : 4D.Entity
	$entity:=ds[This.service.schema[$table].nativeTable].query("GoTenantId = :1 AND Guid = :2"; $account.GoTenantId; $guid).first()
	If ($entity=Null)
		$entity:=This.service.newRow($table; $account.GoTenantId)
	Else 
		If ($entity.GoOwnerAccountId#$account.Id)
			throw(403; "A check with this GUID belongs to another agent")
		End if 
	End if 
	
	This.projectFields($table; $entity; $body)
	$entity.GoOwnerAccountId:=$account.Id
	If (OB Is defined(This.service.schema[$table].fieldMap; "bSent"))
		$entity.bSent:=True
	End if 
	
	This.service.save($entity)
	return $entity
	
	
Function checks($account : 4D.Entity; $body : Object) : 4D.OutgoingMessage
	var $packet:=New object
	var $map:=New object("LadderChecks"; "LadderChecks"; "ToolChecks"; "ToolChecks"; "SpotChecks"; "SpotChecks"; "FAccidents"; "FAccidents"; "MileageSheets"; "MileageSheets"; "AccidentsVehicleDTOs"; "VAccidents"; "VehicleCheckDTOs"; "VehicleCheckHeaders")
	var $kind : Text
	var $count:=0
	For each ($kind; $map)
		var $list:=This.value($body; $kind)
		If (Value type($list)#Is collection)
			throw(400; $kind+" must be an array")
		End if 
		
		$count:=$count+$list.length
		$packet[This.camel($kind)]:=$list
	End for each 
	
	If ($count>1000)
		throw(400; "Send no more than 1000 checks per batch")
	End if 
	
	var $json:=JSON Stringify($packet)
	var $key:=String($account.Id)+":"+Generate digest($json; SHA256 digest)
	If (ds.GoCheckPacket.query("GoTenantId = :1 AND SubmissionKey = :2"; $account.GoTenantId; $key).length=0)
		For each ($kind; $map)
			var $record : Object
			For each ($record; $packet[This.camel($kind)])
				Case of 
					: ($kind="AccidentsVehicleDTOs")
						This.upsertCheck($account; "VAccidents"; $record.accident)
					: ($kind="VehicleCheckDTOs")
						var $header:=This.upsertCheck($account; "VehicleCheckHeaders"; $record.vehicleCheckHeader)
						var $vehicle : Object
						For each ($vehicle; $record.vehicles)
							var $row:=This.upsertCheck($account; "VehicleChecks"; $vehicle)
							$row.VehicleCheckHeaderId:=$header.Id
							This.service.save($row)
						End for each 
					Else 
						This.upsertCheck($account; $map[$kind]; $record)
				End case 
			End for each 
		End for each 
		
		var $saved:=This.service.newRow("GoCheckPacket"; $account.GoTenantId)
		$saved.AccountId:=$account.Id
		$saved.SubmissionKey:=$key
		$saved.Payload:=$json
		$saved.ReceivedAt:=Timestamp
		This.service.save($saved)
		This.queue($account; "ChecksReceived"; $key; $packet)
	End if 
	
	return This.reply(200; New object("status"; "success"))
	
	
Function validFilename($account : 4D.Entity; $name : Text) : Text
	If ((Not(Match regex("^[A-Za-z0-9_-][A-Za-z0-9_.-]{0,199}$"; $name))) | (Position(".."; $name)>0))
		throw(400; "Invalid media filename")
	End if 
	
	If ((Length($name)>=9) & Match regex("^[0-9]{8}_$"; Substring($name; 1; 9)))
		// Survey, fitting and remedial media are tied to a contract number.
		This.contract($account; Substring($name; 1; 8))
	Else 
        // Mileage signatures: dd-MM-yy-<32 hex GUID>.jpg
        // Mileage photos: dd-MM-yy-<random number>-<Agent Code>.jpg
        // Neither filename is contract-related. The agent suffix on a photo
        // identifies who captured it, not necessarily the current uploader.
        If (Match regex("^[0-9]{2}-[0-9]{2}-[0-9]{2}-[A-Fa-f0-9]{32}[.](jpg|jpeg|png|JPG|JPEG|PNG)$"; $name))
            return $name
        End if
        If (Match regex("^[0-9]{2}-[0-9]{2}-[0-9]{2}-[0-9]{1,5}-[A-Za-z0-9_-]{1,32}[.](jpg|jpeg|png|JPG|JPEG|PNG)$"; $name))
            return $name
        End if

		// Check media has a CheckID naming scheme. Log which filename
		// fails rather than incorrectly assuming a survey uses check media.
		var $agent:=This.service.row("Agent"; $account.Id; $account.GoTenantId)
		var $prefix:="-----"+$agent.Code
		$prefix:=Substring($prefix; Length($prefix)-4; 5)
		If (Position($prefix; $name)#1)
			throw(403; "Unrecognised media filename: "+$name+" (expected eight-digit contract code prefix for survey media)")
		End if 
	End if 
	
	return $name
	
	// Keep uploaded media outside the 4D database, separated by tenant ID.
	// Filenames are checked by validFilename before this function is called.
	
	
Function mediaFile($tenant : Integer; $filename : Text) : 4D.File
	var $base:=Folder(fk database folder).folder("SmartOpsMedia")
	If (Not($base.exists))
		$base.create()
	End if 
	
	var $folder:=$base.folder(String($tenant))
	If (Not($folder.exists))
		$folder.create()
	End if 
	
	return $folder.file($filename)
	
	
Function clearMediaChunks($tenant : Integer; $mediaId : Integer; $filename : Text)
	var $part : 4D.Entity
	For each ($part; ds.GoMediaChunk.query("GoTenantId = :1 AND MediaId = :2"; $tenant; $mediaId))
		var $file:=This.mediaFile($tenant; $filename+".chunk"+String($part.ChunkIndex))
		If ($file.exists)
			$file.delete()
		End if 
		
		var $dropped:=$part.drop()
		If (Not($dropped.success))
			throw(409; "Could not reset the previous media upload")
		End if 
	End for each 
	
	
Function sendMedia($account : 4D.Entity; $body : Object) : 4D.OutgoingMessage
	This.service.debugStage:="media: validating filename"
	var $filename:=This.validFilename($account; String(This.value($body; "Filename")))
	This.service.debugStage:="media: reading base64"
	var $data:=This.service.requireText(This.value($body; "Data"); "Media data"; 12000000)
	var $index:=Num(This.value($body; "ChunkIndex"))
	var $total:=Num(This.value($body; "TotalChunks"))
	If ($total=0)
		$total:=1
		$index:=1
	End if 
	
	If (($index<1) | ($index>$total) | ($total>512) | ($index#Int($index)) | ($total#Int($total)))
		throw(400; "Invalid media chunk numbering")
	End if 
	
	var $bytes : Blob
	This.service.debugStage:="media: decoding base64"
	BASE64 DECODE($data; $bytes)
	If ((BLOB size($bytes)=0) | (BLOB size($bytes)>8000000))
		throw(400; "Invalid media chunk size")
	End if 
	
	This.service.debugStage:="media: hashing blob"
	var $digest:=Generate digest($bytes; SHA256 digest)
	
	// Explicit scalar variables avoid passing entity properties as unresolved
	// values into the 4D query parameter evaluator.
	var $tenantId : Integer
	var $accountId : Integer
	var $safeFilename : Text
	$tenantId:=Num($account.GoTenantId)
	$accountId:=Num($account.Id)
	$safeFilename:=String($filename)
	This.service.debugStage:="media: lookup by tenant"
	var $matching:=ds.GoMediaFile.query("GoTenantId = :1"; $tenantId)
	This.service.debugStage:="media: lookup by filename"
	$matching:=$matching.query("Filename = :1"; $safeFilename).orderBy("Completed desc, Id desc")
	This.service.debugStage:="media: checking matching count"
	var $matchingCount : Integer
	$matchingCount:=$matching.length
	var $media : 4D.Entity
	$media:=Null
	If ($matchingCount>0)
		This.service.debugStage:="media: resolving first of "+String($matchingCount)
		$media:=$matching.first()
	Else 
		This.service.debugStage:="media: no existing file, creating"
	End if 
	
	If ($media=Null)
		If ($index#1)
			throw(409; "Start the media upload again from chunk 1")
		End if 
		
		$media:=This.service.newRow("GoMediaFile"; $account.GoTenantId)
		$media.AccountId:=$account.Id
		$media.Filename:=$filename
		$media.TotalChunks:=$total
		$media.Completed:=False
		This.service.debugStage:="media: saving metadata"
		This.service.save($media)
	End if 
	
	If ($index=1)
		// Every resend starts a fresh transfer for this company/filename.
		// Keep an existing completed file available until replacement is assembled.
		var $previous : 4D.Entity
		For each ($previous; $matching)
			This.clearMediaChunks($tenantId; $previous.Id; $filename)
			If ($previous.Id#$media.Id)
				var $dropped:=$previous.drop()
				If (Not($dropped.success))
					throw(409; "Could not replace duplicate media metadata")
				End if 
			End if 
		End for each 
		
		$media.TotalChunks:=$total
		$media.AccountId:=$accountId
		This.service.save($media)
	Else 
		If (($media.TotalChunks#$total) | ($media.AccountId#$accountId))
			throw(409; "Media upload changed; start again from chunk 1")
		End if 
		
		If (ds.GoMediaChunk.query("GoTenantId = :1 AND MediaId = :2 AND ChunkIndex = 1"; $tenantId; Num($media.Id)).length=0)
			throw(409; "Start the media upload again from chunk 1")
		End if 
	End if 
	
	// Store chunk bytes as temporary files, never as ORDA BLOB attributes.
	// 4D 21 R4 exposes entity BLOB values differently from scalar BLOBs.
	// File.getContent() returns a guaranteed 4D.Blob for writeBlob().
	This.service.debugStage:="media: saving incoming chunk to disk"
	var $partFile:=This.mediaFile($account.GoTenantId; $filename+".chunk"+String($index))
	$partFile.setContent($bytes)
	This.service.debugStage:="media: finding chunk metadata"
	var $chunkRows:=ds.GoMediaChunk.query("GoTenantId = :1 AND MediaId = :2 AND ChunkIndex = :3"; Num($account.GoTenantId); Num($media.Id); $index)
	var $chunk : 4D.Entity
	$chunk:=Null
	If ($chunkRows.length>0)
		$chunk:=$chunkRows.first()
	End if 
	
	If ($chunk=Null)
		This.service.debugStage:="media: creating chunk metadata"
		$chunk:=This.service.newRow("GoMediaChunk"; $account.GoTenantId)
		$chunk.AccountId:=$account.Id
		$chunk.MediaId:=$media.Id
		$chunk.ChunkIndex:=$index
	End if 
	
	// A repeated index replaces that part, just as a repeated filename replaces a file.
	$chunk.Digest:=$digest
	This.service.save($chunk)
	This.service.debugStage:="media: reading chunk list"
	var $chunks:=ds.GoMediaChunk.query("GoTenantId = :1 AND MediaId = :2"; Num($account.GoTenantId); Num($media.Id)).orderBy("ChunkIndex")
	If ($chunks.length=$total)
		This.service.debugStage:="media: preparing final file"
		var $file:=This.mediaFile($account.GoTenantId; $filename)
		var $temporary:=This.mediaFile($account.GoTenantId; $filename+".uploading")
		var $handle:=$temporary.open("write")
		$handle.setSize(0)
		var $part : 4D.Entity
		var $expected : Integer
		$expected:=1
		For each ($part; $chunks)
			If ($part.ChunkIndex#$expected)
				$handle:=Null
				throw(409; "Missing media chunk")
			End if 
			
			var $source:=This.mediaFile($account.GoTenantId; $filename+".chunk"+String($expected))
			If (Not($source.exists))
				$handle:=Null
				throw(409; "Media chunk file is missing")
			End if 
			
			This.service.debugStage:="media: writing chunk "+String($expected)
			$handle.writeBlob($source.getContent())
			$expected:=$expected+1
		End for each 
		
		This.service.debugStage:="media: closing file"
		$handle:=Null
		
		// Generate digest requires a scalar BLOB or text variable in 4D 21 R4;
		// File.getContent() returns a 4D.Blob object, which raises error 59.
		// For a single-part photo, the digest calculated from the decoded
		// incoming scalar BLOB is exactly the completed file digest.
		// Do not invent a whole-file hash for multipart video uploads.
		This.service.debugStage:="media: recording file checksum"
		If ($total=1)
			$media.Digest:=$digest
		Else 
			$media.Digest:=""
		End if 
		
		This.service.debugStage:="media: completing file"
		$temporary.copyTo($file.parent; $file.fullName; fk overwrite)
		This.service.debugStage:="media: saving completed metadata"
		$media.Completed:=True
		$media.ReceivedAt:=Timestamp
		This.service.save($media)
		$temporary.delete()
		This.service.debugStage:="media: removing temporary chunks"
		For each ($part; $chunks)
			var $source:=This.mediaFile($account.GoTenantId; $filename+".chunk"+String($part.ChunkIndex))
			If ($source.exists)
				$source.delete()
			End if 
			
			var $deleted:=$part.drop()
			If (Not($deleted.success))
				throw(409; "Could not clear temporary chunk metadata")
			End if 
		End for each 
	End if 
	
	return This.reply(200; New object("comments"; "Success"; "dbId"; $media.Id))
	
	
Function notes($account : 4D.Entity; $body : Object) : Object
	var $c:=This.contract($account; String(This.value($body; "ContractCode")))
	var $customer:=This.service.row("Customer"; $c.CustomerId; $account.GoTenantId)
	var $code:=String($c.ContractNumber; "00000000")
	var $note:=String(This.value($body; "Note"))
	If (Length(Trim($note))>0)
		var $n:=This.service.newRow("ContractNotes"; $account.GoTenantId)
		$n.ContractCode:=$code
		$n.Note:=This.service.requireText($note; "Note"; 4000)
		$n.AddedBy:="PDA : "+$account.Code
		$n.DateAdded:=Timestamp
		This.service.save($n)
	End if 
	
	var $notes:=New collection
	var $item : 4D.Entity
	For each ($item; ds.ContractNotes.query("GoTenantId = :1 AND ContractCode = :2"; $account.GoTenantId; $code).orderBy("Id desc"))
		$notes.push(This.dto("ContractNotes"; $item))
	End for each 
	
	return New object("id"; $c.Id; "contractId"; $c.Id; "contractCode"; $code; "name"; $customer.Name; "add1"; $customer.Add1; "add2"; String($customer.Add2); "add3"; String($customer.Add3); "postcode"; $customer.Postcode; "phone1"; $customer.Phone1; "phone2"; String($customer.Phone2); "phone3"; String($customer.Phone3); "incidentDate"; $c.IncidentDate; "causeOfDamage"; $c.CauseOfDamage; "damageDesc"; $c.DamageDescription; "instructions"; ""; "excess"; $c.Excess; "policyNumber"; $c.PolicyNumber; "contractNotes"; $notes)
	
	
// Published questions belong to the company; drafts never reach tablets.
Function questionVersion($tenant : Integer) : Integer
	var $row : 4D.Entity
	$row:=ds.TenantQuestionSets.query("TenantId = :1 AND GoTenantId = :1"; $tenant).first()
	If ($row#Null)
		return $row.Version
	End if
	return 1

Function publishedQuestions($tenant : Integer) : Object
	var $row : 4D.Entity
	$row:=ds.TenantQuestionSets.query("TenantId = :1 AND GoTenantId = :1"; $tenant).first()
	var $package : Object
	If ($row#Null)
		$package:=JSON Parse($row.QuestionsJson)
		$package.version:=$row.Version
	Else
		$package:=JSON Parse(File("/RESOURCES/dynamic-questions.json").getText())
	End if
	return $package

Function dispatch($request : 4D.IncomingMessage; $body : Object) : 4D.OutgoingMessage
	var $path:=$request.urlPath.join("/")
	If ($path="AgentLogin")
		return This.login($body)
	End if 
	
	var $account:=This.authenticate($body)
	var $tenant:=$account.GoTenantId
	Case of 
		: ($path="GetJobs")
			var $today:=Substring(Timestamp; 1; 10)
			var $tomorrow:=String(Date($today+"T00:00:00")+1; ISO date)
			$tomorrow:=Substring($tomorrow; 1; 10)
			var $jobs:=This.accessibleJobs($account).query("DiaryDate = :1 OR DiaryDate = :2"; $today; $tomorrow).orderBy("DiaryDate, GoTime")
			var $result:=New collection
			var $job : 4D.Entity
			For each ($job; $jobs)
				$result.push(This.headForJob($account; $job))
			End for each 
			
			var $response:=This.reply(200; $result)
			$response.setHeader("X-Dynamic-Questions-Version"; String(This.questionVersion($tenant)))
			return $response
		: ($path="GetDynamicQuestions")
			return This.reply(200; This.publishedQuestions($tenant))
		: ($path="SendJobs")
			return This.sendJobs($account; $body)
		: ($path="SendMedia")
			return This.sendMedia($account; $body)
		: ($path="SendChecks")
			return This.checks($account; $body)
		: ($path="GetImage")
			var $filename:=This.validFilename($account; String(This.value($body; "Filename")))
			var $media:=ds.GoMediaFile.query("GoTenantId = :1 AND Filename = :2 AND Completed = true"; $tenant; $filename).first()
			If ($media=Null)
				throw(404; "Media not found")
			End if 
			
			$response:=4D.OutgoingMessage.new()
			$response.setStatus(200)
			var $file:=This.mediaFile($tenant; $filename)
			If (Not($file.exists))
				throw(404; "Media file is missing from local storage")
			End if 
			
			$response.setBody($file.getContent())
			$response.setHeader("Content-Type"; "application/octet-stream")
			$response.setHeader("Cache-Control"; "no-store")
			return $response
		: ($path="GetVehicles")
			var $vehicles:=New collection
			var $vehicle : 4D.Entity
			For each ($vehicle; ds.Vehicles.query("GoTenantId = :1"; $tenant))
				var $v:=This.dto("Vehicles"; $vehicle)
				$v.branchCode:=""
				If ($vehicle.BranchId#Null)
					If ($vehicle.BranchId>0)
						$v.branchCode:=This.service.row("Branches"; $vehicle.BranchId; $tenant).Code
					End if 
				End if 
				
				$vehicles.push($v)
			End for each 
			
			$response:=This.reply(200; $vehicles)
			$response.setHeader("X-Dynamic-Questions-Version"; String(This.questionVersion($tenant)))
			return $response
		: ($path="GetUpdateContractNotes") | ($path="GetSpotCheckJobInfo")
			return This.reply(200; This.notes($account; $body))
		: ($path="GetAgentMessages") | ($path="GetAgentUnreadMessageCount")
			var $messages:=New collection
			var $unread:=0
			var $recipient : 4D.Entity
			For each ($recipient; ds.AgentMessageRecipients.query("GoTenantId = :1 AND AgentId = :2"; $tenant; $account.Id))
				var $message:=This.service.row("AgentMessages"; $recipient.AgentMessageId; $tenant)
				$messages.push(New object("id"; $message.Id; "subject"; $message.Subject; "bodyHtml"; $message.BodyHtml; "created"; $message.Created; "isRead"; $recipient.IsRead; "readDateTime"; $recipient.ReadDateTime))
				If (Not($recipient.IsRead))
					$unread:=$unread+1
				End if 
			End for each 
			
			If ($path="GetAgentUnreadMessageCount")
				return This.reply(200; New object("unreadCount"; $unread))
			End if 
			
			return This.reply(200; $messages)
		: ($path="MarkAgentMessageRead")
			$recipient:=ds.AgentMessageRecipients.query("GoTenantId = :1 AND AgentId = :2 AND AgentMessageId = :3"; $tenant; $account.Id; Num(This.value($body; "MessageId"))).first()
			If ($recipient=Null)
				throw(404; "Message not found")
			End if 
			
			$recipient.IsRead:=True
			$recipient.ReadDateTime:=Timestamp
			This.service.save($recipient)
			return This.reply(200; New object("status"; "success"))
		: ($path="Home/SurveyAppVersion")
			return This.appVersion($account; $body; $request.getHeader("User-Agent"))
		: ($path="SurveyEnRoute")
			var $c:=This.contract($account; String(This.value($body; "ContractCode")))
			$job:=This.accessibleJobs($account).query("ContractId = :1 AND JobType = 0 AND DiaryDate = :2"; $c.Id; Substring(String(This.value($body; "DiaryDate")); 1; 10)).first()
			If ($job=Null)
				throw(404; "Survey is no longer assigned for that date")
			End if 
			
			If ($job.EnRouteNotificationSent & (This.value($body; "Resend")#True))
				return This.reply(409; New object("reasonPhrase"; "Notification already recorded. Send it again?"; "enRoutePressedAtUtc"; $job.EnRoutePressedAtUtc))
			End if 
			
			$job.EnRouteNotificationSent:=True
			$job.EnRoutePressedAtUtc:=Timestamp
			This.service.save($job)
			This.queue($account; "TestEnRouteNotification"; String($job.Id)+":"+Timestamp; New object("contractCode"; String($c.ContractNumber; "00000000"); "jobId"; $job.Id; "at"; $job.EnRoutePressedAtUtc))
			return This.reply(200; New object("enRoutePressedAtUtc"; $job.EnRoutePressedAtUtc))
	End case 
	
	throw(404; "Unknown survey endpoint")
