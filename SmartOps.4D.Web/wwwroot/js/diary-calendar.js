(() => {
    let disposeCalendar = () => {};
    function initializeDiaryCalendar() {
        disposeCalendar();
        const listeners = new AbortController();
        function listen(target, type, callback, capture = false) {
            target.addEventListener(type, callback, { capture, signal: listeners.signal });
        }

        const branchSelect = document.getElementById("diaryBranch");
        if (branchSelect) {
            listen(branchSelect, "change", () => branchSelect.form.requestSubmit());
        }

            let openPopup = null;
            let closeTimer = null;



            function resizeDayHeadings() {
    document.querySelectorAll(".week-day-heading").forEach(function (heading) {

        const name = heading.querySelector(".week-day-name");

        if (!name) {
            return;
        }

        // Start by trying the full name.
        name.textContent = name.dataset.full;

        // If the contents don't fit, use the three-letter version.
        if (heading.scrollWidth > heading.clientWidth) {
            name.textContent = name.dataset.short;
        }
    });
}

resizeDayHeadings();

listen(window, "resize", resizeDayHeadings);


            function buttonsFor(targetId) {
                return document.querySelectorAll(
                    '[data-diary-target="' + targetId + '"]'
                );
            }

            function setExpanded(targetId, expanded) {
                buttonsFor(targetId).forEach(function (button) {
                    button.setAttribute(
                        "aria-expanded",
                        expanded ? "true" : "false"
                    );
                });
            }

            function positionPopup() {
                if (!openPopup) {
                    return;
                }

                const booking = openPopup.booking;
                const popup = openPopup.popup;

                if (!document.body.contains(booking)) {
                    closePopup();
                    return;
                }

                const rect = booking.getBoundingClientRect();
                const margin = 8;
                const gap = 4;

                const popupWidth = popup.offsetWidth;
                const popupHeight = popup.offsetHeight;

                let left = rect.left;
                let top = rect.bottom + gap;

                // Keep the panel on screen horizontally.
                if (left + popupWidth > window.innerWidth - margin) {
                    left = window.innerWidth - popupWidth - margin;
                }

                if (left < margin) {
                    left = margin;
                }

                // Prefer below the booking. If there is not enough room,
                // show above it where possible.
                if (top + popupHeight > window.innerHeight - margin) {
                    const above = rect.top - popupHeight - gap;

                    if (above >= margin) {
                        top = above;
                    } else {
                        top = Math.max(
                            margin,
                            window.innerHeight - popupHeight - margin
                        );
                    }
                }

                top = Math.max(margin, top - 8);

                popup.style.left = Math.round(left) + "px";
                popup.style.top = Math.round(top) + "px";
            }

            function closePopup() {
                clearTimeout(closeTimer);
                if (!openPopup) {
                    return;
                }

                const popup = openPopup.popup;
                const targetId = popup.id;

                popup.hidden = true;
                popup.classList.remove("show");

                // Return the details block to its original place in the booking.
                if (openPopup.placeholder.parentNode) {
                    openPopup.placeholder.parentNode.insertBefore(
                        popup,
                        openPopup.placeholder
                    );

                    openPopup.placeholder.remove();
                }

                popup.style.left = "";
                popup.style.top = "";
                popup.style.backgroundColor = "";
                popup.style.color = "";

                openPopup.booking.classList.remove("expanded");

                setExpanded(targetId, false);

                openPopup = null;
            }

            function openDetails(button) {
                const targetId = button.getAttribute("data-diary-target");

                if (!targetId) {
                    return;
                }

                clearTimeout(closeTimer);
                if (openPopup && openPopup.popup.id === targetId) {
                    return;
                }

                closePopup();

                const popup = document.getElementById(targetId);
                const booking = button.closest(".diary-booking");

                if (!popup || !booking) {
                    return;
                }

                const bookingStyle = window.getComputedStyle(booking);

                const placeholder = document.createComment(
                    "diary-booking-extra-placeholder"
                );

                popup.parentNode.insertBefore(placeholder, popup);

                // Capture the booking colours before moving the panel to <body>.
                popup.style.backgroundColor = bookingStyle.backgroundColor;
                popup.style.color = bookingStyle.color;

                document.body.appendChild(popup);

                popup.hidden = false;
                popup.classList.add("show");

                booking.classList.add("expanded");
                setExpanded(targetId, true);

                openPopup = {
                    popup: popup,
                    booking: booking,
                    placeholder: placeholder
                };

                // Wait until the browser has measured the floating panel.
                requestAnimationFrame(positionPopup);
            }

            function cancelClose() {
                clearTimeout(closeTimer);
            }

            function scheduleClose() {
                cancelClose();
                // Allow the pointer to cross the small gap into the floating panel.
                closeTimer = setTimeout(function () {
                    if (!openPopup) return;
                    const active = document.activeElement;
                    const hasKeyboardFocus = active && active.matches(":focus-visible") &&
                        (openPopup.booking.contains(active) || openPopup.popup.contains(active));
                    if (openPopup.booking.matches(":hover") ||
                        openPopup.popup.matches(":hover") || hasKeyboardFocus) return;
                    closePopup();
                }, 180);
            }

            document.querySelectorAll(".diary-booking").forEach(function (booking) {
                const trigger = booking.querySelector("[data-diary-target]");
                if (!trigger) return;
                const popup = document.getElementById(trigger.getAttribute("data-diary-target"));
                if (!popup) return;

                booking.addEventListener("pointerenter", function (event) {
                    if (event.pointerType !== "touch") openDetails(trigger);
                });
                booking.addEventListener("pointerleave", scheduleClose);
                popup.addEventListener("pointerenter", cancelClose);
                popup.addEventListener("pointerleave", scheduleClose);

                booking.addEventListener("focusin", function (event) {
                    if (event.target.matches(":focus-visible")) openDetails(trigger);
                });
                booking.addEventListener("focusout", scheduleClose);
                popup.addEventListener("focusin", cancelClose);
                popup.addEventListener("focusout", scheduleClose);
            });

            // Close when clicking anywhere outside the booking or floating details.
            listen(document, "click", function (event) {
                if (!openPopup) {
                    return;
                }

                if (openPopup.popup.contains(event.target) ||
                    openPopup.booking.contains(event.target)) {
                    return;
                }

                closePopup();
            });

            // Escape closes the floating details.
            listen(document, "keydown", function (event) {
                if (event.key === "Escape") {
                    closePopup();
                }
            });

            // Reposition when the page or an individual week cell scrolls.
            listen(document, 
                "scroll",
                function () {
                    if (openPopup) {
                        positionPopup();
                    }
                },
                true
            );

            listen(window, "resize", positionPopup);
        

    document.querySelectorAll(".day-booking-card[data-booking-url]").forEach(card => {
        const openBooking = () => window.location.assign(card.dataset.bookingUrl);
        card.addEventListener("click", event => {
            if (event.target.closest("a, button, input, select, textarea, .diary-received-status")) return;
            openBooking();
        });
        card.addEventListener("keydown", event => {
            if (event.target !== card) return;
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                openBooking();
            }
        });
    });


        disposeCalendar = () => { closePopup(); listeners.abort(); };
    }
    document.addEventListener('DOMContentLoaded', initializeDiaryCalendar);
    document.addEventListener('diary:updated', initializeDiaryCalendar);
})();
