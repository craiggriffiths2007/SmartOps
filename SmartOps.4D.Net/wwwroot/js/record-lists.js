/* Independently paginate collections already loaded on a details page. */
document.querySelectorAll('.record-cards[data-paginate], tbody[data-paginate]').forEach(list => {
    const cards = [...list.children].filter(item => item.matches('.record-card, [data-record-row]'));
    const requestedSize = Number(list.dataset.pageSize);
    const size = Number.isInteger(requestedSize) && requestedSize > 0 ? requestedSize : 10;
    const totalPages = Math.ceil(cards.length / size);
    if (!cards.length) {
        if (list.matches('tbody')) return;
        const empty = document.createElement('p');
        empty.className = 'text-muted';
        empty.textContent = 'No records found.';
        list.append(empty);
        return;
    }
    const controls = document.createElement('div');
    controls.className = 'record-pagination';
    const summary = document.createElement('p');
    summary.className = 'text-muted small';
    summary.setAttribute('aria-live', 'polite');
    const nav = document.createElement('nav');
    nav.setAttribute('aria-label', list.dataset.pageLabel || 'Related record pages');
    controls.append(summary, nav);
    const anchor = list.matches('tbody') ? list.closest('.table-responsive') || list.closest('table') : list;
    anchor.after(controls);
    let current = 1;
    function render(moveFocus = false) {
        cards.forEach((card, index) => {
            card.hidden = index < (current - 1) * size || index >= current * size;
        });
        summary.textContent = `Showing ${(current - 1) * size + 1}–${Math.min(current * size, cards.length)} of ${cards.length}`;
        nav.replaceChildren();
        if (totalPages <= 1) return;
        const pages = document.createElement('ul');
        pages.className = 'pagination flex-wrap';
        function add(label, target, disabled = false) {
            const item = document.createElement('li');
            item.className = 'page-item';
            if (disabled) item.classList.add('disabled');
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'page-link';
            button.textContent = label;
            button.disabled = disabled;
            if (target === current && /^\d+$/.test(label)) {
                item.classList.add('active');
                button.setAttribute('aria-current', 'page');
            }
            button.addEventListener('click', () => { current = target; render(true); });
            item.append(button);
            pages.append(item);
        }
        add('Previous', current - 1, current === 1);
        const visible = [...new Set([1, totalPages, current - 2, current - 1, current, current + 1, current + 2])]
            .filter(page => page >= 1 && page <= totalPages).sort((a, b) => a - b);
        let previous = 0;
        visible.forEach(page => {
            if (previous && page > previous + 1) add('…', 0, true);
            add(String(page), page);
            previous = page;
        });
        add('Next', current + 1, current === totalPages);
        nav.append(pages);
        if (moveFocus) nav.querySelector('[aria-current="page"]').focus();
    }
    render();
});

// Pagination links can reopen the message tab they belong to.
if (location.hash && window.bootstrap) {
    const tab = [...document.querySelectorAll('[data-bs-toggle="tab"]')]
        .find(item => item.dataset.bsTarget === location.hash);
    if (tab) bootstrap.Tab.getOrCreateInstance(tab).show();
}
