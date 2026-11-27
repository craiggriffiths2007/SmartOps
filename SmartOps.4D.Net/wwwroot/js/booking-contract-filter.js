(() => {
    function initializeContractFilter() {
        const filter = document.getElementById('ContractFilter');
        const select = document.getElementById('ContractId');
        const dataElement = document.getElementById('booking-contract-search-data');
        const summary = document.getElementById('ContractFilterSummary');
        if (!filter || !select || !dataElement || select.tagName !== 'SELECT') return;

        const searchValues = JSON.parse(dataElement.textContent);
        const contracts = Array.from(select.options).filter(option => option.value !== '')
            .map(option => ({
                option: option.cloneNode(true),
                number: String(searchValues[option.value].number),
                name: searchValues[option.value].name.toLocaleLowerCase()
            }));
        const normalizeNumber = value => value.replace(/^0+(?=\d)/, '');

        function updateContracts() {
            const query = filter.value.trim();
            const numericQuery = /^\d+$/.test(query);
            const normalizedQuery = numericQuery ? normalizeNumber(query) : query.toLocaleLowerCase();
            const matches = contracts.filter(contract => !query || (numericQuery
                ? normalizeNumber(contract.number) === normalizedQuery
                : contract.name.includes(normalizedQuery)));
            const selectedValue = select.value;
            const options = document.createDocumentFragment();
            const placeholder = document.createElement('option');
            placeholder.value = '';
            placeholder.textContent = matches.length ? '-- Select Contract --' : '-- No matching contracts --';
            options.append(placeholder);
            matches.forEach(contract => options.append(contract.option.cloneNode(true)));
            select.replaceChildren(options);
            select.value = matches.some(contract => contract.option.value === selectedValue) ? selectedValue : '';
            if (select.value !== selectedValue) select.dispatchEvent(new Event('change', { bubbles: true }));
            if (summary) summary.textContent = query
                ? `${matches.length} matching contract${matches.length === 1 ? '' : 's'}` : '';
        }

        filter.addEventListener('input', updateContracts);
        updateContracts();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initializeContractFilter);
    else initializeContractFilter();
})();