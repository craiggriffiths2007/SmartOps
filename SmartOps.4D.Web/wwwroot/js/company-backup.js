(() => {
    'use strict';
    const button = document.getElementById('downloadCompanyData');
    const status = document.getElementById('companyBackupStatus');
    if (!button || !status) return;
    const showStatus = (message, error = false) => {
        status.hidden = false;
        status.textContent = message;
        status.className = 'small mb-3 ' + (error ? 'text-danger' : 'text-muted');
    };
    const pathParts = path => {
        if (typeof path !== 'string' || path.includes('\\') || path.includes(':'))
            throw Error('Invalid backup file path.');
        const parts = path.split('/');
        if (parts.some(part => !part || part === '.' || part === '..')) throw Error('Invalid backup file path.');
        return parts;
    };
    async function fileHandle(folder, path) {
        const parts = pathParts(path);
        for (const part of parts.slice(0, -1)) folder = await folder.getDirectoryHandle(part, {create: true});
        return folder.getFileHandle(parts.at(-1), {create: true});
    }
    async function writeJson(folder, name, data) {
        const stream = await (await fileHandle(folder, name)).createWritable();
        try { await stream.write(JSON.stringify(data, null, 2) + '\n'); await stream.close(); }
        catch (error) { await stream.abort().catch(() => {}); throw error; }
    }
    async function newBackupFolder(parent, name) {
        for (let suffix = 1; ; suffix++) {
            const candidate = suffix === 1 ? name : `${name}-${suffix}`;
            try { await parent.getDirectoryHandle(candidate); }
            catch (error) {
                if (error.name !== 'NotFoundError') throw error;
                return parent.getDirectoryHandle(candidate, {create: true});
            }
        }
    }
    async function downloadFile(folder, path, url, json = false) {
        const location = new URL(url, window.location.href);
        if (location.origin !== window.location.origin) throw Error('Invalid backup download location.');
        const response = await fetch(location, {credentials: 'same-origin', cache: 'no-store'});
        if (!response.ok || response.redirected || !response.body)
            throw Error(`Could not download ${path}. Please check your connection and sign-in.`);
        if (json && !response.headers.get('content-type')?.includes('application/json'))
            throw Error(`Invalid table response for ${path}.`);
        const handle = await fileHandle(folder, path);
        // Stream table rows and large videos directly to disk.
        await response.body.pipeTo(await handle.createWritable());
    }
    button.onclick = async () => {
        let completed = 0;
        let backupFolder;
        button.disabled = true;
        try {
            if (!window.showDirectoryPicker)
                throw Error('Folder download requires Edge or Chrome over HTTPS (or localhost).');
            const parent = await window.showDirectoryPicker({id: 'smartops-company-backup', mode: 'readwrite'});
            showStatus('Preparing your company data and media…');
            const response = await fetch(button.dataset.manifestUrl, {credentials: 'same-origin', cache: 'no-store'});
            if (!response.ok || response.redirected) throw Error('Could not prepare the backup. Check your sign-in and try again.');
            const manifest = await response.json();
            const now = new Date();
            const pad = value => String(value).padStart(2, '0');
            const timestamp = [pad(now.getDate()), pad(now.getMonth() + 1), now.getFullYear(),
                pad(now.getHours()), pad(now.getMinutes())].join('-');
            backupFolder = await newBackupFolder(parent, `SmartOps-Backup-${timestamp}`);
            const mediaFolder = await backupFolder.getDirectoryHandle('media', {create: true});
            const record = {...manifest, startedAtUtc: now.toISOString(), completed: false};
            await writeJson(backupFolder, 'backup.json', record);
            const total = manifest.tables.length + manifest.media.length;
            for (const table of manifest.tables) {
                showStatus(`Downloading ${completed + 1} of ${total}: ${table.fileName}`);
                await downloadFile(backupFolder, table.fileName, table.url, true);
                completed++;
            }
            for (const file of manifest.media) {
                showStatus(`Downloading ${completed + 1} of ${total}: media/${file.relativePath}`);
                await downloadFile(mediaFolder, file.relativePath, file.url);
                completed++;
            }
            record.completed = true;
            record.completedAtUtc = new Date().toISOString();
            await writeJson(backupFolder, 'backup.json', record);
            showStatus(`Backup complete: ${manifest.tables.length} table files and ${manifest.media.length} media files saved in ${backupFolder.name}.`);
        } catch (error) {
            if (error.name === 'AbortError' && !backupFolder) showStatus('Download cancelled.');
            else showStatus(`Backup ${backupFolder ? 'incomplete' : 'failed'} (${completed} files saved): ${error.message}`, true);
        } finally { button.disabled = false; }
    };
})();
