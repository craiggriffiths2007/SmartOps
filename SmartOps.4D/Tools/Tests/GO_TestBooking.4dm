// Isolated native tests. test_survey_send.ps1 -Suite Booking supplies new data.
var $report:=New object("success";True;"tests";New collection)
GO_Seed
var $cases:=New collection
$cases.push(New object("time";"09:00";"minutes";60;"expected";200))
$cases.push(New object("time";"00:00";"minutes";15;"expected";200))
$cases.push(New object("time";"23:45";"minutes";15;"expected";200))
$cases.push(New object("time";"24:00";"minutes";60;"expected";400))
$cases.push(New object("time";"09:60";"minutes";60;"expected";400))
$cases.push(New object("time";"9:00";"minutes";60;"expected";400))
$cases.push(New object("time";"ab:cd";"minutes";60;"expected";400))
$cases.push(New object("time";"09:0";"minutes";60;"expected";400))
$cases.push(New object("time";"09:00";"minutes";14;"expected";400))
$cases.push(New object("time";"09:00";"minutes";481;"expected";400))
$cases.push(New object("time";"09:00";"minutes";60.5;"expected";400))
$cases.push(New object("time";"23:46";"minutes";15;"expected";400))
$cases.push(New object("time";"09:00";"minutes";60;"expected";409;"unauthorised";True))
$cases.push(New object("time";"09:00";"minutes";60;"expected";409;"clash";True))
var $case : Object
For each ($case;$cases)
	var $service:=cs.SmartOpsService.new()
	var $test:=New object("time";$case.time;"minutes";$case.minutes;"expected";$case.expected)
	var $status:=200
	ds.startTransaction()
	Try
		var $account:=ds.GoAccount.query("Role = :1";"Admin").first()
		var $contract:=ds.Contract.all().first()
		$contract.Status:=60
		If ($case.unauthorised=True)
			$contract.Status:=59
		End if
		$service.save($contract)
		var $agent:=ds.Agent.all().first()
		var $date:="2030-01-15"
		If ($case.clash=True)
			var $conflict:=$service.newRow("Job";$account.GoTenantId)
			$conflict.AgentId:=$agent.Id
			$conflict.DiaryDate:=$date
			$conflict.GoStartMinute:=540
			$conflict.GoEndMinute:=600
			$conflict.GoCancelled:=False
			$service.save($conflict)
		End if
		var $body:=New object("ContractId";$contract.Id;"AgentId";$agent.Id;"DiaryDate";$date;"Time";$case.time;"DurationMinutes";$case.minutes;"JobType";1;"Instructions";"Instruction")
		var $result:=$service.book($account;$body)
		If (($result.Time#$case.time) | ($result.JobType#1) | ($result.GoEndMinute-$result.GoStartMinute#$case.minutes))
			throw(500;"Saved fitting booking differs from request")
		End if
		$test.startMinute:=$result.GoStartMinute
		$test.endMinute:=$result.GoEndMinute
	Catch
		var $errors:=Last errors
		$status:=$errors[0].errCode
		$test.message:=$errors[0].message
		$test.stage:=$service.debugStage
	End try
	ds.cancelTransaction()
	$test.actual:=$status
	$test.success:=($status=$case.expected)
	If (Not($test.success))
		$report.success:=False
	End if
	$report.tests.push($test)
End for each
File(Folder(fk database folder).path+"booking-test-result.json").setText(JSON Stringify($report))
QUIT 4D
