window.mw = window.mw || {};
window.mw.scrollToBottom = (el) => {
    try {
        if (!el) return;
        el.scrollTop = el.scrollHeight;
    } catch (e) {
        // ignore
    }
};

window.mw.scrollToAttribute = (attrName, attrValue) => {
    try {
        if (!attrName || !attrValue) return;
        const esc = (window.CSS && CSS.escape) ? CSS.escape(attrValue) : attrValue.replace(/["\\]/g, "\\$&");
        const el = document.querySelector(`[${attrName}="${esc}"]`);
        if (!el) return;
        el.scrollIntoView({ block: "nearest" });
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
