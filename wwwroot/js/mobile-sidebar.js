// Shared off-canvas sidebar toggle for Student/Staff portal pages.
// Works with either .admin-sidebar or .staff-sidebar, whichever is present on the page.
document.addEventListener('DOMContentLoaded', function () {
    var toggleBtns = document.querySelectorAll('.mobile-nav-toggle');
    var sidebar = document.querySelector('.admin-sidebar, .staff-sidebar');
    var backdrop = document.querySelector('.sidebar-backdrop');

    if (!sidebar) return;

    function openDrawer() {
        sidebar.classList.add('mobile-open');
        if (backdrop) backdrop.classList.add('show');
    }

    function closeDrawer() {
        sidebar.classList.remove('mobile-open');
        if (backdrop) backdrop.classList.remove('show');
    }

    toggleBtns.forEach(function (btn) {
        btn.addEventListener('click', function () {
            if (sidebar.classList.contains('mobile-open')) {
                closeDrawer();
            } else {
                openDrawer();
            }
        });
    });

    if (backdrop) {
        backdrop.addEventListener('click', closeDrawer);
    }

    // Close the drawer after tapping a nav link, so it doesn't stay open
    // when the next page loads.
    sidebar.querySelectorAll('a.nav-link, form button[type="submit"]').forEach(function (el) {
        el.addEventListener('click', closeDrawer);
    });
});
