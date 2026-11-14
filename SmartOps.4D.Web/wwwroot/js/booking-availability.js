(() => {
    const section = document.getElementById('booking-availability');
    const agent = document.getElementById('AgentId');
    const date = document.getElementById('Job_DiaryDate');
    const time = document.getElementById('Job_Time');
    const duration = document.getElementById('Job_JobTime');
    if (!section || !agent || !date || !time || !duration) return;
    const status = section.querySelector('[data-availability-status]');
    const days = section.querySelector('[data-availability-days]');
    const previous = section.querySelector('[data-availability-previous]');
    const next = section.querySelector('[data-availability-next]');
    const form = document.getElementById('booking-form');
    const reservationInput = document.getElementById('booking-reservation-id');
    const reservationBanner = section.querySelector('[data-reservation-banner]');
    const reservationText = section.querySelector('[data-reservation-text]');
    let reservation = JSON.parse(section.dataset.reservation || 'null');
    let selectionVersion = 0;
    let reserving = 0;
    let firstDate = date.value;
    let request;
    let timer;
    const dateValue = value => new Date(value + 'T12:00:00');
    const isoDate = day => `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;

    function matchesSelection() {
        return reservation && Number(agent.value) === reservation.agentId && date.value === reservation.date &&
            time.value.substring(0, 5) === reservation.time && duration.value.substring(0, 5) === reservation.duration;
    }

    function showReservation() {
        reservationInput.value = reservation?.id || '';
        reservationBanner.hidden = !reservation;
        if (reservation) reservationText.textContent = `Reserved: ${reservation.date} ${reservation.time}–${reservation.end}. Held until ${new Date(reservation.expiresUtc).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}. Save to confirm the booking.`;
    }

    async function post(url, values, keepalive = false) {
        const body = new URLSearchParams(values);
        body.set('__RequestVerificationToken', form.querySelector('[name="__RequestVerificationToken"]').value);
        return fetch(url, { method: 'POST', body, keepalive });
    }

    function release(id) {
        return post(section.dataset.releaseUrl, { id }, true).catch(() => {});
    }

    function releaseCurrent() {
        if (!reservation) return Promise.resolve();
        const previous = reservation;
        reservation = null;
        showReservation();
        return release(previous.id);
    }

    function selectCurrent() {
        days.querySelectorAll('button').forEach(button => {
            const selected = button.dataset.date === date.value && button.dataset.time === time.value.substring(0, 5);
            const reserved = selected && matchesSelection();
            button.classList.toggle('btn-success', Boolean(reserved));
            button.classList.toggle('btn-outline-primary', !reserved);
            button.textContent = `${button.dataset.time}–${button.dataset.end}${reserved ? ' · Reserved' : ''}`;
            button.setAttribute('aria-pressed', String(selected));
        });
    }

    async function refresh() {
        request?.abort();
        request = new AbortController();
        const current = request;
        days.replaceChildren();
        if (!agent.value || !duration.value || !duration.validity.valid || duration.value === '00:00' || !firstDate || !date.validity.valid) {
            status.textContent = 'Select an agent, a valid date and time allocated to see available slots.';
            previous.disabled = next.disabled = true;
            return;
        }
        previous.disabled = next.disabled = true;
        section.setAttribute('aria-busy', 'true');
        status.textContent = 'Checking available slots…';
        const url = new URL(section.dataset.url, window.location.href);
        url.search = new URLSearchParams({ agentId: agent.value, date: firstDate, duration: duration.value });
        if (Number(section.dataset.jobId) > 0) url.searchParams.set('excludeJobId', section.dataset.jobId);
        if (reservation) url.searchParams.set('reservationId', reservation.id);
        try {
            const response = await fetch(url, { signal: current.signal });
            if (!response.ok || response.redirected) throw new Error('Unavailable');
            const result = await response.json();
            if (current.signal.aborted) return;
            firstDate = result[0].date;
            for (const day of result) {
                const row = document.createElement('div');
                row.className = 'border-bottom py-2';
                const heading = document.createElement('h6');
                heading.textContent = day.label;
                row.append(heading);
                const slots = document.createElement('div');
                slots.className = 'd-flex flex-wrap gap-2';
                if (!day.slots.length) {
                    const empty = document.createElement('span');
                    empty.className = 'text-muted small';
                    empty.textContent = 'No available slots for this duration.';
                    slots.append(empty);
                }
                for (const slot of day.slots) {
                    const button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'btn btn-sm btn-outline-primary';
                    button.dataset.date = day.date;
                    button.dataset.time = slot.time;
                    button.dataset.end = slot.end;
                    button.textContent = `${slot.time}–${slot.end}`;
                    button.setAttribute('aria-label', `${day.label}, ${slot.time} to ${slot.end}`);
                    slots.append(button);
                }
                row.append(slots);
                days.append(row);
            }
            status.textContent = `${agent.selectedOptions[0]?.textContent || 'Selected agent'} · ${result[0].label} – ${result[result.length - 1].label}`;
            previous.disabled = dateValue(firstDate) <= dateValue(section.dataset.firstDate);
            next.disabled = false;
            selectCurrent();
        } catch (error) {
            if (error.name !== 'AbortError') status.textContent = 'Available slots could not be loaded. Please change the date or agent to try again.';
        } finally {
            if (current === request) section.removeAttribute('aria-busy');
        }
    }

    function scheduleRefresh() {
        request?.abort();
        days.replaceChildren();
        clearTimeout(timer);
        timer = setTimeout(refresh, 150);
    }
    function detailsChanged() {
        selectionVersion++;
        if (reservation && !matchesSelection()) releaseCurrent().then(scheduleRefresh);
        else scheduleRefresh();
    }
    agent.addEventListener('change', detailsChanged);
    duration.addEventListener('input', detailsChanged);
    duration.addEventListener('change', detailsChanged);
    date.addEventListener('change', () => { firstDate = date.value; detailsChanged(); });
    time.addEventListener('input', detailsChanged);
    time.addEventListener('change', detailsChanged);
    new MutationObserver(detailsChanged).observe(agent, { childList: true });
    for (const [button, step] of [[previous, -7], [next, 7]]) button.addEventListener('click', () => {
        const day = dateValue(firstDate);
        day.setDate(day.getDate() + step);
        firstDate = isoDate(day);
        scheduleRefresh();
    });
    days.addEventListener('click', async event => {
        const button = event.target.closest('button[data-date]');
        if (!button) return;
        const version = selectionVersion;
        const values = { agentId: agent.value, date: button.dataset.date, time: button.dataset.time, duration: duration.value };
        if (Number(section.dataset.jobId) > 0) values.excludeJobId = section.dataset.jobId;
        if (reservation) values.previousReservationId = reservation.id;
        reserving++;
        form.querySelectorAll('[type="submit"]').forEach(save => save.disabled = true);
        days.querySelectorAll('button').forEach(slot => slot.disabled = true);
        status.textContent = 'Reserving this time…';
        try {
            const response = await post(section.dataset.reserveUrl, values);
            if (response.redirected) throw new Error('Please sign in again before reserving a time.');
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.error || 'This time could not be reserved. Please refresh the available slots.');
            if (version !== selectionVersion) { await release(result.id); return; }
            reservation = result;
            date.value = result.date;
            time.value = result.time;
            showReservation();
            date.dispatchEvent(new Event('change', { bubbles: true }));
            time.dispatchEvent(new Event('change', { bubbles: true }));
        } catch (error) {
            status.textContent = error.message || 'The time could not be reserved. Please try again.';
        } finally {
            reserving--;
            if (!reserving) form.querySelectorAll('[type="submit"]').forEach(save => save.disabled = false);
            days.querySelectorAll('button').forEach(slot => slot.disabled = false);
        }
    });
    section.querySelector('[data-release-reservation]').addEventListener('click', () => {
        selectionVersion++;
        releaseCurrent().then(scheduleRefresh);
    });
    document.querySelectorAll('[data-release-booking]').forEach(cancel => cancel.addEventListener('click', () => {
        selectionVersion++;
        releaseCurrent();
    }));
    setInterval(() => {
        if (reservation && new Date(reservation.expiresUtc).getTime() <= Date.now()) {
            reservation = null;
            showReservation();
            status.textContent = 'Your reservation has expired. Select an available time again.';
            scheduleRefresh();
        }
    }, 1000);
    showReservation();
    refresh();
})();
