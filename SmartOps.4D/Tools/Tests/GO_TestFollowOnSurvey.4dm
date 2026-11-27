// A completed later survey can retain its earlier fitting's agent and local GUID.
var $report:=New object("success";True;"tests";New collection)
GO_Seed
var $scenario : Text
For each ($scenario;New collection("recover and replay";"no fitting history";"cancelled fitting";"restricted account";"wrong time";"wrong date";"wrong customer";"unrelated agent";"ambiguous booking"))
	var $test:=New object("name";$scenario;"success";False)
	var $mobile:=cs.SmartOpsSurveyService.new()
	ds.startTransaction()
	Try
		var $account:=ds.GoAccount.query("Role = :1 AND GoAllJobs = true";"Agent").first()
		var $contract:=ds.Contract.query("GoTenantId = :1";$account.GoTenantId).first()
		var $agent:=ds.Agent.query("Code = :1";"A02").first()
		var $oldAgent:=ds.Agent.query("Code = :1";"FITTER").first()
		If ($scenario#"no fitting history")
			var $previous:=$mobile.service.newRow("Job";$account.GoTenantId)
			$previous.ContractId:=$contract.Id
			$previous.CustomerId:=$contract.CustomerId
			$previous.AgentId:=$oldAgent.Id
			$previous.JobType:=1
			$previous.DiaryDate:="2030-01-14"
			$previous.GoTime:="10:00"
			$previous.JobTime:="01:00:00"
			$previous.GoCancelled:=($scenario="cancelled fitting")
			$mobile.service.save($previous)
		End if
		var $booking:=$mobile.service.newRow("Job";$account.GoTenantId)
		$booking.ContractId:=$contract.Id
		$booking.CustomerId:=$contract.CustomerId
		$booking.AgentId:=$agent.Id
		$booking.JobType:=0
		$booking.DiaryDate:="2030-01-15"
		$booking.GoTime:="08:30"
		$booking.JobTime:="01:00:00"
		$booking.GoCancelled:=False
		$mobile.service.save($booking)
		var $packet:=$mobile.headForJob($account;$booking)
		$packet.head.agentId:=$oldAgent.Id
		$packet.head.guid:=Generate UUID
		$packet.head.bComplete:=True
		$packet.head.jobSummaryAnswersJson:="{\"kept\":true}"
		$packet.customItems:=New collection(New object("guid";Generate UUID;"bComplete";True;"surveyAnswersJson";"{\"itemKept\":true}"))
		Case of
			: ($scenario="restricted account")
				$account.GoAllJobs:=False
				$account.AgentId:=$agent.Id
			: ($scenario="wrong time")
				$packet.head.startTime:="09:30"
			: ($scenario="wrong date")
				$packet.head.diaryDate:="2030-01-16T00:00:00"
			: ($scenario="wrong customer")
				$packet.head.customerId:=99999
			: ($scenario="unrelated agent")
				$packet.head.agentId:=99999
			: ($scenario="ambiguous booking")
				var $other:=$mobile.service.newRow("Job";$account.GoTenantId)
				$other.ContractId:=$contract.Id
				$other.CustomerId:=$contract.CustomerId
				$other.AgentId:=$agent.Id
				$other.JobType:=0
				$other.DiaryDate:=$booking.DiaryDate
				$other.GoTime:=$booking.GoTime
				$other.JobTime:="01:00:00"
				$other.GoCancelled:=False
				$mobile.service.save($other)
		End case
		var $body:=New object("jobHeaders";New collection($packet))
		var $status:=200
		Try
			var $response:=$mobile.sendJobs($account;$body)
			$status:=$response.status
		Catch
			var $errors:=Last errors
			$status:=$errors[0].errCode
		End try
		If ($scenario="recover and replay")
			If ($status#200)
				throw(500;"Follow-on survey recovery failed")
			End if
			var $guid:=Uppercase(Replace string($packet.head.guid;"-";""))
			var $saved:=ds.JobHeader.query("Guid = :1";$guid).first()
			If (($saved.AgentId#$agent.Id) | ($saved.JobSummaryAnswersJson#$packet.head.jobSummaryAnswersJson) | (ds.CustomItems.query("HeaderId = :1";$saved.Id).length#1))
				throw(500;"Recovered survey answers, items or authoritative agent were lost")
			End if
			var $count:=ds.GoSurveySubmission.all().length
			$response:=$mobile.sendJobs($account;$body)
			If (($response.status#200) | (ds.GoSurveySubmission.all().length#$count))
				throw(500;"Survey retry failed or duplicated submission")
			End if
			$packet.head.report:="Changed after acceptance"
			var $rejected:=False
			Try
				$mobile.sendJobs($account;$body)
			Catch
				var $changedErrors:=Last errors
				$rejected:=($changedErrors[0].errCode=409)
			End try
			If (Not($rejected))
				throw(500;"Changed accepted submission was not rejected")
			End if
		Else
			If ($status#409)
				throw(500;"Unsafe recovery should return 409, received "+String($status))
			End if
		End if
		$test.status:=$status
		$test.success:=True
	Catch
		$test.errors:=Last errors
		$test.stage:=$mobile.service.debugStage
		$report.success:=False
	End try
	ds.cancelTransaction()
	$report.tests.push($test)
End for each
File(Folder(fk database folder).path+"follow-on-survey-test-result.json").setText(JSON Stringify($report))
QUIT 4D
