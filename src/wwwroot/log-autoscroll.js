// log-autoscroll.js
window.logAutoscroll = function(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};
