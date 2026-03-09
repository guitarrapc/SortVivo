/**
 * Popover helper for managing click-outside and escape key events
 */
window.popoverHelper = {
    _handlers: new Map(),

    /**
     * Register click-outside and escape handlers for a popover
     * @param {string} containerId - Container element ID
     * @param {DotNetObjectReference} dotNetRef - .NET object reference
     */
    register: function(containerId, dotNetRef) {
        if (this._handlers.has(containerId)) {
            this.unregister(containerId);
        }

        const clickHandler = (e) => {
            const container = document.getElementById(containerId);
            if (container && !container.contains(e.target)) {
                dotNetRef.invokeMethodAsync('OnClickOutside');
            }
        };

        const keyHandler = (e) => {
            if (e.key === 'Escape') {
                dotNetRef.invokeMethodAsync('OnEscapeKey');
            }
        };

        document.addEventListener('click', clickHandler, true);
        document.addEventListener('keydown', keyHandler);

        this._handlers.set(containerId, { clickHandler, keyHandler });
    },

    /**
     * Unregister handlers for a popover
     * @param {string} containerId - Container element ID
     */
    unregister: function(containerId) {
        const handlers = this._handlers.get(containerId);
        if (handlers) {
            document.removeEventListener('click', handlers.clickHandler, true);
            document.removeEventListener('keydown', handlers.keyHandler);
            this._handlers.delete(containerId);
        }
    }
};
