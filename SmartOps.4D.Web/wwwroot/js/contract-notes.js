const contractNotes = (() => {
    const prefix = "quill-note-v1:";
    const maxLength = 4000;
    const inlineFormats = ["bold", "italic", "underline", "strike"];

    function createEditor(element) {
        return new Quill(element, {
            theme: "snow",
            formats: [...inlineFormats, "color", "list"],
            modules: {
                toolbar: [
                    [...inlineFormats, { color: [] }],
                    [{ list: "ordered" }, { list: "bullet" }],
                    ["clean"]
                ]
            }
        });
    }

    function readEditor(editor) {
        const text = editor.getText().slice(0, -1);
        if (!text.trim()) return "";
        const { ops } = editor.getContents();
        const formatted = ops.some(op => op.attributes);
        return formatted || text.startsWith(prefix) ? prefix + JSON.stringify(ops) : text;
    }

    function render(element, note) {
        element.textContent = note;
        if (!note.startsWith(prefix)) return;

        let ops;
        try {
            ops = JSON.parse(note.slice(prefix.length));
        } catch {
            return;
        }

        if (!Array.isArray(ops) || !ops.every(op => op && typeof op.insert === "string")) return;

        const safeOps = ops.map(({ insert, attributes }) => {
            const formats = {};
            for (const format of inlineFormats) {
                if (attributes?.[format] === true) formats[format] = true;
            }
            if (typeof attributes?.color === "string" && /^#[0-9a-f]{6}$/i.test(attributes.color)) {
                formats.color = attributes.color;
            }
            if (attributes?.list === "ordered" || attributes?.list === "bullet") {
                formats.list = attributes.list;
            }
            return { insert, attributes: formats };
        });

        element.replaceChildren();
        const viewer = new Quill(element, {
            theme: "snow",
            readOnly: true,
            modules: { toolbar: false }
        });
        viewer.setContents(safeOps);
    }

    return { createEditor, readEditor, render, maxLength };
})();

const noteForm = document.querySelector("[data-contract-note-form]");
if (noteForm) {
    const input = noteForm.querySelector("[name=Note]");
    const editor = contractNotes.createEditor(noteForm.querySelector("[data-note-editor]"));
    const error = noteForm.querySelector("[data-note-error]");
    if (input.value) editor.setText(input.value);
    input.hidden = true;

    document.querySelectorAll("[data-contract-note]").forEach(element => {
        contractNotes.render(element, element.textContent);
    });

    noteForm.addEventListener("submit", event => {
        const note = contractNotes.readEditor(editor);
        if (!note || note.length > contractNotes.maxLength) {
            event.preventDefault();
            error.textContent = note ? "This note is too long. Shorten the text or remove some formatting." : "Enter a note.";
            editor.focus();
            return;
        }
        input.value = note;
        error.textContent = "";
    });
}
