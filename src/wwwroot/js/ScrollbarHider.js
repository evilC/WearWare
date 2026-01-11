// ScrollbarHider.js: Shared JS file - hides scrollbar of page behind forms
window.modalScrollLock = {
    lock: function () {
        document.body.style.overflow = 'hidden';
    },
    unlock: function () {
        document.body.style.overflow = '';
    }
};
