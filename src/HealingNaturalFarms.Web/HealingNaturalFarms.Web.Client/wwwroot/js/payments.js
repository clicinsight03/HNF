// Thin wrappers around the three payment SDKs (Stripe.js, PayPal SDK,
// Razorpay Checkout.js), called from Checkout.razor via JS interop.
// Stripe.js and Razorpay's checkout.js are loaded statically in
// Components/App.razor (both are safe to load unconditionally - they do
// nothing until invoked). PayPal's SDK needs a client-id + currency at
// script-load time, which depends on the region the visitor picked, so
// it's loaded dynamically here instead of statically in the host page.
window.hnfPayments = {
    _stripe: null,
    _elements: null,
    _paypalLoadedFor: null,

    // ---- Stripe --------------------------------------------------------
    initStripe: function (publishableKey, clientSecret, containerId) {
        this._stripe = Stripe(publishableKey);
        this._elements = this._stripe.elements({ clientSecret });
        const paymentElement = this._elements.create('payment');
        paymentElement.mount('#' + containerId);
    },

    confirmStripePayment: async function () {
        if (!this._stripe || !this._elements) {
            return { success: false, message: 'Stripe was not initialized.' };
        }
        const result = await this._stripe.confirmPayment({
            elements: this._elements,
            redirect: 'if_required'
        });
        if (result.error) {
            return { success: false, message: result.error.message };
        }
        return { success: true, paymentIntentId: result.paymentIntent.id };
    },

    // ---- PayPal ----------------------------------------------------------
    loadAndRenderPayPalButtons: function (clientId, currency, containerId, orderId, dotNetRef) {
        const key = clientId + '|' + currency;
        const render = () => {
            document.getElementById(containerId).innerHTML = '';
            paypal.Buttons({
                createOrder: function () { return orderId; },
                onApprove: function (data) {
                    return dotNetRef.invokeMethodAsync('OnPayPalApproved', data.orderID);
                },
                onCancel: function () {
                    dotNetRef.invokeMethodAsync('OnPaymentCancelled');
                },
                onError: function (err) {
                    dotNetRef.invokeMethodAsync('OnPaymentError', err ? err.toString() : 'PayPal error');
                }
            }).render('#' + containerId);
        };

        if (this._paypalLoadedFor === key && window.paypal) {
            render();
            return;
        }

        const existing = document.getElementById('hnf-paypal-sdk');
        if (existing) existing.remove();

        const script = document.createElement('script');
        script.id = 'hnf-paypal-sdk';
        script.src = `https://www.paypal.com/sdk/js?client-id=${encodeURIComponent(clientId)}&currency=${encodeURIComponent(currency)}`;
        script.onload = () => {
            this._paypalLoadedFor = key;
            render();
        };
        script.onerror = () => dotNetRef.invokeMethodAsync('OnPaymentError', 'Failed to load PayPal SDK.');
        document.body.appendChild(script);
    },

    // ---- Razorpay ----------------------------------------------------
    openRazorpayCheckout: function (keyId, orderId, amountInPaise, currency, name, description, dotNetRef) {
        if (!window.Razorpay) {
            dotNetRef.invokeMethodAsync('OnPaymentError', 'Razorpay Checkout script did not load.');
            return;
        }

        const rzp = new window.Razorpay({
            key: keyId,
            amount: amountInPaise,
            currency: currency,
            order_id: orderId,
            name: name,
            description: description,
            handler: function (response) {
                dotNetRef.invokeMethodAsync('OnRazorpaySuccess', response.razorpay_payment_id, response.razorpay_order_id, response.razorpay_signature);
            },
            modal: {
                ondismiss: function () {
                    dotNetRef.invokeMethodAsync('OnPaymentCancelled');
                }
            }
        });
        rzp.on('payment.failed', function (response) {
            dotNetRef.invokeMethodAsync('OnPaymentError', response.error ? response.error.description : 'Payment failed.');
        });
        rzp.open();
    }
};
