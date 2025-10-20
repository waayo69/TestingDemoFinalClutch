document.addEventListener("DOMContentLoaded", () => {
    const sidebar = document.getElementById("sidebarMenu");

    if (sidebar) {
        sidebar.addEventListener("mouseleave", () => {
            // Close all open dropdown menus
            document.querySelectorAll(".dropdown-menu.show").forEach(menu => {
                menu.classList.remove("show");
            });

            // Reset toggle buttons
            document.querySelectorAll(".dropdown-toggle.show").forEach(toggle => {
                toggle.classList.remove("show");
                toggle.setAttribute("aria-expanded", "false");
            });
        });
    }
});
document.addEventListener("DOMContentLoaded", function () {
    const body = document.body;
    const toggleBtn = document.getElementById("darkModeToggle");

    // Load dark mode setting from localStorage
    if (localStorage.getItem("darkMode") === "enabled") {
        body.classList.add("dark-mode");
    }

    // Toggle dark mode on button click
    if (toggleBtn) {
        toggleBtn.addEventListener("click", function () {
            body.classList.toggle("dark-mode");

            // Save preference
            if (body.classList.contains("dark-mode")) {
                localStorage.setItem("darkMode", "enabled");
            } else {
                localStorage.setItem("darkMode", "disabled");
            }
        });
    }
});

