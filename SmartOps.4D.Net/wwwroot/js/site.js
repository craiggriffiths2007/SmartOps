// Keep table rows accessible after removing the first-column links.
document.addEventListener("keydown", function(e) {
    if (e.key !== "Enter") return;
    const row = e.target.closest(".record-index-table .clickable-row");
    if (!row || e.target !== row) return;
    const url = row.getAttribute("data-href");
    if (url) {
        e.preventDefault();
        window.location.href = url;
    }
});

// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


function printMain() {
    setTimeout(function() {
        window.print();
    }, 50);
}

document.addEventListener("DOMContentLoaded", function() {
    document.body.addEventListener("click", function(e) {

        const row = e.target.closest(".clickable-row");

        if (row && !e.target.closest("a, button")) {
            const url = row.getAttribute("data-href");

            if (url) {
                window.location.href = url;
            }
        }
    });
});


document.addEventListener("DOMContentLoaded", function () {

    const lightbox = document.getElementById("imageLightbox");
    const lightboxImage = document.getElementById("imageLightboxImage");

    if (!lightbox || !lightboxImage)
        return;

    let images = [];
    let currentIndex = 0;

    function openImage(index) {
        if (images.length === 0)
            return;

        currentIndex = index;

        lightboxImage.src = images[currentIndex];
        lightbox.classList.add("open");
    }

    function closeImage() {
        lightbox.classList.remove("open");
        lightboxImage.src = "";
    }

    function previousImage() {
        currentIndex--;

        if (currentIndex < 0)
            currentIndex = images.length - 1;

        openImage(currentIndex);
    }

    function nextImage() {
        currentIndex++;

        if (currentIndex >= images.length)
            currentIndex = 0;

        openImage(currentIndex);
    }

    document.addEventListener("click", function (e) {

        const link = e.target.closest(".image-lightbox-link");

        if (link) {
            e.preventDefault();

            const links = Array.from(
                document.querySelectorAll(".image-lightbox-link"));

            images = links.map(x => x.href);

            currentIndex = links.indexOf(link);

            openImage(currentIndex);

            return;
        }

        if (e.target.closest(".image-lightbox-close")) {
            closeImage();
            return;
        }

        if (e.target.closest(".image-lightbox-prev")) {
            previousImage();
            return;
        }

        if (e.target.closest(".image-lightbox-next")) {
            nextImage();
            return;
        }

        if (e.target === lightbox) {
            closeImage();
        }
    });

    document.addEventListener("keydown", function (e) {

        if (!lightbox.classList.contains("open"))
            return;

        if (e.key === "Escape")
            closeImage();

        if (e.key === "ArrowLeft")
            previousImage();

        if (e.key === "ArrowRight")
            nextImage();
    });

});

// Load the dedicated print layout without leaving the current record.
document.addEventListener("click", function (event) {
    const link = event.target.closest("a[data-print-document]");
    if (!link || event.defaultPrevented || event.button !== 0 ||
        event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;

    event.preventDefault();
    if (document.getElementById("print-document-frame")) return;

    // Edge attaches print preview to the window hosting the print document.
    const popup = /\bEdg\//.test(navigator.userAgent)
        ? window.open("", "_blank", "popup,width=1000,height=800")
        : null;
    const printDocument = popup?.document ?? document;
    if (popup) printDocument.title = "Print document";

    const frame = printDocument.createElement("iframe");
    frame.id = "print-document-frame";
    frame.title = "Print document";
    frame.tabIndex = -1;
    frame.style.cssText = "position: fixed; inset: 0; width: 100%; height: 100%; border: 0;";
    if (!popup) {
        frame.setAttribute("aria-hidden", "true");
        frame.style.opacity = "0";
        frame.style.pointerEvents = "none";
    }

    const removeFrame = () => {
        frame.remove();
        popup?.close();
    };
    frame.addEventListener("load", function () {
        const printWindow = frame.contentWindow;
        if (!frame.contentDocument?.body?.hasAttribute("data-print-ready")) {
            removeFrame();
            window.alert("The print document could not be loaded.");
            return;
        }

        printWindow.addEventListener("afterprint", removeFrame, { once: true });
        printWindow.print();
    }, { once: true });

    frame.src = link.href;
    printDocument.body.appendChild(frame);
});
