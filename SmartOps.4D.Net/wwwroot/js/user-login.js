document.querySelectorAll('[data-generate-password]').forEach(button => {
    button.addEventListener('click', async () => {
        const form = button.closest('form');
        const input = form.elements.namedItem(button.dataset.passwordInput);
        const status = form.querySelector('[data-password-status]');
        button.disabled = true;
        status.textContent = '';
        try {
            const response = await fetch(button.dataset.url, {
                method: 'POST',
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { RequestVerificationToken: form.elements.namedItem('__RequestVerificationToken').value }
            });
            if (!response.ok || response.redirected) throw new Error('Password generation failed.');
            const data = await response.json();
            if (typeof data.password !== 'string' || !data.password) throw new Error('Password generation failed.');
            input.value = data.password;
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
        } catch {
            status.textContent = 'Could not generate a password. Please try again.';
        } finally {
            button.disabled = false;
        }
    });
});

// Fill the emailed credentials once, without putting the password in the address bar.
(async () => {
    const form = document.querySelector('[data-login-token]');
    if (!form?.dataset.loginToken) return;
    const token = form.dataset.loginToken;
    delete form.dataset.loginToken;
    const url = new URL(window.location.href);
    url.searchParams.delete('loginToken');
    window.history.replaceState(null, '', url.pathname + url.search + url.hash);
    const status = form.querySelector('[data-login-prefill-status]');
    const username = form.elements.namedItem('Input.Email');
    const password = form.elements.namedItem('Input.Password');
    const submit = form.querySelector('[type="submit"]');
    let edited = false;
    const markEdited = () => { edited = true; };
    username.addEventListener('input', markEdited);
    password.addEventListener('input', markEdited);
    submit.disabled = true;
    status.textContent = 'Loading your login details…';
    try {
        const response = await fetch(form.dataset.loginPrefillUrl, {
            method: 'POST', credentials: 'same-origin', cache: 'no-store',
            headers: { RequestVerificationToken: form.elements.namedItem('__RequestVerificationToken').value },
            body: new URLSearchParams({ loginToken: token })
        });
        if (!response.ok || response.redirected) throw new Error('Login link unavailable.');
        const data = await response.json();
        if (typeof data.username !== 'string' || !data.username ||
            typeof data.password !== 'string' || !data.password) throw new Error('Login link unavailable.');
        if (!edited) {
            username.value = data.username;
            password.value = data.password;
            for (const input of [username, password]) {
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }
        }
        status.textContent = edited ? 'Your entered details have been kept.' : 'Your login details are filled in. Press Login to continue.';
    } catch {
        status.textContent = 'This login link is expired or unavailable. Enter the username and password from your email.';
    } finally {
        username.removeEventListener('input', markEdited);
        password.removeEventListener('input', markEdited);
        submit.disabled = false;
    }
})();
