// enterToTab.js
// Global helper: convert Enter key presses into moving focus to the next control (Tab behavior)
// Preserves Enter when a submit control has focus. Safe to include site-wide.
(function(){
    console.log('[enterToTab] loaded');
    var composing = false;
    document.addEventListener('compositionstart', function(){ composing = true; }, true);
    document.addEventListener('compositionend', function(){ composing = false; }, true);

    document.addEventListener('keydown', function(e){
        try{
            if (e.key !== 'Enter' && e.keyCode !== 13) return;
            if (composing) return;
            if (e.ctrlKey || e.altKey || e.metaKey) return; // allow shortcuts

            var t = e.target || e.srcElement;
            if (!t) return;
            var tag = (t.tagName || '').toLowerCase();
            if (tag === 'textarea') return; // allow Enter in textareas
            if (t.isContentEditable) return;

            // If a submit control currently has focus, let Enter act as submit
            try{
                if (t.matches && (t.matches('button[type=submit]') || t.matches('input[type=submit]'))) {
                    return;
                }
            }catch(_){}

            // build focusable list scoped to the same form if present, otherwise document
            var scope = t.form || document;
            var nodes = Array.from(scope.querySelectorAll('input,select,textarea,button,a[href],[tabindex]'));

            // filter visible and focusable
            nodes = nodes.filter(function(el){
                try{
                    if (el.disabled) return false;
                    if (el.getAttribute && el.getAttribute('type') === 'hidden') return false;
                    if (el.tabIndex === -1) return false;
                    var cs = window.getComputedStyle(el);
                    if (cs && (cs.display === 'none' || cs.visibility === 'hidden')) return false;
                    return true;
                }catch(err){ return false; }
            });

            var idx = nodes.indexOf(t);
            var next = (idx >= 0 && idx < nodes.length-1) ? nodes[idx+1] : null;
            if (!next){
                for (var i=0;i<nodes.length;i++){
                    try{
                        if (nodes[i] && nodes[i] !== t && (nodes[i].compareDocumentPosition(t) & Node.DOCUMENT_POSITION_FOLLOWING)){
                            next = nodes[i]; break;
                        }
                    }catch(err){}
                }
            }

            if (next){
                e.preventDefault();
                try{ next.focus(); }catch(err){}
                console.log('[enterToTab] moved focus from', t, 'to', next);
            }
        }catch(err){ console.warn('[enterToTab]', err); }
    }, true);
})();
