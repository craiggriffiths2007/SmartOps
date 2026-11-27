// Reproduce a survey -> fitting download retaining the previous survey agent.
var $report:=New object("success";True;"tests";New collection)
GO_Seed
var $scenario : Text
For each ($scenario;New collection("recover and replay";"no saved survey";"restricted account";"wrong time";"unrelated agent";"ambiguous booking"))
	var $test:=New object("name";$scenario;"success";False)
	var $mobile:=cs.SmartOpsSurveyService.new()
	ds.startTransaction()
	Try
		var $account:=ds.GoAccount.query("Role = :1 AND GoAllJobs = true";"Agent").first()
		var $oldAgent:=Num($account.AgentId)
		var $contract:=ds.Contract.all().first()
		var $agent:=ds.Agent.query("Code = :1";"A02").first()
		If ($scenario#"no saved survey")
			var $survey:=$mobile.service.newRow("JobHeader";$account.GoTenantId)
			$survey.ContractId:=$contract.Id
			$survey.CustomerId:=$contract.CustomerId
			$survey.AgentId:=$oldAgent
			$survey.JobType:=0
			$survey.bSent:=True
			$mobile.service.save($survey)
		End if
		var $booking:=$mobile.service.newRow("Job";$account.GoTenantId)
		$booking.ContractId:=$contract.Id
		$booking.CustomerId:=$contract.CustomerId
		$booking.AgentId:=$agent.Id
		$booking.JobType:=1
		$booking.DiaryDate:="2030-01-15"
		$booking.GoTime:="09:00"
		$booking.GoCancelled:=False
		$booking.GoVisitGuid:=Generate UUID
		$mobile.service.save($booking)
		var $packet:=$mobile.headForJob($account;$booking)
		$packet.head.agentId:=$oldAgent
		$packet.head.guid:=Generate UUID
		$packet.head.bComplete:=True
		$packet.head.fitterCompletionAnswersJson:="{\"completed\":true}"
		If ($scenario="restricted account")
			$account.GoAllJobs:=False
			$account.AgentId:=$agent.Id
		End if
		If ($scenario="wrong time")
			$packet.head.startTime:="10:00"
		End if
		If ($scenario="unrelated agent")
			$packet.head.agentId:=99999
		End if
		If ($scenario="ambiguous booking")
			var $other:=$mobile.service.newRow("Job";$account.GoTenantId)
			$other.ContractId:=$contract.Id
			$other.CustomerId:=$contract.CustomerId
			$other.AgentId:=$agent.Id
			$other.JobType:=1
			$other.DiaryDate:=$booking.DiaryDate
			$other.GoTime:=$booking.GoTime
			$other.GoCancelled:=False
			$mobile.service.save($other)
		End if
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
				throw(500;"Completed fitting recovery failed: "+String($status))
			End if
			var $guid:=Uppercase(Replace string($packet.head.guid;"-";""))
			var $saved:=ds.JobHeader.query("Guid = :1";$guid).first()
			If (($saved.AgentId#$agent.Id) | ($saved.FitterCompletionAnswersJson#$packet.head.fitterCompletionAnswersJson))
				throw(500;"Fitting agent or completed answers were not saved correctly")
			End if
			var $count:=ds.GoSurveySubmission.all().length
			$response:=$mobile.sendJobs($account;$body)
			If (($response.status#200) | (ds.GoSurveySubmission.all().length#$count))
				throw(500;"Fitting retry failed or duplicated submission")
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
File(Folder(fk database folder).path+"fitting-recovery-test-result.json").setText(JSON Stringify($report))
QUIT 4D
