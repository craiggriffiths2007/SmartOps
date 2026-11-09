(() => {
    const insurer = document.getElementById('InsurerId');
    const policy = document.getElementById('InsurancePolicyFields');
    if (!insurer || !policy) return;
    const update = () => { policy.disabled = insurer.value === ''; };
    insurer.addEventListener('change', update);
    update();
})();
