// modal-scroll-lock.js
// This script provides functions to lock and unlock scrolling on the main window
// It is used when forms are opened in modals to prevent the scrollbars of the main window from showing
window.modalScrollLock = {
    lock: function () {
        document.body.style.overflow = 'hidden';
    },
    unlock: function () {
        document.body.style.overflow = '';
    }
};
