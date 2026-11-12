// Uses a separate database through test_survey_send.ps1.
var $report:=New object("success";False)
GO_Seed
var $mobile:=cs.SmartOpsSurveyService.new()
ds.startTransaction()
Try
	var $account:=ds.Agent.query("Code = :1";"ADMIN").first()
	var $job:=ds.Job.query("AgentId = :1";$account.Id).first()
	$job.bIncludeSurvey:=False
	$mobile.service.save($job)
	var $packet:=$mobile.headForJob($account;$job)
	$packet.head.bComplete:=True
	$packet.head.report:="Original survey report"
	var $snapshot:="{\"questions\":[{\"id\":\"old_question\",\"question\":\"Original V1 question\",\"type\":\"Text\"}]}"
	var $firstGuid:=Generate UUID
	$packet.customItems:=New collection(New object("guid";$firstGuid;"item_number";1;"questionSetKey";"Composite";"surveyQuestionSetVersion";1;"surveyAnswersJson";"{\"old_question\":\"kept\"}";"surveyQuestionsSnapshotJson";$snapshot))
	$mobile.sendJobs($account;New object("jobHeaders";New collection($packet)))
	// A later send added another item; both persisted items must be downloaded,
	// even though the latest stored payload contains only the second item.
	$packet.customItems:=New collection(New object("guid";Generate UUID;"item_number";2;"questionSetKey";"Composite";"surveyQuestionSetVersion";2;"surveyAnswersJson";"{}";"surveyQuestionsSnapshotJson";"{\"questions\":[]}"))
	$mobile.sendJobs($account;New object("jobHeaders";New collection($packet)))
	var $download:=$mobile.headForJob($account;$job)
	If (($download.head.guid#$packet.head.guid) | ($download.customItems.length#2) | ($download.head.report#"Original survey report"))
		throw(500;"Same GUID must reopen the saved header and all persisted items with Include Survey off")
	End if
	If (($download.customItems[0].surveyQuestionSetVersion#1) | ($download.customItems[0].surveyQuestionsSnapshotJson#$snapshot) | ($download.customItems[1].surveyQuestionSetVersion#2))
		throw(500;"Each item must retain its original question version and snapshot")
	End if
	If (Not(Match regex("^[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}$";$download.customItems[0].guid)))
		throw(500;"Downloaded item GUID must be compatible with System.Text.Json")
	End if
	// Submitting the returned items must update this header without duplicates.
	$download.head.bComplete:=True
	$mobile.sendJobs($account;New object("jobHeaders";New collection($download)))
	If (ds.CustomItems.all().length#2)
		throw(500;"Reopened item submission created duplicates")
	End if
	// A different day, including moving the same booking, is a fresh resurvey.
	var $oldGuid:=$download.head.guid
	$job.DiaryDate:="2030-02-02"
	$mobile.service.save($job)
	var $fresh:=$mobile.headForJob($account;$job)
	If (($fresh.head.guid=$oldGuid) | ($fresh.customItems.length#0) | $fresh.head.bSurvey)
		throw(500;"Different-day resurvey with Include Survey off must have a new GUID and no old items")
	End if
	var $again:=$mobile.headForJob($account;$job)
	If ($again.head.guid#$fresh.head.guid)
		throw(500;"Unsubmitted resurvey GUID must remain stable")
	End if
	var $original:=ds.JobHeader.query("GoTenantId = :1";$account.GoTenantId).first()
	If (($original.Report#"Original survey report") | (ds.CustomItems.query("HeaderId = :1";$original.Id).length#2))
		throw(500;"Fresh download changed historical survey data")
	End if
	// A fitting must include a saved survey even without an upload-log row.
	ds.GoSurveySubmission.query("GoTenantId = :1 AND ContractId = :2";$account.GoTenantId;$job.ContractId).drop()
	$original.DateTimeCompleted:="2030-01-01T12:00:00"
	$mobile.service.save($original)
	// A later inserted row is not necessarily the most recently completed survey.
	var $older:=$mobile.service.newRow("JobHeader";$account.GoTenantId)
	$older.ContractId:=$job.ContractId
	$older.CustomerId:=$job.CustomerId
	$older.JobType:=0
	$older.Report:="Older survey"
	$older.DateTimeCompleted:="2029-01-01T12:00:00"
	$mobile.service.save($older)
	$job.JobType:=1
	$job.bIncludeSurvey:=True
	$mobile.service.save($job)
	var $fitting:=$mobile.headForJob($account;$job)
	If (Not($fitting.head.bSurvey) | ($fitting.head.report#"Original survey report") | ($fitting.customItems.length#2))
		throw(500;"Include Survey fitting must load the saved header and all items without an upload log")
	End if
	If (($fitting.head.jobType#1) | ($fitting.head.guid#$fresh.head.guid) | ($fitting.customItems[0].surveyQuestionsSnapshotJson#$snapshot))
		throw(500;"Included survey must preserve fitting visit identity and item snapshots")
	End if
	$job.bIncludeSurvey:=False
	$mobile.service.save($job)
	var $withoutSurvey:=$mobile.headForJob($account;$job)
	If ($withoutSurvey.head.bSurvey | ($withoutSurvey.customItems.length#0))
		throw(500;"Include Survey off must not attach a different visit's survey")
	End if
	$original.reload()
	If (($original.JobType#0) | ($original.Report#"Original survey report") | (ds.CustomItems.query("HeaderId = :1";$original.Id).length#2))
		throw(500;"Fitting downloads must leave the original saved survey unchanged")
	End if
	// A saved fitting must still receive the included report, while its own
	// visit identity, fitting answers and items remain authoritative.
	var $savedFitting:=$mobile.service.newRow("JobHeader";$account.GoTenantId)
	$savedFitting.ContractId:=$job.ContractId
	$savedFitting.CustomerId:=$job.CustomerId
	$savedFitting.AgentId:=$job.AgentId
	$savedFitting.Guid:=$job.GoVisitGuid
	$savedFitting.JobType:=1
	$savedFitting.DiaryDate:=$job.DiaryDate+"T00:00:00"
	$savedFitting.Report:=""
	$savedFitting.FitComments:="Keep fitting progress"
	$mobile.service.save($savedFitting)
	var $fittingItem:=$mobile.service.newRow("CustomItems";$account.GoTenantId)
	$fittingItem.HeaderId:=$savedFitting.Id
	$fittingItem.Guid:=Generate UUID
	$fittingItem.item_number:=9
	$mobile.service.save($fittingItem)
	$job.bIncludeSurvey:=True
	$mobile.service.save($job)
	var $reopenedFitting:=$mobile.headForJob($account;$job)
	If (Not($reopenedFitting.head.bSurvey) | ($reopenedFitting.head.report#"Original survey report"))
		throw(500;"Saved fitting with Include Survey must receive the latest survey report")
	End if
	If (($reopenedFitting.head.guid#$fitting.head.guid) | ($reopenedFitting.head.fitComments#"Keep fitting progress") | ($reopenedFitting.customItems.length#1) | ($reopenedFitting.customItems[0].item_number#9))
		throw(500;"Including a report must preserve saved fitting identity, progress and items")
	End if
	$savedFitting.reload()
	If ($savedFitting.Report#"")
		throw(500;"Including a report during download must not modify the saved fitting")
	End if
	$original.Report:="Updated survey report"
	$mobile.service.save($original)
	$reopenedFitting:=$mobile.headForJob($account;$job)
	If ($reopenedFitting.head.report#"Updated survey report")
		throw(500;"Repeat fitting download must refresh the included survey report")
	End if
	$job.bIncludeSurvey:=False
	$mobile.service.save($job)
	$reopenedFitting:=$mobile.headForJob($account;$job)
	If (($reopenedFitting.head.report#"") | ($reopenedFitting.customItems.length#1))
		throw(500;"Include Survey off must preserve the saved fitting report and items")
	End if
	$report.success:=True
	$report.checks:="Same-GUID reopen; all persisted V1/V2 items; snapshots; wire GUIDs; duplicate-free resend; fresh resurvey; stable visit GUID; fitting includes latest saved survey without upload log; Include Survey off; historical data retained; saved fitting includes and refreshes survey report without changing fitting identity, progress, items or stored report"
Catch
	$report.errors:=Last errors
	$report.stage:=$mobile.service.debugStage
End try
ds.cancelTransaction()
File(Folder(fk database folder).path+"survey-reopen-test-result.json").setText(JSON Stringify($report))
QUIT 4D
