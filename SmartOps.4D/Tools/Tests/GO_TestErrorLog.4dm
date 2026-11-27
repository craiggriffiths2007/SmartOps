var $report:=New object("success";False)
var $service:=cs.SmartOpsService.new()
Try
	$service.logTenantId:=123
	$service.debugStage:="test: reservation save"
	var $response:=$service.failure(New collection(New object("errCode";409;"message";"Record changed"));"api/web/commit";"POST")
	If ($response.status#409)
		throw(500;"Expected conflict response was lost")
	End if
	$service.logTenantId:=456
	$response:=$service.failure(New collection(New object("errCode";-10729;"message";"Unknown member method."));"GetJobs";"POST")
	If ($response.status#500)
		throw(500;"Unexpected error did not return HTTP 500")
	End if
	var $log:=cs.SmartOpsErrorLog.new()
	var $first:=$log.read(123)
	var $second:=$log.read(456)
	If (($first.entries.length#1) | ($second.entries.length#1))
		throw(500;"Log entries are missing or escaped their company")
	End if
	var $entry:=$first.entries[0]
	If (($entry.stage#"test: reservation save") | ($entry.path#"api/web/commit") | ($entry.status#409) | ($entry.errors[0].errCode#409))
		throw(500;"Log details were not retained")
	End if
	$service.logTenantId:=123
	$response:=$service.failure(New collection(New object("errCode";400;"message";"Invalid date"));"api/diary/book";"POST")
	$first:=$log.read(123)
	If (($first.entries.length#2) | ($first.entries[0].errors[0].errCode#400))
		throw(500;"Log append or newest-first order failed")
	End if
	If (OB Is defined($entry;"body") | OB Is defined($entry;"headers"))
		throw(500;"Log unexpectedly contains request bodies or credentials")
	End if
	$report.success:=True
	$report.checks:="Native file writes; append; newest first; company isolation; 409 and 500 responses; error details; no request bodies"
	$report.logsFolder:=Folder(fk logs folder).path
Catch
	$report.errors:=Last errors
End try
File(Folder(fk database folder).path+"error-log-test-result.json").setText(JSON Stringify($report))
QUIT 4D
