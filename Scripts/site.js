// Seonyx Holdings - Site JavaScript

(function () {
    'use strict';

    // Mobile navigation toggle
    var toggle = document.querySelector('.navbar-toggle');
    var menu = document.querySelector('.nav-menu');

    if (toggle && menu) {
        toggle.addEventListener('click', function () {
            menu.classList.toggle('active');
        });

        // Close menu when clicking outside
        document.addEventListener('click', function (e) {
            if (!toggle.contains(e.target) && !menu.contains(e.target)) {
                menu.classList.remove('active');
            }
        });
    }

    // Mobile dropdown toggle (touch devices)
    var dropdowns = document.querySelectorAll('.dropdown');
    for (var i = 0; i < dropdowns.length; i++) {
        var dropdownToggle = dropdowns[i].querySelector('.dropdown-toggle');
        if (dropdownToggle) {
            dropdownToggle.addEventListener('click', function (e) {
                if (window.innerWidth <= 768) {
                    e.preventDefault();
                    this.parentElement.querySelector('.dropdown-menu').classList.toggle('active');
                }
            });
        }
    }

    // Auto-generate slug from title in admin forms
    var titleInput = document.querySelector('input[name="Title"]');
    var slugInput = document.querySelector('input[name="Slug"]');

    if (titleInput && slugInput && !slugInput.value) {
        titleInput.addEventListener('input', function () {
            var slug = this.value
                .toLowerCase()
                .replace(/[^a-z0-9\s-]/g, '')
                .replace(/\s+/g, '-')
                .replace(/-+/g, '-')
                .replace(/^-|-$/g, '');
            slugInput.value = slug;
        });
    }
})();
