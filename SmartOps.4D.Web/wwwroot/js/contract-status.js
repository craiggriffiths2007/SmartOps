(() => {
    const display = document.getElementById('contract-status-display');
    const form = document.getElementById('contract-status-form');
    const changeButton = document.querySelector('[data-change-contract-status]');
    if (!display || !form || !changeButton) return;

    const select = form.querySelector('[name="status"]');
    const originalStatus = select.value;
    changeButton.addEventListener('click', () => {
        display.hidden = true;
        form.hidden = false;
        changeButton.setAttribute('aria-expanded', 'true');
        select.focus();
    });
    form.querySelector('[data-cancel-contract-status]').addEventListener('click', () => {
        select.value = originalStatus;
        form.hidden = true;
        display.hidden = false;
        changeButton.setAttribute('aria-expanded', 'false');
        changeButton.focus();
    });
})();
