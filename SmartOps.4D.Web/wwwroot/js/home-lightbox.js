/* Mart Ops home page - screenshot lightbox.
   Galleries are built from the DOM, so adding a screenshot only means adding markup:
   - [data-lightbox-item]  : an image in a gallery (an <a href>, or an element with data-src)
   - [data-lightbox-open]  : an element that opens a gallery at data-index (default 0)
   Both use data-gallery="<key>". Items are shown in DOM order. */
(function () {
    'use strict';

    var lightbox = document.getElementById('screenshotLightbox');
    if (!lightbox) return;

    var imageEl = document.getElementById('screenshotLightboxImage');
    var counterEl = document.getElementById('screenshotLightboxCounter');
    var closeBtn = document.getElementById('screenshotLightboxClose');
    var prevBtn = document.getElementById('screenshotLightboxPrev');
    var nextBtn = document.getElementById('screenshotLightboxNext');

    // ---- Build galleries from the page ----
    var galleries = {};
    document.querySelectorAll('[data-lightbox-item]').forEach(function (el) {
        var key = el.dataset.gallery;
        if (!key) return;
        var innerImg = el.querySelector('img');
        (galleries[key] = galleries[key] || []).push({
            el: el,
            src: el.dataset.src || el.getAttribute('href'),
            alt: el.dataset.alt || (innerImg ? innerImg.alt : '')
        });
    });

    var active = [];
    var current = 0;
    var lastFocused = null;

    function show(index) {
        if (!active.length) return;
        if (index < 0) index = active.length - 1;
        if (index >= active.length) index = 0;
        current = index;

        imageEl.src = active[current].src;
        imageEl.alt = active[current].alt;
        counterEl.textContent = (current + 1) + ' / ' + active.length;

        // Warm the cache for neighbours so arrow-key browsing feels instant
        [current - 1, current + 1].forEach(function (i) {
            var n = (i + active.length) % active.length;
            if (n !== current) new Image().src = active[n].src;
        });
    }

    function open(key, index, trigger) {
        active = galleries[key] || [];
        if (!active.length) return;

        var single = active.length < 2;
        prevBtn.hidden = single;
        nextBtn.hidden = single;
        counterEl.hidden = single;

        lastFocused = trigger || document.activeElement;
        show(index || 0);

        lightbox.classList.add('show');
        lightbox.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden';
        closeBtn.focus();
    }

    function close() {
        lightbox.classList.remove('show');
        lightbox.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';
        imageEl.removeAttribute('src');
        if (lastFocused && lastFocused.focus) lastFocused.focus();
    }

    function isOpen() {
        return lightbox.classList.contains('show');
    }

    // ---- Opening: one delegated listener for every trigger on the page ----
    document.addEventListener('click', function (event) {
        var trigger = event.target.closest('[data-gallery]');
        if (!trigger || lightbox.contains(trigger)) return;

        var items = galleries[trigger.dataset.gallery];
        if (!items) return;

        event.preventDefault();

        var index = items.findIndex(function (item) { return item.el === trigger; });
        if (index < 0) index = parseInt(trigger.dataset.index || '0', 10) || 0;

        open(trigger.dataset.gallery, index, trigger);
    });

    // ---- Controls ----
    closeBtn.addEventListener('click', close);
    prevBtn.addEventListener('click', function () { show(current - 1); });
    nextBtn.addEventListener('click', function () { show(current + 1); });

    lightbox.addEventListener('click', function (event) {
        if (event.target === lightbox) close();
    });

    document.addEventListener('keydown', function (event) {
        if (!isOpen()) return;

        if (event.key === 'Escape') close();
        else if (event.key === 'ArrowLeft') show(current - 1);
        else if (event.key === 'ArrowRight') show(current + 1);
        else if (event.key === 'Tab') {
            // Keep keyboard focus inside the dialog
            var focusable = [closeBtn, prevBtn, nextBtn].filter(function (b) { return !b.hidden; });
            var first = focusable[0];
            var last = focusable[focusable.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }
    });

    // ---- Swipe on touch screens ----
    var touchStartX = null;
    lightbox.addEventListener('touchstart', function (event) {
        touchStartX = event.changedTouches[0].clientX;
    }, { passive: true });
    lightbox.addEventListener('touchend', function (event) {
        if (touchStartX === null || active.length < 2) return;
        var dx = event.changedTouches[0].clientX - touchStartX;
        touchStartX = null;
        if (Math.abs(dx) > 50) show(current + (dx < 0 ? 1 : -1));
    }, { passive: true });
})();
