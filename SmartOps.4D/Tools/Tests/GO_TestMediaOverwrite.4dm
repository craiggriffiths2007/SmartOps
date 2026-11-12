// Real disk files and fresh test data only; never use the live data/media directory.
var $report:=New object("success";False;"steps";New collection)
GO_Seed
var $mobile:=cs.SmartOpsSurveyService.new()
Try
	var $account:=ds.GoAccount.query("Role = :1 AND GoAllJobs = true";"Agent").first()
	var $contract:=ds.Contract.query("GoTenantId = :1";$account.GoTenantId).first()
	var $filename:=String($contract.ContractNumber;"00000000")+"_overwrite-test.jpg"
	var $steps:=New collection
	$steps.push(New object("name";"initial photo";"data";"b2xk";"index";0;"total";0;"expected";"old"))
	$steps.push(New object("name";"replace photo";"data";"bmV3";"index";0;"total";0;"expected";"new"))
	$steps.push(New object("name";"repeat photo";"data";"bmV3";"index";0;"total";0;"expected";"new"))
	$steps.push(New object("name";"multipart starts, old file stays";"data";"QQ==";"index";1;"total";3;"expected";"new"))
	$steps.push(New object("name";"multipart continues";"data";"Qg==";"index";2;"total";3;"expected";"new"))
	$steps.push(New object("name";"multipart completes";"data";"Qw==";"index";3;"total";3;"expected";"ABC"))
	$steps.push(New object("name";"replace with different chunk count";"data";"WA==";"index";1;"total";2;"expected";"ABC"))
	$steps.push(New object("name";"shorter replacement completes";"data";"WQ==";"index";2;"total";2;"expected";"XY"))
	$steps.push(New object("name";"interrupted transfer starts";"data";"QQ==";"index";1;"total";3;"expected";"XY"))
	$steps.push(New object("name";"interrupted transfer part two";"data";"Qg==";"index";2;"total";3;"expected";"XY"))
	$steps.push(New object("name";"restart clears stale parts";"data";"WA==";"index";1;"total";2;"expected";"XY"))
	$steps.push(New object("name";"restart completes without mixing";"data";"Wg==";"index";2;"total";2;"expected";"XZ"))
	$steps.push(New object("name";"chunk replacement starts";"data";"QQ==";"index";1;"total";3;"expected";"XZ"))
	$steps.push(New object("name";"chunk original bytes";"data";"Qg==";"index";2;"total";3;"expected";"XZ"))
	$steps.push(New object("name";"changed chunk overwrites";"data";"WA==";"index";2;"total";3;"expected";"XZ"))
	$steps.push(New object("name";"changed chunks assemble correctly";"data";"Qw==";"index";3;"total";3;"expected";"AXC"))
	var $step : Object
	For each ($step;$steps)
		var $body:=New object("Filename";$filename;"Data";$step.data;"ChunkIndex";$step.index;"TotalChunks";$step.total)
		var $reply:=$mobile.sendMedia($account;$body)
		var $file:=$mobile.mediaFile($account.GoTenantId;$filename)
		If (($reply.status#200) | ($file.getText()#$step.expected))
			throw(500;"Wrong file contents after "+$step.name)
		End if
		If (ds.GoMediaFile.query("GoTenantId = :1 AND Filename = :2";$account.GoTenantId;$filename).length#1)
			throw(500;"Duplicate file metadata after "+$step.name)
		End if
		$report.steps.push($step.name)
	End for each
	If (ds.GoMediaChunk.all().length#0)
		throw(500;"Completed upload left chunk metadata behind")
	End if
	var $rejected:=False
	Try
		$mobile.sendMedia($account;New object("Filename";$filename;"Data";"Qw==";"ChunkIndex";3;"TotalChunks";3))
	Catch
		var $errors:=Last errors
		$rejected:=($errors[0].errCode=409)
	End try
	If (Not($rejected))
		throw(500;"A tail chunk without a fresh first chunk was accepted")
	End if
	// A second authorized account writes the same company/filename, not a duplicate.
	$mobile.mediaFile($account.GoTenantId;$filename+".uploading").setText("A larger abandoned assembly must not leave trailing bytes")
	var $second:=ds.GoAccount.query("Role = :1 AND GoAllJobs = false";"Agent").first()
	$second.GoAllJobs:=True
	$mobile.service.save($second)
	var $duplicate:=$mobile.service.newRow("GoMediaFile";$account.GoTenantId)
	$duplicate.AccountId:=$second.Id
	$duplicate.Filename:=$filename
	$duplicate.TotalChunks:=1
	$duplicate.Completed:=False
	$mobile.service.save($duplicate)
	$mobile.sendMedia($second;New object("Filename";$filename;"Data";"Wg=="))
	If ((ds.GoMediaFile.query("GoTenantId = :1 AND Filename = :2";$account.GoTenantId;$filename).length#1) | ($file.getText()#"Z"))
		throw(500;"Second account did not replace the same company file")
	End if
	// The same filename in another company must resolve to a separate disk file.
	var $otherAccount:=$mobile.service.newRow("GoAccount";999)
	$otherAccount.GoAllJobs:=True
	$mobile.service.save($otherAccount)
	var $otherContract:=$mobile.service.newRow("Contract";999)
	$otherContract.ContractNumber:=$contract.ContractNumber
	$mobile.service.save($otherContract)
	var $otherJob:=$mobile.service.newRow("Job";999)
	$otherJob.ContractId:=$otherContract.Id
	$otherJob.DiaryDate:="2030-01-15"
	$otherJob.GoTime:="08:30"
	$otherJob.JobTime:="01:00:00"
	$otherJob.GoCancelled:=False
	$mobile.service.save($otherJob)
	$mobile.sendMedia($otherAccount;New object("Filename";$filename;"Data";"QQ=="))
	If (($file.getText()#"Z") | ($mobile.mediaFile(999;$filename).getText()#"A"))
		throw(500;"Media overwrite crossed company boundaries")
	End if
	$report.success:=True
	$report.additionalChecks:="Tail without start rejected; metadata duplicates consolidated; another agent overwrites same company file; company isolation"
Catch
	$report.errors:=Last errors
	$report.stage:=$mobile.service.debugStage
End try
File(Folder(fk database folder).path+"media-overwrite-test-result.json").setText(JSON Stringify($report))
QUIT 4D
