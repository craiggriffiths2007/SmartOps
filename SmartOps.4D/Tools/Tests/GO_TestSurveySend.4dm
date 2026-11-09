// Run only through test_survey_send.ps1, which supplies a separate project/data file.
var $report:=New object("success";True;"tests";New collection)
GO_Seed
var $mode : Text
For each ($mode;New collection("legacy GUID";"server GUID"))
	var $test:=New object("name";$mode;"success";False)
	var $mobile:=cs.SmartOpsSurveyService.new()
	ds.startTransaction()
	Try
		var $account:=ds.GoAccount.query("Role = :1";"Agent").first()
		var $job:=ds.Job.query("AgentId = :1";$account.AgentId).first()
		var $packet:=$mobile.headForJob($account;$job)
		If ($mode="legacy GUID")
			$packet.head.guid:=Generate UUID
		End if
		$packet.head.bComplete:=True
		$packet.head.jobSummaryAnswersJson:="{\"answer\":\"header answer\"}"
		$packet.head.jobSummaryQuestionSetVersion:=7
		$packet.head.jobSummaryQuestionsSnapshotJson:="{\"snapshot\":true}"
		$packet.customItems:=New collection
		var $i : Integer
		For ($i;1;2)
			$packet.customItems.push(New object("guid";Generate UUID;"bComplete";True;"surveyAnswersJson";"{\"answer\":\"item answer\"}";"surveyQuestionSetVersion";3;"surveyQuestionsSnapshotJson";"{\"snapshot\":true}";"item_number";$i))
		End for
		var $body:=New object("jobHeaders";New collection($packet))
		var $reply:=$mobile.sendJobs($account;$body)
		If ($reply.status#200)
			throw(500;"Submission did not return HTTP 200")
		End if
		var $guid:=Uppercase(Replace string($packet.head.guid;"-";""))
		var $header:=ds.JobHeader.query("Guid = :1";$guid).first()
		If ($header=Null)
			throw(500;"Header was not saved")
		End if
		If (($header.JobSummaryAnswersJson#$packet.head.jobSummaryAnswersJson) | ($header.JobSummaryQuestionSetVersion#7) | ($header.JobSummaryQuestionsSnapshotJson#$packet.head.jobSummaryQuestionsSnapshotJson) | Not($header.bSent))
			throw(500;"Header answers/version/snapshot/sent flag were not preserved")
		End if
		var $items:=ds.CustomItems.query("HeaderId = :1";$header.Id)
		If ($items.length#2)
			throw(500;"Expected two linked custom items")
		End if
		var $item : 4D.Entity
		For each ($item;$items)
			If (($item.SurveyAnswersJson#"{\"answer\":\"item answer\"}") | ($item.SurveyQuestionSetVersion#3) | ($item.SurveyQuestionsSnapshotJson#"{\"snapshot\":true}"))
				throw(500;"Item answers/version/snapshot were not preserved")
			End if
		End for each
		var $submissionCount:=ds.GoSurveySubmission.all().length
		var $headerCount:=ds.JobHeader.all().length
		var $itemCount:=ds.CustomItems.all().length
		var $outboxCount:=ds.GoIntegrationOutbox.all().length
		$reply:=$mobile.sendJobs($account;$body)
		If (($reply.status#200) | (ds.GoSurveySubmission.all().length#$submissionCount) | (ds.JobHeader.all().length#$headerCount) | (ds.CustomItems.all().length#$itemCount) | (ds.GoIntegrationOutbox.all().length#$outboxCount))
			throw(500;"Replay failed or created duplicate records")
		End if
		$test.exactReplay:=True
		If ($mode="legacy GUID")
			$packet.head.report:="Changed payload after acceptance"
			var $rejected:=False
			Try
				$reply:=$mobile.sendJobs($account;$body)
			Catch
				var $errors:=Last errors
				$rejected:=($errors[0].errCode=409)
			End try
			If (Not($rejected))
				throw(500;"Changed legacy replay was not rejected with 409")
			End if
			$test.changedReplayRejected:=True
		End if
		$test.success:=True
		$test.headerAnswers:=True
		$test.linkedItemAnswers:=True
	Catch
		$test.errors:=Last errors
		$test.stage:=$mobile.service.debugStage
		$report.success:=False
	End try
	ds.cancelTransaction()
	$report.tests.push($test)
End for each
// All test submissions must have been rolled back.
$report.remainingSubmissions:=ds.GoSurveySubmission.all().length
If ($report.remainingSubmissions#0)
	$report.success:=False
End if
File(Folder(fk database folder).path+"survey-test-result.json").setText(JSON Stringify($report))
QUIT 4D
