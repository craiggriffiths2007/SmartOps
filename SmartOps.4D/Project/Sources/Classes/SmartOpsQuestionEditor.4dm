// Native 4D persistence for the browser questionnaire editor.
// The published JSON remains authoritative for SmartOps.Survey.
property service : cs.SmartOpsService

Class constructor($service : cs.SmartOpsService)
	This.service:=$service

Function current($tenant : Integer) : Object
	var $row:=ds.TenantQuestionSets.query("GoTenantId = :1"; $tenant).first()
	var $published:=JSON Parse(File("/RESOURCES/dynamic-questions.json").getText())
	var $draft:=Null
	var $version:=1
	var $revision:=0
	If ($row#Null)
		$published:=JSON Parse($row.QuestionsJson)
		$version:=$row.Version
		$revision:=$row.DraftRevision
		If (($row.DraftQuestionsJson#Null) & (Length(String($row.DraftQuestionsJson))>0))
			$draft:=JSON Parse($row.DraftQuestionsJson)
		End if
	End if
	$published.version:=$version
	If ($draft#Null)
		$draft.version:=$version
	End if
	return New object("version";$version;"draftRevision";$revision;"published";$published;"draft";$draft;"hasDraft";$draft#Null)

Function checkedPackage($body : Object) : Object
	If (Value type($body.questionsJson)#Is text)
		throw(400;"Question JSON is required")
	End if
	If ((Length($body.questionsJson)=0) | (Length($body.questionsJson)>1000000))
		throw(400;"Question JSON must be between 1 and 1000000 characters")
	End if
	var $package:=JSON Parse($body.questionsJson)
	If ((Value type($package)#Is object) | (Value type($package.sets)#Is object))
		throw(400;"Invalid question set")
	End if
	var $key : Text
	For each ($key; $package.sets)
		var $definition:=$package.sets[$key]
		If (Value type($definition.questions)#Is collection)
			throw(400;"Missing questions array in "+$key)
		End if
		var $seen:=New collection
		var $q : Object
		For each ($q; $definition.questions)
			If ((Value type($q.id)#Is text) | (Value type($q.question)#Is text))
				throw(400;"Question ID and text are required in "+$key)
			End if
			If ((Not(Match regex("^[A-Za-z0-9_-]{1,100}$";$q.id))) | (Length(Trim($q.question))=0))
				throw(400;"Invalid question ID or label in "+$key)
			End if
			If ($seen.includes($q.id))
				throw(400;"Duplicate question ID in "+$key)
			End if
			$seen.push($q.id)
		End for each
	End for each
	return $package

Function handle($account : 4D.Entity; $path : Text; $verb : Text; $body : Object) : 4D.OutgoingMessage
	This.service.requireRole($account;"Admin,Staff")
	var $tenant:=$account.GoTenantId
	var $state:=This.current($tenant)
	If (($path="api/questions/editor") & ($verb="GET"))
		return This.service.reply(200;$state)
	End if
	If (($path="api/questions/validate") & ($verb="POST"))
		return This.service.reply(200;This.checkedPackage($body))
	End if
	If ($verb#"POST")
		throw(404;"Unknown question editor endpoint")
	End if
	var $expected:=Num($body.version)
	var $revision:=Num($body.draftRevision)
	If (($expected#$state.version) | ($revision#$state.draftRevision))
		return This.service.reply(409;New object("error";"Another editor updated these questions. Export your changes and reload."))
	End if
	var $row:=ds.TenantQuestionSets.query("GoTenantId = :1";$tenant).first()
	If (($path="api/questions/draft") | ($path="api/questions/publish"))
		var $package:=This.checkedPackage($body)
		$package.version:=Choose($path="api/questions/publish";$state.version+1;$state.version)
		If ($row=Null)
			$row:=This.service.newRow("TenantQuestionSets";$tenant)
			$row.TenantId:=$tenant
			$row.Version:=1
			$row.QuestionsJson:=JSON Stringify($state.published)
		End if
		If ($path="api/questions/draft")
			$row.DraftQuestionsJson:=JSON Stringify($package)
			$row.DraftSavedUtc:=Timestamp
			$row.DraftRevision:=$revision+1
			This.service.save($row)
			return This.service.reply(200;New object("revision";$row.DraftRevision;"savedAtUtc";Timestamp))
		End if
		$row.QuestionsJson:=JSON Stringify($package)
		$row.Version:=$package.version
		$row.DraftQuestionsJson:=Null
		$row.DraftSavedUtc:=Null
		$row.DraftRevision:=$revision+1
		$row.UpdatedUtc:=Timestamp
		This.service.save($row)
		return This.service.reply(200;New object("version";$row.Version))
	End if
	If ($path="api/questions/discard")
		If ($row#Null)
			$row.DraftQuestionsJson:=Null
			$row.DraftSavedUtc:=Null
			$row.DraftRevision:=$revision+1
			This.service.save($row)
		End if
		return This.service.reply(200;New object("discarded";True))
	End if
	If ($path="api/questions/reset")
		var $defaults:=JSON Parse(File("/RESOURCES/dynamic-questions.json").getText())
		$defaults.version:=1
		If ($row=Null)
			$row:=This.service.newRow("TenantQuestionSets";$tenant)
			$row.TenantId:=$tenant
		End if
		$row.QuestionsJson:=JSON Stringify($defaults)
		$row.Version:=1
		$row.DraftQuestionsJson:=Null
		$row.DraftSavedUtc:=Null
		$row.DraftRevision:=$revision+1
		$row.UpdatedUtc:=Timestamp
		This.service.save($row)
		return This.service.reply(200;New object("reset";True))
	End if
	throw(404;"Unknown question editor endpoint")
