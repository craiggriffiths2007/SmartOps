var $report:=New object("success";False)
GO_Seed
var $service:=cs.SmartOpsService.new()
ds.startTransaction()
Try
	var $admin:=ds.GoAccount.query("Role = :1";"Admin").first()
	var $bridge:=cs.SmartOpsWebBridge.new($service)
	var $tenant:=$admin.GoTenantId
	var $mobile:=cs.SmartOpsSurveyService.new()
	var $defaults:=File("/RESOURCES/dynamic-questions.json").getText()
	If (($mobile.questionVersion($tenant)#1) | ($mobile.publishedQuestions($tenant).version#1))
		throw(500;"Company without custom questions must receive V1 defaults")
	End if
	var $draft:=JSON Stringify(New object("version";1;"sets";New object("Composite";New object("questions";New collection(New object("id";"christmas_decoration";"question";"Christmas decoration";"type";"Picker";"options";New collection("Christmas wreaths";"Christmas door swags";"Giant Christmas bows")))))))
	var $values:=New object("TenantId";$tenant;"QuestionsJson";$defaults;"Version";1;"DraftQuestionsJson";$draft;"DraftRevision";1;"DraftSavedUtc";Timestamp;"UpdatedUtc";Timestamp)
	var $change:=New object("table";"TenantQuestionSets";"key";String($tenant);"recordId";$tenant;"operation";"add";"revision";0;"values";$values;"references";New object)
	var $result:=$bridge.commit($admin;New object("changes";New collection($change)))
	If ($result.ids["TenantQuestionSets:"+String($tenant)]#$tenant)
		throw(500;"Question set key must be the company ID")
	End if
	var $row : 4D.Entity
	$row:=$service.row("TenantQuestionSets";$tenant;$tenant)
	If (($row.DraftQuestionsJson#$draft) | ($row.DraftRevision#1) | ($row.Version#1))
		throw(500;"New question draft was not saved")
	End if
	If (JSON Stringify($mobile.publishedQuestions($tenant))#JSON Stringify(JSON Parse($defaults)))
		throw(500;"Unpublished draft must not reach tablets")
	End if
	var $snapshot:=$bridge.snapshot($admin)
	If ($snapshot.revisions.TenantQuestionSets[String($tenant)]#$row.GoRevision)
		throw(500;"Snapshot revision must use TenantId")
	End if
	$change.operation:="update"
	$change.revision:=$row.GoRevision
	$change.values:=New object("DraftQuestionsJson";$draft;"DraftRevision";2)
	$bridge.commit($admin;New object("changes";New collection($change)))
	$row:=$service.row("TenantQuestionSets";$tenant;$tenant)
	If ($row.DraftRevision#2)
		throw(500;"Existing question draft was not updated")
	End if
	$change.revision:=$row.GoRevision
	$change.values:=New object("QuestionsJson";$draft;"Version";2;"DraftQuestionsJson";Null;"DraftRevision";3)
	$bridge.commit($admin;New object("changes";New collection($change)))
	$row:=$service.row("TenantQuestionSets";$tenant;$tenant)
	If (($row.QuestionsJson#$draft) | ($row.Version#2) | ($row.DraftQuestionsJson#Null))
		throw(500;"Question draft was not published")
	End if
	var $download:=$mobile.publishedQuestions($tenant)
	If (($mobile.questionVersion($tenant)#2) | ($download.version#2) | ($download.sets.Composite.questions[0].question#"Christmas decoration"))
		throw(500;"Tablet download must receive published V2 and Christmas decoration")
	End if
	If (($mobile.questionVersion(999)#1) | ($mobile.publishedQuestions(999).version#1))
		throw(500;"Another company must retain its own default questions")
	End if
	$row.DraftQuestionsJson:="{\"version\":3,\"sets\":{}}"
	$service.save($row)
	$download:=$mobile.publishedQuestions($tenant)
	If (($download.version#2) | ($download.sets.Composite.questions[0].question#"Christmas decoration"))
		throw(500;"Future draft must not replace published V2 on tablets")
	End if
	$report.success:=True
	$report.checks:="Create draft; company key; snapshot revision; update; publish; tablet V2 and Christmas decoration; defaults; draft exclusion; company isolation"
Catch
	$report.errors:=Last errors
	$report.stage:=$service.debugStage
End try
ds.cancelTransaction()
File(Folder(fk database folder).path+"question-draft-test-result.json").setText(JSON Stringify($report))
QUIT 4D
