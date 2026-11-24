// Transactional smoke test for direct 4D question-editor persistence.
var $report:=New object("success";False)
GO_Seed
var $service:=cs.SmartOpsService.new()
var $editor:=cs.SmartOpsQuestionEditor.new($service)
ds.startTransaction()
Try
	var $admin:=ds.GoAccount.query("Role = :1";"Admin").first()
	If ($admin=Null)
		throw(500;"No seeded administrator")
	End if
	var $tenant:=$admin.GoTenantId
	var $initial:=$editor.current($tenant)
	var $base:=$initial.published
	var $originalVersion:=$initial.version
	var $originalRevision:=$initial.draftRevision
	var $draftBody:=New object("version";$originalVersion;"draftRevision";$originalRevision;"questionsJson";JSON Stringify($base))
	$editor.handle($admin;"api/questions/draft";"POST";$draftBody)
	var $draftState:=$editor.current($tenant)
	If ((Not($draftState.hasDraft)) | ($draftState.version#$originalVersion) | ($draftState.draftRevision#$originalRevision+1))
		throw(500;"Saving a draft must not publish to the survey app")
	End if
	var $mobile:=cs.SmartOpsSurveyService.new()
	If ($mobile.questionVersion($tenant)#$originalVersion)
		throw(500;"Draft unexpectedly reached survey app")
	End if
	var $publishedBody:=New object("version";$originalVersion;"draftRevision";$draftState.draftRevision;"questionsJson";JSON Stringify($base))
	$editor.handle($admin;"api/questions/publish";"POST";$publishedBody)
	var $publishedState:=$editor.current($tenant)
	If (($publishedState.version#$originalVersion+1) | ($publishedState.hasDraft) | ($mobile.questionVersion($tenant)#$originalVersion+1))
		throw(500;"Publishing must advance device version and clear draft")
	End if
	$report.success:=True
	$report.checks:="Draft isolation; draft revision; publish increments version; mobile version updated"
Catch
	$report.errors:=Last errors
	$report.stage:=$service.debugStage
End try
ds.cancelTransaction()
File(Folder(fk database folder).path+"native-question-editor-test-result.json").setText(JSON Stringify($report))
QUIT 4D
