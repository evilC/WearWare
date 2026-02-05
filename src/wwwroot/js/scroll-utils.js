window.mw = window.mw || {};
window.mw.scrollToBottom = (el) => {
    try {
        if (!el) return;
        el.scrollTop = el.scrollHeight;
    } catch (e) {
        // ignore
    }
};

// Backwards-compatibility: global alias used by older callers
if (typeof window.scrollToBottom === 'undefined') {
    window.scrollToBottom = function(el) {
        try { window.mw.scrollToBottom(el); } catch (e) { }
    };
}

// Backwards-compatibility: existing log scroller name
if (typeof window.logAutoscroll === 'undefined') {
    window.logAutoscroll = function(el) {
        try { window.mw.scrollToBottom(el); } catch (e) { }
    };
}
