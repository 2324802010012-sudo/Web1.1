// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener("DOMContentLoaded", () => {
    const revealTargets = document.querySelectorAll([
        ".role-panel",
        ".role-stats article",
        ".role-feature-grid article",
        ".feature-card",
        ".club-card",
        ".mentor-profile-card",
        ".student-card",
        ".schedule-timeline article",
        ".request-progress-list article",
        ".manager-member-row",
        ".mentor-application-list article",
        ".one-resource-card",
        ".one-rank-card"
    ].join(","));

    if ("IntersectionObserver" in window) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, { threshold: 0.08 });

        revealTargets.forEach((item, index) => {
            item.classList.add("ui-reveal");
            item.style.transitionDelay = `${Math.min(index % 8, 7) * 35}ms`;
            observer.observe(item);
        });
    } else {
        revealTargets.forEach((item) => item.classList.add("is-visible"));
    }

    document.querySelectorAll(".btn, .account-primary, .auth-submit, .social-button, .auth-social").forEach((button) => {
        button.addEventListener("click", (event) => {
            const rect = button.getBoundingClientRect();
            const ripple = document.createElement("span");
            ripple.className = "ui-ripple";
            ripple.style.left = `${event.clientX - rect.left}px`;
            ripple.style.top = `${event.clientY - rect.top}px`;
            ripple.style.width = ripple.style.height = `${Math.max(rect.width, rect.height)}px`;
            button.appendChild(ripple);
            window.setTimeout(() => ripple.remove(), 600);
        });
    });
});
