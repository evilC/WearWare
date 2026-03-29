/*
Polls temperature from the TempMonService every few seconds, and invokes a callback in Blazor to update the UI.
This is needed because Blazor Server doesn't have a good way to do background tasks that update the UI,
and we want to avoid using a timer in C# that would keep the TempMonService alive indefinitely
(We do not want to continually poll the temperature when the user is not viewing the temperature)
*/
window.tempPoller = (function() {
    const timers = new Map();
    let nextId = 1;

    return {
        start: function(dotNetRef, intervalMs) {
            const id = nextId++;
            function tick() {
                try {
                    dotNetRef.invokeMethodAsync('Poll').catch(()=>{});
                } catch (e) { }
            }
            // call immediately, then on interval
            tick();
            const handle = setInterval(tick, intervalMs);
            timers.set(id, handle);
            return id;
        },
        stop: function(id) {
            const h = timers.get(id);
            if (h) {
                clearInterval(h);
                timers.delete(id);
            }
        }
    };
})();
