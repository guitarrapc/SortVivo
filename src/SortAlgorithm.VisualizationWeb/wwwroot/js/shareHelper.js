/**
 * Share helper for clipboard and social media sharing
 */
window.shareHelper = {
    /**
     * Copy text to clipboard
     * @param {string} text - Text to copy
     * @returns {Promise<boolean>} - Success status
     */
    copyToClipboard: async function(text) {
        try {
            // Try modern Clipboard API first
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
                return true;
            }
            
            // Fallback for older browsers
            return this._fallbackCopyToClipboard(text);
        } catch (err) {
            console.error('Failed to copy to clipboard:', err);
            return false;
        }
    },

    /**
     * Fallback method for copying to clipboard (for older browsers)
     * @param {string} text - Text to copy
     * @returns {boolean} - Success status
     */
    _fallbackCopyToClipboard: function(text) {
        try {
            const textArea = document.createElement('textarea');
            textArea.value = text;
            textArea.style.position = 'fixed';
            textArea.style.left = '-9999px';
            textArea.style.top = '-9999px';
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();

            const successful = document.execCommand('copy');
            document.body.removeChild(textArea);
            return successful;
        } catch (err) {
            console.error('Fallback copy failed:', err);
            return false;
        }
    },

    /**
     * Open share window (centered popup)
     * @param {string} url - URL to open
     * @param {string} title - Window title
     * @param {number} width - Window width (default: 600)
     * @param {number} height - Window height (default: 400)
     * @returns {Window|null} - Opened window or null
     */
    openShareWindow: function(url, title = 'Share', width = 600, height = 400) {
        try {
            // Calculate center position
            const left = (window.screen.width - width) / 2;
            const top = (window.screen.height - height) / 2;
            
            const features = `width=${width},height=${height},left=${left},top=${top},toolbar=0,menubar=0,location=0,status=0,scrollbars=1,resizable=1`;
            
            return window.open(url, title, features);
        } catch (err) {
            console.error('Failed to open share window:', err);
            return null;
        }
    }
};
