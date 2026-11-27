(() => {
    const form = document.getElementById('estimate-form');
    if (!form) return;
    const lines = document.getElementById('estimate-lines');
    const addButton = document.getElementById('add-estimate-line');
    const vatRate = document.getElementById('VatRate');
    const currency = new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' });
    // Integer pence and hundredths of a percent keep the preview aligned with server rounding.
    const hundredths = value => {
        const match = /^(\d+)(?:\.(\d{0,2}))?$/.exec(value);
        return match ? BigInt(match[1]) * 100n + BigInt((match[2] || '').padEnd(2, '0')) : 0n;
    };
    const money = value => currency.format(Number(value) / 100);
    function update() {
        let subtotal = 0n;
        lines.querySelectorAll('.estimate-cost').forEach(input => subtotal += hundredths(input.value));
        const rate = hundredths(vatRate.value);
        const vat = (subtotal * rate + 5000n) / 10000n;
        document.getElementById('estimate-subtotal').textContent = money(subtotal);
        document.getElementById('estimate-vat-rate').textContent = `(${vatRate.value || '0'}%)`;
        document.getElementById('estimate-vat').textContent = money(vat);
        document.getElementById('estimate-total').textContent = money(subtotal + vat);
        addButton.disabled = lines.children.length >= 100;
    }
    function reindex() {
        [...lines.children].forEach((row, index) => {
            const fields = [row.querySelector('textarea'), row.querySelector('input')];
            ['Details', 'Cost'].forEach((property, fieldIndex) => {
                const field = fields[fieldIndex];
                field.name = `Lines[${index}].${property}`;
                field.id = `Lines_${index}__${property}`;
                row.querySelectorAll('label')[fieldIndex].htmlFor = field.id;
            });
        });
    }
    addButton.addEventListener('click', () => {
        if (lines.children.length >= 100) return;
        lines.append(document.getElementById('estimate-line-template').content.cloneNode(true));
        reindex();
        lines.lastElementChild.querySelector('textarea').focus();
        update();
    });
    lines.addEventListener('click', event => {
        const button = event.target.closest('[data-remove-line]');
        if (!button) return;
        button.closest('tr').remove();
        if (!lines.children.length) {
            lines.append(document.getElementById('estimate-line-template').content.cloneNode(true));
        }
        reindex();
        update();
    });
    form.addEventListener('input', update);
    reindex();
    update();
})();
