var $report:=New object("success";False)
GO_Seed
var $service:=cs.SmartOpsService.new()
ds.startTransaction()
Try
	var $admin:=ds.GoAccount.query("Role = :1";"Admin").first()
	var $mobileAdmin:=ds.GoAccount.query("Username = :1";"agent:admin").first()
	var $contract:=ds.Contract.query("GoTenantId = :1";$admin.GoTenantId).first()
	var $agent:=ds.Agent.query("GoTenantId = :1 AND Code = :2";$admin.GoTenantId;"A02").first()
	var $today:=Substring(Timestamp;1;10)
	var $bridge:=cs.SmartOpsWebBridge.new($service)
	var $values:=New object("ContractId";$contract.Id;"CustomerId";$contract.CustomerId;"AgentId";$agent.Id;"ContractCode";String($contract.ContractNumber;"00000000");"JobType";0;"DiaryDate";$today+" 00:00:00";"Time";$today+" 08:30:00";"JobTime";"01:00:00")
	var $change:=New object("table";"Job";"key";"-1";"recordId";-1;"operation";"add";"revision";0;"values";$values;"references";New object)
	var $result:=$bridge.commit($admin;New object("changes";New collection($change)))
	var $id:=$result.ids["Job:-1"]
	var $job:=ds.Job.get($id)
	$report.saved:=New object("date";$job.DiaryDate;"time";$job.GoTime;"start";$job.GoStartMinute;"end";$job.GoEndMinute)
	If (($job.DiaryDate#$today) | ($job.GoTime#"08:30") | ($job.GoStartMinute#510) | ($job.GoEndMinute#570))
		throw(500;"Web booking was not normalized")
	End if
	// Reproduce the exact shape of an existing booking made before the fix.
	$job.DiaryDate:=$today+" 00:00:00"
	$job.GoTime:=$today+" 08:30:00"
	$job.GoStartMinute:=0
	$job.GoEndMinute:=0
	$service.save($job)
	var $mobile:=cs.SmartOpsSurveyService.new()
	var $jobs:=$mobile.accessibleJobs($mobileAdmin).query("DiaryDate = :1 AND Id = :2";$today;$id)
	If ($jobs.length#1)
		throw(500;"ADMIN cannot download the existing web booking assigned to A02")
	End if
	var $packet:=$mobile.headForJob($mobileAdmin;$jobs.first())
	var $wireDate:=$today+"T00:00:00"
	If (($packet.head.diaryDate#$wireDate) | ($packet.head.startTime#"08:30") | ($packet.head.finishTime#"09:30") | ($packet.head.agentId#$agent.Id))
		throw(500;"Mobile booking date, time or assignment is incorrect")
	End if
	$report.success:=True
	$report.checks:="Web-created booking; existing booking repair; ADMIN access to A02; mobile date; 08:30-09:30 times"
Catch
	$report.errors:=Last errors
	$report.stage:=$service.debugStage
End try
ds.cancelTransaction()
File(Folder(fk database folder).path+"web-booking-test-result.json").setText(JSON Stringify($report))
QUIT 4D
