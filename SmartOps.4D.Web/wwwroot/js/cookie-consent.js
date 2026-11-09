
(() => {
    const cookieName = "smartops_cookie_consent";

    function getPreference() {
        const cookie = document.cookie
            .split("; ")
            .find(x => x.startsWith(cookieName + "="));

        return cookie
            ? decodeURIComponent(cookie.split("=")[1])
            : null;
    }

    function savePreference(value) {
        document.cookie =
            `${cookieName}=${encodeURIComponent(value)}; ` +
            "Max-Age=15552000; Path=/; SameSite=Lax" +
            (location.protocol === "https:" ? "; Secure" : "");

        document.getElementById("cookie-banner").hidden = true;

        // Hook optional analytics into this function.
        // Do not load tracking scripts before consent.
        if (value === "all") {
            enableOptionalAnalytics();
        }
    }

    function enableOptionalAnalytics() {
        // Load analytics here only if configured.
    }

    document.addEventListener("DOMContentLoaded", () => {
        const banner = document.getElementById("cookie-banner");
        const preference = getPreference();

        if (!["all", "essential"].includes(preference)) {
            banner.hidden = false;
        } else if (preference === "all") {
            enableOptionalAnalytics();
        }

        document.querySelectorAll("[data-cookie-choice]")
            .forEach(button => {
                button.addEventListener("click", () => {
                    savePreference(button.dataset.cookieChoice);
                });
            });

        document.getElementById("cookie-settings")
            ?.addEventListener("click", () => {
                banner.hidden = false;
            });
    });
})();
