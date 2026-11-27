(() => {
    function initializeCustomerChoice() {
        const existingFields = document.getElementById('ExistingCustomerFields');
        const newFields = document.getElementById('NewCustomerFields');
        const modes = document.querySelectorAll('input[name="createNewCustomer"]');
        const filter = document.getElementById('CustomerFilter');
        const select = document.getElementById('CustomerId');
        const data = document.getElementById('contract-customer-search-data');
        const summary = document.getElementById('CustomerFilterSummary');
        if (!existingFields || !newFields || !filter || !select || !data) return;
        const searchValues = JSON.parse(data.textContent);
        const customers = Array.from(select.options).filter(option => option.value)
            .map(option => ({ option: option.cloneNode(true), search: searchValues[option.value].toLocaleLowerCase() }));

        function updateCustomerMode() {
            const useNew = Array.from(modes).some(mode => mode.checked && mode.value === 'true');
            existingFields.hidden = useNew;
            existingFields.disabled = useNew;
            newFields.hidden = !useNew;
            newFields.disabled = !useNew;
        }

        function filterCustomers() {
            const query = filter.value.trim().toLocaleLowerCase();
            const matches = customers.filter(customer => !query || customer.search.includes(query));
            const selected = select.value;
            const options = document.createDocumentFragment();
            const placeholder = document.createElement('option');
            placeholder.value = '';
            placeholder.textContent = matches.length ? '-- Select Customer --' : '-- No matching customers --';
            options.append(placeholder);
            matches.forEach(customer => options.append(customer.option.cloneNode(true)));
            select.replaceChildren(options);
            select.value = matches.some(customer => customer.option.value === selected) ? selected : '';
            if (select.value !== selected) select.dispatchEvent(new Event('change', { bubbles: true }));
            summary.textContent = query ? `${matches.length} matching customer${matches.length === 1 ? '' : 's'}` : '';
        }
        modes.forEach(mode => mode.addEventListener('change', updateCustomerMode));
        filter.addEventListener('input', filterCustomers);
        updateCustomerMode();
        filterCustomers();
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initializeCustomerChoice);
    else initializeCustomerChoice();
})();