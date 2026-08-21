self.addEventListener('push', function (event) {
    let payload = {
        title: 'TCMIT Canteen',
        message: 'You have a new update!',
        url: '/' // Fallback root URL if no specific page is sent
    };

    if (event.data) {
        try {
            payload = event.data.json();
        } catch (e) {
            payload.message = event.data.text();
        }
    }

    const options = {
        body: payload.message,
        icon: '/favicon.ico',
        badge: '/favicon.ico',
        vibrate: [200, 100, 200],
        data: {
            url: payload.url || '/'
        }
    };

    event.waitUntil(
        self.registration.showNotification(payload.title, options)
    );
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (clientList) {
            const targetUrl = event.notification.data.url;

            // If a tab is already open with the target page, switch focus to it
            for (let i = 0; i < clientList.length; i++) {
                let client = clientList[i];
                if (client.url.includes(targetUrl) && 'focus' in client) {
                    return client.focus();
                }
            }

            // Otherwise open a new window/tab
            if (clients.openWindow) {
                return clients.openWindow(targetUrl);
            }
        })
    );
});