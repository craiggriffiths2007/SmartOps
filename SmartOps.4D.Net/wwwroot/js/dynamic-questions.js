(() => {
    'use strict';
    const packageData = JSON.parse(document.getElementById('questionSetData').textContent);
    const defaults = JSON.parse(document.getElementById('questionDefaults').textContent);
    let keys = Object.keys(packageData.sets).sort((a,b) => a.localeCompare(b));
    const nonItemKeys = new Set(window.questionNonItemKeys.map(k => k.toLowerCase()));
    const isShared = key => ['header','footer','healthandsafety','specialrequirements','jobsummary','repudiation','garagedoormotordeclaration','fittercompletion','fitterstockusage','remedialcompletion'].includes(key?.toLowerCase());
    const isSurveyHeaderFooter = key => ['header','footer'].includes(key?.toLowerCase());
    const hasProtectedQuestions = isSurveyHeaderFooter;
    const isItem = key => !nonItemKeys.has(key.toLowerCase());
    const types = ['Text','YesNo','Picker','Measurement','PlusMinus','ButtonPicker','Date','Image','Information','ReplaceGlass','ReplacePanel','ReplaceLocking','Handles'];
    const workflowTypes = ['ReplaceGlass','ReplacePanel','ReplaceLocking','Handles'];
    const isYesNo = q => q?.type === 'YesNo' || workflowTypes.includes(q?.type);
    const isFlexibleGroup = () => ['itemassembly','garage','bifolding','composite','conservatory'].includes(currentKey.toLowerCase()) || !Object.keys(defaults.sets).some(k => k.toLowerCase() === currentKey.toLowerCase());
    const supportsWorkflow = type => isItem(currentKey) && !['toolcheck','salescar','fittervan','deliveryvan','deliveryhgv'].includes(currentKey.toLowerCase()) && (type === 'Handles' || isFlexibleGroup() || ['itemassembly'].includes(currentKey.toLowerCase()));
    const workflowLabels = {PlusMinus:'Plus Minus',ReplaceGlass:'Replace glass', ReplacePanel:'Replace panel', ReplaceLocking:'Replace locking', Handles:'Handles'};
    const operators = ['Equals','NotEquals','IsBlank','IsNotBlank','NotBlankOrNone','GreaterThan','Contains','In'];
    const contexts = [
        ['$has_garage_items','Job contains garage items (True / False)'],
        ['$mandate_signed','Fitter mandate signed (True / False)'],
        ['$inherited_material','Frame material inherited from parent (True / False)'],
        ['$material','Material (UPVC / Aluminium / Timber)'],
        ['$opening_type','Opening type'],
        ['$repair','Repair (True / False)'],
        ['$fensa','FENSA (True / False)'],
        ['$base_item','Glass parent item'],
        ['$bifold_doors','Bifold number of doors'],
        ['$conservatory_roof','Conservatory roof type'],
        ['$conservatory_wall','Conservatory wall position'],
        ['$garage_opening','Garage opening type'],
        ['$replace_glass','Replace glass (True / False)'],
        ['$glass_stepped_allowed','Stepped glass allowed (True / False)'],
        ['$damage_different','Cause differs from contract (True / False)'],
        ['$security_required','Security survey required (True / False)'],
        ['$sub_item','Sub-item (True / False)'],
        ['$closing_questions','Show Header and Footer questions (True / False)']
    ];
    const $ = id => document.getElementById(id);
    const elt = (tag, text, className) => {
        const e = document.createElement(tag);
        if (text !== undefined) e.textContent = text;
        if (className) e.className = className;
        return e;
    };
    const splitLines = value => (value || '').split(/\r?\n/).map(s => s.trim()).filter(Boolean);
    const optionsFor = q => isYesNo(q) ? ['Yes','No'] : (q?.options || []);
    const questions = () => packageData.sets[currentKey]?.questions || [];
    const previewQuestions = () => {
        const definition = packageData.sets[currentKey];
        if (!definition || !isItem(currentKey)) return questions();
        return [
            ...(definition.includeHeaderQuestions !== false ? packageData.sets.Header.questions : []),
            ...questions(),
            ...(definition.includeFooterQuestions !== false ? packageData.sets.Footer.questions : [])
        ];
    };
    const referenceQuestions = () => isShared(currentKey) && ['header','footer'].includes(currentKey.toLowerCase())
        ? [...packageData.sets.Header.questions, ...packageData.sets.Footer.questions] : previewQuestions();
    let currentArea = 'Survey';
    const categoryLabel = key => key.toLowerCase() === 'fittercompletion' ? 'Fitter Completion' : key.toLowerCase() === 'fitterstockusage' ? 'Fitter Stock Usage' : key.toLowerCase() === 'remedialcompletion' ? 'Remedial Completion' : key.toLowerCase() === 'itemassembly' ? 'Assembly' : key.toLowerCase() === 'healthandsafety' ? 'Health and Safety' : key.toLowerCase() === 'specialrequirements' ? 'Special Requirements' : key.toLowerCase() === 'jobsummary' ? 'Job Summary' : key.toLowerCase() === 'garagedoormotordeclaration' ? 'Garage Door Motor Declaration' : key;
    const areaKeys = () => keys.filter(key => currentArea === 'SurveyHeaderFooter' ? isSurveyHeaderFooter(key)
        : currentArea === 'Shared' ? isShared(key) && !isSurveyHeaderFooter(key)
        : currentArea === 'Survey' ? isItem(key) : !isItem(key) && !isShared(key))
        .sort((a, b) => categoryLabel(a).localeCompare(categoryLabel(b)));
    const selectedCategories = {};
    let currentKey = areaKeys()[0], currentIndex = 0, dirty = false;
    let editorQuestion = null;
    let previewAnswers = {};
    let previewContext = {'$material':'UPVC','$opening_type':'Door','$repair':'False','$fensa':'True','$base_item':'none','$bifold_doors':'3','$conservatory_roof':'Edwardian','$conservatory_wall':'None','$garage_opening':'Canopy','$replace_glass':'False','$damage_different':'False','$security_required':'False','$sub_item':'False','$closing_questions':'True'};
    let editSerial = 0;
    let autoSaveTimer = null;
    let savePromise = null;
    let autosaveDisabled = false;
    let hasSavedDraft = !$('discardDraft').hidden;
    const markDirty = () => {
        dirty = true;
        editSerial++;
        window.questionEditorDirty = true;
        $('dirtyBadge').hidden = false;
        $('discardDraft').hidden = false;
        if (autoSaveTimer) clearTimeout(autoSaveTimer);
        if (!autosaveDisabled) autoSaveTimer = setTimeout(() => saveDraft(false), 25000);
    };

    function makeSelect(entries, value, className='form-select') {
        const s = elt('select'); s.className = className;
        entries.forEach(([val,label]) => { const o = elt('option',label); o.value = val; s.append(o); });
        s.value = value ?? '';
        return s;
    }
    function field(label,id,type='text',value='') {
        const wrap=elt('div',undefined,'mb-3');
        const lbl=elt('label',label,'form-label'); lbl.htmlFor=id;
        const input=elt(type==='textarea'?'textarea':'input'); input.id=id;
        if(type!=='textarea') input.type=type;
        input.className='form-control'; input.value=value??'';
        wrap.append(lbl,input); return wrap;
    }
    function selectField(label,id,values,value) {
        const wrap=elt('div',undefined,'mb-3');
        const lbl=elt('label',label,'form-label'); lbl.htmlFor=id;
        const s=makeSelect(values,value); s.id=id; wrap.append(lbl,s); return wrap;
    }
    function renderCategories() {
        const visibleKeys = areaKeys();
        if (!visibleKeys.includes(currentKey)) {
            currentKey = visibleKeys.includes(selectedCategories[currentArea])
                ? selectedCategories[currentArea] : visibleKeys[0];
            currentIndex = 0;
            previewAnswers = {};
        }
        selectedCategories[currentArea] = currentKey;
        $('categoryArea').value = currentArea;
        const s=$('setChooser'); s.replaceChildren();
        visibleKeys.forEach(key=>{const o=elt('option',`${categoryLabel(key)} (${packageData.sets[key].questions.length})`);o.value=key;s.append(o);});
        if (!visibleKeys.length) {
            const option = elt('option', 'No question categories'); option.value = ''; s.append(option);
        }
        s.value=currentKey || '';
        s.disabled = !currentKey;
        $('removeItemType').disabled = !currentKey || !isItem(currentKey);
        $('removeItemType').hidden = currentArea !== 'Survey';
        // d-flex overrides the native hidden style, so toggle its Bootstrap class.
        $('newItemTypeArea').classList.toggle('d-none', currentArea !== 'Survey');
        $('newItemTypeArea').classList.toggle('d-flex', currentArea === 'Survey');
        $('newItemTypeHelp').hidden = currentArea !== 'Survey';
        $('standardQuestionIncludes').hidden = !currentKey || !isItem(currentKey);
        $('includeHeaderQuestions').checked = packageData.sets[currentKey]?.includeHeaderQuestions !== false;
        $('includeFooterQuestions').checked = packageData.sets[currentKey]?.includeFooterQuestions !== false;
        $('addQuestion').disabled = !currentKey;
        if ($('resetGroup')) {
            $('resetGroup').disabled = !currentKey;
            $('resetGroup').hidden = hasProtectedQuestions(currentKey);
        }
    }
    function persist() {
        const q=editorQuestion;
        if(!q || !$('dq-id')) return;
        q.id=$('dq-id').value.trim();
        q.question=$('dq-text').value;
        q.type=$('dq-type').value;
        q.required=['Image','Information'].includes(q.type) ? false : $('dq-required').checked;
        q.options=splitLines($('dq-options').value);
        if (q.type === 'Image') q.image = $('dq-image').value.trim();
        q.showWhen=$('dq-condition').checked?{
            questionId:$('dq-previous').value,operator:$('dq-operator').value,value:$('dq-value').value
        }:null;
        q.unit=q.type === 'PlusMinus' ? '' : $('dq-unit').value;
        q.minimum=$('dq-minimum').value === '' ? null : Number($('dq-minimum').value);
        q.maximum=$('dq-maximum').value === '' ? null : Number($('dq-maximum').value);
        q.wholeNumber=q.type === 'PlusMinus' || $('dq-whole').checked;
        q.defaultValue=q.type === 'PlusMinus' && $('dq-default').value !== '' ? Number($('dq-default').value) : null;
        q.readOnly=q.type === 'Picker' && $('dq-readonly').checked;
        // OptionRules and ShowWhenAll are written immediately by their editors.

    }
    function changed() { persist(); markDirty(); renderList(); renderPreview(); }
    function moveQuestion(question, delta, visibleOnly = false) {
        persist();
        const all = questions();
        const ordered = visibleOnly && !$('showAllQuestions')?.checked
            ? all.filter(applicable) : all;
        const position = ordered.indexOf(question);
        const neighbour = ordered[position + delta];
        if (position < 0 || !neighbour) return;
        const index = all.indexOf(question);
        const target = all.indexOf(neighbour);
        all.splice(index, 1);
        all.splice(target, 0, question);
        currentIndex = target;
        markDirty();
        render();
        $('questionList').querySelector('.dq-row.active')?.scrollIntoView({ block: 'nearest' });
        $('questionPreview').querySelector('.dq-preview-selected')?.scrollIntoView({ block: 'nearest' });
    }
    function keepMoveClick(event) {
        // Blurring an edited field can redraw and remove this button before click.
        if (event.button === 0) event.preventDefault();
    }
    function renderList() {
        renderCategories(); $('questionCount').textContent=`(${questions().length})`;
        const list=$('questionList'); list.replaceChildren();
        questions().forEach((q,i)=>{
            const row=elt('div',undefined,'dq-row'+(i===currentIndex?' active':''));
            const button=elt('button',`${i+1}. ${q.question||q.id||'(new question)'}`,'dq-select');
            button.type='button'; button.onclick=()=>{persist();currentIndex=i;render();};row.append(button);
            for(const [label,delta] of [['↑',-1],['↓',1]]) {
                const move=elt('button',label,'dq-action');move.type='button';move.title='Move question';
                move.disabled=i+delta<0||i+delta>=questions().length;
                move.addEventListener('pointerdown', keepMoveClick);
                move.onclick=()=>moveQuestion(q,delta);
                row.append(move);
            }
            list.append(row);
        });
    }
    function ruleSourceOptions(q) {
        return [...contexts,...referenceQuestions().filter(item=>item!==q).map(item=>[item.id,`${item.question||item.id} (${item.id})`])];
    }
    function buildCondition(condition,q,onUpdate,onDelete) {
        const row=elt('div',undefined,'dq-cond-row');
        const source=makeSelect(ruleSourceOptions(q),condition.questionId,'form-select');
        source.title='Answer or page context controlling these choices';
        const operator=makeSelect(operators.map(op=>[op,op.replace(/([a-z])([A-Z])/g,'$1 $2')]),condition.operator,'form-select');
        const value=elt('input');value.className='form-control';value.placeholder='Value, or A|B for In';value.value=condition.value||'';
        const available=elt('datalist');available.id=`dq-candidates-${Math.random().toString(36).slice(2)}`;value.setAttribute('list',available.id);
        const fillCandidates=()=>{
            const target=questions().find(item=>item.id===source.value);
            available.replaceChildren();
            const defaults=isYesNo(target)?['Yes','No']:optionsFor(target);
            const fromRules=(target?.optionRules||[]).flatMap(r=>r.options||[]);
            const known=source.value==='$material'?['UPVC','Aluminium','Timber']:
                source.value==='$opening_type'?['Door','Window','French Doors','Combi Frame','Porch','Conservatory','Conventional Sliding Patio','Tilt & Slide Patio','Bay Window']:
                source.value==='$repair'||source.value==='$fensa'||source.value==='$replace_glass'||source.value==='$glass_stepped_allowed'?['True','False']:
                source.value==='$base_item'?['none','assembly','custom']: [...defaults,...fromRules];
            [...new Set(known)].forEach(s=>{const option=elt('option');option.value=s;available.append(option);});
        };
        const remove=elt('button','×','btn btn-outline-danger btn-sm');remove.type='button';remove.title='Remove condition';remove.onclick=onDelete;
        const save=()=>{condition.questionId=source.value;condition.operator=operator.value;condition.value=value.value;onUpdate();};
        source.onchange=()=>{fillCandidates();save();};operator.onchange=save;value.oninput=save;
        row.append(source,operator,value,available,remove);fillCandidates();return row;
    }
    function renderOptionRules(q) {
        const host=$('dq-rules'); if(!host)return;
        host.replaceChildren();q.optionRules??=[];
        const help=elt('p','The first matching rule supplies the options. Conditions within a rule must ALL match. Use | for alternatives with the “In” operator. The normal options above are the fallback.','dq-help');host.append(help);
        q.optionRules.forEach((rule,index)=>{
            const box=elt('div',undefined,'dq-rule');
            const heading=elt('div',undefined,'d-flex justify-content-between align-items-center mb-2');
            heading.append(elt('strong',`Rule ${index+1}`));
            const actions=elt('div',undefined,'d-flex gap-1');
            for(const [symbol,step] of [['↑',-1],['↓',1]]){
                const move=elt('button',symbol,'btn btn-outline-secondary btn-sm');move.type='button';move.disabled=index+step<0||index+step>=q.optionRules.length;
                move.onclick=()=>{[q.optionRules[index],q.optionRules[index+step]]=[q.optionRules[index+step],q.optionRules[index]];markDirty();renderOptionRules(q);renderPreview();};actions.append(move);
            }
            const remove=elt('button','Delete','btn btn-outline-danger btn-sm');remove.type='button';
            remove.onclick=()=>{q.optionRules.splice(index,1);markDirty();renderOptionRules(q);renderPreview();};actions.append(remove);heading.append(actions);box.append(heading);
            const conditions=elt('div');
            const refresh=()=>{markDirty();renderPreview();};
            (rule.conditions||[]).forEach((condition,i)=>conditions.append(buildCondition(condition,q,refresh,()=>{
                rule.conditions.splice(i,1);refresh();renderOptionRules(q);
            })));
            box.append(conditions);
            const addCondition=elt('button','+ AND condition','btn btn-outline-secondary btn-sm mb-2');addCondition.type='button';
            addCondition.onclick=()=>{rule.conditions??=[];rule.conditions.push({questionId:ruleSourceOptions(q)[0]?.[0]||'',operator:'Equals',value:''});refresh();renderOptionRules(q);};
            box.append(addCondition);
            const opts=field('Options when this rule matches (one per line)',`dq-rule-options-${index}`,'textarea',(rule.options||[]).join('\n'));
            opts.querySelector('textarea').addEventListener('input',e=>{rule.options=splitLines(e.target.value);refresh();});box.append(opts);
            host.append(box);
        });
        const add=elt('button','+ Add conditional option rule','btn btn-outline-primary btn-sm');add.type='button';
        add.onclick=()=>{q.optionRules.push({conditions:[{questionId:ruleSourceOptions(q)[0]?.[0]||'',operator:'Equals',value:''}],options:[]});markDirty();renderOptionRules(q);renderPreview();};
        host.append(add);
    }
    // Image rule editor: match one or more conditions, then show bundled diagrams.
    function renderImageRules(q) {
        const host = $('dq-image-rules');
        if (!host) return;
        host.replaceChildren();
        q.imageRules ??= [];
        host.append(elt('p','Choose files already bundled in the survey app. Each rule can show multiple diagrams. The first matching rule wins.','dq-help'));
        q.imageRules.forEach((rule, index) => {
            const box = elt('div',undefined,'dq-rule');
            const heading = elt('div',undefined,'d-flex justify-content-between align-items-center mb-2');
            heading.append(elt('strong',`Image rule ${index+1}`));
            const remove=elt('button','Delete','btn btn-outline-danger btn-sm');remove.type='button';
            remove.onclick=()=>{q.imageRules.splice(index,1);markDirty();renderImageRules(q);renderPreview();};
            heading.append(remove);box.append(heading);
            const conditions=elt('div');
            (rule.conditions||[]).forEach((condition,i)=>{
                conditions.append(buildCondition(condition,q,()=>{markDirty();renderPreview();},()=>{
                    rule.conditions.splice(i,1);markDirty();renderImageRules(q);renderPreview();
                }));
            });
            box.append(conditions);
            const add=elt('button','+ AND condition','btn btn-outline-secondary btn-sm mb-2');add.type='button';
            add.onclick=()=>{rule.conditions??=[];rule.conditions.push({questionId:currentKey === 'Conservatory' ? '$conservatory_roof' : currentKey === 'Garage' ? '$garage_opening' : '$bifold_doors',operator:'Equals',value:''});markDirty();renderImageRules(q);};
            box.append(add);
            const images=field('Diagram filenames (one per line)',`dq-images-${index}`,'textarea',(rule.images||[]).join('\n'));
            images.querySelector('textarea').addEventListener('input',e=>{rule.images=splitLines(e.target.value);markDirty();renderPreview();});
            const hint=elt('small',undefined,'dq-help');hint.textContent='Available: '+window.questionImages.join(', ');
            box.append(images,hint);host.append(box);
        });
        const addRule=elt('button','+ Add image rule','btn btn-outline-primary btn-sm');addRule.type='button';
        addRule.onclick=()=>{q.imageRules.push({conditions:[{questionId:currentKey === 'Conservatory' ? '$conservatory_roof' : currentKey === 'Garage' ? '$garage_opening' : '$bifold_doors',operator:'Equals',value:''}],images:[]});markDirty();renderImageRules(q);};
        host.append(addRule);
    }

    function renderEditor() {
        // Removing a focused field can fire change during the category switch.
        // Never write the old fields into the newly selected question.
        editorQuestion = null;
        const editor=$('questionEditor');editor.replaceChildren();
        const q=questions()[currentIndex];if(!q){editor.append(elt('p',currentKey ? 'Add a question to begin.' : 'Add an item type to begin.'));return;}
        editor.append(elt('h5','Edit question'));
        editor.append(field('Stable question ID','dq-id','text',q.id));
        $('dq-id').readOnly = hasProtectedQuestions(currentKey);
        editor.append(elt('p','Changing an ID disconnects previously saved answers from this label.','dq-help'));
        editor.append(field('Question / label','dq-text','textarea',q.question));
        editor.append(selectField('Answer type','dq-type',types.filter(t => !workflowTypes.includes(t) || supportsWorkflow(t)).map(t=>[t,workflowLabels[t] || t]),q.type));
        const readOnly=elt('div',undefined,'form-check mb-3');
        const readOnlyCheck=elt('input');readOnlyCheck.type='checkbox';readOnlyCheck.id='dq-readonly';readOnlyCheck.className='form-check-input';readOnlyCheck.checked=!!q.readOnly;
        const readOnlyLabel=elt('label','Selection only','form-check-label');readOnlyLabel.htmlFor=readOnlyCheck.id;
        readOnly.append(readOnlyCheck,readOnlyLabel,elt('small','Choose from the dropdown; typing is disabled.','d-block dq-help'));editor.append(readOnly);
        const measurement=elt('div',undefined,'mb-3');measurement.id='dq-measurement-wrap';
        measurement.append(field('Units (blank for quantities)','dq-unit','text',q.unit ?? 'mm'));
        measurement.append(field('Minimum value (optional)','dq-minimum','number',q.minimum ?? ''));
        measurement.append(field('Maximum value (optional)','dq-maximum','number',q.maximum ?? ''));
        const initialValue=field('Initial value (optional)','dq-default','number',q.defaultValue ?? '');
        measurement.append(initialValue);
        const whole=elt('input');whole.type='checkbox';whole.id='dq-whole';whole.checked=!!q.wholeNumber;
        const wholeLabel=elt('label',' Whole numbers only','form-label');wholeLabel.htmlFor=whole.id;
        measurement.append(whole,wholeLabel);editor.append(measurement);
        const imageSource = selectField('Default diagram (bundled asset)', 'dq-image',
            [['','— None —'],...window.questionImages.map(name => [name,name])], q.image || '');
        imageSource.id = 'dq-image-wrap';
        editor.append(imageSource);
        const imgRulesWrap = elt('div',undefined,'dq-options-wrapper');
        imgRulesWrap.id = 'dq-image-rules-wrap';
        imgRulesWrap.append(elt('h6','Conditional diagrams'));
        const imageRules = elt('div');imageRules.id='dq-image-rules';
        imgRulesWrap.append(imageRules);
        editor.append(imgRulesWrap);
        const required=elt('div',undefined,'form-check mb-3');
        const checkbox=elt('input');checkbox.type='checkbox';checkbox.id='dq-required';checkbox.className='form-check-input';checkbox.checked=!!q.required;
        const label=elt('label','Required when visible','form-check-label');label.htmlFor=checkbox.id;
        required.append(checkbox,label);editor.append(required);
        const opts=field('Default options (one per line)','dq-options','textarea',(q.options||[]).join('\n'));
        opts.id='dq-option-wrap';editor.append(opts);
        const conditional=elt('div',undefined,'dq-options-wrapper');conditional.id='dq-option-rule-wrap';
        const header=elt('h6','Conditional picker options');conditional.append(header);
        const rules=elt('div');rules.id='dq-rules';conditional.append(rules);editor.append(conditional);
        const condGroup=elt('div',undefined,'form-check mb-2');
        const cond=elt('input');cond.type='checkbox';cond.id='dq-condition';cond.className='form-check-input';cond.checked=!!q.showWhen;
        const condLabel=elt('label','Only show this question when…','form-check-label');condLabel.htmlFor=cond.id;
        condGroup.append(cond,condLabel);editor.append(condGroup);
        const wrap=elt('div',undefined,'mb-3');wrap.id='dq-condition-wrap';
        const preceding=referenceQuestions().slice(0,referenceQuestions().indexOf(q));
        const previous=makeSelect([...contexts,...preceding.map(prev=>[prev.id,`${prev.question} (${prev.id})`])],q.showWhen?.questionId);
        previous.id='dq-previous';previous.classList.add('mb-2');wrap.append(previous);
        const operator=makeSelect(operators.map(x=>[x,x]),q.showWhen?.operator||'Equals');operator.id='dq-operator';operator.classList.add('mb-2');wrap.append(operator);
        wrap.append(field('Comparison value (if applicable)','dq-value','text',q.showWhen?.value||''));editor.append(wrap);
        const allConditions=elt('div',undefined,'mb-3');allConditions.id='dq-visibility-all';
        editor.append(allConditions);renderVisibilityConditions(q);
        const del=elt('button','Delete question','btn btn-outline-danger btn-sm');del.type='button';
        del.onclick=()=>{if(!confirm('Delete this question? Existing answers remain in saved jobs but the label may no longer display.'))return;
            questions().splice(currentIndex,1);currentIndex=Math.max(0,Math.min(currentIndex,questions().length-1));markDirty();render();};
        if (!hasProtectedQuestions(currentKey)) editor.append(del);
        else editor.append(elt('p','Shared questions can be edited and reordered, but cannot be deleted.','dq-help'));
        function applyVisibility() {
            const isChoice=['Picker','ButtonPicker'].includes($('dq-type').value);
            opts.hidden=!isChoice;conditional.hidden=!isChoice;wrap.hidden=!cond.checked;
            const isImage = $('dq-type').value === 'Image';
            imageSource.hidden = !isImage; imgRulesWrap.hidden = !isImage;
            required.hidden = isImage || $('dq-type').value === 'Information';
            const isPlusMinus = $('dq-type').value === 'PlusMinus';
            measurement.hidden = !['Measurement','PlusMinus'].includes($('dq-type').value);
            $('dq-unit').closest('.mb-3').hidden = isPlusMinus;
            whole.hidden = isPlusMinus; wholeLabel.hidden = isPlusMinus;
            initialValue.hidden = !isPlusMinus;
            for (const id of ['dq-minimum','dq-maximum','dq-default']) $(id).step = isPlusMinus ? '1' : 'any';
            readOnly.hidden = $('dq-type').value !== 'Picker';
        }
        const workflowHelp = elt('p', 'Replacement and handle question types show Yes/No and open the matching survey details or handle drawing when Yes is selected. Each item group can have one question of each workflow type.', 'dq-help');
        editor.append(workflowHelp);
        editorQuestion = q;
        applyVisibility();renderOptionRules(q);renderImageRules(q);
        editor.querySelectorAll('input,textarea,select').forEach(control=>{
            if (control.closest('#dq-option-rule-wrap') || control.closest('#dq-visibility-all')) return; // Rule editor saves immediately.
            control.addEventListener('input',()=>{applyVisibility();changed();});
            control.addEventListener('change',()=>{applyVisibility();changed();});
        });
    }
    function renderVisibilityConditions(q) {
        const host=$('dq-visibility-all');if(!host)return;
        host.replaceChildren();q.showWhenAll??=[];
        host.append(elt('h6','Additional visibility conditions (all must match)'));
        q.showWhenAll.forEach((condition,index)=>{
            host.append(buildCondition(condition,q,()=>{markDirty();renderPreview();},()=>{
                q.showWhenAll.splice(index,1);markDirty();renderVisibilityConditions(q);renderPreview();
            }));
        });
        const add=elt('button','Add visibility condition','btn btn-outline-primary btn-sm');add.type='button';
        add.onclick=()=>{
            const previous=questions()[currentIndex-1];
            q.showWhenAll.push({questionId:previous?.id || '$repair',operator:'Equals',value:''});
            markDirty();renderVisibilityConditions(q);renderPreview();
        };host.append(add);
    }
    function previewContextValue(id) {
        const assembly = currentKey?.toLowerCase() === 'itemassembly';
        const material = assembly ? previewAnswers.material || '' : '';
        if (assembly) {
            if (id === '$material') return material;
            if (id === '$opening_type') return previewAnswers.opening_type || '';
            if (id === '$repair') return !previewAnswers.flex_repair ? '' : String(['yes','true'].includes(String(previewAnswers.flex_repair || '').toLowerCase()));
            if (id === '$fensa' && previewAnswers.flex_fensa) return String(['yes','true'].includes(String(previewAnswers.flex_fensa).toLowerCase()));
        }
        if (isFlexibleGroup()) {
            const yes = value => ['yes','true'].includes(String(value || '').trim().toLowerCase());
            if (id === '$replace_glass') return String(questions().some(q => q.type === 'ReplaceGlass' && yes(previewAnswers[q.id])));
            if (id === '$repair' && previewAnswers.flex_repair) return String(yes(previewAnswers.flex_repair));
            if (id === '$fensa' && previewAnswers.flex_fensa) return String(yes(previewAnswers.flex_fensa));
            if (id === '$bifold_doors') return previewAnswers.number_of_doors || '';
            if (id === '$conservatory_roof') return previewAnswers.type || '';
            if (id === '$conservatory_wall') return previewAnswers.wall_pos || '';
            if (id === '$garage_opening') return previewAnswers.opening_type || '';
        }
        if (currentKey === 'Glass') {
            const parent = previewContext['$base_item'] || 'none';
            const material = ['assembly','custom'].includes(parent) ? previewContext['$material'] || '' : previewAnswers.ProductInto || '';
            if (id === '$material') return material;
            if (id === '$inherited_material') return String(['assembly','custom'].includes(parent) && !!previewContext['$material']);
            if (id === '$glass_stepped_allowed')
                return parent === 'none' || (['assembly','custom'].includes(parent) && material.toLowerCase() === 'timber') ? 'True' : 'False';
        }
        return previewContext[id] || '';
    }
    function conditionMatches(c) {
        const actual=String(c.questionId?.startsWith('$')?previewContextValue(c.questionId):previewAnswers[c.questionId]||'').trim();
        const expected=String(c.value||'').trim();

        const normalizeBoolean = value => {
            const text = String(value).trim().toLowerCase();

            if (text === 'yes' || text === 'true') return 'true';
            if (text === 'no' || text === 'false') return 'false';

            return text;
        };

        const value = normalizeBoolean(actual);
        const target = normalizeBoolean(expected);

        switch(c.operator) {
            case 'Equals': return value===target;
            case 'NotEquals': return value!==target;
            case 'IsBlank': return value==='';
            case 'IsNotBlank': return value!=='';
            case 'NotBlankOrNone': return value!==''&&value!=='none';
            case 'GreaterThan': return value!==''&&target!==''&&Number.isFinite(Number(value))&&Number.isFinite(Number(target))&&Number(value)>Number(target);
            case 'Contains': return value.includes(target);
            case 'In': return expected.split('|').some(s=>s.trim().toLowerCase()===value);
            default: return true;
        }
    }
    const applicable=q=>(!q.showWhen||conditionMatches(q.showWhen))&&(q.showWhenAll||[]).every(conditionMatches);
    const resolvedImages=q=>{
        const rule=(q.imageRules||[]).find(r=>(r.conditions||[]).every(conditionMatches));
        return rule ? (rule.images||[]) : (q.image ? [q.image] : []);
    };
    const resolvedOptions=q=>{
        const match=(q.optionRules||[]).find(r=>(r.conditions||[]).every(conditionMatches));
        return match ? (match.options||[]) : (q.options||[]);
    };
    function selectPreviewQuestion(index) {
        if (index === currentIndex) return;
        persist();
        currentIndex = index;
        render();
        const active = $('questionList').querySelector('.dq-row.active');
        active?.scrollIntoView({ block: 'nearest' });
    }

    // ------------------------------------------------------------
    // Live preview: selectable cards and app-style answer controls
    // ------------------------------------------------------------
    function renderPreview() {
        const panel = $('questionPreview');
        panel.replaceChildren();

        const combined = previewQuestions();
        combined.filter(q => q.type === 'PlusMinus').forEach(q => {
            const min = q.minimum ?? 0, max = q.maximum ?? 10;
            const value = previewAnswers[q.id] === undefined || previewAnswers[q.id] === '' ? q.defaultValue ?? min : Number(previewAnswers[q.id]);
            previewAnswers[q.id] = String(Math.max(min, Math.min(max, value)));
        });
        const visibleQuestions = $('showAllQuestions')?.checked ? combined : combined.filter(applicable);
        combined.forEach(q => {
            const index = questions().indexOf(q);
            const ownQuestion = index >= 0;
            const ownerKey = ownQuestion ? currentKey : packageData.sets.Header.questions.includes(q) ? 'Header' : 'Footer';
            const visibleIndex = visibleQuestions.indexOf(q);
            if (visibleIndex < 0) return;

            const item = elt(
                'div',
                undefined,
                'dq-question' + (q.showWhen || q.showWhenAll?.length ? ' dq-conditional' : '') + (index === currentIndex ? ' dq-preview-selected' : '')
            );

            // Clicking empty space in the question card selects it for editing.
            // Answer fields, picker buttons and reorder controls remain interactive.
            item.addEventListener('click', event => {
                if (event.target.closest('button, input, select, textarea, option, a, [role="button"]')) {
                    return;
                }

                if (ownQuestion) selectPreviewQuestion(index);
                else {
                    persist(); currentArea = 'SurveyHeaderFooter'; currentKey = ownerKey;
                    currentIndex = questions().indexOf(q); render();
                }
            });
            const heading = elt('div', undefined, 'dq-preview-heading');
            const title = elt('span', q.question || '(Unlabelled question)', 'dq-preview-title');
            if (q.required) title.append(elt('span', ' *', 'dq-required'));
            heading.append(title);

            const actions = elt('div', undefined, 'dq-preview-actions');
            const movableQuestions = visibleQuestions.filter(item => questions().includes(item));
            const moveIndex = movableQuestions.indexOf(q);
            if (!ownQuestion) actions.append(elt('small',`${ownerKey} questions — click to edit`,'dq-help'));
            for (const [symbol, delta, label] of ownQuestion ? [['↑', -1, 'Move question up'], ['↓', 1, 'Move question down']] : []) {
                const move = elt('button', symbol, 'dq-preview-move');
                move.type = 'button';
                move.title = label;
                move.setAttribute('aria-label', label);
                move.disabled = moveIndex + delta < 0 || moveIndex + delta >= movableQuestions.length;
                move.addEventListener('pointerdown', keepMoveClick);
                move.onclick = () => moveQuestion(q, delta, true);
                actions.append(move);
            }
            heading.append(actions);
            item.append(heading);

            const setAnswer = value => {
                previewAnswers[q.id] = value;
                if (currentKey === 'ItemAssembly' && q.id === 'material') previewContext['$material'] = value;
                renderPreview();
            };
            const answer = previewAnswers[q.id] ?? '';

            // Display-only image questions: no answer and no required marker.
            if (q.type === 'Information') {
                item.append(elt('small','Display-only message; no answer required.','dq-help'));
            } else if (q.type === 'Image') {
                const gallery=elt('div',undefined,'dq-image-gallery');
                resolvedImages(q).forEach(name=>{
                    if (!window.questionImages.includes(name)) return;
                    const img=elt('img');
                    img.src=window.questionImageBase + encodeURIComponent(name);
                    img.alt=name.replace(/\.[^.]+$/,'').replaceAll('_',' ');
                    img.loading='lazy';
                    gallery.append(img);
                });
                if (!gallery.children.length) gallery.append(elt('small','No diagram matches these answers.','dq-help'));
                item.append(gallery);
            // Yes / No: two selectable buttons.
            } else
            if (isYesNo(q)) {
                const group = elt('div', undefined, 'dq-preview-yesno');
                for (const value of ['Yes', 'No']) {
                    const button = elt('button', value, 'dq-choice-button' + (answer.toLowerCase() === value.toLowerCase() ? ' selected' : ''));
                    button.type = 'button';
                    button.setAttribute('aria-pressed', String(answer.toLowerCase() === value.toLowerCase()));
                    button.onclick = () => setAnswer(value);
                    group.append(button);
                }
                item.append(group);
                if (workflowTypes.includes(q.type) && ['yes','true'].includes(answer.trim().toLowerCase())) {
                    const details = elt('button', q.type === 'Handles' ? 'Handle drawing' : `${workflowLabels[q.type]} details`, 'btn btn-outline-primary btn-sm mt-2');
                    details.type = 'button';
                    details.onclick = () => alert('The survey app opens the details editor here. This preview does not save item details.');
                    item.append(details);
                }
            // Button picker: button with a dropdown menu.
            } else if (q.type === 'ButtonPicker') {
                const values = resolvedOptions(q);
                const picker = elt('div', undefined, 'dq-button-picker');
                const trigger = elt('button', undefined, 'dq-preview-picker');
                trigger.type = 'button';
                trigger.append(elt('span', answer || '— Select —'));
                trigger.append(elt('span', '▾', 'dq-picker-arrow'));
                trigger.setAttribute('aria-expanded', 'false');
                const menu = elt('div', undefined, 'dq-picker-menu');
                menu.hidden = true;
                const choices = answer && !values.includes(answer) ? [...values, answer] : values;
                if (!choices.length) menu.append(elt('div', 'No options configured', 'dq-picker-empty'));
                choices.forEach(value => {
                    const option = elt('button', value, 'dq-picker-option' + (value === answer ? ' selected' : ''));
                    option.type = 'button';
                    option.onclick = () => setAnswer(value);
                    menu.append(option);
                });
                trigger.onclick = () => {
                    menu.hidden = !menu.hidden;
                    trigger.setAttribute('aria-expanded', String(!menu.hidden));
                };
                picker.append(trigger, menu);
                item.append(picker);
            } else if (q.type === 'PlusMinus') {
                const min = q.minimum ?? 0, max = q.maximum ?? 10;
                const value = Number(answer);
                const counter = elt('div', undefined, 'd-flex align-items-center gap-2');
                const minus = elt('button', '−', 'btn btn-outline-primary');minus.type='button';
                minus.setAttribute('aria-label', 'Decrease ' + q.question);minus.disabled=value <= min;
                minus.onclick=()=>setAnswer(String(Math.max(min, value - 1)));
                const quantity = elt('span', String(value), 'fw-bold');quantity.setAttribute('aria-live','polite');
                const plus = elt('button', '+', 'btn btn-outline-primary');plus.type='button';
                plus.setAttribute('aria-label', 'Increase ' + q.question);plus.disabled=value >= max;
                plus.onclick=()=>setAnswer(String(Math.min(max, value + 1)));
                counter.append(minus,quantity,plus);item.append(counter);
            // Other question types: native picker or input field.
            } else {
                let input;
                if (q.type === 'Picker') {
                    input = elt('select');
                    input.className = 'form-select';
                    const values = resolvedOptions(q);
                    const blank = elt('option', '— Select —');
                    blank.value = '';
                    input.append(blank);
                    if (!q.readOnly) {
                        const typed = elt('input');typed.className='form-control mb-1';typed.value=answer;
                        typed.setAttribute('aria-label', q.question + ' (type or select)');
                        typed.placeholder='Type or select an answer';typed.addEventListener('change',()=>setAnswer(typed.value));
                        item.append(typed);
                    }
                    values.forEach(value => {
                        const option = elt('option', value);
                        option.value = value;
                        input.append(option);
                    });
                    if (answer && !values.includes(answer)) {
                        const old = elt('option', `${answer} (previous answer)`);
                        old.value = answer;
                        input.append(old);
                    }
                } else {
                    input = elt('input');
                    input.className = 'form-control';
                    input.type = q.type === 'Date' ? 'date' : q.type === 'Measurement' ? 'number' : 'text';
                    if (q.type === 'Measurement') {
                        if (q.minimum != null) input.min=q.minimum;
                        if (q.maximum != null) input.max=q.maximum;
                        input.step=q.wholeNumber ? '1' : 'any';
                        input.title=q.unit || 'Quantity';
                    }
                }
                input.value = answer;
                input.oninput = () => { previewAnswers[q.id] = input.value; };
                input.onchange = () => setAnswer(input.value);
                item.append(input);
            }
            panel.append(item);
        });
    }
    const render=()=>{renderList();renderEditor();renderPreview();};
    $('showAllQuestions')?.addEventListener('change', renderPreview);
    $('categoryArea').onchange = e => {
        persist();
        selectedCategories[currentArea] = currentKey;
        currentArea = e.target.value;
        currentKey = selectedCategories[currentArea];
        currentIndex = 0;
        previewAnswers = {};
        render();
    };
    $('setChooser').onchange=e=>{persist();currentKey=e.target.value;currentIndex=0;previewAnswers={};render();};
    for (const [id, property] of [['includeHeaderQuestions','includeHeaderQuestions'],['includeFooterQuestions','includeFooterQuestions']]) {
        $(id).onchange = () => {
            if (!currentKey || !isItem(currentKey)) return;
            persist(); packageData.sets[currentKey][property] = $(id).checked;
            markDirty(); render();
        };
    }
    $('addQuestion').onclick = () => {
        persist();
        currentIndex = questions().length;
        questions().splice(currentIndex, 0, {id:'new_'+Math.random().toString(36).slice(2,8),question:'New question',type:'Text',required:false,options:[],optionRules:[],showWhen:null});
        markDirty();
        render();
    };
    $('addItemType').onclick = () => {
        if (currentArea !== 'Survey') return;
        persist();
        const name = $('newItemType').value.trim();
        const reserved = [...Object.keys(defaults.sets), ...Object.values(window.questionItemLabels)];
        if (!/^[A-Za-z][A-Za-z0-9 _-]{0,79}$/.test(name) || ['constructor','prototype','__proto__'].includes(name.toLowerCase()))
            return fileStatus('Use a name of up to 80 letters, numbers, spaces, _ or -, starting with a letter.', true);
        if (keys.some(k => k.toLowerCase() === name.toLowerCase())) return fileStatus('That item type already exists.', true);
        const builtIn = Object.keys(defaults.sets).find(k => k.toLowerCase() === name.toLowerCase())
            || Object.keys(window.questionItemLabels).find(k => window.questionItemLabels[k].toLowerCase() === name.toLowerCase());
        if (builtIn && keys.some(k => k.toLowerCase() === builtIn.toLowerCase())) return fileStatus('That item type already exists.', true);
        if (!builtIn && reserved.some(k => k.toLowerCase() === name.toLowerCase()))
            return fileStatus('That name is already used by a built-in item type. Use its question category name to restore it.', true);
        if (keys.length >= 100) return fileStatus('Maximum 100 question groups.', true);
        const key = builtIn || name;
        packageData.sets[key] = builtIn ? structuredClone(defaults.sets[key]) : {questions: [], includeHeaderQuestions: true, includeFooterQuestions: true};
        keys = Object.keys(packageData.sets).sort((a,b) => a.localeCompare(b));
        currentKey = key; currentIndex = 0; previewAnswers = {};
        $('newItemType').value = ''; markDirty(); render();
        fileStatus(`Added ${key}. Standard questions are included automatically. Publish to make it available to survey devices.`);
    };
    $('removeItemType').onclick = () => {
        if (!currentKey || !isItem(currentKey)) return;
        if (!confirm(`Remove ${currentKey} from available item types? Existing surveyed items will be retained. The change takes effect after publishing and synchronization.`)) return;
        const removed = currentKey;
        delete packageData.sets[currentKey];
        keys = Object.keys(packageData.sets).sort((a,b) => a.localeCompare(b));
        currentKey = areaKeys()[0]; currentIndex = 0; previewAnswers = {}; markDirty(); render();
        fileStatus(`Removed ${removed}. Publish to update survey devices.`);
    };
    if ($('resetGroup')) $('resetGroup').onclick=()=>{if(hasProtectedQuestions(currentKey))return;if(!confirm(`Restore all default questions for ${currentKey}? Your changes to this category will be lost when saved.`))return;
        packageData.sets[currentKey]=structuredClone(defaults.sets[currentKey] || {questions: [], includeHeaderQuestions: true, includeFooterQuestions: true});currentIndex=0;previewAnswers={};markDirty();render();};
    function fileStatus(message, error = false) {
        const status = $('questionFileStatus');
        status.textContent = message;
        status.className = 'small mb-2 ' + (error ? 'text-danger' : 'text-success');
        status.hidden = false;
    }

    $('exportQuestions').onclick = async () => {
        let written = 0;
        $('exportQuestions').disabled = true;
        try {
            if (!window.showDirectoryPicker)
                throw Error('Folder export requires Edge or Chrome over HTTPS (or localhost).');
            document.activeElement?.blur();
            persist();
            const files = keys.map(category => ({
                name: `${category}Questions.json`,
                json: JSON.stringify({category, ...packageData.sets[category]}, null, 2) + '\n'
            }));
            const parentFolder = await window.showDirectoryPicker({id: 'smartops-question-export', mode: 'readwrite'});
            const exportedAt = new Date();
            const pad = value => String(value).padStart(2, '0');
            const timestamp = [pad(exportedAt.getDate()), pad(exportedAt.getMonth() + 1),
                exportedAt.getFullYear(), pad(exportedAt.getHours()), pad(exportedAt.getMinutes())].join('-');
            const folderName = `questionsv${packageData.version}-${timestamp}`;
            const folder = await parentFolder.getDirectoryHandle(folderName, {create: true});
            for (const file of files) {
                const handle = await folder.getFileHandle(file.name, {create: true});
                const stream = await handle.createWritable();
                try {
                    await stream.write(file.json);
                    await stream.close();
                } catch (error) {
                    await stream.abort().catch(() => {});
                    throw error;
                }
                written++;
            }
            fileStatus(`Exported ${written} category JSON files to ${folder.name}, including current edits.`);
        } catch (error) {
            if (error.name === 'AbortError' && written === 0) fileStatus('Export cancelled.');
            else fileStatus(`Export stopped after ${written} files: ${error.message}`, true);
        } finally {
            $('exportQuestions').disabled = false;
        }
    };

    $('importQuestions').onclick = () => $('importQuestionsFile').click();
    $('importQuestionsFile').onchange = async () => {
        const input = $('importQuestionsFile');
        const files = Array.from(input.files);
        if (!files.length) return;
        $('importQuestions').disabled = true;
        try {
            if (files.length > keys.length)
                throw Error(`Choose up to ${keys.length} category files at a time.`);
            if (files.some(file => file.size === 0 || file.size > 1_000_000)
                || files.reduce((total, file) => total + file.size, 0) > 1_000_000)
                throw Error('Choose non-empty JSON files totalling up to 1 MB.');
            document.activeElement?.blur();
            persist();
            const serial = editSerial;
            const selectedCategory = currentKey;
            const candidate = structuredClone(packageData);
            const importedCategories = new Set();
            const categoryName = name => typeof name === 'string'
                ? keys.find(key => key.toLowerCase() === name.toLowerCase()) || name : undefined;
            const applyCategory = (category, definition) => {
                if (importedCategories.has(category)) throw Error(`More than one file contains ${category}.`);
                candidate.sets[category] = definition;
                importedCategories.add(category);
            };
            for (const file of files) {
                let imported;
                try { imported = JSON.parse((await file.text()).replace(/^\uFEFF/, '')); }
                catch { throw Error(`${file.name} does not contain valid JSON.`); }
                const property = name => imported && !Array.isArray(imported) && typeof imported === 'object'
                    ? Object.keys(imported).find(key => key.toLowerCase() === name) : undefined;
                const setsProperty = property('sets');
                if (setsProperty !== undefined) {
                    if (files.length !== 1) throw Error('Import a full backup on its own, or select category files.');
                    candidate.sets = imported[setsProperty];
                    Object.keys(candidate.sets).forEach(key => importedCategories.add(key));
                    continue;
                }
                if (!Array.isArray(imported) && property('questions') === undefined)
                    throw Error(`${file.name} must contain a questions array.`);
                const categoryProperty = property('category');
                const fromFileName = categoryName(file.name.replace(/Questions\.json$/i, '').replace(/\.json$/i, ''));
                const category = categoryProperty !== undefined ? categoryName(imported[categoryProperty])
                    : fromFileName || (files.length === 1 ? selectedCategory : undefined);
                if (!category) throw Error(`Cannot identify the category in ${file.name}. Use an exported category file.`);
                applyCategory(category, Array.isArray(imported) ? {questions: imported} : imported);
            }

            const data = new FormData($('questionForm'));
            data.set('questionsJson', JSON.stringify(candidate));
            const response = await fetch(window.questionImportValidateUrl,
                {method: 'POST', body: data, credentials: 'same-origin'});
            let result;
            try { result = await response.json(); }
            catch { throw Error('Could not validate the files. Reload the page and try again.'); }
            if (!response.ok) throw Error(result.error || 'Could not import these questions.');
            document.activeElement?.blur();
            if (serial !== editSerial || selectedCategory !== currentKey)
                throw Error('Questions changed while importing. Choose the files again to apply them to the current editor.');
            packageData.sets = result.sets;
            keys = Object.keys(packageData.sets).sort((a,b) => a.localeCompare(b));
            if (!areaKeys().includes(currentKey)) currentKey = areaKeys()[0];
            currentIndex = 0;
            previewAnswers = {};
            markDirty();
            render();
            fileStatus(`Imported ${importedCategories.size} categories: ${[...importedCategories].join(', ')}. Review before publishing.`);
        } catch (error) {
            fileStatus('Import failed: ' + error.message + ' Your questions have not been replaced.', true);
        } finally {
            input.value = '';
            $('importQuestions').disabled = false;
        }
    };

    // Save a draft asynchronously without publishing to devices.
    async function saveDraft(manual) {
        if (savePromise) return savePromise;
        if (!dirty && !manual) return;
        persist();
        const serial = editSerial;
        const status = $('draftStatus');
        status.textContent = 'Saving draft…';
        const form = $('questionForm');
        const data = new FormData(form);
        data.set('questionsJson', JSON.stringify(packageData));
        savePromise = (async () => {
            try {
                const response = await fetch(window.questionDraftSaveUrl, {method:'POST',body:data,credentials:'same-origin',headers:{Accept:'application/json'}});
                if (response.redirected || response.status === 401 || response.status === 403)
                    throw Error('Your session has expired or access was denied. Export your edits before signing in again.');
                if (!response.headers.get('content-type')?.includes('application/json'))
                    throw Error(`The server returned an unexpected response (HTTP ${response.status}). Export your edits before reloading; check the 4D Error Log.`);
                const result = await response.json();
                if (!response.ok) throw Error(result.error || 'Could not save draft.');
                $('draftRevision').value = result.revision;
                hasSavedDraft = true;
                $('discardDraft').hidden = false;
                if (serial === editSerial) {
                    dirty=false;window.questionEditorDirty=false;
                    $('dirtyBadge').hidden=true;
                }
                status.textContent = 'Draft saved ' + new Date(result.savedAtUtc).toLocaleTimeString();
            } catch (error) {
                status.textContent = 'Draft not saved: ' + error.message;
                if (manual) alert(status.textContent);
            } finally {
                savePromise = null;
                if (dirty && !autosaveDisabled) {
                    clearTimeout(autoSaveTimer);
                    autoSaveTimer = setTimeout(() => saveDraft(false), 25000);
                }
            }
        })();
        return savePromise;
    }
    $('saveDraft').onclick = () => saveDraft(true);
    $('discardDraft').onclick = async () => {
        if (!confirm('Discard all unpublished changes and load the published questions?')) return;
        autosaveDisabled = true;
        clearTimeout(autoSaveTimer);
        if (savePromise) await savePromise;
        try {
            if (hasSavedDraft) {
                const formData = new FormData($('questionForm'));
                const response = await fetch(window.questionDraftDiscardUrl,{method:'POST',body:formData,credentials:'same-origin'});
                if (!response.ok) throw Error('Draft changed in another session.');
            }
            window.questionEditorDirty=false;dirty=false;
            location.reload();
        } catch(error) {
            autosaveDisabled=false;
            if (dirty) autoSaveTimer = setTimeout(() => saveDraft(false), 25000);
            alert(error.message);
        }
    };
    if ($('resetAll')) $('resetAll').onclick = async () => {
        if (autosaveDisabled) return;
        const button = $('resetAll');
        if (!confirm(`Reset ALL questions for ${button.dataset.companyName} to defaults and V1? This immediately replaces every published category, removes custom categories, and discards all draft and unsaved changes.`)) return;
        autosaveDisabled = true;
        clearTimeout(autoSaveTimer);
        // Prevent further edits or mutations while waiting for the current draft save.
        const controls = [...document.querySelectorAll('.dq-page button, .dq-page input, .dq-page select, .dq-page textarea')]
            .filter(control => !control.disabled && control.type !== 'hidden');
        controls.forEach(control => control.disabled = true);
        if (savePromise) await savePromise;
        try {
            const response = await fetch(window.questionResetAllUrl, {
                method: 'POST', body: new FormData($('questionForm')), credentials: 'same-origin'
            });
            if (!response.ok) {
                const result = await response.json();
                throw Error(result.error || 'Could not reset questions.');
            }
            dirty = false;
            window.questionEditorDirty = false;
            location.reload();
        } catch (error) {
            autosaveDisabled = false;
            controls.forEach(control => control.disabled = false);
            if (dirty) autoSaveTimer = setTimeout(() => saveDraft(false), 25000);
            alert(error.message);
        }
    };
    // Publish waits for any outstanding draft request, preventing revision races.
    $('questionForm').onsubmit=async e=>{
        e.preventDefault();
        autosaveDisabled=true;
        clearTimeout(autoSaveTimer);
        if (savePromise) await savePromise;
        persist();
        for(const key of keys)for(const q of packageData.sets[key].questions){
            if(!q.id||!/^[a-zA-Z0-9_-]{1,100}$/.test(q.id)||!q.question.trim()){
                autosaveDisabled=false;alert(`Check question ID and text in ${key}: ${q.id}`);return;
            }
        }
        $('questionsJson').value=JSON.stringify(packageData);
        dirty=false;window.questionEditorDirty=false;
        HTMLFormElement.prototype.submit.call($('questionForm'));
    };
    window.addEventListener('beforeunload',e=>{if(dirty){e.preventDefault();e.returnValue='';}});
    render();
})();
